using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Architecture.Analyzer;

internal interface IModelConventionService
{
    ImmutableArray<ModelConvention> ReadConventions(AnalyzerConfigOptions options);
}
