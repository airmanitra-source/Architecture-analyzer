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
    private const string ClassTitle = "La classe dépasse le budget de lignes ajoutées par le prompt";
    private const string MethodTitle = "La méthode dépasse le budget de lignes ajoutées par le prompt";
    private const string SolutionTitle = "La solution dépasse le budget global de lignes ajoutées par le prompt";
    private const string ProjectTitle = "Le projet dépasse le budget de lignes ajoutées par le prompt";
    private const string ClassMessageFormat = "La classe '{0}' ajoute {1} lignes de code et dépasse le budget de {2} lignes.";
    private const string MethodMessageFormat = "La méthode '{0}' ajoute {1} lignes de code et dépasse le budget de {2} lignes.";
    private const string SolutionMessageFormat = "La solution '{0}' ajoute {1} lignes de code et dépasse le budget global de {2} lignes.";
    private const string ProjectMessageFormat = "Le projet '{0}' ajoute {1} lignes de code et dépasse le budget de {2} lignes.";

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
                    var settings = _locBudgetRuleService.GetSettings(
                        startContext.Compilation,
                        startContext.Options.AnalyzerConfigOptionsProvider.GetOptions(declaration.SyntaxTree),
                        nodeContext.CancellationToken);

                    if (!settings.MaxClassLines.HasValue)
                    {
                        return;
                    }

                    var violation = _locBudgetRuleService.AnalyzeType(declaration, settings.MaxClassLines.Value, nodeContext.CancellationToken);
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
                    var settings = _locBudgetRuleService.GetSettings(
                        startContext.Compilation,
                        startContext.Options.AnalyzerConfigOptionsProvider.GetOptions(declaration.SyntaxTree),
                        nodeContext.CancellationToken);

                    if (!settings.MaxMethodLines.HasValue)
                    {
                        return;
                    }

                    var violation = _locBudgetRuleService.AnalyzeMethod(declaration, settings.MaxMethodLines.Value, nodeContext.CancellationToken);
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
                startContext.Compilation,
                startContext.Options.AnalyzerConfigOptionsProvider.GetOptions(syntaxTree),
                startContext.CancellationToken);

            if (!settingsForCompilation.ProjectBudget.HasValue && !settingsForCompilation.GlobalBudget.HasValue)
            {
                return;
            }

            startContext.RegisterCompilationEndAction(endContext =>
            {
                if (settingsForCompilation.ProjectBudget.HasValue)
                {
                    var projectViolation = _locBudgetRuleService.AnalyzeProject(endContext.Compilation, settingsForCompilation.ProjectBudget.Value, endContext.CancellationToken);
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
                    var globalViolation = _locBudgetRuleService.AnalyzeGlobal(endContext.Compilation, settingsForCompilation.GlobalBudget.Value, endContext.CancellationToken);
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