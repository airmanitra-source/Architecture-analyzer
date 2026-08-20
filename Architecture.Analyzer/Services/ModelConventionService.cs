using System;
using System.Collections.Immutable;
using System.IO;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Architecture.Analyzer;

internal sealed class ModelConventionService : IModelConventionService
{
    private const string AnalyzerConfigPrefix = "architecture_analyzer.";
    private const string ModelSuffixOption = AnalyzerConfigPrefix + "model_suffix";
    private const string ModelFolderOption = AnalyzerConfigPrefix + "model_folder";
    private const string ModelConventionsOption = AnalyzerConfigPrefix + "model_conventions";
    private const string DefaultModelSuffix = "BusinessModel";
    private const string DefaultModelFolder = "Business>Models";
    private const char ConventionSeparator = ';';
    private const string ConventionValueSeparator = "=";

    public ImmutableArray<ModelConvention> ReadConventions(AnalyzerConfigOptions options)
    {
        if (options.TryGetValue(ModelConventionsOption, out var configuredConventions)
            && !string.IsNullOrWhiteSpace(configuredConventions))
        {
            var conventions = ImmutableArray.CreateBuilder<ModelConvention>();
            foreach (var item in configuredConventions.Split(new[] { ConventionSeparator }, StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = item.Split(new[] { ConventionValueSeparator }, 2, StringSplitOptions.None);
                if (parts.Length == 2 && !string.IsNullOrWhiteSpace(parts[0]) && !string.IsNullOrWhiteSpace(parts[1]))
                {
                    conventions.Add(new ModelConvention(parts[0].Trim(), NormalizeFolder(parts[1])));
                }
            }

            if (conventions.Count > 0)
            {
                return conventions.ToImmutable();
            }
        }

        var suffix = GetOption(options, ModelSuffixOption, DefaultModelSuffix);
        var folder = GetOption(options, ModelFolderOption, DefaultModelFolder);
        return ImmutableArray.Create(new ModelConvention(suffix, NormalizeFolder(folder)));
    }

    private static string GetOption(AnalyzerConfigOptions options, string key, string defaultValue)
        => options.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : defaultValue;

    private static string NormalizeFolder(string folder)
        => folder.Trim().Replace('>', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar).Trim(Path.DirectorySeparatorChar);
}
