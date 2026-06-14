using Microsoft.Data.SqlClient;

namespace Rgpd.Infrastructure.Data;

public abstract class RgpdRepository<T>
{
    private readonly string _connectionString;
    private readonly ISqlCommandFactory _sqlCommandFactory;

    protected RgpdRepository(string connectionString, ISqlCommandFactory sqlCommandFactory)
    {
        _connectionString = connectionString;
        _sqlCommandFactory = sqlCommandFactory;
    }

    protected async Task<int> ExecuteNonQueryAsync(string sql, Action<SqlParameterCollection>? configureParameters, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = _sqlCommandFactory.CreateFilterCommand(connection, sql);
        configureParameters?.Invoke(command.Parameters);

        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    protected async Task<IReadOnlyList<TResult>> ExecuteQueryAsync<TResult>(
        string sql,
        Action<SqlParameterCollection>? configureParameters,
        Func<SqlDataReader, TResult> map,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = _sqlCommandFactory.CreateFilterCommand(connection, sql);
        configureParameters?.Invoke(command.Parameters);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var items = new List<TResult>();
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(map(reader));
        }

        return items;
    }
}
