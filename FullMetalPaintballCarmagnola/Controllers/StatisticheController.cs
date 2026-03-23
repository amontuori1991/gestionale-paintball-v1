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

            // Data odierna (per calcolo YTD progressivo)
            // Usiamo UtcNow perché Neon/Npgsql richiede DateTimeKind.Utc per le colonne timestamptz.
            // La colonna p.Data è già in UTC sul DB, quindi il confronto è corretto.
            var oggi = DateTime.UtcNow.Date;
            int oggiGiorno = oggi.DayOfYear; // giorno progressivo dell'anno (1-366)

            // 1) Dati storici manuali (confermate)
            var datiStoriciManuali = new Dictionary<int, Dictionary<int, int>>
            {
                [2022] = new() { [1] = 2, [2] = 2, [3] = 11, [4] = 7, [5] = 8, [6] = 10, [7] = 10, [8] = 2, [9] = 10, [10] = 9, [11] = 7, [12] = 0 },
                [2023] = new() { [1] = 1, [2] = 2, [3] = 6, [4] = 10, [5] = 10, [6] = 12, [7] = 10, [8] = 1, [9] = 14, [10] = 11, [11] = 6, [12] = 3 },
                [2024] = new() { [1] = 3, [2] = 7, [3] = 6, [4] = 9, [5] = 18, [6] = 11, [7] = 6, [8] = 5, [9] = 11, [10] = 6, [11] = 3, [12] = 2 },
                [2025] = new() { [1] = 1, [2] = 4, [3] = 7, [4] = 11, [5] = 13, [6] = 5 }
            };

            // 2) Partite CONFERMATE dal DB (CaparraConfermata = true, IsDeleted = false)
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

            // 3) Partite CANCELLATE dal DB (IsDeleted = true) — raggruppate per anno/mese
            var cancellateDbGrouped = await _dbContext.Partite
                .AsNoTracking()
                .Where(p => p.IsDeleted)
                .GroupBy(p => new { p.Data.Year, p.Data.Month })
                .Select(g => new
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    Count = g.Count()
                })
                .ToListAsync();

            // 4) Partite CONFERMATE dal DB per YTD (anno corrente e precedente, fino al giorno odierno)
            //    Per l'anno precedente usiamo lo stesso giorno dell'anno (es. se oggi è 23 marzo 2026,
            //    confrontiamo con il periodo 01/01/2025 – 23/03/2025)
            // Entrambe le date devono essere Kind=Utc per Npgsql/Neon (timestamptz).
            var dataLimiteAnnoCorrente = oggi; // gia' Utc perche' deriva da DateTime.UtcNow.Date
            var dataLimiteAnnoPrecedente = DateTime.SpecifyKind(
                new DateTime(annoPrecedente, oggi.Month, oggi.Day), DateTimeKind.Utc);

            var confermateYtdCorrenteList = await _dbContext.Partite
                .AsNoTracking()
                .Where(p => p.CaparraConfermata && !p.IsDeleted
                    && p.Data.Year == annoCorrente
                    && p.Data <= dataLimiteAnnoCorrente)
                .GroupBy(p => p.Data.Month)
                .Select(g => new { Month = g.Key, Count = g.Count() })
                .ToListAsync();

            var confermateYtdPrecedenteList = await _dbContext.Partite
                .AsNoTracking()
                .Where(p => p.CaparraConfermata && !p.IsDeleted
                    && p.Data.Year == annoPrecedente
                    && p.Data <= dataLimiteAnnoPrecedente)
                .GroupBy(p => p.Data.Month)
                .Select(g => new { Month = g.Key, Count = g.Count() })
                .ToListAsync();

            // 5) Partite CANCELLATE per YTD
            var cancellateYtdCorrenteList = await _dbContext.Partite
                .AsNoTracking()
                .Where(p => p.IsDeleted
                    && p.Data.Year == annoCorrente
                    && p.Data <= dataLimiteAnnoCorrente)
                .GroupBy(p => p.Data.Month)
                .Select(g => new { Month = g.Key, Count = g.Count() })
                .ToListAsync();

            var cancellateYtdPrecedenteList = await _dbContext.Partite
                .AsNoTracking()
                .Where(p => p.IsDeleted
                    && p.Data.Year == annoPrecedente
                    && p.Data <= dataLimiteAnnoPrecedente)
                .GroupBy(p => p.Data.Month)
                .Select(g => new { Month = g.Key, Count = g.Count() })
                .ToListAsync();

            // 6) Merge confermate: manuale + DB
            var confermate = new Dictionary<int, Dictionary<int, int>>();

            void EnsureYearMonth(Dictionary<int, Dictionary<int, int>> dict, int year, int month)
            {
                if (!dict.ContainsKey(year))
                    dict[year] = new Dictionary<int, int>();
                if (!dict[year].ContainsKey(month))
                    dict[year][month] = 0;
            }

            foreach (var y in datiStoriciManuali)
                foreach (var m in y.Value)
                {
                    EnsureYearMonth(confermate, y.Key, m.Key);
                    confermate[y.Key][m.Key] += m.Value;
                }

            foreach (var row in confermateDbGrouped)
            {
                EnsureYearMonth(confermate, row.Year, row.Month);
                confermate[row.Year][row.Month] += row.Count;
            }

            // 7) Build dizionario cancellate (solo DB, no manuale)
            var cancellate = new Dictionary<int, Dictionary<int, int>>();

            foreach (var row in cancellateDbGrouped)
            {
                EnsureYearMonth(cancellate, row.Year, row.Month);
                cancellate[row.Year][row.Month] += row.Count;
            }

            // 8) Lista anni presenti (ordinati)
            var anni = confermate.Keys.OrderBy(x => x).ToList();

            // =====================================================================
            // Calcoli Grafici 1, 2, 3 (invariati rispetto alla logica originale)
            // =====================================================================

            int currentYearMonthValue = confermate.ContainsKey(annoCorrente) && confermate[annoCorrente].ContainsKey(meseCorrente)
                ? confermate[annoCorrente][meseCorrente] : 0;

            int currentYearMonthCancellate = cancellate.ContainsKey(annoCorrente) && cancellate[annoCorrente].ContainsKey(meseCorrente)
                ? cancellate[annoCorrente][meseCorrente] : 0;

            var anniPrecedenti = anni.Where(a => a < annoCorrente).ToList();

            int topYearForMonth = 0;
            int topValueForMonth = 0;
            int topCancellateForMonth = 0;

            foreach (var a in anniPrecedenti)
            {
                int val = confermate.ContainsKey(a) && confermate[a].ContainsKey(meseCorrente) ? confermate[a][meseCorrente] : 0;
                if (val > topValueForMonth)
                {
                    topValueForMonth = val;
                    topYearForMonth = a;
                    topCancellateForMonth = cancellate.ContainsKey(a) && cancellate[a].ContainsKey(meseCorrente) ? cancellate[a][meseCorrente] : 0;
                }
            }

            bool hasPreviousYears = anniPrecedenti.Any();
            int topPrevYearForMonth = topYearForMonth;
            int topPrevValueForMonth = topValueForMonth;

            bool currentIsTopForMonth =
                (!hasPreviousYears && currentYearMonthValue > 0) ||
                (hasPreviousYears && currentYearMonthValue > topPrevValueForMonth);

            // Chart 2
            int prevYearMonthValue = confermate.ContainsKey(annoPrecedente) && confermate[annoPrecedente].ContainsKey(meseCorrente)
                ? confermate[annoPrecedente][meseCorrente] : 0;
            int prevYearMonthCancellate = cancellate.ContainsKey(annoPrecedente) && cancellate[annoPrecedente].ContainsKey(meseCorrente)
                ? cancellate[annoPrecedente][meseCorrente] : 0;

            // Chart 3: totali per anno
            var totalsPerYear = new Dictionary<int, int>();
            var cancellatePerYear = new Dictionary<int, int>();
            foreach (var a in anni)
            {
                totalsPerYear[a] = confermate[a].Values.Sum();
                cancellatePerYear[a] = cancellate.ContainsKey(a) ? cancellate[a].Values.Sum() : 0;
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

            // =====================================================================
            // Calcoli Grafico 4: YTD progressivo (mesi da 1 a meseCorrente)
            // =====================================================================
            // Per ogni mese da gennaio fino al mese corrente, calcoliamo il valore
            // cumulato YTD sia per l'anno corrente sia per l'anno precedente.
            // Per il mese corrente dell'anno corrente: solo fino ad oggi (già filtrato in DB).
            // Per i mesi passati: tutto il mese.
            // Per l'anno precedente: fino allo stesso giorno dell'anno (già filtrato in DB).

            // Dizionari YTD mese -> count (dal DB)
            var ytdCorrenteMese = confermateYtdCorrenteList.ToDictionary(x => x.Month, x => x.Count);
            var ytdPrecedenteMese = confermateYtdPrecedenteList.ToDictionary(x => x.Month, x => x.Count);
            var ytdCancellateCorrenteMese = cancellateYtdCorrenteList.ToDictionary(x => x.Month, x => x.Count);
            var ytdCancellatePrecedenteMese = cancellateYtdPrecedenteList.ToDictionary(x => x.Month, x => x.Count);

            // Per l'anno precedente nei mesi "storici" (prima che il DB tracciasse),
            // integriamo con i dati manuali per i mesi interi (fino al mese precedente a quello corrente).
            // Logica: per i mesi 1..(meseCorrente-1) dell'anno precedente, se il DB non ha nulla,
            // usiamo i dati manuali. Per il mese corrente usiamo sempre DB (già filtrato).
            // In realtà il filtro DB già restituisce tutti i mesi fino al giorno limite,
            // quindi integriamo con manuali solo dove il DB dà 0 e i manuali hanno un valore.

            // Costruiamo array di mesi da visualizzare (1..meseCorrente)
            var mesiYtd = Enumerable.Range(1, meseCorrente).ToList();
            var labelsYtd = mesiYtd.Select(m => it.DateTimeFormat.GetMonthName(m)).ToList();

            // Valori per mese (non cumulati, sarà il JS a renderli se vogliamo linea cumulativa,
            // oppure li cumualiamo qui — scegliamo di cumulare lato C# per semplicità)
            var ytdCorrenteValues = new List<int>();
            var ytdPrecedenteValues = new List<int>();
            var ytdCancellateCorrenteValues = new List<int>();
            var ytdCancellatePrecedenteValues = new List<int>();

            int cumCorrente = 0;
            int cumPrecedente = 0;
            int cumCancellateCorrente = 0;
            int cumCancellatePrecedente = 0;

            foreach (var m in mesiYtd)
            {
                // Anno corrente: DB (già filtrato fino ad oggi per il mese corrente)
                int valCorrente = ytdCorrenteMese.ContainsKey(m) ? ytdCorrenteMese[m] : 0;

                // Anno precedente: DB (filtrato fino allo stesso giorno dell'anno scorso)
                // + dati manuali per i mesi interi non coperti da DB
                int valPrecedente = ytdPrecedenteMese.ContainsKey(m) ? ytdPrecedenteMese[m] : 0;
                // Integrazione manuale per anno precedente (mesi interi già passati nel confronto)
                if (valPrecedente == 0 && datiStoriciManuali.ContainsKey(annoPrecedente) && datiStoriciManuali[annoPrecedente].ContainsKey(m))
                {
                    // Solo per mesi completamente trascorsi nel periodo di confronto
                    // (tutti i mesi tranne l'ultimo se siamo a metà mese)
                    if (m < meseCorrente || (m == meseCorrente))
                        valPrecedente = datiStoriciManuali[annoPrecedente][m];
                }

                int valCancellateCorrente = ytdCancellateCorrenteMese.ContainsKey(m) ? ytdCancellateCorrenteMese[m] : 0;
                int valCancellatePrecedente = ytdCancellatePrecedenteMese.ContainsKey(m) ? ytdCancellatePrecedenteMese[m] : 0;

                cumCorrente += valCorrente;
                cumPrecedente += valPrecedente;
                cumCancellateCorrente += valCancellateCorrente;
                cumCancellatePrecedente += valCancellatePrecedente;

                ytdCorrenteValues.Add(cumCorrente);
                ytdPrecedenteValues.Add(cumPrecedente);
                ytdCancellateCorrenteValues.Add(cumCancellateCorrente);
                ytdCancellatePrecedenteValues.Add(cumCancellatePrecedente);
            }

            // =====================================================================
            // ViewBag
            // =====================================================================
            ViewBag.MeseCorrente = meseCorrente;
            ViewBag.AnnoCorrente = annoCorrente;
            ViewBag.AnnoPrecedente = annoPrecedente;
            ViewBag.LabelMese = it.DateTimeFormat.GetMonthName(meseCorrente);
            ViewBag.OggiLabel = oggi.ToString("dd/MM/yyyy");

            // Chart 1
            ViewBag.Chart1TopPrevYear = topPrevYearForMonth;
            ViewBag.Chart1TopPrevValue = topPrevValueForMonth;
            ViewBag.Chart1TopPrevCancellate = topCancellateForMonth;
            ViewBag.Chart1CurrentYear = annoCorrente;
            ViewBag.Chart1CurrentValue = currentYearMonthValue;
            ViewBag.Chart1CurrentIsTop = currentIsTopForMonth;
            ViewBag.Chart1CurrentCancellate = currentYearMonthCancellate;

            // Chart 2
            ViewBag.Chart2CurrentValue = currentYearMonthValue;
            ViewBag.Chart2PrevValue = prevYearMonthValue;
            ViewBag.Chart2CurrentCancellate = currentYearMonthCancellate;
            ViewBag.Chart2PrevCancellate = prevYearMonthCancellate;

            // Chart 3
            ViewBag.Anni = anni;
            ViewBag.TotalsPerYear = totalsPerYear;
            ViewBag.CancellatePerYear = cancellatePerYear;
            ViewBag.TopYearOverall = topYearOverall;
            ViewBag.TopOverallValue = topOverallValue;

            // Chart 4 (YTD progressivo)
            ViewBag.YtdLabels = labelsYtd;
            ViewBag.YtdCorrenteValues = ytdCorrenteValues;
            ViewBag.YtdPrecedenteValues = ytdPrecedenteValues;
            ViewBag.YtdCancellateCorrenteValues = ytdCancellateCorrenteValues;
            ViewBag.YtdCancellatePrecedenteValues = ytdCancellatePrecedenteValues;

            return View();
        }
    }
}