using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Rgpd.Analyzer.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RgpdSqlCommandAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "RGPD001";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Use SqlCommandFactory for RGPD filtering",
        "Erreur RGPD001 : Pour respecter la conformité RGPD, vous devez utiliser 'SqlCommandFactory.CreateFilterCommand' au lieu de 'new SqlCommand()'.",
        "Security",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeObjectCreation, SyntaxKind.ObjectCreationExpression);
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

        var isSqlCommand = typeSymbol.Name == "SqlCommand"
            && (typeSymbol.ContainingNamespace.ToDisplayString() == "Microsoft.Data.SqlClient"
                || typeSymbol.ContainingNamespace.ToDisplayString() == "System.Data.SqlClient");

        var isDbCommandImplementation = typeSymbol.AllInterfaces.Any(i => i.Name == "IDbCommand" && i.ContainingNamespace.ToDisplayString() == "System.Data");

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

        return containingType.Name.Contains("SqlCommandFactory", StringComparison.Ordinal);
    }
}
