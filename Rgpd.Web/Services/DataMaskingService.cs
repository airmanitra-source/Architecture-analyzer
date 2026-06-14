using System.Security.Claims;
using Rgpd.Infrastructure.Security;

namespace Rgpd.Web.Services;

public sealed class DataMaskingService : IDataMaskingService
{
    public string MaskValue(object? value, PersonalDataAccessContext accessContext, ClaimsPrincipal principal)
    {
        var content = value?.ToString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(content))
        {
            return content;
        }

        if (CanAccessPersonalData(accessContext, principal))
        {
            return content;
        }

        if (content.Contains('@'))
        {
            var at = content.IndexOf('@');
            if (at <= 1)
            {
                return "***";
            }

            return $"{content[0]}***{content[at..]}";
        }

        if (content.Length <= 4)
        {
            return "****";
        }

        return $"{new string('*', Math.Max(0, content.Length - 4))}{content[^4..]}";
    }

    private static bool CanAccessPersonalData(PersonalDataAccessContext accessContext, ClaimsPrincipal principal)
    {
        if (principal.IsInRole(RgpdSecurityConstants.DataOfficerRole))
        {
            return true;
        }

        if (!principal.IsInRole(accessContext.AccessRole.ToString()))
        {
            return false;
        }

        var purposeClaimType = $"{RgpdSecurityConstants.PurposeClaimPrefix}{accessContext.Purpose.ToString().ToLowerInvariant()}";
        if (!principal.HasClaim(purposeClaimType, RgpdSecurityConstants.EnabledClaimValue))
        {
            return false;
        }

        var legalBasisClaimType = $"{RgpdSecurityConstants.LegalBasisClaimPrefix}{accessContext.LegalBasis.ToString().ToLowerInvariant()}";
        return principal.HasClaim(legalBasisClaimType, RgpdSecurityConstants.EnabledClaimValue);
    }
}
