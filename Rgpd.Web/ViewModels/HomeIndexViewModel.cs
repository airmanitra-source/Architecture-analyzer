using Rgpd.Web.Models;

namespace Rgpd.Web.ViewModels;

public sealed class HomeIndexViewModel
{
    public UserProfile Profile { get; init; } = new();
    public bool MarketingConsent { get; init; }
    public bool AnalyticsConsent { get; init; }
    public bool IsAuthenticated { get; init; }
    public string ApplicationRole { get; init; } = "Anonymous";
    public string SqlRole { get; init; } = "Anonymous";
    public IReadOnlyCollection<string> ActivePurposes { get; init; } = Array.Empty<string>();
}
