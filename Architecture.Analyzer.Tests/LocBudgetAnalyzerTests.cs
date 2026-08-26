using Architecture.Analyzer;

namespace Architecture.Analyzer.Tests;

public sealed class LocBudgetAnalyzerTests
{
    private const string ProjectPercent = "architecture_analyzer.loc_budget_percent_project";
    private const string GlobalPercent = "architecture_analyzer.loc_budget_percent_global";
    private const string MaxClassLines = "architecture_analyzer.loc_max_lines_per_class";
    private const string MaxMethodLines = "architecture_analyzer.loc_max_lines_per_method";
    private const string ProjectAdded = "build_property.ArchitectureLocProjectAddedLines";
    private const string ProjectBaseline = "build_property.ArchitectureLocProjectBaselineLines";
    private const string SolutionAdded = "build_property.ArchitectureLocSolutionAddedLines";
    private const string SolutionBaseline = "build_property.ArchitectureLocSolutionBaselineLines";

    private const string TinySource = "namespace Architecture.Rules.Demo;\n\npublic class TestBusiness\n{\n    public void Run()\n    {\n    }\n}\n";

    [Fact]
    public async Task ReportsDiagnosticWhenClassAddsTooManyLines()
    {
        var members = string.Join("\n", Enumerable.Range(1, 30).Select(index => $"    public int Value{index} {{ get; set; }}"));
        var source = $"namespace Architecture.Rules.Demo;\n\npublic class TestBusiness\n{{\n{members}\n}}\n";

        var diagnostics = await ArchitectureAnalyzerTestRunner.AnalyzeAsync(
            source,
            new LocBudgetAnalyzer(),
            "TestBusiness.cs",
            new Dictionary<string, string> { [MaxClassLines] = "20" });

        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == LocBudgetAnalyzer.ClassDiagnosticId);
    }

    [Fact]
    public async Task ReportsDiagnosticWhenMethodAddsTooManyLines()
    {
        var statements = string.Join("\n", Enumerable.Range(1, 30).Select(index => $"        var value{index} = {index};"));
        var source = $"namespace Architecture.Rules.Demo;\n\npublic class TestBusiness\n{{\n    public void Run()\n    {{\n{statements}\n    }}\n}}\n";

        var diagnostics = await ArchitectureAnalyzerTestRunner.AnalyzeAsync(
            source,
            new LocBudgetAnalyzer(),
            "TestBusiness.cs",
            new Dictionary<string, string> { [MaxMethodLines] = "20" });

        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == LocBudgetAnalyzer.MethodDiagnosticId);
    }

    [Fact]
    public async Task ReportsDiagnosticWhenMethodExceedsHardLimitWithoutConfiguration()
    {
        var statements = string.Join("\n", Enumerable.Range(1, 30).Select(index => $"        var value{index} = {index};"));
        var source = $"namespace Architecture.Rules.Demo;\n\npublic class TestBusiness\n{{\n    public void Run()\n    {{\n{statements}\n    }}\n}}\n";

        var diagnostics = await ArchitectureAnalyzerTestRunner.AnalyzeAsync(source, new LocBudgetAnalyzer(), "TestBusiness.cs");

        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == LocBudgetAnalyzer.MethodDiagnosticId);
    }

    [Fact]
    public async Task DoesNotReportBudgetWhenNoLinesWereAdded()
    {
        var diagnostics = await ArchitectureAnalyzerTestRunner.AnalyzeAsync(
            TinySource,
            new LocBudgetAnalyzer(),
            "TestBusiness.cs",
            new Dictionary<string, string>
            {
                [ProjectPercent] = "2",
                [GlobalPercent] = "2",
                [ProjectBaseline] = "1000",
                [SolutionBaseline] = "1000",
                [ProjectAdded] = "0",
                [SolutionAdded] = "0"
            });

        Assert.DoesNotContain(diagnostics, diagnostic =>
            diagnostic.Id is LocBudgetAnalyzer.ProjectDiagnosticId or LocBudgetAnalyzer.SolutionDiagnosticId);
    }

    [Fact]
    public async Task ReportsDiagnosticWhenProjectAddsTooManyLines()
    {
        // budget = ceil(1000 * 2%) = 20; added 30 exceeds it.
        var diagnostics = await ArchitectureAnalyzerTestRunner.AnalyzeAsync(
            TinySource,
            new LocBudgetAnalyzer(),
            "TestBusiness.cs",
            new Dictionary<string, string>
            {
                [ProjectPercent] = "2",
                [ProjectBaseline] = "1000",
                [ProjectAdded] = "30"
            });

        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == LocBudgetAnalyzer.ProjectDiagnosticId);
    }

    [Fact]
    public async Task DoesNotReportProjectWhenAddedLinesAreWithinBudget()
    {
        // budget = ceil(1000 * 2%) = 20; added 10 stays within it.
        var diagnostics = await ArchitectureAnalyzerTestRunner.AnalyzeAsync(
            TinySource,
            new LocBudgetAnalyzer(),
            "TestBusiness.cs",
            new Dictionary<string, string>
            {
                [ProjectPercent] = "2",
                [ProjectBaseline] = "1000",
                [ProjectAdded] = "10"
            });

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == LocBudgetAnalyzer.ProjectDiagnosticId);
    }

    [Fact]
    public async Task ReportsDiagnosticWhenSolutionAddsTooManyLines()
    {
        // budget = ceil(1000 * 2%) = 20; added 30 exceeds it.
        var diagnostics = await ArchitectureAnalyzerTestRunner.AnalyzeAsync(
            TinySource,
            new LocBudgetAnalyzer(),
            "TestBusiness.cs",
            new Dictionary<string, string>
            {
                [GlobalPercent] = "2",
                [SolutionBaseline] = "1000",
                [SolutionAdded] = "30"
            });

        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == LocBudgetAnalyzer.SolutionDiagnosticId);
    }

    [Fact]
    public async Task ReportsSolutionButNotProjectWhenGlobalBudgetIsStricter()
    {
        // project budget = ceil(1000 * 3%) = 30, added 25 -> within budget.
        // solution budget = ceil(1000 * 2%) = 20, added 25 -> exceeds budget.
        var diagnostics = await ArchitectureAnalyzerTestRunner.AnalyzeAsync(
            TinySource,
            new LocBudgetAnalyzer(),
            "TestBusiness.cs",
            new Dictionary<string, string>
            {
                [ProjectPercent] = "3",
                [GlobalPercent] = "2",
                [ProjectBaseline] = "1000",
                [SolutionBaseline] = "1000",
                [ProjectAdded] = "25",
                [SolutionAdded] = "25"
            });

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == LocBudgetAnalyzer.ProjectDiagnosticId);
        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == LocBudgetAnalyzer.SolutionDiagnosticId);
    }
}
