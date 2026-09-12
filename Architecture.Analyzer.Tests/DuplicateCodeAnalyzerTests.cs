using Architecture.Analyzer;

namespace Architecture.Analyzer.Tests;

public sealed class DuplicateCodeAnalyzerTests
{
    private const string MinTokens = "architecture_analyzer.duplicate_code_min_tokens";
    private const string Similarity = "architecture_analyzer.duplicate_code_similarity_percent";

    // Same shape, different names and constants: the classic "copy, paste, rename".
    private const string RenamedCopy = @"
public class C
{
    public int First(int a, int b)
    {
        var x = a + b;
        var y = x * 2;
        var z = y - a;
        return z + x;
    }

    public int Second(int m, int n)
    {
        var p = m + n;
        var q = p * 2;
        var r = q - m;
        return r + p;
    }
}";

    private static async Task<int> CountAsync(string source, Dictionary<string, string>? config = null)
    {
        var diagnostics = await ArchitectureAnalyzerTestRunner.AnalyzeAsync(
            source,
            new DuplicateCodeAnalyzer(),
            "C.cs",
            config ?? new Dictionary<string, string> { [MinTokens] = "10" });

        return diagnostics.Count(diagnostic => diagnostic.Id == DuplicateCodeAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task ReportsIdenticalStructureWithRenamedVariables()
        => Assert.Equal(1, await CountAsync(RenamedCopy));

    [Fact]
    public async Task ReportsThePairOnlyOnce()
    {
        // Two methods, one diagnostic: the later one is flagged and points at the earlier.
        Assert.Equal(1, await CountAsync(RenamedCopy));
    }

    [Fact]
    public async Task NamesTheMethodToReuse()
    {
        var diagnostics = await ArchitectureAnalyzerTestRunner.AnalyzeAsync(
            RenamedCopy,
            new DuplicateCodeAnalyzer(),
            "C.cs",
            new Dictionary<string, string> { [MinTokens] = "10" });

        var diagnostic = Assert.Single(diagnostics, d => d.Id == DuplicateCodeAnalyzer.DiagnosticId);
        var message = diagnostic.GetMessage();
        Assert.Contains("C.Second", message);   // the duplicate
        Assert.Contains("C.First", message);    // the one to reuse
    }

    [Fact]
    public async Task DoesNotReportStructurallyDifferentMethods()
    {
        const string source = @"
public class C
{
    public int Sum(int a, int b)
    {
        var x = a + b;
        var y = x * 2;
        var z = y - a;
        return z + x;
    }

    public string Describe(string label, bool flag)
    {
        if (flag)
        {
            return label.Trim().ToUpperInvariant();
        }

        return label;
    }
}";

        Assert.Equal(0, await CountAsync(source));
    }

    [Fact]
    public async Task DoesNotReportMethodsBelowTheMinimumSize()
    {
        // Same bodies, but the threshold puts them out of scope: a shared shape in a tiny method
        // is not duplication.
        Assert.Equal(0, await CountAsync(RenamedCopy, new Dictionary<string, string> { [MinTokens] = "500" }));
    }

    [Fact]
    public async Task DoesNotReportASingleMethod()
    {
        const string source = @"
public class C
{
    public int Only(int a, int b)
    {
        var x = a + b;
        var y = x * 2;
        var z = y - a;
        return z + x;
    }
}";

        Assert.Equal(0, await CountAsync(source));
    }

    [Fact]
    public async Task DoesNotReportAbstractOrInterfaceDeclarations()
    {
        const string source = @"
public interface IThing
{
    int Compute(int a, int b);
    int Evaluate(int a, int b);
}

public abstract class Base
{
    public abstract int Compute(int a, int b);
    public abstract int Evaluate(int a, int b);
}";

        Assert.Equal(0, await CountAsync(source));
    }

    // --- Near duplicates: same shape plus one extra statement ---

    private const string NearCopy = @"
public class C
{
    public int First(int a, int b)
    {
        var x = a + b;
        var y = x * 2;
        var z = y - a;
        return z + x;
    }

    public int Second(int m, int n)
    {
        var p = m + n;
        var q = p * 2;
        var r = q - m;
        var s = r + 1;
        return s + p;
    }
}";

    [Fact]
    public async Task DetectsNearDuplicateWhenThresholdIsLowered()
        => Assert.Equal(1, await CountAsync(NearCopy, new Dictionary<string, string> { [MinTokens] = "10", [Similarity] = "60" }));

    [Fact]
    public async Task DoesNotReportNearDuplicateWhenExactMatchIsRequired()
        => Assert.Equal(0, await CountAsync(NearCopy, new Dictionary<string, string> { [MinTokens] = "10", [Similarity] = "100" }));
}
