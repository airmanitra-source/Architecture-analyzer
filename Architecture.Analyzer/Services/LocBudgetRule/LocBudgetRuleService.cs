using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Architecture.Analyzer.Models;
using LibGit2Sharp;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Architecture.Analyzer.Services.LocBudgetRule;

internal sealed class LocBudgetRuleService : ILocBudgetRuleService
{
    private const string AnalyzerConfigPrefix = "architecture_analyzer.";
    private const string ProjectPercentOption = AnalyzerConfigPrefix + "loc_budget_percent_project";
    private const string GlobalPercentOption = AnalyzerConfigPrefix + "loc_budget_percent_global";
    private const string MaxClassLinesOption = AnalyzerConfigPrefix + "loc_max_lines_per_class";
    private const string MaxMethodLinesOption = AnalyzerConfigPrefix + "loc_max_lines_per_method";
    private const int DefaultMaxClassLines = 20;
    private const int DefaultMaxMethodLines = 20;
    private const string CsExtension = ".cs";

    // When LibGit2Sharp ships as an analyzer dependency there is no deps.json to map its native
    // binary, so the default resolver never probes runtimes/<rid>/native. We point LibGit2Sharp at
    // the correct native folder (next to its managed assembly) once, before the first native call.
    private static readonly bool NativeLibraryConfigured = TryConfigureNativeLibraryPath();

    public (int? MaxClassLines, int? MaxMethodLines) GetLineLimits(AnalyzerConfigOptions options)
    {
        var maxClassLines = GetPositiveIntOption(options, MaxClassLinesOption) ?? DefaultMaxClassLines;
        var maxMethodLines = GetPositiveIntOption(options, MaxMethodLinesOption) ?? DefaultMaxMethodLines;
        return (maxClassLines, maxMethodLines);
    }

    public LocBudgetSettings GetSettings(
        Compilation compilation,
        AnalyzerConfigOptions options,
        global::System.Threading.CancellationToken cancellationToken)
    {
        var (maxClassLines, maxMethodLines) = GetLineLimits(options);
        var projectPercent = GetPositiveIntOption(options, ProjectPercentOption);
        var globalPercent = GetPositiveIntOption(options, GlobalPercentOption);

        if (!projectPercent.HasValue && !globalPercent.HasValue)
        {
            return new LocBudgetSettings(maxClassLines, maxMethodLines, null, null);
        }

        var repositoryPath = GetRepositoryPath(compilation);
        if (repositoryPath is null)
        {
            return new LocBudgetSettings(maxClassLines, maxMethodLines, null, null);
        }

        try
        {
            using var repository = new Repository(repositoryPath);
            int? projectBudget = projectPercent.HasValue
                ? CalculateBudget(CountHeadProjectLines(compilation, repository, cancellationToken), projectPercent.Value)
                : null;
            int? globalBudget = globalPercent.HasValue
                ? CalculateBudget(CountHeadSolutionLines(repository, cancellationToken), globalPercent.Value)
                : null;

            return new LocBudgetSettings(
                maxClassLines,
                maxMethodLines,
                projectBudget > 0 ? projectBudget : null,
                globalBudget > 0 ? globalBudget : null);
        }
        catch (Exception exception) when (IsGitFailure(exception))
        {
            return new LocBudgetSettings(maxClassLines, maxMethodLines, null, null);
        }
    }

    public LocBudgetViolation? AnalyzeType(TypeDeclarationSyntax declaration, int maxLines, global::System.Threading.CancellationToken cancellationToken)
        => AnalyzeCurrentDeclaration(declaration.SyntaxTree, declaration.Span, declaration.Identifier.ValueText, "classe", maxLines, cancellationToken, declaration.Identifier.GetLocation());

