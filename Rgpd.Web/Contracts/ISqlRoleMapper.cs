using System.Security.Claims;

namespace Rgpd.Web.Contracts;

public interface ISqlRoleMapper
{
    string ResolveSqlRole(ClaimsPrincipal principal, IReadOnlyCollection<string> activePurposes);
}
