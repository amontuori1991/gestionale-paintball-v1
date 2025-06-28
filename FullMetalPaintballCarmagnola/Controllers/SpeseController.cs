using Full_Metal_Paintball_Carmagnola.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;

namespace Full_Metal_Paintball_Carmagnola.Controllers
{
    [Authorize(Policy = "Spese")]
    public class SpeseController : Controller
    {
        private readonly TesseramentoDbContext _db;

        public SpeseController(TesseramentoDbContext db)
        {
            _db = db;
        }

        public async Task<IActionResult> Index()
        {
            var spese = await _db.Spese.OrderByDescending(s => s.Data).ThenByDescending(s => s.Ora).ToListAsync();
            return View(spese);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Aggiungi([FromBody] Spesa nuova)
        {
            if (nuova == null || string.IsNullOrWhiteSpace(nuova.Descrizione) || nuova.Importo <= 0)
                return BadRequest(new { message = "Dati della spesa non validi." });

            nuova.Data = DateTime.SpecifyKind(nuova.Data, DateTimeKind.Utc);

            if (!string.IsNullOrEmpty(nuova.Riferimento))
            {
                var validi = new[] { "Montuo", "Bosax", "Flavio" };
                if (!Array.Exists(validi, v => v.Equals(nuova.Riferimento, StringComparison.OrdinalIgnoreCase)))
                    return BadRequest(new { message = "Il riferimento inserito non è valido." });
            }

            _db.Spese.Add(nuova);
            await _db.SaveChangesAsync();
            return Ok(new { message = "Spesa aggiunta con successo." });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AggiornaRimborsato([FromBody] RimborsatoDTO dati)
        {
            if (dati == null || dati.Id <= 0)
                return BadRequest(new { message = "Dati non validi." });

            try
            {
                var spesa = await _db.Spese.FindAsync(dati.Id);
                if (spesa == null)
                    return NotFound();

                Console.WriteLine($"Salvataggio Spesa ID: {dati.Id}, Nuovo valore Rimborsato: {dati.Rimborsato}");

                spesa.Rimborsato = dati.Rimborsato;

                // Correzione per il problema DateTime Unspecified
                if (spesa.Data.Kind == DateTimeKind.Unspecified)
                    spesa.Data = DateTime.SpecifyKind(spesa.Data, DateTimeKind.Utc);

                _db.Spese.Update(spesa);
                await _db.SaveChangesAsync();

                Console.WriteLine($"Salvataggio completato senza eccezioni.");
                return Ok(new { message = "Stato rimborsato aggiornato." });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore aggiornamento rimborsato: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"InnerException: {ex.InnerException.Message}");
                }
                return StatusCode(500, new { message = "Errore interno durante il salvataggio.", error = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Elimina(int id)
        {
            var spesa = await _db.Spese.FindAsync(id);
            if (spesa == null)
                return NotFound();

            try
            {
                _db.Spese.Remove(spesa);
                await _db.SaveChangesAsync();
                return Ok(new { message = "Spesa eliminata con successo." });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore eliminazione spesa: {ex.Message}");
                return StatusCode(500, new { message = "Errore interno durante l'eliminazione." });
            }
        }
    }
}
