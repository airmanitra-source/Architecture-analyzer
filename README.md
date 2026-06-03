# Rgpd.NET

RGPD compliance sample in .NET 10 with:

- `Rgpd.Infrastructure`: ADO.NET command factory (`SqlCommandFactory.CreateFilterCommand`) that injects SQL `SESSION_CONTEXT` role and a base `RgpdRepository<T>`.
- `Rgpd.Analyzer`: Roslyn analyzer + code fix (`RGPD001`) blocking direct `new SqlCommand()` usage.
- `Rgpd.Web`: consent-oriented web app with claim enrichment middleware, role mapping, application-level masking service and `<rgpd:field asp-for="..." />` tag helper.
- `Rgpd.SqlSetup`: admin web setup for SQL masking metadata, RLS policy synchronization and identity role ↔ SQL role mappings.

## Build and test

```bash
dotnet build /tmp/workspace/airmanitra-source/Rgpd.NET/Rgpd.NET.slnx
dotnet test /tmp/workspace/airmanitra-source/Rgpd.NET/Rgpd.Analyzer.Tests/Rgpd.Analyzer.Tests.csproj
```
