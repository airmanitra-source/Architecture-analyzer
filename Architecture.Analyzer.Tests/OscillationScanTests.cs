using System.Collections.Generic;
using Architecture.Analyzer.Build;

namespace Architecture.Analyzer.Tests;

// The oscillation parser/normalizer/hasher are framework-free static helpers in the compiled task
// assembly, so they are unit-tested directly here — the payoff of moving the task out of the inline
// RoslynCodeTaskFactory block.
public sealed class OscillationScanTests
{
    // The git log format begins each commit line with a record separator (0x1e) and splits the sha
    // from the subject with a unit separator (0x1f).
    private const char Rs = (char)30;
    private const char Us = (char)31;

    [Fact]
    public void NormalizeCollapsesWhitespaceSoIndentationDoesNotMatter()
        => Assert.Equal(
            OscillationScan.NormalizeLine("    var x = a + b;"),
            OscillationScan.NormalizeLine("\tvar   x =  a + b;"));

    [Theory]
    [InlineData("", true)]
    [InlineData("}", true)]
    [InlineData("});", true)]
    [InlineData("return x;", false)]
    public void TrivialLinesCarryNoSignal(string line, bool trivial)
        => Assert.Equal(trivial, OscillationScan.IsTrivial(OscillationScan.NormalizeLine(line)));

    [Fact]
    public void RemovedAndAddedHashesLineUpForTheSameBlock()
    {
        var log =
            Rs + "abc1234def" + Us + "hurried cleanup\n" +
            "diff --git a/C.cs b/C.cs\n" +
            "--- a/C.cs\n" +
            "+++ b/C.cs\n" +
            "@@ -10,3 +10,0 @@\n" +
            "-        var first = Compute(a);\n" +
            "-        var second = Compute(b);\n" +
            "-        return first + second;\n";

        var diff =
            "diff --git a/C.cs b/C.cs\n" +
            "--- a/C.cs\n" +
            "+++ b/C.cs\n" +
            "@@ -20,0 +21,3 @@\n" +
            "+        var first = Compute(a);\n" +
            "+        var second = Compute(b);\n" +
            "+        return first + second;\n";

        var removed = OscillationScan.ParseRemovedWindows(log);
        var added = OscillationScan.ParseAddedWindows(diff);

        var removedWindow = Assert.Single(removed);
        var addedWindow = Assert.Single(added);

        Assert.Equal(removedWindow.Hash, addedWindow.Hash);
        Assert.Equal("abc1234", removedWindow.Sha);
        Assert.Equal("hurried cleanup", removedWindow.Subject);
        Assert.Equal(1, removedWindow.Ago);
        Assert.Equal("C.cs", addedWindow.Path);
        Assert.Equal(21, addedWindow.StartLine);
    }

    [Fact]
    public void ReindentedReAddStillMatches()
    {
        var log =
            Rs + "sha1" + Us + "first\n" +
            "@@ -1,3 +1,0 @@\n" +
            "-var first = Compute(a);\n" +
            "-var second = Compute(b);\n" +
            "-return first + second;\n";
        var diff =
            "+++ b/C.cs\n" +
            "@@ -1,0 +1,3 @@\n" +
            "+            var first = Compute(a);\n" +
            "+            var second = Compute(b);\n" +
            "+            return first + second;\n";

        Assert.Equal(
            Assert.Single(OscillationScan.ParseRemovedWindows(log)).Hash,
            Assert.Single(OscillationScan.ParseAddedWindows(diff)).Hash);
    }

    [Fact]
    public void ADifferentBlockDoesNotMatch()
    {
        var log =
            Rs + "sha1" + Us + "first\n" +
            "@@ -1,3 +1,0 @@\n" +
            "-        var first = Compute(a);\n" +
            "-        var second = Compute(b);\n" +
            "-        return first + second;\n";
        var diff =
            "+++ b/C.cs\n" +
            "@@ -1,0 +1,3 @@\n" +
            "+        var totallyDifferent = 1;\n" +
            "+        var another = 2;\n" +
            "+        return another;\n";

        var removed = OscillationScan.ParseRemovedWindows(log);
        var added = OscillationScan.ParseAddedWindows(diff);

        Assert.NotEqual(Assert.Single(removed).Hash, Assert.Single(added).Hash);
    }

    [Fact]
    public void ARunShorterThanTheBlockProducesNoWindow()
    {
        var diff =
            "+++ b/C.cs\n" +
            "@@ -1,0 +1,2 @@\n" +
            "+        var one = 1;\n" +
            "+        var two = 2;\n";
        Assert.Empty(OscillationScan.ParseAddedWindows(diff));
    }

    [Fact]
    public void UntrackedContentIsWindowedWithRealLineNumbers()
    {
        var lines = new List<string>
        {
            "namespace App;",     // 1
            "public class C",     // 2
            "{",                  // 3 trivial, breaks the run
            "    int A() => 1;",  // 4
            "    int B() => 2;",  // 5
            "    int Cc() => 3;", // 6
        };

        var window = Assert.Single(OscillationScan.AddedWindowsFromContent("C.cs", lines));
        Assert.Equal(4, window.StartLine);
        Assert.Equal("C.cs", window.Path);
    }
}
