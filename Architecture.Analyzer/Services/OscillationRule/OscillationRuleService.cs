using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Architecture.Analyzer.Services.OscillationRule;

internal sealed class OscillationRuleService : IOscillationRuleService
{
    private const string RemovedMetadata = "build_metadata.AdditionalFiles.ArchitectureOscillationRemoved";
    private const string AddedMetadata = "build_metadata.AdditionalFiles.ArchitectureOscillationAdded";

    public Dictionary<string, (string Sha, string Subject, int Ago)> ReadRemoved(
        ImmutableArray<AdditionalText> additionalFiles,
        AnalyzerConfigOptionsProvider optionsProvider,
        CancellationToken cancellationToken)
    {
        var removed = new Dictionary<string, (string, string, int)>(StringComparer.Ordinal);

        foreach (var file in additionalFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsMarked(optionsProvider, file, RemovedMetadata))
            {
                continue;
            }

            var text = file.GetText(cancellationToken);
            if (text is null)
            {
                continue;
            }

            // "hash|sha|subject|ago" — the subject was stripped of '|' when written.
            foreach (var line in text.Lines)
            {
                var value = line.ToString();
                var first = value.IndexOf('|');
                var last = value.LastIndexOf('|');
                if (first <= 0 || last <= first)
                {
                    continue;
                }

                var second = value.IndexOf('|', first + 1);
                if (second < 0 || second > last)
                {
                    continue;
                }

                var hash = value.Substring(0, first);
                var sha = value.Substring(first + 1, second - first - 1);
                var subject = value.Substring(second + 1, last - second - 1);
                if (!int.TryParse(value.Substring(last + 1), out var ago))
                {
                    ago = 0;
                }

                if (!removed.ContainsKey(hash))
                {
                    removed[hash] = (sha, subject, ago);
                }
            }
        }

        return removed;
    }

    public Dictionary<string, List<(int Line, string Hash)>> ReadAdded(
        ImmutableArray<AdditionalText> additionalFiles,
        AnalyzerConfigOptionsProvider optionsProvider,
        CancellationToken cancellationToken)
    {
        var added = new Dictionary<string, List<(int, string)>>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in additionalFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsMarked(optionsProvider, file, AddedMetadata))
            {
                continue;
            }

            var text = file.GetText(cancellationToken);
            if (text is null)
            {
                continue;
            }

            // "hash|absolute path|line" — the path may contain anything but '|', which a path never has.
            foreach (var line in text.Lines)
            {
                var value = line.ToString();
                var first = value.IndexOf('|');
                var last = value.LastIndexOf('|');
                if (first <= 0 || last <= first)
                {
                    continue;
                }

                var hash = value.Substring(0, first);
                var path = value.Substring(first + 1, last - first - 1);
                if (!int.TryParse(value.Substring(last + 1), out var lineNumber))
                {
                    continue;
                }

                if (!added.TryGetValue(path, out var list))
                {
                    list = new List<(int, string)>();
                    added[path] = list;
                }

                list.Add((lineNumber, hash));
            }
        }

        return added;
    }

    private static bool IsMarked(AnalyzerConfigOptionsProvider optionsProvider, AdditionalText file, string metadata)
        => optionsProvider.GetOptions(file).TryGetValue(metadata, out var marker)
           && string.Equals(marker, "true", StringComparison.OrdinalIgnoreCase);
}
