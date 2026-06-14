using Rgpd.Infrastructure.Data;
using Rgpd.Infrastructure.Migrations;
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

if (app.Environment.IsDevelopment())
{
    var connectionString = app.Configuration.GetConnectionString("DefaultConnection");
    if (!string.IsNullOrWhiteSpace(connectionString))
    {
        try
        {
            var migrator = new DatabaseMigrator(connectionString);
            await migrator.MigrateAsync();
        }
        catch (Exception ex)
        {
            app.Logger.LogWarning(ex, "Migration de la base RGPD ignorée (base indisponible ?).");
        }
    }
}

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
