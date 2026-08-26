using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Architecture.Analyzer.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Architecture.Analyzer.Services.PropertyOrderRule;

internal sealed class ClassPropertyOrderRuleService : IClassPropertyOrderRuleService
{
    private static readonly string[] IdentifierSuffixes = ["Id", "Ids", "IDs"];

    public List<ClassPropertyOrderViolation> GetClassPropertyOrderViolations(ClassDeclarationSyntax declaration, CancellationToken cancellationToken)
    {
        var violations = new List<ClassPropertyOrderViolation>();
        string? previousPropertyName = null;

        foreach (var property in declaration.Members.OfType<PropertyDeclarationSyntax>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (IsIdentifierProperty(property.Identifier.ValueText))
            {
                continue;
            }

            var currentPropertyName = property.Identifier.ValueText;
            if (previousPropertyName is not null
                && string.Compare(currentPropertyName, previousPropertyName, StringComparison.Ordinal) < 0)
            {
                violations.Add(new ClassPropertyOrderViolation(
                    declaration.Identifier.ValueText,
                    previousPropertyName,
                    currentPropertyName,
                    property.Identifier.GetLocation()));
            }

            previousPropertyName = currentPropertyName;
        }

        return violations;
    }

    private static bool IsIdentifierProperty(string propertyName)
        => IdentifierSuffixes.Any(suffix => propertyName.EndsWith(suffix, StringComparison.Ordinal));
}