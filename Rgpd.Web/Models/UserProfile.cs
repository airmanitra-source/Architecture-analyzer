using Rgpd.Infrastructure.Security;

namespace Rgpd.Web.Models;

public sealed class UserProfile
{
    public string UserId { get; init; } = string.Empty;

    [PersonalData(Purpose.Marketing, LegalBasis.Consent, AccessRole.Marketing)]
    public string Email { get; init; } = string.Empty;

    [PersonalData(Purpose.Analytics, LegalBasis.LegitimateInterest, AccessRole.Analytics)]
    public string PhoneNumber { get; init; } = string.Empty;

    public string FullName { get; init; } = string.Empty;
}
