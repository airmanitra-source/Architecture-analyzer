using Microsoft.CodeAnalysis;

namespace Architecture.Analyzer.Models;

internal sealed class ConventionViolation
{
    public ConventionViolation(string typeName, Convention convention, string actual, Location location)
    {
        TypeName = typeName;
        Convention = convention;
        Actual = actual;
        Location = location;
    }

    public string TypeName { get; }

    public Convention Convention { get; }

    public string Actual { get; }

    public Location Location { get; }
}
