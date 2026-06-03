namespace Rgpd.SqlSetup.ViewModels;

public sealed class RlsPolicyViewModel
{
    public string PolicyName { get; init; } = string.Empty;
    public string PredicateFunctionName { get; init; } = string.Empty;
    public bool IsEnabled { get; init; }
}
