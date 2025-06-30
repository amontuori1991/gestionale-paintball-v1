using Full_Metal_Paintball_Carmagnola.Helpers;
using Full_Metal_Paintball_Carmagnola.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
        ViewBag.FullName = user.FirstName + " " + user.LastName;

        // Recupera il ruolo dell'utente
        var ruoli = await _userManager.GetRolesAsync(user);
        var ruolo = ruoli.FirstOrDefault() ?? "Nessuno";

        // Admin può vedere tutto
        if (ruolo == "Admin")
        {
            ViewBag.PermessiVisibili = Features.AllFeatures.ToList();
        }

        else
        {
            // Recupera solo i permessi consentiti
            var permessi = await _db.RolePermissions
                .Where(p => p.RoleName == ruolo && p.IsAllowed)
                .Select(p => p.FeatureName)
                .ToListAsync();

            ViewBag.PermessiVisibili = permessi;
        }

        return View();
    }
}
