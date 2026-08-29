using System;
using System.Collections.Immutable;
using System.Linq;
using Architecture.Analyzer.Services.MethodOrderRule;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Architecture.Analyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ClassMethodOrderAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "ARCH011";

    private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.ClassMethodOrderTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.ClassMethodOrderMessageFormat), Resources.ResourceManager, typeof(Resources));
    private const string Category = "Architecture";

    private readonly IMethodOrderRuleService _methodOrderRuleService;

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        Title,
        MessageFormat,
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public ClassMethodOrderAnalyzer()
        : this(new MethodOrderRuleService())
    {
    }

    internal ClassMethodOrderAnalyzer(IMethodOrderRuleService methodOrderRuleService)
    {
        _methodOrderRuleService = methodOrderRuleService;
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
        
        var violations = _methodOrderRuleService.GetClassMethodOrderViolations(declaration, context.CancellationToken);
        foreach (var violation in violations)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Rule,
                violation.Location,
                violation.ClassTypeName,
                violation.CurrentMethodName,
                violation.PreviousMethodName));
        }
    }
}