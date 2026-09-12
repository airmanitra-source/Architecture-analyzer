using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Architecture.Analyzer.Models;
using Architecture.Analyzer.Services.DuplicateCodeRule;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Architecture.Analyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DuplicateCodeAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "ARCH019";

    private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.DuplicateCodeTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.DuplicateCodeMessageFormat), Resources.ResourceManager, typeof(Resources));
    private const string Category = "Architecture";

    private readonly IDuplicateCodeRuleService _ruleService;

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        Title,
        MessageFormat,
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: [WellKnownDiagnosticTags.CompilationEnd]);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public DuplicateCodeAnalyzer()
        : this(new DuplicateCodeRuleService())
    {
    }

    internal DuplicateCodeAnalyzer(IDuplicateCodeRuleService ruleService)
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
            var (minTokens, similarityPercent) = _ruleService.GetSettings(options);

            // Fingerprints are gathered while the compilation walks the trees, then compared once at
            // the end: duplication is a whole-compilation property, not a per-file one.
            var fingerprints = new ConcurrentBag<MethodFingerprint>();

            startContext.RegisterSyntaxNodeAction(
                nodeContext =>
                {
                    var fingerprint = _ruleService.CreateFingerprint((MethodDeclarationSyntax)nodeContext.Node, minTokens);
                    if (fingerprint is not null)
                    {
                        fingerprints.Add(fingerprint);
                    }
                },
                SyntaxKind.MethodDeclaration);

            startContext.RegisterCompilationEndAction(endContext =>
            {
                var violations = _ruleService.FindDuplicates(new List<MethodFingerprint>(fingerprints), similarityPercent);
                foreach (var violation in violations)
                {
                    endContext.ReportDiagnostic(Diagnostic.Create(
                        Rule,
                        violation.Location,
                        violation.DuplicateName,
                        violation.OriginalName,
                        violation.SimilarityPercent));
                }
            });
        });
    }
}
