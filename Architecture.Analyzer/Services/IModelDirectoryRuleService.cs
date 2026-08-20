namespace Architecture.Analyzer;

internal interface IModelDirectoryRuleService
{
    bool IsTypeInExpectedFolder(string filePath, string expectedFolder);
}
