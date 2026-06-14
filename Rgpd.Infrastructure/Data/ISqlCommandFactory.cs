using System.Diagnostics.CodeAnalysis;
using Microsoft.Data.SqlClient;

namespace Rgpd.Infrastructure.Data;

public interface ISqlCommandFactory
{
    SqlCommand CreateFilterCommand(SqlConnection connection, string commandText);
}
