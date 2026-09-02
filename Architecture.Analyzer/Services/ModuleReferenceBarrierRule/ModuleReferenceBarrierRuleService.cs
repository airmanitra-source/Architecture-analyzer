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
    private const string ExceptionsOption = AnalyzerConfigPrefix + "module_reference_barrier_exceptions";

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

        // Rules are separated by ';' (consistent with every other multi-rule key of
        // this analyzer) or ',' (the historically documented separator). A single
        // rule "Source>Forbidden" never contains either character.
        foreach (var entry in value.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries))
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

    // Files (by name or path suffix) where the barrier does not apply — typically the
    // composition root (Program.cs / Startup.cs) that legitimately wires modules together.
    public IReadOnlyCollection<string> GetExemptedFiles(AnalyzerConfigOptions options)
    {
        if (!options.TryGetValue(ExceptionsOption, out var value) || string.IsNullOrWhiteSpace(value))
        {
            return Array.Empty<string>();
        }

        var exempted = new List<string>();
        foreach (var entry in value.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = entry.Trim();
            if (trimmed.Length > 0)
            {
                exempted.Add(Normalize(trimmed));
            }
        }

        return exempted;
    }

    public bool IsFileExempted(string? filePath, IReadOnlyCollection<string> exemptedFiles)
    {
        if (string.IsNullOrEmpty(filePath) || exemptedFiles.Count == 0)
        {
            return false;
        }

        var normalizedPath = Normalize(filePath!);
        var lastSlash = normalizedPath.LastIndexOf('/');
        var fileName = lastSlash >= 0 ? normalizedPath.Substring(lastSlash + 1) : normalizedPath;

        foreach (var exempted in exemptedFiles)
        {
            if (fileName.Equals(exempted, StringComparison.OrdinalIgnoreCase)
                || normalizedPath.Equals(exempted, StringComparison.OrdinalIgnoreCase)
                || normalizedPath.EndsWith("/" + exempted, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
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

    private static string Normalize(string path)
        => path.Replace('\\', '/');
}
