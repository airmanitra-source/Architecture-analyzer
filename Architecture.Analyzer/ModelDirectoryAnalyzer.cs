using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Architecture.Analyzer.Services.FileNameRule;
using Architecture.Analyzer.Models;
using Architecture.Analyzer.Services.ModelConventionRule;
using Architecture.Analyzer.Services.ModelDirectoryRule;
using Architecture.Analyzer.Services.ModelFolderRule;
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
    public const string FolderAllowedDiagnosticId = "ARCH003";

    private static readonly LocalizableString DirectoryMessageFormat = new LocalizableResourceString(nameof(Resources.ModelDirectoryMessageFormat), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableString DirectoryTitle = new LocalizableResourceString(nameof(Resources.ModelDirectoryTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableString FileNameMessageFormat = new LocalizableResourceString(nameof(Resources.ModelFileNameMessageFormat), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableString FileNameTitle = new LocalizableResourceString(nameof(Resources.ModelFileNameTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableString FolderAllowedMessageFormat = new LocalizableResourceString(nameof(Resources.ModelFolderAllowedMessageFormat), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableString FolderAllowedTitle = new LocalizableResourceString(nameof(Resources.ModelFolderAllowedTitle), Resources.ResourceManager, typeof(Resources));
    private const string Category = "Architecture";

    private readonly IModelConventionRuleService _modelConventionService;
    private readonly IModelDirectoryRuleService _modelDirectoryRuleService;
    private readonly IModelFolderRuleService _modelFolderRuleService;
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

    private static readonly DiagnosticDescriptor FolderAllowedRule = new(
        FolderAllowedDiagnosticId,
        FolderAllowedTitle,
        FolderAllowedMessageFormat,
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(DirectoryRule, FileNameRule, FolderAllowedRule);

    public ModelDirectoryAnalyzer()
        : this(new ModelConventionRuleService(), new ModelDirectoryRuleService(), new ModelFolderRuleService(), new TypeFileNameRuleService())
    {
    }

    internal ModelDirectoryAnalyzer(
        IModelConventionRuleService modelConventionService,
        IModelDirectoryRuleService modelDirectoryRuleService,
        IModelFolderRuleService modelFolderRuleService,
        ITypeFileNameRuleService typeFileNameRuleService)
    {
        _modelConventionService = modelConventionService;
        _modelDirectoryRuleService = modelDirectoryRuleService;
        _modelFolderRuleService = modelFolderRuleService;
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
        var typeName = declaration.Identifier.ValueText;
        var filePath = declaration.SyntaxTree.FilePath;

        if (!_typeFileNameRuleService.DoesFileNameMatchTypeName(filePath, typeName))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                FileNameRule,
                declaration.Identifier.GetLocation(),
                typeName,
                typeName + ".cs"));
        }

        var options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(declaration.SyntaxTree);
        var conventions = _modelConventionService.GetModelConventionViolations(options);
        var matchingConvention = conventions
            .Where(convention => typeName.EndsWith(convention.Suffix, StringComparison.Ordinal))
            .OrderByDescending(convention => convention.Suffix.Length)
            .FirstOrDefault();

        if (matchingConvention is not null
            && !_modelDirectoryRuleService.IsTypeInExpectedFolder(filePath, matchingConvention.Folder))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DirectoryRule,
                declaration.Identifier.GetLocation(),
                typeName,
                matchingConvention.Suffix,
                matchingConvention.Folder.Replace(Path.DirectorySeparatorChar, '/')));
        }

        var folderRules = _modelFolderRuleService.ReadFolderRules(options);
        var matchingFolderRule = GetMostSpecificMatchingFolderRule(filePath, folderRules);
        if (matchingFolderRule is null)
        {
            return;
        }

        if (matchingFolderRule.AllowedSuffixes.Any(suffix => typeName.EndsWith(suffix, StringComparison.Ordinal)))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            FolderAllowedRule,
            declaration.Identifier.GetLocation(),
            typeName,
            matchingFolderRule.Folder.Replace(Path.DirectorySeparatorChar, '/'),
            string.Join(", ", matchingFolderRule.AllowedSuffixes)));
    }

    private ModelFolderSuffixRule? GetMostSpecificMatchingFolderRule(string filePath, IReadOnlyList<ModelFolderSuffixRule> folderRules)
    {
        ModelFolderSuffixRule? bestMatch = null;
        var bestDepth = -1;

        foreach (var folderRule in folderRules)
        {
            if (!_modelDirectoryRuleService.IsTypeInExpectedFolder(filePath, folderRule.Folder))
            {
                continue;
            }

            var depth = GetFolderDepth(folderRule.Folder);
            if (depth > bestDepth)
            {
                bestDepth = depth;
                bestMatch = folderRule;
            }
        }

        return bestMatch;
    }

    private static int GetFolderDepth(string folder)
        => folder.Split(new[] { Path.DirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries).Length;
}
