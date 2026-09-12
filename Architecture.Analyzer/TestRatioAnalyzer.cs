using System.Collections.Immutable;
using System.Linq;
using Architecture.Analyzer.Services.TestRatioRule;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Architecture.Analyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TestRatioAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "ARCH021";

    private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.TestRatioTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.TestRatioMessageFormat), Resources.ResourceManager, typeof(Resources));
    private const string Category = "Architecture";

    private readonly ITestRatioRuleService _ruleService;

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        Title,
        MessageFormat,
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: [WellKnownDiagnosticTags.CompilationEnd]);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public TestRatioAnalyzer()
        : this(new TestRatioRuleService())
    {
    }

    internal TestRatioAnalyzer(ITestRatioRuleService ruleService)
    {
        _ruleService = ruleService;
    }

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(startContext =>
        {
            var firstTree = startContext.Compilation.SyntaxTrees.FirstOrDefault();
            if (firstTree is null)
            {
                return;
            }

            var settings = _ruleService.GetSettings(
                startContext.Options.AnalyzerConfigOptionsProvider.GetOptions(firstTree),
                startContext.Options.AnalyzerConfigOptionsProvider.GlobalOptions);

            if (!settings.IsEnabled)
            {
                return;
            }

            startContext.RegisterCompilationEndAction(endContext =>
            {
                var violation = _ruleService.GetViolation(endContext.Compilation, settings, endContext.CancellationToken);
                if (violation is null)
                {
                    return;
                }

                endContext.ReportDiagnostic(Diagnostic.Create(
                    Rule,
                    violation.Location,
                    violation.ProductionLines,
                    violation.TestLines,
                    violation.ActualPercent,
                    violation.RequiredPercent));
            });
        });
    }
}
