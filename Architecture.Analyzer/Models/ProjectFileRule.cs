using System.Collections.Generic;

namespace Architecture.Analyzer.Models;

// A per-project allow-list: inside a project whose assembly name matches Project, only files whose
// name matches one of AllowedFiles are permitted; every other file is reported (ARCH003).
internal sealed class ProjectFileRule
{
    public ProjectFileRule(NamePattern project, IReadOnlyList<NamePattern> allowedFiles, string rawAllowed)
    {
        Project = project;
        AllowedFiles = allowedFiles;
        RawAllowed = rawAllowed;
    }

    public NamePattern Project { get; }

    public IReadOnlyList<NamePattern> AllowedFiles { get; }

    // The configured pattern list, kept verbatim for the diagnostic message.
    public string RawAllowed { get; }
}
