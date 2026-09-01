using Architecture.Analyzer.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Architecture.Analyzer.Services.NameLengthRule;

internal sealed class NameLengthRuleService : INameLengthRuleService
{
    private const string AnalyzerConfigPrefix = "architecture_analyzer.";
    private const string MinVariableNameLengthOption = AnalyzerConfigPrefix + "min_variable_name_length";
    private const string MinClassNameLengthOption = AnalyzerConfigPrefix + "min_class_name_length";
    private const string MinMethodNameLengthOption = AnalyzerConfigPrefix + "min_method_name_length";

    public int? GetMinVariableNameLength(AnalyzerConfigOptions options)
        => GetPositiveIntOption(options, MinVariableNameLengthOption);

    public int? GetMinClassNameLength(AnalyzerConfigOptions options)
        => GetPositiveIntOption(options, MinClassNameLengthOption);

    public int? GetMinMethodNameLength(AnalyzerConfigOptions options)
        => GetPositiveIntOption(options, MinMethodNameLengthOption);

    public NameLengthViolation? GetVariableNameViolation(SyntaxToken identifier, int minLength)
        => CreateViolation(identifier, minLength);

    public NameLengthViolation? GetClassNameViolation(ClassDeclarationSyntax declaration, int minLength)
        => CreateViolation(declaration.Identifier, minLength);

    public NameLengthViolation? GetMethodNameViolation(MethodDeclarationSyntax declaration, int minLength)
        => CreateViolation(declaration.Identifier, minLength);

    // A method is exempt only when its name is dictated by an EXTERNAL contract:
    // a method overriding a base member, or implementing an interface member,
    // that is declared in a referenced assembly (NuGet, Microsoft, etc.).
    // Contracts defined by the application itself are still checked — including
    // the app's own overrides and interface implementations — because the app
    // freely chose those names.
    public bool IsExemptFromMethodNameRule(IMethodSymbol? method)
    {
        if (method is null)
        {
            return false;
        }

        if (method.IsOverride)
        {
            var introducedBy = GetOverriddenRoot(method);
            return introducedBy is not null && IsExternal(introducedBy);
        }

        foreach (var explicitImplementation in method.ExplicitInterfaceImplementations)
        {
            if (IsExternal(explicitImplementation))
            {
                return true;
            }
        }

        return ImplementsExternalInterfaceMember(method);
    }

    // A symbol with no declaring syntax in the current compilation comes from a
    // referenced assembly (metadata) — i.e. it is external to this project.
    private static bool IsExternal(ISymbol symbol)
        => symbol.DeclaringSyntaxReferences.IsDefaultOrEmpty;

    // Walks the override chain up to the member that first introduced it
    // (the virtual/abstract declaration), whose assembly owns the name.
    private static IMethodSymbol? GetOverriddenRoot(IMethodSymbol method)
    {
        var current = method.OverriddenMethod;
        while (current?.OverriddenMethod is not null)
        {
            current = current.OverriddenMethod;
        }

        return current;
    }

    private static bool ImplementsExternalInterfaceMember(IMethodSymbol method)
    {
        var containingType = method.ContainingType;
        if (containingType is null)
        {
            return false;
        }

        foreach (var interfaceType in containingType.AllInterfaces)
        {
            foreach (var member in interfaceType.GetMembers())
            {
                if (member is not IMethodSymbol)
                {
                    continue;
                }

                var implementation = containingType.FindImplementationForInterfaceMember(member);
                if (SymbolEqualityComparer.Default.Equals(implementation, method) && IsExternal(member))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static NameLengthViolation? CreateViolation(SyntaxToken identifier, int minLength)
    {
        var name = identifier.ValueText;
        var length = name.Length;
        if (length >= minLength)
        {
            return null;
        }

        return new NameLengthViolation(name, length, minLength, identifier.GetLocation());
    }

    private static int? GetPositiveIntOption(AnalyzerConfigOptions options, string key)
        => TryGetIntOption(options, key, out var value) && value > 0 ? value : null;

    private static bool TryGetIntOption(AnalyzerConfigOptions options, string key, out int value)
    {
        value = 0;
        if (!options.TryGetValue(key, out var configuredValue) || string.IsNullOrWhiteSpace(configuredValue))
        {
            return false;
        }

        return int.TryParse(configuredValue.Trim(), out value);
    }
}
