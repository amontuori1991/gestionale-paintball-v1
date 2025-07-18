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

        public async Task<IActionResult> Index(int? mese)
        {
            int meseCorrente = mese ?? DateTime.Now.Month;

            var partiteDb = await _dbContext.Partite.Where(p => !p.IsDeleted).ToListAsync();
            var partiteCancellateDb = await _dbContext.Partite.Where(p => p.IsDeleted).ToListAsync();

            var datiStorici = new Dictionary<string, Dictionary<string, int>>
            {
                ["2022"] = new() { ["01"] = 2, ["02"] = 2, ["03"] = 11, ["04"] = 7, ["05"] = 8, ["06"] = 10, ["07"] = 10, ["08"] = 2, ["09"] = 10, ["10"] = 9, ["11"] = 7, ["12"] = 0 },
                ["2023"] = new() { ["01"] = 1, ["02"] = 2, ["03"] = 6, ["04"] = 10, ["05"] = 10, ["06"] = 12, ["07"] = 10, ["08"] = 1, ["09"] = 14, ["10"] = 11, ["11"] = 6, ["12"] = 3 },
                ["2024"] = new() { ["01"] = 3, ["02"] = 7, ["03"] = 6, ["04"] = 9, ["05"] = 18, ["06"] = 11, ["07"] = 6, ["08"] = 5, ["09"] = 11, ["10"] = 6, ["11"] = 3, ["12"] = 2 }
            };

            var dati2025 = new Dictionary<string, int> { ["01"] = 1, ["02"] = 4, ["03"] = 7, ["04"] = 11, ["05"] = 13 };
            var partiteGiugnoDb = partiteDb.Count(p => p.Data.Year == 2025 && p.Data.Month == 6);
            dati2025["06"] = 5 + partiteGiugnoDb;

            for (int m = 7; m <= 12; m++)
            {
                string key = m.ToString("D2");
                int count = partiteDb.Count(p => p.Data.Year == 2025 && p.Data.Month == m);
                dati2025[key] = count;
            }

            datiStorici["2025"] = dati2025;

            var datiCancellati = new Dictionary<string, Dictionary<string, int>>();
            var anniTutti = datiStorici.Keys
                .Union(partiteCancellateDb.Select(p => p.Data.Year.ToString()).Distinct())
                .Distinct()
                .OrderBy(a => a)
                .ToList();

            foreach (var anno in anniTutti)
            {
                var annoCancellati = new Dictionary<string, int>();
                for (int m = 1; m <= 12; m++)
                {
                    string key = m.ToString("D2");
                    int count = partiteCancellateDb.Count(p => p.Data.Year.ToString() == anno && p.Data.Month == m);
                    annoCancellati[key] = count;
                }
                datiCancellati[anno] = annoCancellati;
            }

            var datiMese = new Dictionary<string, int>();
            var datiCancellatiMese = new Dictionary<string, int>();
            var annualTotals = new Dictionary<string, (int Confermate, int Cancellate)>();

            string meseKey = meseCorrente.ToString("D2");

            foreach (var anno in anniTutti)
            {
                int confermate = datiStorici.ContainsKey(anno) && datiStorici[anno].ContainsKey(meseKey) ? datiStorici[anno][meseKey] : 0;
                int cancellate = datiCancellati.ContainsKey(anno) && datiCancellati[anno].ContainsKey(meseKey) ? datiCancellati[anno][meseKey] : 0;
                datiMese[anno] = confermate;
                datiCancellatiMese[anno] = cancellate;

                int totaleAnnualeConf = datiStorici.ContainsKey(anno) ? datiStorici[anno].Values.Sum() : 0;
                int totaleAnnualeCanc = datiCancellati.ContainsKey(anno) ? datiCancellati[anno].Values.Sum() : 0;

                annualTotals[anno] = (totaleAnnualeConf, totaleAnnualeCanc);
            }

            ViewBag.LabelMese = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(meseCorrente);
            ViewBag.MeseCorrente = meseCorrente;
            ViewBag.Anni = anniTutti;
            ViewBag.DatiMese = datiMese;
            ViewBag.DatiCancellatiMese = datiCancellatiMese;
            ViewBag.AnnualTotals = annualTotals;

            return View();
        }

    }
}
