using System;
using System.Collections.Generic;
using System.Linq;
using Architecture.Analyzer.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Architecture.Analyzer.Services.DuplicateTypeNameRule;

internal sealed class DuplicateTypeNameRuleService : IDuplicateTypeNameRuleService
{
    private const string AnalyzerConfigPrefix = "architecture_analyzer.";
    private const string ScopeOption = AnalyzerConfigPrefix + "duplicate_type_name_scope";
    private const string ExceptionsOption = AnalyzerConfigPrefix + "duplicate_type_name_exceptions";

    public IReadOnlyCollection<string> GetExceptions(AnalyzerConfigOptions options)
    {
        if (!options.TryGetValue(ExceptionsOption, out var value) || string.IsNullOrWhiteSpace(value))
        {
            return Array.Empty<string>();
        }

        var exceptions = new List<string>();
        foreach (var entry in value.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = entry.Trim();
            if (trimmed.Length > 0)
            {
                exceptions.Add(trimmed);
            }
        }

        return exceptions;
    }

    public Dictionary<string, string> GetReferencedTypeNames(Compilation compilation, AnalyzerConfigOptions options)
    {
        var names = new Dictionary<string, string>(StringComparer.Ordinal);

        var assemblyName = compilation.AssemblyName;
        if (string.IsNullOrWhiteSpace(assemblyName))
        {
            return names;
        }

        // Only assemblies of the same product family are compared. Without this, declaring a type
        // named Task, Timer or Settings would collide with the framework and the rule would be noise.
        var scope = GetScope(options) ?? FirstSegment(assemblyName!);

        // Sorted by name so that, when two referenced assemblies carry the same type name, the one
        // reported stays the same from one compilation to the next.
        var assemblies = new List<IAssemblySymbol>();
        foreach (var reference in compilation.References)
        {
            if (compilation.GetAssemblyOrModuleSymbol(reference) is IAssemblySymbol assembly
                && BelongsToScope(assembly.Name, scope))
            {
                assemblies.Add(assembly);
            }
        }

        assemblies.Sort((left, right) => string.CompareOrdinal(left.Name, right.Name));
        foreach (var assembly in assemblies)
        {
            CollectPublicTypes(assembly.GlobalNamespace, assembly.Name, names);
        }

        return names;
    }

    public DuplicateTypeNameViolation? CheckType(
        INamedTypeSymbol type,
        Dictionary<string, string> referencedTypeNames,
        IReadOnlyCollection<string> exceptions)
    {
        // Nested types are out of scope: their name is already qualified by the enclosing type.
        if (type.ContainingType is not null)
        {
            return null;
        }

        if (exceptions.Contains(type.Name, StringComparer.Ordinal))
        {
            return null;
        }

        // MetadataName carries the generic arity, so Wrapper<T> and Wrapper are not confused.
        if (!referencedTypeNames.TryGetValue(type.MetadataName, out var declaringAssembly))
        {
            return null;
        }

        var location = type.Locations.FirstOrDefault(candidate => candidate.IsInSource);
        if (location is null)
        {
            return null;
        }

        return new DuplicateTypeNameViolation(type.Name, declaringAssembly, location);
    }

    // Only public types are collected: an internal type of another assembly could not have been
    // reused anyway, so redeclaring one is not duplication.
    private static void CollectPublicTypes(INamespaceSymbol namespaceSymbol, string assemblyName, Dictionary<string, string> names)
    {
        foreach (var type in namespaceSymbol.GetTypeMembers())
        {
            if (type.DeclaredAccessibility == Accessibility.Public && !names.ContainsKey(type.MetadataName))
            {
                names[type.MetadataName] = assemblyName;
            }
        }

        foreach (var nested in namespaceSymbol.GetNamespaceMembers())
        {
            CollectPublicTypes(nested, assemblyName, names);
        }
    }

    private static string? GetScope(AnalyzerConfigOptions options)
        => options.TryGetValue(ScopeOption, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : null;

    private static string FirstSegment(string assemblyName)
    {
        var dot = assemblyName.IndexOf('.');
        return dot > 0 ? assemblyName.Substring(0, dot) : assemblyName;
    }

    private static bool BelongsToScope(string assemblyName, string scope)
        => assemblyName.Equals(scope, StringComparison.OrdinalIgnoreCase)
           || assemblyName.StartsWith(scope + ".", StringComparison.OrdinalIgnoreCase);
}
