using Microsoft.CodeAnalysis;

namespace Architecture.Analyzer.Models;

internal sealed class DuplicateTypeNameViolation
{
    public DuplicateTypeNameViolation(string typeName, string referencedAssembly, Location location)
    {
        TypeName = typeName;
        ReferencedAssembly = referencedAssembly;
        Location = location;
    }

    public string TypeName { get; }

    // The referenced assembly that already carries a type with this name — the one to reuse.
    public string ReferencedAssembly { get; }

    public Location Location { get; }
}
