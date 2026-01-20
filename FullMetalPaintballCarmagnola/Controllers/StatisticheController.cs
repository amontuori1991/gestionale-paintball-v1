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
            // Usa cultura IT per nomi mesi coerenti
            var it = new CultureInfo("it-IT");

            int meseCorrente = mese ?? DateTime.Now.Month;
            int annoCorrente = DateTime.Now.Year;
            int annoPrecedente = annoCorrente - 1;

            // 1) Dati storici manuali (li lasciamo, ma NON blocchiamo gli anni futuri)
            //    NOTA: questi rappresentano "confermate".
            var datiStoriciManuali = new Dictionary<int, Dictionary<int, int>>
            {
                [2022] = new() { [1] = 2, [2] = 2, [3] = 11, [4] = 7, [5] = 8, [6] = 10, [7] = 10, [8] = 2, [9] = 10, [10] = 9, [11] = 7, [12] = 0 },
                [2023] = new() { [1] = 1, [2] = 2, [3] = 6, [4] = 10, [5] = 10, [6] = 12, [7] = 10, [8] = 1, [9] = 14, [10] = 11, [11] = 6, [12] = 3 },
                [2024] = new() { [1] = 3, [2] = 7, [3] = 6, [4] = 9, [5] = 18, [6] = 11, [7] = 6, [8] = 5, [9] = 11, [10] = 6, [11] = 3, [12] = 2 },

                // Se vuoi tenere anche un "baseline" 2025 manuale, puoi.
                // Il DB poi si somma sopra (utile se 2025 nei primi mesi non era tracciato).
                [2025] = new() { [1] = 1, [2] = 4, [3] = 7, [4] = 11, [5] = 13, [6] = 5 }
            };

            // 2) Dati dal DB (confermate reali)
            //    Qui è la parte che ti mancava: ora 2026 (e oltre) entra in automatico.
            var confermateDbGrouped = await _dbContext.Partite
                .AsNoTracking()
                .Where(p => p.CaparraConfermata && !p.IsDeleted)
                .GroupBy(p => new { p.Data.Year, p.Data.Month })
                .Select(g => new
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    Count = g.Count()
                })
                .ToListAsync();

            // 3) Merge: manuale + DB (sommiamo, così non perdi il “baseline” storico)
            var confermate = new Dictionary<int, Dictionary<int, int>>();

            void EnsureYearMonth(int year, int month)
            {
                if (!confermate.ContainsKey(year))
                    confermate[year] = new Dictionary<int, int>();
                if (!confermate[year].ContainsKey(month))
                    confermate[year][month] = 0;
            }

            // manuale
            foreach (var y in datiStoriciManuali)
            {
                foreach (var m in y.Value)
                {
                    EnsureYearMonth(y.Key, m.Key);
                    confermate[y.Key][m.Key] += m.Value;
                }
            }

            // db
            foreach (var row in confermateDbGrouped)
            {
                EnsureYearMonth(row.Year, row.Month);
                confermate[row.Year][row.Month] += row.Count;
            }

            // 4) Lista anni presenti (ordinati)
            var anni = confermate.Keys.OrderBy(x => x).ToList();

            // 5) Calcoli per i grafici richiesti

            // Grafico 1: solo 2 barre (TOP storico vs anno corrente) per il mese selezionato
            int currentYearMonthValue = confermate.ContainsKey(annoCorrente) && confermate[annoCorrente].ContainsKey(meseCorrente)
                ? confermate[annoCorrente][meseCorrente]
                : 0;

            // TOP tra gli anni precedenti (anni < annoCorrente)
            var anniPrecedenti = anni.Where(a => a < annoCorrente).ToList();

            int topYearForMonth = 0;
            int topValueForMonth = 0;

            foreach (var a in anniPrecedenti)
            {
                int val = confermate.ContainsKey(a) && confermate[a].ContainsKey(meseCorrente) ? confermate[a][meseCorrente] : 0;
                if (val > topValueForMonth)
                {
                    topValueForMonth = val;
                    topYearForMonth = a;
                }
            }

            // Se l'anno corrente supera il TOP, diventa lui il TOP
            // TOP sempre e solo tra anni precedenti (escludendo l'anno corrente)
            bool hasPreviousYears = anniPrecedenti.Any();

            // Se non ci sono anni precedenti, il "TOP precedente" è 0 (grafico ok comunque)
            int topPrevYearForMonth = topYearForMonth;   // già calcolato solo su anniPrecedenti
            int topPrevValueForMonth = topValueForMonth; // già calcolato solo su anniPrecedenti

            // L'anno corrente è TOP se supera il TOP precedente (o se non esiste storico)
            bool currentIsTopForMonth =
                (!hasPreviousYears && currentYearMonthValue > 0) ||
                (hasPreviousYears && currentYearMonthValue > topPrevValueForMonth);


            // Grafico 2: mese anno corrente vs stesso mese anno precedente
            int prevYearMonthValue = confermate.ContainsKey(annoPrecedente) && confermate[annoPrecedente].ContainsKey(meseCorrente)
                ? confermate[annoPrecedente][meseCorrente]
                : 0;

            // Grafico 3: totali raggruppati per anno + TOP assoluto + anno corrente
            var totalsPerYear = new Dictionary<int, int>();
            foreach (var a in anni)
            {
                int sum = confermate[a].Values.Sum();
                totalsPerYear[a] = sum;
            }

            int topYearOverall = 0;
            int topOverallValue = 0;
            foreach (var kv in totalsPerYear)
            {
                if (kv.Value > topOverallValue)
                {
                    topOverallValue = kv.Value;
                    topYearOverall = kv.Key;
                }
            }

            // 6) ViewBag per la View
            ViewBag.MeseCorrente = meseCorrente;
            ViewBag.AnnoCorrente = annoCorrente;
            ViewBag.AnnoPrecedente = annoPrecedente;
            ViewBag.LabelMese = it.DateTimeFormat.GetMonthName(meseCorrente);

            // Chart 1
            ViewBag.Chart1TopPrevYear = topPrevYearForMonth;
            ViewBag.Chart1TopPrevValue = topPrevValueForMonth;

            ViewBag.Chart1CurrentYear = annoCorrente;
            ViewBag.Chart1CurrentValue = currentYearMonthValue;
            ViewBag.Chart1CurrentIsTop = currentIsTopForMonth;


            // Chart 2
            ViewBag.Chart2CurrentValue = currentYearMonthValue;
            ViewBag.Chart2PrevValue = prevYearMonthValue;

            // Chart 3
            ViewBag.Anni = anni;
            ViewBag.TotalsPerYear = totalsPerYear;
            ViewBag.TopYearOverall = topYearOverall;
            ViewBag.TopOverallValue = topOverallValue;

            return View();
        }
    }
}
