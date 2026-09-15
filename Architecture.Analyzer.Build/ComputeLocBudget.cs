using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Architecture.Analyzer.Build
{
    public sealed class ComputeLocBudget : Task
    {
        public string WorkingDirectory { get; set; }
        public string ProjectFiles { get; set; }
        public string IsDesignTimeBuild { get; set; }

        // Where the HEAD copies are written (inside obj/, never in the repository).
        public string BaselineDirectory { get; set; }

        // The HEAD version of every changed file, handed to the analyzer as AdditionalFiles.
        [Output] public ITaskItem[] BaselineFiles { get; set; }

        // How often each file changed recently. One shared table per repository, not per project.
        [Output] public ITaskItem[] ChurnTable { get; set; }

        // The files this change touched (modified or added), so rules that only judge new code know
        // which types are new.
        [Output] public ITaskItem[] ChangedFiles { get; set; }

        // The changed TEST files, handed to the production compilation as source it never compiles, so
        // the related-test gate (ARCH025) can see which tests this change added or modified.
        [Output] public ITaskItem[] ChangedTestFiles { get; set; }

        // Windows of consecutive lines removed by recent commits, and windows this change adds, for the
        // oscillation rule (ARCH027). The analyzer intersects the two by hash.
        [Output] public ITaskItem[] OscillationRemoved { get; set; }

        [Output] public ITaskItem[] OscillationAdded { get; set; }

        private readonly List<string> _changedPaths = new List<string>();

        [Output] public int ProjectAddedLines { get; set; }
        [Output] public int ProjectBaselineLines { get; set; }
        [Output] public int SolutionAddedLines { get; set; }
        [Output] public int SolutionBaselineLines { get; set; }

        // Added lines split by destination: tests live in their own project, so the ratio between
        // the two only makes sense solution-wide.
        [Output] public int AddedProductionLines { get; set; }
        [Output] public int AddedTestLines { get; set; }

        private string _repoRoot;

        public override bool Execute()
        {
            try
            {
                var root = RunGit("rev-parse --show-toplevel");
                if (root == null)
                {
                    // git missing or not a repository: leave every count at 0 so budgets stay inactive.
                    return true;
                }

                root = root.Trim();

                // Run every following command from the repository root so paths are root-relative and the
                // whole repository is in scope (ls-files defaults to the current directory otherwise).
                _repoRoot = root;
                var testPatterns = ReadTestPatterns();

                var projectFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (!string.IsNullOrEmpty(ProjectFiles))
                {
                    foreach (var raw in ProjectFiles.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        var trimmed = raw.Trim();
                        if (trimmed.Length != 0)
                        {
                            projectFiles.Add(Path.GetFullPath(trimmed));
                        }
                    }
                }

                // Added lines (working tree + index vs HEAD) for tracked .cs files, via numstat.
                var numstat = RunGit("diff --numstat HEAD -- *.cs");
                foreach (var line in SplitLines(numstat))
                {
                    var columns = line.Split('\t');
                    if (columns.Length < 3)
                    {
                        continue;
                    }

                    if (!int.TryParse(columns[0], out var added))
                    {
                        continue; // "-" marks a binary file.
                    }

                    var full = ToFullPath(root, columns[2]);
                    SolutionAddedLines += added;
                    Accumulate(columns[2], added, testPatterns);
                    _changedPaths.Add(full);
                    if (projectFiles.Contains(full))
                    {
                        ProjectAddedLines += added;
                    }
                }

                // Untracked .cs files count fully as added lines.
                var untracked = RunGit("ls-files --others --exclude-standard -- *.cs");
                foreach (var relative in SplitLines(untracked))
                {
                    var full = ToFullPath(root, relative);
                    var added = CountLines(full);
                    SolutionAddedLines += added;
                    Accumulate(relative, added, testPatterns);
                    _changedPaths.Add(full);
                    if (projectFiles.Contains(full))
                    {
                        ProjectAddedLines += added;
                    }
                }

                // Baseline = size of the existing tracked .cs code base (working copy is a stable proxy
                // for HEAD; the two differ only by the change being measured, negligible for a percentage).
                var tracked = RunGit("ls-files -- *.cs");
                foreach (var relative in SplitLines(tracked))
                {
                    var full = ToFullPath(root, relative);
                    var count = CountLines(full);
                    SolutionBaselineLines += count;
                    if (projectFiles.Contains(full))
                    {
                        ProjectBaselineLines += count;
                    }
                }

                BaselineFiles = WriteHeadBaseline();
                ChurnTable = WriteChurnTable();
                ChangedFiles = WriteChangedFiles();
                ChangedTestFiles = WriteChangedTestFiles(testPatterns);

                var oscillationCommits = ReadIntOption("architecture_analyzer.oscillation_window_commits");
                if (oscillationCommits > 0)
                {
                    OscillationRemoved = WriteOscillationRemoved(oscillationCommits);
                    OscillationAdded = WriteOscillationAdded();
                }

                LogBudgetGauge();
                WriteImpactHistory();
                return true;
            }
            catch (Exception exception)
            {
                // Never fail the build over the budget computation; just log and leave counts at 0.
                Log.LogMessage(MessageImportance.Low, "Architecture.Analyzer LOC budget computation skipped: " + exception.Message);
                return true;
            }
        }

        private const int DefaultChurnWindowDays = 90;
        private const int ChurnCacheMinutes = 10;

        // Counts how many commits touched each file over a recent window. Churn is never compared to
        // a previous value — it only ranks files — so it is written once per repository and shared
        // by every project of the solution rather than recomputed for each.
        private ITaskItem[] WriteChurnTable()
        {
            var empty = new ITaskItem[0];

            var windowDays = ReadIntOption("architecture_analyzer.churn_window_days");
            if (windowDays <= 0)
            {
                windowDays = DefaultChurnWindowDays;
            }

            var cachePath = ChurnCachePath(windowDays);
            if (!IsFresh(cachePath))
            {
                var history = RunGit("log --no-merges --since=" + windowDays.ToString() + ".days --format=format: --name-only -- *.cs");
                if (history == null)
                {
                    return empty;
                }

                var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var relative in SplitLines(history))
                {
                    var full = ToFullPath(_repoRoot, relative);
                    int seen;
                    counts.TryGetValue(full, out seen);
                    counts[full] = seen + 1;
                }

                var lines = new List<string>();
                foreach (var pair in counts)
                {
                    // "count|absolute path" — '|' cannot appear in a Windows path, so no escaping.
                    lines.Add(pair.Value.ToString() + "|" + pair.Key);
                }

                Directory.CreateDirectory(Path.GetDirectoryName(cachePath));
                File.WriteAllLines(cachePath, lines);
            }

            var item = new TaskItem(cachePath);
            item.SetMetadata("ArchitectureChurnTable", "true");
            return new ITaskItem[] { item };
        }

        private string ChurnCachePath(int windowDays)
        {
            // Stable hash of the repository root: string.GetHashCode is not stable across processes.
            var hash = 17;
            foreach (var character in _repoRoot)
            {
                unchecked
                {
                    hash = (hash * 31) + character;
                }
            }

            var name = "churn-" + (hash & 0x7fffffff).ToString() + "-" + windowDays.ToString() + ".txt";
            return Path.Combine(Path.GetTempPath(), "architecture-analyzer", name);
        }

        private static bool IsFresh(string path)
            => File.Exists(path)
               && (DateTime.UtcNow - File.GetLastWriteTimeUtc(path)).TotalMinutes < ChurnCacheMinutes;

        private ITaskItem[] WriteChangedFiles()
        {
            if (string.IsNullOrEmpty(BaselineDirectory) || _changedPaths.Count == 0)
            {
                return new ITaskItem[0];
            }

            Directory.CreateDirectory(BaselineDirectory);
            var target = Path.Combine(BaselineDirectory, "changed-files.txt");
            File.WriteAllLines(target, _changedPaths);

            var item = new TaskItem(target);
            item.SetMetadata("ArchitectureChangedFiles", "true");
            return new ITaskItem[] { item };
        }

        // Points the analyzer at the CURRENT content of each changed test file (working-copy path, no
        // copy). The production compilation does not include test sources, so this is the only way the
        // related-test gate can see the tests this change touched. Test-ness reuses the same path
        // classification as ARCH021, so there is a single definition of "a test file".
        private ITaskItem[] WriteChangedTestFiles(List<string> testPatterns)
        {
            var items = new List<ITaskItem>();
            foreach (var full in _changedPaths)
            {
                if (!File.Exists(full) || !IsTestFile(full, testPatterns))
                {
                    continue; // deleted files, and production files, are not test evidence.
                }

                var item = new TaskItem(full);
                item.SetMetadata("ArchitectureChangedTestFile", "true");
                items.Add(item);
            }

            return items.ToArray();
        }

        // Removed-line windows from the last N commits, written once as "hash|sha|subject|ago".
        private ITaskItem[] WriteOscillationRemoved(int windowCommits)
        {
            var empty = new ITaskItem[0];
            if (string.IsNullOrEmpty(BaselineDirectory))
            {
                return empty;
            }

            // Separators (0x1e between commits, 0x1f between sha and subject) instead of spaces, so the
            // format survives ProcessStartInfo splitting the argument string on spaces.
            var output = RunGit("log --no-merges --unified=0 -n " + windowCommits.ToString()
                + " --format=format:%x1e%H%x1f%s -p -- *.cs");
            if (output == null)
            {
                return empty;
            }

            var windows = OscillationScan.ParseRemovedWindows(output);
            if (windows.Count == 0)
            {
                return empty;
            }

            var lines = new List<string>();
            foreach (var window in windows)
            {
                lines.Add(window.Hash + "|" + window.Sha + "|" + Sanitize(window.Subject) + "|" + window.Ago.ToString());
            }

            Directory.CreateDirectory(BaselineDirectory);
            var target = Path.Combine(BaselineDirectory, "oscillation-removed.txt");
            File.WriteAllLines(target, lines);

            var item = new TaskItem(target);
            item.SetMetadata("ArchitectureOscillationRemoved", "true");
            return new ITaskItem[] { item };
        }

        // Windows this change adds (working tree vs HEAD, plus untracked files), as "hash|absPath|line".
        private ITaskItem[] WriteOscillationAdded()
        {
            var empty = new ITaskItem[0];
            if (string.IsNullOrEmpty(BaselineDirectory))
            {
                return empty;
            }

            var diff = RunGit("diff HEAD --unified=0 -- *.cs");
            var windows = diff == null ? new List<AddedWindow>() : OscillationScan.ParseAddedWindows(diff);

            var untracked = RunGit("ls-files --others --exclude-standard -- *.cs");
            if (untracked != null)
            {
                foreach (var relative in SplitLines(untracked))
                {
                    var full = ToFullPath(_repoRoot, relative);
                    if (!File.Exists(full))
                    {
                        continue;
                    }

                    windows.AddRange(OscillationScan.AddedWindowsFromContent(relative, File.ReadAllLines(full)));
                }
            }

            if (windows.Count == 0)
            {
                return empty;
            }

            var lines = new List<string>();
            foreach (var window in windows)
            {
                lines.Add(window.Hash + "|" + ToFullPath(_repoRoot, window.Path) + "|" + window.StartLine.ToString());
            }

            Directory.CreateDirectory(BaselineDirectory);
            var target = Path.Combine(BaselineDirectory, "oscillation-added.txt");
            File.WriteAllLines(target, lines);

            var item = new TaskItem(target);
            item.SetMetadata("ArchitectureOscillationAdded", "true");
            return new ITaskItem[] { item };
        }

        private static string Sanitize(string value)
            => value == null ? string.Empty : value.Replace('|', ' ').Replace('\r', ' ').Replace('\n', ' ');

        private const string ArchitectureDirName = ".architecture";
        private const string PromptFileName = "current-prompt.txt";
        private const string HistoryFileName = "impact-history.csv";
        private const string HistoryHeader = "timestamp_utc,prompt,files_changed,added_production_lines,added_test_lines,test_ratio_percent,project_budget_percent,solution_budget_percent";
        private const string ImpactHistoryOption = "architecture_analyzer.impact_history_enabled";

        // The prompt that triggered this change: the file an AI tool's hook writes at the repository
        // root (Claude Code's UserPromptSubmit hook, for example), or the ARCHITECTURE_ANALYZER_PROMPT
        // environment variable as a tool-agnostic fallback. Empty on a manual build with neither — the
        // row is still written, just with no prompt attributed.
        private string ReadCurrentPrompt()
        {
            if (!string.IsNullOrEmpty(_repoRoot))
            {
                var promptFile = Path.Combine(Path.Combine(_repoRoot, ArchitectureDirName), PromptFileName);
                if (File.Exists(promptFile))
                {
                    try
                    {
                        return File.ReadAllText(promptFile).Trim();
                    }
                    catch
                    {
                        // unreadable prompt file: fall through to the environment variable.
                    }
                }
            }

            var fromEnv = Environment.GetEnvironmentVariable("ARCHITECTURE_ANALYZER_PROMPT");
            return fromEnv == null ? string.Empty : fromEnv.Trim();
        }

        // Appends one row per meaningful change to .architecture/impact-history.csv at the repository
        // root, each row carrying the prompt that caused the change. Opt-in through
        // architecture_analyzer.impact_history_enabled, because it writes into the consuming repository.
        // A row is written only when the prompt or the set of changed files (with their line counts)
        // differs from the last row, so the many builds a single prompt triggers collapse to one line.
        // Never throws: an audit trail must never break a build.
        private void WriteImpactHistory()
        {
            if (!string.IsNullOrEmpty(IsDesignTimeBuild)
                && IsDesignTimeBuild.Trim().Equals("true", StringComparison.OrdinalIgnoreCase))
            {
                return; // IDE design-time builds run constantly.
            }

            if (string.IsNullOrEmpty(_repoRoot) || _changedPaths.Count == 0)
            {
                return; // nothing changed since HEAD, nothing to record.
            }

            var enabled = ReadStringOption(ImpactHistoryOption);
            if (string.IsNullOrEmpty(enabled) || !enabled.Trim().Equals("true", StringComparison.OrdinalIgnoreCase))
            {
                return; // feature is off by default; writing into the repo must be a deliberate choice.
            }

            try
            {
                var directory = Path.Combine(_repoRoot, ArchitectureDirName);
                Directory.CreateDirectory(directory);
                var csvPath = Path.Combine(directory, HistoryFileName);
                var keyPath = csvPath + ".key";
                var lockPath = csvPath + ".lock";

                var prompt = ReadCurrentPrompt();
                var key = ComputeChangeKey(prompt);

                int projectPercent, globalPercent;
                ReadBudgetPercents(out projectPercent, out globalPercent);
                var ratio = AddedProductionLines > 0 ? AddedTestLines * 100 / AddedProductionLines : 0;

                // One solution-wide column set, so every project of a solution build produces the same
                // row and the deduplication below collapses them to a single line.
                var row = string.Join(",", new[]
                {
                    CsvField(DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)),
                    CsvField(prompt),
                    CsvField(_changedPaths.Count.ToString(CultureInfo.InvariantCulture)),
                    CsvField(AddedProductionLines.ToString(CultureInfo.InvariantCulture)),
                    CsvField(AddedTestLines.ToString(CultureInfo.InvariantCulture)),
                    CsvField(ratio.ToString(CultureInfo.InvariantCulture)),
                    CsvField(projectPercent.ToString(CultureInfo.InvariantCulture)),
                    CsvField(globalPercent.ToString(CultureInfo.InvariantCulture)),
                });

                // Serialize read-key/append across the parallel MSBuild nodes of one solution build.
                using (var gate = AcquireLock(lockPath))
                {
                    if (gate == null)
                    {
                        return; // another node holds the lock and will write the row.
                    }

                    var lastKeyText = File.Exists(keyPath) ? SafeReadAllText(keyPath) : null;
                    var lastKey = lastKeyText == null ? null : lastKeyText.Trim();
                    if (string.Equals(lastKey, key, StringComparison.Ordinal))
                    {
                        return; // same prompt and same change as the last recorded row.
                    }

                    var writeHeader = !File.Exists(csvPath) || new FileInfo(csvPath).Length == 0;
                    using (var writer = new StreamWriter(csvPath, true))
                    {
                        if (writeHeader)
                        {
                            writer.WriteLine(HistoryHeader);
                        }

                        writer.WriteLine(row);
                    }

                    File.WriteAllText(keyPath, key);
                    LogImpactHistory(csvPath, prompt);
                }
            }
            catch
            {
                // History is a convenience; never fail a build over it.
            }
        }

        private void LogImpactHistory(string csvPath, string prompt)
        {
            var french = !CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("en", StringComparison.OrdinalIgnoreCase);
            var ratio = AddedProductionLines > 0 ? AddedTestLines * 100 / AddedProductionLines : 0;
            var hasPrompt = !string.IsNullOrEmpty(prompt);

            Log.LogMessage(MessageImportance.High, french
                ? string.Format("[Architecture.Analyzer] impact de ce changement : {0} fichier(s), {1} ligne(s) prod / {2} test (ratio {3} %)", _changedPaths.Count, AddedProductionLines, AddedTestLines, ratio)
                : string.Format("[Architecture.Analyzer] impact of this change: {0} file(s), {1} prod line(s) / {2} test (ratio {3}%)", _changedPaths.Count, AddedProductionLines, AddedTestLines, ratio));

            Log.LogMessage(MessageImportance.High, french
                ? string.Format("[Architecture.Analyzer] historique : ligne ajoutée ({0}prompt) -> {1}", hasPrompt ? string.Empty : "sans ", csvPath)
                : string.Format("[Architecture.Analyzer] history: row appended ({0}prompt) -> {1}", hasPrompt ? string.Empty : "no ", csvPath));
        }

        // FNV-1a over the prompt, the sorted changed-file paths and the added-line counts. A build
        // whose prompt is unchanged and whose diff is unchanged reproduces the same key, so no new row
        // is written; the smallest edit to any of them yields a new key and therefore a new row.
        private string ComputeChangeKey(string prompt)
        {
            var paths = new List<string>(_changedPaths);
            paths.Sort(StringComparer.OrdinalIgnoreCase);

            ulong hash = 14695981039346656037UL;
            hash = FoldString(hash, prompt);
            foreach (var path in paths)
            {
                hash = FoldString(hash, path);
            }

            hash = FoldInt(hash, AddedProductionLines);
            hash = FoldInt(hash, AddedTestLines);
            return hash.ToString("x", CultureInfo.InvariantCulture);
        }

        private static ulong FoldString(ulong hash, string value)
        {
            if (value != null)
            {
                foreach (var c in value)
                {
                    unchecked
                    {
                        hash ^= c;
                        hash *= 1099511628211UL;
                    }
                }
            }

            unchecked
            {
                hash ^= (byte)'|';
                hash *= 1099511628211UL;
            }

            return hash;
        }

        private static ulong FoldInt(ulong hash, int value)
        {
            unchecked
            {
                hash ^= (ulong)value;
                hash *= 1099511628211UL;
            }

            return hash;
        }

        // Minimal RFC 4180 escaping: a field is quoted only when it contains a comma, a quote or a
        // line break, and embedded quotes are doubled. The prompt is the one field that needs it.
        private static string CsvField(string value)
        {
            if (value == null)
            {
                value = string.Empty;
            }

            var mustQuote = value.IndexOf(',') >= 0
                || value.IndexOf(Quote) >= 0
                || value.IndexOf('\n') >= 0
                || value.IndexOf('\r') >= 0;

            if (!mustQuote)
            {
                return value;
            }

            return Quote + value.Replace("\"", "\"\"") + Quote;
        }

        private static string SafeReadAllText(string path)
        {
            try
            {
                return File.ReadAllText(path);
            }
            catch
            {
                return null;
            }
        }

        private static FileStream AcquireLock(string lockPath)
        {
            for (var attempt = 0; attempt < 20; attempt++)
            {
                try
                {
                    return new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                }
                catch (IOException)
                {
                    System.Threading.Thread.Sleep(25);
                }
            }

            return null;
        }

        private const int MaxBaselineFiles = 200;
        private const char Quote = '"';

        // Writes the HEAD content of each changed .cs file so the analyzer can measure what this
        // change made worse. Files added by this change have no HEAD version and are skipped.
        private ITaskItem[] WriteHeadBaseline()
        {
            var items = new List<ITaskItem>();
            if (string.IsNullOrEmpty(BaselineDirectory))
            {
                return items.ToArray();
            }

            var changed = RunGit("diff --name-only HEAD -- *.cs");
            if (changed == null)
            {
                return items.ToArray();
            }

            Directory.CreateDirectory(BaselineDirectory);

            foreach (var relative in SplitLines(changed))
            {
                if (items.Count >= MaxBaselineFiles)
                {
                    break;
                }

                var content = RunGit("show HEAD:" + Quote + relative + Quote);
                if (content == null)
                {
                    continue;
                }

                // Path.DirectorySeparatorChar rather than a literal separator: no escaping to get wrong.
                var flattened = relative.Replace('/', '_').Replace(Path.DirectorySeparatorChar, '_');
                var target = Path.Combine(BaselineDirectory, flattened);
                File.WriteAllText(target, content);

                var item = new TaskItem(target);
                item.SetMetadata("ArchitectureBaselineFor", relative);
                items.Add(item);
            }

            return items.ToArray();
        }

        private static string ToFullPath(string root, string relative)
            => Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));

        private static int CountLines(string fullPath)
        {
            if (!File.Exists(fullPath))
            {
                return 0;
            }

            var count = 0;
            foreach (var _ in File.ReadLines(fullPath))
            {
                count++;
            }

            return count;
        }

        private static IEnumerable<string> SplitLines(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                yield break;
            }

            foreach (var line in value.Replace("\r", string.Empty).Split('\n'))
            {
                if (line.Length != 0)
                {
                    yield return line;
                }
            }
        }

        private string RunGit(string arguments)
        {
            // safe.directory=* skips git's ownership check (repos owned by another user on CI/shared boxes);
            // -C <root> pins every command to the repository root once it is known.
            var prefix = "-c safe.directory=* ";
            if (!string.IsNullOrEmpty(_repoRoot))
            {
                prefix += "-C \"" + _repoRoot + "\" ";
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = prefix + arguments,
                WorkingDirectory = WorkingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            try
            {
                using (var process = Process.Start(startInfo))
                {
                    if (process == null)
                    {
                        return null;
                    }

                    var output = process.StandardOutput.ReadToEnd();
                    process.StandardError.ReadToEnd();
                    process.WaitForExit();
                    return process.ExitCode == 0 ? output : null;
                }
            }
            catch
            {
                return null; // git is not installed or not on PATH.
            }
        }

        // Prints a per-build "gauge" so the LOC budget is visible continuously (not only when it is
        // exceeded). It reads the same loc_budget_percent_* keys from the project's .editorconfig
        // chain that the analyzer enforces, and mirrors the analyzer's budget formula. Enforcement
        // stays with ARCH008/ARCH009; this message is purely informational.
        private void LogBudgetGauge()
        {
            // The message is skipped during IDE design-time builds (they run constantly) — the
            // [Output] counts are still produced so live analyzer squiggles keep working.
            if (!string.IsNullOrEmpty(IsDesignTimeBuild)
                && IsDesignTimeBuild.Trim().Equals("true", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            int projectPercent, globalPercent;
            ReadBudgetPercents(out projectPercent, out globalPercent);

            // French by default (the analyzer's neutral catalog is French); English when the build's
            // UI culture is English - mirrors how the ARCHxxx diagnostics localize.
            var french = !CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("en", StringComparison.OrdinalIgnoreCase);

            if (projectPercent > 0)
            {
                LogGaugeLine(french, french ? "projet" : "project", ProjectAddedLines, ProjectBaselineLines, projectPercent, "ARCH009");
            }

            if (globalPercent > 0)
            {
                LogGaugeLine(french, "solution", SolutionAddedLines, SolutionBaselineLines, globalPercent, "ARCH008");
            }

            var minTestPercent = ReadIntOption("architecture_analyzer.min_test_lines_percent");
            if (minTestPercent > 0 && AddedProductionLines > 0)
            {
                var actual = AddedTestLines * 100 / AddedProductionLines;
                var status = actual < minTestPercent
                    ? (french ? "  >>> TESTS INSUFFISANTS (ARCH021)" : "  >>> NOT ENOUGH TESTS (ARCH021)")
                    : string.Empty;

                Log.LogMessage(MessageImportance.High, french
                    ? string.Format("[Architecture.Analyzer] ratio tests : {0} ligne(s) de test pour {1} ligne(s) de production ajoutees (ratio {2} %, minimum {3} %){4}", AddedTestLines, AddedProductionLines, actual, minTestPercent, status)
                    : string.Format("[Architecture.Analyzer] test ratio: {0} test line(s) for {1} production line(s) added (ratio {2}%, minimum {3}%){4}", AddedTestLines, AddedProductionLines, actual, minTestPercent, status));
            }
        }

        private void LogGaugeLine(bool french, string scope, int added, int baseline, int percent, string ruleId)
        {
            var budget = ComputeBudget(baseline, percent);
            if (budget <= 0)
            {
                Log.LogMessage(MessageImportance.High, french
                    ? string.Format("[Architecture.Analyzer] budget LOC ({0}) : {1} ligne(s) ajoutée(s) depuis HEAD - budget inactif (référence {2} ligne(s), {3}% configuré)", scope, added, baseline, percent)
                    : string.Format("[Architecture.Analyzer] LOC budget ({0}): {1} line(s) added since HEAD - budget inactive (baseline {2}, {3}% configured)", scope, added, baseline, percent));
                return;
            }

            var usage = (int)Math.Round(added * 100.0 / budget);
            var status = added > budget
                ? (french ? "  >>> BUDGET DÉPASSÉ (" + ruleId + ")" : "  >>> OVER BUDGET (" + ruleId + ")")
                : string.Empty;

            Log.LogMessage(MessageImportance.High, french
                ? string.Format("[Architecture.Analyzer] budget LOC ({0}) : {1}/{2} ligne(s) ajoutée(s) depuis HEAD ({3}% du budget de {4}% sur {5} ligne(s) de référence){6}", scope, added, budget, usage, percent, baseline, status)
                : string.Format("[Architecture.Analyzer] LOC budget ({0}): {1}/{2} line(s) added since HEAD ({3}% of the {4}% budget on {5} baseline line(s)){6}", scope, added, budget, usage, percent, baseline, status));
        }

        // Mirror of the analyzer's CalculateBudget: ceil(baseline * percent / 100), at least 1;
        // 0 (inactive) when there is no baseline or no percentage.
        private static int ComputeBudget(int baseline, int percent)
        {
            if (baseline <= 0 || percent <= 0)
            {
                return 0;
            }

            var budget = (int)Math.Ceiling(baseline * percent / 100.0);
            return budget < 1 ? 1 : budget;
        }

        private void ReadBudgetPercents(out int projectPercent, out int globalPercent)
        {
            projectPercent = 0;
            globalPercent = 0;

            foreach (var file in CollectEditorConfigs())
            {
                string section = null; // null = preamble (applies to every file)
                foreach (var raw in File.ReadAllLines(file))
                {
                    var line = raw.Trim();
                    if (line.Length == 0 || line[0] == '#' || line[0] == ';')
                    {
                        continue;
                    }

                    if (line[0] == '[' && line[line.Length - 1] == ']')
                    {
                        section = line.Substring(1, line.Length - 2).Trim();
                        continue;
                    }

                    if (!SectionAppliesToCSharp(section))
                    {
                        continue;
                    }

                    var eq = line.IndexOf('=');
                    if (eq <= 0)
                    {
                        continue;
                    }

                    var key = line.Substring(0, eq).Trim();
                    int parsed;
                    if (!int.TryParse(line.Substring(eq + 1).Trim(), out parsed) || parsed <= 0)
                    {
                        continue;
                    }

                    if (key.Equals("architecture_analyzer.loc_budget_percent_project", StringComparison.OrdinalIgnoreCase))
                    {
                        projectPercent = parsed;
                    }
                    else if (key.Equals("architecture_analyzer.loc_budget_percent_global", StringComparison.OrdinalIgnoreCase))
                    {
                        globalPercent = parsed;
                    }
                }
            }
        }

        // .editorconfig files from the repo root down to the project directory (nearer overrides
        // farther), stopping the upward walk at a file that declares "root = true".
        private List<string> CollectEditorConfigs()
        {
            var found = new List<string>();
            var dir = new DirectoryInfo(WorkingDirectory);
            while (dir != null)
            {
                var path = Path.Combine(dir.FullName, ".editorconfig");
                if (File.Exists(path))
                {
                    found.Add(path);
                    if (DeclaresRoot(path))
                    {
                        break;
                    }
                }

                if (_repoRoot != null
                    && string.Equals(dir.FullName.TrimEnd('\\', '/'), _repoRoot.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                dir = dir.Parent;
            }

            found.Reverse(); // farthest (root-most) first, so nearer files override
            return found;
        }

        private static bool DeclaresRoot(string file)
        {
            foreach (var raw in File.ReadAllLines(file))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line[0] == '#' || line[0] == ';')
                {
                    continue;
                }

                if (line[0] == '[')
                {
                    return false; // "root" must appear before any section
                }

                var eq = line.IndexOf('=');
                if (eq > 0
                    && line.Substring(0, eq).Trim().Equals("root", StringComparison.OrdinalIgnoreCase)
                    && line.Substring(eq + 1).Trim().Equals("true", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        // Accepts the sections under which these keys are realistically placed: the preamble, [*],
        // [*.cs], and brace sets that include cs (e.g. [*.{cs,vb}]). Anything else is ignored so a
        // key scoped to, say, [*.csproj] is not mistaken for a C# rule.
        // Splits an added-lines figure between production and tests, according to where the file lives.
        private void Accumulate(string relativePath, int added, List<string> testPatterns)
        {
            if (IsTestFile(relativePath, testPatterns))
            {
                AddedTestLines += added;
            }
            else
            {
                AddedProductionLines += added;
            }
        }

        private static bool IsTestFile(string relativePath, List<string> testPatterns)
        {
            // Leading slash so a pattern such as "/tests/" also matches a path starting with "tests/".
            var normalized = "/" + relativePath.Replace('\\', '/').ToLowerInvariant();
            foreach (var pattern in testPatterns)
            {
                if (normalized.IndexOf(pattern, StringComparison.Ordinal) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private List<string> ReadTestPatterns()
        {
            var configured = ReadStringOption("architecture_analyzer.test_file_patterns");
            var raw = string.IsNullOrEmpty(configured) ? "/tests/;.tests/;tests.cs;test.cs;spec.cs" : configured;

            var patterns = new List<string>();
            foreach (var entry in raw.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = entry.Trim().Replace('\\', '/').ToLowerInvariant();
                if (trimmed.Length != 0)
                {
                    patterns.Add(trimmed);
                }
            }

            return patterns;
        }

        private int ReadIntOption(string key)
        {
            var value = ReadStringOption(key);
            int parsed;
            return !string.IsNullOrEmpty(value) && int.TryParse(value, out parsed) && parsed > 0 ? parsed : 0;
        }

        private string ReadStringOption(string key)
        {
            string result = null;
            foreach (var file in CollectEditorConfigs())
            {
                string section = null;
                foreach (var raw in File.ReadAllLines(file))
                {
                    var line = raw.Trim();
                    if (line.Length == 0 || line[0] == '#' || line[0] == ';')
                    {
                        continue;
                    }

                    if (line[0] == '[' && line[line.Length - 1] == ']')
                    {
                        section = line.Substring(1, line.Length - 2).Trim();
                        continue;
                    }

                    if (!SectionAppliesToCSharp(section))
                    {
                        continue;
                    }

                    var eq = line.IndexOf('=');
                    if (eq > 0 && line.Substring(0, eq).Trim().Equals(key, StringComparison.OrdinalIgnoreCase))
                    {
                        result = line.Substring(eq + 1).Trim(); // nearer files override farther ones
                    }
                }
            }

            return result;
        }

        private static bool SectionAppliesToCSharp(string section)
        {
            if (section == null || section == "*" || section == "*.cs")
            {
                return true;
            }

            return section.IndexOf('{') >= 0 && section.IndexOf("cs", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
