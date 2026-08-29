using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Architecture.Analyzer.Services.RequiredProjectFolderRule;

internal sealed class RequiredProjectFolderRuleService : IRequiredProjectFolderRuleService
{
    private const string AnalyzerConfigPrefix = "architecture_analyzer.";
    private const string RequiredProjectFoldersOption = AnalyzerConfigPrefix + "required_project_folders";
    private static readonly char[] FolderSeparator = [',', '|'];
    private const char RuleSeparator = ';';
    private const string RuleValueSeparator = "=";

    public List<Models.RequiredProjectFolderRule> ReadRules(AnalyzerConfigOptions options)
    {
        if (!options.TryGetValue(RequiredProjectFoldersOption, out var configuredRules)
            || string.IsNullOrWhiteSpace(configuredRules))
        {
            return [];
        }

        var rules = new List<Models.RequiredProjectFolderRule>();
        foreach (var item in configuredRules.Split(new[] { RuleSeparator }, StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = item.Split(new[] { RuleValueSeparator }, 2, StringSplitOptions.None);
            if (parts.Length != 2)
            {
                continue;
            }

            var projectName = parts[0].Trim();
            var requiredFolders = parts[1]
                .Split(FolderSeparator, StringSplitOptions.RemoveEmptyEntries)
                .Select(NormalizeFolder)
                .Where(folder => !string.IsNullOrWhiteSpace(folder))
                .ToList();

            if (string.IsNullOrWhiteSpace(projectName) || requiredFolders.Count == 0)
            {
                continue;
            }

            rules.Add(new Models.RequiredProjectFolderRule(projectName, requiredFolders));
        }

        return rules;
    }

    private static string NormalizeFolder(string folder)
        => folder.Trim().Replace('>', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar).Trim(Path.DirectorySeparatorChar);
}
