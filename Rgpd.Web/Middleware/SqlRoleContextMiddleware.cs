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
        context.Items["RgpdSqlRole"] = context.User.FindFirst("rgpd:sql-role")?.Value ?? "Anonymous";
        await _next(context);
    }
}
