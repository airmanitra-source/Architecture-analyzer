using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Architecture.Analyzer.Services.ModuleReferenceBarrierRule;

internal sealed class ModuleReferenceBarrierRuleService : IModuleReferenceBarrierRuleService
{
    private const string AnalyzerConfigPrefix = "architecture_analyzer.";
    private const string BarrierOption = AnalyzerConfigPrefix + "module_reference_barrier";

    public List<Models.ModuleReferenceBarrierRule> GetBarrierRules(AnalyzerConfigOptions options, string? assemblyName)
    {
        if (string.IsNullOrWhiteSpace(assemblyName))
        {
            return new List<Models.ModuleReferenceBarrierRule>();
        }

        if (!options.TryGetValue(BarrierOption, out var value) || string.IsNullOrWhiteSpace(value))
        {
            return new List<Models.ModuleReferenceBarrierRule>();
        }

        var rules = new List<Models.ModuleReferenceBarrierRule>();

        foreach (var entry in value.Split(','))
        {
            var trimmed = entry.Trim();
            var separatorIndex = trimmed.IndexOf('>');
            if (separatorIndex <= 0 || separatorIndex >= trimmed.Length - 1)
            {
                continue;
            }

            var source = trimmed.Substring(0, separatorIndex).Trim();
            var forbidden = trimmed.Substring(separatorIndex + 1).Trim();

            if (AssemblyMatchesModule(assemblyName, source))
            {
                rules.Add(new Models.ModuleReferenceBarrierRule(source, forbidden));
            }
        }

        return rules;
    }

    public Models.ModuleReferenceBarrierViolation? CheckSymbolReference(
        ISymbol referencedSymbol,
        ISymbol containingSymbol,
        List<Models.ModuleReferenceBarrierRule> rules,
        Location location)
    {
        if (rules.Count == 0)
        {
            return null;
        }

        var referencedAssembly = referencedSymbol.ContainingAssembly?.Name;
        if (string.IsNullOrWhiteSpace(referencedAssembly))
        {
            return null;
        }

        foreach (var rule in rules)
        {
            if (AssemblyMatchesModule(referencedAssembly, rule.ForbiddenModule))
            {
                return new Models.ModuleReferenceBarrierViolation(
                    containingSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                    referencedSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                    rule.ForbiddenModule,
                    location);
            }
        }

        return null;
    }

    private static bool AssemblyMatchesModule(string assemblyName, string moduleName)
        => assemblyName.Equals(moduleName, StringComparison.OrdinalIgnoreCase)
           || assemblyName.StartsWith(moduleName + ".", StringComparison.OrdinalIgnoreCase)
           || assemblyName.EndsWith("." + moduleName, StringComparison.OrdinalIgnoreCase);
}
