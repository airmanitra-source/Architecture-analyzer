namespace Rgpd.Infrastructure.Security;

public static class RgpdSecurityConstants
{
    public const string AnonymousRole = "Anonymous";
    public const string DataOfficerRole = "DataOfficer";
    public const string EnabledClaimValue = "true";
    public const string LegalBasisClaimPrefix = "rgpd:legal-basis:";
    public const string PurposeClaimPrefix = "rgpd:purpose:";
    public const string SqlRoleClaimType = "rgpd:sql-role";
    public const string SqlRoleItemKey = "RgpdSqlRole";

    public static readonly IReadOnlySet<string> AllowedLegalBases = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        nameof(LegalBasis.Consent),
        nameof(LegalBasis.Contract),
        nameof(LegalBasis.LegalObligation),
        nameof(LegalBasis.VitalInterests),
        nameof(LegalBasis.PublicTask),
        nameof(LegalBasis.LegitimateInterest)
    };

    public static readonly IReadOnlyDictionary<string, string[]> AllowedRolePurposesByLegalBasis = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
    {
        [nameof(LegalBasis.Consent)] = ["Marketing"],
        [nameof(LegalBasis.LegitimateInterest)] = ["Analytics"],
        [nameof(LegalBasis.Contract)] = [],
        [nameof(LegalBasis.LegalObligation)] = [],
        [nameof(LegalBasis.VitalInterests)] = [],
        [nameof(LegalBasis.PublicTask)] = []
    };
}