namespace Architecture.Analyzer;

internal sealed class ModelConvention
{
    public ModelConvention(string suffix, string folder)
    {
        Suffix = suffix;
        Folder = folder;
    }

    public string Suffix { get; }

    public string Folder { get; }
}
