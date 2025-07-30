using System.Security.Cryptography;
using Full_Metal_Paintball_Carmagnola.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Full_Metal_Paintball_Carmagnola.Models.ViewModels;
using QRCoder;
using System;

[AllowAnonymous]
public class CodiciPromoController : Controller
{
    private readonly TesseramentoDbContext _db;

    public CodiciPromoController(TesseramentoDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public IActionResult RichiediCodice()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> RichiediCodice(string instagramAccount)
    {
        if (string.IsNullOrWhiteSpace(instagramAccount))
        {
            ViewBag.Errore = "Inserisci un nome account valido.";
            return View();
        }

        instagramAccount = instagramAccount.Trim().ToLower();

        var esiste = await _db.codicipromozionali.AnyAsync(c => c.InstagramAccount == instagramAccount);
        if (esiste)
        {
            ViewBag.GiaRichiesto = true;
            return View("CodiceEsistente");
        }

        var codice = GeneraCodice();
        var oggi = DateTime.UtcNow;

        var nuovo = new CodicePromozionale
        {
            InstagramAccount = instagramAccount,
            Codice = codice,
            DataCreazione = oggi,
            DataScadenza = oggi.AddMonths(12),
            Utilizzato = false
        };

        _db.codicipromozionali.Add(nuovo);
        await _db.SaveChangesAsync();

        ViewBag.QRCodeImage = GeneraQRCodeBase64(nuovo);

        return View("CodiceGenerato", nuovo);
    }

    private string GeneraCodice()
    {
        using var rng = RandomNumberGenerator.Create();
        var bytes = new byte[6];
        rng.GetBytes(bytes);
        return BitConverter.ToString(bytes).Replace("-", "").Substring(0, 8).ToUpper();
    }

    [AllowAnonymous]
    [HttpGet("/promo")]
    public IActionResult GeneraCodiceView()
    {
        return View("GeneraCodice");
    }

    [AllowAnonymous]
    [HttpPost("/promo")]
    public async Task<IActionResult> GeneraCodice(string instagramHandle)
    {
        if (string.IsNullOrWhiteSpace(instagramHandle))
        {
            TempData["Errore"] = "Inserisci il tuo account Instagram.";
            return RedirectToAction("GeneraCodiceView");
        }

        instagramHandle = instagramHandle.Trim().ToLower();

        var esiste = await _db.codicipromozionali
            .AnyAsync(c => c.InstagramAccount == instagramHandle);

        if (esiste)
        {
            TempData["Errore"] = "Questo account ha già ricevuto un codice.";
            return RedirectToAction("GeneraCodiceView");
        }

        var codice = GeneraCodice();

        var promo = new CodicePromozionale
        {
            InstagramAccount = instagramHandle,
            Codice = codice,
            DataCreazione = DateTime.UtcNow,
            DataScadenza = DateTime.UtcNow.AddMonths(12),
            Utilizzato = false
        };

        _db.codicipromozionali.Add(promo);
        await _db.SaveChangesAsync();

        // ✅ GENERAZIONE QR CODE
        using var qrGenerator = new QRCodeGenerator();
        var qrData = qrGenerator.CreateQrCode(codice, QRCodeGenerator.ECCLevel.Q);
        var qrCode = new PngByteQRCode(qrData);
        var qrBytes = qrCode.GetGraphic(20);
        var base64 = Convert.ToBase64String(qrBytes);
        ViewBag.QRCode = base64;

        return View("CodiceGenerato", promo);
    }


    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> VerificaCodici()
    {
        var codici = await _db.codicipromozionali
            .OrderByDescending(c => c.DataCreazione)
            .Select(c => new CodicePromozionaleViewModel
            {
                Id = c.Id,
                Codice = c.Codice,
                NomeInstagram = c.InstagramAccount,
                DataCreazione = c.DataCreazione,
                DataScadenza = c.DataScadenza,
                Utilizzato = c.Utilizzato
            }).ToListAsync();

        return View("VerificaCodici", codici);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> SegnaUtilizzato(int id, bool utilizzato)
    {
        var codice = await _db.codicipromozionali.FindAsync(id);
        if (codice == null) return NotFound();

        codice.Utilizzato = utilizzato;
        await _db.SaveChangesAsync();

        return Ok();
    }

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<IActionResult> VisualizzaBuono(int id)
    {
        var codice = await _db.codicipromozionali.FindAsync(id);
        if (codice == null) return NotFound();

        using var qrGenerator = new QRCodeGenerator();
        var qrData = qrGenerator.CreateQrCode(codice.Codice, QRCodeGenerator.ECCLevel.Q);
        var qrCode = new PngByteQRCode(qrData);
        var qrBytes = qrCode.GetGraphic(20);
        var base64 = Convert.ToBase64String(qrBytes);
        ViewBag.QRCode = base64;

        return View("CodiceGenerato", codice);
    }


    // --- Metodo QR code base64 ---
    private string GeneraQRCodeBase64(CodicePromozionale codice)
    {
        var qrText = $"Buono sconto Full Metal Paintball\nCodice: {codice.Codice}\nValido fino al {codice.DataScadenza:dd/MM/yyyy}";

        var generator = new QRCodeGenerator();
        var data = generator.CreateQrCode(qrText, QRCodeGenerator.ECCLevel.Q);
        var qrCode = new PngByteQRCode(data);
        var bytes = qrCode.GetGraphic(20);

        return $"data:image/png;base64,{Convert.ToBase64String(bytes)}";
    }
}
