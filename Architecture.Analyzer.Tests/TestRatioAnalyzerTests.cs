using Architecture.Analyzer;

namespace Architecture.Analyzer.Tests;

public sealed class TestRatioAnalyzerTests
{
    private const string MinPercent = "architecture_analyzer.min_test_lines_percent";
    private const string MinAdded = "architecture_analyzer.test_ratio_min_added_lines";
    private const string Production = "build_property.ArchitectureLocAddedProductionLines";
    private const string Tests = "build_property.ArchitectureLocAddedTestLines";

    private const string AnySource = "public class C { public void M() { } }";

    private static async Task<List<Microsoft.CodeAnalysis.Diagnostic>> AnalyzeAsync(Dictionary<string, string>? config)
        => await ArchitectureAnalyzerTestRunner.AnalyzeAsync(AnySource, new TestRatioAnalyzer(), "C.cs", config);

    private static async Task<int> CountAsync(Dictionary<string, string>? config)
        => (await AnalyzeAsync(config)).Count(d => d.Id == TestRatioAnalyzer.DiagnosticId);

    [Fact]
    public async Task DoesNotReportWhenRuleIsNotConfigured()
        => Assert.Equal(0, await CountAsync(new Dictionary<string, string> { [Production] = "500", [Tests] = "0" }));

    [Fact]
    public async Task ReportsWhenTooFewTestLinesWereAdded()
    {
        // 10 test lines for 200 production lines = 5%, below the required 20%.
        var count = await CountAsync(new Dictionary<string, string>
        {
            [MinPercent] = "20",
            [Production] = "200",
            [Tests] = "10"
        });

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task DoesNotReportWhenTheRatioIsMet()
    {
        // 50 test lines for 200 production lines = 25%, above the required 20%.
        var count = await CountAsync(new Dictionary<string, string>
        {
            [MinPercent] = "20",
            [Production] = "200",
            [Tests] = "50"
        });

        Assert.Equal(0, count);
    }

    [Fact]
    public async Task DoesNotReportWhenTheChangeIsTooSmallToMatter()
    {
        // 20 added lines is below the default 50-line floor: a small fix is exempt.
        var count = await CountAsync(new Dictionary<string, string>
        {
            [MinPercent] = "20",
            [Production] = "20",
            [Tests] = "0"
        });

        Assert.Equal(0, count);
    }

    [Fact]
    public async Task ReportsSmallChangesWhenTheFloorIsLowered()
    {
        var count = await CountAsync(new Dictionary<string, string>
        {
            [MinPercent] = "20",
            [MinAdded] = "10",
            [Production] = "20",
            [Tests] = "0"
        });

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task DoesNotReportWhenNoProductionLineWasAdded()
    {
        // A prompt that only touches tests must never be penalised.
        var count = await CountAsync(new Dictionary<string, string>
        {
            [MinPercent] = "20",
            [Production] = "0",
            [Tests] = "120"
        });

        Assert.Equal(0, count);
    }

    [Fact]
    public async Task MessageReportsBothCountsAndTheRatio()
    {
        var diagnostics = await AnalyzeAsync(new Dictionary<string, string>
        {
            [MinPercent] = "25",
            [Production] = "400",
            [Tests] = "40"
        });

        var message = Assert.Single(diagnostics, d => d.Id == TestRatioAnalyzer.DiagnosticId).GetMessage();
        Assert.Contains("400", message);   // production lines
        Assert.Contains("40", message);    // test lines
        Assert.Contains("10", message);    // actual ratio
        Assert.Contains("25", message);    // required ratio
    }
}
