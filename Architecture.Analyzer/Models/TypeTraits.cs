using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Architecture.Analyzer.Models;

// The observable traits of one type, from which conventions are inferred. Every value is a plain
// string so the inference can treat all traits uniformly.
internal sealed class TypeTraits
{
    public TypeTraits(
        string name,
        IReadOnlyList<string> suffixes,
        string filePath,
        Dictionary<string, string> values,
        Location location)
    {
        Name = name;
        Suffixes = suffixes;
        FilePath = filePath;
        Values = values;
        Location = location;
    }

    public string Name { get; }

    // Candidate grouping keys, most specific first: "CustomerBusinessModel" gives
    // ["BusinessModel", "Model"]. Conventions are learned per suffix.
    public IReadOnlyList<string> Suffixes { get; }

    public string FilePath { get; }

    // Trait name -> observed value (folder, namespace, kind, sealed, static, accessibility, base).
    public Dictionary<string, string> Values { get; }

    public Location Location { get; }
}
