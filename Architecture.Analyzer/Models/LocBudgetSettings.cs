namespace Architecture.Analyzer.Models;

internal sealed class LocBudgetSettings
{
    public LocBudgetSettings(int? maxClassLines, int? maxMethodLines, int? projectBudget, int? globalBudget)
    {
        MaxClassLines = maxClassLines;
        MaxMethodLines = maxMethodLines;
        ProjectBudget = projectBudget;
        GlobalBudget = globalBudget;
    }

    public int? MaxClassLines { get; }

    public int? MaxMethodLines { get; }

    public int? ProjectBudget { get; }

    public int? GlobalBudget { get; }

    public bool HasRules => MaxClassLines.HasValue || MaxMethodLines.HasValue || ProjectBudget.HasValue || GlobalBudget.HasValue;
}