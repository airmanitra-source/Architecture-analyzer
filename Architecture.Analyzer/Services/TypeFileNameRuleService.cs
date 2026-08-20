using System;
using System.IO;

namespace Architecture.Analyzer;

internal sealed class TypeFileNameRuleService : ITypeFileNameRuleService
{
    public bool DoesFileNameMatchTypeName(string filePath, string typeName)
    {
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        return string.Equals(fileName, typeName, StringComparison.Ordinal);
    }
}
