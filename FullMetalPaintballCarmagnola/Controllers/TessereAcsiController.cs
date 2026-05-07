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
        private static readonly DateTime DataAvvioFabbisognoTessereUtc =
            DateTime.SpecifyKind(new DateTime(2026, 5, 1), DateTimeKind.Utc);

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
            ViewBag.FabbisognoTessere = await ContaFabbisognoTessereAsync();

            return View(allRanges);
        }




        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddRange(long NumeroIniziale, long? NumeroFinale, int? QuantitaTessere, DateTime? DataValidita)
        {
            long numeroFinaleCalcolato;

            if (QuantitaTessere.HasValue)
            {
                if (QuantitaTessere.Value <= 0)
                {
                    ModelState.AddModelError("", "La quantità di tessere da aggiungere deve essere maggiore di zero.");
                    await PopolaTessereAssegnateAsync();
                    return View("Index", await GetTessereValideAsync());
                }

                try
                {
                    numeroFinaleCalcolato = checked(NumeroIniziale + QuantitaTessere.Value - 1);
                }
                catch (OverflowException)
                {
                    ModelState.AddModelError("", "Il range calcolato supera il limite massimo consentito.");
                    await PopolaTessereAssegnateAsync();
                    return View("Index", await GetTessereValideAsync());
                }
            }
            else
            {
                if (!NumeroFinale.HasValue)
                {
                    ModelState.AddModelError("", "Inserisci il numero finale oppure la quantità di tessere da aggiungere.");
                    await PopolaTessereAssegnateAsync();
                    return View("Index", await GetTessereValideAsync());
                }

                numeroFinaleCalcolato = NumeroFinale.Value;
            }

            if (numeroFinaleCalcolato < NumeroIniziale)
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
            for (long num = NumeroIniziale; num <= numeroFinaleCalcolato; num++)
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
            ViewBag.FabbisognoTessere = await ContaFabbisognoTessereAsync();
        }

        private static DateTime GetInizioFabbisognoAnnoCorrente()
        {
            var inizioAnnoCorrente = DateTime.SpecifyKind(new DateTime(DateTime.UtcNow.Year, 1, 1), DateTimeKind.Utc);
            return inizioAnnoCorrente > DataAvvioFabbisognoTessereUtc
                ? inizioAnnoCorrente
                : DataAvvioFabbisognoTessereUtc;
        }

        private async Task<int> ContaFabbisognoTessereAsync()
        {
            var inizio = GetInizioFabbisognoAnnoCorrente();
            var fine = DateTime.SpecifyKind(new DateTime(inizio.Year + 1, 1, 1), DateTimeKind.Utc);

            return await _dbContext.Partite
                .Where(p => !p.IsDeleted && p.Data >= inizio && p.Data < fine)
                .SumAsync(p => p.NumeroPartecipanti);
        }


    }
}
