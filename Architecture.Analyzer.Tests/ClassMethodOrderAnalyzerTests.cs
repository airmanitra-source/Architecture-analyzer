using Architecture.Analyzer;

namespace Architecture.Analyzer.Tests;

public sealed class ClassMethodOrderAnalyzerTests
{
    [Fact]
    public async Task ReportsDiagnosticWhenMethodsAreNotAlphabetical()
    {
        const string source = "public class Customer { public void SaveCustomer() { } public void DeleteCustomer() { } }";

        var diagnostics = await ArchitectureAnalyzerTestRunner.AnalyzeAsync(source, new ClassMethodOrderAnalyzer());

        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == ClassMethodOrderAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task AllowsAlphabeticalMethods()
    {
        const string source = "public class Customer { public void DeleteCustomer() { } public void SaveCustomer() { } }";

        var diagnostics = await ArchitectureAnalyzerTestRunner.AnalyzeAsync(source, new ClassMethodOrderAnalyzer());

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == ClassMethodOrderAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task DoesNotReportDiagnosticForASingleMethod()
    {
        const string source = "public class Customer { public void SaveCustomer() { } }";

        var diagnostics = await ArchitectureAnalyzerTestRunner.AnalyzeAsync(source, new ClassMethodOrderAnalyzer());

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == ClassMethodOrderAnalyzer.DiagnosticId);
    }
}
