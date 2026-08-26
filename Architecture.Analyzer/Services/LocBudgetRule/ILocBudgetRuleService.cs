using Architecture.Analyzer.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Architecture.Analyzer.Services.LocBudgetRule;

internal interface ILocBudgetRuleService
{
    LocBudgetSettings GetSettings(Compilation compilation, AnalyzerConfigOptions options, global::System.Threading.CancellationToken cancellationToken);

    LocBudgetViolation? AnalyzeType(TypeDeclarationSyntax declaration, int maxLines, global::System.Threading.CancellationToken cancellationToken);

    LocBudgetViolation? AnalyzeMethod(MethodDeclarationSyntax declaration, int maxLines, global::System.Threading.CancellationToken cancellationToken);

    LocBudgetViolation? AnalyzeProject(Compilation compilation, int budget, global::System.Threading.CancellationToken cancellationToken);

    LocBudgetViolation? AnalyzeGlobal(Compilation compilation, int budget, global::System.Threading.CancellationToken cancellationToken);
}