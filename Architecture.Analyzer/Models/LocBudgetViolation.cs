using Microsoft.CodeAnalysis;

namespace Architecture.Analyzer.Models;

internal sealed class LocBudgetViolation
{
    public LocBudgetViolation(string scopeName, string itemName, int currentLines, int allowedLines, Location location)
    {
        ScopeName = scopeName;
        ItemName = itemName;
        CurrentLines = currentLines;
        AllowedLines = allowedLines;
        Location = location;
    }

    public string ScopeName { get; }

    public string ItemName { get; }

    public int CurrentLines { get; }

    public int AllowedLines { get; }

    public Location Location { get; }
}