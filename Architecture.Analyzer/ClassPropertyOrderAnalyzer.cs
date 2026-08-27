using System;
using System.Collections.Immutable;
using System.Linq;
using Architecture.Analyzer.Services.PropertyOrderRule;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Architecture.Analyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ClassPropertyOrderAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "ARCH005";

    private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.ClassPropertyOrderTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.ClassPropertyOrderMessageFormat), Resources.ResourceManager, typeof(Resources));
    private const string Category = "Architecture";
    private static readonly string[] DtoSuffixes = ["BusinessModel", "DataModel", "ViewModel"];

    private readonly IClassPropertyOrderRuleService _dtoPropertyOrderRuleService;

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        Title,
        MessageFormat,
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public ClassPropertyOrderAnalyzer()
        : this(new ClassPropertyOrderRuleService())
    {
    }

    internal ClassPropertyOrderAnalyzer(IClassPropertyOrderRuleService dtoPropertyOrderRuleService)
    {
        _dtoPropertyOrderRuleService = dtoPropertyOrderRuleService;
    }

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeClass, SyntaxKind.ClassDeclaration);
    }

    private void AnalyzeClass(SyntaxNodeAnalysisContext context)
    {
        var declaration = (ClassDeclarationSyntax)context.Node;
        if (!IsDtoType(declaration.Identifier.ValueText))
        {
            return;
        }

        var violations = _dtoPropertyOrderRuleService.GetClassPropertyOrderViolations(declaration, context.CancellationToken);
        foreach (var violation in violations)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Rule,
                violation.Location,
                violation.DtoTypeName,
                violation.CurrentPropertyName,
                violation.PreviousPropertyName));
        }
    }

    private static bool IsDtoType(string typeName)
    {
        foreach (var suffix in DtoSuffixes)
        {
            if (typeName.EndsWith(suffix, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}