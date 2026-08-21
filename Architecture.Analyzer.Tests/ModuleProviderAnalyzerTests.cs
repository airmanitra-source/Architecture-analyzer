using Architecture.Analyzer;

namespace Architecture.Analyzer.Tests;

public sealed class ModuleProviderAnalyzerTests
{
    [Fact]
    public async Task DoesNotReportDiagnosticOutsideModuleProjects()
    {
        var diagnostics = await ArchitectureAnalyzerTestRunner.AnalyzeAsync(
            [("Class1.cs", "public class Class1 { }")],
            new ModuleProviderAnalyzer(),
            "HR.Core");

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == ModuleProviderAnalyzer.MissingProviderContractsDiagnosticId);
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == ModuleProviderAnalyzer.LocalProviderImplementationDiagnosticId);
    }

    [Fact]
    public async Task ReportsDiagnosticWhenModuleProjectDoesNotContainProviderInterfaces()
    {
        var diagnostics = await ArchitectureAnalyzerTestRunner.AnalyzeAsync(
            [("Class1.cs", "public class Class1 { }")],
            new ModuleProviderAnalyzer(),
            "HR.Module");

        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == ModuleProviderAnalyzer.MissingProviderContractsDiagnosticId);
    }

    [Fact]
    public async Task AllowsModuleProjectWithProviderInterfaceInExpectedFolder()
    {
        var diagnostics = await ArchitectureAnalyzerTestRunner.AnalyzeAsync(
            [("Models/Data/Providers/IEmployeeProvider.cs", "public interface IEmployeeProvider { }")],
            new ModuleProviderAnalyzer(),
            "HR.Module");

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == ModuleProviderAnalyzer.MissingProviderContractsDiagnosticId);
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == ModuleProviderAnalyzer.LocalProviderImplementationDiagnosticId);
    }

    [Fact]
    public async Task ReportsDiagnosticWhenProviderImplementationIsInsideModule()
    {
        var diagnostics = await ArchitectureAnalyzerTestRunner.AnalyzeAsync(
            [
                ("Models/Data/Providers/IEmployeeProvider.cs", "namespace HR.Module.Models.Data.Providers { public interface IEmployeeProvider { } }"),
                ("Services/EmployeeProvider.cs", "namespace HR.Module.Services { public sealed class EmployeeProvider : Models.Data.Providers.IEmployeeProvider { } }")
            ],
            new ModuleProviderAnalyzer(),
            "HR.Module");

        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == ModuleProviderAnalyzer.LocalProviderImplementationDiagnosticId);
    }
}