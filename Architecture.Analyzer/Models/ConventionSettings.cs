using System.Collections.Generic;

namespace Architecture.Analyzer.Models;

internal sealed class ConventionSettings
{
    public ConventionSettings(int minSupport, int minRatio, HashSet<string> ignored, HashSet<string> explicitFolderSuffixes)
    {
        MinSupport = minSupport;
        MinRatio = minRatio;
        Ignored = ignored;
        ExplicitFolderSuffixes = explicitFolderSuffixes;
    }

    // Fewer examples than this and a regularity is a coincidence, not a convention.
    public int MinSupport { get; }

    // Share (in percent) the majority value must reach.
    public int MinRatio { get; }

    // "Suffix>trait" pairs the user has told us to leave alone.
    public HashSet<string> Ignored { get; }

    // Suffixes whose folder is already governed explicitly by ARCH001/ARCH003. The explicit rule
    // wins: no folder convention is inferred for them, so a deviation is never reported twice.
    public HashSet<string> ExplicitFolderSuffixes { get; }
}
