using System;
using System.Collections.Immutable;
using System.IO;
using Architecture.Analyzer.Services.FileNameRule;
using Architecture.Analyzer.Services.ModelConventionRule;
using Architecture.Analyzer.Services.ModelDirectoryRule;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Architecture.Analyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ModelDirectoryAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "ARCH001";
    public const string FileNameDiagnosticId = "ARCH002";

    private const string DirectoryMessageFormat = "Le type '{0}' se termine par '{1}' et doit être dans le dossier '{2}'.";
    private const string DirectoryTitle = "Type de modèle dans le mauvais dossier";
    private const string FileNameMessageFormat = "Le type '{0}' doit être déclaré dans un fichier nommé '{1}'.";
    private const string FileNameTitle = "Le nom du fichier doit correspondre au nom du type";
    private const string Category = "Architecture";

    private readonly IModelConventionRuleService _modelConventionService;
    private readonly IModelDirectoryRuleService _modelDirectoryRuleService;
    private readonly ITypeFileNameRuleService _typeFileNameRuleService;

    private static readonly DiagnosticDescriptor DirectoryRule = new(
        DiagnosticId,
        DirectoryTitle,
        DirectoryMessageFormat,
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor FileNameRule = new(
        FileNameDiagnosticId,
        FileNameTitle,
        FileNameMessageFormat,
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(DirectoryRule, FileNameRule);

    public ModelDirectoryAnalyzer()
        : this(new ModelConventionRuleService(), new ModelDirectoryRuleService(), new TypeFileNameRuleService())
    {
    }

    internal ModelDirectoryAnalyzer(
        IModelConventionRuleService modelConventionService,
        IModelDirectoryRuleService modelDirectoryRuleService,
        ITypeFileNameRuleService typeFileNameRuleService)
    {
        _modelConventionService = modelConventionService;
        _modelDirectoryRuleService = modelDirectoryRuleService;
        _typeFileNameRuleService = typeFileNameRuleService;
    }

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(
            AnalyzeType,
            SyntaxKind.ClassDeclaration,
            SyntaxKind.EnumDeclaration,
            SyntaxKind.RecordDeclaration,
            SyntaxKind.RecordStructDeclaration);
    }

    private void AnalyzeType(SyntaxNodeAnalysisContext context)
    {
        var declaration = (BaseTypeDeclarationSyntax)context.Node;
        if (!_typeFileNameRuleService.DoesFileNameMatchTypeName(declaration.SyntaxTree.FilePath, declaration.Identifier.ValueText))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                FileNameRule,
                declaration.Identifier.GetLocation(),
                declaration.Identifier.ValueText,
                declaration.Identifier.ValueText + ".cs"));
        }

        var options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(declaration.SyntaxTree);
        var conventions = _modelConventionService.ReadConventions(options);
        foreach (var convention in conventions)
        {
            if (!declaration.Identifier.ValueText.EndsWith(convention.Suffix, StringComparison.Ordinal))
            {
                continue;
            }

            if (!_modelDirectoryRuleService.IsTypeInExpectedFolder(declaration.SyntaxTree.FilePath, convention.Folder))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DirectoryRule,
                    declaration.Identifier.GetLocation(),
                    declaration.Identifier.ValueText,
                    convention.Suffix,
                    convention.Folder.Replace(Path.DirectorySeparatorChar, '/')));
            }

            return;
        }
    }
}
