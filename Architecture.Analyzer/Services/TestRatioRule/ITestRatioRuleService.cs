using System.Threading;
using Architecture.Analyzer.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Architecture.Analyzer.Services.TestRatioRule;

internal interface ITestRatioRuleService
{
    // The percentages come from .editorconfig; the added-line counts come from the git-backed
    // MSBuild target through CompilerVisibleProperty.
    TestRatioSettings GetSettings(AnalyzerConfigOptions editorConfigOptions, AnalyzerConfigOptions buildPropertyOptions);

    TestRatioViolation? GetViolation(Compilation compilation, TestRatioSettings settings, CancellationToken cancellationToken);
}
