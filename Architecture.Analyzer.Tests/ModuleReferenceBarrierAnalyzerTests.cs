using System.Collections.Immutable;
using Architecture.Analyzer;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Architecture.Analyzer.Tests;

public sealed class ModuleReferenceBarrierAnalyzerTests
{
    private static readonly Dictionary<string, string> BarrierConfig = new()
    {
        ["architecture_analyzer.module_reference_barrier"] = "Portal>Domain"
    };

    [Fact]
    public async Task DoesNotReportWhenNoBarrierConfigured()
    {
        var diagnostics = await ArchitectureAnalyzerTestRunner.AnalyzeAsync(
            [("Class1.cs", "public class Class1 { }")],
            new ModuleReferenceBarrierAnalyzer(),
            "MyApp.Portal");

        Assert.DoesNotContain(diagnostics, d => d.Id == ModuleReferenceBarrierAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task DoesNotReportWhenProjectIsNotTheSource()
    {
        var diagnostics = await ArchitectureAnalyzerTestRunner.AnalyzeAsync(
            [("Class1.cs", "public class Class1 { }")],
            new ModuleReferenceBarrierAnalyzer(),
            "MyApp.Infrastructure",
            BarrierConfig);

        Assert.DoesNotContain(diagnostics, d => d.Id == ModuleReferenceBarrierAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task ReportsDiagnosticWhenPortalReferencesDomainType()
    {
        var domainRef = BuildAssemblyReference(
            "MyApp.Domain",
            "namespace MyApp.Domain.Models { public class User { } }");

        var diagnostics = await AnalyzeWithExternalReference(
            "MyApp.Portal",
            [("Service.cs", @"
using MyApp.Domain.Models;
namespace MyApp.Portal
{
    public class Service
    {
        public void Do(User user) { }
    }
}
")],
            domainRef,
            BarrierConfig);

        Assert.Contains(diagnostics, d => d.Id == ModuleReferenceBarrierAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task AllowsReferenceToNonForbiddenModule()
    {
        var infraRef = BuildAssemblyReference(
            "MyApp.Infrastructure",
            "namespace MyApp.Infrastructure { public class DbContext { } }");

        var diagnostics = await AnalyzeWithExternalReference(
            "MyApp.Portal",
            [("Service.cs", @"
using MyApp.Infrastructure;
namespace MyApp.Portal
{
    public class Service
    {
        public void Do(DbContext ctx) { }
    }
}
")],
            infraRef,
            BarrierConfig);

        Assert.DoesNotContain(diagnostics, d => d.Id == ModuleReferenceBarrierAnalyzer.DiagnosticId);
    }

    private static MetadataReference BuildAssemblyReference(string assemblyName, string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create(
            assemblyName,
            [tree],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var ms = new System.IO.MemoryStream();
        compilation.Emit(ms);
        ms.Seek(0, System.IO.SeekOrigin.Begin);
        return MetadataReference.CreateFromStream(ms);
    }

    private static async Task<List<Diagnostic>> AnalyzeWithExternalReference(
        string projectName,
        IReadOnlyList<(string fileName, string source)> documents,
        MetadataReference externalRef,
        IReadOnlyDictionary<string, string> analyzerConfigOptions)
    {
        var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();

        var solution = workspace.CurrentSolution
            .AddProject(ProjectInfo.Create(projectId, VersionStamp.Default, projectName, projectName, LanguageNames.CSharp))
            .WithProjectCompilationOptions(projectId, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .AddMetadataReference(projectId, MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
            .AddMetadataReference(projectId, MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location))
            .AddMetadataReference(projectId, MetadataReference.CreateFromFile(typeof(Attribute).Assembly.Location))
            .AddMetadataReference(projectId, externalRef);

        foreach (var (fileName, source) in documents)
        {
            solution = solution.AddDocument(DocumentId.CreateNewId(projectId), fileName, SourceText.From(source));
        }

        workspace.TryApplyChanges(solution);
        var project = workspace.CurrentSolution.GetProject(projectId)!;
        var compilation = await project.GetCompilationAsync();

        var options = new AnalyzerOptions(
            ImmutableArray<AdditionalText>.Empty,
            new TestConfigOptionsProvider(analyzerConfigOptions));

        var diagnostics = await compilation!
            .WithAnalyzers(
                ImmutableArray.Create<DiagnosticAnalyzer>(new ModuleReferenceBarrierAnalyzer()),
                new CompilationWithAnalyzersOptions(options, null, true, false))
            .GetAnalyzerDiagnosticsAsync();

        workspace.Dispose();
        return diagnostics.ToList();
    }

    private sealed class TestConfigOptionsProvider(IReadOnlyDictionary<string, string> values) : AnalyzerConfigOptionsProvider
    {
        private readonly AnalyzerConfigOptions _options = new TestConfigOptions(values);
        public override AnalyzerConfigOptions GlobalOptions => _options;
        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => _options;
        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => _options;
    }

    private sealed class TestConfigOptions(IReadOnlyDictionary<string, string> values) : AnalyzerConfigOptions
    {
        public override bool TryGetValue(string key, out string value)
            => values.TryGetValue(key, out value!);
    }
}
