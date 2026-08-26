using System.Collections.Generic;
using Architecture.Analyzer.Models;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Architecture.Analyzer.Services.ModelConventionRule;

internal interface IModelConventionRuleService
{
    List<ModelConventionViolation> ReadConventions(AnalyzerConfigOptions options);
}
