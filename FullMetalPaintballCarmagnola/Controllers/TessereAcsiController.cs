using Full_Metal_Paintball_Carmagnola.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Full_Metal_Paintball_Carmagnola.Controllers
{
    [Authorize(Policy = "ACSI")]
    public class TessereAcsiController : Controller
    {
        private readonly TesseramentoDbContext _dbContext;

        private static DateTime OggiUtc => DateTime.UtcNow.Date;

        public TessereAcsiController(TesseramentoDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<IActionResult> Index(long? numeroTessera)
        {
            var oggi = OggiUtc;

            var query = _dbContext.RangeTessereAcsi
                .Where(r => !r.DataValidita.HasValue || r.DataValidita.Value.Date >= oggi);

            if (numeroTessera.HasValue)
            {
                query = query.Where(r => numeroTessera.Value >= r.NumeroDa && numeroTessera.Value <= r.NumeroA);
            }

            var allRanges = await query
                .OrderBy(r => r.NumeroDa)
                .ToListAsync();

            // Trova le tessere già assegnate a un tesseramento
            var tessereAssegnate = await _dbContext.Tesseramenti
                .Where(t => !string.IsNullOrEmpty(t.Tessera))
                .Select(t => t.Tessera)
                .ToListAsync();

            ViewBag.TessereAssegnate = tessereAssegnate;

            return View(allRanges);
        }




        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddRange(long NumeroIniziale, long NumeroFinale, DateTime? DataValidita)
        {
            if (NumeroFinale < NumeroIniziale)
            {
                ModelState.AddModelError("", "Il numero finale deve essere maggiore o uguale al numero iniziale.");
                await PopolaTessereAssegnateAsync();
                return View("Index", await GetTessereValideAsync());
            }

            if (!DataValidita.HasValue)
            {
                ModelState.AddModelError("", "La data di validità è obbligatoria.");
                await PopolaTessereAssegnateAsync();
                return View("Index", await GetTessereValideAsync());
            }

            var dataValiditaUtc = DateTime.SpecifyKind(DataValidita.Value.Date, DateTimeKind.Utc);
            if (dataValiditaUtc < OggiUtc)
            {
                ModelState.AddModelError("", "La data di validità non può essere già superata.");
                await PopolaTessereAssegnateAsync();
                return View("Index", await GetTessereValideAsync());
            }

            // Genera tutte le tessere singole nel range
            for (long num = NumeroIniziale; num <= NumeroFinale; num++)
            {
                var tesseraSingola = new RangeTessereAcsi
                {
                    NumeroDa = num,
                    NumeroA = num,
                    DataValidita = dataValiditaUtc,
                    Assegnata = false
                };
                _dbContext.RangeTessereAcsi.Add(tesseraSingola);
            }
            await _dbContext.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        private async Task<List<RangeTessereAcsi>> GetTessereValideAsync()
        {
            var oggi = OggiUtc;

            return await _dbContext.RangeTessereAcsi
                .Where(r => !r.DataValidita.HasValue || r.DataValidita.Value.Date >= oggi)
                .OrderBy(r => r.NumeroDa)
                .ToListAsync();
        }

        private async Task PopolaTessereAssegnateAsync()
        {
            ViewBag.TessereAssegnate = await _dbContext.Tesseramenti
                .Where(t => !string.IsNullOrEmpty(t.Tessera))
                .Select(t => t.Tessera)
                .ToListAsync();
        }


    }
}
