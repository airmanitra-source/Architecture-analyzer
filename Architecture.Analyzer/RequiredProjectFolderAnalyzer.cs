using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Architecture.Analyzer.Services.ModelDirectoryRule;
using Architecture.Analyzer.Services.RequiredProjectFolderRule;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Architecture.Analyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RequiredProjectFolderAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "ARCH010";

    private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.RequiredProjectFolderTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.RequiredProjectFolderMessageFormat), Resources.ResourceManager, typeof(Resources));
    private const string Category = "Architecture";

    private readonly IRequiredProjectFolderRuleService _requiredProjectFolderRuleService;
    private readonly IModelDirectoryRuleService _modelDirectoryRuleService;

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        Title,
        MessageFormat,
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: [WellKnownDiagnosticTags.CompilationEnd]);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public RequiredProjectFolderAnalyzer()
        : this(new RequiredProjectFolderRuleService(), new ModelDirectoryRuleService())
    {
    }

    internal RequiredProjectFolderAnalyzer(
        IRequiredProjectFolderRuleService requiredProjectFolderRuleService,
        IModelDirectoryRuleService modelDirectoryRuleService)
    {
        _requiredProjectFolderRuleService = requiredProjectFolderRuleService;
        _modelDirectoryRuleService = modelDirectoryRuleService;
    }

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationAction(AnalyzeCompilation);
    }

    private void AnalyzeCompilation(CompilationAnalysisContext context)
    {
        var syntaxTree = context.Compilation.SyntaxTrees.FirstOrDefault(IsUserSourceTree);
        if (syntaxTree is null)
        {
            return;
        }

        var options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(syntaxTree);
        var rules = _requiredProjectFolderRuleService.ReadRules(options);
        var matchingRule = rules.FirstOrDefault(rule => rule.ProjectName == context.Compilation.AssemblyName);
        if (matchingRule is null)
        {
            return;
        }

        AnalyzeCompilationEnd(context, matchingRule);
    }

    private void AnalyzeCompilationEnd(CompilationAnalysisContext context, Models.RequiredProjectFolderRule matchingRule)
    {
        var sourceFilePaths = context.Compilation.SyntaxTrees
            .Where(IsUserSourceTree)
            .Select(tree => tree.FilePath)
            .ToList();

        var projectLocation = GetProjectLocation(context.Compilation, context.CancellationToken);
        if (projectLocation is null)
        {
            return;
        }

        foreach (var requiredFolder in matchingRule.RequiredFolders)
        {
            var hasFileInFolder = sourceFilePaths.Any(filePath => _modelDirectoryRuleService.IsTypeInExpectedFolder(filePath, requiredFolder));
            if (hasFileInFolder)
            {
                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(
                Rule,
                projectLocation,
                matchingRule.ProjectName,
                requiredFolder.Replace(Path.DirectorySeparatorChar, '/')));
        }
    }

    private static Location? GetProjectLocation(Compilation compilation, System.Threading.CancellationToken cancellationToken)
    {
        var syntaxTree = compilation.SyntaxTrees.FirstOrDefault();
        if (syntaxTree is null)
        {
            return null;
        }

        return syntaxTree.GetRoot(cancellationToken).GetLocation();
    }

    private static bool IsUserSourceTree(SyntaxTree syntaxTree)
    {
        var filePath = syntaxTree.FilePath;
        return !string.IsNullOrWhiteSpace(filePath)
               && filePath.EndsWith(".cs", System.StringComparison.OrdinalIgnoreCase)
               && filePath.IndexOf($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", System.StringComparison.OrdinalIgnoreCase) < 0
               && filePath.IndexOf($"{Path.AltDirectorySeparatorChar}obj{Path.AltDirectorySeparatorChar}", System.StringComparison.OrdinalIgnoreCase) < 0;
    }
}
