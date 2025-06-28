using System; // Per Exception
using System.Linq; // Per SelectMany in ModelState
using System.Threading.Tasks; // Assicurati di avere questo using
using Full_Metal_Paintball_Carmagnola.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

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

        public async Task<IActionResult> Index()
        {
            var oggi = DateTime.SpecifyKind(DateTime.Today, DateTimeKind.Utc);

            // Recupera partite confermate e non cancellate
            var partite = await _db.Partite
                .Where(p => p.CaparraConfermata == true && p.Data.Date <= oggi.Date)
                .OrderByDescending(p => p.Data)
                .ToListAsync();

            var movimentiPartita = await _db.MovimentiPartita.ToListAsync();
            var movimentiExtra = await _db.MovimentiExtra.ToListAsync();

            // Mappa le partite
            var listaPartite = partite.Select(p => new MovimentiViewModel
            {
                PartitaId = p.Id,
                Data = p.Data,
                Ora = p.OraInizio,
                Caparra = p.Caparra,
                MetodoCaparra = p.MetodoPagamentoCaparra,
                Dare = movimentiPartita.FirstOrDefault(m => m.PartitaId == p.Id)?.Dare,
                Avere = movimentiPartita.FirstOrDefault(m => m.PartitaId == p.Id)?.Avere,
                DareBis = movimentiPartita.FirstOrDefault(m => m.PartitaId == p.Id)?.DareBis,
                AvereBis = movimentiPartita.FirstOrDefault(m => m.PartitaId == p.Id)?.AvereBis,
                Note = movimentiPartita.FirstOrDefault(m => m.PartitaId == p.Id)?.Note,
                Stato = p.IsDeleted ? "Cancellata" : "Confermata"
            }).ToList();

            // Mappa i movimenti extra
            var listaExtra = movimentiExtra.Select(m => new MovimentiViewModel
            {
                PartitaId = 0,
                ExtraId = m.Id,
                Data = m.Data,
                Ora = m.Ora,
                Caparra = 0,
                MetodoCaparra = string.Empty,
                Dare = m.Dare,
                Avere = m.Avere,
                DareBis = m.DareBis,
                AvereBis = m.AvereBis,
                Note = m.Note,
                Stato = "Movimento Manuale"
            }).ToList();


            // Unisci e ordina
            var model = listaPartite.Concat(listaExtra)
                .OrderByDescending(m => m.Data)
                .ThenByDescending(m => m.Ora)
                .ToList();

            return View(model);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AggiungiExtra([FromBody] JsonElement movimento)
        {
            try
            {
                Console.WriteLine($"JSON grezzo ricevuto: {movimento}");

                // Parsing dei campi
                var dataString = movimento.GetProperty("Data").GetString();
                var oraString = movimento.GetProperty("Ora").GetString();
                var note = movimento.GetProperty("Note").GetString();

                DateTime data = DateTime.SpecifyKind(DateTime.Parse(dataString), DateTimeKind.Utc);
                TimeSpan ora = TimeSpan.Parse(oraString);

                decimal? dare = movimento.TryGetProperty("Dare", out JsonElement dareEl) && dareEl.ValueKind != JsonValueKind.Null
                                ? dareEl.GetDecimal()
                                : (decimal?)null;

                decimal? avere = movimento.TryGetProperty("Avere", out JsonElement avereEl) && avereEl.ValueKind != JsonValueKind.Null
                                ? avereEl.GetDecimal()
                                : (decimal?)null;

                decimal? dareBis = movimento.TryGetProperty("DareBis", out JsonElement dareBisEl) && dareBisEl.ValueKind != JsonValueKind.Null
                                ? dareBisEl.GetDecimal()
                                : (decimal?)null;

                decimal? avereBis = movimento.TryGetProperty("AvereBis", out JsonElement avereBisEl) && avereBisEl.ValueKind != JsonValueKind.Null
                                ? avereBisEl.GetDecimal()
                                : (decimal?)null;

                var nuovo = new MovimentoExtra
                {
                    Data = data,
                    Ora = ora,
                    Dare = dare,
                    Avere = avere,
                    DareBis = dareBis,
                    AvereBis = avereBis,
                    Note = note
                };

                _db.MovimentiExtra.Add(nuovo);
                await _db.SaveChangesAsync();

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERRORE durante l'inserimento di MovimentoExtra: {ex.Message}");
                Console.WriteLine($"StackTrace: {ex.StackTrace}");
                return StatusCode(500, new { message = "Errore interno durante il salvataggio.", error = ex.Message });
            }
        }


        [HttpDelete]
[ValidateAntiForgeryToken]
public async Task<IActionResult> EliminaExtra(int id)
{
    try
    {
        var movimento = await _db.MovimentiExtra.FindAsync(id);
        if (movimento == null)
            return NotFound();

        _db.MovimentiExtra.Remove(movimento);
        await _db.SaveChangesAsync();

        return Ok(new { success = true });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"ERRORE eliminazione MovimentoExtra: {ex.Message}");
        return StatusCode(500, new { message = "Errore interno durante l'eliminazione." });
    }
}



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