using System.Collections.Generic;
using Architecture.Analyzer.Models;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Architecture.Analyzer.Services.DuplicateCodeRule;

internal interface IDuplicateCodeRuleService
{
    (int MinTokens, int SimilarityPercent) GetSettings(AnalyzerConfigOptions options);

    // Null when the method has no body, or when its body is shorter than MinTokens: trivial
    // accessors and one-liners share a shape without being duplication.
    MethodFingerprint? CreateFingerprint(MethodDeclarationSyntax method, int minTokens);

    List<DuplicateCodeViolation> FindDuplicates(List<MethodFingerprint> fingerprints, int similarityPercent);
}
