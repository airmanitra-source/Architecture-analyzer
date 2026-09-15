using System.Collections.Generic;
using System.Text;

namespace Architecture.Analyzer.Build
{
    internal struct RemovedWindow
    {
        public string Hash;
        public string Sha;
        public string Subject;
        public int Ago;
    }

    internal struct AddedWindow
    {
        public string Hash;
        public string Path;
        public int StartLine;
    }

    // Pure, framework-free helpers for the oscillation rule (ARCH027): no Microsoft.Build, no IO, no
    // Roslyn, so they are unit-tested directly. The MSBuild task shells out to git and feeds the raw
    // output here; the analyzer only intersects the resulting hashes. Because the removed side (git
    // history) and the added side (working tree) are hashed by the SAME code here, the two always line
    // up — there is no second implementation to drift from.
    internal static class OscillationScan
    {
        // A window is a run of this many consecutive non-trivial lines. Three defeats coincidental
        // single-line matches (a stray "return true;" removed here and added there) while still
        // catching a re-introduced block.
        public const int BlockLines = 3;

        private const ulong FnvOffset = 14695981039346656037UL;
        private const ulong FnvPrime = 1099511628211UL;

        // The git log format begins each commit line with a record separator (0x1e) and splits the
        // sha from the subject with a unit separator (0x1f). Neither appears in source, and neither is
        // a space, so ProcessStartInfo cannot split the format into separate arguments.
        private const char RecordSeparator = (char)30;
        private const char UnitSeparator = (char)31;

        // Trim and collapse internal whitespace, so a re-add with different indentation still matches.
        internal static string NormalizeLine(string line)
        {
            if (line == null)
            {
                return string.Empty;
            }

            var builder = new StringBuilder(line.Length);
            var inWhitespace = false;
            var started = false;
            foreach (var c in line)
            {
                if (c == ' ' || c == '\t' || c == '\r' || c == '\n' || c == '\f' || c == '\v')
                {
                    inWhitespace = true;
                    continue;
                }

                if (inWhitespace && started)
                {
                    builder.Append(' ');
                }

                inWhitespace = false;
                started = true;
                builder.Append(c);
            }

            return builder.ToString();
        }

        // Structural-only lines (braces, parens, semicolons, commas) carry no signal and would match
        // everywhere, so they never enter a window and they break a run.
        internal static bool IsTrivial(string normalized)
        {
            if (normalized.Length == 0)
            {
                return true;
            }

            foreach (var c in normalized)
            {
                if (c != '{' && c != '}' && c != '(' && c != ')' && c != ';' && c != ',')
                {
                    return false;
                }
            }

            return true;
        }

        internal static ulong HashLine(string normalized)
        {
            var hash = FnvOffset;
            foreach (var c in normalized)
            {
                unchecked
                {
                    hash ^= c;
                    hash *= FnvPrime;
                }
            }

            return hash;
        }

        internal static string CombineWindow(IReadOnlyList<ulong> hashes, int start, int count)
        {
            var hash = FnvOffset;
            for (var i = 0; i < count; i++)
            {
                var value = hashes[start + i];
                for (var b = 0; b < 8; b++)
                {
                    unchecked
                    {
                        hash ^= (value >> (b * 8)) & 0xff;
                        hash *= FnvPrime;
                    }
                }
            }

            return hash.ToString("x16");
        }

        // Parses `git log --no-merges --unified=0 -n N --format=format:%x1e%H%x1f%s -p -- *.cs`.
        // Each commit begins with a line starting with the record separator (0x1e), then sha, unit
        // separator (0x1f), subject; removed content lines start with '-'. Windows are runs of
        // consecutive non-trivial removed lines.
        internal static List<RemovedWindow> ParseRemovedWindows(string gitOutput)
        {
            var result = new List<RemovedWindow>();
            var seen = new HashSet<string>();
            if (string.IsNullOrEmpty(gitOutput))
            {
                return result;
            }

            var sha = string.Empty;
            var subject = string.Empty;
            var ago = 0;
            var run = new List<ulong>();

            void Flush()
            {
                if (run.Count >= BlockLines && sha.Length > 0)
                {
                    for (var i = 0; i + BlockLines <= run.Count; i++)
                    {
                        var windowHash = CombineWindow(run, i, BlockLines);
                        if (seen.Add(windowHash))
                        {
                            result.Add(new RemovedWindow { Hash = windowHash, Sha = sha, Subject = subject, Ago = ago });
                        }
                    }
                }

                run.Clear();
            }

            foreach (var raw in SplitLines(gitOutput))
            {
                if (raw.Length > 0 && raw[0] == RecordSeparator)
                {
                    Flush();
                    ago++;
                    var rest = raw.Substring(1);
                    var separator = rest.IndexOf(UnitSeparator);
                    if (separator < 0)
                    {
                        sha = Short(rest);
                        subject = string.Empty;
                    }
                    else
                    {
                        sha = Short(rest.Substring(0, separator));
                        subject = rest.Substring(separator + 1);
                    }

                    continue;
                }

                if (raw.Length > 0 && raw[0] == '-' && !raw.StartsWith("---"))
                {
                    var normalized = NormalizeLine(raw.Substring(1));
                    if (IsTrivial(normalized))
                    {
                        Flush();
                    }
                    else
                    {
                        run.Add(HashLine(normalized));
                    }
                }
                else
                {
                    Flush();
                }
            }

            Flush();
            return result;
        }

