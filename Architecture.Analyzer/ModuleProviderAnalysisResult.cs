using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Architecture.Analyzer;

internal sealed class ModuleProviderAnalysisResult
{
    public ModuleProviderAnalysisResult(bool isApplicable, Location? missingProvidersLocation, ImmutableArray<ProviderImplementationViolation> violations)
    {
        IsApplicable = isApplicable;
        MissingProvidersLocation = missingProvidersLocation;
        Violations = violations;
    }

    public bool IsApplicable { get; }

    public Location? MissingProvidersLocation { get; }

    public ImmutableArray<ProviderImplementationViolation> Violations { get; }
}