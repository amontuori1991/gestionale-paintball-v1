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

    // Helper: forza Kind=Unspecified (richiesto da colonne Postgres "date" e "timestamp without time zone")
    private static DateTime ToUnspecified(DateTime dt) => DateTime.SpecifyKind(dt, DateTimeKind.Unspecified);

    [HttpGet]
    public async Task<IActionResult> Archivio()
    {
        var list = await _db.Promozioni
            .OrderByDescending(p => p.DataScadenza)
            .ToListAsync();
        return View(list);
    }

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public IActionResult Crea()
    {
        return View(new Promozione
        {
            DataScadenza = DateTime.UtcNow.Date.AddMonths(1), // default UI
            PromotionType = "Instagram"
        });
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Crea([Bind("Nome,Alias,Descrizione,DataScadenza,PromotionType,EditionYear")] Promozione model)
    {
        if (!ModelState.IsValid)
            return View(model);

        // Normalizza alias
        if (!string.IsNullOrWhiteSpace(model.Alias))
            model.Alias = model.Alias.Trim().ToLowerInvariant();

        // Forza i DateTime per le colonne Postgres:
        // datascadenza = DATE (usa solo la data, Kind=Unspecified)
        model.DataScadenza = ToUnspecified(model.DataScadenza.Date);
        // datacreazione = timestamp without time zone (Kind=Unspecified)
        model.DataCreazione = ToUnspecified(DateTime.UtcNow);

        _db.Promozioni.Add(model);

        try
        {
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Archivio));
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("promozioni_alias_key") == true)
        {
            ModelState.AddModelError(nameof(model.Alias), "Alias già in uso. Scegline un altro.");
            return View(model);
        }
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> Visualizza(string alias)
    {
        if (string.IsNullOrWhiteSpace(alias)) return NotFound();

        var promo = await _db.Promozioni
            .FirstOrDefaultAsync(p => p.Alias.ToLower() == alias.ToLower());

        if (promo == null) return NotFound();

        return View(promo);
    }

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var promo = await _db.Promozioni.FirstOrDefaultAsync(p => p.Id == id);
        if (promo == null) return NotFound();
        return View(promo);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Id,Nome,Alias,Descrizione,DataScadenza,PromotionType,EditionYear,DataCreazione")] Promozione model)
    {
        if (id != model.Id) return NotFound();
        if (!ModelState.IsValid) return View(model);

        // Normalizza alias
        if (!string.IsNullOrWhiteSpace(model.Alias))
            model.Alias = model.Alias.Trim().ToLowerInvariant();

        // Forza Kind=Unspecified (il form <input type="date"> arriva Unspecified ma assicuriamo lo stato)
        model.DataScadenza = ToUnspecified(model.DataScadenza.Date);
        model.DataCreazione = ToUnspecified(model.DataCreazione);

        try
        {
            _db.Update(model);
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Archivio));
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("promozioni_alias_key") == true)
        {
            ModelState.AddModelError(nameof(model.Alias), "Alias già in uso. Scegline un altro.");
            return View(model);
        }
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var promo = await _db.Promozioni.FindAsync(id);
        if (promo == null) return NotFound();

        _db.Promozioni.Remove(promo);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Archivio));
    }
    // In PromozioniController

    [HttpGet]
    public async Task<IActionResult> Poster(string alias)
    {
        if (string.IsNullOrWhiteSpace(alias)) return NotFound();

        var promo = await _db.Promozioni
            .FirstOrDefaultAsync(p => p.Alias.ToLower() == alias.ToLower());
        if (promo == null) return NotFound();

        // URL assoluto alla pagina /promo/{alias}
        var urlAssoluto = Url.Action(
            action: "RichiediDaAlias",
            controller: "CodiciPromo",
            values: new { alias = promo.Alias },
            protocol: Request.Scheme,
            host: Request.Host.Value
        );

        // QR (PNG base64) dal link assoluto
        using var qrGen = new QRCoder.QRCodeGenerator();
        var qrData = qrGen.CreateQrCode(urlAssoluto, QRCoder.QRCodeGenerator.ECCLevel.Q);
        var qrPng = new QRCoder.PngByteQRCode(qrData);
        var qrBytes = qrPng.GetGraphic(20);

        ViewBag.QRBase64 = Convert.ToBase64String(qrBytes);
        ViewBag.UrlAssoluto = urlAssoluto;
        ViewBag.LogoUrl = Url.Content("~/img/logo.gif");

        // Tema colori (puoi cambiare gli esadecimali qui)
        var isEvento = string.Equals(promo.PromotionType, "EventoRichiedeDati", StringComparison.OrdinalIgnoreCase);
        ViewBag.Accent = isEvento ? "#16a34a" : "#7c3aed"; // verde / viola
        ViewBag.AccentSoft = isEvento ? "#dcfce7" : "#ede9fe";
        ViewBag.AccentDark = isEvento ? "#065f46" : "#4c1d95";

        return View("Poster", promo);
    }


    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Duplica(int id)
    {
        var src = await _db.Promozioni.FirstOrDefaultAsync(p => p.Id == id);
        if (src == null) return NotFound();

        // 1) Calcolo edizione successiva
        int? nextEdition = src.EditionYear.HasValue ? src.EditionYear.Value + 1 : (int?)null;

        // 2) Nuova data scadenza = +1 anno mantenendo giorno/mese (fallback a AddYears)
        DateTime nuovaScadenza;
        try
        {
            nuovaScadenza = new DateTime(
                (src.DataScadenza.Year + 1),
                src.DataScadenza.Month,
                src.DataScadenza.Day);
        }
        catch
        {
            nuovaScadenza = src.DataScadenza.AddYears(1);
        }

        // Forza Kind=Unspecified e tronca l'ora
        var nuovaScadenzaUnspec = ToUnspecified(nuovaScadenza.Date);

        // 3) Nuovo alias...
        string newAliasBase = (src.Alias ?? "").Trim().ToLowerInvariant();
        string proposedAlias = NextAlias(newAliasBase, nextEdition ?? DateTime.UtcNow.Year + 1);

        // 4) Crea clone (anche DataCreazione Unspecified)
        var clone = new Promozione
        {
            Nome = src.Nome,
            Descrizione = src.Descrizione,
            DataScadenza = nuovaScadenzaUnspec,
            DataCreazione = ToUnspecified(DateTime.UtcNow),
            PromotionType = src.PromotionType,
            EditionYear = nextEdition,
            Alias = proposedAlias
        };

        _db.Promozioni.Add(clone);

        try
        {
            await _db.SaveChangesAsync();
            TempData["Msg"] = $"Promozione duplicata: {clone.Nome} ({clone.Alias}).";
            return RedirectToAction(nameof(Archivio));
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("promozioni_alias_key") == true)
        {
            // Collisione alias: prova con suffisso -2, -3, ...
            for (int i = 2; i <= 10; i++)
            {
                clone.Alias = $"{proposedAlias}-{i}";
                try
                {
                    await _db.SaveChangesAsync();
                    TempData["Msg"] = $"Promozione duplicata: {clone.Nome} ({clone.Alias}).";
                    return RedirectToAction(nameof(Archivio));
                }
                catch (DbUpdateException ex2) when (ex2.InnerException?.Message.Contains("promozioni_alias_key") == true)
                {
                    // continua a provare
                }
            }

            ModelState.AddModelError("", "Impossibile generare un alias univoco per la copia. Modifica manualmente l’alias.");
            var list = await _db.Promozioni.OrderByDescending(p => p.DataScadenza).ToListAsync();
            return View(nameof(Archivio), list);
        }

        // Helper locale
        static string NextAlias(string current, int nextYear)
        {
            if (string.IsNullOrWhiteSpace(current))
                return $"promo-{nextYear}";

            // Se termina con numeri, incrementali: es. sportpiazza2025 -> sportpiazza2026
            var i = current.Length - 1;
            while (i >= 0 && char.IsDigit(current[i])) i--;
            var prefix = current.Substring(0, i + 1);
            var digits = current.Substring(i + 1);

            if (digits.Length > 0 && int.TryParse(digits, out var n))
                return $"{prefix}{n + 1}";

            // Altrimenti, se non termina con l'anno, aggiungi nextYear
            if (!current.EndsWith(nextYear.ToString()))
                return $"{current}{nextYear}";

            // Fallback
            return $"{current}-{nextYear}";
        }
    }
}
