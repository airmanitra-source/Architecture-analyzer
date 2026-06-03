using Microsoft.AspNetCore.Authentication.Cookies;
using Rgpd.Infrastructure.Data;
using Rgpd.Infrastructure.Security;
using Rgpd.Web.Contracts;
using Rgpd.Web.Data;
using Rgpd.Web.Middleware;
using Rgpd.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie();

builder.Services.AddAuthorization();

builder.Services.AddScoped<IRgpdRoleAccessor, HttpContextRgpdRoleAccessor>();
builder.Services.AddScoped<ISqlCommandFactory, SqlCommandFactory>();
builder.Services.AddScoped<IConsentRepository, AdoConsentRepository>();
builder.Services.AddScoped<ISqlRoleMapper, SqlRoleMapper>();
builder.Services.AddScoped<IDataMaskingService, DataMaskingService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseMiddleware<ConsentClaimsMiddleware>();
app.UseMiddleware<SqlRoleContextMiddleware>();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
