using Microsoft.AspNetCore.Mvc;
using Rgpd.SqlSetup.Contracts;
using Rgpd.SqlSetup.ViewModels;

namespace Rgpd.SqlSetup.Controllers;

public sealed class SetupController : Controller
{
    private readonly ISqlSetupService _sqlSetupService;

    public SetupController(ISqlSetupService sqlSetupService)
    {
        _sqlSetupService = sqlSetupService;
    }

    [HttpGet]
    public async Task<IActionResult> Fields(CancellationToken cancellationToken)
    {
        var model = await _sqlSetupService.GetFieldsAsync(cancellationToken);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddField(AddFieldRequest request, CancellationToken cancellationToken)
    {
        await _sqlSetupService.ApplyMaskingAsync(request, cancellationToken);
        return RedirectToAction(nameof(Fields));
    }

    [HttpGet]
    public async Task<IActionResult> Policies(CancellationToken cancellationToken)
    {
        var model = await _sqlSetupService.GetPoliciesAsync(cancellationToken);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SyncPolicies(CancellationToken cancellationToken)
    {
        await _sqlSetupService.SyncRlsPoliciesAsync(cancellationToken);
        return RedirectToAction(nameof(Policies));
    }

    [HttpGet]
    public async Task<IActionResult> RoleMappings(CancellationToken cancellationToken)
    {
        var model = await _sqlSetupService.GetRoleMappingsAsync(cancellationToken);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddRoleMapping(RoleMappingViewModel request, CancellationToken cancellationToken)
    {
        await _sqlSetupService.UpsertRoleMappingAsync(request, cancellationToken);
        return RedirectToAction(nameof(RoleMappings));
    }
}
