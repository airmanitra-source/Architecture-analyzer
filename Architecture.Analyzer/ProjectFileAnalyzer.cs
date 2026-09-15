using System.IO;
using System.Collections.Immutable;
using System.Linq;
using Architecture.Analyzer.Models;
using Architecture.Analyzer.Services.ModelFolderRule;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Architecture.Analyzer;

// Strict per-project file allow-list. Emits ARCH003 (the whitelist rule) — like ModuleProviderAnalyzer,
// it is a second enforcer of the same id, not a new rule. When a project's assembly name matches a
// configured pattern, every source file whose name matches none of the allowed patterns is forbidden.
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ProjectFileAnalyzer : DiagnosticAnalyzer
{
    // Deliberately the same id as ARCH003: this is the whitelist rule, applied to files-per-project.
    public const string DiagnosticId = "ARCH003";

    private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.ModelProjectFileTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.ModelProjectFileMessageFormat), Resources.ResourceManager, typeof(Resources));
    private const string Category = "Architecture";

    private readonly IModelFolderRuleService _ruleService;

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        Title,
        MessageFormat,
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public ProjectFileAnalyzer()
        : this(new ModelFolderRuleService())
    {
    }

    internal ProjectFileAnalyzer(IModelFolderRuleService ruleService)
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

            var rules = _ruleService.ReadProjectFileRules(
                startContext.Options.AnalyzerConfigOptionsProvider.GetOptions(firstTree));
            if (rules.Count == 0)
            {
                return;
            }

            // The first rule whose pattern matches this project's assembly name wins; projects with no
            // matching rule are never restricted.
            var assemblyName = startContext.Compilation.AssemblyName;
            ProjectFileRule? matching = null;
            foreach (var rule in rules)
            {
                if (rule.Project.Matches(assemblyName))
                {
                    matching = rule;
                    break;
                }
            }

            if (matching is null)
            {
                return;
            }

            var matchedRule = matching;
            startContext.RegisterSyntaxTreeAction(treeContext => AnalyzeTree(treeContext, matchedRule, assemblyName));
        });
    }

    private void AnalyzeTree(SyntaxTreeAnalysisContext context, ProjectFileRule rule, string? assemblyName)
    {
        var fileName = Path.GetFileName(context.Tree.FilePath);
        if (string.IsNullOrEmpty(fileName))
        {
            return;
        }

        foreach (var pattern in rule.AllowedFiles)
        {
            if (pattern.Matches(fileName))
            {
                return;
            }
        }

        context.ReportDiagnostic(Diagnostic.Create(
            Rule,
            Location.Create(context.Tree, new TextSpan(0, 0)),
            fileName,
            assemblyName ?? rule.Project.Raw,
            rule.RawAllowed));
    }
}
