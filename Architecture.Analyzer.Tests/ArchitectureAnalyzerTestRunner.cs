using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Architecture.Analyzer.Tests;

internal static class ArchitectureAnalyzerTestRunner
{
    public static async Task<List<Diagnostic>> AnalyzeAsync(
        IReadOnlyList<(string fileName, string source)> documents,
        DiagnosticAnalyzer analyzer,
        string projectName,
        IReadOnlyDictionary<string, string>? analyzerConfigOptions = null)
    {
        var (_, diagnostics, _) = await AnalyzeWithDocumentsAsync(documents, analyzer, projectName, analyzerConfigOptions);
        return diagnostics;
    }

    public static async Task<List<Diagnostic>> AnalyzeAsync(
        string source,
        DiagnosticAnalyzer analyzer,
        string fileName = "Test.cs",
        IReadOnlyDictionary<string, string>? analyzerConfigOptions = null)
    {
        var (_, diagnostics, _) = await AnalyzeWithDocumentsAsync(
            [(fileName, source)],
            analyzer,
            "TestProject",
            analyzerConfigOptions);
        return diagnostics;
    }

    public static async Task<(Document document, List<Diagnostic> diagnostics, AdhocWorkspace workspace)> AnalyzeWithDocumentAsync(
        string source,
        DiagnosticAnalyzer analyzer,
        string fileName = "Test.cs",
        IReadOnlyDictionary<string, string>? analyzerConfigOptions = null)
    {
        var result = await AnalyzeWithDocumentsAsync(
            [(fileName, source)],
            analyzer,
            "TestProject",
            analyzerConfigOptions);

        return result;
    }

    public static async Task<(Document document, List<Diagnostic> diagnostics, AdhocWorkspace workspace)> AnalyzeWithDocumentsAsync(
        IReadOnlyList<(string fileName, string source)> documents,
        DiagnosticAnalyzer analyzer,
        string projectName,
        IReadOnlyDictionary<string, string>? analyzerConfigOptions = null)
    {
        var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();

        var solution = workspace.CurrentSolution
            .AddProject(ProjectInfo.Create(projectId, VersionStamp.Default, projectName, projectName, LanguageNames.CSharp))
            .WithProjectCompilationOptions(projectId, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .AddMetadataReference(projectId, MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
            .AddMetadataReference(projectId, MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location))
            .AddMetadataReference(projectId, MetadataReference.CreateFromFile(typeof(Attribute).Assembly.Location));

        DocumentId? firstDocumentId = null;
        foreach (var (fileName, source) in documents)
        {
            var documentId = DocumentId.CreateNewId(projectId);
            firstDocumentId ??= documentId;
            solution = solution.AddDocument(documentId, fileName, SourceText.From(source));
        }

        workspace.TryApplyChanges(solution);

        var document = workspace.CurrentSolution.GetDocument(firstDocumentId!)!;
        var compilation = await document.Project.GetCompilationAsync();
        Assert.NotNull(compilation);

        var options = new AnalyzerOptions(
            ImmutableArray<AdditionalText>.Empty,
            new TestAnalyzerConfigOptionsProvider(analyzerConfigOptions));

        var diagnostics = await compilation!
            .WithAnalyzers(
                ImmutableArray.Create(analyzer),
                new CompilationWithAnalyzersOptions(options, null, true, false))
            .GetAnalyzerDiagnosticsAsync();

        return (document, diagnostics.ToList(), workspace);
    }

    private sealed class TestAnalyzerConfigOptionsProvider(IReadOnlyDictionary<string, string>? values) : AnalyzerConfigOptionsProvider
    {
        private readonly AnalyzerConfigOptions _options = new TestAnalyzerConfigOptions(values);

        public override AnalyzerConfigOptions GlobalOptions => _options;

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => _options;

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => _options;
    }

    private sealed class TestAnalyzerConfigOptions(IReadOnlyDictionary<string, string>? values) : AnalyzerConfigOptions
    {
        private readonly IReadOnlyDictionary<string, string> _values = values ?? new Dictionary<string, string>();

        public override bool TryGetValue(string key, out string value)
            => _values.TryGetValue(key, out value!);
    }
}
