using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Architecture.Analyzer.Services.OscillationRule;

internal interface IOscillationRuleService
{
    // Windows of code recently removed, keyed by content hash, produced by the shipped MSBuild task
    // from the last N commits. Empty when the task did not run (rule disabled): the rule then stays
    // silent.
    Dictionary<string, (string Sha, string Subject, int Ago)> ReadRemoved(
        ImmutableArray<AdditionalText> additionalFiles,
        AnalyzerConfigOptionsProvider optionsProvider,
        CancellationToken cancellationToken);

    // Windows this change adds, grouped by the absolute file path they live in, each with its 1-based
    // start line, produced by the same task from the working tree.
    Dictionary<string, List<(int Line, string Hash)>> ReadAdded(
        ImmutableArray<AdditionalText> additionalFiles,
        AnalyzerConfigOptionsProvider optionsProvider,
        CancellationToken cancellationToken);
}
