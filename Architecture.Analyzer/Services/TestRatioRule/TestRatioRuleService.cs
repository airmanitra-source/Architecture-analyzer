using System.Linq;
using System.Threading;
using Architecture.Analyzer.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Architecture.Analyzer.Services.TestRatioRule;

internal sealed class TestRatioRuleService : ITestRatioRuleService
{
    private const string AnalyzerConfigPrefix = "architecture_analyzer.";
    private const string MinTestPercentOption = AnalyzerConfigPrefix + "min_test_lines_percent";
    private const string MinAddedLinesOption = AnalyzerConfigPrefix + "test_ratio_min_added_lines";

    // Fed by the git CLI at build time (see Architecture.Analyzer.targets).
    private const string AddedProductionProperty = "build_property.ArchitectureLocAddedProductionLines";
    private const string AddedTestProperty = "build_property.ArchitectureLocAddedTestLines";

    private const int DefaultMinAddedLines = 50;

    public TestRatioSettings GetSettings(AnalyzerConfigOptions editorConfigOptions, AnalyzerConfigOptions buildPropertyOptions)
        => new(
            GetIntOption(editorConfigOptions, MinTestPercentOption, 0),
            GetIntOption(editorConfigOptions, MinAddedLinesOption, DefaultMinAddedLines),
            GetIntOption(buildPropertyOptions, AddedProductionProperty, 0),
            GetIntOption(buildPropertyOptions, AddedTestProperty, 0));

    public TestRatioViolation? GetViolation(Compilation compilation, TestRatioSettings settings, CancellationToken cancellationToken)
    {
        if (!settings.IsEnabled || settings.AddedProductionLines < settings.MinAddedLines)
        {
            return null;
        }

        // Integer ratio: 30 test lines for 200 production lines is 15%.
        var actualPercent = settings.AddedTestLines * 100 / settings.AddedProductionLines;
        if (actualPercent >= settings.MinTestPercent)
        {
            return null;
        }

        var syntaxTree = compilation.SyntaxTrees.FirstOrDefault();
        if (syntaxTree is null)
        {
            return null;
        }

        return new TestRatioViolation(
            settings.AddedProductionLines,
            settings.AddedTestLines,
            actualPercent,
            settings.MinTestPercent,
            syntaxTree.GetRoot(cancellationToken).GetLocation());
    }

    private static int GetIntOption(AnalyzerConfigOptions options, string key, int fallback)
    {
        if (!options.TryGetValue(key, out var configuredValue)
            || string.IsNullOrWhiteSpace(configuredValue)
            || !int.TryParse(configuredValue.Trim(), out var value)
            || value < 0)
        {
            return fallback;
        }

        return value;
    }
}
