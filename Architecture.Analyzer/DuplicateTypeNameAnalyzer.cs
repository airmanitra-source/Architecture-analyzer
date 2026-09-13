using System.Collections.Generic;
using System.Collections.Immutable;
using Architecture.Analyzer.Services.DuplicateTypeNameRule;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Linq;

namespace Architecture.Analyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DuplicateTypeNameAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "ARCH020";

    private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.DuplicateTypeNameTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.DuplicateTypeNameMessageFormat), Resources.ResourceManager, typeof(Resources));
    private const string Category = "Architecture";

    private readonly IDuplicateTypeNameRuleService _ruleService;

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        Title,
        MessageFormat,
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public DuplicateTypeNameAnalyzer()
        : this(new DuplicateTypeNameRuleService())
    {
    }

    internal DuplicateTypeNameAnalyzer(IDuplicateTypeNameRuleService ruleService)
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

            var options = startContext.Options.AnalyzerConfigOptionsProvider.GetOptions(firstTree);

            // Built once per compilation: the set of type names this project could already have used.
            var referencedTypeNames = _ruleService.GetReferencedTypeNames(startContext.Compilation, options);
            if (referencedTypeNames.Count == 0)
            {
                return;
            }

            var exceptions = _ruleService.GetExceptions(options);

            startContext.RegisterSymbolAction(
                symbolContext => AnalyzeNamedType(symbolContext, referencedTypeNames, exceptions),
                SymbolKind.NamedType);
        });
    }

    private void AnalyzeNamedType(
        SymbolAnalysisContext context,
        Dictionary<string, string> referencedTypeNames,
        IReadOnlyCollection<string> exceptions)
    {
        var violation = _ruleService.CheckType((INamedTypeSymbol)context.Symbol, referencedTypeNames, exceptions);
        if (violation is null)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            Rule,
            violation.Location,
            violation.TypeName,
            violation.ReferencedAssembly));
    }
}
