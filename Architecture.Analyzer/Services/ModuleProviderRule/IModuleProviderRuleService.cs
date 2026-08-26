using System.Collections.Generic;
using System.Threading;
using Architecture.Analyzer.Models;
using Microsoft.CodeAnalysis;

namespace Architecture.Analyzer.Services.ModuleProviderRule;

internal interface IModuleProviderRuleService
{
    ProviderImplementationViolation? GetProviderImplementionViolation(INamedTypeSymbol type, List<INamedTypeSymbol> providerInterfaces, CancellationToken cancellationToken);

    List<INamedTypeSymbol> GetProviderInterfaces(Compilation compilation, CancellationToken cancellationToken);

    Location? GetProjectLocation(Compilation compilation, CancellationToken cancellationToken);

    bool IsModuleProject(string? assemblyName);
}