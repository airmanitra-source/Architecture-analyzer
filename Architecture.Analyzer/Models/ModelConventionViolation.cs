namespace Architecture.Analyzer.Models;

internal sealed class ModelConventionViolation
{
    public ModelConventionViolation(string suffix, string folder)
    {
        Suffix = suffix;
        Folder = folder;
    }

    public string Suffix { get; }

    public string Folder { get; }
}