    public LocBudgetViolation? AnalyzeMethod(MethodDeclarationSyntax declaration, int maxLines, global::System.Threading.CancellationToken cancellationToken)
        => AnalyzeCurrentDeclaration(declaration.SyntaxTree, declaration.Span, declaration.Identifier.ValueText, "méthode", maxLines, cancellationToken, declaration.Identifier.GetLocation());

    public LocBudgetViolation? AnalyzeProject(Compilation compilation, int budget, global::System.Threading.CancellationToken cancellationToken)
    {
        var repositoryPath = GetRepositoryPath(compilation);
        if (repositoryPath is null)
        {
            return null;
        }

        try
        {
            using var repository = new Repository(repositoryPath);
            var addedLines = CountChangedProjectLines(compilation, repository, cancellationToken);
            if (addedLines <= budget)
            {
                return null;
            }

            var location = GetCompilationLocation(compilation, cancellationToken);
            if (location is null)
            {
                return null;
            }

            return new LocBudgetViolation("projet", compilation.AssemblyName ?? "Projet", addedLines, budget, location);
        }
        catch (Exception exception) when (IsGitFailure(exception))
        {
            return null;
        }
    }

    public LocBudgetViolation? AnalyzeGlobal(Compilation compilation, int budget, global::System.Threading.CancellationToken cancellationToken)
    {
        var repositoryPath = GetRepositoryPath(compilation);
        if (repositoryPath is null)
        {
            return null;
        }

        try
        {
            using var repository = new Repository(repositoryPath);
            var patch = GetPatch(repository);
            var addedLines = CountPatchAddedLines(patch);
            if (addedLines <= budget)
            {
                return null;
            }

            var location = GetCompilationLocation(compilation, cancellationToken);
            if (location is null)
            {
                return null;
            }

            return new LocBudgetViolation("solution", compilation.AssemblyName ?? "Solution", addedLines, budget, location);
        }
        catch (Exception exception) when (IsGitFailure(exception))
        {
            return null;
        }
    }

    private static LocBudgetViolation? AnalyzeCurrentDeclaration(
        SyntaxTree syntaxTree,
        TextSpan span,
        string itemName,
        string scopeName,
        int maxLines,
        global::System.Threading.CancellationToken cancellationToken,
        Location location)
    {
        var currentText = syntaxTree.GetText(cancellationToken);
        var startLine = GetStartLine(currentText, span);
        var endLine = GetEndLine(currentText, span);
        var currentLines = CountNonBlankLines(currentText, startLine, endLine);
        if (currentLines <= maxLines)
        {
            return null;
        }

        return new LocBudgetViolation(scopeName, itemName, currentLines, maxLines, location);
    }

    private static string? GetRepositoryPath(Compilation compilation)
    {
        foreach (var tree in compilation.SyntaxTrees)
        {
            var repositoryPath = GetRepositoryPath(tree.FilePath);
            if (repositoryPath is not null)
            {
                return repositoryPath;
            }
        }

        return null;
    }

    private static string? GetRepositoryPath(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return null;
        }

        var directory = Path.GetDirectoryName(filePath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return null;
        }

