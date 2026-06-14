using System;
using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Rgpd.Analyzer.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RgpdSqlCommandAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "RGPD001";
    public const string SqlConcatenationDiagnosticId = "RGPD002";
    public const string PersonalDataPolicyDiagnosticId = "RGPD003";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        Constants.UseSqlCommandFactoryTitle,
        Constants.UseSqlCommandFactoryMessage,
        Constants.SecurityCategory,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor SqlConcatenationRule = new(
        SqlConcatenationDiagnosticId,
        Constants.AvoidSqlConcatenationTitle,
        Constants.AvoidSqlConcatenationMessage,
        Constants.SecurityCategory,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor PersonalDataPolicyRule = new(
        PersonalDataPolicyDiagnosticId,
        Constants.PersonalDataPolicyTitle,
        Constants.PersonalDataPolicyMessage,
        Constants.SecurityCategory,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly Regex SqlTargetNameRegex = new(Constants.SqlTargetNameRegexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule, SqlConcatenationRule, PersonalDataPolicyRule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeObjectCreation, SyntaxKind.ObjectCreationExpression);
        context.RegisterSyntaxNodeAction(AnalyzeSqlStringComposition, SyntaxKind.VariableDeclarator, SyntaxKind.SimpleAssignmentExpression);
        context.RegisterSyntaxNodeAction(AnalyzePersonalDataAttribute, SyntaxKind.Attribute);
    }

    private static void AnalyzeObjectCreation(SyntaxNodeAnalysisContext context)
    {
        var creation = (ObjectCreationExpressionSyntax)context.Node;
        var typeSymbol = context.SemanticModel.GetSymbolInfo(creation.Type, context.CancellationToken).Symbol as INamedTypeSymbol;
        if (typeSymbol is null)
        {
            return;
        }

        if (IsInsideSqlCommandFactory(context))
        {
            return;
        }

        var isSqlCommand = typeSymbol.Name == Constants.SqlCommand
            && (typeSymbol.ContainingNamespace.ToDisplayString() == Constants.MicrosoftDataSqlClientNamespace
                || typeSymbol.ContainingNamespace.ToDisplayString() == Constants.SystemDataSqlClientNamespace);

        var isDbCommandImplementation = typeSymbol.AllInterfaces.Any(i => i.Name == Constants.DbCommandInterfaceName && i.ContainingNamespace.ToDisplayString() == Constants.SystemDataNamespace);

        if (!isSqlCommand && !isDbCommandImplementation)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, creation.GetLocation()));
    }

    private static bool IsInsideSqlCommandFactory(SyntaxNodeAnalysisContext context)
    {
        var containingType = context.ContainingSymbol?.ContainingType;
        if (containingType is null)
        {
            return false;
        }

        return containingType.Name.Contains(Constants.SqlCommandFactoryName, StringComparison.Ordinal);
    }

    private static void AnalyzeSqlStringComposition(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is VariableDeclaratorSyntax variableDeclarator)
        {
            if (variableDeclarator.Initializer?.Value is null)
            {
                return;
            }

            if (!LooksLikeSqlTarget(variableDeclarator.Identifier.ValueText))
            {
                return;
            }

            if (IsSqlStringComposition(variableDeclarator.Initializer.Value))
            {
                context.ReportDiagnostic(Diagnostic.Create(SqlConcatenationRule, variableDeclarator.Initializer.Value.GetLocation(), variableDeclarator.Identifier.ValueText));
            }

            return;
        }

        if (context.Node is AssignmentExpressionSyntax assignment && assignment.Left is IdentifierNameSyntax leftIdentifier)
        {
            if (!LooksLikeSqlTarget(leftIdentifier.Identifier.ValueText))
            {
                return;
            }

            if (IsSqlStringComposition(assignment.Right))
            {
                context.ReportDiagnostic(Diagnostic.Create(SqlConcatenationRule, assignment.Right.GetLocation(), leftIdentifier.Identifier.ValueText));
            }
        }
    }

    private static bool LooksLikeSqlTarget(string name)
        => SqlTargetNameRegex.IsMatch(name);

    private static bool IsSqlStringComposition(ExpressionSyntax expression)
        => expression is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.AddExpression }
           || expression is InterpolatedStringExpressionSyntax;

    private static void AnalyzePersonalDataAttribute(SyntaxNodeAnalysisContext context)
    {
        var attributeSyntax = (AttributeSyntax)context.Node;
        var attributeType = context.SemanticModel.GetTypeInfo(attributeSyntax, context.CancellationToken).Type;
        if (attributeType?.ToDisplayString() == Constants.PersonalDataAttributeMetadataName)
        {
            _ = PersonalDataPolicyRule;
            return;
        }

        if (attributeType?.ToDisplayString() != Constants.AuthorizedPurposeAttributeMetadataName)
        {
            return;
        }

        if (attributeSyntax.Parent?.Parent is not MethodDeclarationSyntax methodDeclaration)
        {
            return;
        }

        var methodSymbol = context.SemanticModel.GetDeclaredSymbol(methodDeclaration, context.CancellationToken);
        if (methodSymbol is null)
        {
            return;
        }

        var arguments = attributeSyntax.ArgumentList?.Arguments;
        if (arguments is null || arguments.Value.Count < 3)
        {
            return;
        }

        var purpose = GetEnumArgumentName(context, arguments.Value[0].Expression);
        var legalBasis = GetEnumArgumentName(context, arguments.Value[1].Expression);
        var accessRole = GetEnumArgumentName(context, arguments.Value[2].Expression);
        if (purpose is null || legalBasis is null || accessRole is null)
        {
            return;
        }

        if (!IsAllowedPurposeForLegalBasis(purpose, legalBasis))
        {
            var message = string.Format(Constants.LegalBasisPurposeMismatchMessage, purpose, legalBasis);
            context.ReportDiagnostic(Diagnostic.Create(PersonalDataPolicyRule, attributeSyntax.GetLocation(), methodSymbol.Name, message));
            return;
        }

        var accessedPersonalDataProperties = methodDeclaration.Body?
            .DescendantNodes()
            .OfType<MemberAccessExpressionSyntax>()
            .Select(memberAccess => context.SemanticModel.GetSymbolInfo(memberAccess, context.CancellationToken).Symbol)
            .OfType<IPropertySymbol>()
            .Where(property => property.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == Constants.PersonalDataAttributeMetadataName))
            .Distinct(SymbolEqualityComparer.Default)
            .ToArray() ?? [];

        foreach (var property in accessedPersonalDataProperties)
        {
            if (string.Equals(accessRole, Constants.DataOfficerRole, StringComparison.Ordinal))
            {
                continue;
            }

            var matchingPolicyExists = property.GetAttributes()
                .Where(attribute => attribute.AttributeClass?.ToDisplayString() == Constants.PersonalDataAttributeMetadataName)
                .Any(attribute =>
                    string.Equals(GetEnumConstructorArgumentName(attribute, 0), purpose, StringComparison.Ordinal)
                    && string.Equals(GetEnumConstructorArgumentName(attribute, 1), legalBasis, StringComparison.Ordinal)
                    && string.Equals(GetEnumConstructorArgumentName(attribute, 2), accessRole, StringComparison.Ordinal));

            if (!matchingPolicyExists)
            {
                var message = string.Format(Constants.NoMatchingPersonalDataPolicyMessage, property.Name, purpose, legalBasis, accessRole);
                context.ReportDiagnostic(Diagnostic.Create(PersonalDataPolicyRule, attributeSyntax.GetLocation(), methodSymbol.Name, message));
                return;
            }
        }
    }

    private static string? GetEnumArgumentName(SyntaxNodeAnalysisContext context, ExpressionSyntax expression)
    {
        var symbol = context.SemanticModel.GetSymbolInfo(expression, context.CancellationToken).Symbol;
        return symbol?.Name;
    }

    private static string? GetEnumConstructorArgumentName(AttributeData attributeData, int argumentIndex)
    {
        if (attributeData.ConstructorArguments.Length <= argumentIndex)
        {
            return null;
        }

        var argument = attributeData.ConstructorArguments[argumentIndex];
        if (argument.Type?.TypeKind != TypeKind.Enum || argument.Value is null)
        {
            return null;
        }

        var field = argument.Type.GetMembers()
            .OfType<IFieldSymbol>()
            .FirstOrDefault(member => member.HasConstantValue && Equals(member.ConstantValue, argument.Value));

        return field?.Name;
    }

    private static bool IsAllowedPurposeForLegalBasis(string purpose, string legalBasisName)
    {
        return legalBasisName switch
        {
            Constants.ConsentLegalBasisName => string.Equals(purpose, Constants.MarketingPurpose, StringComparison.OrdinalIgnoreCase),
            Constants.LegitimateInterestLegalBasisName => string.Equals(purpose, Constants.AnalyticsPurpose, StringComparison.OrdinalIgnoreCase),
            Constants.ContractLegalBasisName => string.Equals(purpose, Constants.OrderTrackingPurpose, StringComparison.OrdinalIgnoreCase),
            Constants.LegalObligationLegalBasisName => false,
            Constants.VitalInterestsLegalBasisName => false,
            Constants.PublicTaskLegalBasisName => false,
            _ => false
        };
    }
}
