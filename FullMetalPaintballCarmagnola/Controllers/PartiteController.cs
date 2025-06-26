using System;
using System.Linq;
using System.Threading.Tasks;
// Rimossi gli using non utilizzati nel codice fornito, ma mantienili se li usi altrove nel file completo
// using ClosedXML.Excel;
using Full_Metal_Paintball_Carmagnola.Models;
using Full_Metal_Paintball_Carmagnola.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
// Rimossi gli using non utilizzati nel codice fornito, ma mantienili se li usi altrove nel file completo
// using OfficeOpenXml;
// using OfficeOpenXml.Style;

namespace Full_Metal_Paintball_Carmagnola.Controllers
{
    [Authorize(Policy = "Prenotazioni")] // Questa policy si applica all'intero controller, tranne per le azioni [AllowAnonymous]
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

        // Metodo privato per inviare notifiche a tutti gli utenti (mantenuto come prima)
        private async Task SendNotificationToAllUsers(string subject, string messageHtml)
        {
            var users = await _userManager.Users.ToListAsync();
            var adminEmails = _configuration.GetSection("AdminNotifications").Get<string[]>();

            foreach (var user in users)
            {
                if (!string.IsNullOrEmpty(user.Email))
                {
                    try
                    {
                        await _emailService.SendEmailAsync(user.Email, subject, messageHtml);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Errore nell'invio email a {user.Email}: {ex.Message}");
                    }
                }
            }

            if (adminEmails != null && adminEmails.Any())
            {
                foreach (var adminEmail in adminEmails)
                {
                    if (!string.IsNullOrEmpty(adminEmail))
                    {
                        try
                        {
                            await _emailService.SendEmailAsync(adminEmail, subject, messageHtml);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Errore nell'invio email all'admin {adminEmail}: {ex.Message}");
                        }
                    }
                }
            }
        }


        // GET: Partite
        public async Task<IActionResult> Index()
        {
            var partite = await _dbContext.Partite
                .Include(p => p.Tesseramenti)
                .ToListAsync();

            var oggi = DateTime.Today;

            var datePartite = partite.Select(p => p.Data.Date).Distinct().ToList();

            var assenzeCalendario = await _dbContext.AssenzeCalendario
                .Where(a => datePartite.Contains(a.Data.Date))
                .ToListAsync();

            foreach (var partita in partite)
            {
                var assenza = assenzeCalendario.FirstOrDefault(a => a.Data.Date == partita.Data.Date);
                partita.Reperibile = assenza != null ? assenza.Reperibile : "In attesa";
            }

            var partiteFuture = partite
                .Where(p => p.Data.Date >= oggi && !p.IsDeleted)
                .OrderBy(p => p.Data + p.OraInizio)
                .ToList();

            var partitePassate = partite
                .Where(p => p.Data.Date < oggi && !p.IsDeleted)
                .OrderBy(p => p.Data + p.OraInizio)
                .ToList();
            var partiteCancellate = partite
        .Where(p => p.IsDeleted)
        .OrderBy(p => p.Data + p.OraInizio)
        .ToList();

            ViewBag.PartiteCancellate = partiteCancellate;


            ViewBag.PartiteFuture = partiteFuture;
            ViewBag.PartitePassate = partitePassate;

            return View();
        }

        // GET: Partite/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Partite/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Partita partita)
        {
            if (ModelState.IsValid)
            {
                _dbContext.Add(partita);
                await _dbContext.SaveChangesAsync();

                string oraFormattata = $"{(int)partita.OraInizio.TotalHours:D2}:{partita.OraInizio.Minutes:D2}";
                var subject = $"NUOVA PARTITA: {partita.Data:dd/MM/yyyy} - {oraFormattata}";

                var messageHtml = $@"
                    <html>
                    <head><style>body {{ font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; }}</style></head>
                    <body>
                        <p>Ciao,</p>
                        <p>È stata inserita una nuova partita!</p>
                        <p><strong>Data:</strong> {partita.Data:dd/MM/yyyy}</p>
                        <p><strong>Orario:</strong> {oraFormattata}</p> 
                        <p><strong>Annotazioni:</strong> {partita.Annotazioni}</p>
                        <p>Controlla il calendario per maggiori dettagli.</p>
                        <p>Il team di Full Metal Paintball Carmagnola</p>
                    </body>
                    </html>";
                await SendNotificationToAllUsers(subject, messageHtml);

                return RedirectToAction(nameof(Index));
            }
            return View(partita);
        }

