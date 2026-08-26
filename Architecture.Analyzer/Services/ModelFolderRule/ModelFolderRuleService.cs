using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Architecture.Analyzer.Models;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Architecture.Analyzer.Services.ModelFolderRule;

internal sealed class ModelFolderRuleService : IModelFolderRuleService
{
    private const string AnalyzerConfigPrefix = "architecture_analyzer.";
    private const string ModelFolderAllowedSuffixesOption = AnalyzerConfigPrefix + "model_folder_allowed_suffixes";
    private const char RuleSeparator = ';';
    private const string RuleValueSeparator = "=";

    public List<ModelFolderSuffixRule> ReadFolderRules(AnalyzerConfigOptions options)
    {
        if (!options.TryGetValue(ModelFolderAllowedSuffixesOption, out var configuredRules)
            || string.IsNullOrWhiteSpace(configuredRules))
        {
            return [];
        }

        var rules = new List<ModelFolderSuffixRule>();
        foreach (var item in configuredRules.Split(new[] { RuleSeparator }, StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = item.Split(new[] { RuleValueSeparator }, 2, StringSplitOptions.None);
            if (parts.Length != 2)
            {
                continue;
            }

            var folder = NormalizeFolder(parts[0]);
            var allowedSuffixes = parts[1]
                .Split(new[] { ',', '|' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(suffix => suffix.Trim())
                .Where(suffix => !string.IsNullOrWhiteSpace(suffix))
                .ToList();

            if (string.IsNullOrWhiteSpace(folder) || allowedSuffixes.Count == 0)
            {
                continue;
            }

            rules.Add(new ModelFolderSuffixRule(folder, allowedSuffixes));
        }

        return rules;
    }

    private static string NormalizeFolder(string folder)
        => folder.Trim().Replace('>', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar).Trim(Path.DirectorySeparatorChar);
}