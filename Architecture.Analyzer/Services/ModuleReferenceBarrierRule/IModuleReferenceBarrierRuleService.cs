using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Architecture.Analyzer.Services.ModuleReferenceBarrierRule;

internal interface IModuleReferenceBarrierRuleService
{
    List<Models.ModuleReferenceBarrierRule> GetBarrierRules(AnalyzerConfigOptions options, string? assemblyName);

    Models.ModuleReferenceBarrierViolation? CheckSymbolReference(ISymbol referencedSymbol, ISymbol containingSymbol, List<Models.ModuleReferenceBarrierRule> rules, Location location);
}
