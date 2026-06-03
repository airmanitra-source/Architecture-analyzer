using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing.Verifiers;
using Rgpd.Analyzer.Analyzers;
using Xunit;

namespace Rgpd.Analyzer.Tests;

using Verify = CSharpCodeFixVerifier<RgpdSqlCommandAnalyzer, RgpdSqlCommandCodeFixProvider, XUnitVerifier>;

public sealed class UnitTest1
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
        var command = {|RGPD001:new SqlCommand(\"SELECT 1\", _connection)|};
    }
}
""";

        await Verify.VerifyAnalyzerAsync(source);
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
        var command = {|RGPD001:new SqlCommand(\"SELECT 1\", _connection)|};
    }
}
""";

        const string fixedSource = """
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
        var command = _sqlCommandFactory.CreateFilterCommand(_connection, "SELECT 1");
    }
}
""";

        await Verify.VerifyCodeFixAsync(source, fixedSource);
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

        await Verify.VerifyAnalyzerAsync(source);
    }
}
