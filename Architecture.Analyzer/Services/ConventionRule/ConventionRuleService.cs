using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using Architecture.Analyzer.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Architecture.Analyzer.Services.ConventionRule;

internal sealed class ConventionRuleService : IConventionRuleService
{
    private const string AnalyzerConfigPrefix = "architecture_analyzer.";
    private const string MinSupportOption = AnalyzerConfigPrefix + "convention_min_support";
    private const string MinRatioOption = AnalyzerConfigPrefix + "convention_min_ratio";
    private const string IgnoreOption = AnalyzerConfigPrefix + "convention_ignore";

    // The ARCH001 / ARCH003 keys: whatever they govern explicitly is left to them.
    private const string ModelSuffixOption = AnalyzerConfigPrefix + "model_suffix";
    private const string ModelConventionsOption = AnalyzerConfigPrefix + "model_conventions";
    private const string FolderAllowedSuffixesOption = AnalyzerConfigPrefix + "model_folder_allowed_suffixes";

    private const string ChangedFilesMetadata = "build_metadata.AdditionalFiles.ArchitectureChangedFiles";

    private const int DefaultMinSupport = 5;
    private const int DefaultMinRatio = 90;

    // The traits a convention can be about. Their order is the order of the report.
    public const string FolderTrait = "folder";
    public const string NamespaceTrait = "namespace";
    public const string KindTrait = "kind";
    public const string SealedTrait = "sealed";
    public const string StaticTrait = "static";
    public const string AccessibilityTrait = "accessibility";
    public const string BaseTrait = "base";

    private static readonly string[] Traits =
    {
        FolderTrait, NamespaceTrait, KindTrait, SealedTrait, StaticTrait, AccessibilityTrait, BaseTrait
    };

    public ConventionSettings GetSettings(AnalyzerConfigOptions options)
    {
        var explicitSuffixes = new HashSet<string>(StringComparer.Ordinal);

        if (options.TryGetValue(ModelSuffixOption, out var single) && !string.IsNullOrWhiteSpace(single))
        {
            explicitSuffixes.Add(single.Trim());
        }

        // "BusinessModel=Models/Business;DataModel=Models/Data" -> the suffixes are the left sides.
        if (options.TryGetValue(ModelConventionsOption, out var conventions) && !string.IsNullOrWhiteSpace(conventions))
        {
            foreach (var entry in conventions.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var equals = entry.IndexOf('=');
                if (equals > 0)
                {
                    explicitSuffixes.Add(entry.Substring(0, equals).Trim());
                }
            }
        }

        // "Models/Business=BusinessModel;Models/Data=DataModel,DataProvider" -> the right sides.
        if (options.TryGetValue(FolderAllowedSuffixesOption, out var allowed) && !string.IsNullOrWhiteSpace(allowed))
        {
            foreach (var entry in allowed.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var equals = entry.IndexOf('=');
                if (equals < 0)
                {
                    continue;
                }

                foreach (var suffix in entry.Substring(equals + 1).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    explicitSuffixes.Add(suffix.Trim());
                }
            }
        }

        var ignored = new HashSet<string>(StringComparer.Ordinal);
        if (options.TryGetValue(IgnoreOption, out var ignoreValue) && !string.IsNullOrWhiteSpace(ignoreValue))
        {
            foreach (var entry in ignoreValue.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                ignored.Add(entry.Trim());
            }
        }

        return new ConventionSettings(
            GetIntOption(options, MinSupportOption, DefaultMinSupport, 2, int.MaxValue),
            GetIntOption(options, MinRatioOption, DefaultMinRatio, 51, 100),
            ignored,
            explicitSuffixes);
    }

    public HashSet<string> ReadChangedFiles(
        ImmutableArray<AdditionalText> additionalFiles,
        AnalyzerConfigOptionsProvider optionsProvider,
        CancellationToken cancellationToken)
    {
        var changed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in additionalFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!optionsProvider.GetOptions(file).TryGetValue(ChangedFilesMetadata, out var marker)
                || !string.Equals(marker, "true", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var text = file.GetText(cancellationToken);
            if (text is null)
            {
                continue;
            }

            foreach (var line in text.Lines)
            {
                var path = line.ToString().Trim();
                if (path.Length > 0)
                {
                    changed.Add(path);
                }
            }
        }

        return changed;
    }

    public TypeTraits? Collect(INamedTypeSymbol type)
    {
        if (type.ContainingType is not null || type.IsImplicitlyDeclared)
        {
            return null;
        }

        var location = type.Locations.FirstOrDefault(candidate => candidate.IsInSource);
        if (location is null)
        {
            return null;
        }

        var suffixes = GetSuffixes(type.Name);
        if (suffixes.Count == 0)
        {
            return null; // a single-word name has no suffix to group on.
        }

        var filePath = location.SourceTree?.FilePath ?? string.Empty;
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [FolderTrait] = Path.GetDirectoryName(filePath) ?? string.Empty,
            [NamespaceTrait] = type.ContainingNamespace?.IsGlobalNamespace == false ? type.ContainingNamespace.Name : string.Empty,
            [KindTrait] = type.IsRecord ? "record" : type.TypeKind.ToString().ToLowerInvariant(),
            [SealedTrait] = type.IsSealed && !type.IsStatic ? "true" : "false",
            [StaticTrait] = type.IsStatic ? "true" : "false",
            [AccessibilityTrait] = type.DeclaredAccessibility.ToString().ToLowerInvariant(),
            [BaseTrait] = type.BaseType is null || type.BaseType.SpecialType == SpecialType.System_Object
                ? string.Empty
                : type.BaseType.Name,
        };

