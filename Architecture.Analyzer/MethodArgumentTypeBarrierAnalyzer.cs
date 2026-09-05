using System.Collections.Generic;
using System.Collections.Immutable;
using Architecture.Analyzer.Services.MethodArgumentTypeBarrierRule;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Architecture.Analyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MethodArgumentTypeBarrierAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "ARCH016";

    private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.MethodArgumentTypeBarrierTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.MethodArgumentTypeBarrierMessageFormat), Resources.ResourceManager, typeof(Resources));
    private const string Category = "Architecture";

    private readonly IMethodArgumentTypeBarrierRuleService _ruleService;

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        Title,
        MessageFormat,
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public MethodArgumentTypeBarrierAnalyzer()
        : this(new MethodArgumentTypeBarrierRuleService())
    {
    }

    internal MethodArgumentTypeBarrierAnalyzer(IMethodArgumentTypeBarrierRuleService ruleService)
    {
        _ruleService = ruleService;
    }

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeMethod, SyntaxKind.MethodDeclaration);
    }

    private void AnalyzeMethod(SyntaxNodeAnalysisContext context)
    {
        var declaration = (MethodDeclarationSyntax)context.Node;
        if (declaration.ParameterList.Parameters.Count == 0)
        {
            return;
        }

        var options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(declaration.SyntaxTree);
        var rules = _ruleService.GetRules(options);
        if (rules.Count == 0)
        {
            return;
        }

        var method = context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken);
        if (method?.ContainingType is null)
        {
            return;
        }

        // The rule targets classes (records included — they are reference types of the
        // same kind). Methods declared in interfaces or structs are out of scope.
        if (method.ContainingType.TypeKind != TypeKind.Class)
        {
            return;
        }

        var classRules = _ruleService.GetRulesForClass(method.ContainingType.Name, rules);
        if (classRules.Count == 0)
        {
            return;
        }

        foreach (var parameter in declaration.ParameterList.Parameters)
        {
            AnalyzeParameter(context, parameter, method.Name, method.ContainingType.Name, classRules);
        }
    }

    private void AnalyzeParameter(
        SyntaxNodeAnalysisContext context,
        ParameterSyntax parameter,
        string methodName,
        string className,
        List<Models.MethodArgumentTypeBarrierRule> classRules)
    {
        if (parameter.Type is null)
        {
            return;
        }

        var parameterType = context.SemanticModel.GetTypeInfo(parameter.Type, context.CancellationToken).Type;

        // Skip unresolved (error) types so the rule does not pile onto code that does not
        // yet compile — consistent with how ARCH012 ignores unresolved references.
        if (parameterType is null || parameterType.TypeKind == TypeKind.Error)
        {
            return;
        }

        var violation = _ruleService.CheckParameterType(methodName, className, parameterType, classRules, parameter.Type.GetLocation());
        if (violation is null)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            Rule,
            violation.Location,
            violation.MethodName,
            violation.ClassName,
            violation.ParameterTypeName,
            violation.ClassPattern,
            violation.ForbiddenTypePattern));
    }
}
