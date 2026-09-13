using Microsoft.CodeAnalysis;

namespace Architecture.Analyzer.Models;

internal sealed class ComplexityRatchetViolation
{
    public ComplexityRatchetViolation(string methodName, int before, int after, Location location)
    {
        MethodName = methodName;
        Before = before;
        After = after;
        Location = location;
    }

    public string MethodName { get; }

    // Cyclomatic complexity of the method as it stands at HEAD.
    public int Before { get; }

    public int After { get; }

    public Location Location { get; }
}
