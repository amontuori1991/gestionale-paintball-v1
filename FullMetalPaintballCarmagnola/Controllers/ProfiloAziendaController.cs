using Full_Metal_Paintball_Carmagnola.Models;
using Full_Metal_Paintball_Carmagnola.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Full_Metal_Paintball_Carmagnola.Controllers;

[Authorize(Roles = "Admin")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ProfiloAziendaController(CompanyProfileService profiles) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct) => View(await profiles.GetAsync(ct));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(CompanyProfile model, CancellationToken ct)
    {
        model.Normalize();
        ModelState.Clear();
        if (!TryValidateModel(model)) return View(model);
        await profiles.SaveAsync(model, ct);
        TempData["CompanyProfileSaved"] = "Profilo azienda salvato.";
        return RedirectToAction(nameof(Index));
    }
}
