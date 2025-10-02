using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
// using DocumentFormat.OpenXml.InkML; // non serve qui, puoi rimuoverlo
using Full_Metal_Paintball_Carmagnola.Models;
using Full_Metal_Paintball_Carmagnola.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
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

        public PartiteController(
            TesseramentoDbContext dbContext,
            IConfiguration configuration,
            IEmailService emailService,
            UserManager<ApplicationUser> userManager,
            IWebHostEnvironment env)
        {
            _dbContext = dbContext;
            _configuration = configuration;
            _emailService = emailService;
            _userManager = userManager;
            _env = env;
        }

        private async Task SendNotificationToAllUsers(string subject, string messageHtml)
        {
            var users = await _userManager.Users.ToListAsync();
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

        // ----------- INDEX -----------
        public async Task<IActionResult> Index()
        {
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

            ViewBag.PartiteCancellate = partiteCancellate;
            ViewBag.PartiteFuture = partiteFuture;
            ViewBag.PartitePassate = partitePassate;
            return View();
        }

        // ----------- CREATE -----------
        public IActionResult Create() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Partita partita)
        {
            if (!ModelState.IsValid) return View(partita);

            // 👇 forza UTC anche se prendi solo la data
            partita.Data = DateTime.SpecifyKind(partita.Data.Date, DateTimeKind.Utc);

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

                foreach (var nome in new[] { "Simone", "Davide", "Andrea", "Federico", "Enrico" })
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
            if (partita.Caccia) extra += "🐰 Caccia al Coniglio<br>";
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
            return partita == null ? NotFound() : View(partita);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Partita partita)
        {
            if (id != partita.Id) return NotFound();
            if (!ModelState.IsValid) return View(partita);

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
            existingPartita.Riferimento = partita.Riferimento;
            existingPartita.Annotazioni = partita.Annotazioni;
            existingPartita.Tipo = partita.Tipo;

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
        public IActionResult GeneraMessaggioDettagli(int id)
        {
            var partita = _dbContext.Partite.FirstOrDefault(p => p.Id == id);
            if (partita == null)
                return Json(new { success = false, messaggio = "Partita non trovata." });

            string messaggio = $@"
<strong>Data:</strong> {partita.Data:dd/MM/yyyy}<br>
<strong>Ora:</strong> {partita.OraInizio}<br>
<strong>Riferimento:</strong> {partita.Riferimento}<br>
<strong>Durata:</strong> {partita.Durata} ore<br>
<strong>Partecipanti:</strong> {partita.NumeroPartecipanti}<br>
<strong>Caparra:</strong> {partita.Caparra:0.00}€<br>";

            if (partita.Torneo) messaggio += "<strong>🏆 Torneo</strong><br>";
            if (partita.ColpiIllimitati) messaggio += "<strong>♾️ Colpi Illimitati</strong><br>";
            if (partita.Caccia) messaggio += "<strong>🐰 Caccia al Coniglio</strong><br>";

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
                                       PartitaId = t.PartitaId
                                   }).ToList()
            };

            return View(viewModel);
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
                return Json(new { success = false, message = "Errore durante l’invio delle email di recensione." });
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

            // Attacco un "guscio" dell'entità per fare un update parziale senza toccare altre colonne
            var entity = new Partita { Id = id };
            _dbContext.Partite.Attach(entity);

            // Imposto il solo campo richiesto
            var prop = typeof(Partita).GetProperty(campo);
            prop.SetValue(entity, string.IsNullOrWhiteSpace(valore) ? null : valore);

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

            var presenze = await _dbContext.PresenzaStaff
                .Where(p => p.Data == giorno && (p.Presente == true || p.Presente == null))
                .OrderBy(p => p.NomeStaff)
                .ToListAsync();

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
        public IActionResult GeneraMessaggioPrenotazione(int id)
        {
            var partita = _dbContext.Partite.FirstOrDefault(p => p.Id == id);
            if (partita == null)
                return Json(new { success = false, messaggio = "Partita non trovata." });

            string tipo = partita.Tipo?.ToLowerInvariant() ?? "adulti";

            string prezzo, colpi, extraCaccia, infoTesseramento;

            if ((partita.Tipo?.ToLowerInvariant() ?? "adulti") == "kids")
            {
                // KIDS
                prezzo = partita.Durata switch
                {
                    1.0 => "17€",
                    1.5 => "22€",
                    2.0 => "27€",
                    _ => "-"
                };
                colpi = "Illimitati";
                extraCaccia = "";
                infoTesseramento = "<strong>⚠️ Il prezzo include il tesseramento con validità fino al 31/12.</strong><br>";
            }
            else
            {
                // ADULTI
                if (partita.ColpiIllimitati)
                {
                    prezzo = partita.Durata switch
                    {
                        1.0 => "35€",
                        1.5 => "42€",
                        2.0 => "NON PREVISTA",
                        _ => "-"
                    };
                    colpi = "Illimitati";
                }
                else
                {
                    prezzo = partita.Durata switch
                    {
                        1.0 => "22€",
                        1.5 => "27€",
                        2.0 => "32€",
                        _ => "-"
                    };
                    colpi = partita.Durata switch
                    {
                        1.0 => "200",
                        1.5 => "300",
                        2.0 => "400",
                        _ => "-"
                    };
                }

                extraCaccia = partita.Caccia ? "💥 Extra: Caccia al Coniglio 60€<br>" : "";
                infoTesseramento = "Da far compilare a tutti i partecipanti entro 3 ore dall'arrivo al campo.<br>";
            }

            // Blocco coerenza adulti 2h illimitati
            if ((partita.Tipo?.ToLowerInvariant() ?? "adulti") != "kids"
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

            string messaggio = $@"
Ciao! Di seguito il riepilogo della tua prenotazione:<br><br>
📅 Data: {partita.Data:dd/MM/yyyy}<br>
🕒 Orario: {partita.OraInizio.ToString(@"hh\:mm")}<br>
👶 Tipologia: {(partita.Tipo?.ToUpperInvariant() == "KIDS" ? "KIDS" : "Adulti")}<br>
⏳ Durata: {partita.Durata} ore<br> 
👤 Referente: {partita.Riferimento}<br>
👥 Nr. Partecipanti: {partita.NumeroPartecipanti}<br>
💶 Caparra: {partita.Caparra:0.00}€<br>
💰 {prezzo} a testa<br>
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

            messaggio += @"È richiesto l'arrivo almeno 15 minuti prima della prenotazione.<br>
Il tempo di gioco inizia alle " + partita.OraInizio.ToString(@"hh\:mm") + @" anche in caso di ritardo.<br>
Comunicare variazioni di partecipanti entro 3 ore dall'inizio.<br>
Il campo è all'aperto, senza spogliatoi o docce: abbigliamento sportivo consigliato.<br>
Lenti a contatto consigliate, occhiali sconsigliati sotto la maschera.<br>
Minorenni solo se autorizzati dai genitori.<br>
In caso di maltempo si gioca salvo impraticabilità.<br>
Pagamenti solo contanti o Satispay, no bancomat.<br><br>
Ti aspettiamo! 🎯";

            return Json(new { success = true, messaggio });
        }
    }
}
