using System.Collections.Generic;
using Architecture.Analyzer.Models;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Architecture.Analyzer.Services.ModelFolderRule;

internal interface IModelFolderRuleService
{
    List<ModelFolderSuffixRule> ReadFolderRules(AnalyzerConfigOptions options);
}