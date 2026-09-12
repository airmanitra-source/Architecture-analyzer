using Microsoft.CodeAnalysis;

namespace Architecture.Analyzer.Models;

internal sealed class DuplicateCodeViolation
{
    public DuplicateCodeViolation(string duplicateName, string originalName, int similarityPercent, Location location)
    {
        DuplicateName = duplicateName;
        OriginalName = originalName;
        SimilarityPercent = similarityPercent;
        Location = location;
    }

    public string DuplicateName { get; }

    public string OriginalName { get; }

    public int SimilarityPercent { get; }

    public Location Location { get; }
}
