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

        var hiddenFeatures = ParseHiddenFeatures(user.DashboardHiddenFeatures);
        var visibleDashboardFeatures = permessiVisibili
            .Where(feature => !hiddenFeatures.Contains(feature))
            .ToList();

        ViewBag.PermessiVisibili = permessiVisibili;
        ViewBag.DashboardFeaturesVisibili = visibleDashboardFeatures;

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SalvaPreferenzeDashboard(List<string>? visibleFeatures)
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

        var selectedFeatures = (visibleFeatures ?? new List<string>())
            .Where(feature => permessiVisibili.Contains(feature))
            .ToHashSet();

        var hiddenFeatures = permessiVisibili
            .Where(feature => !selectedFeatures.Contains(feature))
            .ToList();

        user.DashboardHiddenFeatures = JsonSerializer.Serialize(hiddenFeatures);
        await _userManager.UpdateAsync(user);

        TempData["DashboardMessage"] = "Preferenze dashboard salvate.";
        return RedirectToAction(nameof(Index));
    }

    private static HashSet<string> ParseHiddenFeatures(string? hiddenFeaturesJson)
    {
        if (string.IsNullOrWhiteSpace(hiddenFeaturesJson))
            return new HashSet<string>();

        try
        {
            return JsonSerializer.Deserialize<List<string>>(hiddenFeaturesJson)?.ToHashSet()
                ?? new HashSet<string>();
        }
        catch
        {
            return new HashSet<string>();
        }
    }
}
