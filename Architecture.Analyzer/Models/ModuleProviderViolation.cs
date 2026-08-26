using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Architecture.Analyzer.Models;

internal sealed class ModuleProviderViolation
{
    public ModuleProviderViolation(bool isApplicable, Location? missingProvidersLocation, List<ProviderImplementationViolation> violations)
    {
        IsApplicable = isApplicable;
        MissingProvidersLocation = missingProvidersLocation;
        Violations = violations;
    }

    public bool IsApplicable { get; }

    public Location? MissingProvidersLocation { get; }

    public List<ProviderImplementationViolation> Violations { get; }
}