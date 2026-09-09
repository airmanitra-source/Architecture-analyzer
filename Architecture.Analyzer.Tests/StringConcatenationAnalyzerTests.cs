using Architecture.Analyzer;

namespace Architecture.Analyzer.Tests;

public sealed class StringConcatenationAnalyzerTests
{
    private const string Concat = StringConcatenationAnalyzer.ConcatenationDiagnosticId;   // ARCH017
    private const string Interp = StringConcatenationAnalyzer.InterpolationDiagnosticId;    // ARCH018

    // Wraps a method body whose parameters a, b, c are strings, then counts diagnostics of `id`.
    private static async Task<int> CountAsync(string body, string id)
    {
        var source = "public class C { public void M(string a, string b, string c) {\n" + body + "\n} }";
        var diagnostics = await ArchitectureAnalyzerTestRunner.AnalyzeAsync(source, new StringConcatenationAnalyzer(), "C.cs");
        return diagnostics.Count(diagnostic => diagnostic.Id == id);
    }

    // --- ARCH017: '+' / '+=' concatenation ---

    [Fact]
    public async Task ReportsForSimpleConcatenation() => Assert.Equal(1, await CountAsync("var s = a + b;", Concat));

    [Fact]
    public async Task ReportsForSimpleAssignmentConcatenation() => Assert.Equal(1, await CountAsync("a = b + c;", Concat));

    [Fact]
    public async Task ReportsForCompoundAssignment() => Assert.Equal(1, await CountAsync("a += b;", Concat));

    [Fact]
    public async Task ReportsForStringPlusNonString() => Assert.Equal(1, await CountAsync("var s = a + 1;", Concat));

    [Fact]
    public async Task ReportsForLiteralPlusVariable() => Assert.Equal(1, await CountAsync("var s = \"prefix \" + a;", Concat));

    [Fact]
    public async Task ReportsConcatenationChainExactlyOnce() => Assert.Equal(1, await CountAsync("var s = a + b + c;", Concat));

    [Fact]
    public async Task ReportsCompoundWithNestedConcatenationOnce() => Assert.Equal(1, await CountAsync("a += b + c;", Concat));

    [Fact]
    public async Task DoesNotReportNumericAddition() => Assert.Equal(0, await CountAsync("var n = 1 + 2;", Concat));

    [Fact]
    public async Task DoesNotReportConstantLiteralConcatenation() => Assert.Equal(0, await CountAsync("var s = \"x\" + \"y\";", Concat));

    [Fact]
    public async Task DoesNotReportConstFieldConcatenation()
    {
        const string source = "public class C { private const string X = \"a\" + \"b\" + \"c\"; }";

        var diagnostics = await ArchitectureAnalyzerTestRunner.AnalyzeAsync(source, new StringConcatenationAnalyzer(), "C.cs");

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == Concat);
    }

    [Fact]
    public async Task DoesNotReportUserDefinedPlusOperatorReturningString()
    {
        const string source = "public struct Money { public static string operator +(Money a, Money b) => \"x\"; public string Combine(Money x, Money y) => x + y; }";

        var diagnostics = await ArchitectureAnalyzerTestRunner.AnalyzeAsync(source, new StringConcatenationAnalyzer(), "Money.cs");

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == Concat);
    }

    // --- ARCH018: string interpolation ---

    [Fact]
    public async Task ReportsForInterpolationWithSingleHole() => Assert.Equal(1, await CountAsync("var s = $\"{a}\";", Interp));

    [Fact]
    public async Task ReportsForInterpolationWithTextAndHoles() => Assert.Equal(1, await CountAsync("var s = $\"Hello {a} and {b}\";", Interp));

    [Fact]
    public async Task ReportsInterpolationOncePerLiteral() => Assert.Equal(1, await CountAsync("var s = $\"{a}{b}{c}\";", Interp));

    [Fact]
    public async Task DoesNotReportInterpolationWithoutHoles() => Assert.Equal(0, await CountAsync("var s = $\"just text\";", Interp));

    [Fact]
    public async Task DoesNotReportPlainStringLiteral() => Assert.Equal(0, await CountAsync("var s = \"plain\";", Interp));

    [Fact]
    public async Task DoesNotReportConstantInterpolatedString()
    {
        // C# 10 allows a const interpolated string when every hole is a constant string; it folds.
        const string source = "public class C { private const string Y = \"y\"; private const string X = $\"a{Y}b\"; }";

        var diagnostics = await ArchitectureAnalyzerTestRunner.AnalyzeAsync(source, new StringConcatenationAnalyzer(), "C.cs");

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == Interp);
    }

    // --- Independence: each pattern raises only its own rule ---

    [Fact]
    public async Task ConcatenationDoesNotRaiseInterpolationRule() => Assert.Equal(0, await CountAsync("var s = a + b;", Interp));

    [Fact]
    public async Task InterpolationDoesNotRaiseConcatenationRule() => Assert.Equal(0, await CountAsync("var s = $\"{a}{b}\";", Concat));
}
