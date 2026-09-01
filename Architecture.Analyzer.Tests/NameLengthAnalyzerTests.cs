using Architecture.Analyzer;

namespace Architecture.Analyzer.Tests;

public sealed class NameLengthAnalyzerTests
{
    private const string MinVariable = "architecture_analyzer.min_variable_name_length";
    private const string MinClass = "architecture_analyzer.min_class_name_length";
    private const string MinMethod = "architecture_analyzer.min_method_name_length";

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
    public async Task ReportsDiagnosticWhenMethodNameIsTooShort()
    {
        const string source = "public class Customer { public void Go() { } }";

        var diagnostics = await ArchitectureAnalyzerTestRunner.AnalyzeAsync(
            source,
            new NameLengthAnalyzer(),
            "Customer.cs",
            new Dictionary<string, string> { [MinMethod] = "4" });

        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == NameLengthAnalyzer.MethodDiagnosticId);
    }

    [Fact]
    public async Task AllowsMethodNameThatMeetsMinimum()
    {
        const string source = "public class Customer { public void Execute() { } }";

        var diagnostics = await ArchitectureAnalyzerTestRunner.AnalyzeAsync(
            source,
            new NameLengthAnalyzer(),
            "Customer.cs",
            new Dictionary<string, string> { [MinMethod] = "4" });

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == NameLengthAnalyzer.MethodDiagnosticId);
    }

    [Fact]
    public async Task DoesNotReportMethodWhenNotConfigured()
    {
        const string source = "public class Customer { public void Go() { } }";

        var diagnostics = await ArchitectureAnalyzerTestRunner.AnalyzeAsync(source, new NameLengthAnalyzer(), "Customer.cs");

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == NameLengthAnalyzer.MethodDiagnosticId);
    }

    [Fact]
    public async Task RulesAreIndependent_MethodConfiguredClassIsNot()
    {
        const string source = "public class Ab { public void Go() { } }";

        var diagnostics = await ArchitectureAnalyzerTestRunner.AnalyzeAsync(
            source,
            new NameLengthAnalyzer(),
            "Ab.cs",
            new Dictionary<string, string> { [MinMethod] = "4" });

        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == NameLengthAnalyzer.MethodDiagnosticId);
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

    [Fact]
    public async Task DoesNotReportMethodThatOverridesFrameworkMethod()
    {
        const string source = "public class Customer { public override string ToString() { return \"x\"; } }";

        var diagnostics = await ArchitectureAnalyzerTestRunner.AnalyzeAsync(
            source,
            new NameLengthAnalyzer(),
            "Customer.cs",
            new Dictionary<string, string> { [MinMethod] = "14" });

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == NameLengthAnalyzer.MethodDiagnosticId);
    }

    [Fact]
    public async Task DoesNotReportMethodThatImplementsInterfaceMember()
    {
        const string source = "using System; public class Handle : IDisposable { public void Dispose() { } }";

        var diagnostics = await ArchitectureAnalyzerTestRunner.AnalyzeAsync(
            source,
            new NameLengthAnalyzer(),
            "Handle.cs",
            new Dictionary<string, string> { [MinMethod] = "14" });

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == NameLengthAnalyzer.MethodDiagnosticId);
    }

    [Fact]
    public async Task DoesNotReportExplicitInterfaceImplementation()
    {
        const string source = "using System; public class Handle : IDisposable { void IDisposable.Dispose() { } }";

        var diagnostics = await ArchitectureAnalyzerTestRunner.AnalyzeAsync(
            source,
            new NameLengthAnalyzer(),
            "Handle.cs",
            new Dictionary<string, string> { [MinMethod] = "14" });

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == NameLengthAnalyzer.MethodDiagnosticId);
    }

    [Fact]
    public async Task ReportsDiagnosticForShortInterfaceMethodDeclaration()
    {
        const string source = "public interface IThing { void Go(); }";

        var diagnostics = await ArchitectureAnalyzerTestRunner.AnalyzeAsync(
            source,
            new NameLengthAnalyzer(),
            "IThing.cs",
            new Dictionary<string, string> { [MinMethod] = "4" });

        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == NameLengthAnalyzer.MethodDiagnosticId);
    }

    [Fact]
    public async Task ReportsMethodThatOverridesApplicationBaseMethod()
    {
        const string source = "public class Base { public virtual void Go() { } } public class Derived : Base { public override void Go() { } }";

        var diagnostics = await ArchitectureAnalyzerTestRunner.AnalyzeAsync(
            source,
            new NameLengthAnalyzer(),
            "Types.cs",
            new Dictionary<string, string> { [MinMethod] = "4" });

        // The base is app-defined, so both it and its app-side override are flagged.
        Assert.Equal(2, diagnostics.Count(diagnostic => diagnostic.Id == NameLengthAnalyzer.MethodDiagnosticId));
    }

    [Fact]
    public async Task ReportsMethodThatImplementsApplicationInterfaceMember()
    {
        const string source = "public interface IThing { void Go(); } public class Thing : IThing { public void Go() { } }";

        var diagnostics = await ArchitectureAnalyzerTestRunner.AnalyzeAsync(
            source,
            new NameLengthAnalyzer(),
            "Types.cs",
            new Dictionary<string, string> { [MinMethod] = "4" });

        // The interface is app-defined, so both its member and the app-side implementation are flagged.
        Assert.Equal(2, diagnostics.Count(diagnostic => diagnostic.Id == NameLengthAnalyzer.MethodDiagnosticId));
    }
}
