using System.Collections.Immutable;
using Architecture.Analyzer;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Architecture.Analyzer.Tests;

public sealed class ConventionAnalyzerTests
{
    private const string ChangedMetadata = "build_metadata.AdditionalFiles.ArchitectureChangedFiles";
    private const string MinSupport = "architecture_analyzer.convention_min_support";
    private const string MinRatio = "architecture_analyzer.convention_min_ratio";
    private const string Ignore = "architecture_analyzer.convention_ignore";
    private const string ModelSuffix = "architecture_analyzer.model_suffix";

    // Four providers in Providers/, and a fifth one that landed elsewhere.
    private static readonly (string Path, string Source)[] FolderDeviation =
    [
        ("Providers/AlphaProvider.cs", "namespace N.Providers; public class AlphaProvider { }"),
        ("Providers/BetaProvider.cs", "namespace N.Providers; public class BetaProvider { }"),
        ("Providers/GammaProvider.cs", "namespace N.Providers; public class GammaProvider { }"),
        ("Providers/DeltaProvider.cs", "namespace N.Providers; public class DeltaProvider { }"),
        ("Services/SmtpProvider.cs", "namespace N.Providers; public class SmtpProvider { }"),
    ];

    private static Dictionary<string, string> Config(params (string Key, string Value)[] entries)
    {
        // 4 of 5 examples agree in the fixtures below (80%), so the ratio is lowered under that.
        var config = new Dictionary<string, string> { [MinSupport] = "3", [MinRatio] = "75" };
        foreach (var (key, value) in entries)
        {
            config[key] = value;
        }

        return config;
    }

    [Fact]
    public async Task ReportsATypeThatDeviatesFromTheObservedFolder()
    {
        var diagnostics = await AnalyzeAsync(FolderDeviation, changed: ["Services/SmtpProvider.cs"], Config());

        var diagnostic = Assert.Single(diagnostics, d => d.Id == ConventionAnalyzer.DiagnosticId);
        Assert.Contains("SmtpProvider", diagnostic.GetMessage());
    }

    [Fact]
    public async Task TheMessageShowsItsEvidence()
    {
        var diagnostics = await AnalyzeAsync(FolderDeviation, changed: ["Services/SmtpProvider.cs"], Config());

        var message = Assert.Single(diagnostics, d => d.Id == ConventionAnalyzer.DiagnosticId).GetMessage();
        Assert.Contains("5", message);          // total *Provider types
        Assert.Contains("4", message);          // how many share the folder
        Assert.Contains("Provider", message);   // the suffix
        Assert.Contains("folder", message);     // the trait
    }

