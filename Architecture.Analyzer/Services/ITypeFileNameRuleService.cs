namespace Architecture.Analyzer;

internal interface ITypeFileNameRuleService
{
    bool DoesFileNameMatchTypeName(string filePath, string typeName);
}
