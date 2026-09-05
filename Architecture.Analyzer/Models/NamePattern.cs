using System;

namespace Architecture.Analyzer.Models;

internal enum NameMatchKind
{
    Exact,
    Prefix,
    Suffix,
    Contains
}

// A name-matching pattern parsed from a configuration token. A leading and/or
// trailing '*' selects the match kind, so the same syntax expresses every mode:
//   "*Module"  -> Suffix   (name ends with "Module")
//   "Module*"  -> Prefix   (name starts with "Module")
//   "*Module*" -> Contains (name contains "Module")
//   "Module"   -> Exact    (name equals "Module")
internal readonly struct NamePattern
{
    private NamePattern(string raw, string token, NameMatchKind kind)
    {
        Raw = raw;
        Token = token;
        Kind = kind;
    }

    // The original token, kept for display in diagnostic messages (e.g. "*Module").
    public string Raw { get; }

    public string Token { get; }

    public NameMatchKind Kind { get; }

    // Returns null when the token is empty (e.g. "" or "*"), i.e. not a usable pattern.
    public static NamePattern? Parse(string raw)
    {
        if (raw is null)
        {
            return null;
        }

        var trimmed = raw.Trim();
        var startsWithStar = trimmed.StartsWith("*", StringComparison.Ordinal);
        var endsWithStar = trimmed.EndsWith("*", StringComparison.Ordinal);
        var token = trimmed.Trim('*');
        if (token.Length == 0)
        {
            return null;
        }

        NameMatchKind kind;
        if (startsWithStar && endsWithStar)
        {
            kind = NameMatchKind.Contains;
        }
        else if (startsWithStar)
        {
            kind = NameMatchKind.Suffix;
        }
        else if (endsWithStar)
        {
            kind = NameMatchKind.Prefix;
        }
        else
        {
            kind = NameMatchKind.Exact;
        }

        return new NamePattern(trimmed, token, kind);
    }

    public bool Matches(string? name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        return Kind switch
        {
            NameMatchKind.Prefix => name!.StartsWith(Token, StringComparison.Ordinal),
            NameMatchKind.Suffix => name!.EndsWith(Token, StringComparison.Ordinal),
            NameMatchKind.Contains => name!.IndexOf(Token, StringComparison.Ordinal) >= 0,
            _ => name!.Equals(Token, StringComparison.Ordinal),
        };
    }
}
