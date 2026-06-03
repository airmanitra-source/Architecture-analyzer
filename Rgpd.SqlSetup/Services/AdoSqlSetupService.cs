using Microsoft.Data.SqlClient;
using Rgpd.Infrastructure.Data;
using Rgpd.SqlSetup.Contracts;
using Rgpd.SqlSetup.ViewModels;

namespace Rgpd.SqlSetup.Services;

public sealed class AdoSqlSetupService : ISqlSetupService
{
    private readonly string _connectionString;
    private readonly ISqlCommandFactory _sqlCommandFactory;

    public AdoSqlSetupService(IConfiguration configuration, ISqlCommandFactory sqlCommandFactory)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
        _sqlCommandFactory = sqlCommandFactory;
    }

    public async Task<IReadOnlyList<SqlFieldViewModel>> GetFieldsAsync(CancellationToken cancellationToken)
    {
        const string sql = "SELECT TableName, ColumnName, Purpose, AllowedRoles FROM RgpdField ORDER BY TableName, ColumnName";
        return await ExecuteReaderAsync(sql, static reader => new SqlFieldViewModel
        {
            TableName = reader.GetString(0),
            ColumnName = reader.GetString(1),
            Purpose = reader.GetString(2),
            AllowedRoles = reader.GetString(3)
        }, cancellationToken);
    }

    public async Task ApplyMaskingAsync(AddFieldRequest request, CancellationToken cancellationToken)
    {
        var ddl = $"ALTER TABLE [{request.TableName}] ALTER COLUMN [{request.ColumnName}] ADD MASKED WITH (FUNCTION = '{request.MaskFunction}');";
        const string upsertMetadata = """
MERGE RgpdField AS target
USING (SELECT @tableName AS TableName, @columnName AS ColumnName) AS source
ON target.TableName = source.TableName AND target.ColumnName = source.ColumnName
WHEN MATCHED THEN UPDATE SET Purpose = @purpose, AllowedRoles = @roles
WHEN NOT MATCHED THEN INSERT (TableName, ColumnName, Purpose, AllowedRoles) VALUES (@tableName, @columnName, @purpose, @roles);
""";

        await ExecuteNonQueryAsync(ddl, null, cancellationToken);
        await ExecuteNonQueryAsync(
            upsertMetadata,
            parameters =>
            {
                parameters.Add(new SqlParameter("@tableName", request.TableName));
                parameters.Add(new SqlParameter("@columnName", request.ColumnName));
                parameters.Add(new SqlParameter("@purpose", request.Purpose));
                parameters.Add(new SqlParameter("@roles", request.AllowedRoles));
            },
            cancellationToken);
    }

    public async Task<IReadOnlyList<RlsPolicyViewModel>> GetPoliciesAsync(CancellationToken cancellationToken)
    {
        const string sql = """
SELECT p.name, OBJECT_NAME(pr.target_object_id), p.is_enabled
FROM sys.security_policies p
LEFT JOIN sys.security_predicates pr ON p.object_id = pr.object_id
ORDER BY p.name
""";

        return await ExecuteReaderAsync(sql, static reader => new RlsPolicyViewModel
        {
            PolicyName = reader.GetString(0),
            PredicateFunctionName = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
            IsEnabled = reader.GetBoolean(2)
        }, cancellationToken);
    }

    public async Task SyncRlsPoliciesAsync(CancellationToken cancellationToken)
    {
        const string syncSql = """
IF OBJECT_ID('dbo.fn_rgpd_rls_predicate', 'IF') IS NULL
EXEC('CREATE FUNCTION dbo.fn_rgpd_rls_predicate(@Purpose nvarchar(100)) RETURNS TABLE WITH SCHEMABINDING AS RETURN SELECT 1 AS fn_securitypredicate_result WHERE @Purpose = CAST(SESSION_CONTEXT(N''rgpd_role'') AS nvarchar(100));');

IF NOT EXISTS (SELECT 1 FROM sys.security_policies WHERE name = 'rgpd_policy')
BEGIN
    CREATE SECURITY POLICY dbo.rgpd_policy
    ADD FILTER PREDICATE dbo.fn_rgpd_rls_predicate(Purpose) ON dbo.ConsentRecord
    WITH (STATE = ON);
END
""";

        await ExecuteNonQueryAsync(syncSql, null, cancellationToken);
    }

    public async Task<IReadOnlyList<RoleMappingViewModel>> GetRoleMappingsAsync(CancellationToken cancellationToken)
    {
        const string sql = "SELECT ApplicationRole, SqlRole, Purpose FROM RgpdRoleMapping ORDER BY ApplicationRole";
        return await ExecuteReaderAsync(sql, static reader => new RoleMappingViewModel
        {
            ApplicationRole = reader.GetString(0),
            SqlRole = reader.GetString(1),
            Purpose = reader.GetString(2)
        }, cancellationToken);
    }

    public Task UpsertRoleMappingAsync(RoleMappingViewModel request, CancellationToken cancellationToken)
    {
        const string sql = """
IF DATABASE_PRINCIPAL_ID(@sqlRole) IS NULL
    EXEC('CREATE ROLE [' + @sqlRole + ']');

MERGE RgpdRoleMapping AS target
USING (SELECT @appRole AS ApplicationRole) AS source
ON target.ApplicationRole = source.ApplicationRole
WHEN MATCHED THEN UPDATE SET SqlRole = @sqlRole, Purpose = @purpose
WHEN NOT MATCHED THEN INSERT (ApplicationRole, SqlRole, Purpose) VALUES (@appRole, @sqlRole, @purpose);
""";

        return ExecuteNonQueryAsync(
            sql,
            parameters =>
            {
                parameters.Add(new SqlParameter("@appRole", request.ApplicationRole));
                parameters.Add(new SqlParameter("@sqlRole", request.SqlRole));
                parameters.Add(new SqlParameter("@purpose", request.Purpose));
            },
            cancellationToken);
    }

    private async Task<IReadOnlyList<T>> ExecuteReaderAsync<T>(string sql, Func<SqlDataReader, T> map, CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = _sqlCommandFactory.CreateFilterCommand(connection, sql);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var items = new List<T>();
            while (await reader.ReadAsync(cancellationToken))
            {
                items.Add(map(reader));
            }

            return items;
        }
        catch
        {
            return Array.Empty<T>();
        }
    }

    private async Task ExecuteNonQueryAsync(string sql, Action<SqlParameterCollection>? configureParameters, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            return;
        }

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = _sqlCommandFactory.CreateFilterCommand(connection, sql);
        configureParameters?.Invoke(command.Parameters);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
