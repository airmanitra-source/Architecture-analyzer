namespace Architecture.Analyzer.Services.FileNameRule;

internal interface ITypeFileNameRuleService
{
    bool DoesFileNameMatchTypeName(string filePath, string typeName);
}
