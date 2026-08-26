using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Architecture.Analyzer.Models;
using Architecture.Analyzer.Services.ModelDirectoryRule;
using Microsoft.CodeAnalysis;

namespace Architecture.Analyzer.Services.ModuleProviderRule;

internal sealed class ModuleProviderRuleService : IModuleProviderRuleService
{
    private const string ModuleSuffix = "Module";
    private const string ProvidersFolder = "Models/Data/Providers";

    private readonly IModelDirectoryRuleService _modelDirectoryRuleService;

    public ModuleProviderRuleService(IModelDirectoryRuleService modelDirectoryRuleService)
    {
        _modelDirectoryRuleService = modelDirectoryRuleService;
    }

    public ProviderImplementationViolation? GetProviderImplementionViolation(
        INamedTypeSymbol type,
        List<INamedTypeSymbol> providerInterfaces,
        CancellationToken cancellationToken)
    {
        if (type.TypeKind is TypeKind.Interface or TypeKind.Error)
        {
            return null;
        }

        foreach (var providerInterface in providerInterfaces)
        {
            if (!type.AllInterfaces.Contains(providerInterface, SymbolEqualityComparer.Default))
            {
                continue;
            }

            var location = GetDeclarationLocation(type, cancellationToken);
            if (location is null)
            {
                continue;
            }

            return new ProviderImplementationViolation(providerInterface.Name, type.Name, location);
        }

        return null;
    }

    public List<INamedTypeSymbol> GetProviderInterfaces(Compilation compilation, CancellationToken cancellationToken)
        => GetAllNamedTypes(compilation.Assembly.GlobalNamespace)
            .Where(type => type.TypeKind == TypeKind.Interface && IsInProvidersFolder(type, cancellationToken))
            .ToList();

    public bool IsModuleProject(string? assemblyName)
        => !string.IsNullOrWhiteSpace(assemblyName) && assemblyName.EndsWith(ModuleSuffix, StringComparison.Ordinal);

    private bool IsInProvidersFolder(INamedTypeSymbol symbol, CancellationToken cancellationToken)
    {
        foreach (var declaration in symbol.DeclaringSyntaxReferences)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_modelDirectoryRuleService.IsTypeInExpectedFolder(declaration.SyntaxTree.FilePath, ProvidersFolder))
            {
                return true;
            }
        }

        return false;
    }

    public Location? GetProjectLocation(Compilation compilation, CancellationToken cancellationToken)
    {
        var syntaxTree = compilation.SyntaxTrees.FirstOrDefault();
        if (syntaxTree is null)
        {
            return null;
        }

        var root = syntaxTree.GetRoot(cancellationToken);
        return root.GetLocation();
    }

    private static Location? GetDeclarationLocation(INamedTypeSymbol type, CancellationToken cancellationToken)
    {
        var declaration = type.DeclaringSyntaxReferences.FirstOrDefault();
        if (declaration is null)
        {
            return null;
        }

        return declaration.GetSyntax(cancellationToken).GetLocation();
    }

    private static IEnumerable<INamedTypeSymbol> GetAllNamedTypes(INamespaceSymbol namespaceSymbol)
    {
        foreach (var nestedNamespace in namespaceSymbol.GetNamespaceMembers())
        {
            foreach (var nestedType in GetAllNamedTypes(nestedNamespace))
            {
                yield return nestedType;
            }
        }

        foreach (var type in namespaceSymbol.GetTypeMembers())
        {
            foreach (var nestedType in GetTypeAndNestedTypes(type))
            {
                yield return nestedType;
            }
        }
    }

    private static IEnumerable<INamedTypeSymbol> GetTypeAndNestedTypes(INamedTypeSymbol type)
    {
        yield return type;

        foreach (var nestedType in type.GetTypeMembers())
        {
            foreach (var child in GetTypeAndNestedTypes(nestedType))
            {
                yield return child;
            }
        }
    }
}