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

    [Theory]
    [InlineData("Business/Models")]
    [InlineData("Business>Models")]
    [InlineData("Business\\Models")]
    public async Task AllowsBusinessModelInsideBusinessModelsDirectory_WithConfiguredFolderSeparators(string configuredFolder)
    {
        const string source = "public class CustomerBusinessModel { }";

        var diagnostics = await ArchitectureAnalyzerTestRunner.AnalyzeAsync(
            source,
            new ModelDirectoryAnalyzer(),
            "Business/Models/CustomerBusinessModel.cs",
            new Dictionary<string, string>
            {
                ["architecture_analyzer.model_folder"] = configuredFolder,
            });

        Assert.DoesNotContain(diagnostics, d => d.Id == ModelDirectoryAnalyzer.DiagnosticId);
    }

    [Theory]
    [InlineData("BusinessModel=Business/Models")]
    [InlineData("BusinessModel=Business>Models")]
    [InlineData("BusinessModel=Business\\Models")]
    public async Task AllowsBusinessModelInsideBusinessModelsDirectory_WithConfiguredConventionSeparators(string configuredConvention)
    {
        const string source = "public class CustomerBusinessModel { }";

        var diagnostics = await ArchitectureAnalyzerTestRunner.AnalyzeAsync(
            source,
            new ModelDirectoryAnalyzer(),
            "Business/Models/CustomerBusinessModel.cs",
            new Dictionary<string, string>
            {
                ["architecture_analyzer.model_conventions"] = configuredConvention,
            });

        Assert.DoesNotContain(diagnostics, d => d.Id == ModelDirectoryAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task ReportsBusinessModelWhenGenericConventionWouldOtherwiseMatch()
    {
        const string source = "public class CustomerBusinessModel { }";

        var diagnostics = await ArchitectureAnalyzerTestRunner.AnalyzeAsync(
            source,
            new ModelDirectoryAnalyzer(),
            "Models/Common/CustomerBusinessModel.cs",
            new Dictionary<string, string>
            {
                ["architecture_analyzer.model_conventions"] = "Model=Models/Common;BusinessModel=Models/Business",
            });

        Assert.Contains(diagnostics, d => d.Id == ModelDirectoryAnalyzer.DiagnosticId);
    }

    [Theory]
    [InlineData("Models/Business")]
    [InlineData("Models>Business")]
    [InlineData("Models\\Business")]
    public async Task ReportsWhenModelSuffixIsNotAllowedInConfiguredFolder(string configuredFolder)
    {
        const string source = "public class CustomerDataModel { }";

        var diagnostics = await ArchitectureAnalyzerTestRunner.AnalyzeAsync(
            source,
            new ModelDirectoryAnalyzer(),
            "Models/Business/CustomerDataModel.cs",
            new Dictionary<string, string>
            {
                ["architecture_analyzer.model_folder_allowed_suffixes"] = $"{configuredFolder}=BusinessModel;Models/Data=DataModel",
            });

        Assert.Contains(diagnostics, d => d.Id == ModelDirectoryAnalyzer.FolderAllowedDiagnosticId);
    }

    [Fact]
    public async Task AllowsConfiguredSuffixInsideConfiguredFolder()
    {
        const string source = "public class CustomerBusinessModel { }";

        var diagnostics = await ArchitectureAnalyzerTestRunner.AnalyzeAsync(
            source,
            new ModelDirectoryAnalyzer(),
            "Models/Business/CustomerBusinessModel.cs",
            new Dictionary<string, string>
            {
                ["architecture_analyzer.model_folder_allowed_suffixes"] = "Models/Business=BusinessModel;Models/Data=DataModel",
            });

        Assert.DoesNotContain(diagnostics, d => d.Id == ModelDirectoryAnalyzer.FolderAllowedDiagnosticId);
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
