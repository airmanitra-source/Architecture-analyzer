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

    // Same regression, but in a file the team keeps touching. Separate id so both can be tuned
    // independently, and an error rather than a warning: this is where debt actually costs money.
    public const string HotspotDiagnosticId = "ARCH023";

    private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.ComplexityRatchetTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.ComplexityRatchetMessageFormat), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableString HotspotTitle = new LocalizableResourceString(nameof(Resources.ComplexityHotspotTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableString HotspotMessageFormat = new LocalizableResourceString(nameof(Resources.ComplexityHotspotMessageFormat), Resources.ResourceManager, typeof(Resources));
    private const string Category = "Architecture";

    private readonly IComplexityRatchetRuleService _ruleService;

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        Title,
        MessageFormat,
        Category,
        // A warning, not an error: unlike a duplicate or a name collision, a complexity increase is
        // sometimes the legitimate evolution of the software. Blocking it would push people to split
        // methods artificially just to satisfy the tool — worse code than the one being prevented.
        // The ratchet has no backlog, so a warning stays rare and therefore visible.
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor HotspotRule = new(
        HotspotDiagnosticId,
        HotspotTitle,
        HotspotMessageFormat,
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule, HotspotRule);

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
            var options = firstTree is null ? null : startContext.Options.AnalyzerConfigOptionsProvider.GetOptions(firstTree);
            var allowedIncrease = options is null ? 0 : _ruleService.GetAllowedIncrease(options);

            // Churn does not gate anything on its own — it only selects which files get the strict
            // treatment. It can only ever grow, so it could never be ratcheted itself.
            var churn = _ruleService.BuildChurn(
                startContext.Options.AdditionalFiles,
                startContext.Options.AnalyzerConfigOptionsProvider,
                startContext.CancellationToken);
            var hotspotThreshold = options is null ? int.MaxValue : _ruleService.GetHotspotThreshold(churn, options);

            startContext.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeMethod(nodeContext, baseline, allowedIncrease, churn, hotspotThreshold),
                SyntaxKind.MethodDeclaration);
        });
    }

    private void AnalyzeMethod(
        SyntaxNodeAnalysisContext context,
        Dictionary<(string Type, string Method, int Arity), int> baseline,
        int allowedIncrease,
        Dictionary<string, int> churn,
        int hotspotThreshold)
    {
        var violation = _ruleService.Check((MethodDeclarationSyntax)context.Node, baseline, allowedIncrease);
        if (violation is null)
        {
            return;
        }

        var hotspotChurn = _ruleService.GetHotspotChurn(context.Node.SyntaxTree.FilePath, churn, hotspotThreshold);
        if (hotspotChurn > 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                HotspotRule,
                violation.Location,
                violation.MethodName,
                violation.Before,
                violation.After,
                hotspotChurn));
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
