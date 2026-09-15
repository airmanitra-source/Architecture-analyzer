using System.Collections.Immutable;
using Architecture.Analyzer;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Architecture.Analyzer.Tests;

public sealed class TestLinkAnalyzerTests
{
    private const string BaselineMetadata = "build_metadata.AdditionalFiles.ArchitectureBaselineFor";
    private const string ChangedFilesMetadata = "build_metadata.AdditionalFiles.ArchitectureChangedFiles";
    private const string ChangedTestFileMetadata = "build_metadata.AdditionalFiles.ArchitectureChangedTestFile";
    private const string MinAddedLines = "architecture_analyzer.test_link_min_added_lines";

    private const string ProductionWithAdd = @"
public class C
{
    public int Add(int a, int b)
    {
        var sum = a + b;
        return sum;
    }
}";

    private const string EmptyType = @"
public class C
{
}";

    private const string TestThatCallsAdd = @"
public class CTests
{
    [Fact]
    public void AddWorks()
    {
        var c = new C();
        Assert.Equal(3, c.Add(1, 2));
    }
}";

    [Fact]
    public async Task ReportsANewMemberWithNoRelatedTest()
        => Assert.Equal(1, await CountAsync(current: ProductionWithAdd, head: EmptyType));

    [Fact]
    public async Task DoesNotReportWhenARelatedTestExists()
        => Assert.Equal(0, await CountAsync(current: ProductionWithAdd, head: EmptyType, test: ("CTests.cs", TestThatCallsAdd)));

    [Fact]
    public async Task DoesNotReportAnUnchangedMember()
        => Assert.Equal(0, await CountAsync(current: ProductionWithAdd, head: ProductionWithAdd));

    [Fact]
    public async Task DoesNotReportAPrivateMember()
    {
        const string current = @"
public class C
{
    private int Add(int a, int b)
    {
        var sum = a + b;
        return sum;
    }
}";
        Assert.Equal(0, await CountAsync(current: current, head: EmptyType));
    }

    [Fact]
    public async Task ReportsWhenTheTestOnlyMocksTheInterface()
    {
        const string test = @"
public class CTests
{
    [Fact]
    public void AddWorks()
    {
        IC c = Substitute.For<IC>();
        Assert.Equal(3, c.Add(1, 2));
    }
}";
        // The test references IC, never the concrete C, so it does not count as exercising C.Add.
        Assert.Equal(1, await CountAsync(current: ProductionWithAdd, head: EmptyType, test: ("CTests.cs", test)));
    }

    [Fact]
    public async Task ReportsWhenTheTestHasNoAssertion()
    {
        const string test = @"
public class CTests
{
    [Fact]
    public void AddWorks()
    {
        var c = new C();
        c.Add(1, 2);
    }
}";
        Assert.Equal(1, await CountAsync(current: ProductionWithAdd, head: EmptyType, test: ("CTests.cs", test)));
    }

    [Fact]
    public async Task ReportsWhenTheCallIsSwallowedInACatchAll()
    {
        const string test = @"
public class CTests
{
    [Fact]
    public void AddWorks()
    {
        var c = new C();
        try { c.Add(1, 2); } catch { }
        Assert.True(true);
    }
}";
        Assert.Equal(1, await CountAsync(current: ProductionWithAdd, head: EmptyType, test: ("CTests.cs", test)));
    }

    [Fact]
    public async Task ReportsWhenTheMemberIsOnlyNamed()
    {
        const string test = @"
public class CTests
{
    [Fact]
    public void AddWorks()
    {
        var c = new C();
        Assert.Equal(""Add"", nameof(C.Add));
    }
}";
        Assert.Equal(1, await CountAsync(current: ProductionWithAdd, head: EmptyType, test: ("CTests.cs", test)));
    }

    [Fact]
    public async Task DoesNotReportWhenDisabled()
    {
        var config = new Dictionary<string, string> { [MinAddedLines] = "0" };
        Assert.Equal(0, await CountAsync(current: ProductionWithAdd, head: EmptyType, config: config));
    }

    [Fact]
    public async Task StaysSilentWithoutTheChangedFilesList()
        => Assert.Equal(0, await CountAsync(current: ProductionWithAdd, head: EmptyType, includeChangedFiles: false));

    [Fact]
    public async Task DoesNotReportABelowFloorNewMember()
    {
        const string current = @"
public class C
{
    public int One() => 1;
}";
        var config = new Dictionary<string, string> { [MinAddedLines] = "5" };
        Assert.Equal(0, await CountAsync(current: current, head: EmptyType, config: config));
    }

