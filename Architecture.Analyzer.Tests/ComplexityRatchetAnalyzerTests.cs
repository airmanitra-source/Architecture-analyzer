using System.Collections.Immutable;
using Architecture.Analyzer;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Architecture.Analyzer.Tests;

public sealed class ComplexityRatchetAnalyzerTests
{
    private const string BaselineMetadata = "build_metadata.AdditionalFiles.ArchitectureBaselineFor";
    private const string AllowedIncrease = "architecture_analyzer.complexity_ratchet_allowed_increase";
    private const string ChurnMetadata = "build_metadata.AdditionalFiles.ArchitectureChurnTable";

    // Complexity 1: a single return, no decision point.
    private const string Simple = @"
public class C
{
    public int Compute(int a)
    {
        return a;
    }
}";

    // Complexity 3: two ifs on top of the baseline path.
    private const string Complicated = @"
public class C
{
    public int Compute(int a)
    {
        if (a > 0)
        {
            return 1;
        }

        if (a < 0)
        {
            return 2;
        }

        return a;
    }
}";

    [Fact]
    public async Task ReportsWhenComplexityIncreases()
        => Assert.Equal(1, await CountAsync(current: Complicated, head: Simple));

    [Fact]
    public async Task DoesNotReportWhenComplexityDecreases()
        => Assert.Equal(0, await CountAsync(current: Simple, head: Complicated));

    [Fact]
    public async Task DoesNotReportWhenComplexityIsUnchanged()
        => Assert.Equal(0, await CountAsync(current: Complicated, head: Complicated));

    [Fact]
    public async Task DoesNotReportWhenThereIsNoBaseline()
        => Assert.Equal(0, await CountAsync(current: Complicated, head: null));

    [Fact]
    public async Task DoesNotReportANewMethod()
    {
        // The baseline knows another method entirely: this one is new, so it degraded nothing.
        const string head = @"
public class C
{
    public int Other(int a)
    {
        return a;
    }
}";

        Assert.Equal(0, await CountAsync(current: Complicated, head: head));
    }

    [Fact]
    public async Task MatchesTheMethodEvenWhenItMovedInTheFile()
    {
        // Same method, preceded by another one: keyed on type + name + parameter count, never on a
        // line number, so moving it must not lose the baseline.
        const string moved = @"
public class C
{
    public void Padding()
    {
    }

    public int Compute(int a)
    {
        if (a > 0)
        {
            return 1;
        }

        if (a < 0)
        {
            return 2;
        }

        return a;
    }
}";

        Assert.Equal(1, await CountAsync(current: moved, head: Simple));
    }

    [Fact]
    public async Task RespectsAnAllowedIncrease()
    {
        var config = new Dictionary<string, string> { [AllowedIncrease] = "2" };
        Assert.Equal(0, await CountAsync(current: Complicated, head: Simple, config: config));
    }

    [Fact]
    public async Task ReportsBeyondTheAllowedIncrease()
    {
        var config = new Dictionary<string, string> { [AllowedIncrease] = "1" };
        Assert.Equal(1, await CountAsync(current: Complicated, head: Simple, config: config));
    }

    [Fact]
    public async Task IgnoresAdditionalFilesThatAreNotBaselines()
        => Assert.Equal(0, await CountAsync(current: Complicated, head: Simple, markAsBaseline: false));

    [Fact]
    public async Task MessageReportsBothComplexities()
    {
        var diagnostics = await AnalyzeAsync(Complicated, Simple, null, true);
        var message = Assert.Single(diagnostics, d => d.Id == ComplexityRatchetAnalyzer.DiagnosticId).GetMessage();

        Assert.Contains("C.Compute", message);
        Assert.Contains("1", message);   // before
        Assert.Contains("3", message);   // after
    }

    [Fact]
    public async Task IsReportedAsAWarningNotAnError()
    {
        // Deliberate: a complexity increase is sometimes the legitimate evolution of the software,
        // and blocking it would push people to split methods artificially. Changing this should be
        // a conscious decision, so the test pins it.
        var diagnostics = await AnalyzeAsync(Complicated, Simple, null, true);
        var diagnostic = Assert.Single(diagnostics, d => d.Id == ComplexityRatchetAnalyzer.DiagnosticId);

        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
    }

