using System;
using System.Linq;
using System.Threading.Tasks;
using DocumentFormat.OpenXml.InkML;
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

        public PartiteController(TesseramentoDbContext dbContext, IConfiguration configuration, IEmailService emailService, UserManager<ApplicationUser> userManager)
        {
            _dbContext = dbContext;
            _configuration = configuration;
            _emailService = emailService;
            _userManager = userManager;
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

        public async Task<IActionResult> Index()
        {
            var partite = await _dbContext.Partite.Include(p => p.Tesseramenti).ToListAsync();
            var oggi = DateTime.SpecifyKind(DateTime.Today, DateTimeKind.Utc);

            var datePartite = partite.Select(p => DateTime.SpecifyKind(p.Data.Date, DateTimeKind.Utc)).Distinct().ToList();
            var assenzeCalendario = await _dbContext.AssenzeCalendario.Where(a => datePartite.Contains(DateTime.SpecifyKind(a.Data.Date, DateTimeKind.Utc))).ToListAsync();

            foreach (var partita in partite)
            {
                var assenza = assenzeCalendario.FirstOrDefault(a => DateTime.SpecifyKind(a.Data.Date, DateTimeKind.Utc) == DateTime.SpecifyKind(partita.Data.Date, DateTimeKind.Utc));
                partita.Reperibile = assenza != null ? assenza.Reperibile : "In attesa";
            }

            var partiteFuture = partite.Where(p => DateTime.SpecifyKind(p.Data.Date, DateTimeKind.Utc) >= oggi && !p.IsDeleted).OrderBy(p => p.Data + p.OraInizio).ToList();
            var partitePassate = partite.Where(p => DateTime.SpecifyKind(p.Data.Date, DateTimeKind.Utc) < oggi && !p.IsDeleted).OrderBy(p => p.Data + p.OraInizio).ToList();
            var partiteCancellate = partite.Where(p => p.IsDeleted).OrderBy(p => p.Data + p.OraInizio).ToList();

            ViewBag.PartiteCancellate = partiteCancellate;
            ViewBag.PartiteFuture = partiteFuture;
            ViewBag.PartitePassate = partitePassate;
            return View();
        }

        public IActionResult Create() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Partita partita)
        {
            if (ModelState.IsValid)
            {
                partita.Data = DateTime.SpecifyKind(partita.Data, DateTimeKind.Utc);
                _dbContext.Add(partita);
                await _dbContext.SaveChangesAsync();

                string oraFormattata = $"{(int)partita.OraInizio.TotalHours:D2}:{partita.OraInizio.Minutes:D2}";
                var subject = $"NUOVA PARTITA: {partita.Data:dd/MM/yyyy} - {oraFormattata}";

                var messageHtml = $@"<html><body><p>Ciao,</p><p>È stata inserita una nuova partita!</p><p><strong>Data:</strong> {partita.Data:dd/MM/yyyy}</p><p><strong>Orario:</strong> {oraFormattata}</p><p><strong>Annotazioni:</strong> {partita.Annotazioni}</p><p>Controlla il calendario per maggiori dettagli.</p><p>Il team di Full Metal Paintball Carmagnola</p></body></html>";
                await SendNotificationToAllUsers(subject, messageHtml);

                return RedirectToAction(nameof(Index));
            }
            return View(partita);
        }

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
            if (ModelState.IsValid)
            {
                partita.Data = DateTime.SpecifyKind(partita.Data, DateTimeKind.Utc);
                try { _dbContext.Update(partita); await _dbContext.SaveChangesAsync(); }
                catch (DbUpdateConcurrencyException) { if (!_dbContext.Partite.Any(p => p.Id == id)) return NotFound(); else throw; }
                return RedirectToAction(nameof(Index));
            }
            return View(partita);
        }

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
                partita.IsDeleted = true;
                await _dbContext.SaveChangesAsync();

                string oraFormattata = $"{(int)partita.OraInizio.TotalHours:D2}:{partita.OraInizio.Minutes:D2}";
                var subject = $"PARTITA CANCELLATA: {partita.Data:dd/MM/yyyy} - {oraFormattata}";

                var messageHtml = $@"<html><body><p>Ciao,</p><p>Una partita è stata cancellata.</p><p><strong>Data:</strong> {partita.Data:dd/MM/yyyy}</p><p><strong>Orario:</strong> {oraFormattata}</p><p><strong>Annotazioni:</strong> {partita.Annotazioni}</p><p>Controlla il calendario per gli aggiornamenti.</p><p>Il team di Full Metal Paintball Carmagnola</p></body></html>";
                await SendNotificationToAllUsers(subject, messageHtml);
            }
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> TesseratiPerPartita(int id)
        {
            var partita = await _dbContext.Partite.Include(p => p.Tesseramenti).FirstOrDefaultAsync(p => p.Id == id);
            if (partita == null) return NotFound();

            ViewBag.DataPartita = partita.Data.ToString("dd/MM/yyyy");
            ViewBag.OraPartita = partita.OraInizio;
            ViewBag.PartitaId = partita.Id;

            var tesserati = partita.Tesseramenti.Select(t => new TesseramentoViewModel
            {
                Id = t.Id,
                Nome = t.Nome,
                Cognome = t.Cognome,
                DataNascita = t.DataNascita,
                PartitaId = t.PartitaId
            }).ToList();

            return View(tesserati);
        }

        [AllowAnonymous]
        public async Task<IActionResult> VisualizzaTesseratiPubblico(int id)
        {
            var partita = await _dbContext.Partite.Include(p => p.Tesseramenti).FirstOrDefaultAsync(p => p.Id == id);
            if (partita == null) return NotFound();

            var tesseratiPubblici = partita.Tesseramenti.Select(t => new TesseramentoPubblicoViewModel
            {
                Nome = t.Nome,
                Cognome = t.Cognome
            }).OrderBy(t => t.Nome).ThenBy(t => t.Cognome).ToList();

            var model = new PartitaPubblicoViewModel
            {
                PartitaId = partita.Id,
                DataPartita = partita.Data,
                OraPartita = partita.OraInizio,
                Tesserati = tesseratiPubblici
            };

            return View(model);
        }

        public async Task<IActionResult> Caparre()
        {
            var partite = await _dbContext.Partite.Where(p => p.Caparra > 0).OrderBy(p => p.Data).ThenBy(p => p.OraInizio).ToListAsync();
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
            catch (Exception)
            {
                return Json(new { success = false, message = "Errore durante la cancellazione della caparra." });
            }
        }

        [HttpPost]
        public async Task<JsonResult> InviaRecensione(int id)
        {
            var partita = await _dbContext.Partite.Include(p => p.Tesseramenti).FirstOrDefaultAsync(p => p.Id == id);
            if (partita == null) return Json(new { success = false, message = "Partita non trovata." });

            var emailList = partita.Tesseramenti.Where(t => !string.IsNullOrEmpty(t.Email)).Select(t => t.Email).Distinct().ToList();
            if (!emailList.Any()) return Json(new { success = false, message = "Nessuna email trovata per i tesserati." });

            try
            {
                var subject = "FMP Carmagnola - La tua opinione è importante!";
                var bodyHtml = $@"<div style='font-family: Arial, sans-serif; text-align: center; background-color: #f7f7f7; padding: 30px;'><div style='max-width: 600px; margin: auto; background-color: white; padding: 30px; border-radius: 10px; box-shadow: 0 2px 8px rgba(0,0,0,0.1);'><img src='https://{Request.Host}/img/logo.gif' alt='FMP Carmagnola Logo' style='max-width: 150px; margin-bottom: 20px;' /><h2 style='color: #28a745;'>Ciao,</h2><p>Grazie per aver scelto A.S.D. Full Metal Paintball Carmagnola. Speriamo che la tua esperienza con noi sia stata positiva e che tu sia soddisfatto del nostro servizio.</p><p>Ci farebbe molto piacere ricevere una tua recensione. Il tuo feedback è estremamente importante per noi e ci aiuta a migliorare continuamente.</p><p>Se hai qualche minuto, ti invitiamo a cliccare sul link qui sotto per lasciare una recensione:</p><a href='https://g.page/r/CSY7ElrZDaxMEBM/review' style='display: inline-block; background-color: #28a745; color: white; padding: 12px 24px; border-radius: 6px; font-weight: bold; text-decoration: none; margin: 20px 0;'>Lascia una Recensione</a><div><img src='https://upload.wikimedia.org/wikipedia/commons/thumb/5/5e/Google_Reviews_logo.svg/240px-Google_Reviews_logo.svg.png' alt='Google Reviews' style='max-width: 120px; margin: 20px auto;' /></div><p>Grazie mille per il tuo tempo e la tua collaborazione.</p><p>Cordiali saluti,<br>Il Team di A.S.D. Full Metal Paintball Carmagnola</p></div></div>";

                foreach (var email in emailList) { await _emailService.SendEmailAsync(email, subject, bodyHtml); }

                return Json(new { success = true, message = "Email inviate con successo!" });
            }
            catch (Exception)
            {
                return Json(new { success = false, message = "Errore durante l’invio delle email di recensione." });
            }
        }
        [HttpPost]
        public async Task<IActionResult> RimuoviTesserato(int id, int partitaId)
        {
            var tesserato = await _dbContext.Tesseramenti.FindAsync(id);
            if (tesserato == null)
                return NotFound();

            _dbContext.Tesseramenti.Remove(tesserato);
            await _dbContext.SaveChangesAsync();

            return RedirectToAction("TesseratiPerPartita", new { id = partitaId });
        }

        [HttpPost]
        public async Task<IActionResult> AggiornaStaff(int id, string campo, string valore)
        {
            var partita = await _dbContext.Partite.FindAsync(id);
            if (partita == null)
                return Json(new { success = false, message = "Partita non trovata." });

            try
            {
                var prop = typeof(Partita).GetProperty(campo);
                if (prop == null)
                    return Json(new { success = false, message = "Campo non valido." });

                prop.SetValue(partita, valore);
                _dbContext.Update(partita);
                await _dbContext.SaveChangesAsync();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public IActionResult GeneraMessaggioPrenotazione(int id)
        {
            var partita = _dbContext.Partite.FirstOrDefault(p => p.Id == id);

            if (partita == null)
            {
                return Json(new { success = false, messaggio = "Partita non trovata." });
            }

            string prezzo = partita.Torneo ? "22€" : partita.Durata switch
            {
                1 => "22€",
                1.5 => "27€",
                2 => "32€",
                _ => "-"
            };

            string colpi = partita.ColpiIllimitati ? "Illimitati" : partita.Durata switch
            {
                1 => "200",
                1.5 => "300",
                2 => "400",
                _ => "-"
            };

            string extraCaccia = partita.Caccia ? "💥 Extra: Caccia al Coniglio 60€" : "";

            // Costruzione del link al tesseramento dinamico per quella partita
            string baseUrl = $"{Request.Scheme}://{Request.Host}";
            string linkTesseramento = $"{baseUrl}/Tesseramento?partitaId={partita.Id}";


            string messaggio = $@"
Ciao! Di seguito il riepilogo della tua prenotazione:<br><br>
📅 Data: {partita.Data:dd/MM/yyyy}<br>
🕒 Orario: {partita.OraInizio}<br>
👤 Referente: {partita.Riferimento}<br>
💶 Caparra: {partita.Caparra:0.00}€<br>
💰 {prezzo} a testa<br>
🎯 Colpi a disposizione: {colpi}<br>
{extraCaccia}<br><br>
📎 Link Tesseramento: {linkTesseramento}<br><br>
Da far compilare a tutti i partecipanti entro 3 ore dall'arrivo al campo.<br>
Tramite lo stesso link, in fondo alla pagina, potete visualizzare l'elenco dei tesserati già registrati.<br><br>
Eventuali colpi extra potranno essere acquistati al campo.<br>
È richiesto l'arrivo almeno 15 minuti prima della prenotazione.<br>
Il tempo di gioco inizia alle {partita.OraInizio} anche in caso di ritardo.<br>
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
