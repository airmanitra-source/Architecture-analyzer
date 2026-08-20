using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Architecture.Analyzer.Tests;

internal static class ArchitectureAnalyzerTestRunner
{
    public static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string source, DiagnosticAnalyzer analyzer, string fileName = "Test.cs")
    {
        var (_, diagnostics, _) = await AnalyzeWithDocumentAsync(source, analyzer, fileName);
        return diagnostics;
    }

    public static async Task<(Document document, ImmutableArray<Diagnostic> diagnostics, AdhocWorkspace workspace)> AnalyzeWithDocumentAsync(string source, DiagnosticAnalyzer analyzer, string fileName = "Test.cs")
    {
        var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var documentId = DocumentId.CreateNewId(projectId);

        var solution = workspace.CurrentSolution
            .AddProject(ProjectInfo.Create(projectId, VersionStamp.Default, "TestProject", "TestProject", LanguageNames.CSharp))
            .WithProjectCompilationOptions(projectId, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .AddMetadataReference(projectId, MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
            .AddMetadataReference(projectId, MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location))
            .AddMetadataReference(projectId, MetadataReference.CreateFromFile(typeof(Attribute).Assembly.Location))
            .AddDocument(documentId, fileName, SourceText.From(source));

        workspace.TryApplyChanges(solution);

        var document = workspace.CurrentSolution.GetDocument(documentId)!;
        var compilation = await document.Project.GetCompilationAsync();
        Assert.NotNull(compilation);

        var diagnostics = await compilation!
            .WithAnalyzers(ImmutableArray.Create(analyzer))
            .GetAnalyzerDiagnosticsAsync();

        return (document, diagnostics, workspace);
    }
}
