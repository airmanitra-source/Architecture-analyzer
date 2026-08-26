namespace Architecture.Analyzer.Services.ModelDirectoryRule;

internal interface IModelDirectoryRuleService
{
    bool IsTypeInExpectedFolder(string filePath, string expectedFolder);
}