    [Fact]
    public async Task DoesNotReportADeviationInAFileThatDidNotChange()
    {
        // The deviant exists, but this change did not touch it: legacy is never reported.
        var diagnostics = await AnalyzeAsync(FolderDeviation, changed: ["Providers/AlphaProvider.cs"], Config());

        Assert.DoesNotContain(diagnostics, d => d.Id == ConventionAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task DoesNotReportAnythingWithoutAChangedFilesTable()
    {
        var diagnostics = await AnalyzeAsync(FolderDeviation, changed: null, Config());

        Assert.DoesNotContain(diagnostics, d => d.Id == ConventionAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task DoesNotInferAConventionBelowTheMinimumSupport()
    {
        var diagnostics = await AnalyzeAsync(FolderDeviation, changed: ["Services/SmtpProvider.cs"], Config((MinSupport, "10")));

        Assert.DoesNotContain(diagnostics, d => d.Id == ConventionAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task LeavesTheFolderToArch001WhenTheSuffixIsConfiguredThere()
    {
        // The explicit rule wins: no folder convention is inferred for a suffix ARCH001 governs.
        var diagnostics = await AnalyzeAsync(FolderDeviation, changed: ["Services/SmtpProvider.cs"], Config((ModelSuffix, "Provider")));

        Assert.DoesNotContain(diagnostics, d => d.Id == ConventionAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task TheIgnoreKeySilencesOneConvention()
    {
        var diagnostics = await AnalyzeAsync(FolderDeviation, changed: ["Services/SmtpProvider.cs"], Config((Ignore, "Provider>folder")));

        Assert.DoesNotContain(diagnostics, d => d.Id == ConventionAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task ReportsAKindDeviation()
    {
        // Every *Model is a record, except the new one.
        (string Path, string Source)[] documents =
        [
            ("M/AModel.cs", "namespace N; public record AModel;"),
            ("M/BModel.cs", "namespace N; public record BModel;"),
            ("M/CModel.cs", "namespace N; public record CModel;"),
            ("M/DModel.cs", "namespace N; public record DModel;"),
            ("M/EModel.cs", "namespace N; public class EModel { }"),
        ];

        var diagnostics = await AnalyzeAsync(documents, changed: ["M/EModel.cs"], Config());

        var message = Assert.Single(diagnostics, d => d.Id == ConventionAnalyzer.DiagnosticId).GetMessage();
        Assert.Contains("kind", message);
        Assert.Contains("record", message);
    }

    [Fact]
    public async Task DoesNotReportATypeThatFollowsTheConvention()
    {
        var diagnostics = await AnalyzeAsync(FolderDeviation, changed: ["Providers/AlphaProvider.cs"], Config());

        Assert.DoesNotContain(diagnostics, d => d.Id == ConventionAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task IsReportedAsAWarning()
    {
        var diagnostics = await AnalyzeAsync(FolderDeviation, changed: ["Services/SmtpProvider.cs"], Config());

        var diagnostic = Assert.Single(diagnostics, d => d.Id == ConventionAnalyzer.DiagnosticId);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
    }

    private static async Task<List<Diagnostic>> AnalyzeAsync(
        IReadOnlyList<(string Path, string Source)> documents,
        string[]? changed,
        Dictionary<string, string> config)
    {
        using var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();

        var solution = workspace.CurrentSolution
            .AddProject(ProjectInfo.Create(projectId, VersionStamp.Default, "N", "N", LanguageNames.CSharp))
            .WithProjectCompilationOptions(projectId, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .AddMetadataReference(projectId, MetadataReference.CreateFromFile(typeof(object).Assembly.Location));

        foreach (var (path, source) in documents)
        {
            solution = solution.AddDocument(DocumentId.CreateNewId(projectId), path, SourceText.From(source), filePath: path);
        }

        workspace.TryApplyChanges(solution);
        var compilation = await workspace.CurrentSolution.GetProject(projectId)!.GetCompilationAsync();

        var files = new List<AdditionalText>();
        if (changed is not null)
        {
            var table = new InMemoryText("changed-files.txt", string.Join(Environment.NewLine, changed));
            table.Metadata[ChangedMetadata] = "true";
            files.Add(table);
        }

        var options = new AnalyzerOptions(files.ToImmutableArray(), new ConfigProvider(config));
        var diagnostics = await compilation!
            .WithAnalyzers(
                ImmutableArray.Create<DiagnosticAnalyzer>(new ConventionAnalyzer()),
                new CompilationWithAnalyzersOptions(options, null, true, false))
            .GetAnalyzerDiagnosticsAsync();

        return diagnostics.ToList();
    }

    private sealed class InMemoryText(string path, string text) : AdditionalText
    {
        public Dictionary<string, string> Metadata { get; } = new();

        public override string Path => path;

        public override SourceText GetText(CancellationToken cancellationToken = default) => SourceText.From(text);
    }

    private sealed class ConfigProvider(IReadOnlyDictionary<string, string> values) : AnalyzerConfigOptionsProvider
    {
        public override AnalyzerConfigOptions GlobalOptions => new Options(values);

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => new Options(values);

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile)
        {
            var merged = new Dictionary<string, string>(values);
            if (textFile is InMemoryText inMemory)
            {
                foreach (var entry in inMemory.Metadata)
                {
                    merged[entry.Key] = entry.Value;
                }
            }

            return new Options(merged);
        }
    }

    private sealed class Options(IReadOnlyDictionary<string, string> values) : AnalyzerConfigOptions
    {
        public override bool TryGetValue(string key, out string value) => values.TryGetValue(key, out value!);
    }
}
