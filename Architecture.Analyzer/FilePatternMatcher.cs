using System;

namespace Architecture.Analyzer;

// Matches a file against an allow-list pattern (ARCH003 per-project file list).
//  - A pattern with no path separator matches the file NAME anywhere in the project ("*Dto.cs").
//  - A pattern with a separator matches the file's PATH relative to the project root, root-anchored
//    ("Models/*BusinessModel.cs"). When the project directory is unknown, the pattern is matched as a
//    suffix of the full path instead, so folder rules still work in degraded conditions.
// '*' matches any run of characters within one segment; '**' matches across segments. Comparison is
// ordinal, like the rest of the analyzer.
internal static class FilePatternMatcher
{
    public static bool Matches(string pattern, string fileName, string? relativePath, string fullPath)
    {
        if (string.IsNullOrEmpty(pattern))
        {
            return false;
        }

        var normalized = pattern.Replace('\\', '/').Trim();
        if (normalized.IndexOf('/') < 0)
        {
            return MatchSegment(normalized, fileName ?? string.Empty);
        }

        string target;
        bool anchored;
        if (!string.IsNullOrEmpty(relativePath))
        {
            target = relativePath!;
            anchored = true;
        }
        else
        {
            target = (fullPath ?? string.Empty).Replace('\\', '/');
            anchored = false;
        }

        var patternSegments = normalized.Split('/');
        var pathSegments = target.Split('/');

        if (anchored)
        {
            return MatchFrom(patternSegments, 0, pathSegments, 0);
        }

        // Unknown project root: allow any leading segments, i.e. match the pattern as a path suffix.
        for (var start = 0; start <= pathSegments.Length; start++)
        {
            if (MatchFrom(patternSegments, 0, pathSegments, start))
            {
                return true;
            }
        }

        return false;
    }

    private static bool MatchFrom(string[] pattern, int pi, string[] path, int si)
    {
        while (pi < pattern.Length)
        {
            if (pattern[pi] == "**")
            {
                for (var k = si; k <= path.Length; k++)
                {
                    if (MatchFrom(pattern, pi + 1, path, k))
                    {
                        return true;
                    }
                }

                return false;
            }

            if (si >= path.Length || !MatchSegment(pattern[pi], path[si]))
            {
                return false;
            }

            pi++;
            si++;
        }

        return si == path.Length;
    }

    // Wildcard match within a single segment: '*' matches any run of characters (never a '/').
    private static bool MatchSegment(string pattern, string text)
    {
        int p = 0, t = 0, star = -1, mark = 0;
        while (t < text.Length)
        {
            if (p < pattern.Length && pattern[p] == text[t])
            {
                p++;
                t++;
            }
            else if (p < pattern.Length && pattern[p] == '*')
            {
                star = p;
                mark = t;
                p++;
            }
            else if (star != -1)
            {
                p = star + 1;
                mark++;
                t = mark;
            }
            else
            {
                return false;
            }
        }

        while (p < pattern.Length && pattern[p] == '*')
        {
            p++;
        }

        return p == pattern.Length;
    }
}
