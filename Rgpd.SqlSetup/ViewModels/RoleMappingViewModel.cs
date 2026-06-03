namespace Rgpd.SqlSetup.ViewModels;

public sealed class RoleMappingViewModel
{
    public string ApplicationRole { get; init; } = string.Empty;
    public string SqlRole { get; init; } = string.Empty;
    public string Purpose { get; init; } = string.Empty;
}
