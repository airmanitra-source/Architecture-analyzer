using System;
using System.Linq;
using Architecture.Analyzer.Models;
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

    // Fed by the git CLI at build time through CompilerVisibleProperty (see Architecture.Analyzer.targets).
    private const string ProjectAddedProperty = "build_property.ArchitectureLocProjectAddedLines";
    private const string ProjectBaselineProperty = "build_property.ArchitectureLocProjectBaselineLines";
    private const string SolutionAddedProperty = "build_property.ArchitectureLocSolutionAddedLines";
    private const string SolutionBaselineProperty = "build_property.ArchitectureLocSolutionBaselineLines";

    private const int DefaultMaxClassLines = 20;
    private const int DefaultMaxMethodLines = 20;

    public (int? MaxClassLines, int? MaxMethodLines) GetLineLimits(AnalyzerConfigOptions options)
    {
        var maxClassLines = GetPositiveIntOption(options, MaxClassLinesOption) ?? DefaultMaxClassLines;
        var maxMethodLines = GetPositiveIntOption(options, MaxMethodLinesOption) ?? DefaultMaxMethodLines;
        return (maxClassLines, maxMethodLines);
    }

    public LocBudgetSettings GetSettings(AnalyzerConfigOptions editorConfigOptions, AnalyzerConfigOptions buildPropertyOptions)
    {
        var (maxClassLines, maxMethodLines) = GetLineLimits(editorConfigOptions);
        var projectPercent = GetPositiveIntOption(editorConfigOptions, ProjectPercentOption);
        var globalPercent = GetPositiveIntOption(editorConfigOptions, GlobalPercentOption);

        var projectAdded = GetNonNegativeIntOption(buildPropertyOptions, ProjectAddedProperty);
        var projectBaseline = GetNonNegativeIntOption(buildPropertyOptions, ProjectBaselineProperty);
        var solutionAdded = GetNonNegativeIntOption(buildPropertyOptions, SolutionAddedProperty);
        var solutionBaseline = GetNonNegativeIntOption(buildPropertyOptions, SolutionBaselineProperty);

        int? projectBudget = projectPercent.HasValue
            ? NullIfNonPositive(CalculateBudget(projectBaseline, projectPercent.Value))
            : null;
        int? globalBudget = globalPercent.HasValue
            ? NullIfNonPositive(CalculateBudget(solutionBaseline, globalPercent.Value))
            : null;

        return new LocBudgetSettings(maxClassLines, maxMethodLines, projectBudget, globalBudget, projectAdded, solutionAdded);
    }

    public LocBudgetViolation? GetLocViolationsOnType(TypeDeclarationSyntax declaration, int maxLines, global::System.Threading.CancellationToken cancellationToken)
        => AnalyzeCurrentDeclaration(declaration.SyntaxTree, declaration.Span, declaration.Identifier.ValueText, "classe", maxLines, cancellationToken, declaration.Identifier.GetLocation());

    public LocBudgetViolation? GetLocViolationOnMethod(MethodDeclarationSyntax declaration, int maxLines, global::System.Threading.CancellationToken cancellationToken)
        => AnalyzeCurrentDeclaration(declaration.SyntaxTree, declaration.Span, declaration.Identifier.ValueText, "méthode", maxLines, cancellationToken, declaration.Identifier.GetLocation());

    public LocBudgetViolation? GetLocViolationOnProject(Compilation compilation, int addedLines, int budget, global::System.Threading.CancellationToken cancellationToken)
    {
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

    public LocBudgetViolation? GetLocViolationOnGlobal(Compilation compilation, int addedLines, int budget, global::System.Threading.CancellationToken cancellationToken)
    {
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

    private static int? NullIfNonPositive(int value) => value > 0 ? value : null;

    private static int? GetPositiveIntOption(AnalyzerConfigOptions options, string key)
        => TryGetIntOption(options, key, out var value) && value > 0 ? value : null;

    private static int GetNonNegativeIntOption(AnalyzerConfigOptions options, string key)
        => TryGetIntOption(options, key, out var value) && value > 0 ? value : 0;

    private static bool TryGetIntOption(AnalyzerConfigOptions options, string key, out int value)
    {
        value = 0;
        if (!options.TryGetValue(key, out var configuredValue) || string.IsNullOrWhiteSpace(configuredValue))
        {
            return false;
        }

        return int.TryParse(configuredValue.Trim(), out value);
    }
}