        try
        {
            // Force the native library path configuration before the first native call.
            _ = NativeLibraryConfigured;
            return Repository.Discover(directory);
        }
        catch (Exception exception) when (IsGitFailure(exception))
        {
            return null;
        }
    }

    private static int CountHeadProjectLines(Compilation compilation, Repository repository, global::System.Threading.CancellationToken cancellationToken)
    {
        var total = 0;
        foreach (var tree in compilation.SyntaxTrees)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var filePath = tree.FilePath;
            if (string.IsNullOrWhiteSpace(filePath))
            {
                continue;
            }

            var baselineText = TryGetHeadText(repository, filePath);
            if (baselineText is not null)
            {
                total += CountNonBlankLines(baselineText);
            }
        }

        return total;
    }

    private static int CountHeadSolutionLines(Repository repository, global::System.Threading.CancellationToken cancellationToken)
    {
        var tip = repository.Head.Tip;
        if (tip is null)
        {
            return 0;
        }

        var total = 0;
        foreach (var blobText in EnumerateHeadCsFileTexts(tip.Tree, cancellationToken))
        {
            total += CountNonBlankLines(blobText);
        }

        return total;
    }

    private static int CountChangedProjectLines(Compilation compilation, Repository repository, global::System.Threading.CancellationToken cancellationToken)
    {
        var total = 0;
        foreach (var tree in compilation.SyntaxTrees)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var currentText = tree.GetText(cancellationToken);
            var baselineText = TryGetHeadText(repository, tree.FilePath);
            if (baselineText is null)
            {
                total += CountNonBlankLines(currentText);
                continue;
            }

            if (!HasFileChanged(currentText, baselineText))
            {
                continue;
            }

            total += Math.Max(0, CountNonBlankLines(currentText) - CountNonBlankLines(baselineText));
        }

        return total;
    }

    private static Patch GetPatch(Repository repository)
        => repository.Diff.Compare<Patch>(repository.Head.Tip?.Tree, DiffTargets.Index | DiffTargets.WorkingDirectory);

    private static int CountPatchAddedLines(Patch patch)
    {
        var total = 0;
        foreach (var entry in patch)
        {
            if (!entry.Path.EndsWith(CsExtension, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            total += entry.LinesAdded;
        }

        return total;
    }

    private static IEnumerable<SourceText> EnumerateHeadCsFileTexts(Tree tree, global::System.Threading.CancellationToken cancellationToken)
    {
        foreach (var entry in tree)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (entry.TargetType == TreeEntryTargetType.Tree)
            {
                foreach (var nested in EnumerateHeadCsFileTexts((Tree)entry.Target!, cancellationToken))
                {
                    yield return nested;
                }

                continue;
            }

            if (entry.TargetType == TreeEntryTargetType.Blob && entry.Name.EndsWith(CsExtension, StringComparison.OrdinalIgnoreCase))
            {
                var blob = (Blob)entry.Target!;
                var content = blob.GetContentText();
                if (content is not null)
                {
                    yield return SourceText.From(content);
                }
            }
        }
    }

    private static SourceText? TryGetHeadText(Repository repository, string filePath)
    {
        var relativePath = GetRelativePath(repository, filePath);
        if (relativePath is null)
        {
            return null;
        }

        var entry = repository.Head.Tip?[relativePath.Replace(Path.DirectorySeparatorChar, '/')];
        if (entry?.TargetType != TreeEntryTargetType.Blob)
        {
            return null;
        }

        var content = ((Blob)entry.Target!).GetContentText();
        return content is null ? null : SourceText.From(content);
    }

    private static string? GetRelativePath(Repository repository, string filePath)
    {
        var workingDirectory = repository.Info.WorkingDirectory;
        if (string.IsNullOrWhiteSpace(workingDirectory))
        {
            return null;
        }

        var baseUri = new Uri(AppendDirectorySeparatorChar(workingDirectory));
        var fileUri = new Uri(filePath);
        var relativeUri = baseUri.MakeRelativeUri(fileUri);
        return Uri.UnescapeDataString(relativeUri.ToString()).Replace('/', Path.DirectorySeparatorChar);
    }

    private static string AppendDirectorySeparatorChar(string path)
        => path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal) ? path : path + Path.DirectorySeparatorChar;

    private static bool HasFileChanged(SourceText currentText, SourceText baselineText)
        => !string.Equals(currentText.ToString(), baselineText.ToString(), StringComparison.Ordinal);

    private static Location? GetCompilationLocation(Compilation compilation, global::System.Threading.CancellationToken cancellationToken)
    {
        var syntaxTree = compilation.SyntaxTrees.FirstOrDefault();
        if (syntaxTree is null)
        {
            return null;
        }

        return syntaxTree.GetRoot(cancellationToken).GetLocation();
    }

    private static int GetStartLine(SourceText text, TextSpan span)
        => text.Lines.GetLineFromPosition(span.Start).LineNumber;

    private static int GetEndLine(SourceText text, TextSpan span)
    {
        var endPosition = Math.Max(span.Start, span.End - 1);
        return text.Lines.GetLineFromPosition(endPosition).LineNumber;
    }

    private static int CountNonBlankLines(SourceText text)
        => CountNonBlankLines(text, 0, Math.Max(0, text.Lines.Count - 1));

    private static int CountNonBlankLines(SourceText text, int startLine, int endLine)
    {
        if (text.Lines.Count == 0)
        {
            return 0;
        }

        var clampedStart = Math.Max(0, Math.Min(startLine, text.Lines.Count - 1));
        var clampedEnd = Math.Max(0, Math.Min(endLine, text.Lines.Count - 1));
        if (clampedEnd < clampedStart)
        {
            return 0;
        }

        var count = 0;
        for (var index = clampedStart; index <= clampedEnd; index++)
        {
            var line = text.Lines[index];
            if (!string.IsNullOrWhiteSpace(text.ToString(line.Span)))
            {
                count++;
            }
        }

        return count;
    }

    private static int CalculateBudget(int baselineLoc, int percent)
    {
        if (baselineLoc <= 0 || percent <= 0)
        {
            return 0;
        }

        var budget = (int)Math.Ceiling(baselineLoc * percent / 100.0);
        return Math.Max(1, budget);
    }

    private static bool TryConfigureNativeLibraryPath()
    {
        try
        {
            var assemblyLocation = typeof(Repository).Assembly.Location;
            if (string.IsNullOrWhiteSpace(assemblyLocation))
            {
                return false;
            }

            var assemblyDirectory = Path.GetDirectoryName(assemblyLocation);
            if (string.IsNullOrWhiteSpace(assemblyDirectory))
            {
                return false;
            }

            var runtimeIdentifier = GetRuntimeIdentifier();
            if (runtimeIdentifier is null)
            {
                return false;
            }

            // The directory may not exist for every RID we ship; if it is missing or wrong the
            // native load simply fails and is handled as a git failure (budgets degrade to null).
            GlobalSettings.NativeLibraryPath = Path.Combine(assemblyDirectory, "runtimes", runtimeIdentifier, "native");

            // The analyzer only reads git history; skip git's safe.directory ownership check so a
            // repository owned by another user (common on CI or shared machines) can still be opened.
            GlobalSettings.SetOwnerValidation(false);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string? GetRuntimeIdentifier()
    {
        string? os;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            os = "win";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            os = "linux";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            os = "osx";
        }
        else
        {
            return null;
        }

        var architecture = RuntimeInformation.ProcessArchitecture switch
        {
            System.Runtime.InteropServices.Architecture.X64 => "x64",
            System.Runtime.InteropServices.Architecture.X86 => "x86",
            System.Runtime.InteropServices.Architecture.Arm64 => "arm64",
            System.Runtime.InteropServices.Architecture.Arm => "arm",
            _ => null
        };

        return architecture is null ? null : $"{os}-{architecture}";
    }

    private static bool IsGitFailure(Exception exception)
        => exception is not OperationCanceledException
           && exception is FileNotFoundException
               or FileLoadException
               or BadImageFormatException
               or DllNotFoundException
               or TypeInitializationException
               or LibGit2SharpException;

    private static int? GetPositiveIntOption(AnalyzerConfigOptions options, string key)
        => TryGetPositiveIntOption(options, key, out var value) ? value : null;

    private static bool TryGetPositiveIntOption(AnalyzerConfigOptions options, string key, out int value)
    {
        value = 0;
        if (!options.TryGetValue(key, out var configuredValue) || string.IsNullOrWhiteSpace(configuredValue))
        {
            return false;
        }

        return int.TryParse(configuredValue.Trim(), out value) && value > 0;
    }
}