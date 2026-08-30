namespace Architecture.Analyzer.Models;

internal sealed class ModuleReferenceBarrierRule
{
    public ModuleReferenceBarrierRule(string sourceModule, string forbiddenModule)
    {
        SourceModule = sourceModule;
        ForbiddenModule = forbiddenModule;
    }

    public string SourceModule { get; }

    public string ForbiddenModule { get; }
}
