using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using Architecture.Analyzer.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Architecture.Analyzer.Services.ComplexityRatchetRule;

internal interface IComplexityRatchetRuleService
{
    // How much a method is allowed to grow before it is reported. 0 (the default) is a strict
    // ratchet: complexity may stay equal or go down, never up.
    int GetAllowedIncrease(AnalyzerConfigOptions options);

    // Complexity of every method as it stands at HEAD, keyed by type + method + parameter count so
    // the comparison survives the method moving inside its file.
    Dictionary<(string Type, string Method, int Arity), int> BuildBaseline(
        ImmutableArray<AdditionalText> additionalFiles,
        AnalyzerConfigOptionsProvider optionsProvider,
        CancellationToken cancellationToken);

    int ComputeComplexity(MethodDeclarationSyntax method);

    ComplexityRatchetViolation? Check(
        MethodDeclarationSyntax method,
        Dictionary<(string Type, string Method, int Arity), int> baseline,
        int allowedIncrease);

    // How many times each file was touched recently, produced by the shipped MSBuild target from
    // the git history. Empty when the table is absent: hotspot mode then stays off.
    Dictionary<string, int> BuildChurn(
        ImmutableArray<AdditionalText> additionalFiles,
        AnalyzerConfigOptionsProvider optionsProvider,
        CancellationToken cancellationToken);

    // Churn value above which a file counts as a hotspot, derived from a percentile so it adapts to
    // the repository instead of relying on an absolute number.
    int GetHotspotThreshold(Dictionary<string, int> churn, AnalyzerConfigOptions options);

    // The recent change count when the file is a hotspot, 0 otherwise.
    int GetHotspotChurn(string? filePath, Dictionary<string, int> churn, int threshold);
}
