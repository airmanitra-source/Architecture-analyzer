using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using Architecture.Analyzer.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Architecture.Analyzer.Services.ConventionRule;

internal interface IConventionRuleService
{
    ConventionSettings GetSettings(AnalyzerConfigOptions options);

    // Files modified or added by the current change, produced by the shipped MSBuild target. Empty
    // when absent — and then nothing is reported: with no notion of "new", every legacy deviation
    // would surface at once.
    HashSet<string> ReadChangedFiles(
        ImmutableArray<AdditionalText> additionalFiles,
        AnalyzerConfigOptionsProvider optionsProvider,
        CancellationToken cancellationToken);

    // Null for types that cannot carry a convention: nested, implicit, or single-word names.
    TypeTraits? Collect(INamedTypeSymbol type);

    // Learned from EVERY type of the project — the legacy majority defines the norm.
    List<Convention> Infer(List<TypeTraits> types, ConventionSettings settings);

    // Checked only against the types living in changed files — only new code must conform.
    List<ConventionViolation> Check(
        List<TypeTraits> types,
        List<Convention> conventions,
        HashSet<string> changedFiles);
}
