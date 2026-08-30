using Microsoft.CodeAnalysis;

namespace Architecture.Analyzer.Models;

internal sealed class NameLengthViolation
{
    public NameLengthViolation(string name, int currentLength, int minimumLength, Location location)
    {
        Name = name;
        CurrentLength = currentLength;
        MinimumLength = minimumLength;
        Location = location;
    }

    public string Name { get; }

    public int CurrentLength { get; }

    public int MinimumLength { get; }

    public Location Location { get; }
}
