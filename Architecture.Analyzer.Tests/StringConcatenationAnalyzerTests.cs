using Architecture.Analyzer;

namespace Architecture.Analyzer.Tests;

public sealed class StringConcatenationAnalyzerTests
{
    // Wraps a method body whose parameters a, b, c are strings, then counts ARCH017.
    private static async Task<int> CountAsync(string body)
    {
        var source = "public class C { public void M(string a, string b, string c) {\n" + body + "\n} }";
        var diagnostics = await ArchitectureAnalyzerTestRunner.AnalyzeAsync(source, new StringConcatenationAnalyzer(), "C.cs");
        return diagnostics.Count(diagnostic => diagnostic.Id == StringConcatenationAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task ReportsForSimpleConcatenation() => Assert.Equal(1, await CountAsync("var s = a + b;"));

    [Fact]
    public async Task ReportsForSimpleAssignmentConcatenation() => Assert.Equal(1, await CountAsync("a = b + c;"));

    [Fact]
    public async Task ReportsForCompoundAssignment() => Assert.Equal(1, await CountAsync("a += b;"));

    [Fact]
    public async Task ReportsForStringPlusNonString() => Assert.Equal(1, await CountAsync("var s = a + 1;"));

    [Fact]
    public async Task ReportsForLiteralPlusVariable() => Assert.Equal(1, await CountAsync("var s = \"prefix \" + a;"));

    [Fact]
    public async Task ReportsConcatenationChainExactlyOnce() => Assert.Equal(1, await CountAsync("var s = a + b + c;"));

    [Fact]
    public async Task ReportsCompoundWithNestedConcatenationOnce() => Assert.Equal(1, await CountAsync("a += b + c;"));

    [Fact]
    public async Task DoesNotReportNumericAddition() => Assert.Equal(0, await CountAsync("var n = 1 + 2;"));

    [Fact]
    public async Task DoesNotReportStringInterpolation() => Assert.Equal(0, await CountAsync("var s = $\"{a}{b}\";"));

    [Fact]
    public async Task DoesNotReportConstantLiteralConcatenation() => Assert.Equal(0, await CountAsync("var s = \"x\" + \"y\";"));

    [Fact]
    public async Task DoesNotReportConstFieldConcatenation()
    {
        // A const cannot use a StringBuilder, and the literals are folded at compile time.
        const string source = "public class C { private const string X = \"a\" + \"b\" + \"c\"; }";

        var diagnostics = await ArchitectureAnalyzerTestRunner.AnalyzeAsync(source, new StringConcatenationAnalyzer(), "C.cs");

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == StringConcatenationAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task DoesNotReportUserDefinedPlusOperatorReturningString()
    {
        // 'a + b' resolves to a user-defined operator, not the built-in string concatenation.
        const string source = "public struct Money { public static string operator +(Money a, Money b) => \"x\"; public string Combine(Money x, Money y) => x + y; }";

        var diagnostics = await ArchitectureAnalyzerTestRunner.AnalyzeAsync(source, new StringConcatenationAnalyzer(), "Money.cs");

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == StringConcatenationAnalyzer.DiagnosticId);
    }
}
