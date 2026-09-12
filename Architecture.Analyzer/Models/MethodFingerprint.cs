using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Architecture.Analyzer.Models;

// A method reduced to the *shape* of its body: the sequence of token kinds, with identifiers and
// literal values discarded. Two methods that differ only by names or constants share a fingerprint,
// which is exactly the "copy, paste, rename" duplication an agent produces.
internal sealed class MethodFingerprint
{
    public MethodFingerprint(
        string displayName,
        int hash,
        int tokenCount,
        Dictionary<int, int> shingles,
        int shingleCount,
        Location location,
        string filePath,
        int spanStart)
    {
        DisplayName = displayName;
        Hash = hash;
        TokenCount = tokenCount;
        Shingles = shingles;
        ShingleCount = shingleCount;
        Location = location;
        FilePath = filePath;
        SpanStart = spanStart;
    }

    public string DisplayName { get; }

    // Hash of the ordered token-kind sequence: equal hashes mean an identical structure.
    public int Hash { get; }

    public int TokenCount { get; }

    // Hashes of every window of consecutive token kinds -> occurrences. Comparing windows instead of
    // individual tokens keeps the comparison sensitive to ORDER: two methods built from the same
    // tokens arranged differently share almost no window, and are therefore not reported.
    public Dictionary<int, int> Shingles { get; }

    public int ShingleCount { get; }

    public Location Location { get; }

    // Source position, used only to order results deterministically.
    public string FilePath { get; }

    public int SpanStart { get; }
}
