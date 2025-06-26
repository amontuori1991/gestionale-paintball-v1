using System.Globalization;
using Full_Metal_Paintball_Carmagnola.Data;
using Full_Metal_Paintball_Carmagnola.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Full_Metal_Paintball_Carmagnola.Controllers
{
    [Authorize(Policy = "Statistiche")]
    public class StatisticheController : Controller
    {
        private readonly TesseramentoDbContext _dbContext;

        public StatisticheController(TesseramentoDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<IActionResult> Index()
        {
            // Partite confermate (non cancellate)
            var partiteDb = await _dbContext.Partite
                .Where(p => !p.IsDeleted)
                .ToListAsync();

            // Partite cancellate
            var partiteCancellateDb = await _dbContext.Partite
                .Where(p => p.IsDeleted)
                .ToListAsync();

            var datiStorici = new Dictionary<string, Dictionary<string, int>>
            {
                ["2022"] = new()
                {
                    ["01"] = 2,
                    ["02"] = 2,
                    ["03"] = 11,
                    ["04"] = 7,
                    ["05"] = 8,
                    ["06"] = 10,
                    ["07"] = 10,
                    ["08"] = 2,
                    ["09"] = 10,
                    ["10"] = 9,
                    ["11"] = 7,
                    ["12"] = 0
                },
                ["2023"] = new()
                {
                    ["01"] = 1,
                    ["02"] = 2,
                    ["03"] = 6,
                    ["04"] = 10,
                    ["05"] = 10,
                    ["06"] = 12,
                    ["07"] = 10,
                    ["08"] = 1,
                    ["09"] = 14,
                    ["10"] = 11,
                    ["11"] = 6,
                    ["12"] = 3
                },
                ["2024"] = new()
                {
                    ["01"] = 3,
                    ["02"] = 7,
                    ["03"] = 6,
                    ["04"] = 9,
                    ["05"] = 18,
                    ["06"] = 11,
                    ["07"] = 6,
                    ["08"] = 5,
                    ["09"] = 11,
                    ["10"] = 6,
                    ["11"] = 3,
                    ["12"] = 2
                }
            };

            var dati2025 = new Dictionary<string, int>
            {
                ["01"] = 1,
                ["02"] = 4,
                ["03"] = 7,
                ["04"] = 11,
                ["05"] = 13
            };

            // Partite confermate giugno 2025 (fisse + db)
            var partiteGiugnoDb = partiteDb.Count(p => p.Data.Year == 2025 && p.Data.Month == 6);
            dati2025["06"] = 5 + partiteGiugnoDb;

            // Luglio → Dicembre 2025 confermate
            for (int mese = 7; mese <= 12; mese++)
            {
                string key = mese.ToString("D2");
                int count = partiteDb.Count(p => p.Data.Year == 2025 && p.Data.Month == mese);
                dati2025[key] = count;
            }

            datiStorici["2025"] = dati2025;

            // Ora calcola dati cancellati per anno e mese (simile a sopra)
            var datiCancellati = new Dictionary<string, Dictionary<string, int>>();

            // Prendi tutti gli anni presenti fra confermate + cancellate
            var anniTutti = datiStorici.Keys.Union(
                partiteCancellateDb.Select(p => p.Data.Year.ToString()).Distinct()
            ).Distinct().OrderBy(a => a).ToList();

            foreach (var anno in anniTutti)
            {
                var annoCancellati = new Dictionary<string, int>();
                for (int mese = 1; mese <= 12; mese++)
                {
                    string key = mese.ToString("D2");
                    int count = partiteCancellateDb.Count(p => p.Data.Year.ToString() == anno && p.Data.Month == mese);
                    annoCancellati[key] = count;
                }
                datiCancellati[anno] = annoCancellati;
            }

            // Etichette mesi
            var mesi = Enumerable.Range(1, 12)
                .Select(m => CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(m))
                .ToList();

            var anni = anniTutti;

            ViewBag.Labels = mesi;
            ViewBag.Anni = anni;
            ViewBag.Dati = datiStorici;       // dati confermati (già usati)
            ViewBag.DatiCancellati = datiCancellati; // dati partite cancellate

            return View();
        }
    }
}
