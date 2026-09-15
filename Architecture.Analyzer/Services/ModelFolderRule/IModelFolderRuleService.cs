using System.Collections.Generic;
using Architecture.Analyzer.Models;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Architecture.Analyzer.Services.ModelFolderRule;

internal interface IModelFolderRuleService
{
    List<ModelFolderSuffixRule> ReadFolderRules(AnalyzerConfigOptions options);

    // Per-project file allow-lists (ARCH003): "<projectPattern>=<filePattern>,<filePattern>;...".
    List<ProjectFileRule> ReadProjectFileRules(AnalyzerConfigOptions options);
}