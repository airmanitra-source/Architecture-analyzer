using System.Collections.Generic;
using Architecture.Analyzer.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Architecture.Analyzer.Services.DuplicateTypeNameRule;

internal interface IDuplicateTypeNameRuleService
{
    IReadOnlyCollection<string> GetExceptions(AnalyzerConfigOptions options);

    // Public type names visible from referenced assemblies that belong to the same product family
    // as the compiled assembly, keyed by metadata name and mapped to the assembly declaring them.
    Dictionary<string, string> GetReferencedTypeNames(Compilation compilation, AnalyzerConfigOptions options);

    DuplicateTypeNameViolation? CheckType(
        INamedTypeSymbol type,
        Dictionary<string, string> referencedTypeNames,
        IReadOnlyCollection<string> exceptions);
}
