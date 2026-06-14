using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Rgpd.Infrastructure.Security;

public sealed class HttpContextRgpdRoleAccessor : IRgpdRoleAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextRgpdRoleAccessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string GetCurrentSqlRole()
    {
        var context = _httpContextAccessor.HttpContext;
        if (context is null)
        {
            return RgpdSecurityConstants.AnonymousRole;
        }

        if (context.Items.TryGetValue(RgpdSecurityConstants.SqlRoleItemKey, out var role) && role is string roleValue && !string.IsNullOrWhiteSpace(roleValue))
        {
            return roleValue;
        }

        return context.User.FindFirstValue(RgpdSecurityConstants.SqlRoleClaimType)
            ?? context.User.FindFirstValue(ClaimTypes.Role)
            ?? RgpdSecurityConstants.AnonymousRole;
    }
}
