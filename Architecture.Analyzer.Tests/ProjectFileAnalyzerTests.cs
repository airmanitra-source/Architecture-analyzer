using Architecture.Analyzer;
using Microsoft.CodeAnalysis;

namespace Architecture.Analyzer.Tests;

// ARCH003, per-project file allow-list facet (ProjectFileAnalyzer).
public sealed class ProjectFileAnalyzerTests
{
    private const string Option = "architecture_analyzer.project_allowed_files";

    private static readonly (string, string) Helper = ("Helper.cs", "public class Helper { }");
    private static readonly (string, string) OrderDto = ("OrderDto.cs", "public class OrderDto { }");
    private static readonly (string, string) CreateRequest = ("CreateRequest.cs", "public class CreateRequest { }");

    [Fact]
    public async Task ReportsAFileMatchingNoAllowedPattern()
    {
        var diagnostics = await Run("HR.Contracts", new Dictionary<string, string>
        {
            [Option] = "*.Contracts=*Dto.cs,*Request.cs",
        }, Helper);

        Assert.Contains(diagnostics, d => d.Id == ProjectFileAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task DoesNotReportAFileMatchingAnAllowedPattern()
    {
        var diagnostics = await Run("HR.Contracts", new Dictionary<string, string>
        {
            [Option] = "*.Contracts=*Dto.cs,*Request.cs",
        }, OrderDto, CreateRequest);

        Assert.DoesNotContain(diagnostics, d => d.Id == ProjectFileAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task DoesNotReportWhenTheProjectMatchesNoRule()
    {
        var diagnostics = await Run("HR.Api", new Dictionary<string, string>
        {
            [Option] = "*.Contracts=*Dto.cs",
        }, Helper);

        Assert.DoesNotContain(diagnostics, d => d.Id == ProjectFileAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task DoesNotReportWithoutConfiguration()
    {
        var diagnostics = await Run("HR.Contracts", null, Helper);
        Assert.DoesNotContain(diagnostics, d => d.Id == ProjectFileAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task TheMessageNamesTheFileAndProject()
    {
        var diagnostics = await Run("HR.Contracts", new Dictionary<string, string>
        {
            [Option] = "HR.Contracts=*Dto.cs",
        }, Helper);

        var message = Assert.Single(diagnostics, d => d.Id == ProjectFileAnalyzer.DiagnosticId).GetMessage();
        Assert.Contains("Helper.cs", message);
        Assert.Contains("HR.Contracts", message);
    }

    [Fact]
    public async Task TheDiagnosticIsAnError()
    {
        var diagnostics = await Run("HR.Contracts", new Dictionary<string, string>
        {
            [Option] = "HR.Contracts=*Dto.cs",
        }, Helper);

        Assert.Equal(DiagnosticSeverity.Error, Assert.Single(diagnostics, d => d.Id == ProjectFileAnalyzer.DiagnosticId).Severity);
    }

    [Fact]
    public async Task FolderPatternAllowsAFileInTheRightFolder()
    {
        var diagnostics = await Run("Acme.Module", new Dictionary<string, string>
        {
            [Option] = "*.Module=Models/*BusinessModel.cs",
        }, ("Models/CustomerBusinessModel.cs", "public class CustomerBusinessModel { }"));

        Assert.DoesNotContain(diagnostics, d => d.Id == ProjectFileAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task FolderPatternForbidsTheRightNameInTheWrongFolder()
    {
        var diagnostics = await Run("Acme.Module", new Dictionary<string, string>
        {
            [Option] = "*.Module=Models/*BusinessModel.cs",
        }, ("Services/CustomerBusinessModel.cs", "public class CustomerBusinessModel { }"));

        Assert.Contains(diagnostics, d => d.Id == ProjectFileAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task FolderPatternForbidsTheWrongNameInTheRightFolder()
    {
        var diagnostics = await Run("Acme.Module", new Dictionary<string, string>
        {
            [Option] = "*.Module=Models/*BusinessModel.cs",
        }, ("Models/Helper.cs", "public class Helper { }"));

        Assert.Contains(diagnostics, d => d.Id == ProjectFileAnalyzer.DiagnosticId);
    }

    private static Task<List<Diagnostic>> Run(
        string projectName,
        IReadOnlyDictionary<string, string>? config,
        params (string fileName, string source)[] documents)
        => ArchitectureAnalyzerTestRunner.AnalyzeAsync(documents, new ProjectFileAnalyzer(), projectName, config);
}
