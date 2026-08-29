using System.Collections.Generic;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Architecture.Analyzer.Services.RequiredProjectFolderRule;

internal interface IRequiredProjectFolderRuleService
{
    List<Models.RequiredProjectFolderRule> ReadRules(AnalyzerConfigOptions options);
}
