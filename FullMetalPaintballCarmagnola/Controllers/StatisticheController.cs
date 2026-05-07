using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
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
        private static readonly Regex ComuneProvinciaPattern = new(
            @"^(?<comune>.+?)\s*\((?<provincia>[A-Za-z]{2})\)\s*$",
            RegexOptions.Compiled);

        private readonly TesseramentoDbContext _dbContext;
        private readonly IWebHostEnvironment _environment;

        public StatisticheController(TesseramentoDbContext dbContext, IWebHostEnvironment environment)
        {
            _dbContext = dbContext;
            _environment = environment;
        }

        public async Task<IActionResult> Index(int? mese)
        {
            var it = new CultureInfo("it-IT");

            int meseCorrente = mese ?? DateTime.Now.Month;
            int annoCorrente = DateTime.Now.Year;
            int annoPrecedente = annoCorrente - 1;
            var oggi = DateTime.UtcNow.Date;

            var datiStoriciManuali = new Dictionary<int, Dictionary<int, int>>
            {
                [2022] = new() { [1] = 2, [2] = 2, [3] = 11, [4] = 7, [5] = 8, [6] = 10, [7] = 10, [8] = 2, [9] = 10, [10] = 9, [11] = 7, [12] = 0 },
                [2023] = new() { [1] = 1, [2] = 2, [3] = 6, [4] = 10, [5] = 10, [6] = 12, [7] = 10, [8] = 1, [9] = 14, [10] = 11, [11] = 6, [12] = 3 },
                [2024] = new() { [1] = 3, [2] = 7, [3] = 6, [4] = 9, [5] = 18, [6] = 11, [7] = 6, [8] = 5, [9] = 11, [10] = 6, [11] = 3, [12] = 2 },
                [2025] = new() { [1] = 1, [2] = 4, [3] = 7, [4] = 11, [5] = 13, [6] = 5 }
            };

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

            var dataLimiteAnnoCorrente = oggi.AddDays(1).AddTicks(-1);
            var giornoConfrontoAnnoPrecedente = Math.Min(oggi.Day, DateTime.DaysInMonth(annoPrecedente, oggi.Month));
            var dataLimiteAnnoPrecedente = DateTime.SpecifyKind(
                new DateTime(annoPrecedente, oggi.Month, giornoConfrontoAnnoPrecedente), DateTimeKind.Utc)
                .AddDays(1)
                .AddTicks(-1);

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

            var confermate = new Dictionary<int, Dictionary<int, int>>();

            void EnsureYearMonth(Dictionary<int, Dictionary<int, int>> dict, int year, int month)
            {
                if (!dict.ContainsKey(year))
                    dict[year] = new Dictionary<int, int>();
                if (!dict[year].ContainsKey(month))
                    dict[year][month] = 0;
            }

            foreach (var y in datiStoriciManuali)
            {
                foreach (var m in y.Value)
                {
                    EnsureYearMonth(confermate, y.Key, m.Key);
                    confermate[y.Key][m.Key] += m.Value;
                }
            }

            foreach (var row in confermateDbGrouped)
            {
                EnsureYearMonth(confermate, row.Year, row.Month);
                confermate[row.Year][row.Month] += row.Count;
            }

            var cancellate = new Dictionary<int, Dictionary<int, int>>();
            foreach (var row in cancellateDbGrouped)
            {
                EnsureYearMonth(cancellate, row.Year, row.Month);
                cancellate[row.Year][row.Month] += row.Count;
            }

            var anni = confermate.Keys.OrderBy(x => x).ToList();

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

            int prevYearMonthValue = confermate.ContainsKey(annoPrecedente) && confermate[annoPrecedente].ContainsKey(meseCorrente)
                ? confermate[annoPrecedente][meseCorrente] : 0;
            int prevYearMonthCancellate = cancellate.ContainsKey(annoPrecedente) && cancellate[annoPrecedente].ContainsKey(meseCorrente)
                ? cancellate[annoPrecedente][meseCorrente] : 0;

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

            var ytdCorrenteMese = confermateYtdCorrenteList.ToDictionary(x => x.Month, x => x.Count);
            var ytdPrecedenteMese = confermateYtdPrecedenteList.ToDictionary(x => x.Month, x => x.Count);
            var ytdCancellateCorrenteMese = cancellateYtdCorrenteList.ToDictionary(x => x.Month, x => x.Count);
            var ytdCancellatePrecedenteMese = cancellateYtdPrecedenteList.ToDictionary(x => x.Month, x => x.Count);

            var mesiYtd = Enumerable.Range(1, meseCorrente).ToList();
            var labelsYtd = mesiYtd.Select(m => it.DateTimeFormat.GetMonthName(m)).ToList();

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
                int valCorrente = ytdCorrenteMese.ContainsKey(m) ? ytdCorrenteMese[m] : 0;

                int valPrecedente = ytdPrecedenteMese.ContainsKey(m) ? ytdPrecedenteMese[m] : 0;
                if (valPrecedente == 0 &&
                    datiStoriciManuali.ContainsKey(annoPrecedente) &&
                    datiStoriciManuali[annoPrecedente].ContainsKey(m) &&
                    m < meseCorrente)
                {
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

            var catalogoComuni = await CaricaCatalogoComuniAsync();
            var residenze = await _dbContext.Tesseramenti
                .AsNoTracking()
                .Select(t => new
                {
                    t.ComuneResidenza,
                    t.NazioneResidenza,
                    t.PartitaId,
                    DataPartita = t.Partita != null ? (DateTime?)t.Partita.Data : null
                })
                .ToListAsync();

            var catalogoNazioniEuropa = await CaricaCatalogoNazioniEuropaAsync();
            var conteggiComuni = new Dictionary<string, StatisticheComuneMappaPunto>();
            int residentiEstero = 0;
            int nonClassificabili = 0;

            foreach (var residenza in residenze)
            {
                var nazioneResidenza = residenza.NazioneResidenza?.Trim();
                if (TryResolveForeignResidence(
                    nazioneResidenza,
                    residenza.ComuneResidenza,
                    catalogoNazioniEuropa,
                    out _))
                {
                    residentiEstero++;
                    continue;
                }

                if (!TryParseComuneProvincia(residenza.ComuneResidenza, out var comune, out var provincia))
                {
                    nonClassificabili++;
                    continue;
                }

                var chiave = BuildComuneLookupKey(comune, provincia);
                if (!catalogoComuni.TryGetValue(chiave, out var catalogoComune))
                {
                    nonClassificabili++;
                    continue;
                }

                if (conteggiComuni.TryGetValue(chiave, out var puntoEsistente))
                {
                    puntoEsistente.Totale++;
                }
                else
                {
                    conteggiComuni[chiave] = new StatisticheComuneMappaPunto
                    {
                        Comune = catalogoComune.Nome,
                        Provincia = catalogoComune.Provincia,
                        Lat = catalogoComune.Lat,
                        Lon = catalogoComune.Lon,
                        Totale = 1
                    };
                }
            }

            var puntiMappaComuni = conteggiComuni.Values
                .OrderByDescending(p => p.Totale)
                .ThenBy(p => p.Comune)
                .ToList();
            var totaleLocalizzati = puntiMappaComuni.Sum(p => p.Totale);
            var topComuneMappa = puntiMappaComuni.FirstOrDefault();

            var conteggiNazioni = new Dictionary<string, StatisticheNazioneMappaDato>(StringComparer.OrdinalIgnoreCase);
            int esteroNonClassificabile = 0;

            foreach (var residenza in residenze)
            {
                if (!TryResolveForeignResidence(
                    residenza.NazioneResidenza,
                    residenza.ComuneResidenza,
                    catalogoNazioniEuropa,
                    out var nazioneEuropea))
                {
                    continue;
                }

                if (conteggiNazioni.TryGetValue(nazioneEuropea.Iso2, out var nazioneEsistente))
                {
                    nazioneEsistente.Totale++;
                }
                else
                {
                    conteggiNazioni[nazioneEuropea.Iso2] = new StatisticheNazioneMappaDato
                    {
                        Iso2 = nazioneEuropea.Iso2,
                        Iso3 = nazioneEuropea.Iso3,
                        Nome = nazioneEuropea.NomeVisualizzato,
                        Totale = 1
                    };
                }
            }

            foreach (var residenza in residenze)
            {
                var nazioneResidenza = residenza.NazioneResidenza?.Trim();
                if (!string.IsNullOrWhiteSpace(nazioneResidenza) &&
                    !string.Equals(nazioneResidenza, "Italia", StringComparison.OrdinalIgnoreCase) &&
                    !TryResolveEuropeanCountry(nazioneResidenza, catalogoNazioniEuropa, out _))
                {
                    esteroNonClassificabile++;
                    continue;
                }

                if (string.IsNullOrWhiteSpace(nazioneResidenza) &&
                    !string.IsNullOrWhiteSpace(residenza.ComuneResidenza) &&
                    !TryParseComuneProvincia(residenza.ComuneResidenza, out _, out _) &&
                    !TryResolveEuropeanCountryFromLegacyResidence(residenza.ComuneResidenza, catalogoNazioniEuropa, out _))
                {
                    esteroNonClassificabile++;
                }
            }

            var nazioniMappaEuropa = conteggiNazioni.Values
                .OrderByDescending(n => n.Totale)
                .ThenBy(n => n.Nome)
                .ToList();
            var totaleEsteroLocalizzato = nazioniMappaEuropa.Sum(n => n.Totale);
            var topNazioneMappa = nazioniMappaEuropa.FirstOrDefault();

            var chiaveCampo = BuildComuneLookupKey("Carmagnola", "TO");
            catalogoComuni.TryGetValue(chiaveCampo, out var comuneCampo);
            var latCampo = comuneCampo?.Lat ?? 44.8493d;
            var lonCampo = comuneCampo?.Lon ?? 7.7204d;

            var distanzeMensiliCorrente = Enumerable.Range(1, 12)
                .ToDictionary(m => m, _ => new List<double>());
            var distanzeMensiliPrecedente = Enumerable.Range(1, 12)
                .ToDictionary(m => m, _ => new List<double>());
            var distanzeAnnuali = new Dictionary<int, List<double>>();
            var distanzeAnnoCorrente = new List<double>();

            foreach (var residenza in residenze)
            {
                if (!residenza.DataPartita.HasValue)
                {
                    continue;
                }

                if (TryResolveForeignResidence(
                    residenza.NazioneResidenza,
                    residenza.ComuneResidenza,
                    catalogoNazioniEuropa,
                    out _))
                {
                    continue;
                }

                if (!TryParseComuneProvincia(residenza.ComuneResidenza, out var comuneDistanza, out var provinciaDistanza))
                {
                    continue;
                }

                var chiaveDistanza = BuildComuneLookupKey(comuneDistanza, provinciaDistanza);
                if (!catalogoComuni.TryGetValue(chiaveDistanza, out var catalogoComuneDistanza))
                {
                    continue;
                }

                var distanzaKm = CalcolaDistanzaKm(
                    latCampo,
                    lonCampo,
                    catalogoComuneDistanza.Lat,
                    catalogoComuneDistanza.Lon);

                var annoDistanza = residenza.DataPartita.Value.Year;
                var meseDistanza = residenza.DataPartita.Value.Month;

                if (!distanzeAnnuali.TryGetValue(annoDistanza, out var listaAnno))
                {
                    listaAnno = new List<double>();
                    distanzeAnnuali[annoDistanza] = listaAnno;
                }

                listaAnno.Add(distanzaKm);

                if (annoDistanza == annoCorrente)
                {
                    distanzeMensiliCorrente[meseDistanza].Add(distanzaKm);
                    distanzeAnnoCorrente.Add(distanzaKm);
                }
                else if (annoDistanza == annoPrecedente)
                {
                    distanzeMensiliPrecedente[meseDistanza].Add(distanzaKm);
                }
            }

            var distanzaMonthLabels = Enumerable.Range(1, 12)
                .Select(m => it.DateTimeFormat.GetMonthName(m))
                .ToList();

            var distanzaMediaMensileCorrente = Enumerable.Range(1, 12)
                .Select(m => Math.Round(CalcolaMedia(distanzeMensiliCorrente[m]), 1))
                .ToList();
            var distanzaMediaMensilePrecedente = Enumerable.Range(1, 12)
                .Select(m => Math.Round(CalcolaMedia(distanzeMensiliPrecedente[m]), 1))
                .ToList();

            var distanzaMediaAnnua = distanzeAnnuali
                .OrderBy(kv => kv.Key)
                .Select(kv => new StatisticheDistanzaAnnualeDato
                {
                    Anno = kv.Key,
                    MediaKm = Math.Round(CalcolaMedia(kv.Value), 1)
                })
                .ToList();

            var distanzaMediaAnnoCorrente = Math.Round(CalcolaMedia(distanzeAnnoCorrente), 1);
            var distanzaMedianaAnnoCorrente = Math.Round(CalcolaMediana(distanzeAnnoCorrente), 1);
            var distanzaEntro25AnnoCorrente = distanzeAnnoCorrente.Count == 0
                ? 0
                : (int)Math.Round(distanzeAnnoCorrente.Count(d => d <= 25d) * 100d / distanzeAnnoCorrente.Count);
            var distanzaOltre50AnnoCorrente = distanzeAnnoCorrente.Count == 0
                ? 0
                : (int)Math.Round(distanzeAnnoCorrente.Count(d => d > 50d) * 100d / distanzeAnnoCorrente.Count);

            ViewBag.MeseCorrente = meseCorrente;
            ViewBag.AnnoCorrente = annoCorrente;
            ViewBag.AnnoPrecedente = annoPrecedente;
            ViewBag.LabelMese = it.DateTimeFormat.GetMonthName(meseCorrente);
            ViewBag.OggiLabel = oggi.ToString("dd/MM/yyyy");
            ViewBag.OggiLabelAnnoPrecedente = dataLimiteAnnoPrecedente.ToString("dd/MM/yyyy");

            ViewBag.Chart1TopPrevYear = topPrevYearForMonth;
            ViewBag.Chart1TopPrevValue = topPrevValueForMonth;
            ViewBag.Chart1TopPrevCancellate = topCancellateForMonth;
            ViewBag.Chart1CurrentYear = annoCorrente;
            ViewBag.Chart1CurrentValue = currentYearMonthValue;
            ViewBag.Chart1CurrentIsTop = currentIsTopForMonth;
            ViewBag.Chart1CurrentCancellate = currentYearMonthCancellate;

            ViewBag.Chart2CurrentValue = currentYearMonthValue;
            ViewBag.Chart2PrevValue = prevYearMonthValue;
            ViewBag.Chart2CurrentCancellate = currentYearMonthCancellate;
            ViewBag.Chart2PrevCancellate = prevYearMonthCancellate;

            ViewBag.Anni = anni;
            ViewBag.TotalsPerYear = totalsPerYear;
            ViewBag.CancellatePerYear = cancellatePerYear;
            ViewBag.TopYearOverall = topYearOverall;
            ViewBag.TopOverallValue = topOverallValue;

            ViewBag.YtdLabels = labelsYtd;
            ViewBag.YtdCorrenteValues = ytdCorrenteValues;
            ViewBag.YtdPrecedenteValues = ytdPrecedenteValues;
            ViewBag.YtdCancellateCorrenteValues = ytdCancellateCorrenteValues;
            ViewBag.YtdCancellatePrecedenteValues = ytdCancellatePrecedenteValues;

            ViewBag.ComuneMapPoints = puntiMappaComuni;
            ViewBag.ComuneMapMatched = totaleLocalizzati;
            ViewBag.ComuneMapExcluded = nonClassificabili;
            ViewBag.ComuneMapForeign = residentiEstero;
            ViewBag.ComuneMapTopLabel = topComuneMappa == null
                ? "N/D"
                : $"{topComuneMappa.Comune} ({topComuneMappa.Provincia})";
            ViewBag.ComuneMapTopValue = topComuneMappa?.Totale ?? 0;

            ViewBag.EuropeMapCountries = nazioniMappaEuropa;
            ViewBag.EuropeMapMatched = totaleEsteroLocalizzato;
            ViewBag.EuropeMapExcluded = esteroNonClassificabile;
            ViewBag.EuropeMapTopLabel = topNazioneMappa?.Nome ?? "N/D";
            ViewBag.EuropeMapTopValue = topNazioneMappa?.Totale ?? 0;

            ViewBag.DistanceReferenceLabel = "Centro di Carmagnola (TO)";
            ViewBag.DistanceMonthLabels = distanzaMonthLabels;
            ViewBag.DistanceMonthlyCurrent = distanzaMediaMensileCorrente;
            ViewBag.DistanceMonthlyPrevious = distanzaMediaMensilePrecedente;
            ViewBag.DistanceYearlyAverages = distanzaMediaAnnua;
            ViewBag.DistanceCurrentAverage = distanzaMediaAnnoCorrente;
            ViewBag.DistanceCurrentMedian = distanzaMedianaAnnoCorrente;
            ViewBag.DistanceCurrentWithin25Pct = distanzaEntro25AnnoCorrente;
            ViewBag.DistanceCurrentOver50Pct = distanzaOltre50AnnoCorrente;

            return View();
        }

        private async Task<Dictionary<string, ComuneCatalogoEntry>> CaricaCatalogoComuniAsync()
        {
            var percorsoFile = Path.Combine(_environment.WebRootPath, "data", "gi_comuni.json");
            if (!System.IO.File.Exists(percorsoFile))
            {
                return new Dictionary<string, ComuneCatalogoEntry>(StringComparer.OrdinalIgnoreCase);
            }

            await using var stream = System.IO.File.OpenRead(percorsoFile);
            var comuni = await JsonSerializer.DeserializeAsync<List<ComuneGeoJsonEntry>>(stream)
                ?? new List<ComuneGeoJsonEntry>();

            var catalogo = new Dictionary<string, ComuneCatalogoEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (var comune in comuni)
            {
                if (!double.TryParse(comune.Lat, NumberStyles.Float, CultureInfo.InvariantCulture, out var lat) ||
                    !double.TryParse(comune.Lon, NumberStyles.Float, CultureInfo.InvariantCulture, out var lon) ||
                    string.IsNullOrWhiteSpace(comune.SiglaProvincia) ||
                    string.IsNullOrWhiteSpace(comune.DenominazioneIta))
                {
                    continue;
                }

                var entry = new ComuneCatalogoEntry
                {
                    Nome = comune.DenominazioneIta.Trim(),
                    Provincia = comune.SiglaProvincia.Trim().ToUpperInvariant(),
                    Lat = lat,
                    Lon = lon
                };

                RegisterComuneCatalogoEntry(catalogo, comune.DenominazioneIta, comune.SiglaProvincia, entry);
                RegisterComuneCatalogoEntry(catalogo, comune.DenominazioneItaAltra, comune.SiglaProvincia, entry);
                RegisterComuneCatalogoEntry(catalogo, comune.DenominazioneAltra, comune.SiglaProvincia, entry);
            }

            return catalogo;
        }

        private async Task<Dictionary<string, NazioneEuropaCatalogoEntry>> CaricaCatalogoNazioniEuropaAsync()
        {
            var pathNazioni = Path.Combine(_environment.WebRootPath, "data", "gi_nazioni.json");
            var pathGeoJson = Path.Combine(_environment.WebRootPath, "data", "europe.geojson");

            var catalogo = new Dictionary<string, NazioneEuropaCatalogoEntry>(StringComparer.OrdinalIgnoreCase);
            var alias = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (!System.IO.File.Exists(pathGeoJson))
            {
                return catalogo;
            }

            await using (var geoStream = System.IO.File.OpenRead(pathGeoJson))
            {
                var europeGeoJson = await JsonSerializer.DeserializeAsync<EuropeGeoJsonFeatureCollection>(geoStream)
                    ?? new EuropeGeoJsonFeatureCollection();

                foreach (var feature in europeGeoJson.Features)
                {
                    if (feature.Properties == null || string.IsNullOrWhiteSpace(feature.Properties.ISO2))
                    {
                        continue;
                    }

                    var iso2 = feature.Properties.ISO2.Trim().ToUpperInvariant();
                    var entry = new NazioneEuropaCatalogoEntry
                    {
                        Iso2 = iso2,
                        Iso3 = feature.Properties.ISO3?.Trim().ToUpperInvariant() ?? string.Empty,
                        NomeGeoJson = feature.Properties.NAME?.Trim() ?? iso2,
                        NomeVisualizzato = feature.Properties.NAME?.Trim() ?? iso2
                    };

                    catalogo[iso2] = entry;
                    RegisterCountryAlias(alias, entry.NomeGeoJson, iso2);
                    RegisterCountryAlias(alias, entry.Iso2, iso2);
                    RegisterCountryAlias(alias, entry.Iso3, iso2);
                }
            }

            if (System.IO.File.Exists(pathNazioni))
            {
                await using var nazioniStream = System.IO.File.OpenRead(pathNazioni);
                var nazioni = await JsonSerializer.DeserializeAsync<List<GiNazioneEntry>>(nazioniStream)
                    ?? new List<GiNazioneEntry>();

                foreach (var nazione in nazioni)
                {
                    var iso2 = NormalizeNationCode(nazione.SiglaNazione);
                    if (string.IsNullOrWhiteSpace(iso2) || !catalogo.TryGetValue(iso2, out var entry))
                    {
                        continue;
                    }

                    RegisterCountryAlias(alias, nazione.DenominazioneNazione, iso2);
                    RegisterCountryAlias(alias, nazione.DenominazioneCittadinanza, iso2);

                    if (!string.IsNullOrWhiteSpace(nazione.DenominazioneNazione))
                    {
                        entry.NomeVisualizzato = ToDisplayCase(nazione.DenominazioneNazione);
                    }
                }
            }

            RegisterManualCountryAliases(alias);

            foreach (var entry in catalogo.Values)
            {
                entry.Alias = alias;
            }

            return catalogo;
        }

        private static void RegisterComuneCatalogoEntry(
            IDictionary<string, ComuneCatalogoEntry> catalogo,
            string? nomeComune,
            string? provincia,
            ComuneCatalogoEntry entry)
        {
            if (string.IsNullOrWhiteSpace(nomeComune) || string.IsNullOrWhiteSpace(provincia))
            {
                return;
            }

            var chiave = BuildComuneLookupKey(nomeComune, provincia);
            if (!catalogo.ContainsKey(chiave))
            {
                catalogo[chiave] = entry;
            }
        }

        private static bool TryParseComuneProvincia(string? valore, out string comune, out string provincia)
        {
            comune = string.Empty;
            provincia = string.Empty;

            if (string.IsNullOrWhiteSpace(valore))
            {
                return false;
            }

            var match = ComuneProvinciaPattern.Match(valore.Trim());
            if (!match.Success)
            {
                return false;
            }

            comune = match.Groups["comune"].Value.Trim();
            provincia = match.Groups["provincia"].Value.Trim().ToUpperInvariant();
            return !string.IsNullOrWhiteSpace(comune) && provincia.Length == 2;
        }

        private static bool TryResolveEuropeanCountry(
            string rawCountry,
            IReadOnlyDictionary<string, NazioneEuropaCatalogoEntry> catalogo,
            out NazioneEuropaCatalogoEntry entry)
        {
            entry = null!;

            if (string.IsNullOrWhiteSpace(rawCountry) || catalogo.Count == 0)
            {
                return false;
            }

            var aliasMap = catalogo.Values.First().Alias;
            var normalizedCountry = NormalizeLookupValue(rawCountry);
            if (!aliasMap.TryGetValue(normalizedCountry, out var iso2))
            {
                return false;
            }

            return catalogo.TryGetValue(iso2, out entry!);
        }

        private static bool TryResolveForeignResidence(
            string? rawCountry,
            string? rawResidence,
            IReadOnlyDictionary<string, NazioneEuropaCatalogoEntry> catalogo,
            out NazioneEuropaCatalogoEntry entry)
        {
            entry = null!;

            if (!string.IsNullOrWhiteSpace(rawCountry) &&
                !string.Equals(rawCountry.Trim(), "Italia", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryResolveEuropeanCountry(rawCountry, catalogo, out entry))
                {
                    return false;
                }

                return !string.Equals(entry.Iso2, "IT", StringComparison.OrdinalIgnoreCase);
            }

            if (string.IsNullOrWhiteSpace(rawResidence) ||
                TryParseComuneProvincia(rawResidence, out _, out _))
            {
                return false;
            }

            if (!TryResolveEuropeanCountryFromLegacyResidence(rawResidence, catalogo, out entry))
            {
                return false;
            }

            return !string.Equals(entry.Iso2, "IT", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryResolveEuropeanCountryFromLegacyResidence(
            string rawResidence,
            IReadOnlyDictionary<string, NazioneEuropaCatalogoEntry> catalogo,
            out NazioneEuropaCatalogoEntry entry)
        {
            entry = null!;

            if (string.IsNullOrWhiteSpace(rawResidence))
            {
                return false;
            }

            if (TryResolveEuropeanCountry(rawResidence, catalogo, out entry))
            {
                return true;
            }

            var normalizedResidence = rawResidence
                .Replace('(', ' ')
                .Replace(')', ' ')
                .Replace(',', ' ')
                .Replace('-', ' ');

            foreach (var segment in normalizedResidence.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (TryResolveEuropeanCountry(segment, catalogo, out entry))
                {
                    return true;
                }
            }

            return false;
        }

        private static string BuildComuneLookupKey(string comune, string provincia)
            => $"{NormalizeLookupValue(comune)}|{NormalizeLookupValue(provincia)}";

        private static void RegisterCountryAlias(IDictionary<string, string> alias, string? rawValue, string iso2)
        {
            if (string.IsNullOrWhiteSpace(rawValue) || string.IsNullOrWhiteSpace(iso2))
            {
                return;
            }

            var key = NormalizeLookupValue(rawValue);
            if (!alias.ContainsKey(key))
            {
                alias[key] = iso2.Trim().ToUpperInvariant();
            }
        }

        private static void RegisterManualCountryAliases(IDictionary<string, string> alias)
        {
            RegisterCountryAlias(alias, "UNITED KINGDOM", "GB");
            RegisterCountryAlias(alias, "ENGLAND", "GB");
            RegisterCountryAlias(alias, "SCOTLAND", "GB");
            RegisterCountryAlias(alias, "WALES", "GB");
            RegisterCountryAlias(alias, "NORTHERN IRELAND", "GB");
            RegisterCountryAlias(alias, "GREAT BRITAIN", "GB");
            RegisterCountryAlias(alias, "INGHILTERRA", "GB");
            RegisterCountryAlias(alias, "REGNO UNITO", "GB");
            RegisterCountryAlias(alias, "GRAN BRETAGNA", "GB");
            RegisterCountryAlias(alias, "NETHERLANDS", "NL");
            RegisterCountryAlias(alias, "OLANDA", "NL");
            RegisterCountryAlias(alias, "HOLLAND", "NL");
            RegisterCountryAlias(alias, "CZECHIA", "CZ");
            RegisterCountryAlias(alias, "CZECH REPUBLIC", "CZ");
            RegisterCountryAlias(alias, "REPUBBLICA CECA", "CZ");
            RegisterCountryAlias(alias, "BOSNIA", "BA");
            RegisterCountryAlias(alias, "BOSNIA HERZEGOVINA", "BA");
            RegisterCountryAlias(alias, "SVIZZERA", "CH");
            RegisterCountryAlias(alias, "SWITZERLAND", "CH");
            RegisterCountryAlias(alias, "DANIMARCA", "DK");
            RegisterCountryAlias(alias, "DENMARK", "DK");
            RegisterCountryAlias(alias, "FRANCIA", "FR");
            RegisterCountryAlias(alias, "FRANCE", "FR");
            RegisterCountryAlias(alias, "SPAGNA", "ES");
            RegisterCountryAlias(alias, "SPAIN", "ES");
            RegisterCountryAlias(alias, "KOBENHAVN", "DK");
            RegisterCountryAlias(alias, "KOBENHAVN NV", "DK");
            RegisterCountryAlias(alias, "COPENHAGEN", "DK");
            RegisterCountryAlias(alias, "CPH DENMARK", "DK");
            RegisterCountryAlias(alias, "AALBORG", "DK");
            RegisterCountryAlias(alias, "AMSTERDAM", "NL");
            RegisterCountryAlias(alias, "LEIDEN", "NL");
            RegisterCountryAlias(alias, "ZURICH", "CH");
            RegisterCountryAlias(alias, "LUZERN", "CH");
            RegisterCountryAlias(alias, "BERN", "CH");
            RegisterCountryAlias(alias, "BELLINZONA", "CH");
            RegisterCountryAlias(alias, "BETTLACH", "CH");
            RegisterCountryAlias(alias, "GRENCHEN", "CH");
            RegisterCountryAlias(alias, "HINDELBANK", "CH");
            RegisterCountryAlias(alias, "LANGENDORF", "CH");
            RegisterCountryAlias(alias, "MINUSIO", "CH");
            RegisterCountryAlias(alias, "REGENSDORF", "CH");
            RegisterCountryAlias(alias, "SORENGO TI SVIZZERA", "CH");
            RegisterCountryAlias(alias, "TICINO SVIZZERA", "CH");
            RegisterCountryAlias(alias, "LOCARNO CH", "CH");
            RegisterCountryAlias(alias, "TENERO CH", "CH");
            RegisterCountryAlias(alias, "CASTIONE CH", "CH");
            RegisterCountryAlias(alias, "CASTELLO DE LA PLANA SPAGNA", "ES");
        }

        private static string NormalizeNationCode(string? code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return string.Empty;
            }

            return code.Trim().ToUpperInvariant() switch
            {
                "A" => "AT",
                "B" => "BE",
                "CH" => "CH",
                "D" => "DE",
                "DK" => "DK",
                "E" => "ES",
                "F" => "FR",
                "GB" => "GB",
                "NL" => "NL",
                "P" => "PT",
                "GR" => "GR",
                "S" => "SE",
                "N" => "NO",
                var normalized when normalized.Length == 2 => normalized,
                _ => string.Empty
            };
        }

        private static string ToDisplayCase(string value)
        {
            var lower = value.Trim().ToLowerInvariant();
            return CultureInfo.GetCultureInfo("it-IT").TextInfo.ToTitleCase(lower);
        }

        private static string NormalizeLookupValue(string value)
        {
            var normalized = value
                .Replace('’', '\'')
                .Replace('‘', '\'')
                .Replace('`', '\'')
                .Normalize(NormalizationForm.FormD);

            var builder = new StringBuilder(normalized.Length);
            bool previousWasSpace = false;

            foreach (var ch in normalized)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
                {
                    continue;
                }

                if (char.IsLetterOrDigit(ch))
                {
                    builder.Append(char.ToUpperInvariant(ch));
                    previousWasSpace = false;
                    continue;
                }

                if (!previousWasSpace)
                {
                    builder.Append(' ');
                    previousWasSpace = true;
                }
            }

            return builder.ToString().Trim();
        }

        private static double CalcolaDistanzaKm(double lat1, double lon1, double lat2, double lon2)
        {
            const double raggioTerraKm = 6371d;
            var dLat = ToRadians(lat2 - lat1);
            var dLon = ToRadians(lon2 - lon1);
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return raggioTerraKm * c;
        }

        private static double ToRadians(double angle) => angle * Math.PI / 180d;

        private static double CalcolaMedia(IReadOnlyCollection<double> valori)
            => valori.Count == 0 ? 0d : valori.Average();

        private static double CalcolaMediana(IReadOnlyCollection<double> valori)
        {
            if (valori.Count == 0)
            {
                return 0d;
            }

            var ordinati = valori.OrderBy(v => v).ToList();
            var centro = ordinati.Count / 2;
            return ordinati.Count % 2 == 0
                ? (ordinati[centro - 1] + ordinati[centro]) / 2d
                : ordinati[centro];
        }
    }

    public sealed class StatisticheComuneMappaPunto
    {
        public string Comune { get; set; } = string.Empty;
        public string Provincia { get; set; } = string.Empty;
        public double Lat { get; set; }
        public double Lon { get; set; }
        public int Totale { get; set; }
    }

    public sealed class StatisticheNazioneMappaDato
    {
        public string Iso2 { get; set; } = string.Empty;
        public string Iso3 { get; set; } = string.Empty;
        public string Nome { get; set; } = string.Empty;
        public int Totale { get; set; }
    }

    public sealed class StatisticheDistanzaAnnualeDato
    {
        public int Anno { get; set; }
        public double MediaKm { get; set; }
    }

    public sealed class ComuneGeoJsonEntry
    {
        [JsonPropertyName("sigla_provincia")]
        public string? SiglaProvincia { get; set; }

        [JsonPropertyName("denominazione_ita_altra")]
        public string? DenominazioneItaAltra { get; set; }

        [JsonPropertyName("denominazione_ita")]
        public string? DenominazioneIta { get; set; }

        [JsonPropertyName("denominazione_altra")]
        public string? DenominazioneAltra { get; set; }

        [JsonPropertyName("lat")]
        public string? Lat { get; set; }

        [JsonPropertyName("lon")]
        public string? Lon { get; set; }
    }

    public sealed class ComuneCatalogoEntry
    {
        public string Nome { get; set; } = string.Empty;
        public string Provincia { get; set; } = string.Empty;
        public double Lat { get; set; }
        public double Lon { get; set; }
    }

    public sealed class NazioneEuropaCatalogoEntry
    {
        public string Iso2 { get; set; } = string.Empty;
        public string Iso3 { get; set; } = string.Empty;
        public string NomeGeoJson { get; set; } = string.Empty;
        public string NomeVisualizzato { get; set; } = string.Empty;
        public Dictionary<string, string> Alias { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    public sealed class EuropeGeoJsonFeatureCollection
    {
        [JsonPropertyName("features")]
        public List<EuropeGeoJsonFeature> Features { get; set; } = new();
    }

    public sealed class EuropeGeoJsonFeature
    {
        [JsonPropertyName("properties")]
        public EuropeGeoJsonProperties? Properties { get; set; }
    }

    public sealed class EuropeGeoJsonProperties
    {
        [JsonPropertyName("ISO2")]
        public string? ISO2 { get; set; }

        [JsonPropertyName("ISO3")]
        public string? ISO3 { get; set; }

        [JsonPropertyName("NAME")]
        public string? NAME { get; set; }
    }

    public sealed class GiNazioneEntry
    {
        [JsonPropertyName("sigla_nazione")]
        public string? SiglaNazione { get; set; }

        [JsonPropertyName("denominazione_nazione")]
        public string? DenominazioneNazione { get; set; }

        [JsonPropertyName("denominazione_cittadinanza")]
        public string? DenominazioneCittadinanza { get; set; }
    }
}