        return new TypeTraits(type.Name, suffixes, filePath, values, location);
    }

    public List<Convention> Infer(List<TypeTraits> types, ConventionSettings settings)
    {
        var conventions = new List<Convention>();

        var bySuffix = new Dictionary<string, List<TypeTraits>>(StringComparer.Ordinal);
        foreach (var type in types)
        {
            foreach (var suffix in type.Suffixes)
            {
                if (!bySuffix.TryGetValue(suffix, out var group))
                {
                    group = new List<TypeTraits>();
                    bySuffix[suffix] = group;
                }

                group.Add(type);
            }
        }

        foreach (var suffix in bySuffix.Keys.OrderBy(key => key, StringComparer.Ordinal))
        {
            var group = bySuffix[suffix];
            if (group.Count < settings.MinSupport)
            {
                continue;
            }

            foreach (var trait in Traits)
            {
                if (settings.Ignored.Contains(suffix + ">" + trait))
                {
                    continue;
                }

                if (trait == FolderTrait && settings.ExplicitFolderSuffixes.Contains(suffix))
                {
                    continue; // ARCH001 / ARCH003 own this one.
                }

                var counts = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (var type in group)
                {
                    var value = type.Values[trait];
                    counts.TryGetValue(value, out var seen);
                    counts[value] = seen + 1;
                }

                // Deterministic tie-break on the value itself.
                var modal = counts.OrderByDescending(pair => pair.Value).ThenBy(pair => pair.Key, StringComparer.Ordinal).First();
                if (modal.Value < settings.MinSupport || modal.Value * 100 < group.Count * settings.MinRatio)
                {
                    continue;
                }

                if (!IsMeaningful(trait, modal.Key))
                {
                    continue;
                }

                conventions.Add(new Convention(suffix, trait, modal.Key, modal.Value, group.Count));
            }
        }

        return conventions;
    }

    public List<ConventionViolation> Check(
        List<TypeTraits> types,
        List<Convention> conventions,
        HashSet<string> changedFiles)
    {
        var violations = new List<ConventionViolation>();
        if (conventions.Count == 0 || changedFiles.Count == 0)
        {
            return violations;
        }

        foreach (var type in types.OrderBy(candidate => candidate.Name, StringComparer.Ordinal))
        {
            if (!changedFiles.Contains(type.FilePath))
            {
                continue;
            }

            // When both "Model" and "BusinessModel" carry a convention on the same trait, the more
            // specific suffix wins. Suffixes are ordered most specific first.
            var decided = new HashSet<string>(StringComparer.Ordinal);
            foreach (var suffix in type.Suffixes)
            {
                foreach (var convention in conventions)
                {
                    if (convention.Suffix != suffix || !decided.Add(convention.Trait))
                    {
                        continue;
                    }

                    var actual = type.Values[convention.Trait];
                    if (!string.Equals(actual, convention.Expected, StringComparison.Ordinal))
                    {
                        violations.Add(new ConventionViolation(type.Name, convention, actual, type.Location));
                    }
                }
            }
        }

        return violations;
    }

    // "CustomerBusinessModel" -> ["BusinessModel", "Model"]: the last two words, then the last one.
    private static List<string> GetSuffixes(string name)
    {
        var words = new List<int>();
        for (var index = 0; index < name.Length; index++)
        {
            if (!char.IsUpper(name[index]))
            {
                continue;
            }

            // A capital opens a word when the previous character is not a capital — or when the next
            // one is lower case, which is how an acronym ends: "HTTPClient" is HTTP + Client, and
            // "AModel" is A + Model rather than one word.
            var afterNonCapital = index == 0 || !char.IsUpper(name[index - 1]);
            var endsAnAcronym = index + 1 < name.Length && char.IsLower(name[index + 1]);
            if (afterNonCapital || endsAnAcronym)
            {
                words.Add(index);
            }
        }

        var suffixes = new List<string>();
        if (words.Count >= 3)
        {
            suffixes.Add(name.Substring(words[words.Count - 2]));
        }

        if (words.Count >= 2)
        {
            suffixes.Add(name.Substring(words[words.Count - 1]));
        }

        return suffixes;
    }

    // A majority of "not sealed", "not static" or "no base type" is just the default, not a
    // convention worth enforcing.
    private static bool IsMeaningful(string trait, string value)
    {
        switch (trait)
        {
            case SealedTrait:
            case StaticTrait:
                return value == "true";
            case BaseTrait:
            case FolderTrait:
            case NamespaceTrait:
                return value.Length > 0;
            default:
                return true;
        }
    }

    private static int GetIntOption(AnalyzerConfigOptions options, string key, int fallback, int min, int max)
    {
        if (!options.TryGetValue(key, out var configuredValue)
            || string.IsNullOrWhiteSpace(configuredValue)
            || !int.TryParse(configuredValue.Trim(), out var value)
            || value < min
            || value > max)
        {
            return fallback;
        }

        return value;
    }
}
