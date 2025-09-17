using System;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Full_Metal_Paintball_Carmagnola.Models;
using Full_Metal_Paintball_Carmagnola.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QRCoder;

[AllowAnonymous]
public class CodiciPromoController : Controller
{
    private readonly TesseramentoDbContext _db;

    public CodiciPromoController(TesseramentoDbContext db)
    {
        _db = db;
    }

    // ========== Helpers ==========
    private string GeneraCodice()
    {
        using var rng = RandomNumberGenerator.Create();
        var bytes = new byte[6];
        rng.GetBytes(bytes);
        return BitConverter.ToString(bytes).Replace("-", "").Substring(0, 8).ToUpper();
    }

    private static bool IsValidEmail(string email)
    {
        try { _ = new MailAddress(email); return true; }
        catch { return false; }
    }

    private static byte[] GenerateQrPngBytes(string testo)
    {
        var generator = new QRCodeGenerator();
        var data = generator.CreateQrCode(testo, QRCodeGenerator.ECCLevel.Q);
        var qr = new PngByteQRCode(data);
        return qr.GetGraphic(20);
    }

    private async Task SendVoucherEmailAsync(string toEmail, CodicePromozionale cp, Promozione promo, byte[] pngBytes)
    {
        // 🔧 Legge da "SmtpSettings" (come nel tuo appsettings.json)
        var cfg = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
        var host = cfg["SmtpSettings:Host"];
        var portText = cfg["SmtpSettings:Port"];
        var enableSslStr = cfg["SmtpSettings:EnableSsl"];
        var user = cfg["SmtpSettings:User"];
        var pass = cfg["SmtpSettings:Password"];

        // Se non hai un "From" separato, usa l'utente come mittente
        var fromAddr = user;
        var fromName = "Full Metal Paintball";

        int port = 587;
        int.TryParse(portText, out port);
        bool enableSsl = string.Equals(enableSslStr, "true", StringComparison.OrdinalIgnoreCase);

        using var msg = new MailMessage();
        msg.From = new MailAddress(fromAddr!, fromName);
        msg.To.Add(toEmail);
        msg.Subject = $"Il tuo buono — {promo.Nome}";
        msg.IsBodyHtml = true;

        var scadenza = cp.DataScadenza.ToUniversalTime().ToString("dd/MM/yyyy");
        // Corpo HTML
        var diritto = string.IsNullOrWhiteSpace(promo.CosaDaDiritto) ? "" :
            $"<p><strong>Il buono dà diritto a:</strong> {WebUtility.HtmlEncode(promo.CosaDaDiritto)}</p>";

        msg.Body = $@"
    <p>Ciao {WebUtility.HtmlEncode(cp.Nome ?? "")} {WebUtility.HtmlEncode(cp.Cognome ?? "")},</p>
    <p>ecco il tuo buono per <strong>{WebUtility.HtmlEncode(promo.Nome ?? "")}</strong>.</p>
    {diritto}
    <p><strong>Codice:</strong> {WebUtility.HtmlEncode(cp.Codice)}<br/>
       <strong>Valido fino al:</strong> {scadenza}</p>
    <p>In allegato trovi il QR del buono. Mostralo alla cassa.</p>
    <p>Grazie!<br/>Full Metal Paintball</p>";


        // Allegato PNG del QR
        using var ms = new MemoryStream(pngBytes);
        var attachment = new Attachment(ms, $"buono-{cp.Codice}.png", "image/png");
        msg.Attachments.Add(attachment);

        using var client = new SmtpClient(host, port)
        {
            EnableSsl = enableSsl,                                 // Gmail 587 = STARTTLS
            Credentials = new NetworkCredential(user, pass)
        };

        // Invio
        await client.SendMailAsync(msg);
    }

    private string GeneraQRCodeBase64(CodicePromozionale codice)
    {
        var qrText = $"Buono sconto Full Metal Paintball\nCodice: {codice.Codice}\nValido fino al {codice.DataScadenza:dd/MM/yyyy}";
        var generator = new QRCodeGenerator();
        var data = generator.CreateQrCode(qrText, QRCodeGenerator.ECCLevel.Q);
        var qrCode = new PngByteQRCode(data);
        var bytes = qrCode.GetGraphic(20);
        return $"data:image/png;base64,{Convert.ToBase64String(bytes)}";
    }

