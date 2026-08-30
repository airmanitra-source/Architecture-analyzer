using Architecture.Analyzer;

namespace Architecture.Analyzer.Tests;

public sealed class NameLengthAnalyzerTests
{
    private const string MinVariable = "architecture_analyzer.min_variable_name_length";
    private const string MinClass = "architecture_analyzer.min_class_name_length";

    [Fact]
    public async Task ReportsDiagnosticWhenVariableNameIsTooShort()
    {
        const string source = "public class Customer { public void Run() { int a = 1; } }";

        var diagnostics = await ArchitectureAnalyzerTestRunner.AnalyzeAsync(
            source,
            new NameLengthAnalyzer(),
            "Customer.cs",
            new Dictionary<string, string> { [MinVariable] = "3" });

        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == NameLengthAnalyzer.VariableDiagnosticId);
    }

    [Fact]
    public async Task ReportsDiagnosticForShortFieldName()
    {
        const string source = "public class Customer { private int a; }";

        var diagnostics = await ArchitectureAnalyzerTestRunner.AnalyzeAsync(
            source,
            new NameLengthAnalyzer(),
            "Customer.cs",
            new Dictionary<string, string> { [MinVariable] = "3" });

        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == NameLengthAnalyzer.VariableDiagnosticId);
    }

    [Fact]
    public async Task AllowsVariableNameThatMeetsMinimum()
    {
        const string source = "public class Customer { public void Run() { int total = 1; } }";

        var diagnostics = await ArchitectureAnalyzerTestRunner.AnalyzeAsync(
            source,
            new NameLengthAnalyzer(),
            "Customer.cs",
            new Dictionary<string, string> { [MinVariable] = "3" });

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == NameLengthAnalyzer.VariableDiagnosticId);
    }

    [Fact]
    public async Task DoesNotReportVariableWhenNotConfigured()
    {
        const string source = "public class Customer { public void Run() { int a = 1; } }";

        var diagnostics = await ArchitectureAnalyzerTestRunner.AnalyzeAsync(source, new NameLengthAnalyzer(), "Customer.cs");

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == NameLengthAnalyzer.VariableDiagnosticId);
    }

    [Fact]
    public async Task ReportsDiagnosticWhenClassNameIsTooShort()
    {
        const string source = "public class Ab { }";

        var diagnostics = await ArchitectureAnalyzerTestRunner.AnalyzeAsync(
            source,
            new NameLengthAnalyzer(),
            "Ab.cs",
            new Dictionary<string, string> { [MinClass] = "3" });

        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == NameLengthAnalyzer.ClassDiagnosticId);
    }

    [Fact]
    public async Task AllowsClassNameThatMeetsMinimum()
    {
        const string source = "public class Customer { }";

        var diagnostics = await ArchitectureAnalyzerTestRunner.AnalyzeAsync(
            source,
            new NameLengthAnalyzer(),
            "Customer.cs",
            new Dictionary<string, string> { [MinClass] = "3" });

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == NameLengthAnalyzer.ClassDiagnosticId);
    }

    [Fact]
    public async Task DoesNotReportClassWhenNotConfigured()
    {
        const string source = "public class Ab { }";

        var diagnostics = await ArchitectureAnalyzerTestRunner.AnalyzeAsync(source, new NameLengthAnalyzer(), "Ab.cs");

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == NameLengthAnalyzer.ClassDiagnosticId);
    }

    [Fact]
    public async Task RulesAreIndependent_ClassConfiguredVariableIsNot()
    {
        const string source = "public class Ab { public void Run() { int a = 1; } }";

        var diagnostics = await ArchitectureAnalyzerTestRunner.AnalyzeAsync(
            source,
            new NameLengthAnalyzer(),
            "Ab.cs",
            new Dictionary<string, string> { [MinClass] = "3" });

        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == NameLengthAnalyzer.ClassDiagnosticId);
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == NameLengthAnalyzer.VariableDiagnosticId);
    }

    [Fact]
    public async Task RulesAreIndependent_VariableConfiguredClassIsNot()
    {
        const string source = "public class Ab { public void Run() { int a = 1; } }";

        var diagnostics = await ArchitectureAnalyzerTestRunner.AnalyzeAsync(
            source,
            new NameLengthAnalyzer(),
            "Ab.cs",
            new Dictionary<string, string> { [MinVariable] = "3" });

        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == NameLengthAnalyzer.VariableDiagnosticId);
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == NameLengthAnalyzer.ClassDiagnosticId);
    }
}
