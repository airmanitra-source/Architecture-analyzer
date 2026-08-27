using System.Collections.Generic;
using System.Collections.Immutable;
using Architecture.Analyzer.Services.ModuleProviderRule;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Architecture.Analyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ModuleProviderAnalyzer : DiagnosticAnalyzer
{
    public const string MissingProviderContractsDiagnosticId = "ARCH003";
    public const string LocalProviderImplementationDiagnosticId = "ARCH004";

    private static readonly LocalizableString MissingProviderContractsTitle = new LocalizableResourceString(nameof(Resources.MissingProviderContractsTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableString MissingProviderContractsMessageFormat = new LocalizableResourceString(nameof(Resources.MissingProviderContractsMessageFormat), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableString LocalProviderImplementationTitle = new LocalizableResourceString(nameof(Resources.LocalProviderImplementationTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableString LocalProviderImplementationMessageFormat = new LocalizableResourceString(nameof(Resources.LocalProviderImplementationMessageFormat), Resources.ResourceManager, typeof(Resources));
    private const string Category = "Architecture";

    private readonly IModuleProviderRuleService _moduleProviderRuleService;

    private static readonly DiagnosticDescriptor MissingProviderContractsRule = new(
        MissingProviderContractsDiagnosticId,
        MissingProviderContractsTitle,
        MissingProviderContractsMessageFormat,
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: [WellKnownDiagnosticTags.CompilationEnd]);

    private static readonly DiagnosticDescriptor LocalProviderImplementationRule = new(
        LocalProviderImplementationDiagnosticId,
        LocalProviderImplementationTitle,
        LocalProviderImplementationMessageFormat,
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(MissingProviderContractsRule, LocalProviderImplementationRule);

    public ModuleProviderAnalyzer()
        : this(new ModuleProviderRuleService(new ModelDirectoryRuleService()))
    {
    }

    internal ModuleProviderAnalyzer(IModuleProviderRuleService moduleProviderRuleService)
    {
        _moduleProviderRuleService = moduleProviderRuleService;
    }

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(InitializeModuleAnalysis);
    }

    private void InitializeModuleAnalysis(CompilationStartAnalysisContext context)
    {
        if (!_moduleProviderRuleService.IsModuleProject(context.Compilation.AssemblyName))
        {
            return;
        }

        var providerInterfaces = _moduleProviderRuleService.GetProviderInterfaces(context.Compilation, context.CancellationToken);

        context.RegisterSymbolAction(
            symbolContext => AnalyzeNamedType(symbolContext, providerInterfaces),
            SymbolKind.NamedType);

        context.RegisterCompilationEndAction(
            endContext => AnalyzeCompilationEnd(endContext, providerInterfaces));
    }

    private void AnalyzeNamedType(SymbolAnalysisContext context, List<INamedTypeSymbol> providerInterfaces)
    {
        var type = (INamedTypeSymbol)context.Symbol;
        var violation = _moduleProviderRuleService.GetProviderImplementionViolation(type, providerInterfaces, context.CancellationToken);
        if (violation is null)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            LocalProviderImplementationRule,
            violation.Location,
            violation.ImplementationName,
            violation.InterfaceName,
            context.Compilation.AssemblyName));
    }

    private void AnalyzeCompilationEnd(CompilationAnalysisContext context, List<INamedTypeSymbol> providerInterfaces)
    {
        if (providerInterfaces.Count > 0)
        {
            return;
        }

        var projectLocation = _moduleProviderRuleService.GetProjectLocation(context.Compilation, context.CancellationToken);
        if (projectLocation is null)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            MissingProviderContractsRule,
            projectLocation,
            context.Compilation.AssemblyName));
    }
}