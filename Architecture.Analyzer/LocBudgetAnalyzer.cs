using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Architecture.Analyzer.Services.LocBudgetRule;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Architecture.Analyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class LocBudgetAnalyzer : DiagnosticAnalyzer
{
    public const string ClassDiagnosticId = "ARCH006";
    public const string MethodDiagnosticId = "ARCH007";
    public const string SolutionDiagnosticId = "ARCH008";
    public const string ProjectDiagnosticId = "ARCH009";

    private const string Category = "Architecture";
    private static readonly LocalizableString ClassTitle = new LocalizableResourceString(nameof(Resources.LocBudgetClassTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableString MethodTitle = new LocalizableResourceString(nameof(Resources.LocBudgetMethodTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableString SolutionTitle = new LocalizableResourceString(nameof(Resources.LocBudgetSolutionTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableString ProjectTitle = new LocalizableResourceString(nameof(Resources.LocBudgetProjectTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableString ClassMessageFormat = new LocalizableResourceString(nameof(Resources.LocBudgetClassMessageFormat), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableString MethodMessageFormat = new LocalizableResourceString(nameof(Resources.LocBudgetMethodMessageFormat), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableString SolutionMessageFormat = new LocalizableResourceString(nameof(Resources.LocBudgetSolutionMessageFormat), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableString ProjectMessageFormat = new LocalizableResourceString(nameof(Resources.LocBudgetProjectMessageFormat), Resources.ResourceManager, typeof(Resources));

    private readonly ILocBudgetRuleService _locBudgetRuleService;

    private static readonly DiagnosticDescriptor ClassRule = new(
        ClassDiagnosticId,
        ClassTitle,
        ClassMessageFormat,
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor MethodRule = new(
        MethodDiagnosticId,
        MethodTitle,
        MethodMessageFormat,
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor SolutionRule = new(
        SolutionDiagnosticId,
        SolutionTitle,
        SolutionMessageFormat,
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: [WellKnownDiagnosticTags.CompilationEnd]);

    private static readonly DiagnosticDescriptor ProjectRule = new(
        ProjectDiagnosticId,
        ProjectTitle,
        ProjectMessageFormat,
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: [WellKnownDiagnosticTags.CompilationEnd]);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(ClassRule, MethodRule, SolutionRule, ProjectRule);

    public LocBudgetAnalyzer()
        : this(new LocBudgetRuleService())
    {
    }

    internal LocBudgetAnalyzer(ILocBudgetRuleService locBudgetRuleService)
    {
        _locBudgetRuleService = locBudgetRuleService;
    }

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(startContext =>
        {
            startContext.RegisterSyntaxNodeAction(
                nodeContext =>
                {
                    var declaration = (TypeDeclarationSyntax)nodeContext.Node;
                    var limits = _locBudgetRuleService.GetLineLimits(
                        startContext.Options.AnalyzerConfigOptionsProvider.GetOptions(declaration.SyntaxTree));

                    if (!limits.MaxClassLines.HasValue)
                    {
                        return;
                    }

                    var violation = _locBudgetRuleService.GetLocViolationsOnType(declaration, limits.MaxClassLines.Value, nodeContext.CancellationToken);
                    if (violation is null)
                    {
                        return;
                    }

                    nodeContext.ReportDiagnostic(Diagnostic.Create(
                        ClassRule,
                        violation.Location,
                        violation.ItemName,
                        violation.CurrentLines,
                        violation.AllowedLines));
                },
                SyntaxKind.ClassDeclaration,
                SyntaxKind.RecordDeclaration,
                SyntaxKind.RecordStructDeclaration);

            startContext.RegisterSyntaxNodeAction(
                nodeContext =>
                {
                    var declaration = (MethodDeclarationSyntax)nodeContext.Node;
                    var limits = _locBudgetRuleService.GetLineLimits(
                        startContext.Options.AnalyzerConfigOptionsProvider.GetOptions(declaration.SyntaxTree));

                    if (!limits.MaxMethodLines.HasValue)
                    {
                        return;
                    }

                    var violation = _locBudgetRuleService.GetLocViolationOnMethod(declaration, limits.MaxMethodLines.Value, nodeContext.CancellationToken);
                    if (violation is null)
                    {
                        return;
                    }

                    nodeContext.ReportDiagnostic(Diagnostic.Create(
                        MethodRule,
                        violation.Location,
                        violation.ItemName,
                        violation.CurrentLines,
                        violation.AllowedLines));
                },
                SyntaxKind.MethodDeclaration);

            var syntaxTree = startContext.Compilation.SyntaxTrees.FirstOrDefault(IsUserSourceTree);
            if (syntaxTree is null)
            {
                return;
            }

            var settingsForCompilation = _locBudgetRuleService.GetSettings(
                startContext.Options.AnalyzerConfigOptionsProvider.GetOptions(syntaxTree),
                startContext.Options.AnalyzerConfigOptionsProvider.GlobalOptions);

            if (!settingsForCompilation.ProjectBudget.HasValue && !settingsForCompilation.GlobalBudget.HasValue)
            {
                return;
            }

            startContext.RegisterCompilationEndAction(endContext =>
            {
                if (settingsForCompilation.ProjectBudget.HasValue)
                {
                    var projectViolation = _locBudgetRuleService.GetLocViolationOnProject(endContext.Compilation, settingsForCompilation.ProjectAddedLines, settingsForCompilation.ProjectBudget.Value, endContext.CancellationToken);
                    if (projectViolation is not null)
                    {
                        endContext.ReportDiagnostic(Diagnostic.Create(
                            ProjectRule,
                            projectViolation.Location,
                            projectViolation.ItemName,
                            projectViolation.CurrentLines,
                            projectViolation.AllowedLines));
                    }
                }

                if (settingsForCompilation.GlobalBudget.HasValue)
                {
                    var globalViolation = _locBudgetRuleService.GetLocViolationOnGlobal(endContext.Compilation, settingsForCompilation.SolutionAddedLines, settingsForCompilation.GlobalBudget.Value, endContext.CancellationToken);
                    if (globalViolation is not null)
                    {
                        endContext.ReportDiagnostic(Diagnostic.Create(
                            SolutionRule,
                            globalViolation.Location,
                            globalViolation.ItemName,
                            globalViolation.CurrentLines,
                            globalViolation.AllowedLines));
                    }
                }
            });
        });
    }

    private static bool IsUserSourceTree(SyntaxTree syntaxTree)
    {
        var filePath = syntaxTree.FilePath;
        return !string.IsNullOrWhiteSpace(filePath)
               && filePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
               && filePath.IndexOf($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) < 0
               && filePath.IndexOf($"{Path.AltDirectorySeparatorChar}obj{Path.AltDirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) < 0;
    }
}