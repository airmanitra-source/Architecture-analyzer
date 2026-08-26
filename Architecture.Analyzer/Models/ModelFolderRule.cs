using System.Collections.Generic;

namespace Architecture.Analyzer.Models;

internal sealed class ModelFolderSuffixRule
{
    public ModelFolderSuffixRule(string folder, IReadOnlyList<string> allowedSuffixes)
    {
        Folder = folder;
        AllowedSuffixes = allowedSuffixes;
    }

    public string Folder { get; }

    public IReadOnlyList<string> AllowedSuffixes { get; }
}