    private string GeneraQRCode(string codice)
    {
        var qrGenerator = new QRCodeGenerator();
        var qrCodeData = qrGenerator.CreateQrCode(codice, QRCodeGenerator.ECCLevel.Q);
        var qrCode = new PngByteQRCode(qrCodeData);
        var qrCodeBytes = qrCode.GetGraphic(20);
        return Convert.ToBase64String(qrCodeBytes);
    }

    // ========== Public pages ==========
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

        var esiste = await _db.codicipromozionali
            .AnyAsync(c => c.InstagramAccount == instagramAccount && string.IsNullOrEmpty(c.Alias));

        if (esiste)
        {
            ViewBag.GiaRichiesto = true;
            return View("CodiceEsistente");
        }

        var codice = GeneraCodice();
        var oggiUtc = DateTime.UtcNow;

        var nuovo = new CodicePromozionale
        {
            InstagramAccount = instagramAccount,
            Codice = codice,
            DataCreazione = oggiUtc,              // timestamptz
            DataScadenza = oggiUtc.AddMonths(12), // timestamptz
            Utilizzato = false
        };

        _db.codicipromozionali.Add(nuovo);
        await _db.SaveChangesAsync();

        ViewBag.QRCodeImage = GeneraQRCodeBase64(nuovo);
        ViewBag.PromotionType = "Instagram";
        return View("CodiceGenerato", nuovo);
    }

    [HttpGet("/promo")]
    public IActionResult GeneraCodiceView()
    {
        return View("GeneraCodice");
    }

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
            DataCreazione = DateTime.UtcNow,              // timestamptz
            DataScadenza = DateTime.UtcNow.AddMonths(12), // timestamptz
            Utilizzato = false
        };

        _db.codicipromozionali.Add(promo);
        await _db.SaveChangesAsync();

        var qrBytes = GenerateQrPngBytes(codice);
        ViewBag.QRCode = Convert.ToBase64String(qrBytes);
        ViewBag.PromotionType = "Instagram";

        return View("CodiceGenerato", promo);
    }

    [HttpGet("/promo/{alias}")]
    public async Task<IActionResult> RichiediDaAlias(string alias)
    {
        var promo = await _db.Promozioni.FirstOrDefaultAsync(p => p.Alias.ToLower() == alias.ToLower());
        if (promo == null || promo.DataScadenza < DateTime.UtcNow.Date)
            return View("PromoNonValida");

        promo.PromotionType ??= "Instagram";
        return View("GeneraCodice", promo);
    }

    [AllowAnonymous]
    [HttpPost]
    public async Task<IActionResult> GeneraDaAlias(
        string alias,
        string? instagramHandle,
        string? nome,
        string? cognome,
        DateTime? dataNascita,
        string? comuneNascita,
        string? comuneResidenza,
        string? email)
    {
        // --- recupero promo ---
        if (string.IsNullOrWhiteSpace(alias))
            return View("PromoNonValida");

        var promo = await _db.Promozioni.FirstOrDefaultAsync(p => p.Alias.ToLower() == alias.ToLower());
        if (promo == null || promo.DataScadenza < DateTime.UtcNow.Date)
            return View("PromoNonValida");

        bool requiresPersonal = string.Equals(promo.PromotionType ?? "Instagram", "EventoRichiedeDati", StringComparison.OrdinalIgnoreCase);

        // scadenza del codice = fine del giorno di scadenza promo, UTC
        var nowUtc = DateTime.UtcNow;
        var codiceScadenzaUtc = new DateTime(
            promo.DataScadenza.Year, promo.DataScadenza.Month, promo.DataScadenza.Day, 23, 59, 59, DateTimeKind.Utc);

        if (requiresPersonal)
        {
            // --- IDEMPOTENZA su persona (Nome+Cognome+DataNascita+Email) per QUELLA promo ---
            const bool RESEND_EMAIL_ON_DUPLICATE = true;

            // validazione
            if (string.IsNullOrWhiteSpace(nome) ||
                string.IsNullOrWhiteSpace(cognome) ||
                !dataNascita.HasValue ||
                string.IsNullOrWhiteSpace(comuneNascita) ||
                string.IsNullOrWhiteSpace(comuneResidenza) ||
                string.IsNullOrWhiteSpace(email) || !IsValidEmail(email))
            {
                TempData["Errore"] = "Compila tutti i campi richiesti e una email valida.";
                return RedirectToAction("RichiediDaAlias", new { alias });
            }

            // normalizza
            nome = nome!.Trim();
            cognome = cognome!.Trim();
            comuneNascita = comuneNascita!.Trim();
            comuneResidenza = comuneResidenza!.Trim();
            email = email!.Trim();

            var aliasLower = promo.Alias.ToLower();
            var nomeLower = nome.ToLower();
            var cognomeLower = cognome.ToLower();
            var emailLower = email.ToLower();
            var dataOnly = dataNascita.Value.Date;

            // cerca se esiste già
            var esistente = await _db.codicipromozionali
                .Where(c => c.Alias != null && c.Nome != null && c.Cognome != null && c.Email != null && c.DataNascita != null)
                .FirstOrDefaultAsync(c =>
                    c.Alias!.ToLower() == aliasLower &&
                    c.Nome!.ToLower() == nomeLower &&
                    c.Cognome!.ToLower() == cognomeLower &&
                    c.Email!.ToLower() == emailLower &&
                    c.DataNascita == dataOnly);

            if (esistente != null)
            {
                // ripresento lo stesso codice
                var qrBytes = GenerateQrPngBytes(esistente.Codice);
                ViewBag.QRCode = Convert.ToBase64String(qrBytes);
                ViewBag.DescrizionePromo = promo.Descrizione;
                ViewBag.PromotionType = promo.PromotionType;
                ViewBag.BenefitPromo = promo.CosaDaDiritto;
                TempData["Info"] = "Hai già un buono attivo per questa promozione: ecco il tuo codice.";

                if (RESEND_EMAIL_ON_DUPLICATE)
                {
                    try
                    {
                        await SendVoucherEmailAsync(email, esistente, promo, qrBytes);
                        TempData["EmailEsito"] = "Ti abbiamo reinviato il buono via email.";
                    }
                    catch
                    {
                        TempData["EmailEsito"] = "Codice già presente. Invio email non riuscito.";
                    }
                }

                return View("CodiceGenerato", esistente);
            }

            // genera nuovo
            var codice = GeneraCodice();
            var promoCode = new CodicePromozionale
            {
                InstagramAccount = null,
                Codice = codice,
                DataCreazione = nowUtc,           // timestamptz
                DataScadenza = codiceScadenzaUtc, // timestamptz
                Utilizzato = false,
                Alias = promo.Alias,

                Nome = nome,
                Cognome = cognome,
                DataNascita = dataOnly,           // date
                ComuneNascita = comuneNascita,
                ComuneResidenza = comuneResidenza,
                Email = email
            };

            _db.codicipromozionali.Add(promoCode);
            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // race condition possibile: recupera e mostra
                esistente = await _db.codicipromozionali.FirstOrDefaultAsync(c =>
                    c.Alias!.ToLower() == aliasLower &&
                    c.Nome!.ToLower() == nomeLower &&
                    c.Cognome!.ToLower() == cognomeLower &&
                    c.Email!.ToLower() == emailLower &&
                    c.DataNascita == dataOnly);

                if (esistente != null)
                {
                    var qrDup = GenerateQrPngBytes(esistente.Codice);
                    ViewBag.QRCode = Convert.ToBase64String(qrDup);
                    ViewBag.DescrizionePromo = promo.Descrizione;
                    ViewBag.PromotionType = promo.PromotionType;
                    TempData["Info"] = "Hai già un buono attivo per questa promozione: ecco il tuo codice.";
                    return View("CodiceGenerato", esistente);
                }
                throw;
            }

            var qrBytesNew = GenerateQrPngBytes(codice);
            ViewBag.QRCode = Convert.ToBase64String(qrBytesNew);
            ViewBag.DescrizionePromo = promo.Descrizione;
            ViewBag.PromotionType = promo.PromotionType;
            ViewBag.BenefitPromo = promo.CosaDaDiritto;

            try
            {
                await SendVoucherEmailAsync(email, promoCode, promo, qrBytesNew);
                TempData["EmailEsito"] = "Ti abbiamo inviato il buono via email.";
            }
            catch
            {
                TempData["EmailEsito"] = "Codice generato. Invio email non riuscito.";
            }

            return View("CodiceGenerato", promoCode);
        }
        else
        {
            // --- ramo Instagram ---
            if (string.IsNullOrWhiteSpace(instagramHandle))
            {
                TempData["Errore"] = "Inserisci il tuo account Instagram.";
                return RedirectToAction("RichiediDaAlias", new { alias });
            }

            instagramHandle = instagramHandle.Trim().ToLower();

            var esisteIG = await _db.codicipromozionali.AnyAsync(c =>
                c.Alias != null && c.Alias.ToLower() == promo.Alias.ToLower() &&
                c.InstagramAccount != null && c.InstagramAccount.ToLower() == instagramHandle);

            if (esisteIG)
            {
                TempData["Errore"] = "Hai già richiesto un codice per questa promozione.";
                return RedirectToAction("RichiediDaAlias", new { alias });
            }

            var codice = GeneraCodice();
            var promoCode = new CodicePromozionale
            {
                InstagramAccount = instagramHandle,
                Codice = codice,
                DataCreazione = nowUtc,           // timestamptz
                DataScadenza = codiceScadenzaUtc, // timestamptz
                Utilizzato = false,
                Alias = promo.Alias
            };

            _db.codicipromozionali.Add(promoCode);
            await _db.SaveChangesAsync();

            var qrBytes = GenerateQrPngBytes(codice);
            ViewBag.QRCode = Convert.ToBase64String(qrBytes);
            ViewBag.DescrizionePromo = promo.Descrizione;
            ViewBag.PromotionType = promo.PromotionType;
            ViewBag.BenefitPromo = promo.CosaDaDiritto;
            return View("CodiceGenerato", promoCode);
        }

        // fallback teoricamente irraggiungibile, utile solo per il compilatore
        // (tutti i rami sopra già ritornano)
        // return RedirectToAction("RichiediDaAlias", new { alias });
    }


    // ========== Admin ==========
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> VerificaCodici()
    {
        var query = from c in _db.codicipromozionali
                    join p in _db.Promozioni on c.Alias equals p.Alias into gj
                    from p in gj.DefaultIfEmpty()
                    orderby c.DataCreazione descending
                    select new CodicePromozionaleViewModel
                    {
                        Id = c.Id,
                        Codice = c.Codice,
                        Alias = c.Alias,
                        PromotionType = p != null ? p.PromotionType : "Instagram",
                        PromozioneNome = p != null ? p.Nome : null,
                        EditionYear = p != null ? p.EditionYear : null,

                        InstagramHandle = c.InstagramAccount,

                        Nome = c.Nome,
                        Cognome = c.Cognome,
                        DataNascita = c.DataNascita,
                        ComuneNascita = c.ComuneNascita,
                        ComuneResidenza = c.ComuneResidenza,

                        DataCreazione = c.DataCreazione,
                        DataScadenza = c.DataScadenza,
                        Utilizzato = c.Utilizzato
                    };

        var codici = await query.ToListAsync();
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
        var codice = await _db.codicipromozionali.FirstOrDefaultAsync(c => c.Id == id);
        if (codice == null) return NotFound();

        Promozione? promo = null;

        if (!string.IsNullOrEmpty(codice.Alias))
            promo = await _db.Promozioni.FirstOrDefaultAsync(p => p.Alias.ToLower() == codice.Alias.ToLower());

        ViewBag.QRCode = GeneraQRCode(codice.Codice);
        ViewBag.DescrizionePromo = promo?.Descrizione ?? "Offerta promozionale";
        ViewBag.BenefitPromo = promo?.CosaDaDiritto;
        ViewBag.PromotionType = promo?.PromotionType ?? "Instagram";

        return View("CodiceGenerato", codice);
    }
}
