using Architecture.Analyzer.Services.ModelDirectoryRule;
using System;
using System.IO;

namespace Architecture.Analyzer;

internal sealed class ModelDirectoryRuleService : IModelDirectoryRuleService
{
    public bool IsTypeInExpectedFolder(string filePath, string expectedFolder)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (directory is null)
        {
            return false;
        }

        var actualParts = directory.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
        var expectedParts = expectedFolder.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);

        if (expectedParts.Length > actualParts.Length)
        {
            return false;
        }

        var offset = actualParts.Length - expectedParts.Length;
        for (var index = 0; index < expectedParts.Length; index++)
        {
            if (!string.Equals(actualParts[offset + index], expectedParts[index], StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }
}
