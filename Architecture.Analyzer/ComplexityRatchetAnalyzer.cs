using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Architecture.Analyzer.Services.ComplexityRatchetRule;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Architecture.Analyzer;

// The ratchet: a method's cyclomatic complexity may stay equal or go down, never up. Unlike an
// absolute ceiling, this needs no arbitrary threshold and no baseline file — it only ever looks at
// what this change makes worse, so it can be switched on as-is on a legacy code base.
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ComplexityRatchetAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "ARCH022";

    private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.ComplexityRatchetTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.ComplexityRatchetMessageFormat), Resources.ResourceManager, typeof(Resources));
    private const string Category = "Architecture";

    private readonly IComplexityRatchetRuleService _ruleService;

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        Title,
        MessageFormat,
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public ComplexityRatchetAnalyzer()
        : this(new ComplexityRatchetRuleService())
    {
    }

    internal ComplexityRatchetAnalyzer(IComplexityRatchetRuleService ruleService)
    {
        _ruleService = ruleService;
    }

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(startContext =>
        {
            // The HEAD version of every changed file, supplied as AdditionalFiles by the shipped
            // MSBuild target. No baseline, no rule: an unchanged file cannot have regressed.
            var baseline = _ruleService.BuildBaseline(
                startContext.Options.AdditionalFiles,
                startContext.Options.AnalyzerConfigOptionsProvider,
                startContext.CancellationToken);

            if (baseline.Count == 0)
            {
                return;
            }

            var firstTree = startContext.Compilation.SyntaxTrees.FirstOrDefault();
            var allowedIncrease = firstTree is null
                ? 0
                : _ruleService.GetAllowedIncrease(startContext.Options.AnalyzerConfigOptionsProvider.GetOptions(firstTree));

            startContext.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeMethod(nodeContext, baseline, allowedIncrease),
                SyntaxKind.MethodDeclaration);
        });
    }

    private void AnalyzeMethod(SyntaxNodeAnalysisContext context, Dictionary<string, int> baseline, int allowedIncrease)
    {
        var violation = _ruleService.Check((MethodDeclarationSyntax)context.Node, baseline, allowedIncrease);
        if (violation is null)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            Rule,
            violation.Location,
            violation.MethodName,
            violation.Before,
            violation.After));
    }
}