    [Fact]
    public async Task ReportsANewMemberInANewFileWithoutAHeadCopy()
        // No HEAD copy at all: the file is new, so its members are new and accountable.
        => Assert.Equal(1, await CountAsync(current: ProductionWithAdd, head: null));

    [Fact]
    public async Task DoesNotCreditATestThatWasNotChangedByThisChange()
    {
        // The test file's HEAD copy is identical to its current content: the test method is not new
        // or modified, so it gives no credit even though it calls Add with an assertion.
        var diagnostics = await AnalyzeAsync(
            ProductionWithAdd,
            EmptyType,
            ("CTests.cs", TestThatCallsAdd),
            testHead: TestThatCallsAdd,
            config: null,
            includeChangedFiles: true);

        Assert.Equal(1, diagnostics.Count(d => d.Id == TestLinkAnalyzer.DiagnosticId));
    }

    [Fact]
    public async Task SatisfiesANewConstructorConstructedByATest()
    {
        const string current = @"
public class C
{
    public C(int seed)
    {
        var local = seed;
        Value = local;
    }

    public int Value;
}";
        const string test = @"
public class CTests
{
    [Fact]
    public void ConstructsC()
    {
        var c = new C(7);
        Assert.Equal(7, c.Value);
    }
}";
        Assert.Equal(0, await CountAsync(current: current, head: EmptyType, test: ("CTests.cs", test)));
    }

    [Fact]
    public async Task TheMessageNamesTheMember()
    {
        var diagnostics = await AnalyzeAsync(ProductionWithAdd, EmptyType, null, null, null, true);
        var message = Assert.Single(diagnostics, d => d.Id == TestLinkAnalyzer.DiagnosticId).GetMessage();
        Assert.Contains("C.Add", message);
    }

    [Fact]
    public async Task TheDiagnosticIsAnError()
    {
        var diagnostics = await AnalyzeAsync(ProductionWithAdd, EmptyType, null, null, null, true);
        var diagnostic = Assert.Single(diagnostics, d => d.Id == TestLinkAnalyzer.DiagnosticId);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
    }

    private static async Task<int> CountAsync(
        string current,
        string? head,
        (string path, string content)? test = null,
        Dictionary<string, string>? config = null,
        bool includeChangedFiles = true)
        => (await AnalyzeAsync(current, head, test, null, config, includeChangedFiles))
            .Count(d => d.Id == TestLinkAnalyzer.DiagnosticId);

    private static async Task<List<Diagnostic>> AnalyzeAsync(
        string current,
        string? head,
        (string path, string content)? test,
        string? testHead,
        Dictionary<string, string>? config,
        bool includeChangedFiles)
    {
        const string productionPath = "C.cs";

        using var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var solution = workspace.CurrentSolution
            .AddProject(ProjectInfo.Create(projectId, VersionStamp.Default, "P", "P", LanguageNames.CSharp))
            .WithProjectCompilationOptions(projectId, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .AddMetadataReference(projectId, MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
            .AddDocument(DocumentId.CreateNewId(projectId), productionPath, SourceText.From(current));

        workspace.TryApplyChanges(solution);
        var compilation = await workspace.CurrentSolution.GetProject(projectId)!.GetCompilationAsync();

        var files = new List<AdditionalText>();

        if (head is not null)
        {
            var baseline = new InMemoryText("head/C.cs", head);
            baseline.Metadata[BaselineMetadata] = productionPath;
            files.Add(baseline);
        }

        var changedPaths = new List<string> { productionPath };

        if (test is not null)
        {
            var testFile = new InMemoryText(test.Value.path, test.Value.content);
            testFile.Metadata[ChangedTestFileMetadata] = "true";
            files.Add(testFile);
            changedPaths.Add(test.Value.path);

            if (testHead is not null)
            {
                var testBaseline = new InMemoryText("head/" + test.Value.path, testHead);
                testBaseline.Metadata[BaselineMetadata] = test.Value.path;
                files.Add(testBaseline);
            }
        }

        if (includeChangedFiles)
        {
            var changed = new InMemoryText("changed-files.txt", string.Join(Environment.NewLine, changedPaths));
            changed.Metadata[ChangedFilesMetadata] = "true";
            files.Add(changed);
        }

        var values = config ?? new Dictionary<string, string> { [MinAddedLines] = "1" };
        if (!values.ContainsKey(MinAddedLines))
        {
            values[MinAddedLines] = "1";
        }

        var options = new AnalyzerOptions(files.ToImmutableArray(), new ConfigProvider(values));

        var diagnostics = await compilation!
            .WithAnalyzers(
                ImmutableArray.Create<DiagnosticAnalyzer>(new TestLinkAnalyzer()),
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
