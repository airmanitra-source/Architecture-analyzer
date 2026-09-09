using System.Collections.Immutable;
using Architecture.Analyzer.Services.StringConcatenationRule;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Architecture.Analyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class StringConcatenationAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "ARCH017";

    private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.StringConcatenationTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.StringConcatenationMessageFormat), Resources.ResourceManager, typeof(Resources));
    private const string Category = "Architecture";

    private readonly IStringConcatenationRuleService _ruleService;

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        Title,
        MessageFormat,
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public StringConcatenationAnalyzer()
        : this(new StringConcatenationRuleService())
    {
    }

    internal StringConcatenationAnalyzer(IStringConcatenationRuleService ruleService)
    {
        _ruleService = ruleService;
    }

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(AnalyzeOperation, OperationKind.Binary, OperationKind.CompoundAssignment);
    }

    private void AnalyzeOperation(OperationAnalysisContext context)
    {
        var operation = context.Operation;
        if (!_ruleService.IsForbiddenConcatenation(operation))
        {
            return;
        }

        // Report once per concatenation chain: skip when the parent is itself a forbidden
        // concatenation (the inner 'a + b' of 'a + b + c', or the 'a + b' of 's += a + b').
        if (_ruleService.IsForbiddenConcatenation(operation.Parent))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, operation.Syntax.GetLocation()));
    }
}
