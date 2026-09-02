using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Architecture.Analyzer.Models;
using Architecture.Analyzer.Services.ModuleReferenceBarrierRule;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Architecture.Analyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ModuleReferenceBarrierAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "ARCH012";

    private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.ModuleReferenceBarrierTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.ModuleReferenceBarrierMessageFormat), Resources.ResourceManager, typeof(Resources));
    private const string Category = "Architecture";

    private readonly IModuleReferenceBarrierRuleService _ruleService;

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        Title,
        MessageFormat,
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public ModuleReferenceBarrierAnalyzer()
        : this(new ModuleReferenceBarrierRuleService())
    {
    }

    internal ModuleReferenceBarrierAnalyzer(IModuleReferenceBarrierRuleService ruleService)
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
            var rules = _ruleService.GetBarrierRules(options, startContext.Compilation.AssemblyName);

            if (rules.Count == 0)
            {
                return;
            }

            var exemptedFiles = _ruleService.GetExemptedFiles(options);

            startContext.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeIdentifier(nodeContext, rules, exemptedFiles),
                SyntaxKind.IdentifierName,
                SyntaxKind.GenericName,
                SyntaxKind.QualifiedName);
        });
    }

    private void AnalyzeIdentifier(SyntaxNodeAnalysisContext context, List<ModuleReferenceBarrierRule> rules, IReadOnlyCollection<string> exemptedFiles)
    {
        if (_ruleService.IsFileExempted(context.Node.SyntaxTree.FilePath, exemptedFiles))
        {
            return;
        }

        var symbolInfo = context.SemanticModel.GetSymbolInfo(context.Node, context.CancellationToken);
        var referencedSymbol = symbolInfo.Symbol;
        if (referencedSymbol is null)
        {
            return;
        }

        var referencedType = referencedSymbol is INamedTypeSymbol
            ? referencedSymbol
            : referencedSymbol.ContainingType;

        if (referencedType is null)
        {
            return;
        }

        var containingSymbol = context.ContainingSymbol;
        if (containingSymbol is null)
        {
            return;
        }

        var violation = _ruleService.CheckSymbolReference(referencedType, containingSymbol, rules, context.Node.GetLocation());
        if (violation is null)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            Rule,
            violation.Location,
            violation.ReferencingType,
            violation.ReferencedType,
            violation.ForbiddenModule));
    }
}
