namespace Rgpd.Web.Middleware;

public sealed class SqlRoleContextMiddleware
{
    private readonly RequestDelegate _next;

    public SqlRoleContextMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        context.Items[Rgpd.Infrastructure.Security.RgpdSecurityConstants.SqlRoleItemKey] = context.User.FindFirst(Rgpd.Infrastructure.Security.RgpdSecurityConstants.SqlRoleClaimType)?.Value ?? Rgpd.Infrastructure.Security.RgpdSecurityConstants.AnonymousRole;
        await _next(context);
    }
}
