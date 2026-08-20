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
public sealed class SqlCommandAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "RGPD001";
    public const string SqlConcatenationDiagnosticId = "RGPD002";

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

    private static readonly Regex SqlTargetNameRegex = new(Constants.SqlTargetNameRegexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule, SqlConcatenationRule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeObjectCreation, SyntaxKind.ObjectCreationExpression);
        context.RegisterSyntaxNodeAction(AnalyzeSqlStringComposition, SyntaxKind.VariableDeclarator, SyntaxKind.SimpleAssignmentExpression);
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
}
