using Full_Metal_Paintball_Carmagnola.Models;
using Full_Metal_Paintball_Carmagnola.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Full_Metal_Paintball_Carmagnola.Controllers;

[Authorize(Roles = "Admin")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class VolantiniController(CompanyProfileService profiles, FlyerRenderer renderer) : Controller
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewBag.Company = await profiles.GetAsync(ct);
        return View(new FlyerRequest());
    }

    [HttpPost, ValidateAntiForgeryToken, RequestSizeLimit(16384)]
    public async Task<IActionResult> Genera(FlyerRequest model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(new { message = "Controlla i campi obbligatori e la lunghezza dei testi." });
        var company = await profiles.GetAsync(ct);
        if (string.IsNullOrWhiteSpace(company.Name)) return BadRequest(new { message = "Compila prima il Profilo Azienda." });
        if (!await Gate.WaitAsync(TimeSpan.FromSeconds(5), ct)) return StatusCode(429, new { message = "Generazione in corso. Riprova tra poco." });
        try
        {
            var bytes = renderer.Render(model, company);
            if (model.Format == "preview") return File(bytes, "image/jpeg");
            return File(bytes, model.Format == "pdf" ? "application/pdf" : "image/jpeg", "Volantino-FullMetal." + model.Format);
        }
        catch (InvalidDataException e) { return BadRequest(new { message = e.Message }); }
        finally { Gate.Release(); }
    }
}
