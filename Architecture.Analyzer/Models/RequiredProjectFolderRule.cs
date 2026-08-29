using System.Collections.Generic;

namespace Architecture.Analyzer.Models;

internal sealed class RequiredProjectFolderRule
{
    public RequiredProjectFolderRule(string projectName, IReadOnlyList<string> requiredFolders)
    {
        ProjectName = projectName;
        RequiredFolders = requiredFolders;
    }

    public string ProjectName { get; }

    public IReadOnlyList<string> RequiredFolders { get; }
}