        // Parses `git diff HEAD --unified=0 -- *.cs`, returning the new-side windows the working tree
        // added, each with its file (repo-relative, as git prints it) and 1-based start line.
        internal static List<AddedWindow> ParseAddedWindows(string gitDiff)
        {
            var result = new List<AddedWindow>();
            if (string.IsNullOrEmpty(gitDiff))
            {
                return result;
            }

            string path = null;
            var newLine = 0;
            var run = new List<ulong>();
            var runStart = 0;

            void Flush()
            {
                if (path != null && run.Count >= BlockLines)
                {
                    for (var i = 0; i + BlockLines <= run.Count; i++)
                    {
                        result.Add(new AddedWindow
                        {
                            Hash = CombineWindow(run, i, BlockLines),
                            Path = path,
                            StartLine = runStart + i,
                        });
                    }
                }

                run.Clear();
            }

            foreach (var raw in SplitLines(gitDiff))
            {
                if (raw.StartsWith("+++ "))
                {
                    Flush();
                    var target = raw.Substring(4);
                    path = target == "/dev/null" ? null : (target.StartsWith("b/") ? target.Substring(2) : target);
                    continue;
                }

                if (raw.StartsWith("@@"))
                {
                    Flush();
                    newLine = ParseHunkNewStart(raw);
                    continue;
                }

                if (raw.StartsWith("--- ") || raw.StartsWith("diff ") || raw.StartsWith("index ") || raw.StartsWith("\\"))
                {
                    Flush();
                    continue;
                }

                if (raw.Length > 0 && raw[0] == '+')
                {
                    var normalized = NormalizeLine(raw.Substring(1));
                    if (IsTrivial(normalized))
                    {
                        Flush();
                    }
                    else
                    {
                        if (run.Count == 0)
                        {
                            runStart = newLine;
                        }

                        run.Add(HashLine(normalized));
                    }

                    newLine++;
                    continue;
                }

                if (raw.Length > 0 && raw[0] == '-')
                {
                    Flush(); // a removed line breaks the added run and consumes no new-side line.
                    continue;
                }

                Flush();
                newLine++; // context line (rare with -U0) consumes a new-side line.
            }

            Flush();
            return result;
        }

        // Windows for a whole file (used for untracked files, which git diff does not show), 1-based.
        internal static List<AddedWindow> AddedWindowsFromContent(string path, IReadOnlyList<string> lines)
        {
            var result = new List<AddedWindow>();
            var run = new List<ulong>();
            var runStart = 0;

            for (var n = 0; n < lines.Count; n++)
            {
                var normalized = NormalizeLine(lines[n]);
                if (IsTrivial(normalized))
                {
                    FlushContent(result, path, run, runStart);
                }
                else
                {
                    if (run.Count == 0)
                    {
                        runStart = n + 1;
                    }

                    run.Add(HashLine(normalized));
                }
            }

            FlushContent(result, path, run, runStart);
            return result;
        }

        private static void FlushContent(List<AddedWindow> result, string path, List<ulong> run, int runStart)
        {
            if (run.Count >= BlockLines)
            {
                for (var i = 0; i + BlockLines <= run.Count; i++)
                {
                    result.Add(new AddedWindow
                    {
                        Hash = CombineWindow(run, i, BlockLines),
                        Path = path,
                        StartLine = runStart + i,
                    });
                }
            }

            run.Clear();
        }

        private static int ParseHunkNewStart(string hunk)
        {
            var plus = hunk.IndexOf('+');
            if (plus < 0)
            {
                return 0;
            }

            var index = plus + 1;
            var value = 0;
            var any = false;
            while (index < hunk.Length && hunk[index] >= '0' && hunk[index] <= '9')
            {
                value = (value * 10) + (hunk[index] - '0');
                index++;
                any = true;
            }

            return any ? value : 0;
        }

        private static string Short(string sha)
            => sha.Length <= 7 ? sha : sha.Substring(0, 7);

        private static IEnumerable<string> SplitLines(string value)
        {
            var start = 0;
            for (var i = 0; i < value.Length; i++)
            {
                if (value[i] == '\n')
                {
                    var end = i;
                    if (end > start && value[end - 1] == '\r')
                    {
                        end--;
                    }

                    yield return value.Substring(start, end - start);
                    start = i + 1;
                }
            }

            if (start < value.Length)
            {
                var end = value.Length;
                if (end > start && value[end - 1] == '\r')
                {
                    end--;
                }

                yield return value.Substring(start, end - start);
            }
        }
    }
}
