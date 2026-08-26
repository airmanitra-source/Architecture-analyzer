using System;
using System.Collections.Generic;
using System.IO;
using Architecture.Analyzer.Models;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Architecture.Analyzer.Services.ModelConventionRule;

internal sealed class ModelConventionRuleService : IModelConventionRuleService
{
    private const string AnalyzerConfigPrefix = "architecture_analyzer.";
    private const string ModelSuffixOption = AnalyzerConfigPrefix + "model_suffix";
    private const string ModelFolderOption = AnalyzerConfigPrefix + "model_folder";
    private const string ModelConventionsOption = AnalyzerConfigPrefix + "model_conventions";
    private const string DefaultModelSuffix = "BusinessModel";
    private const string DefaultModelFolder = "Models>Business";
    private const char ConventionSeparator = ';';
    private const string ConventionValueSeparator = "=";

    public List<ModelConventionViolation> ReadConventions(AnalyzerConfigOptions options)
    {
        if (options.TryGetValue(ModelConventionsOption, out var configuredConventions)
            && !string.IsNullOrWhiteSpace(configuredConventions))
        {
            var conventions = new List<ModelConventionViolation>();
            foreach (var item in configuredConventions.Split(new[] { ConventionSeparator }, StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = item.Split(new[] { ConventionValueSeparator }, 2, StringSplitOptions.None);
                if (parts.Length == 2 && !string.IsNullOrWhiteSpace(parts[0]) && !string.IsNullOrWhiteSpace(parts[1]))
                {
                    conventions.Add(new ModelConventionViolation(parts[0].Trim(), NormalizeFolder(parts[1])));
                }
            }

            if (conventions.Count > 0)
            {
                return conventions;
            }
        }

        var suffix = GetOption(options, ModelSuffixOption, DefaultModelSuffix);
        var folder = GetOption(options, ModelFolderOption, DefaultModelFolder);
        return [new ModelConventionViolation(suffix, NormalizeFolder(folder))];
    }

    private static string GetOption(AnalyzerConfigOptions options, string key, string defaultValue)
        => options.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : defaultValue;

    private static string NormalizeFolder(string folder)
        => folder.Trim().Replace('>', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar).Trim(Path.DirectorySeparatorChar);
}
