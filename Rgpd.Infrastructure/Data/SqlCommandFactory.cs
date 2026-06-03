using System.Data;
using Microsoft.Data.SqlClient;
using Rgpd.Infrastructure.Security;

namespace Rgpd.Infrastructure.Data;

public sealed class SqlCommandFactory : ISqlCommandFactory
{
    private readonly IRgpdRoleAccessor _roleAccessor;

    public SqlCommandFactory(IRgpdRoleAccessor roleAccessor)
    {
        _roleAccessor = roleAccessor;
    }

    public SqlCommand CreateFilterCommand(SqlConnection connection, string commandText)
    {
        var role = _roleAccessor.GetCurrentSqlRole();
        var filteredCommandText = $"EXEC sp_set_session_context @key=N'rgpd_role', @value=@rgpdRole; {commandText}";

        var command = new SqlCommand(filteredCommandText, connection)
        {
            CommandType = CommandType.Text
        };

        command.Parameters.Add(new SqlParameter("@rgpdRole", SqlDbType.NVarChar, 128)
        {
            Value = role
        });

        return command;
    }
}
