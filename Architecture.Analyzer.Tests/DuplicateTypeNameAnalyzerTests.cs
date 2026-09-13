using System.Collections.Immutable;
using Architecture.Analyzer;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Architecture.Analyzer.Tests;

public sealed class DuplicateTypeNameAnalyzerTests
{
    private const string Exceptions = "architecture_analyzer.duplicate_type_name_exceptions";

    [Fact]
    public async Task ReportsWhenTheTypeAlreadyExistsInAReferencedAssembly()
    {
        var reference = BuildAssembly("MyApp.Infrastructure", "namespace MyApp.Infrastructure { public class TextJoiner { } }");

        var diagnostics = await AnalyzeAsync(
            "MyApp.Portal",
            "namespace MyApp.Portal { public class TextJoiner { } }",
            reference);

        Assert.Single(diagnostics, d => d.Id == DuplicateTypeNameAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task MessageNamesTheAssemblyToReuse()
    {
        var reference = BuildAssembly("MyApp.Infrastructure", "namespace MyApp.Infrastructure { public class TextJoiner { } }");

        var diagnostics = await AnalyzeAsync(
            "MyApp.Portal",
            "namespace MyApp.Portal { public class TextJoiner { } }",
            reference);

        var message = Assert.Single(diagnostics, d => d.Id == DuplicateTypeNameAnalyzer.DiagnosticId).GetMessage();
        Assert.Contains("TextJoiner", message);
        Assert.Contains("MyApp.Infrastructure", message);
    }

    [Fact]
    public async Task DoesNotReportWhenTheNamesDiffer()
    {
        var reference = BuildAssembly("MyApp.Infrastructure", "namespace MyApp.Infrastructure { public class TextJoiner { } }");

        var diagnostics = await AnalyzeAsync(
            "MyApp.Portal",
            "namespace MyApp.Portal { public class SegmentJoiner { } }",
            reference);

        Assert.DoesNotContain(diagnostics, d => d.Id == DuplicateTypeNameAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task DoesNotReportWhenTheReferencedAssemblyIsOutsideTheProductFamily()
    {
        // Another vendor's library sharing a type name is not duplication of ours.
        var reference = BuildAssembly("Other.Library", "namespace Other.Library { public class TextJoiner { } }");

        var diagnostics = await AnalyzeAsync(
            "MyApp.Portal",
            "namespace MyApp.Portal { public class TextJoiner { } }",
            reference);

        Assert.DoesNotContain(diagnostics, d => d.Id == DuplicateTypeNameAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task DoesNotReportWhenTheReferencedTypeIsInternal()
    {
        // An internal type could not have been reused, so redeclaring one is not duplication.
        var reference = BuildAssembly("MyApp.Infrastructure", "namespace MyApp.Infrastructure { internal class TextJoiner { } }");

        var diagnostics = await AnalyzeAsync(
            "MyApp.Portal",
            "namespace MyApp.Portal { public class TextJoiner { } }",
            reference);

        Assert.DoesNotContain(diagnostics, d => d.Id == DuplicateTypeNameAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task DoesNotReportWhenTheGenericArityDiffers()
    {
        var reference = BuildAssembly("MyApp.Infrastructure", "namespace MyApp.Infrastructure { public class Wrapper<T> { } }");

        var diagnostics = await AnalyzeAsync(
            "MyApp.Portal",
            "namespace MyApp.Portal { public class Wrapper { } }",
            reference);

        Assert.DoesNotContain(diagnostics, d => d.Id == DuplicateTypeNameAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task DoesNotReportAnExceptedName()
    {
        var reference = BuildAssembly("MyApp.Infrastructure", "namespace MyApp.Infrastructure { public class TextJoiner { } }");

        var diagnostics = await AnalyzeAsync(
            "MyApp.Portal",
            "namespace MyApp.Portal { public class TextJoiner { } }",
            reference,
            new Dictionary<string, string> { [Exceptions] = "TextJoiner;Program" });

        Assert.DoesNotContain(diagnostics, d => d.Id == DuplicateTypeNameAnalyzer.DiagnosticId);
    }

    private static MetadataReference BuildAssembly(string assemblyName, string source)
    {
        var compilation = CSharpCompilation.Create(
            assemblyName,
            [CSharpSyntaxTree.ParseText(source)],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var stream = new MemoryStream();
        compilation.Emit(stream);
        stream.Seek(0, SeekOrigin.Begin);
        return MetadataReference.CreateFromStream(stream);
    }

    private static async Task<List<Diagnostic>> AnalyzeAsync(
        string projectName,
        string source,
        MetadataReference externalReference,
        IReadOnlyDictionary<string, string>? config = null)
    {
        using var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();

        var solution = workspace.CurrentSolution
            .AddProject(ProjectInfo.Create(projectId, VersionStamp.Default, projectName, projectName, LanguageNames.CSharp))
            .WithProjectCompilationOptions(projectId, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .AddMetadataReference(projectId, MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
            .AddMetadataReference(projectId, externalReference)
            .AddDocument(DocumentId.CreateNewId(projectId), "Type.cs", SourceText.From(source));

        workspace.TryApplyChanges(solution);

        var compilation = await workspace.CurrentSolution.GetProject(projectId)!.GetCompilationAsync();
        var options = new AnalyzerOptions(
            ImmutableArray<AdditionalText>.Empty,
            new ConfigProvider(config ?? new Dictionary<string, string>()));

        var diagnostics = await compilation!
            .WithAnalyzers(
                ImmutableArray.Create<DiagnosticAnalyzer>(new DuplicateTypeNameAnalyzer()),
                new CompilationWithAnalyzersOptions(options, null, true, false))
            .GetAnalyzerDiagnosticsAsync();

        return diagnostics.ToList();
    }

    private sealed class ConfigProvider(IReadOnlyDictionary<string, string> values) : AnalyzerConfigOptionsProvider
    {
        private readonly AnalyzerConfigOptions _options = new Options(values);

        public override AnalyzerConfigOptions GlobalOptions => _options;

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => _options;

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => _options;
    }

    private sealed class Options(IReadOnlyDictionary<string, string> values) : AnalyzerConfigOptions
    {
        public override bool TryGetValue(string key, out string value) => values.TryGetValue(key, out value!);
    }
}
