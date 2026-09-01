using System.Collections.Immutable;
using Architecture.Analyzer.Services.NameLengthRule;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Architecture.Analyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NameLengthAnalyzer : DiagnosticAnalyzer
{
    public const string VariableDiagnosticId = "ARCH013";
    public const string ClassDiagnosticId = "ARCH014";
    public const string MethodDiagnosticId = "ARCH015";

    private const string Category = "Architecture";

    private static readonly LocalizableString VariableTitle = new LocalizableResourceString(nameof(Resources.NameLengthVariableTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableString VariableMessageFormat = new LocalizableResourceString(nameof(Resources.NameLengthVariableMessageFormat), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableString ClassTitle = new LocalizableResourceString(nameof(Resources.NameLengthClassTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableString ClassMessageFormat = new LocalizableResourceString(nameof(Resources.NameLengthClassMessageFormat), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableString MethodTitle = new LocalizableResourceString(nameof(Resources.NameLengthMethodTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableString MethodMessageFormat = new LocalizableResourceString(nameof(Resources.NameLengthMethodMessageFormat), Resources.ResourceManager, typeof(Resources));

    private readonly INameLengthRuleService _nameLengthRuleService;

    private static readonly DiagnosticDescriptor VariableRule = new(
        VariableDiagnosticId,
        VariableTitle,
        VariableMessageFormat,
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

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

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(VariableRule, ClassRule, MethodRule);

    public NameLengthAnalyzer()
        : this(new NameLengthRuleService())
    {
    }

    internal NameLengthAnalyzer(INameLengthRuleService nameLengthRuleService)
    {
        _nameLengthRuleService = nameLengthRuleService;
    }

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeClass, SyntaxKind.ClassDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzeVariable, SyntaxKind.VariableDeclarator);
        context.RegisterSyntaxNodeAction(AnalyzeMethod, SyntaxKind.MethodDeclaration);
    }

    private void AnalyzeClass(SyntaxNodeAnalysisContext context)
    {
        var declaration = (ClassDeclarationSyntax)context.Node;
        var options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(declaration.SyntaxTree);

        var minLength = _nameLengthRuleService.GetMinClassNameLength(options);
        if (!minLength.HasValue)
        {
            return;
        }

        var violation = _nameLengthRuleService.GetClassNameViolation(declaration, minLength.Value);
        if (violation is null)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            ClassRule,
            violation.Location,
            violation.Name,
            violation.CurrentLength,
            violation.MinimumLength));
    }

    private void AnalyzeMethod(SyntaxNodeAnalysisContext context)
    {
        var declaration = (MethodDeclarationSyntax)context.Node;
        var options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(declaration.SyntaxTree);

        var minLength = _nameLengthRuleService.GetMinMethodNameLength(options);
        if (!minLength.HasValue)
        {
            return;
        }

        var symbol = context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken) as IMethodSymbol;
        if (_nameLengthRuleService.IsExemptFromMethodNameRule(symbol))
        {
            return;
        }

        var violation = _nameLengthRuleService.GetMethodNameViolation(declaration, minLength.Value);
        if (violation is null)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            MethodRule,
            violation.Location,
            violation.Name,
            violation.CurrentLength,
            violation.MinimumLength));
    }

    private void AnalyzeVariable(SyntaxNodeAnalysisContext context)
    {
        var declarator = (VariableDeclaratorSyntax)context.Node;
        var options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(declarator.SyntaxTree);

        var minLength = _nameLengthRuleService.GetMinVariableNameLength(options);
        if (!minLength.HasValue)
        {
            return;
        }

        var violation = _nameLengthRuleService.GetVariableNameViolation(declarator.Identifier, minLength.Value);
        if (violation is null)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            VariableRule,
            violation.Location,
            violation.Name,
            violation.CurrentLength,
            violation.MinimumLength));
    }
}
