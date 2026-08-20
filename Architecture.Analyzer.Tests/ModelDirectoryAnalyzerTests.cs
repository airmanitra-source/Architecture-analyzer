using Architecture.Analyzer;

namespace Architecture.Analyzer.Tests;

public sealed class ModelDirectoryAnalyzerTests
{
    [Fact]
    public async Task ReportsBusinessModelOutsideBusinessModelsDirectory()
    {
        const string source = "public class CustomerBusinessModel { }";

        var diagnostics = await ArchitectureAnalyzerTestRunner.AnalyzeAsync(source, new ModelDirectoryAnalyzer(), "Features/CustomerBusinessModel.cs");

        Assert.Contains(diagnostics, d => d.Id == ModelDirectoryAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task AllowsBusinessModelInsideBusinessModelsDirectory()
    {
        const string source = "public class CustomerBusinessModel { }";

        var diagnostics = await ArchitectureAnalyzerTestRunner.AnalyzeAsync(source, new ModelDirectoryAnalyzer(), "Business/Models/CustomerBusinessModel.cs");

        Assert.DoesNotContain(diagnostics, d => d.Id == ModelDirectoryAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task ChecksEnumsAndRecords()
    {
        const string source = "public enum CustomerBusinessModel { Value } public record OrderBusinessModel;";

        var diagnostics = await ArchitectureAnalyzerTestRunner.AnalyzeAsync(source, new ModelDirectoryAnalyzer(), "Features/CustomerBusinessModel.cs");

        Assert.Equal(2, diagnostics.Count(d => d.Id == ModelDirectoryAnalyzer.DiagnosticId));
    }

    [Fact]
    public async Task ReportsDiagnosticWhenFileNameDoesNotMatchTypeName()
    {
        const string source = "public class CustomerBusinessModel { }";

        var diagnostics = await ArchitectureAnalyzerTestRunner.AnalyzeAsync(source, new ModelDirectoryAnalyzer(), "Business/Models/Customer.cs");

        Assert.Contains(diagnostics, d => d.Id == ModelDirectoryAnalyzer.FileNameDiagnosticId);
    }

    [Fact]
    public async Task AllowsMatchingFileNameForEnumAndRecord()
    {
        const string enumSource = "public enum CustomerBusinessModel { Value }";
        const string recordSource = "public record OrderBusinessModel;";

        var enumDiagnostics = await ArchitectureAnalyzerTestRunner.AnalyzeAsync(enumSource, new ModelDirectoryAnalyzer(), "Business/Models/CustomerBusinessModel.cs");
        var recordDiagnostics = await ArchitectureAnalyzerTestRunner.AnalyzeAsync(recordSource, new ModelDirectoryAnalyzer(), "Business/Models/OrderBusinessModel.cs");

        Assert.DoesNotContain(enumDiagnostics, d => d.Id == ModelDirectoryAnalyzer.FileNameDiagnosticId);
        Assert.DoesNotContain(recordDiagnostics, d => d.Id == ModelDirectoryAnalyzer.FileNameDiagnosticId);
    }
}
