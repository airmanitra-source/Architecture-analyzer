using System.Security.Claims;
using Rgpd.Web.Contracts;

namespace Rgpd.Web.Services;

public sealed class SqlRoleMapper : ISqlRoleMapper
{
    public string ResolveSqlRole(ClaimsPrincipal principal, IReadOnlyCollection<string> activePurposes)
    {
        if (principal.IsInRole("DataOfficer"))
        {
            return "DataOfficer";
        }

        if (activePurposes.Contains("Marketing", StringComparer.OrdinalIgnoreCase))
        {
            return "Marketing";
        }

        if (activePurposes.Contains("Analytics", StringComparer.OrdinalIgnoreCase))
        {
            return "Analytics";
        }

        return "Anonymous";
    }
}
