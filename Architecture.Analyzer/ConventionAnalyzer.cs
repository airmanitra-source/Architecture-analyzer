using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Architecture.Analyzer.Models;
using Architecture.Analyzer.Services.ConventionRule;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Architecture.Analyzer;

// Conventions the project already follows, learned from the code instead of declared in
// configuration: "17 of the 18 *Provider types live in Infrastructure/Providers". Learned on every
// type, enforced only on the types this change touches — the legacy defines the norm, the new code
// must follow it. Purely additive to ARCH001/ARCH003: whatever they govern explicitly is left to them.
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ConventionAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "ARCH024";

    private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.ConventionTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.ConventionMessageFormat), Resources.ResourceManager, typeof(Resources));
    private const string Category = "Architecture";

    private readonly IConventionRuleService _ruleService;

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        Title,
        MessageFormat,
        Category,
        // A warning: the majority can be the mistake, and only a human can tell. The message shows
        // its evidence ("17 of 18") precisely so that call takes ten seconds.
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        customTags: [WellKnownDiagnosticTags.CompilationEnd]);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public ConventionAnalyzer()
        : this(new ConventionRuleService())
    {
    }

    internal ConventionAnalyzer(IConventionRuleService ruleService)
    {
        _ruleService = ruleService;
    }

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(startContext =>
        {
            var changedFiles = _ruleService.ReadChangedFiles(
                startContext.Options.AdditionalFiles,
                startContext.Options.AnalyzerConfigOptionsProvider,
                startContext.CancellationToken);

            if (changedFiles.Count == 0)
            {
                return; // nothing is new, so nothing can deviate.
            }

            var firstTree = startContext.Compilation.SyntaxTrees.FirstOrDefault();
            if (firstTree is null)
            {
                return;
            }

            var settings = _ruleService.GetSettings(startContext.Options.AnalyzerConfigOptionsProvider.GetOptions(firstTree));
            var types = new ConcurrentBag<TypeTraits>();

            startContext.RegisterSymbolAction(
                symbolContext =>
                {
                    var traits = _ruleService.Collect((INamedTypeSymbol)symbolContext.Symbol);
                    if (traits is not null)
                    {
                        types.Add(traits);
                    }
                },
                SymbolKind.NamedType);

            startContext.RegisterCompilationEndAction(endContext =>
            {
                var all = types.ToList();
                var conventions = _ruleService.Infer(all, settings);
                foreach (var violation in _ruleService.Check(all, conventions, changedFiles))
                {
                    endContext.ReportDiagnostic(Diagnostic.Create(
                        Rule,
                        violation.Location,
                        violation.Convention.Total,
                        violation.Convention.Suffix,
                        violation.Convention.Matching,
                        violation.Convention.Trait,
                        ForDisplay(violation.Convention.Trait, violation.Convention.Expected),
                        violation.TypeName,
                        ForDisplay(violation.Convention.Trait, violation.Actual)));
                }
            });
        });
    }

    // Folders are compared as full paths but read as their last two segments.
    private static string ForDisplay(string trait, string value)
    {
        if (trait != ConventionRuleService.FolderTrait || value.Length == 0)
        {
            return value.Length == 0 ? "(none)" : value;
        }

        var parent = Path.GetFileName(Path.GetDirectoryName(value) ?? string.Empty);
        var leaf = Path.GetFileName(value);
        return parent.Length == 0 ? leaf : parent + "/" + leaf;
    }
}
