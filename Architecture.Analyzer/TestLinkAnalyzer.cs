using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Architecture.Analyzer.Models;
using Architecture.Analyzer.Services.TestLinkRule;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Architecture.Analyzer;

// The related-test gate: a production member added or grown by this change must be invoked by a test
// that the same change added or modified, with an assertion, in a file that references the member's
// concrete type. It looks only at what this change makes new, so it has no backlog and can be switched
// on as-is on a legacy code base — like the complexity ratchet, it measures movement, not state.
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TestLinkAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "ARCH025";

    private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.TestLinkTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.TestLinkMessageFormat), Resources.ResourceManager, typeof(Resources));
    private const string Category = "Architecture";

    private readonly ITestLinkRuleService _ruleService;

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        Title,
        MessageFormat,
        Category,
        // A defect of the change, not a trend: a member whose behaviour changed with no test that even
        // exercises it. Following the house rule (defect -> error), and because a warning is what an AI
        // agent ignores, this is an error. It stays opt-in and honours a folder-scoped severity override.
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public TestLinkAnalyzer()
        : this(new TestLinkRuleService())
    {
    }

    internal TestLinkAnalyzer(ITestLinkRuleService ruleService)
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

            var settings = _ruleService.GetSettings(startContext.Options.AnalyzerConfigOptionsProvider.GetOptions(firstTree));
            if (!settings.IsEnabled)
            {
                return;
            }

            // Without the MSBuild-supplied change context the rule cannot know what is new, so it stays
            // silent by design — exactly like the complexity ratchet without its baseline.
            var changedFiles = _ruleService.ReadChangedFiles(
                startContext.Options.AdditionalFiles,
                startContext.Options.AnalyzerConfigOptionsProvider,
                startContext.CancellationToken);
            if (changedFiles.Count == 0)
            {
                return;
            }

            var testFilePaths = _ruleService.ReadTestFilePaths(
                startContext.Options.AdditionalFiles,
                startContext.Options.AnalyzerConfigOptionsProvider,
                startContext.CancellationToken);

            var headMembers = _ruleService.BuildHeadMembers(
                startContext.Options.AdditionalFiles,
                startContext.Options.AnalyzerConfigOptionsProvider,
                startContext.CancellationToken);

            var evidence = _ruleService.BuildTestEvidence(
                startContext.Options.AdditionalFiles,
                startContext.Options.AnalyzerConfigOptionsProvider,
                headMembers,
                settings,
                startContext.CancellationToken);

            startContext.RegisterSyntaxTreeAction(treeContext =>
                AnalyzeTree(treeContext, changedFiles, testFilePaths, headMembers, evidence, settings));
        });
    }

    private void AnalyzeTree(
        SyntaxTreeAnalysisContext context,
        HashSet<string> changedFiles,
        HashSet<string> testFilePaths,
        Dictionary<(string Type, string Member, int Arity), (int Hash, int Lines)> headMembers,
        Dictionary<string, List<HashSet<string>>> evidence,
        TestLinkSettings settings)
    {
        var path = context.Tree.FilePath;

        // Only files this change touched can hold a new member; unchanged trees exit on one lookup.
        if (!changedFiles.Contains(path))
        {
            return;
        }

        // A test project's own compilation would otherwise treat its test methods as production members.
        if (testFilePaths.Contains(path))
        {
            return;
        }

        if (IsExcluded(path, settings.ExcludePaths))
        {
            return;
        }

        var root = context.Tree.GetRoot(context.CancellationToken);
        foreach (var node in root.DescendantNodes())
        {
            if (node is not MethodDeclarationSyntax and not ConstructorDeclarationSyntax)
            {
                continue;
            }

            var violation = _ruleService.Check((MemberDeclarationSyntax)node, headMembers, evidence, settings, context.CancellationToken);
            if (violation is null)
            {
                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(
                Rule,
                violation.Location,
                violation.DisplayName,
                violation.MemberName,
                violation.TypeName,
                violation.ReferencingTestCount));
        }
    }

    private static bool IsExcluded(string path, ImmutableArray<string> fragments)
    {
        if (fragments.Length == 0 || string.IsNullOrEmpty(path))
        {
            return false;
        }

        var normalized = path.Replace('\\', '/');
        foreach (var fragment in fragments)
        {
            if (normalized.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }
}
