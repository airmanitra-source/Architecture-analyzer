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
    Dictionary<string, int> BuildBaseline(
        ImmutableArray<AdditionalText> additionalFiles,
        AnalyzerConfigOptionsProvider optionsProvider,
        CancellationToken cancellationToken);

    int ComputeComplexity(MethodDeclarationSyntax method);

    ComplexityRatchetViolation? Check(MethodDeclarationSyntax method, Dictionary<string, int> baseline, int allowedIncrease);
}