        // GET: Partite/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var partita = await _dbContext.Partite.FindAsync(id);
            if (partita == null) return NotFound();

            return View(partita);
        }

        // POST: Partite/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Partita partita)
        {
            if (id != partita.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _dbContext.Update(partita);
                    await _dbContext.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_dbContext.Partite.Any(p => p.Id == id))
                        return NotFound();
                    else
                        throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(partita);
        }

        // GET: Partite/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var partita = await _dbContext.Partite.FirstOrDefaultAsync(p => p.Id == id);

            if (partita == null) return NotFound();

            return View(partita);
        }

        // POST: Partite/Delete/5 (soft delete)
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

                var messageHtml = $@"
                    <html>
                    <head><style>body {{ font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; }}</style></head>
                    <body>
                        <p>Ciao,</p>
                        <p>Una partita è stata cancellata.</p>
                        <p><strong>Data:</strong> {partita.Data:dd/MM/yyyy}</p>
                        <p><strong>Orario:</strong> {oraFormattata}</p> 
                        <p><strong>Annotazioni:</strong> {partita.Annotazioni}</p>
                        <p>Controlla il calendario per gli aggiornamenti.</p>
                        <p>Il team di Full Metal Paintball Carmagnola</p>
                    </body>
                    </html>";
                await SendNotificationToAllUsers(subject, messageHtml);

            }
            return RedirectToAction(nameof(Index));
        }

        // GET: Tesserati per partita (VISIBILE SOLO ALLO STAFF)
        public async Task<IActionResult> TesseratiPerPartita(int id)
        {
            var partita = await _dbContext.Partite
                .Include(p => p.Tesseramenti)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (partita == null)
                return NotFound();

            ViewBag.DataPartita = partita.Data.ToString("dd/MM/yyyy");
            ViewBag.OraPartita = partita.OraInizio; // L'OraInizio è un TimeSpan
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

        // <<< NUOVA AZIONE PUBBLICA: Visualizza Elenco Tesserati per Clienti
        [AllowAnonymous] // NON RICHIEDE AUTORIZZAZIONE
        public async Task<IActionResult> VisualizzaTesseratiPubblico(int id)
        {
            var partita = await _dbContext.Partite
                .Include(p => p.Tesseramenti) // Include i tesseramenti
                .FirstOrDefaultAsync(p => p.Id == id);

            if (partita == null)
            {
                return NotFound(); // Partita non trovata
            }

            // Mappa i tesseramenti al ViewModel semplificato per la visualizzazione pubblica
            var tesseratiPubblici = partita.Tesseramenti
                .Select(t => new TesseramentoPubblicoViewModel // Usa il nuovo ViewModel
                {
                    Nome = t.Nome,
                    Cognome = t.Cognome
                })
                .OrderBy(t => t.Nome) // Ordina per nome per una migliore leggibilità
                .ThenBy(t => t.Cognome)
                .ToList();

            // Prepara il ViewModel per la vista pubblica
            var model = new PartitaPubblicoViewModel
            {
                PartitaId = partita.Id,
                DataPartita = partita.Data,
                OraPartita = partita.OraInizio,
                Tesserati = tesseratiPubblici
            };

            return View(model); // Restituisci la nuova vista pubblica
        }
        // >>> FINE NUOVA AZIONE PUBBLICA


        // Lista caparre ordinata (mantenuto come prima)
        public async Task<IActionResult> Caparre()
        {
            var partite = await _dbContext.Partite
                .Where(p => p.Caparra > 0)
                .OrderBy(p => p.Data)
                .ThenBy(p => p.OraInizio)
                .ToListAsync();

            return View(partite);
        }

        // POST: Cancella singola caparra (mantenuto come prima)
        [HttpPost]
        public async Task<JsonResult> CancellaCaparra(int id)
        {
            var partita = await _dbContext.Partite.FindAsync(id);
            if (partita == null)
            {
                return Json(new { success = false, message = "Partita non trovata." });
            }

            try
            {
                partita.Caparra = 0;
                partita.CaparraConfermata = false;
                partita.MetodoPagamentoCaparra = null;

                _dbContext.Partite.Update(partita);
                await _dbContext.SaveChangesAsync();

                return Json(new { success = true, message = "Caparra cancellata con successo." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Errore durante la cancellazione della caparra." });
            }
        }

        // POST: Invia email recensione - Adattato per usare IEmailService (mantenuto come prima)
        [HttpPost]
        public async Task<JsonResult> InviaRecensione(int id)
        {
            var partita = await _dbContext.Partite
                .Include(p => p.Tesseramenti)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (partita == null)
                return Json(new { success = false, message = "Partita non trovata." });

            var emailList = partita.Tesseramenti
                .Where(t => !string.IsNullOrEmpty(t.Email))
                .Select(t => t.Email)
                .Distinct()
                .ToList();

            if (!emailList.Any())
                return Json(new { success = false, message = "Nessuna email trovata per i tesserati." });

            try
            {
                var subject = "FMP Carmagnola - La tua opinione è importante!";
                var bodyHtml = $@"
<div style='font-family: Arial, sans-serif; text-align: center; background-color: #f7f7f7; padding: 30px;'>
    <div style='max-width: 600px; margin: auto; background-color: white; padding: 30px; border-radius: 10px; box-shadow: 0 2px 8px rgba(0,0,0,0.1);'>
        <img src='https://{Request.Host}/img/logo.gif' alt='FMP Carmagnola Logo' style='max-width: 150px; margin-bottom: 20px;' />
        <h2 style='color: #28a745;'>Ciao,</h2>
        <p>Grazie per aver scelto A.S.D. Full Metal Paintball Carmagnola. Speriamo che la tua esperienza con noi sia stata positiva e che tu sia soddisfatto del nostro servizio.</p>
        <p>Ci farebbe molto piacere ricevere una tua recensione. Il tuo feedback è estremamente importante per noi e ci aiuta a migliorare continuamente.</p>
        <p>Se hai qualche minuto, ti invitiamo a cliccare sul link qui sotto per lasciare una recensione:</p>
        <a href='https://g.page/r/CSY7ElrZDaxMEBM/review' 
            style='display: inline-block; background-color: #28a745; color: white; padding: 12px 24px; border-radius: 6px; font-weight: bold; text-decoration: none; margin: 20px 0;'>Lascia una Recensione</a>
        <div>
            <img src='https://upload.wikimedia.org/wikipedia/commons/thumb/5/5e/Google_Reviews_logo.svg/240px-Google_Reviews_logo.svg.png' alt='Google Reviews' style='max-width: 120px; margin: 20px auto;' />
        </div>
        <p>Grazie mille per il tuo tempo e la tua collaborazione.</p>
        <p>Cordiali saluti,<br>Il Team di A.S.D. Full Metal Paintball Carmagnola</p>
    </div>
</div>";

                foreach (var email in emailList)
                {
                    await _emailService.SendEmailAsync(email, subject, bodyHtml);
                }

                return Json(new { success = true, message = "Email inviate con successo!" });
            }
            catch (Exception ex)
            {
                // Log dell'eccezione
                return Json(new { success = false, message = "Errore durante l’invio delle email di recensione." });
            }
        }
    }
}