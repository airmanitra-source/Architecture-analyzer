namespace Architecture.Analyzer.Models;

// "Types whose name ends with Suffix almost all have Trait = Expected" — observed, not configured.
internal sealed class Convention
{
    public Convention(string suffix, string trait, string expected, int matching, int total)
    {
        Suffix = suffix;
        Trait = trait;
        Expected = expected;
        Matching = matching;
        Total = total;
    }

    public string Suffix { get; }

    public string Trait { get; }

    public string Expected { get; }

    // How many of the Total types share the expected value: the evidence shown in the message.
    public int Matching { get; }

    public int Total { get; }
}
