using Full_Metal_Paintball_Carmagnola.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks; // Assicurati di avere questo using
using System; // Per Exception
using System.Linq; // Per SelectMany in ModelState

namespace Full_Metal_Paintball_Carmagnola.Controllers
{
    [Authorize(Policy = "Movimenti")]
    public class MovimentiController : Controller
    {
        private readonly TesseramentoDbContext _db;

        public MovimentiController(TesseramentoDbContext db)
        {
            _db = db;
        }

        public async Task<IActionResult> Index(int? annoFiltro, int? meseFiltro)
        {
            var oggi = DateTime.SpecifyKind(DateTime.Today, DateTimeKind.Utc);

            var query = _db.Partite
                .Where(p => p.CaparraConfermata == true && p.Data.Date <= oggi.Date)
                .OrderByDescending(p => p.Data)
                .AsQueryable();

            if (annoFiltro.HasValue)
            {
                query = query.Where(p => p.Data.Year == annoFiltro.Value);
            }

            if (meseFiltro.HasValue)
            {
                query = query.Where(p => p.Data.Month == meseFiltro.Value);
            }

            var partite = await query.ToListAsync();
            var movimenti = await _db.MovimentiPartita.ToListAsync();

            var model = partite.Select(p => new MovimentiViewModel
            {
                PartitaId = p.Id,
                Stato = p.IsDeleted ? "Cancellata" : "Confermata",
                Data = p.Data,
                Ora = p.OraInizio,
                Caparra = p.Caparra,
                MetodoCaparra = p.MetodoPagamentoCaparra,
                // Recupera i valori dal movimento associato, se esiste
                Dare = movimenti.FirstOrDefault(m => m.PartitaId == p.Id)?.Dare,
                Avere = movimenti.FirstOrDefault(m => m.PartitaId == p.Id)?.Avere,
                DareBis = movimenti.FirstOrDefault(m => m.PartitaId == p.Id)?.DareBis,
                AvereBis = movimenti.FirstOrDefault(m => m.PartitaId == p.Id)?.AvereBis,
                Note = movimenti.FirstOrDefault(m => m.PartitaId == p.Id)?.Note
            }).ToList();

            ViewBag.AnniDisponibili = _db.Partite
                .Where(p => p.Data < oggi && p.CaparraConfermata == true)
                .Select(p => p.Data.Year)
                .Distinct()
                .OrderByDescending(y => y)
                .ToList();

            ViewBag.AnnoFiltro = annoFiltro;
            ViewBag.MeseFiltro = meseFiltro;

            return View(model);
        }

        // --- Modifiche qui ---
        [HttpPost]
        [ValidateAntiForgeryToken] // RICHIESTO per il token anti-forgery
        public async Task<IActionResult> Salva([FromBody] MovimentiViewModel movimento) // RICHIESTO per ricevere JSON
        {
            // Debug: Stampa i dati ricevuti nella console del server (output di Visual Studio)
            Console.WriteLine($"Received PartitaId: {movimento?.PartitaId}");
            Console.WriteLine($"Received Dare: {movimento?.Dare}");
            Console.WriteLine($"Received Avere: {movimento?.Avere}");
            Console.WriteLine($"Received Note: {movimento?.Note}");

            if (movimento == null || movimento.PartitaId <= 0)
            {
                // Restituisci un errore più specifico al client
                return BadRequest(new { message = "Dati del movimento non validi o ID Partita mancante." });
            }

            // Aggiungi qui la validazione del modello se hai attributi [Required] ecc. in MovimentiViewModel
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                               .SelectMany(v => v.Errors)
                               .Select(e => e.ErrorMessage)
                               .ToList();
                // Restituisci gli errori di validazione in formato JSON
                return BadRequest(new { message = "Errore di validazione del modello.", errors = errors });
            }

            try
            {
                var esistente = await _db.MovimentiPartita.FirstOrDefaultAsync(m => m.PartitaId == movimento.PartitaId);

                if (esistente != null)
                {
                    // Aggiorna solo le proprietà rilevanti dal ViewModel
                    esistente.Dare = movimento.Dare;
                    esistente.Avere = movimento.Avere;
                    esistente.DareBis = movimento.DareBis;
                    esistente.AvereBis = movimento.AvereBis;
                    esistente.Note = movimento.Note;

                    _db.MovimentiPartita.Update(esistente);
                }
                else
                {
                    // Crea un nuovo MovimentoPartita se non esiste
                    var nuovo = new MovimentoPartita
                    {
                        PartitaId = movimento.PartitaId,
                        Dare = movimento.Dare,
                        Avere = movimento.Avere,
                        DareBis = movimento.DareBis,
                        AvereBis = movimento.AvereBis,
                        Note = movimento.Note
                    };
                    _db.MovimentiPartita.Add(nuovo);
                }

                await _db.SaveChangesAsync(); // Salva le modifiche nel database
                return Ok(new { success = true, message = "Movimento salvato con successo." }); // Messaggio di successo
            }
            catch (Exception ex)
            {
                // Logga l'eccezione completa nel server per il debug
                Console.WriteLine($"ERRORE durante il salvataggio del movimento (ID Partita: {movimento.PartitaId}): {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                // Restituisci un errore 500 con un messaggio descrittivo al client
                return StatusCode(500, new { message = "Si è verificato un errore interno durante il salvataggio del movimento.", error = ex.Message });
            }
        }
    }
}