using Architecture.Analyzer.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Architecture.Analyzer.Services.LocBudgetRule;

internal interface ILocBudgetRuleService
{
    (int? MaxClassLines, int? MaxMethodLines) GetLineLimits(AnalyzerConfigOptions options);

    LocBudgetSettings GetSettings(AnalyzerConfigOptions editorConfigOptions, AnalyzerConfigOptions buildPropertyOptions);

    LocBudgetViolation? GetLocViolationsOnType(TypeDeclarationSyntax declaration, int maxLines, global::System.Threading.CancellationToken cancellationToken);

    LocBudgetViolation? GetLocViolationOnMethod(MethodDeclarationSyntax declaration, int maxLines, global::System.Threading.CancellationToken cancellationToken);

    LocBudgetViolation? GetLocViolationOnProject(Compilation compilation, int addedLines, int budget, global::System.Threading.CancellationToken cancellationToken);

    LocBudgetViolation? GetLocViolationOnGlobal(Compilation compilation, int addedLines, int budget, global::System.Threading.CancellationToken cancellationToken);
}