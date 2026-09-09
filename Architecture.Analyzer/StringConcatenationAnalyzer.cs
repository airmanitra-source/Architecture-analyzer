using System.Collections.Immutable;
using Architecture.Analyzer.Services.StringConcatenationRule;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Architecture.Analyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class StringConcatenationAnalyzer : DiagnosticAnalyzer
{
    public const string ConcatenationDiagnosticId = "ARCH017";
    public const string InterpolationDiagnosticId = "ARCH018";

    private const string Category = "Architecture";

    private static readonly LocalizableString ConcatenationTitle = new LocalizableResourceString(nameof(Resources.StringConcatenationTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableString ConcatenationMessageFormat = new LocalizableResourceString(nameof(Resources.StringConcatenationMessageFormat), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableString InterpolationTitle = new LocalizableResourceString(nameof(Resources.StringInterpolationTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableString InterpolationMessageFormat = new LocalizableResourceString(nameof(Resources.StringInterpolationMessageFormat), Resources.ResourceManager, typeof(Resources));

    private readonly IStringConcatenationRuleService _ruleService;

    private static readonly DiagnosticDescriptor ConcatenationRule = new(
        ConcatenationDiagnosticId,
        ConcatenationTitle,
        ConcatenationMessageFormat,
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor InterpolationRule = new(
        InterpolationDiagnosticId,
        InterpolationTitle,
        InterpolationMessageFormat,
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(ConcatenationRule, InterpolationRule);

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
        context.RegisterOperationAction(
            AnalyzeOperation,
            OperationKind.Binary,
            OperationKind.CompoundAssignment,
            OperationKind.InterpolatedString);
    }

    private void AnalyzeOperation(OperationAnalysisContext context)
    {
        var operation = context.Operation;

        // ARCH018 - string interpolation. Each $"..." is a single operation (holes are parts),
        // so there is no chain to de-duplicate.
        if (_ruleService.IsForbiddenInterpolation(operation))
        {
            context.ReportDiagnostic(Diagnostic.Create(InterpolationRule, operation.Syntax.GetLocation()));
            return;
        }

        // ARCH017 - '+' / '+=' concatenation. Report once per chain: skip when the parent is
        // itself a forbidden concatenation (the inner 'a + b' of 'a + b + c', or of 's += a + b').
        if (_ruleService.IsForbiddenConcatenation(operation)
            && !_ruleService.IsForbiddenConcatenation(operation.Parent))
        {
            context.ReportDiagnostic(Diagnostic.Create(ConcatenationRule, operation.Syntax.GetLocation()));
        }
    }
}
