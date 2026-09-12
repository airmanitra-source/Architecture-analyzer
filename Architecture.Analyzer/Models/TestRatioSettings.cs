namespace Architecture.Analyzer.Models;

internal sealed class TestRatioSettings
{
    public TestRatioSettings(int minTestPercent, int minAddedLines, int addedProductionLines, int addedTestLines)
    {
        MinTestPercent = minTestPercent;
        MinAddedLines = minAddedLines;
        AddedProductionLines = addedProductionLines;
        AddedTestLines = addedTestLines;
    }

    // 0 when the key is absent: the rule is opt-in and stays inactive until a percentage is set.
    public int MinTestPercent { get; }

    // Small changes are exempt: demanding tests for a three-line fix only teaches people to
    // write filler tests.
    public int MinAddedLines { get; }

    public int AddedProductionLines { get; }

    public int AddedTestLines { get; }

    public bool IsEnabled => MinTestPercent > 0;
}
