using System.Text.Json;
using System.Text.RegularExpressions;
using Full_Metal_Paintball_Carmagnola.Models;
using Full_Metal_Paintball_Carmagnola.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SkiaSharp;

namespace Full_Metal_Paintball_Carmagnola.Controllers;

[Authorize(Roles = "Admin,Staff"), Authorize(Policy = "Buoni regalo")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class BuoniRegaloController(TesseramentoDbContext db, CompanyProfileService profiles, GiftVoucherRenderer renderer) : Controller
{
    private const string SignatureKey = "GiftVoucherSignatureV1";
    private string Actor => User.Identity?.Name ?? "Operatore";

    public async Task<IActionResult> Index(string? q, string? stato, int page = 1)
    {
        var query = db.GiftVouchers.AsNoTracking();
        q = q?.Trim();
        if (!string.IsNullOrEmpty(q)) query = query.Where(v => v.Code.Contains(q.ToUpper()) || v.Payload.ToLower().Contains(q.ToLower()));
        var today = GiftVoucher.Today;
        query = stato switch
        {
            "Disponibile" => query.Where(v => !v.Cancelled && !v.Redeemed && v.Paid && v.ExpiresOn >= today && v.IssuedOn <= today),
            "Da pagare" => query.Where(v => !v.Cancelled && !v.Redeemed && !v.Paid && v.ExpiresOn >= today),
            "Scaduto" => query.Where(v => !v.Cancelled && !v.Redeemed && v.ExpiresOn < today),
            "Riscattato" => query.Where(v => v.Redeemed),
            "Annullato" => query.Where(v => v.Cancelled),
            _ => query
        };
        page = Math.Clamp(page, 1, 100000);
        ViewBag.Query = q; ViewBag.Stato = stato; ViewBag.Page = page;
        ViewBag.HasNext = await query.CountAsync() > page * 40;
        ViewBag.HasSignature = await db.AppSettings.AnyAsync(s => s.Key == SignatureKey);
        return View(await query.OrderByDescending(v => v.IssuedOn).ThenBy(v => v.Code).Skip((page - 1) * 40).Take(40).ToListAsync());
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Crea(Guid? id)
    {
        if (id == null) return View(new GiftVoucherInput());
        var row = await db.GiftVouchers.FindAsync(id.Value);
        if (row == null) return NotFound();
        if (row.Redeemed || row.Cancelled) return Conflict("Non puoi modificare un buono riscattato o annullato.");
        ViewBag.Id = row.Id; ViewBag.Version = row.Version;
        return View(row.Details);
    }

    [HttpPost, Authorize(Roles = "Admin"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Crea(GiftVoucherInput model, Guid? id, int version = 0)
    {
        // Accept decimal comma or point, but not ambiguous thousands separators.
        ModelState.Remove(nameof(model.Amount));
        var rawAmount = Request.Form[nameof(model.Amount)].ToString().Trim().Replace(',', '.');
        if (!decimal.TryParse(rawAmount, System.Globalization.NumberStyles.AllowDecimalPoint,
            System.Globalization.CultureInfo.InvariantCulture, out var amount) || amount <= 0 || amount > 100000 || decimal.Round(amount, 2) != amount)
            ModelState.AddModelError(nameof(model.Amount), "Inserisci un importo tra 0,01 e 100.000 euro, con massimo due decimali e senza separatore delle migliaia.");
        else model.Amount = amount;
        if (model.IssuedOn.Year < 2000 || model.IssuedOn > GiftVoucher.Today)
            ModelState.AddModelError(nameof(model.IssuedOn), "La data deve essere dal 2000 a oggi.");
        var company = await profiles.GetAsync();
        if (string.IsNullOrWhiteSpace(company.Name)) ModelState.AddModelError("", "Compila prima il Profilo Azienda.");
        var signature = await db.AppSettings.Where(s => s.Key == SignatureKey).Select(s => s.Value).SingleOrDefaultAsync();
        if (signature == null) ModelState.AddModelError("", "Carica prima la firma dalla pagina Buoni regalo.");
        if (!ModelState.IsValid) { ViewBag.Id = id; ViewBag.Version = version; return View(model); }
        GiftVoucher row;
        if (id.HasValue)
        {
            row = await db.GiftVouchers.FindAsync(id.Value) ?? throw new BadHttpRequestException("Buono non trovato.");
            if (row.Version != version || row.Redeemed || row.Cancelled) return Conflict("Il buono e' cambiato. Riapri la scheda.");
            if (row.Paid && row.Details.Amount != model.Amount)
            {
                ModelState.AddModelError(nameof(model.Amount), "Il valore di un buono pagato non puo essere modificato. Annulla il buono ed emettine uno nuovo.");
                ViewBag.Id = id; ViewBag.Version = version;
                return View(model);
            }
            row.Record(Actor, "Modifica", "Dati precedenti: " + row.Payload);
        }
        else
        {
            row = new(); db.GiftVouchers.Add(row);
            row.CompanyJson = JsonSerializer.Serialize(company);
            row.Record(Actor, "Emissione");
        }
        if (model.Type == "Kids") model.Unlimited = true;
        row.Payload = JsonSerializer.Serialize(model);
        row.IssuedOn = model.IssuedOn;
        row.ExpiresOn = model.IssuedOn.AddYears(1);
        try { renderer.Render(row, Convert.FromBase64String(signature!), "preview"); }
        catch (InvalidDataException)
        {
            ModelState.AddModelError("", "Testi troppo lunghi per il buono: accorcia i testi o i dati del Profilo Azienda.");
            ViewBag.Id = id; ViewBag.Version = version;
            return View(model);
        }
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException) { return Conflict("Modifica contemporanea: ricarica la scheda."); }
        return RedirectToAction(nameof(Dettaglio), new { id = row.Id });
    }

    public async Task<IActionResult> Dettaglio(Guid id)
    {
        var row = await db.GiftVouchers.AsNoTracking().SingleOrDefaultAsync(v => v.Id == id);
        if (row == null) return NotFound();
        var m = row.Details;
        var phone = Regex.Replace(m.Phone ?? "", "[^0-9]", "");
        if (phone.Length > 0)
        {
            var message = $"Ciao {m.Buyer}, ecco le coordinate per il pagamento del buono regalo.\n\nIban paintball\nIT82Q0883330262000160100706\n\nBIC/SWIFT\nCCRT IT 2T CSS\n\nIntestato a: ASSOCIAZIONE FULL METAL PAINTBALL CARMAGNOLA\n\nImporto: {m.Amount:0.00} euro\nCausale: Buono regalo {row.Code}\n\nMi alleghi qui la ricevuta del pagamento appena eseguito? Il buono sara' attivato dopo la verifica del pagamento. Grazie!";
            ViewBag.PaymentUrl = "https://wa.me/" + phone + "?text=" + Uri.EscapeDataString(message);
        }
        return View(row);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Stato(Guid id, int version, string operation, string? reason)
    {
        if (reason?.Length > 500) return BadRequest("Motivazione troppo lunga (massimo 500 caratteri).");
        var row = await db.GiftVouchers.FindAsync(id);
        if (row == null) return NotFound();
        if (row.Version != version) { TempData["VoucherMessage"] = "Il buono e' stato aggiornato da un altro operatore. Controlla lo stato."; return RedirectToAction(nameof(Dettaglio), new { id }); }
        var error = row.ChangeState(operation, User.IsInRole("Admin"), Actor, reason);
        if (error == null)
        {
            try { await db.SaveChangesAsync(); }
            catch (DbUpdateConcurrencyException) { error = "Operazione gia' effettuata o modifica contemporanea. Controlla lo stato aggiornato."; }
        }
        TempData["VoucherMessage"] = error ?? "Operazione registrata.";
        return RedirectToAction(nameof(Dettaglio), new { id });
    }

    [HttpPost, Authorize(Roles = "Admin"), ValidateAntiForgeryToken, RequestSizeLimit(2200000)]
    public async Task<IActionResult> Firma(IFormFile? file)
    {
        if (file == null || file.Length == 0 || file.Length > 2000000) return BadRequest("Carica una firma PNG o JPG entro 2 MB.");
        using var input = file.OpenReadStream();
        using var memory = new MemoryStream(); await input.CopyToAsync(memory);
        using var data = SKData.CreateCopy(memory.ToArray());
        using var codec = SKCodec.Create(data);
        if (codec == null || codec.EncodedFormat is not (SKEncodedImageFormat.Jpeg or SKEncodedImageFormat.Png) || codec.Info.Width * (long)codec.Info.Height > 12000000)
            return BadRequest("Usa un'immagine PNG o JPG valida, massimo 12 megapixel.");
        using var bitmap = SKBitmap.Decode(codec);
        if (bitmap == null) return BadRequest("Immagine non leggibile.");
        using var image = SKImage.FromBitmap(bitmap);
        using var png = image.Encode(SKEncodedImageFormat.Png, 100);
        var value = Convert.ToBase64String(png.ToArray());
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"AppSettings\" (\"Key\", \"Value\") VALUES ({SignatureKey}, {value}) ON CONFLICT (\"Key\") DO UPDATE SET \"Value\" = EXCLUDED.\"Value\"");
        TempData["VoucherMessage"] = "Firma salvata nell'area protetta.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Esporta(Guid id, string format = "preview")
    {
        if (format is not ("preview" or "pdf" or "jpg")) return BadRequest();
        var row = await db.GiftVouchers.AsNoTracking().SingleOrDefaultAsync(v => v.Id == id);
        if (row == null) return NotFound();
        var signature = await db.AppSettings.Where(s => s.Key == SignatureKey).Select(s => s.Value).SingleOrDefaultAsync();
        if (signature == null) return BadRequest("Firma non configurata.");
        try
        {
            var bytes = renderer.Render(row, Convert.FromBase64String(signature), format);
            return format == "preview" ? File(bytes, "image/jpeg") : File(bytes, format == "pdf" ? "application/pdf" : "image/jpeg", row.Code + "." + format);
        }
        catch (InvalidDataException) { return BadRequest("Testi troppo lunghi per il buono: accorcia la dedica o i dati di contatto."); }
    }
}
