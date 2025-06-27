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
            var allRanges = await _dbContext.RangeTessereAcsi.ToListAsync();

            if (numeroTessera.HasValue)
            {
                allRanges = allRanges.Where(r => numeroTessera.Value >= r.NumeroDa && numeroTessera.Value <= r.NumeroA).ToList();
            }

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
