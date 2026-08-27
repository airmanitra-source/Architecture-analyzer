using Architecture.Analyzer;

namespace Architecture.Analyzer.Tests;

public sealed class ClassPropertyOrderAnalyzerTests
{
    [Theory]
    [InlineData("public class CustomerBusinessModel { public string Name { get; set; } public string Age { get; set; } }")]
    [InlineData("public class CustomerDataModel { public string Name { get; set; } public string Age { get; set; } }")]
    [InlineData("public class CustomerViewModel { public string Name { get; set; } public string Age { get; set; } }")]
    public async Task ReportsDiagnosticWhenDtoPropertiesAreNotAlphabetical(string source)
    {
        var diagnostics = await ArchitectureAnalyzerTestRunner.AnalyzeAsync(source, new ClassPropertyOrderAnalyzer());

        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == ClassPropertyOrderAnalyzer.DiagnosticId);
    }

    [Theory]
    [InlineData("public class CustomerBusinessModel { public string Age { get; set; } public string Name { get; set; } }")]
    [InlineData("public class CustomerDataModel { public string Age { get; set; } public string Name { get; set; } }")]
    [InlineData("public class CustomerViewModel { public string Age { get; set; } public string Name { get; set; } }")]
    public async Task AllowsAlphabeticalDtoProperties(string source)
    {
        var diagnostics = await ArchitectureAnalyzerTestRunner.AnalyzeAsync(source, new ClassPropertyOrderAnalyzer());

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == ClassPropertyOrderAnalyzer.DiagnosticId);
    }

    [Theory]
    [InlineData("public class CustomerBusinessModel { public string Age { get; set; } public string CustomerId { get; set; } public string Name { get; set; } }")]
    [InlineData("public class CustomerDataModel { public string Age { get; set; } public string CustomerIds { get; set; } public string Name { get; set; } }")]
    [InlineData("public class CustomerViewModel { public string Age { get; set; } public string CustomerIDs { get; set; } public string Name { get; set; } }")]
    public async Task IgnoresIdentifierPropertiesWhenCheckingOrder(string source)
    {
        var diagnostics = await ArchitectureAnalyzerTestRunner.AnalyzeAsync(source, new ClassPropertyOrderAnalyzer());

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == ClassPropertyOrderAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task DoesNotAnalyzeNonDtoClasses()
    {
        const string source = "public class Customer { public string Name { get; set; } public string Age { get; set; } }";

        var diagnostics = await ArchitectureAnalyzerTestRunner.AnalyzeAsync(source, new ClassPropertyOrderAnalyzer());

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == ClassPropertyOrderAnalyzer.DiagnosticId);
    }
}