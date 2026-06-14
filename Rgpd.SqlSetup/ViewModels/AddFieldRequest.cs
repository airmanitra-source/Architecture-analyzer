namespace Rgpd.SqlSetup.ViewModels;

public sealed class AddFieldRequest
{
    public string TableName { get; init; } = string.Empty;
    public string ColumnName { get; init; } = string.Empty;
    public string MaskFunction { get; init; } = "default()";
    public string Purpose { get; init; } = string.Empty;
    public string LegalBasis { get; init; } = "Consent";
    public string AllowedRoles { get; init; } = string.Empty;
}