    // --- Hotspot mode: churn selects which files get the strict treatment ---

    [Fact]
    public async Task ReportsTheHotspotRuleWhenTheFileIsChurned()
    {
        var churn = new Dictionary<string, int> { ["C.cs"] = 12 };
        var diagnostics = await AnalyzeAsync(Complicated, Simple, null, true, churn);

        Assert.Single(diagnostics, d => d.Id == ComplexityRatchetAnalyzer.HotspotDiagnosticId);
        Assert.DoesNotContain(diagnostics, d => d.Id == ComplexityRatchetAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task TheHotspotRuleIsAnError()
    {
        // The plain ratchet is a warning; in a file the team keeps touching it becomes an error.
        var churn = new Dictionary<string, int> { ["C.cs"] = 12 };
        var diagnostics = await AnalyzeAsync(Complicated, Simple, null, true, churn);

        var diagnostic = Assert.Single(diagnostics, d => d.Id == ComplexityRatchetAnalyzer.HotspotDiagnosticId);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
    }

    [Fact]
    public async Task FallsBackToThePlainRatchetWhenTheFileIsRarelyTouched()
    {
        // Below the minimum churn: a file changed once is never a hotspot.
        var churn = new Dictionary<string, int> { ["C.cs"] = 1 };
        var diagnostics = await AnalyzeAsync(Complicated, Simple, null, true, churn);

        Assert.Single(diagnostics, d => d.Id == ComplexityRatchetAnalyzer.DiagnosticId);
        Assert.DoesNotContain(diagnostics, d => d.Id == ComplexityRatchetAnalyzer.HotspotDiagnosticId);
    }

    [Fact]
    public async Task FallsBackToThePlainRatchetWithoutAChurnTable()
        => Assert.Equal(1, await CountAsync(current: Complicated, head: Simple));

    private static async Task<int> CountAsync(
        string current,
        string? head,
        Dictionary<string, string>? config = null,
        bool markAsBaseline = true)
        => (await AnalyzeAsync(current, head, config, markAsBaseline, null))
            .Count(d => d.Id == ComplexityRatchetAnalyzer.DiagnosticId);

    private static async Task<List<Diagnostic>> AnalyzeAsync(
        string current,
        string? head,
        Dictionary<string, string>? config,
        bool markAsBaseline,
        Dictionary<string, int>? churn = null)
    {
        using var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();

        var solution = workspace.CurrentSolution
            .AddProject(ProjectInfo.Create(projectId, VersionStamp.Default, "P", "P", LanguageNames.CSharp))
            .WithProjectCompilationOptions(projectId, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .AddMetadataReference(projectId, MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
            .AddDocument(DocumentId.CreateNewId(projectId), "C.cs", SourceText.From(current));

        workspace.TryApplyChanges(solution);
        var compilation = await workspace.CurrentSolution.GetProject(projectId)!.GetCompilationAsync();

        var files = new List<AdditionalText>();
        if (head is not null)
        {
            var baseline = new InMemoryText("head/C.cs", head);
            if (markAsBaseline)
            {
                baseline.Metadata[BaselineMetadata] = "C.cs";
            }

            files.Add(baseline);
        }

        if (churn is not null)
        {
            var table = new InMemoryText(
                "churn.txt",
                string.Join(Environment.NewLine, churn.Select(pair => pair.Value + "|" + pair.Key)));
            table.Metadata[ChurnMetadata] = "true";
            files.Add(table);
        }

        var options = new AnalyzerOptions(
            files.ToImmutableArray(),
            new ConfigProvider(config ?? new Dictionary<string, string>()));

        var diagnostics = await compilation!
            .WithAnalyzers(
                ImmutableArray.Create<DiagnosticAnalyzer>(new ComplexityRatchetAnalyzer()),
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
