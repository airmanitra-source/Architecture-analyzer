using System;
using System.Collections.Generic;
using Architecture.Analyzer.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Architecture.Analyzer.Services.MethodArgumentTypeBarrierRule;

internal sealed class MethodArgumentTypeBarrierRuleService : IMethodArgumentTypeBarrierRuleService
{
    private const string AnalyzerConfigPrefix = "architecture_analyzer.";
    private const string BarrierOption = AnalyzerConfigPrefix + "method_argument_type_barrier";

    // Parses "ClassPattern>ForbiddenTypePattern" entries. Each side is a NamePattern
    // ("*Module" suffix, "Module*" prefix, "Module" whole name, "*Module*" substring).
    // Multiple rules are separated by ';' (consistent with every other multi-rule key)
    // or ',' (also accepted).
    public List<Models.MethodArgumentTypeBarrierRule> GetRules(AnalyzerConfigOptions options)
    {
        if (!options.TryGetValue(BarrierOption, out var value) || string.IsNullOrWhiteSpace(value))
        {
            return new List<Models.MethodArgumentTypeBarrierRule>();
        }

        var rules = new List<Models.MethodArgumentTypeBarrierRule>();
        foreach (var entry in value.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = entry.Trim();
            var separatorIndex = trimmed.IndexOf('>');
            if (separatorIndex <= 0 || separatorIndex >= trimmed.Length - 1)
            {
                continue;
            }

            var classPattern = NamePattern.Parse(trimmed.Substring(0, separatorIndex));
            var forbiddenTypePattern = NamePattern.Parse(trimmed.Substring(separatorIndex + 1));
            if (classPattern is null || forbiddenTypePattern is null)
            {
                continue;
            }

            rules.Add(new Models.MethodArgumentTypeBarrierRule(classPattern.Value, forbiddenTypePattern.Value));
        }

        return rules;
    }

    // The rules whose ClassPattern matches the declaring type — computed once per method
    // so the analyzer can skip resolving parameter types for types that match nothing.
    public List<Models.MethodArgumentTypeBarrierRule> GetRulesForClass(string className, List<Models.MethodArgumentTypeBarrierRule> rules)
    {
        var matching = new List<Models.MethodArgumentTypeBarrierRule>();
        foreach (var rule in rules)
        {
            if (rule.ClassPattern.Matches(className))
            {
                matching.Add(rule);
            }
        }

        return matching;
    }

    public Models.MethodArgumentTypeBarrierViolation? CheckParameterType(
        string methodName,
        string className,
        ITypeSymbol parameterType,
        List<Models.MethodArgumentTypeBarrierRule> classRules,
        Location location)
    {
        var parameterTypeName = parameterType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        foreach (var rule in classRules)
        {
            if (!ContainsForbiddenType(parameterType, rule.ForbiddenTypePattern))
            {
                continue;
            }

            return new Models.MethodArgumentTypeBarrierViolation(
                methodName,
                className,
                parameterTypeName,
                rule.ClassPattern.Raw,
                rule.ForbiddenTypePattern.Raw,
                location);
        }

        return null;
    }

    // Whether the pattern matches this type, looking through arrays and generic type
    // arguments (so List<CustomerDataModel> and CustomerDataModel[] are caught, not just
    // a bare CustomerDataModel). Only nominal types are matched by name: a generic type
    // parameter (a method's own 'T'), a pointer, 'dynamic', etc. are placeholders, never
    // a forbidden argument type, so they are ignored.
    private static bool ContainsForbiddenType(ITypeSymbol type, NamePattern pattern)
    {
        switch (type)
        {
            case IArrayTypeSymbol array:
                return ContainsForbiddenType(array.ElementType, pattern);

            case INamedTypeSymbol named:
                if (pattern.Matches(named.Name))
                {
                    return true;
                }

                foreach (var argument in named.TypeArguments)
                {
                    if (ContainsForbiddenType(argument, pattern))
                    {
                        return true;
                    }
                }

                return false;

            default:
                return false;
        }
    }
}
