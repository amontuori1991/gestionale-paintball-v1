using Full_Metal_Paintball_Carmagnola.Helpers;
using Full_Metal_Paintball_Carmagnola.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

[Authorize]
public class DashboardController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly TesseramentoDbContext _db;

    public DashboardController(UserManager<ApplicationUser> userManager, TesseramentoDbContext db)
    {
        _userManager = userManager;
        _db = db;
    }

    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return Challenge();

        ViewBag.FullName = user.FirstName + " " + user.LastName;

        // Recupera il ruolo dell'utente
        var ruoli = await _userManager.GetRolesAsync(user);
        var ruolo = ruoli.FirstOrDefault() ?? "Nessuno";

        List<string> permessiVisibili;

        // Admin può vedere tutto
        if (ruolo == "Admin")
        {
            permessiVisibili = Features.AllFeatures.ToList();
        }

        else
        {
            // Recupera solo i permessi consentiti
            permessiVisibili = await _db.RolePermissions
                .Where(p => p.RoleName == ruolo && p.IsAllowed)
                .Select(p => p.FeatureName)
                .ToListAsync();
        }

        var orderedFeatures = ApplyDashboardOrder(permessiVisibili, ParseFeatureList(user.DashboardFeatureOrder));
        var hiddenFeatures = ParseFeatureSet(user.DashboardHiddenFeatures);
        var visibleDashboardFeatures = orderedFeatures
            .Where(feature => !hiddenFeatures.Contains(feature))
            .ToList();

        ViewBag.PermessiVisibili = orderedFeatures;
        ViewBag.DashboardFeaturesVisibili = visibleDashboardFeatures;

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SalvaPreferenzeDashboard(List<string>? visibleFeatures, List<string>? featureOrder)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return Challenge();

        var ruoli = await _userManager.GetRolesAsync(user);
        var ruolo = ruoli.FirstOrDefault() ?? "Nessuno";

        List<string> permessiVisibili;
        if (ruolo == "Admin")
        {
            permessiVisibili = Features.AllFeatures.ToList();
        }
        else
        {
            permessiVisibili = await _db.RolePermissions
                .Where(p => p.RoleName == ruolo && p.IsAllowed)
                .Select(p => p.FeatureName)
                .ToListAsync();
        }

        var orderedFeatures = ApplyDashboardOrder(permessiVisibili, featureOrder ?? new List<string>());

        var selectedFeatures = (visibleFeatures ?? new List<string>())
            .Where(feature => permessiVisibili.Contains(feature))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var hiddenFeatures = orderedFeatures
            .Where(feature => !selectedFeatures.Contains(feature))
            .ToList();

        user.DashboardHiddenFeatures = JsonSerializer.Serialize(hiddenFeatures);
        user.DashboardFeatureOrder = JsonSerializer.Serialize(orderedFeatures);
        await _userManager.UpdateAsync(user);

        TempData["DashboardMessage"] = "Preferenze dashboard salvate.";
        return RedirectToAction(nameof(Index));
    }

    private static HashSet<string> ParseFeatureSet(string? featuresJson)
    {
        return ParseFeatureList(featuresJson).ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static List<string> ParseFeatureList(string? featuresJson)
    {
        if (string.IsNullOrWhiteSpace(featuresJson))
            return new List<string>();

        try
        {
            return JsonSerializer.Deserialize<List<string>>(featuresJson)?
                .Where(feature => !string.IsNullOrWhiteSpace(feature))
                .Select(feature => feature.Trim())
                .ToList()
                ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    private static List<string> ApplyDashboardOrder(List<string> allowedFeatures, List<string> requestedOrder)
    {
        var allowedSet = allowedFeatures.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var orderedFeatures = requestedOrder
            .Where(feature => allowedSet.Contains(feature))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        orderedFeatures.AddRange(allowedFeatures.Where(feature =>
            !orderedFeatures.Contains(feature, StringComparer.OrdinalIgnoreCase)));

        return orderedFeatures;
    }
}
