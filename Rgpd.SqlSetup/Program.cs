using Rgpd.Infrastructure.Data;
using Rgpd.Infrastructure.Security;
using Rgpd.SqlSetup.Contracts;
using Rgpd.SqlSetup.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IRgpdRoleAccessor, HttpContextRgpdRoleAccessor>();
builder.Services.AddScoped<ISqlCommandFactory, SqlCommandFactory>();
builder.Services.AddScoped<ISqlSetupService, AdoSqlSetupService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Setup}/{action=Fields}/{id?}")
    .WithStaticAssets();

app.Run();
