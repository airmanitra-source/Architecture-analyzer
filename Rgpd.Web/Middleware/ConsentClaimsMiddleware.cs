using System.Security.Claims;
using Rgpd.Web.Contracts;

namespace Rgpd.Web.Middleware;

public sealed class ConsentClaimsMiddleware
{
    private readonly RequestDelegate _next;

    public ConsentClaimsMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IConsentRepository consentRepository, ISqlRoleMapper sqlRoleMapper)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? context.User.Identity.Name;
            if (!string.IsNullOrWhiteSpace(userId))
            {
                var purposes = await consentRepository.GetActivePurposesAsync(userId, context.RequestAborted);
                var identity = new ClaimsIdentity();
                foreach (var purpose in purposes)
                {
                    identity.AddClaim(new Claim($"rgpd:purpose:{purpose.ToLowerInvariant()}", "true"));
                }

                var sqlRole = sqlRoleMapper.ResolveSqlRole(context.User, purposes);
                identity.AddClaim(new Claim("rgpd:sql-role", sqlRole));
                context.User.AddIdentity(identity);
            }
        }

        await _next(context);
    }
}
