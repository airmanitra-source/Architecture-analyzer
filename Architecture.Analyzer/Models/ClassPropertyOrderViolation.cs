using Microsoft.CodeAnalysis;

namespace Architecture.Analyzer.Models;

internal sealed class ClassPropertyOrderViolation
{
    public ClassPropertyOrderViolation(string dtoTypeName, string previousPropertyName, string currentPropertyName, Location location)
    {
        DtoTypeName = dtoTypeName;
        PreviousPropertyName = previousPropertyName;
        CurrentPropertyName = currentPropertyName;
        Location = location;
    }

    public string DtoTypeName { get; }

    public string PreviousPropertyName { get; }

    public string CurrentPropertyName { get; }

    public Location Location { get; }
}