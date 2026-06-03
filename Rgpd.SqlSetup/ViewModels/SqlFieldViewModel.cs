namespace Rgpd.SqlSetup.ViewModels;

public sealed class SqlFieldViewModel
{
    public string TableName { get; init; } = string.Empty;
    public string ColumnName { get; init; } = string.Empty;
    public string Purpose { get; init; } = string.Empty;
    public string AllowedRoles { get; init; } = string.Empty;
}
