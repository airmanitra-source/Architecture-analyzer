using Architecture.Analyzer;

namespace Architecture.Analyzer.Tests;

public sealed class RequiredProjectFolderAnalyzerTests
{
    private static readonly Dictionary<string, string> Options = new()
    {
        ["architecture_analyzer.required_project_folders"] = "HR.Infrastructure=Models/Entities;HR.Module=Models/Data,Presentation/ViewModels"
    };

    [Fact]
    public async Task DoesNotReportDiagnosticWhenProjectHasNoConfiguredRule()
    {
        var diagnostics = await ArchitectureAnalyzerTestRunner.AnalyzeAsync(
            [("Class1.cs", "public class Class1 { }")],
            new RequiredProjectFolderAnalyzer(),
            "HR.Core",
            Options);

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == RequiredProjectFolderAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task ReportsDiagnosticWhenRequiredFolderIsMissing()
    {
        var diagnostics = await ArchitectureAnalyzerTestRunner.AnalyzeAsync(
            [("Class1.cs", "public class Class1 { }")],
            new RequiredProjectFolderAnalyzer(),
            "HR.Infrastructure",
            Options);

        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == RequiredProjectFolderAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task DoesNotReportDiagnosticWhenRequiredFolderContainsAFile()
    {
        var diagnostics = await ArchitectureAnalyzerTestRunner.AnalyzeAsync(
            [("Models/Entities/Employee.cs", "public class Employee { }")],
            new RequiredProjectFolderAnalyzer(),
            "HR.Infrastructure",
            Options);

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == RequiredProjectFolderAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task ReportsOneDiagnosticPerMissingFolder()
    {
        var diagnostics = await ArchitectureAnalyzerTestRunner.AnalyzeAsync(
            [("Class1.cs", "public class Class1 { }")],
            new RequiredProjectFolderAnalyzer(),
            "HR.Module",
            Options);

        Assert.Equal(2, diagnostics.Count(diagnostic => diagnostic.Id == RequiredProjectFolderAnalyzer.DiagnosticId));
    }
}
