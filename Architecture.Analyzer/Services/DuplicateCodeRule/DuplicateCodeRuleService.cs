using System;
using System.Collections.Generic;
using Architecture.Analyzer.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Architecture.Analyzer.Services.DuplicateCodeRule;

internal sealed class DuplicateCodeRuleService : IDuplicateCodeRuleService
{
    private const string AnalyzerConfigPrefix = "architecture_analyzer.";
    private const string MinTokensOption = AnalyzerConfigPrefix + "duplicate_code_min_tokens";
    private const string SimilarityOption = AnalyzerConfigPrefix + "duplicate_code_similarity_percent";

    private const int DefaultMinTokens = 30;
    private const int DefaultSimilarityPercent = 90;

    // Above this many candidate methods, only exact structural matches are searched, so the rule
    // stays linear on very large projects instead of comparing pairs.
    private const int MaxMethodsForSimilaritySearch = 4000;

    // Window length for the shingles. 5 consecutive token kinds is long enough that an accidental
    // match is unlikely, short enough that a lightly edited copy still shares most windows.
    private const int ShingleSize = 5;

    public (int MinTokens, int SimilarityPercent) GetSettings(AnalyzerConfigOptions options)
    {
        var minTokens = GetIntOption(options, MinTokensOption, DefaultMinTokens, 1, int.MaxValue);
        var similarity = GetIntOption(options, SimilarityOption, DefaultSimilarityPercent, 1, 100);
        return (minTokens, similarity);
    }

    public MethodFingerprint? CreateFingerprint(MethodDeclarationSyntax method, int minTokens)
    {
        SyntaxNode? body = method.Body;
        if (body is null)
        {
            body = method.ExpressionBody;
        }

        if (body is null)
        {
            return null; // abstract, partial or interface declaration: nothing to compare.
        }

        var kinds = new List<int>();
        foreach (var token in body.DescendantTokens())
        {
            // Only the KIND of each token is kept, never its text: two different identifiers are
            // both an identifier, 1 and 42 are both a numeric literal. Renaming changes nothing.
            kinds.Add(token.RawKind);
        }

        if (kinds.Count < minTokens)
        {
            return null;
        }

        var hash = 17;
        foreach (var kind in kinds)
        {
            unchecked
            {
                hash = (hash * 31) + kind;
            }
        }

        // Windows of consecutive kinds ("shingles"): order-sensitive, unlike counting tokens one by
        // one, so a body with the same tokens in another order is not mistaken for a copy.
        var shingles = new Dictionary<int, int>();
        var shingleCount = 0;
        for (var start = 0; start + ShingleSize <= kinds.Count; start++)
        {
            var windowHash = 17;
            for (var offset = 0; offset < ShingleSize; offset++)
            {
                unchecked
                {
                    windowHash = (windowHash * 31) + kinds[start + offset];
                }
            }

            shingles.TryGetValue(windowHash, out var occurrences);
            shingles[windowHash] = occurrences + 1;
            shingleCount++;
        }

        return new MethodFingerprint(
            BuildDisplayName(method),
            hash,
            kinds.Count,
            shingles,
            shingleCount,
            method.Identifier.GetLocation(),
            method.SyntaxTree.FilePath ?? string.Empty,
            method.SpanStart);
    }

    public List<DuplicateCodeViolation> FindDuplicates(List<MethodFingerprint> fingerprints, int similarityPercent)
    {
        var violations = new List<DuplicateCodeViolation>();
        if (fingerprints.Count < 2)
        {
            return violations;
        }

        // Ascending token count, then source position: the window below relies on that ordering, and
        // the tie-breaker keeps the result identical from one compilation to the next.
        fingerprints.Sort(CompareByLengthThenPosition);

        var reported = new HashSet<int>();
        var exactOnly = fingerprints.Count > MaxMethodsForSimilaritySearch;

        for (var i = 0; i < fingerprints.Count; i++)
        {
            var current = fingerprints[i];
            for (var j = i + 1; j < fingerprints.Count; j++)
            {
                var candidate = fingerprints[j];

                // Similarity can never exceed shortest/longest, so once a candidate is too long for
                // the threshold, every later one is too and the window closes.
                if (candidate.TokenCount * similarityPercent > current.TokenCount * 100)
                {
                    break;
                }

                var similarity = Similarity(current, candidate, exactOnly);
                if (similarity < similarityPercent)
                {
                    continue;
                }

                // Report whichever comes later in the source and point at the earlier one: the
                // earlier method is the one to reuse.
                var duplicateIndex = ComparePosition(current, candidate) <= 0 ? j : i;
                if (!reported.Add(duplicateIndex))
                {
                    continue;
                }

                var duplicate = fingerprints[duplicateIndex];
                var original = fingerprints[duplicateIndex == j ? i : j];
                violations.Add(new DuplicateCodeViolation(
                    duplicate.DisplayName,
                    original.DisplayName,
                    similarity,
                    duplicate.Location));
            }
        }

        violations.Sort((left, right) => string.CompareOrdinal(left.DuplicateName, right.DuplicateName));
        return violations;
    }

    private static int Similarity(MethodFingerprint left, MethodFingerprint right, bool exactOnly)
    {
        if (left.Hash == right.Hash && left.TokenCount == right.TokenCount)
        {
            return 100; // identical token-kind sequence.
        }

        if (exactOnly)
        {
            return 0;
        }

        // Overlap of shared windows: cheap, and order-sensitive.
        var shared = 0;
        foreach (var entry in left.Shingles)
        {
            if (right.Shingles.TryGetValue(entry.Key, out var occurrences))
            {
                shared += Math.Min(entry.Value, occurrences);
            }
        }

        var longest = Math.Max(left.ShingleCount, right.ShingleCount);
        return longest == 0 ? 0 : shared * 100 / longest;
    }

    private static int CompareByLengthThenPosition(MethodFingerprint left, MethodFingerprint right)
    {
        var byLength = left.TokenCount.CompareTo(right.TokenCount);
        return byLength != 0 ? byLength : ComparePosition(left, right);
    }

    private static int ComparePosition(MethodFingerprint left, MethodFingerprint right)
    {
        var byFile = string.CompareOrdinal(left.FilePath, right.FilePath);
        return byFile != 0 ? byFile : left.SpanStart.CompareTo(right.SpanStart);
    }

    private static string BuildDisplayName(MethodDeclarationSyntax method)
    {
        var name = method.Identifier.ValueText;
        return method.Parent is TypeDeclarationSyntax type
            ? type.Identifier.ValueText + "." + name
            : name;
    }

    private static int GetIntOption(AnalyzerConfigOptions options, string key, int fallback, int min, int max)
    {
        if (!options.TryGetValue(key, out var configuredValue)
            || string.IsNullOrWhiteSpace(configuredValue)
            || !int.TryParse(configuredValue.Trim(), out var value)
            || value < min
            || value > max)
        {
            return fallback;
        }

        return value;
    }
}
