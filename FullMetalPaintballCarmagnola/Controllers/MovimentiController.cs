using System;
using System.Linq;
using System.Threading.Tasks;
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

            var partite = await _db.Partite
                .Where(p => p.CaparraConfermata == true && p.Data.Date <= oggi.Date)
                .OrderByDescending(p => p.Data)
                .ToListAsync();

            var movimentiPartita = await _db.MovimentiPartita.ToListAsync();
            var movimentiExtra = await _db.MovimentiExtra.ToListAsync();

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
                var data = DateTime.SpecifyKind(DateTime.Parse(movimento.GetProperty("Data").GetString()), DateTimeKind.Utc);
                var ora = TimeSpan.Parse(movimento.GetProperty("Ora").GetString());
                var note = movimento.GetProperty("Note").GetString();

                decimal? dare = movimento.TryGetProperty("Dare", out var dareEl) && dareEl.ValueKind != JsonValueKind.Null ? dareEl.GetDecimal() : null;
                decimal? avere = movimento.TryGetProperty("Avere", out var avereEl) && avereEl.ValueKind != JsonValueKind.Null ? avereEl.GetDecimal() : null;
                decimal? dareBis = movimento.TryGetProperty("DareBis", out var dareBisEl) && dareBisEl.ValueKind != JsonValueKind.Null ? dareBisEl.GetDecimal() : null;
                decimal? avereBis = movimento.TryGetProperty("AvereBis", out var avereBisEl) && avereBisEl.ValueKind != JsonValueKind.Null ? avereBisEl.GetDecimal() : null;

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
                return Ok();
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        [HttpDelete]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminaExtra(int id)
        {
            var movimento = await _db.MovimentiExtra.FindAsync(id);
            if (movimento == null) return NotFound();

            _db.MovimentiExtra.Remove(movimento);
            await _db.SaveChangesAsync();
            return Ok();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Salva([FromBody] MovimentiViewModel movimento)
        {
            if (movimento == null || movimento.PartitaId <= 0)
                return BadRequest(new { message = "Dati non validi" });

            var esistente = await _db.MovimentiPartita.FirstOrDefaultAsync(m => m.PartitaId == movimento.PartitaId);

            if (esistente != null)
            {
                esistente.Dare = movimento.Dare;
                esistente.Avere = movimento.Avere;
                esistente.DareBis = movimento.DareBis;
                esistente.AvereBis = movimento.AvereBis;
                esistente.Note = movimento.Note;
                _db.MovimentiPartita.Update(esistente);
            }
            else
            {
                _db.MovimentiPartita.Add(new MovimentoPartita
                {
                    PartitaId = movimento.PartitaId,
                    Dare = movimento.Dare,
                    Avere = movimento.Avere,
                    DareBis = movimento.DareBis,
                    AvereBis = movimento.AvereBis,
                    Note = movimento.Note
                });
            }

            await _db.SaveChangesAsync();
            return Ok();
        }
    }
}
