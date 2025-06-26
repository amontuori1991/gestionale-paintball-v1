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

        public TessereAcsiController(TesseramentoDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<IActionResult> Index(long? numeroTessera)
        {
            // Prendi tutte le tessere (assegnate e non)
            var allRanges = await _dbContext.RangeTessereAcsi.ToListAsync();

            // Se c'è un filtro per numero tessera, filtriamo
            if (numeroTessera.HasValue)
            {
                allRanges = allRanges.Where(r => numeroTessera.Value >= r.NumeroDa && numeroTessera.Value <= r.NumeroA).ToList();
            }

            return View(allRanges);
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddRange(long NumeroIniziale, long NumeroFinale)
        {
            if (NumeroFinale < NumeroIniziale)
            {
                ModelState.AddModelError("", "Il numero finale deve essere maggiore o uguale al numero iniziale.");
                return View("Index", await _dbContext.RangeTessereAcsi.ToListAsync());
            }

            // Genera tutte le tessere singole nel range
            for (long num = NumeroIniziale; num <= NumeroFinale; num++)
            {
                var tesseraSingola = new RangeTessereAcsi
                {
                    NumeroDa = num,
                    NumeroA = num,
                    Assegnata = false
                };
                _dbContext.RangeTessereAcsi.Add(tesseraSingola);
            }
            await _dbContext.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }


    }
}
