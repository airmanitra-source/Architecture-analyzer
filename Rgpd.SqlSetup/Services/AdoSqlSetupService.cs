using Microsoft.Data.SqlClient;
using Rgpd.Infrastructure.Data;
using Rgpd.Infrastructure.Migrations;
using Rgpd.Infrastructure.Security;
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
        const string sql = """
SELECT f.TableName, f.ColumnName, p.Code, ISNULL(lb.Code, N''), f.AllowedRoles
FROM dbo.RgpdField f
JOIN dbo.Purpose p ON p.PurposeId = f.PurposeId
LEFT JOIN dbo.LegalBasis lb ON lb.LegalBasisId = f.LegalBasisId
ORDER BY f.TableName, f.ColumnName
""";
        return await ExecuteReaderAsync(sql, static reader => new SqlFieldViewModel
        {
            TableName = reader.GetString(0),
            ColumnName = reader.GetString(1),
            Purpose = reader.GetString(2),
            LegalBasis = reader.GetString(3),
            AllowedRoles = reader.GetString(4)
        }, cancellationToken);
    }

    public async Task ApplyMaskingAsync(AddFieldRequest request, CancellationToken cancellationToken)
    {
        if (!RgpdSecurityConstants.AllowedLegalBases.Contains(request.LegalBasis))
        {
            throw new ArgumentException("La base légale doit correspondre à l'une des bases prévues par l'article 6 du RGPD.", nameof(request));
        }

        const string ddl = """
DECLARE @sql nvarchar(max) = N'ALTER TABLE ' + QUOTENAME(@tableName) + N' ALTER COLUMN ' + QUOTENAME(@columnName) + N' ADD MASKED WITH (FUNCTION = ' + QUOTENAME(@maskFunction, '''') + N');';
EXEC sys.sp_executesql @sql;
""";

        const string upsertMetadata = """
DECLARE @purposeId INT = (SELECT PurposeId FROM dbo.Purpose WHERE Code = @purpose);
IF @purposeId IS NULL
BEGIN
    INSERT INTO dbo.Purpose (Code, Label) VALUES (@purpose, @purpose);
    SET @purposeId = SCOPE_IDENTITY();
END;

DECLARE @legalBasisId INT = (SELECT LegalBasisId FROM dbo.LegalBasis WHERE Code = @legalBasis);
IF @legalBasisId IS NULL
    THROW 50001, N'Base légale RGPD invalide.', 1;

MERGE dbo.RgpdField AS target
USING (SELECT @tableName AS TableName, @columnName AS ColumnName) AS source
ON target.TableName = source.TableName AND target.ColumnName = source.ColumnName
WHEN MATCHED THEN UPDATE SET PurposeId = @purposeId, LegalBasisId = @legalBasisId, AllowedRoles = @roles
WHEN NOT MATCHED THEN INSERT (TableName, ColumnName, PurposeId, LegalBasisId, AllowedRoles) VALUES (@tableName, @columnName, @purposeId, @legalBasisId, @roles);
""";

        await ExecuteNonQueryAsync(
            ddl,
            parameters =>
            {
                parameters.Add(new SqlParameter("@tableName", request.TableName));
                parameters.Add(new SqlParameter("@columnName", request.ColumnName));
                parameters.Add(new SqlParameter("@maskFunction", request.MaskFunction));
            },
            cancellationToken);
        await ExecuteNonQueryAsync(
            upsertMetadata,
            parameters =>
            {
                parameters.Add(new SqlParameter("@tableName", request.TableName));
                parameters.Add(new SqlParameter("@columnName", request.ColumnName));
                parameters.Add(new SqlParameter("@maskFunction", request.MaskFunction));
                parameters.Add(new SqlParameter("@purpose", request.Purpose));
                parameters.Add(new SqlParameter("@legalBasis", request.LegalBasis));
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
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            return;
        }

        // La politique RLS est un script "manuel" embarqué, appliqué explicitement depuis cet écran.
        var migrator = new DatabaseMigrator(_connectionString);
        await migrator.ApplyManualScriptAsync("RlsPolicy.sql", cancellationToken);
    }

    public async Task<IReadOnlyList<RoleMappingViewModel>> GetRoleMappingsAsync(CancellationToken cancellationToken)
    {
        const string sql = """
SELECT r.Name, r.SqlRole, ISNULL(p.Code, N'')
FROM dbo.[Role] r
LEFT JOIN dbo.RolePurpose rp ON rp.RoleId = r.RoleId
LEFT JOIN dbo.Purpose p ON p.PurposeId = rp.PurposeId
ORDER BY r.Name, p.Code
""";
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

MERGE dbo.[Role] AS target
USING (SELECT @appRole AS Name) AS source
ON target.Name = source.Name
WHEN MATCHED THEN UPDATE SET SqlRole = @sqlRole
WHEN NOT MATCHED THEN INSERT (Name, SqlRole) VALUES (@appRole, @sqlRole);

DECLARE @roleId INT = (SELECT RoleId FROM dbo.[Role] WHERE Name = @appRole);
DECLARE @purposeId INT = (SELECT PurposeId FROM dbo.Purpose WHERE Code = @purpose);

IF @purposeId IS NULL AND NULLIF(@purpose, N'') IS NOT NULL
BEGIN
    INSERT INTO dbo.Purpose (Code, Label) VALUES (@purpose, @purpose);
    SET @purposeId = SCOPE_IDENTITY();
END;

IF @roleId IS NOT NULL AND @purposeId IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM dbo.RolePurpose WHERE RoleId = @roleId AND PurposeId = @purposeId)
BEGIN
    INSERT INTO dbo.RolePurpose (RoleId, PurposeId) VALUES (@roleId, @purposeId);
END;
""";

        return ExecuteNonQueryAsync(
            sql,
            parameters =>
            {
                parameters.Add(new SqlParameter("@appRole", request.ApplicationRole));
                parameters.Add(new SqlParameter("@sqlRole", request.SqlRole));
                parameters.Add(new SqlParameter("@purpose", (object?)request.Purpose ?? string.Empty));
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
