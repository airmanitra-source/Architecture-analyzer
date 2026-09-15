using System.Collections.Generic;
using System.Collections.Immutable;
using Architecture.Analyzer;
using Architecture.Analyzer.Build;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Architecture.Analyzer.Tests;

public sealed class OscillationAnalyzerTests
{
    private const string RemovedMetadata = "build_metadata.AdditionalFiles.ArchitectureOscillationRemoved";
    private const string AddedMetadata = "build_metadata.AdditionalFiles.ArchitectureOscillationAdded";

    private static readonly string[] Block =
    {
        "var first = Compute(a);",
        "var second = Compute(b);",
        "return first + second;",
    };

    // A tree whose lines 3..5 are the block; the analyzer only needs the line to exist for a location.
    private const string Tree = @"public class C
{
    var first = Compute(a);
    var second = Compute(b);
    return first + second;
}";

    private static string BlockHash()
    {
        var hashes = new List<ulong>();
        foreach (var line in Block)
        {
            hashes.Add(OscillationScan.HashLine(OscillationScan.NormalizeLine(line)));
        }

        return OscillationScan.CombineWindow(hashes, 0, 3);
    }

    [Fact]
    public async Task ReportsWhenAnAddedBlockMatchesARemovedOne()
    {
        var hash = BlockHash();
        var diagnostics = await AnalyzeAsync(
            removed: new[] { hash + "|abc1234|hurried cleanup|2" },
            added: new[] { hash + "|C.cs|3" });

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(OscillationAnalyzer.DiagnosticId, diagnostic.Id);
        var message = diagnostic.GetMessage();
        Assert.Contains("abc1234", message);
        Assert.Contains("hurried cleanup", message);
        Assert.Contains("2", message);
    }

    [Fact]
    public async Task DoesNotReportWhenNoRemovedMatch()
        => Assert.Empty(await AnalyzeAsync(
            removed: new[] { "0000000000000000|abc1234|other|1" },
            added: new[] { BlockHash() + "|C.cs|3" }));

    [Fact]
    public async Task StaysSilentWhenThereIsNoRemovedTable()
        => Assert.Empty(await AnalyzeAsync(removed: null, added: new[] { BlockHash() + "|C.cs|3" }));

    [Fact]
    public async Task StaysSilentWhenNothingWasAdded()
        => Assert.Empty(await AnalyzeAsync(removed: new[] { BlockHash() + "|abc1234|x|1" }, added: null));

    [Fact]
    public async Task TheDiagnosticIsAWarning()
    {
        var hash = BlockHash();
        var diagnostics = await AnalyzeAsync(
            removed: new[] { hash + "|abc1234|x|1" },
            added: new[] { hash + "|C.cs|3" });
        Assert.Equal(DiagnosticSeverity.Warning, Assert.Single(diagnostics).Severity);
    }

    [Fact]
    public async Task OverlappingWindowsOfOneBlockReportOnce()
    {
        var hash = BlockHash();
        // Two windows of the same re-added block, on adjacent start lines.
        var diagnostics = await AnalyzeAsync(
            removed: new[] { hash + "|abc1234|x|1" },
            added: new[] { hash + "|C.cs|3", hash + "|C.cs|4" });
        Assert.Single(diagnostics);
    }

    private static async Task<List<Diagnostic>> AnalyzeAsync(string[]? removed, string[]? added)
    {
        using var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var solution = workspace.CurrentSolution
            .AddProject(ProjectInfo.Create(projectId, VersionStamp.Default, "P", "P", LanguageNames.CSharp))
            .WithProjectCompilationOptions(projectId, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .AddMetadataReference(projectId, MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
            .AddDocument(DocumentId.CreateNewId(projectId), "C.cs", SourceText.From(Tree));

        workspace.TryApplyChanges(solution);
        var compilation = await workspace.CurrentSolution.GetProject(projectId)!.GetCompilationAsync();

        var files = new List<AdditionalText>();
        if (removed is not null)
        {
            var file = new InMemoryText("oscillation-removed.txt", string.Join(Environment.NewLine, removed));
            file.Metadata[RemovedMetadata] = "true";
            files.Add(file);
        }

        if (added is not null)
        {
            var file = new InMemoryText("oscillation-added.txt", string.Join(Environment.NewLine, added));
            file.Metadata[AddedMetadata] = "true";
            files.Add(file);
        }

        var options = new AnalyzerOptions(files.ToImmutableArray(), new ConfigProvider());

        var diagnostics = await compilation!
            .WithAnalyzers(
                ImmutableArray.Create<DiagnosticAnalyzer>(new OscillationAnalyzer()),
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

    private sealed class ConfigProvider : AnalyzerConfigOptionsProvider
    {
        public override AnalyzerConfigOptions GlobalOptions => new Options(new Dictionary<string, string>());

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => new Options(new Dictionary<string, string>());

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile)
        {
            var merged = new Dictionary<string, string>();
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
