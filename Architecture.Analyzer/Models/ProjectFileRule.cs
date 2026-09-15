using System.Collections.Generic;

namespace Architecture.Analyzer.Models;

// A per-project allow-list: inside a project whose assembly name matches Project, only files matching
// one of AllowedFiles are permitted; every other file is reported (ARCH003). A file pattern without a
// path separator matches the file name anywhere; a pattern with a separator (e.g. Models/*BusinessModel.cs)
// matches the path relative to the project root.
internal sealed class ProjectFileRule
{
    public ProjectFileRule(NamePattern project, IReadOnlyList<string> allowedFiles, string rawAllowed)
    {
        Project = project;
        AllowedFiles = allowedFiles;
        RawAllowed = rawAllowed;
    }

    public NamePattern Project { get; }

    public IReadOnlyList<string> AllowedFiles { get; }

    // The configured pattern list, kept verbatim for the diagnostic message.
    public string RawAllowed { get; }
}
