using Microsoft.CodeAnalysis;

namespace Architecture.Analyzer.Models;

internal sealed class ModuleReferenceBarrierViolation
{
    public ModuleReferenceBarrierViolation(string referencingType, string referencedType, string forbiddenModule, Location location)
    {
        ReferencingType = referencingType;
        ReferencedType = referencedType;
        ForbiddenModule = forbiddenModule;
        Location = location;
    }

    public string ReferencingType { get; }

    public string ReferencedType { get; }

    public string ForbiddenModule { get; }

    public Location Location { get; }
}
