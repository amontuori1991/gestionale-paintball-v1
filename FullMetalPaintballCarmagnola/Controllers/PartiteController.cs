using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
// using DocumentFormat.OpenXml.InkML; // non serve qui, puoi rimuoverlo
using Full_Metal_Paintball_Carmagnola.Models;
using Full_Metal_Paintball_Carmagnola.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Full_Metal_Paintball_Carmagnola.Controllers
{
    [Authorize(Policy = "Prenotazioni")]
    public class PartiteController : Controller
    {
        private readonly TesseramentoDbContext _dbContext;
        private readonly IConfiguration _configuration;
        private readonly IEmailService _emailService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _env;
        private readonly PricingCatalogService _pricingCatalogService;
        private readonly StaffRegistryService _staffRegistryService;
        private readonly WeatherForecastService _weatherForecastService;

        private static readonly Regex NonDigitRegex = new(@"[^\d]", RegexOptions.Compiled);
        private static readonly string[] KnownPhonePrefixes =
        {
            "420", "421", "423", "386", "385", "387", "381", "380", "351", "352", "353", "354", "355", "356", "357", "358", "359",
            "212", "213", "216", "218", "220", "221", "222", "223", "224", "225", "226", "227", "228", "229",
            "230", "231", "232", "233", "234", "235", "236", "237", "238", "239",
            "240", "241", "242", "243", "244", "245", "246", "247", "248", "249",
            "250", "251", "252", "253", "254", "255", "256", "257", "258", "260", "261", "262", "263", "264", "265", "266", "267", "268", "269",
            "297", "298", "299", "376", "377", "378", "379", "590", "591", "592", "593", "594", "595", "596", "597", "598", "599",
            "39", "33", "34", "41", "44", "49", "31", "32", "43", "45", "46", "47", "48", "36", "40", "30", "37", "38",
            "1", "7"
        };

        public PartiteController(
            TesseramentoDbContext dbContext,
            IConfiguration configuration,
            IEmailService emailService,
            UserManager<ApplicationUser> userManager,
            IWebHostEnvironment env,
            PricingCatalogService pricingCatalogService,
            StaffRegistryService staffRegistryService,
            WeatherForecastService weatherForecastService)
        {
            _dbContext = dbContext;
            _configuration = configuration;
            _emailService = emailService;
            _userManager = userManager;
            _env = env;
            _pricingCatalogService = pricingCatalogService;
            _staffRegistryService = staffRegistryService;
            _weatherForecastService = weatherForecastService;
        }

        private async Task SendNotificationToAllUsers(string subject, string messageHtml)
        {
            var users = await _userManager.Users.Where(u => u.IsApproved).ToListAsync();
            var adminEmails = _configuration.GetSection("AdminNotifications").Get<string[]>();

            foreach (var user in users)
            {
                if (!string.IsNullOrEmpty(user.Email))
                {
                    try { await _emailService.SendEmailAsync(user.Email, subject, messageHtml); }
                    catch (Exception ex) { Console.WriteLine($"Errore nell'invio email a {user.Email}: {ex.Message}"); }
                }
            }

            if (adminEmails != null && adminEmails.Any())
            {
                foreach (var adminEmail in adminEmails)
                {
                    if (!string.IsNullOrEmpty(adminEmail))
                    {
                        try { await _emailService.SendEmailAsync(adminEmail, subject, messageHtml); }
                        catch (Exception ex) { Console.WriteLine($"Errore nell'invio email all'admin {adminEmail}: {ex.Message}"); }
                    }
                }
            }
        }

        private static void NormalizzaContattoRiferimento(Partita partita)
        {
            partita.NomeRiferimento = string.IsNullOrWhiteSpace(partita.NomeRiferimento)
                ? null
                : partita.NomeRiferimento.Trim();

            var prefisso = (partita.PrefissoTelefonoRiferimento ?? string.Empty).Trim();
            var numeroOriginale = (partita.TelefonoRiferimento ?? string.Empty).Trim();
            var numeroCompatto = numeroOriginale.Replace(" ", string.Empty).Replace("-", string.Empty).Replace(".", string.Empty).Replace("/", string.Empty);

            if (numeroCompatto.StartsWith("00", StringComparison.Ordinal))
            {
                var digits = numeroCompatto[2..];
                if (TrySplitInternationalPhone(digits, out var parsedPrefix, out var parsedNumber))
                {
                    prefisso = "+" + parsedPrefix;
                    numeroCompatto = parsedNumber;
                }
            }
            else if (numeroCompatto.StartsWith("+", StringComparison.Ordinal))
            {
                var digits = NonDigitRegex.Replace(numeroCompatto, string.Empty);
                if (TrySplitInternationalPhone(digits, out var parsedPrefix, out var parsedNumber))
                {
                    prefisso = "+" + parsedPrefix;
                    numeroCompatto = parsedNumber;
                }
            }

            var prefissoDigits = NonDigitRegex.Replace(prefisso, string.Empty);
            partita.PrefissoTelefonoRiferimento = string.IsNullOrWhiteSpace(prefissoDigits)
                ? null
                : "+" + prefissoDigits;

            partita.TelefonoRiferimento = string.IsNullOrWhiteSpace(numeroCompatto)
                ? null
                : NonDigitRegex.Replace(numeroCompatto, string.Empty);
        }

        private static bool TrySplitInternationalPhone(string digits, out string prefix, out string number)
        {
            prefix = string.Empty;
            number = string.Empty;
            if (string.IsNullOrWhiteSpace(digits) || digits.Length < 7)
                return false;

            foreach (var knownPrefix in KnownPhonePrefixes.OrderByDescending(p => p.Length))
            {
                if (digits.StartsWith(knownPrefix, StringComparison.Ordinal) && digits.Length > knownPrefix.Length + 5)
                {
                    prefix = knownPrefix;
                    number = digits[knownPrefix.Length..];
                    return true;
                }
            }

            prefix = digits[..Math.Min(3, digits.Length - 6)];
            number = digits[prefix.Length..];
            return !string.IsNullOrWhiteSpace(prefix) && number.Length >= 6;
        }

        private static string GetNomeReferente(Partita partita)
        {
            return !string.IsNullOrWhiteSpace(partita.NomeRiferimento)
                ? partita.NomeRiferimento.Trim()
                : partita.Riferimento?.Trim() ?? string.Empty;
        }

        // ----------- INDEX -----------
        public async Task<IActionResult> Index()
        {
            await PopulateListinoLabelsAsync();
            var activeStaff = await _staffRegistryService.GetStaffAsync();
            ViewBag.StaffList = activeStaff;

            var partite = await _dbContext.Partite
                .Include(p => p.Tesseramenti)
                .ToListAsync();

            var oggi = DateTime.UtcNow.Date; // solo data
            var dueSettimaneFa = oggi.AddDays(-14);

            // Date presenti tra le partite
            var datePartite = partite.Select(p => p.Data.Date).Distinct().ToList();
            if (datePartite.Count == 0) datePartite.Add(oggi);

            var minData = datePartite.Min();
            var maxData = datePartite.Max();

            // Reperibilità in quel range
            var assenzeCalendario = await _dbContext.AssenzeCalendario
                .Where(a => a.Data >= minData && a.Data <= maxData)
                .ToListAsync();

            foreach (var partita in partite)
            {
                var giorno = partita.Data.Date;
                var assenza = assenzeCalendario.FirstOrDefault(a => a.Data == giorno);
                partita.Reperibile = assenza?.Reperibile ?? "In attesa";
            }

            var partiteFuture = partite
                .Where(p => p.Data.Date >= oggi && !p.IsDeleted)
                .OrderBy(p => p.Data + p.OraInizio)
                .ToList();

            var partitePassate = partite
                .Where(p => p.Data.Date < oggi && p.Data.Date >= dueSettimaneFa && !p.IsDeleted)
                .OrderByDescending(p => p.Data + p.OraInizio)
                .ToList();

            var partiteCancellate = partite
                .Where(p => p.IsDeleted && p.Data.Date >= dueSettimaneFa)
                .OrderByDescending(p => p.Data + p.OraInizio)
                .ToList();

            ViewBag.WeatherByPartitaId = await _weatherForecastService.GetWeatherForPartiteAsync(partiteFuture);
            ViewBag.PartiteCancellate = partiteCancellate;
            ViewBag.PartiteFuture = partiteFuture;
            ViewBag.PartitePassate = partitePassate;

            // ✅ STAFF DISPONIBILE/IN ATTESA PER DATA (Presente == true || Presente == null)
            var tutteLePartiteMostrate = partiteFuture
                .Concat(partitePassate)
                .Concat(partiteCancellate)
                .ToList();

            var dateMostrate = tutteLePartiteMostrate
                .Select(p => p.Data.Date)
                .Distinct()
                .ToList();

            var presenzeStaff = await _dbContext.PresenzaStaff
                .Where(p => dateMostrate.Contains(p.Data))
                .ToListAsync();

            var staffDisponibiliPerData = presenzeStaff
                .Where(p => (p.Presente == true || p.Presente == null) &&
                            activeStaff.Contains(p.NomeStaff))
                .GroupBy(p => p.Data.Date)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(x => x.NomeStaff).Distinct().OrderBy(x => x).ToList()
                );

            ViewBag.StaffDisponibiliPerData = staffDisponibiliPerData;

            return View();
        }

        // ----------- CREATE -----------
        public async Task<IActionResult> Create()
        {
            var catalog = await _pricingCatalogService.GetCatalogAsync();
            await PopulateListinoOptionsAsync(catalog.CurrentListinoId);
            return View(new Partita
            {
                Data = DateTime.Today,
                Listino = catalog.CurrentListinoId
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Partita partita)
        {
            if (!ModelState.IsValid)
            {
                await PopulateListinoOptionsAsync(partita.Listino);
                return View(partita);
            }

            partita.Listino = await ResolveValidListinoIdAsync(partita.Listino);
            NormalizzaContattoRiferimento(partita);

            // 👇 forza UTC anche se prendi solo la data
            partita.Data = DateTime.SpecifyKind(partita.Data.Date, DateTimeKind.Utc);
            if (!partita.Caccia)
                partita.CacciaDoppia = false;

            _dbContext.Add(partita);

            // INSERIMENTO AUTOMATICO PER PARTITE INFRASETTIMANALI
            var data = partita.Data; // è già UTC alla mezzanotte
            if (data.DayOfWeek != DayOfWeek.Saturday && data.DayOfWeek != DayOfWeek.Sunday)
            {
                var giorno = CultureInfo.CurrentCulture.TextInfo
                    .ToTitleCase(data.ToString("dddd", CultureInfo.GetCultureInfo("it-IT")));

                if (!await _dbContext.AssenzeCalendario.AnyAsync(r => r.Data == data))
                {
                    _dbContext.AssenzeCalendario.Add(new AssenzaCalendario
                    {
                        Data = data,
                        Giorno = giorno,
                        Reperibile = "In attesa"
                    });
                }

                var staffList = await _staffRegistryService.GetStaffAsync();
                foreach (var nome in staffList)
                {
                    if (!await _dbContext.PresenzaStaff.AnyAsync(p => p.Data == data && p.NomeStaff == nome))
                    {
                        _dbContext.PresenzaStaff.Add(new PresenzaStaff
                        {
                            Data = data,
                            Giorno = giorno,
                            NomeStaff = nome,
                            Presente = null
                        });
                    }
                }
            }

            await _dbContext.SaveChangesAsync();

            string oraFormattata = $"{(int)partita.OraInizio.TotalHours:D2}:{partita.OraInizio.Minutes:D2}";
            var subject = $"NUOVA PARTITA: {partita.Data:dd/MM/yyyy} - {oraFormattata}";
            string tipologia = (partita.Tipo?.Equals("kids", StringComparison.OrdinalIgnoreCase) ?? false) ? "KIDS" : "Adulti";

            string extra = "";
            if (partita.ColpiIllimitati) extra += "♾️ Colpi Illimitati<br>";
            if (partita.Caccia)
            {
                var catalog = await _pricingCatalogService.GetCatalogAsync();
                var rabbitPrice = GetRabbitPrice(catalog, partita.CacciaDoppia, partita.Listino);
                var labelX2 = partita.CacciaDoppia
                    ? $" x2 ({PricingCatalogService.FormatCurrency(rabbitPrice)})"
                    : $" ({PricingCatalogService.FormatCurrency(rabbitPrice)})";
                extra += $"🐰 Caccia al Coniglio{labelX2}<br>";
            }

            if (string.IsNullOrWhiteSpace(extra)) extra = "—";

            var messageHtml = $@"
<html><body><div style='font-family:Arial,sans-serif;line-height:1.5;'>
<p>Ciao,</p><p>È stata inserita una nuova partita!</p>
<p><strong>Data:</strong> {partita.Data:dd/MM/yyyy}<br>
<strong>Orario:</strong> {oraFormattata}<br>
<strong>Durata:</strong> {partita.Durata:0.##} ore<br>
<strong>Numero partecipanti:</strong> {partita.NumeroPartecipanti}<br>
<strong>Tipologia:</strong> {tipologia}<br>
<strong>Extra:</strong><br>{extra}</p>
<p>Controlla il calendario per maggiori dettagli.</p>
<p>Il team di Full Metal Paintball Carmagnola</p>
</div></body></html>";

            if (!_env.IsDevelopment())
                await SendNotificationToAllUsers(subject, messageHtml);

            return RedirectToAction(nameof(Index));
        }


        // ----------- EDIT -----------
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var partita = await _dbContext.Partite.FindAsync(id);
            if (partita == null) return NotFound();

            await PopulateListinoOptionsAsync(partita.Listino);
            return View(partita);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Partita partita)
        {
            if (id != partita.Id) return NotFound();
            if (!ModelState.IsValid)
            {
                await PopulateListinoOptionsAsync(partita.Listino);
                return View(partita);
            }
            if (!partita.Caccia)
                partita.CacciaDoppia = false;
            partita.Listino = await ResolveValidListinoIdAsync(partita.Listino);
            var existingPartita = await _dbContext.Partite.FindAsync(id);
            if (existingPartita == null) return NotFound();

            // 👇 forza UTC anche qui
            existingPartita.Data = DateTime.SpecifyKind(partita.Data.Date, DateTimeKind.Utc);

            existingPartita.OraInizio = partita.OraInizio;
            existingPartita.Durata = partita.Durata;
            existingPartita.NumeroPartecipanti = partita.NumeroPartecipanti;
            existingPartita.Caparra = partita.Caparra;
            existingPartita.CaparraConfermata = partita.CaparraConfermata;
            existingPartita.MetodoPagamentoCaparra = partita.MetodoPagamentoCaparra;
            existingPartita.Torneo = partita.Torneo;
            existingPartita.ColpiIllimitati = partita.ColpiIllimitati;
            existingPartita.Caccia = partita.Caccia;
            existingPartita.CacciaDoppia = partita.CacciaDoppia;
            existingPartita.Riferimento = partita.Riferimento;
            NormalizzaContattoRiferimento(partita);
            existingPartita.NomeRiferimento = partita.NomeRiferimento;
            existingPartita.PrefissoTelefonoRiferimento = partita.PrefissoTelefonoRiferimento;
            existingPartita.TelefonoRiferimento = partita.TelefonoRiferimento;
            existingPartita.Annotazioni = partita.Annotazioni;
            existingPartita.Tipo = partita.Tipo;
            existingPartita.Listino = partita.Listino;

            try
            {
                await _dbContext.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await _dbContext.Partite.AnyAsync(p => p.Id == id)) return NotFound();
                throw;
            }

            return RedirectToAction(nameof(Index));
        }


        // ----------- LISTE / VISTE SUPPORTO -----------
        [HttpGet]
        public async Task<IActionResult> Semplificata()
        {
            await PopulateListinoLabelsAsync();

            var oggi = DateTime.UtcNow.Date;

            var partite = await _dbContext.Partite
                .Where(p => p.Data.Date >= oggi && !p.IsDeleted)
                .OrderBy(p => p.Data)
                .ThenBy(p => p.OraInizio)
                .ToListAsync();

            var assenze = await _dbContext.AssenzeCalendario
                .Where(a => a.Data >= oggi)
                .ToListAsync();

            foreach (var partita in partite)
            {
                var assenza = assenze.FirstOrDefault(a => a.Data == partita.Data.Date);
                partita.Reperibile = assenza?.Reperibile ?? "In attesa";
            }

            return View(partite);
        }

        [HttpGet]
        public async Task<IActionResult> Archivio()
        {
            var oggi = DateTime.UtcNow.Date;
            var sogliaArchivio = oggi.AddDays(-14);

            var partite = await _dbContext.Partite
                .Where(p => p.Data < sogliaArchivio)
                .OrderByDescending(p => p.Data)
                .ToListAsync();

            var archivio = new Dictionary<int, Dictionary<int, List<Partita>>>();

            foreach (var partita in partite)
            {
                var anno = partita.Data.Year;
                var mese = partita.Data.Month;

                if (!archivio.ContainsKey(anno))
                    archivio[anno] = new Dictionary<int, List<Partita>>();

                if (!archivio[anno].ContainsKey(mese))
                    archivio[anno][mese] = new List<Partita>();

                archivio[anno][mese].Add(partita);
            }

            var model = new ArchivioViewModel { Archivio = archivio };
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> GeneraMessaggioDettagli(int id)
        {
            var partita = _dbContext.Partite.FirstOrDefault(p => p.Id == id);
            if (partita == null)
                return Json(new { success = false, messaggio = "Partita non trovata." });

            string messaggio = $@"
<strong>Data:</strong> {partita.Data:dd/MM/yyyy}<br>
<strong>Ora:</strong> {partita.OraInizio}<br>
<strong>Riferimento:</strong> {GetNomeReferente(partita)}<br>
<strong>Durata:</strong> {partita.Durata} ore<br>
<strong>Partecipanti:</strong> {partita.NumeroPartecipanti}<br>
<strong>Caparra:</strong> {partita.Caparra:0.00}€<br>";

            if (partita.Torneo) messaggio += "<strong>🏆 Torneo</strong><br>";
            if (partita.ColpiIllimitati) messaggio += "<strong>♾️ Colpi Illimitati</strong><br>";
            if (partita.Caccia)
            {
                var catalog = await _pricingCatalogService.GetCatalogAsync();
                var labelX2 = partita.CacciaDoppia ? " x2" : "";
                var prezzo = PricingCatalogService.FormatCurrency(GetRabbitPrice(catalog, partita.CacciaDoppia, partita.Listino));
                messaggio += $"<strong>🐰 Caccia al Coniglio{labelX2} ({prezzo})</strong><br>";
            }

            return Json(new { success = true, messaggio });
        }

        // ----------- DELETE (GET + POST) -----------
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var partita = await _dbContext.Partite.FirstOrDefaultAsync(p => p.Id == id);
            return partita == null ? NotFound() : View(partita);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var partita = await _dbContext.Partite.FindAsync(id);
            if (partita != null)
            {
                // soft-delete
                partita.IsDeleted = true;
                await _dbContext.SaveChangesAsync();

                string oraFormattata = $"{(int)partita.OraInizio.TotalHours:D2}:{partita.OraInizio.Minutes:D2}";
                var subject = $"PARTITA CANCELLATA: {partita.Data:dd/MM/yyyy} - {oraFormattata}";
                var messageHtml = $@"<html><body><p>Ciao,</p><p>Una partita è stata cancellata.</p>
<p><strong>Data:</strong> {partita.Data:dd/MM/yyyy}</p>
<p><strong>Orario:</strong> {oraFormattata}</p>
<p><strong>Annotazioni:</strong> {partita.Annotazioni}</p>
<p>Controlla il calendario per gli aggiornamenti.</p>
<p>Il team di Full Metal Paintball Carmagnola</p></body></html>";

                if (!string.Equals(_env.EnvironmentName, "Development", StringComparison.OrdinalIgnoreCase))
                    await SendNotificationToAllUsers(subject, messageHtml);

                // ATTENZIONE: doppia variabile per evitare il crash su timestamptz
                var giorno = partita.Data.Date;                                  // per colonne DATE (Unspecified)
                var giornoUtc = DateTime.SpecifyKind(partita.Data.Date, DateTimeKind.Utc); // per Partite (timestamptz)

                // Se nello stesso giorno (stessa data) ci sono ALTRE partite confermate (caparra) non cancellate,
                // NON rimuovere le righe infrasettimanali
                bool altreConfermate = await _dbContext.Partite
                    .AnyAsync(p => p.Id != id
                                && p.CaparraConfermata
                                && !p.IsDeleted
                                && p.Data.Date == giornoUtc); // confronto su Partite (timestamptz) → parametro UTC

                if (!altreConfermate
                    && giorno.DayOfWeek != DayOfWeek.Saturday
                    && giorno.DayOfWeek != DayOfWeek.Sunday)
                {
                    // Queste tabelle hanno colonne DATE → usa 'giorno' (Unspecified va bene)
                    var assenza = await _dbContext.AssenzeCalendario.FirstOrDefaultAsync(a => a.Data == giorno);
                    if (assenza != null) _dbContext.AssenzeCalendario.Remove(assenza);

                    var presenze = _dbContext.PresenzaStaff.Where(p => p.Data == giorno);
                    _dbContext.PresenzaStaff.RemoveRange(presenze);

                    await _dbContext.SaveChangesAsync();
                }
            }

            return RedirectToAction(nameof(Index));
        }


        // ----------- TESSERATI / POPUP -----------
        public async Task<IActionResult> TesseratiPerPartita(int id)
        {
            var partita = await _dbContext.Partite
                                          .Include(p => p.Tesseramenti)
                                          .FirstOrDefaultAsync(p => p.Id == id);

            if (partita == null) return NotFound();

            string oraPartitaFormattata = partita.OraInizio.ToString(@"hh\:mm");

            var viewModel = new TesseratiPerPartitaViewModel
            {
                PartitaId = partita.Id,
                DataPartita = partita.Data,
                OraPartita = oraPartitaFormattata,
                Tesserati = partita.Tesseramenti
                                   .OrderBy(t => t.Nome)
                                   .ThenBy(t => t.Cognome)
                                   .Select(t => new TesseramentoViewModel
                                   {
                                       Id = t.Id,
                                       Nome = t.Nome,
                                       Cognome = t.Cognome,
                                       DataCreazione = t.DataCreazione,
                                       PartitaId = t.PartitaId,
                                       NoTesseramento = t.NoTesseramento
                                   }).ToList()
            };

            return View(viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> TesseratiPerPopup(int id)
        {
            var partita = await _dbContext.Partite
                .AsNoTracking()
                .Include(p => p.Tesseramenti)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (partita == null)
                return Content("<div class=\"alert alert-warning\">Partita non trovata.</div>", "text/html", Encoding.UTF8);

            var tesserati = (partita.Tesseramenti ?? new List<Tesseramento>())
                .OrderBy(t => t.Nome)
                .ThenBy(t => t.Cognome)
                .ToList();

            if (!tesserati.Any())
                return Content("<div class=\"alert alert-info mb-0\">Nessun tesserato registrato per questa partita.</div>", "text/html", Encoding.UTF8);

            var html = new StringBuilder();
            html.Append("<div class=\"list-group\">");

            foreach (var t in tesserati)
            {
                var nome = HtmlEncoder.Default.Encode($"{t.Nome} {t.Cognome}");
                html.Append("<div class=\"list-group-item d-flex justify-content-between align-items-center gap-2\">");
                html.Append("<span>").Append(nome).Append("</span>");

                if (t.NoTesseramento)
                {
                    html.Append("<span class=\"badge text-bg-warning\">No Tesseramento</span>");
                }
                else
                {
                    html.Append("<span class=\"badge text-bg-success\">Da tesserare</span>");
                }

                html.Append("</div>");
            }

            html.Append("</div>");
            return Content(html.ToString(), "text/html", Encoding.UTF8);
        }

        [AllowAnonymous]
        public async Task<IActionResult> VisualizzaTesseratiPubblico(int id)
        {
            var partita = await _dbContext.Partite
                                          .Include(p => p.Tesseramenti)
                                          .FirstOrDefaultAsync(p => p.Id == id);
            if (partita == null) return NotFound();

            var tesseratiPubblici = partita.Tesseramenti
                                           .Select(t => new TesseramentoPubblicoViewModel
                                           {
                                               Nome = t.Nome,
                                               Cognome = t.Cognome
                                           })
                                           .OrderBy(t => t.Nome)
                                           .ThenBy(t => t.Cognome)
                                           .ToList();

            var model = new PartitaPubblicoViewModel
            {
                PartitaId = partita.Id,
                DataPartita = partita.Data,
                OraPartita = partita.OraInizio,
                NumeroPartecipanti = partita.NumeroPartecipanti,
                Tesserati = tesseratiPubblici
            };

            return View(model);
        }

        // ----------- CAPARRE / AZIONI VARIE -----------
        public async Task<IActionResult> Caparre()
        {
            var partite = await _dbContext.Partite
                .Where(p => p.Caparra > 0)
                .OrderBy(p => p.Data)
                .ThenBy(p => p.OraInizio)
                .ToListAsync();

            return View(partite);
        }

        [HttpPost]
        public async Task<JsonResult> CancellaCaparra(int id)
        {
            var partita = await _dbContext.Partite.FindAsync(id);
            if (partita == null) return Json(new { success = false, message = "Partita non trovata." });

            try
            {
                partita.Caparra = 0;
                partita.CaparraConfermata = false;
                partita.MetodoPagamentoCaparra = null;
                _dbContext.Partite.Update(partita);
                await _dbContext.SaveChangesAsync();
                return Json(new { success = true, message = "Caparra cancellata con successo." });
            }
            catch
            {
                return Json(new { success = false, message = "Errore durante la cancellazione della caparra." });
            }
        }

        [HttpPost]
        public async Task<JsonResult> InviaRecensione(int id)
        {
            var partita = await _dbContext.Partite.Include(p => p.Tesseramenti).FirstOrDefaultAsync(p => p.Id == id);
            if (partita == null) return Json(new { success = false, message = "Partita non trovata." });

            var emailList = partita.Tesseramenti
                .Where(t => !string.IsNullOrWhiteSpace(t.Email))
                .Select(t => t.Email.Replace(" ", "").ToLowerInvariant())
                .Distinct()
                .ToList();

            if (!emailList.Any()) return Json(new { success = false, message = "Nessuna email trovata per i tesserati." });

            try
            {
                var subject = "FMP Carmagnola - La tua opinione è importante!";
                var bodyHtml = $@"<div style='font-family: Arial, sans-serif; text-align: center; background-color: #f7f7f7; padding: 30px;'>
                            <div style='max-width: 600px; margin: auto; background-color: white; padding: 30px; border-radius: 10px; box-shadow: 0 2px 8px rgba(0,0,0,0.1);'>
                                <img src='https://i.imgur.com/K9Ugseg.gif' alt='FMP Carmagnola Logo' style='max-width: 150px; margin-bottom: 20px;' />
                                <h2 style='color: #28a745;'>Ciao,</h2>
                                <p>Grazie per aver scelto A.S.D. Full Metal Paintball Carmagnola. Speriamo che la tua esperienza con noi sia stata positiva.</p>
                                <p>Ci farebbe piacere ricevere una tua recensione:</p>
                                <a href='https://g.page/r/CSY7ElrZDaxMEBM/review' style='display: inline-block; background-color: #28a745; color: white; padding: 12px 24px; border-radius: 6px; font-weight: bold; text-decoration: none; margin: 20px 0;'>Lascia una Recensione</a>
                                <p>Grazie mille per il tuo tempo!</p>
                                <p>Il Team di A.S.D. Full Metal Paintball Carmagnola</p>
                            </div>
                        </div>";

                foreach (var email in emailList)
                    await _emailService.SendEmailAsync(email, subject, bodyHtml);

                return Json(new { success = true, message = "Email inviate con successo!" });
            }
            catch
            {
                return Json(new { success = false, message = "Errore durante l'invio delle email di recensione." });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RimuoviTesserato(int id, int partitaId)
        {
            var tesserato = await _dbContext.Tesseramenti.FindAsync(id);
            if (tesserato != null)
            {
                _dbContext.Tesseramenti.Remove(tesserato);
                await _dbContext.SaveChangesAsync();
            }
            return RedirectToAction("TesseratiPerPartita", new { id = partitaId });
        }

        [HttpPost]
        public async Task<IActionResult> AggiornaStaff(int id, string campo, string valore)
        {
            // Consenti solo i campi Staff1..Staff4 (evita nomi arbitrari via reflection)
            var campiAmmessi = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "Staff1", "Staff2", "Staff3", "Staff4" };
            if (!campiAmmessi.Contains(campo))
                return Json(new { success = false, message = "Campo non valido." });

            var valoreNormalizzato = string.IsNullOrWhiteSpace(valore) ? null : valore.Trim();
            var staffAttivi = await _staffRegistryService.GetStaffAsync();

            if (!string.IsNullOrWhiteSpace(valoreNormalizzato))
            {
                var staffValido = staffAttivi.FirstOrDefault(s =>
                    string.Equals(s, valoreNormalizzato, StringComparison.OrdinalIgnoreCase));

                if (staffValido == null)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Anagrafica staff non più attiva. Aggiorna la pagina e seleziona un nominativo valido."
                    });
                }

                valoreNormalizzato = staffValido;
            }

            // Attacco un "guscio" dell'entità per fare un update parziale senza toccare altre colonne
            var entity = new Partita { Id = id };
            _dbContext.Partite.Attach(entity);

            // Imposto il solo campo richiesto
            var prop = typeof(Partita).GetProperty(campo);
            if (prop == null)
                return Json(new { success = false, message = "Campo non valido." });

            prop.SetValue(entity, valoreNormalizzato);

            // Marco come modificata SOLO la proprietà StaffX indicata
            _dbContext.Entry(entity).Property(campo).IsModified = true;

            try
            {
                await _dbContext.SaveChangesAsync();
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = "Errore durante il salvataggio: " + ex.Message + " - Dettaglio: " + ex.InnerException?.Message
                });
            }
        }


        [HttpGet]
        public async Task<IActionResult> PresenzeStaffPopup(string data)
        {
            if (!DateTime.TryParse(data, out var dataParsed))
                return Content("<p>Data non valida.</p>");

            var giorno = dataParsed.Date;
            var staffAttivi = await _staffRegistryService.GetStaffAsync();
            var staffAttiviSet = staffAttivi.ToHashSet(StringComparer.OrdinalIgnoreCase);

            var presenze = await _dbContext.PresenzaStaff
                .Where(p => p.Data == giorno && (p.Presente == true || p.Presente == null))
                .ToListAsync();

            presenze = presenze
                .Where(p => staffAttiviSet.Contains(p.NomeStaff))
                .OrderBy(p => p.NomeStaff)
                .ToList();

            if (!presenze.Any())
                return Content("<p>Nessun membro dello staff risulta disponibile o in attesa.</p>");

            string html = "<ul style='text-align:left; padding-left:20px;'>";
            foreach (var p in presenze)
            {
                string stato = p.Presente == true ? "✅ Disponibile" : "⏳ In attesa";
                html += $"<li><strong>{p.NomeStaff}:</strong> {stato}</li>";
            }
            html += "</ul>";

            return Content(html);
        }

        [HttpGet]
        public async Task<IActionResult> GeneraMessaggioPrenotazione(int id)
        {
            var partita = _dbContext.Partite.FirstOrDefault(p => p.Id == id);
            if (partita == null)
                return Json(new { success = false, messaggio = "Partita non trovata." });

            string tipo = partita.Tipo?.ToLowerInvariant() ?? "adulti";
            var catalog = await _pricingCatalogService.GetCatalogAsync();
            var listinoId = catalog.Listini.Any(l => l.Id == partita.Listino) ? partita.Listino : catalog.CurrentListinoId;

            string prezzo, colpi, extraCaccia, infoTesseramento;
            decimal? prezzoUnitario = null;
            const decimal quotaTesseramento = 5m;

            if (tipo == "kids")
            {
                var kidsEntry = ResolveKidsPricingEntry(catalog, partita);
                prezzoUnitario = kidsEntry?.GetPrice(listinoId);
                prezzo = prezzoUnitario.HasValue ? PricingCatalogService.FormatCurrency(prezzoUnitario.Value) : "-";
                colpi = string.IsNullOrWhiteSpace(kidsEntry?.IncludedShots) ? "Illimitati" : kidsEntry.IncludedShots;
                extraCaccia = "";
                infoTesseramento = "<strong>⚠️ Il prezzo include il tesseramento con validità fino al 31/12.</strong><br>";
            }
            else
            {
                if (partita.Torneo)
                {
                    var tournamentEntry = catalog.GetEntry(partita.ColpiIllimitati
                        ? PricingEntryCodes.AdultTournamentUnlimited
                        : PricingEntryCodes.AdultTournamentStandard);

                    prezzoUnitario = tournamentEntry?.GetPrice(listinoId);
                    prezzo = prezzoUnitario.HasValue ? PricingCatalogService.FormatCurrency(prezzoUnitario.Value) : "-";
                    colpi = tournamentEntry?.IncludedShots ?? "-";
                }
                else
                {
                    if (partita.ColpiIllimitati)
                    {
                        var unlimitedEntry = ResolveAdultUnlimitedPricingEntry(catalog, partita.Durata);
                        prezzoUnitario = unlimitedEntry?.GetPrice(listinoId);
                        prezzo = prezzoUnitario.HasValue ? PricingCatalogService.FormatCurrency(prezzoUnitario.Value) : "-";
                        colpi = unlimitedEntry?.IncludedShots ?? "-";
                    }
                    else
                    {
                        var standardEntry = ResolveAdultStandardPricingEntry(catalog, partita.Durata);
                        prezzoUnitario = standardEntry?.GetPrice(listinoId);
                        prezzo = prezzoUnitario.HasValue ? PricingCatalogService.FormatCurrency(prezzoUnitario.Value) : "-";
                        colpi = standardEntry?.IncludedShots ?? "-";
                    }
                }

                if (partita.Caccia)
                {
                    var prezzoCaccia = PricingCatalogService.FormatCurrency(GetRabbitPrice(catalog, partita.CacciaDoppia, listinoId));
                    var labelX2 = partita.CacciaDoppia ? " x2" : "";
                    extraCaccia = $"💥 Extra: Caccia al Coniglio{labelX2} {prezzoCaccia}<br>";
                }
                else
                {
                    extraCaccia = "";
                }

                infoTesseramento = "Da far compilare a tutti i partecipanti entro 3 ore dall'arrivo al campo.<br>";
            }

            // Blocco coerenza adulti 2h illimitati
            if (tipo != "kids"
                && partita.ColpiIllimitati
                && Math.Abs(partita.Durata - 2.0) < 0.001)
            {
                return Json(new
                {
                    success = false,
                    messaggio = "Per adulti, la formula '2 ore con colpi illimitati' non è prevista. Modifica la durata o togli gli illimitati."
                });
            }

            string baseUrl = $"{Request.Scheme}://{Request.Host}";
            string linkTesseramento = $"{baseUrl}/Tesseramento?partitaId={partita.Id}";
            string linkTesseratiPubblico = $"{baseUrl}/Partite/VisualizzaTesseratiPubblico/{partita.Id}";
            var supplementoMinimoPartecipanti = "";
            if (prezzoUnitario.HasValue && partita.NumeroPartecipanti > 0 && partita.NumeroPartecipanti < 8)
            {
                var personeMancanti = 8 - partita.NumeroPartecipanti;
                var quotaGiocoSenzaTessera = Math.Max(0, prezzoUnitario.Value - quotaTesseramento);
                var totaleDifferenza = personeMancanti * quotaGiocoSenzaTessera;
                var differenzaATesta = totaleDifferenza / partita.NumeroPartecipanti;
                supplementoMinimoPartecipanti = $"⚠️ Il gruppo minimo richiesto è di 8 persone. Essendo in {partita.NumeroPartecipanti}, occorre coprire anche la quota gioco delle {personeMancanti} persone mancanti, escluso il tesseramento da 5€. Supplemento indicativo: {differenzaATesta:0.00}€ a testa.<br>";
            }

            string messaggio = $@"
Ciao! Di seguito il riepilogo della tua prenotazione:<br><br>
📅 Data: {partita.Data:dd/MM/yyyy}<br>
🕒 Orario: {partita.OraInizio.ToString(@"hh\:mm")}<br>
👶 Tipologia: {(partita.Torneo ? "Torneo + " : "")}{(partita.Tipo?.Equals("kids", StringComparison.OrdinalIgnoreCase) == true ? "KIDS" : "Adulti")}<br>
⏳ Durata: {partita.Durata} ore<br> 
👤 Referente: {GetNomeReferente(partita)}<br>
👥 Nr. Partecipanti: {partita.NumeroPartecipanti}<br>
💶 Caparra: {partita.Caparra:0.00}€<br>
💰 {prezzo} a testa<br>
{supplementoMinimoPartecipanti}
🎯 Colpi a disposizione: {colpi}<br>
{extraCaccia}
📎 Link Tesseramento: <a href='{linkTesseramento}' target='_blank'>{linkTesseramento}</a><br><br>
{infoTesseramento}
Potrete visualizzare in tempo reale gli iscritti qui:<br>
🔎 <a href='{linkTesseratiPubblico}' target='_blank'>{linkTesseratiPubblico}</a><br><br>";

            if (colpi != "Illimitati")
                messaggio += "Eventuali colpi extra potranno essere acquistati al campo.<br><br>";
            else
                messaggio += "<br>";

            messaggio += @"📍 Ci trovi a: Via Ceis 80, Carmagnola<br>
🧺 Disponiamo di un'area pic-nic dedicata per festeggiamenti, tagli torta e celebrazioni, senza costi aggiuntivi. Vi chiediamo solo di lasciare l'area pulita portando con voi i rifiuti a fine giornata.<br><br>";

            messaggio += @"È richiesto l'arrivo almeno 15 minuti prima della prenotazione.<br>
Il tempo di gioco inizia alle " + partita.OraInizio.ToString(@"hh\:mm") + @" anche in caso di ritardo.<br>
Comunicare variazioni di partecipanti entro 3 ore dall'inizio.<br>
Il campo è all'aperto ed è disponibile uno spogliatoio, ma non sono presenti docce: abbigliamento sportivo consigliato.<br>
Lenti a contatto consigliate, occhiali sconsigliati sotto la maschera.<br>
Minorenni solo se autorizzati dai genitori.<br>
In caso di maltempo si gioca salvo impraticabilità.<br>
Pagamenti solo contanti o Satispay, no bancomat.<br><br>
Ti aspettiamo! 🎯";

            return Json(new { success = true, messaggio });
        }

        private async Task PopulateListinoOptionsAsync(short selectedListinoId)
        {
            var catalog = await _pricingCatalogService.GetCatalogAsync();
            ViewBag.ListinoOptions = catalog.Listini
                .OrderBy(l => l.Id)
                .Select(l => new SelectListItem
                {
                    Value = l.Id.ToString(CultureInfo.InvariantCulture),
                    Text = catalog.BuildStandardSummary(l.Id),
                    Selected = l.Id == selectedListinoId
                })
                .ToList();
            ViewBag.ListinoAdultLabels = catalog.Listini.ToDictionary(l => l.Id.ToString(CultureInfo.InvariantCulture), l => catalog.BuildStandardSummary(l.Id));
            ViewBag.ListinoKidsLabels = catalog.Listini.ToDictionary(l => l.Id.ToString(CultureInfo.InvariantCulture), l => catalog.BuildKidsSummary(l.Id));
        }

        private async Task PopulateListinoLabelsAsync()
        {
            var catalog = await _pricingCatalogService.GetCatalogAsync();
            ViewBag.PricingCatalog = catalog;
            ViewBag.ListinoLabels = catalog.Listini.ToDictionary(l => l.Id, l => l.Name);
            ViewBag.CurrentListinoId = catalog.CurrentListinoId;
        }

        private async Task<short> ResolveValidListinoIdAsync(short listinoId)
        {
            var catalog = await _pricingCatalogService.GetCatalogAsync();
            return catalog.Listini.Any(l => l.Id == listinoId) ? listinoId : catalog.CurrentListinoId;
        }

        private static PricingEntry? ResolveAdultStandardPricingEntry(PricingCatalog catalog, double durata)
        {
            if (MatchesDuration(durata, 1.0))
            {
                return catalog.GetEntry(PricingEntryCodes.AdultStandard1Hour);
            }

            if (MatchesDuration(durata, 1.5))
            {
                return catalog.GetEntry(PricingEntryCodes.AdultStandard90Minutes);
            }

            if (MatchesDuration(durata, 2.0))
            {
                return catalog.GetEntry(PricingEntryCodes.AdultStandard2Hours);
            }

            return null;
        }

        private static PricingEntry? ResolveAdultUnlimitedPricingEntry(PricingCatalog catalog, double durata)
        {
            if (MatchesDuration(durata, 1.0))
            {
                return catalog.GetEntry(PricingEntryCodes.AdultUnlimited1Hour);
            }

            if (MatchesDuration(durata, 1.5))
            {
                return catalog.GetEntry(PricingEntryCodes.AdultUnlimited90Minutes);
            }

            return null;
        }

        private static PricingEntry? ResolveKidsPricingEntry(PricingCatalog catalog, Partita partita)
        {
            if (partita.Torneo)
            {
                var tournamentEntry = catalog.GetEntry(PricingEntryCodes.KidsTournament);
                if (tournamentEntry != null && (tournamentEntry.Listino1Price > 0m || tournamentEntry.Listino2Price > 0m))
                {
                    return tournamentEntry;
                }
            }

            if (MatchesDuration(partita.Durata, 1.0))
            {
                return catalog.GetEntry(PricingEntryCodes.Kids1Hour);
            }

            if (MatchesDuration(partita.Durata, 1.5))
            {
                return catalog.GetEntry(PricingEntryCodes.Kids90Minutes);
            }

            if (MatchesDuration(partita.Durata, 2.0))
            {
                return catalog.GetEntry(PricingEntryCodes.Kids2Hours);
            }

            return null;
        }

        private static decimal GetRabbitPrice(PricingCatalog catalog, bool doubleCostume, short listinoId)
        {
            var entry = catalog.GetEntry(doubleCostume ? PricingEntryCodes.RabbitDouble : PricingEntryCodes.RabbitSingle);
            return entry?.GetPrice(listinoId) ?? 0m;
        }

        private static bool MatchesDuration(double actual, double expected) =>
            Math.Abs(actual - expected) < 0.01;
    }
}
