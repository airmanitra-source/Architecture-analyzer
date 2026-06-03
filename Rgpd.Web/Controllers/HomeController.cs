using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Rgpd.Web.Contracts;
using Rgpd.Web.Models;
using Rgpd.Web.ViewModels;

namespace Rgpd.Web.Controllers;

public class HomeController : Controller
{
    private readonly IConsentRepository _consentRepository;

    public HomeController(IConsentRepository consentRepository)
    {
        _consentRepository = consentRepository;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var userId = User.Identity?.IsAuthenticated == true
            ? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "demo-user"
            : "demo-user";

        var activePurposes = await _consentRepository.GetActivePurposesAsync(userId, cancellationToken);

        var vm = new HomeIndexViewModel
        {
            Profile = new UserProfile
            {
                UserId = userId,
                FullName = "Demo User",
                Email = "demo.user@example.com",
                PhoneNumber = "+33102030405"
            },
            MarketingConsent = activePurposes.Contains("Marketing", StringComparer.OrdinalIgnoreCase),
            AnalyticsConsent = activePurposes.Contains("Analytics", StringComparer.OrdinalIgnoreCase)
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveConsent(string userId, bool marketingConsent, bool analyticsConsent, CancellationToken cancellationToken)
    {
        await _consentRepository.UpsertConsentAsync(userId, "Marketing", marketingConsent, cancellationToken);
        await _consentRepository.UpsertConsentAsync(userId, "Analytics", analyticsConsent, cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
