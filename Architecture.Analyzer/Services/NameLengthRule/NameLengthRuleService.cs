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
