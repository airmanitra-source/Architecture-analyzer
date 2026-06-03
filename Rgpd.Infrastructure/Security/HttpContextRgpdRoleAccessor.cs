using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Rgpd.Infrastructure.Security;

public sealed class HttpContextRgpdRoleAccessor : IRgpdRoleAccessor
{
    private const string SqlRoleItemKey = "RgpdSqlRole";
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
            return "Anonymous";
        }

        if (context.Items.TryGetValue(SqlRoleItemKey, out var role) && role is string roleValue && !string.IsNullOrWhiteSpace(roleValue))
        {
            return roleValue;
        }

        return context.User.FindFirstValue("rgpd:sql-role")
            ?? context.User.FindFirstValue(ClaimTypes.Role)
            ?? "Anonymous";
    }
}
