using Microsoft.Data.SqlClient;
using Rgpd.Infrastructure.Data;
using Rgpd.Web.Contracts;
using Rgpd.Web.Models;

namespace Rgpd.Web.Data;

public sealed class AdoConsentRepository : RgpdRepository<ConsentRecord>, IConsentRepository
{
    public AdoConsentRepository(IConfiguration configuration, ISqlCommandFactory sqlCommandFactory)
        : base(configuration.GetConnectionString("DefaultConnection") ?? string.Empty, sqlCommandFactory)
    {
    }

    public async Task<IReadOnlyCollection<string>> GetActivePurposesAsync(string userId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Array.Empty<string>();
        }

        try
        {
            var result = await ExecuteQueryAsync(
                """
                SELECT p.Code
                FROM dbo.Consent c
                JOIN dbo.Purpose p ON p.PurposeId = c.PurposeId
                WHERE c.UserId = @userId
                  AND c.ConsentedAt IS NOT NULL
                  AND c.WithdrawnAt IS NULL
                """,
                parameters =>
                {
                    parameters.Add(new SqlParameter("@userId", userId));
                },
                reader => reader.GetString(0),
                cancellationToken);

            return result.ToArray();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    public Task UpsertConsentAsync(string userId, string purpose, bool accepted, CancellationToken cancellationToken)
    {
        var sql = """
DECLARE @purposeId INT = (SELECT PurposeId FROM dbo.Purpose WHERE Code = @purpose);
IF @purposeId IS NULL
    RETURN;

MERGE dbo.Consent AS target
USING (SELECT @userId AS UserId, @purposeId AS PurposeId) AS source
ON target.UserId = source.UserId AND target.PurposeId = source.PurposeId
WHEN MATCHED THEN
    UPDATE SET ConsentedAt = CASE WHEN @accepted = 1 THEN SYSUTCDATETIME() ELSE NULL END,
               WithdrawnAt = CASE WHEN @accepted = 1 THEN NULL ELSE SYSUTCDATETIME() END
WHEN NOT MATCHED THEN
    INSERT (UserId, PurposeId, ConsentedAt, WithdrawnAt)
    VALUES (@userId, @purposeId, CASE WHEN @accepted = 1 THEN SYSUTCDATETIME() ELSE NULL END, CASE WHEN @accepted = 1 THEN NULL ELSE SYSUTCDATETIME() END);
""";

        return ExecuteNonQueryAsync(
            sql,
            parameters =>
            {
                parameters.Add(new SqlParameter("@userId", userId));
                parameters.Add(new SqlParameter("@purpose", purpose));
                parameters.Add(new SqlParameter("@accepted", accepted));
            },
            cancellationToken);
    }
}
