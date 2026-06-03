using Rgpd.Web.Models;

namespace Rgpd.Web.ViewModels;

public sealed class HomeIndexViewModel
{
    public UserProfile Profile { get; init; } = new();
    public bool MarketingConsent { get; init; }
    public bool AnalyticsConsent { get; init; }
}
