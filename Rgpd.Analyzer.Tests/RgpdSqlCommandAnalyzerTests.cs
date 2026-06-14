using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Rgpd.Analyzer.Analyzers;

namespace Rgpd.Analyzer.Tests;

public sealed class RgpdSqlCommandAnalyzerTests
{
    [Fact]
    public async Task ReportsDiagnosticForDirectSqlCommandCreation()
    {
        const string source = """
using Microsoft.Data.SqlClient;

namespace Microsoft.Data.SqlClient
{
    public class SqlConnection { }
    public class SqlCommand
    {
        public SqlCommand(string sql, SqlConnection connection) { }
    }
}

public class Demo
{
    private readonly SqlConnection _connection = new();

    public void Run()
    {
        var command = new SqlCommand("SELECT 1", _connection);
    }
}
""";

        var diagnostics = await AnalyzeAsync(source);
        Assert.Contains(diagnostics, d => d.Id == RgpdSqlCommandAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task AppliesCodeFixToUseFactory()
    {
        const string source = """
using Microsoft.Data.SqlClient;

namespace Microsoft.Data.SqlClient
{
    public class SqlConnection { }
    public class SqlCommand
    {
        public SqlCommand(string sql, SqlConnection connection) { }
    }
}

public class Demo
{
    private readonly SqlConnection _connection = new();
    private readonly dynamic _sqlCommandFactory = null;

    public void Run()
    {
        var command = new SqlCommand("SELECT 1", _connection);
    }
}
""";

        var (document, diagnostics, workspace) = await AnalyzeWithDocumentAsync(source);
        var diagnostic = Assert.Single(diagnostics.Where(d => d.Id == RgpdSqlCommandAnalyzer.DiagnosticId));

        var provider = new RgpdSqlCommandCodeFixProvider();
        var actions = new List<CodeAction>();

        var context = new CodeFixContext(
            document,
            diagnostic,
            (action, _) => actions.Add(action),
            CancellationToken.None);

        await provider.RegisterCodeFixesAsync(context);

        var action = Assert.Single(actions);
        var operations = await action.GetOperationsAsync(CancellationToken.None);
        foreach (var operation in operations)
        {
            operation.Apply(workspace, CancellationToken.None);
        }

        var updatedDocument = workspace.CurrentSolution.GetDocument(document.Id)!;
        var updatedText = (await updatedDocument.GetTextAsync()).ToString();

        Assert.Contains("_sqlCommandFactory.CreateFilterCommand(_connection, \"SELECT 1\")", updatedText);
    }

    [Fact]
    public async Task IgnoresCreationInsideSqlCommandFactory()
    {
        const string source = """
using Microsoft.Data.SqlClient;

namespace Microsoft.Data.SqlClient
{
    public class SqlConnection { }
    public class SqlCommand
    {
        public SqlCommand(string sql, SqlConnection connection) { }
    }
}

public class SqlCommandFactory
{
    private readonly SqlConnection _connection = new();

    public void Run()
    {
        var command = new SqlCommand("SELECT 1", _connection);
    }
}
""";

        var diagnostics = await AnalyzeAsync(source);
        Assert.DoesNotContain(diagnostics, d => d.Id == RgpdSqlCommandAnalyzer.DiagnosticId);
    }

    [Fact]
    public async Task ReportsDiagnosticForAuthorizedPurposeWhenLegalBasisDoesNotMatchPurpose()
    {
        const string source = """
using System;

namespace Rgpd.Infrastructure.Security
{
    public enum Purpose { OrderTracking, Marketing, Analytics }
    public enum LegalBasis { Consent, Contract, LegalObligation, VitalInterests, PublicTask, LegitimateInterest }
    public enum AccessRole { Anonymous, HumanResources, Marketing, Analytics, DataOfficer }

    [AttributeUsage(AttributeTargets.Property)]
    public sealed class PersonalDataAttribute : Attribute
    {
        public PersonalDataAttribute(Purpose purpose, LegalBasis legalBasis, AccessRole accessRole) { }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class AuthorizedPurposeAttribute : Attribute
    {
        public AuthorizedPurposeAttribute(Purpose purpose, LegalBasis legalBasis, AccessRole accessRole) { }
    }
}

public sealed class DemoUserProfile
{
    [Rgpd.Infrastructure.Security.PersonalData(Rgpd.Infrastructure.Security.Purpose.Marketing, Rgpd.Infrastructure.Security.LegalBasis.Consent, Rgpd.Infrastructure.Security.AccessRole.Marketing)]
    public string Email { get; init; } = string.Empty;
}

public sealed class DemoService
{
    [Rgpd.Infrastructure.Security.AuthorizedPurpose(Rgpd.Infrastructure.Security.Purpose.Marketing, Rgpd.Infrastructure.Security.LegalBasis.Contract, Rgpd.Infrastructure.Security.AccessRole.Marketing)]
    public void Run(DemoUserProfile user)
    {
        Console.WriteLine(user.Email);
    }
}
""";

        var diagnostics = await AnalyzeAsync(source);
        Assert.Contains(diagnostics, d => d.Id == RgpdSqlCommandAnalyzer.PersonalDataPolicyDiagnosticId);
    }

    [Fact]
    public async Task ReportsDiagnosticForAuthorizedPurposeWhenRoleDoesNotMatchPersonalDataOwnership()
    {
        const string source = """
using System;

namespace Rgpd.Infrastructure.Security
{
    public enum Purpose { OrderTracking, Marketing, Analytics }
    public enum LegalBasis { Consent, Contract, LegalObligation, VitalInterests, PublicTask, LegitimateInterest }
    public enum AccessRole { Anonymous, HumanResources, Marketing, Analytics, DataOfficer }

    [AttributeUsage(AttributeTargets.Property)]
    public sealed class PersonalDataAttribute : Attribute
    {
        public PersonalDataAttribute(Purpose purpose, LegalBasis legalBasis, AccessRole accessRole) { }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class AuthorizedPurposeAttribute : Attribute
    {
        public AuthorizedPurposeAttribute(Purpose purpose, LegalBasis legalBasis, AccessRole accessRole) { }
    }
}

public sealed class DemoUserProfile
{
    [Rgpd.Infrastructure.Security.PersonalData(Rgpd.Infrastructure.Security.Purpose.Marketing, Rgpd.Infrastructure.Security.LegalBasis.Consent, Rgpd.Infrastructure.Security.AccessRole.Marketing)]
    public string Email { get; init; } = string.Empty;
}

public sealed class DemoService
{
    [Rgpd.Infrastructure.Security.AuthorizedPurpose(Rgpd.Infrastructure.Security.Purpose.Marketing, Rgpd.Infrastructure.Security.LegalBasis.Consent, Rgpd.Infrastructure.Security.AccessRole.Analytics)]
    public void Run(DemoUserProfile user)
    {
        Console.WriteLine(user.Email);
    }
}
""";

        var diagnostics = await AnalyzeAsync(source);
        Assert.Contains(diagnostics, d => d.Id == RgpdSqlCommandAnalyzer.PersonalDataPolicyDiagnosticId);
    }

    [Fact]
    public async Task DoesNotReportDiagnosticWhenAuthorizedPurposeMatchesOnePersonalDataPolicy()
    {
        const string source = """
using System;

namespace Rgpd.Infrastructure.Security
{
    public enum Purpose { OrderTracking, Marketing, Analytics }
    public enum LegalBasis { Consent, Contract, LegalObligation, VitalInterests, PublicTask, LegitimateInterest }
    public enum AccessRole { Anonymous, HumanResources, Marketing, Analytics, DataOfficer }

    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
    public sealed class PersonalDataAttribute : Attribute
    {
        public PersonalDataAttribute(Purpose purpose, LegalBasis legalBasis, AccessRole accessRole) { }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class AuthorizedPurposeAttribute : Attribute
    {
        public AuthorizedPurposeAttribute(Purpose purpose, LegalBasis legalBasis, AccessRole accessRole) { }
    }
}

public sealed class DemoUserProfile
{
    [Rgpd.Infrastructure.Security.PersonalData(Rgpd.Infrastructure.Security.Purpose.OrderTracking, Rgpd.Infrastructure.Security.LegalBasis.Contract, Rgpd.Infrastructure.Security.AccessRole.Marketing)]
    [Rgpd.Infrastructure.Security.PersonalData(Rgpd.Infrastructure.Security.Purpose.Marketing, Rgpd.Infrastructure.Security.LegalBasis.Consent, Rgpd.Infrastructure.Security.AccessRole.Marketing)]
    public string Email { get; init; } = string.Empty;
}

public sealed class DemoService
{
    [Rgpd.Infrastructure.Security.AuthorizedPurpose(Rgpd.Infrastructure.Security.Purpose.Marketing, Rgpd.Infrastructure.Security.LegalBasis.Consent, Rgpd.Infrastructure.Security.AccessRole.Marketing)]
    public void Run(DemoUserProfile user)
    {
        Console.WriteLine(user.Email);
    }
}
""";

        var diagnostics = await AnalyzeAsync(source);
        Assert.DoesNotContain(diagnostics, d => d.Id == RgpdSqlCommandAnalyzer.PersonalDataPolicyDiagnosticId);
    }

    private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string source)
    {
        var (_, diagnostics, _) = await AnalyzeWithDocumentAsync(source);
        return diagnostics;
    }

    private static async Task<(Document document, ImmutableArray<Diagnostic> diagnostics, AdhocWorkspace workspace)> AnalyzeWithDocumentAsync(string source)
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
            .AddDocument(documentId, "Test.cs", SourceText.From(source));

        workspace.TryApplyChanges(solution);

        var document = workspace.CurrentSolution.GetDocument(documentId)!;
        var compilation = await document.Project.GetCompilationAsync();
        Assert.NotNull(compilation);

        var analyzer = new RgpdSqlCommandAnalyzer();
        var diagnostics = await compilation!
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(analyzer))
            .GetAnalyzerDiagnosticsAsync();

        return (document, diagnostics, workspace);
    }
}
