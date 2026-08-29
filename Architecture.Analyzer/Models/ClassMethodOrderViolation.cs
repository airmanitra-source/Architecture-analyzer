using Microsoft.CodeAnalysis;

namespace Architecture.Analyzer.Models;

internal sealed class ClassMethodOrderViolation
{
    public ClassMethodOrderViolation(string classTypeName, string previousMethodName, string currentMethodName, Location location)
    {
        ClassTypeName = classTypeName;
        PreviousMethodName = previousMethodName;
        CurrentMethodName = currentMethodName;
        Location = location;
    }

    public string ClassTypeName { get; }

    public string PreviousMethodName { get; }

    public string CurrentMethodName { get; }

    public Location Location { get; }
}