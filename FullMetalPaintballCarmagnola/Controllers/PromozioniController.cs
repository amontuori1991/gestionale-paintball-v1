using Microsoft.AspNetCore.Mvc;
using Full_Metal_Paintball_Carmagnola.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

[Authorize(Roles = "Admin")]
public class PromozioniController : Controller
{
    private readonly TesseramentoDbContext _db;

    public PromozioniController(TesseramentoDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Archivio()
    {
        var promozioni = await _db.Promozioni.OrderByDescending(p => p.DataCreazione).ToListAsync();
        return View(promozioni);
    }
    [Authorize(Roles = "Admin")]
    [HttpGet]
    public IActionResult Crea()
    {
        return View();
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<IActionResult> Crea(Promozione model)
    {
        if (!ModelState.IsValid)
            return View(model);

        // Check se esiste già un alias con lo stesso nome
        var aliasEsistente = await _db.Promozioni.AnyAsync(p => p.Alias == model.Alias);
        if (aliasEsistente)
        {
            ModelState.AddModelError("Alias", "Alias già esistente. Scegline un altro.");
            return View(model);
        }

        model.DataScadenza = DateTime.SpecifyKind(model.DataScadenza, DateTimeKind.Utc);
        model.DataCreazione = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);



        _db.Promozioni.Add(model);
        await _db.SaveChangesAsync();

        return RedirectToAction("Archivio");
    }
    [AllowAnonymous]
    [HttpGet]
    public IActionResult Visualizza(string alias)
    {
        if (string.IsNullOrWhiteSpace(alias))
            return NotFound();

        // Redirect alla pagina pubblica corretta della promozione
        return Redirect($"/promo/{alias}");
    }




}
