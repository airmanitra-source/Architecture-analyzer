using Microsoft.CodeAnalysis;

namespace Architecture.Analyzer.Models;

internal sealed class TestRatioViolation
{
    public TestRatioViolation(int productionLines, int testLines, int actualPercent, int requiredPercent, Location location)
    {
        ProductionLines = productionLines;
        TestLines = testLines;
        ActualPercent = actualPercent;
        RequiredPercent = requiredPercent;
        Location = location;
    }

    public int ProductionLines { get; }

    public int TestLines { get; }

    public int ActualPercent { get; }

    public int RequiredPercent { get; }

    public Location Location { get; }
}
