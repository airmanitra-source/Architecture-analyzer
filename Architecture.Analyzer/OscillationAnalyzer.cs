using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Architecture.Analyzer.Services.OscillationRule;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Architecture.Analyzer;

// Change oscillation: this change re-adds a block of code that a recent commit removed — the signature
// of an agent going in circles. It only ever states the fact (which commit, how long ago); it makes no
// judgement about whether the re-add is a deliberate revert or thrashing, so it is a warning, not an
// error. Temporal by nature: it compares against git history, which a stateless pattern matcher cannot.
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class OscillationAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "ARCH027";

    // Overlapping windows of one re-added block all match; collapse reports closer than this.
    private const int BlockSpacing = 3;

    private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.OscillationTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.OscillationMessageFormat), Resources.ResourceManager, typeof(Resources));
    private const string Category = "Architecture";

    private readonly IOscillationRuleService _ruleService;

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        Title,
        MessageFormat,
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public OscillationAnalyzer()
        : this(new OscillationRuleService())
    {
    }

    internal OscillationAnalyzer(IOscillationRuleService ruleService)
    {
        _ruleService = ruleService;
    }

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(startContext =>
        {
            var removed = _ruleService.ReadRemoved(
                startContext.Options.AdditionalFiles,
                startContext.Options.AnalyzerConfigOptionsProvider,
                startContext.CancellationToken);
            if (removed.Count == 0)
            {
                return; // the rule is off, or no history was supplied.
            }

            var added = _ruleService.ReadAdded(
                startContext.Options.AdditionalFiles,
                startContext.Options.AnalyzerConfigOptionsProvider,
                startContext.CancellationToken);
            if (added.Count == 0)
            {
                return; // this change adds nothing that could be a re-add.
            }

            startContext.RegisterSyntaxTreeAction(treeContext => AnalyzeTree(treeContext, removed, added));
        });
    }

    private void AnalyzeTree(
        SyntaxTreeAnalysisContext context,
        Dictionary<string, (string Sha, string Subject, int Ago)> removed,
        Dictionary<string, List<(int Line, string Hash)>> added)
    {
        if (!added.TryGetValue(context.Tree.FilePath, out var windows))
        {
            return;
        }

        var text = context.Tree.GetText(context.CancellationToken);
        var lastReported = -BlockSpacing;

        foreach (var window in windows.OrderBy(w => w.Line))
        {
            if (!removed.TryGetValue(window.Hash, out var origin))
            {
                continue;
            }

            if (window.Line - lastReported < BlockSpacing)
            {
                continue; // same re-added block as the previous report.
            }

            var index = window.Line - 1;
            if (index < 0 || index >= text.Lines.Count)
            {
                continue;
            }

            lastReported = window.Line;
            context.ReportDiagnostic(Diagnostic.Create(
                Rule,
                Location.Create(context.Tree, text.Lines[index].Span),
                origin.Sha,
                origin.Subject,
                origin.Ago));
        }
    }
}
