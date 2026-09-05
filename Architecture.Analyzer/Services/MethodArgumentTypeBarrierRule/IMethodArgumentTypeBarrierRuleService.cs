using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Architecture.Analyzer.Services.MethodArgumentTypeBarrierRule;

internal interface IMethodArgumentTypeBarrierRuleService
{
    List<Models.MethodArgumentTypeBarrierRule> GetRules(AnalyzerConfigOptions options);

    List<Models.MethodArgumentTypeBarrierRule> GetRulesForClass(string className, List<Models.MethodArgumentTypeBarrierRule> rules);

    Models.MethodArgumentTypeBarrierViolation? CheckParameterType(
        string methodName,
        string className,
        ITypeSymbol parameterType,
        List<Models.MethodArgumentTypeBarrierRule> classRules,
        Location location);
}
