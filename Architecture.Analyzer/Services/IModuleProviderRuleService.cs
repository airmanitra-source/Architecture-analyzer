using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;

namespace Architecture.Analyzer;

internal interface IModuleProviderRuleService
{
    ProviderImplementationViolation? AnalyzeType(INamedTypeSymbol type, ImmutableArray<INamedTypeSymbol> providerInterfaces, CancellationToken cancellationToken);

    ImmutableArray<INamedTypeSymbol> GetProviderInterfaces(Compilation compilation, CancellationToken cancellationToken);

    Location? GetProjectLocation(Compilation compilation, CancellationToken cancellationToken);

    bool IsModuleProject(string? assemblyName);
}