using System.Globalization;
using Full_Metal_Paintball_Carmagnola.Models;
using Full_Metal_Paintball_Carmagnola.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Full_Metal_Paintball_Carmagnola.Controllers
{
    [Authorize(Policy = "Disponibilita Campo")]
    public class DisponibilitaCampoController : Controller
    {
        private static readonly TimeSpan AperturaCampo = new(9, 0, 0);
        private static readonly TimeSpan MargineCampo = TimeSpan.FromMinutes(30);
        private static readonly TimeSpan DurataMinimaPartita = TimeSpan.FromHours(1);
        private const double CarmagnolaLatitude = 44.849;
        private const double CarmagnolaLongitude = 7.720;

        private readonly TesseramentoDbContext _dbContext;
        private readonly PricingCatalogService _pricingCatalogService;

        public DisponibilitaCampoController(TesseramentoDbContext dbContext, PricingCatalogService pricingCatalogService)
        {
            _dbContext = dbContext;
            _pricingCatalogService = pricingCatalogService;
        }

        public async Task<IActionResult> Index()
        {
            var oggi = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);
            var fine = oggi.AddMonths(6);

            var weekendDates = Enumerable.Range(0, (fine - oggi).Days + 1)
                .Select(offset => oggi.AddDays(offset))
                .Where(date => date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                .ToList();

            var partiteWeekend = await _dbContext.Partite
                .Where(p => !p.IsDeleted &&
                            p.Data >= oggi &&
                            p.Data <= fine)
                .OrderBy(p => p.Data)
                .ThenBy(p => p.OraInizio)
                .ToListAsync();

            partiteWeekend = partiteWeekend
                .Where(p => p.Data.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                .ToList();

            var partitePerGiorno = partiteWeekend
                .GroupBy(p => p.Data.Date)
                .ToDictionary(g => g.Key, g => g.ToList());

            var model = new CampoDisponibilitaViewModel
            {
                Giorni = weekendDates
                    .Select(data => BuildGiorno(data, partitePerGiorno.TryGetValue(data.Date, out var partite) ? partite : new List<Partita>()))
                    .ToList()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerificaRichiesta([FromForm] CampoDisponibilitaRequest request)
        {
            if (!DateTime.TryParseExact(request.Data, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
            {
                return BadRequest(new { success = false, message = "Data non valida." });
            }

            var hasSpecificTime = !string.IsNullOrWhiteSpace(request.OraInizio);
            TimeSpan oraInizio = default;
            if (hasSpecificTime && !TimeSpan.TryParseExact(request.OraInizio, @"hh\:mm", CultureInfo.InvariantCulture, out oraInizio))
            {
                return BadRequest(new { success = false, message = "Orario non valido." });
            }

            if (!double.TryParse(request.Durata.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var durata) || durata < 1)
            {
                return BadRequest(new { success = false, message = "La durata minima è di 1 ora." });
            }

            var data = DateTime.SpecifyKind(parsedDate.Date, DateTimeKind.Utc);
            var partite = await _dbContext.Partite
                .Where(p => !p.IsDeleted && p.Data >= data && p.Data < data.AddDays(1))
                .OrderBy(p => p.OraInizio)
                .ToListAsync();

            var giorno = BuildGiorno(data, partite);
            var slotLiberi = giorno.Fasce
                .Where(f => f.Prenotabile)
                .Select(BuildSlotDisponibileLabel)
                .ToList();

            var catalog = await _pricingCatalogService.GetCatalogAsync();
            var currentListinoId = catalog.GetCurrentListino().Id;

            if (!hasSpecificTime)
            {
                var messageForDay = BuildCustomerMessageForDay(data, slotLiberi, catalog, currentListinoId, request.TipoGruppo);
                return Json(new
                {
                    success = true,
                    disponibile = slotLiberi.Count > 0,
                    status = slotLiberi.Count > 0 ? "Disponibile" : "Non disponibile",
                    motivi = Array.Empty<string>(),
                    slots = slotLiberi,
                    message = messageForDay
                });
            }

            var finePartita = oraInizio.Add(TimeSpan.FromHours(durata));
            var inizioOccupazioneRichiesta = oraInizio.Subtract(MargineCampo);
            var fineOccupazioneRichiesta = finePartita.Add(MargineCampo);

            if (inizioOccupazioneRichiesta - AperturaCampo < DurataMinimaPartita)
                inizioOccupazioneRichiesta = AperturaCampo;

            var motivi = new List<string>();
            if (oraInizio < AperturaCampo)
                motivi.Add($"l'orario richiesto è prima dell'apertura campo delle {AperturaCampo:hh\\:mm}");
            if (finePartita > giorno.UltimaFinePartita)
                motivi.Add($"la partita terminerebbe dopo il limite massimo delle {giorno.UltimaFinePartita:hh\\:mm}");

            var occupate = giorno.Fasce
                .Where(f => f.Stato == "Occupato")
                .ToList();

            if (occupate.Any(f => inizioOccupazioneRichiesta < f.Fine && fineOccupazioneRichiesta > f.Inizio))
                motivi.Add("il campo risulta già occupato in quella fascia oraria");

            var richiestaDisponibile = motivi.Count == 0;
            var message = BuildCustomerMessage(data, oraInizio, finePartita, richiestaDisponibile, motivi, slotLiberi, catalog, currentListinoId, request.TipoGruppo);

            return Json(new
            {
                success = true,
                disponibile = richiestaDisponibile,
                status = richiestaDisponibile ? "Disponibile" : "Non disponibile",
                motivi,
                slots = slotLiberi,
                message
            });
        }

        private static CampoDisponibilitaGiornoViewModel BuildGiorno(DateTime data, List<Partita> partite)
        {
            var tramonto = GetSunsetTime(data);
            var ultimaFinePartita = RoundDownToHalfHour(tramonto - MargineCampo);
            if (ultimaFinePartita < AperturaCampo)
                ultimaFinePartita = AperturaCampo;

            var fasceOccupate = partite
                .Select(partita => BuildFasciaOccupata(partita, ultimaFinePartita))
                .Where(fascia => fascia.Fine > AperturaCampo && fascia.Inizio < ultimaFinePartita)
                .Select(fascia =>
                {
                    fascia.Inizio = Max(fascia.Inizio, AperturaCampo);
                    fascia.Fine = Min(fascia.Fine, ultimaFinePartita);
                    return fascia;
                })
                .OrderBy(fascia => fascia.Inizio)
                .ToList();

            var fasce = new List<CampoFasciaViewModel>();
            var cursore = AperturaCampo;

            foreach (var occupata in MergeOccupate(fasceOccupate))
            {
                AddFasciaLibera(fasce, cursore, occupata.Inizio);
                fasce.Add(occupata);
                if (occupata.Fine > cursore)
                    cursore = occupata.Fine;
            }

            AddFasciaLibera(fasce, cursore, ultimaFinePartita);

            return new CampoDisponibilitaGiornoViewModel
            {
                Data = data,
                GiornoLabel = CultureInfo.GetCultureInfo("it-IT").TextInfo.ToTitleCase(data.ToString("dddd", CultureInfo.GetCultureInfo("it-IT"))),
                Apertura = AperturaCampo,
                Tramonto = tramonto,
                UltimaFinePartita = ultimaFinePartita,
                Fasce = fasce
            };
        }

        private static CampoFasciaViewModel BuildFasciaOccupata(Partita partita, TimeSpan ultimaFinePartita)
        {
            var finePartita = partita.OraInizio.Add(TimeSpan.FromHours(partita.Durata));
            var inizioOccupato = partita.OraInizio.Subtract(MargineCampo);
            var fineOccupato = finePartita.Add(MargineCampo);

            if (inizioOccupato - AperturaCampo < DurataMinimaPartita)
                inizioOccupato = AperturaCampo;

            return new CampoFasciaViewModel
            {
                Inizio = inizioOccupato,
                Fine = fineOccupato,
                Stato = "Occupato",
                Dettaglio = "Campo occupato",
                Prenotabile = false
            };
        }

        private static void AddFasciaLibera(List<CampoFasciaViewModel> fasce, TimeSpan inizio, TimeSpan fine)
        {
            if (fine <= inizio)
                return;

            var durata = fine - inizio;
            fasce.Add(new CampoFasciaViewModel
            {
                Inizio = inizio,
                Fine = fine,
                Stato = durata >= DurataMinimaPartita ? "Libero" : "Non utile",
                Dettaglio = durata >= DurataMinimaPartita
                    ? "Spazio sufficiente per una partita"
                    : "Spazio inferiore a 1 ora",
                Prenotabile = durata >= DurataMinimaPartita
            });
        }

        private static List<CampoFasciaViewModel> MergeOccupate(List<CampoFasciaViewModel> fasce)
        {
            var result = new List<CampoFasciaViewModel>();
            foreach (var fascia in fasce)
            {
                var last = result.LastOrDefault();
                if (last == null || fascia.Inizio > last.Fine)
                {
                    result.Add(fascia);
                    continue;
                }

                if (fascia.Fine > last.Fine)
                    last.Fine = fascia.Fine;

                last.Dettaglio = "Campo occupato";
            }

            return result;
        }

        private static string BuildCustomerMessage(
            DateTime data,
            TimeSpan oraInizio,
            TimeSpan finePartita,
            bool richiestaDisponibile,
            List<string> motivi,
            List<string> slotLiberi,
            PricingCatalog catalog,
            short listinoId,
            string tipoGruppo)
        {
            var it = CultureInfo.GetCultureInfo("it-IT");
            var dataLabel = data.ToString("d MMMM", it);
            var availabilityLines = new List<string> { "Salve," };

            if (richiestaDisponibile)
            {
                availabilityLines.Add($"Per il {dataLabel} ho posto:");
                availabilityLines.Add($"Dalle {oraInizio:hh\\:mm} alle {finePartita:hh\\:mm}");
                availabilityLines.Add($"{finePartita:hh\\:mm} è il termine ultimo massimo di fine partita.");
            }
            else
            {
                availabilityLines.Add($"Per il {dataLabel} l'orario richiesto dalle {oraInizio:hh\\:mm} alle {finePartita:hh\\:mm} non è disponibile.");
                if (motivi.Count > 0)
                    availabilityLines.Add($"Motivo: {string.Join("; ", motivi)}.");

                if (slotLiberi.Count > 0)
                {
                    availabilityLines.Add("Ho disponibilità in queste fasce:");
                    availabilityLines.AddRange(slotLiberi);
                    availabilityLines.Add("L'orario finale indicato è il termine ultimo massimo di fine partita.");
                }
                else
                {
                    availabilityLines.Add("Al momento non risultano fasce disponibili per quella data.");
                }
            }

            availabilityLines.Add("Il campo viene riservato al gruppo prenotante.");

            return string.Join(Environment.NewLine, availabilityLines)
                + Environment.NewLine
                + BuildInformativeMessage(catalog, listinoId, tipoGruppo);
        }

        private static string BuildCustomerMessageForDay(
            DateTime data,
            List<string> slotLiberi,
            PricingCatalog catalog,
            short listinoId,
            string tipoGruppo)
        {
            var it = CultureInfo.GetCultureInfo("it-IT");
            var dataLabel = data.ToString("d MMMM", it);
            var availabilityLines = new List<string> { "Salve," };

            if (slotLiberi.Count > 0)
            {
                availabilityLines.Add($"Per il {dataLabel} ho posto:");
                availabilityLines.AddRange(slotLiberi);
                availabilityLines.Add("Gli orari finali indicati sono il termine ultimo massimo di fine partita.");
            }
            else
            {
                availabilityLines.Add($"Per il {dataLabel} al momento non risultano fasce disponibili.");
            }

            availabilityLines.Add("Il campo viene riservato al gruppo prenotante.");

            return string.Join(Environment.NewLine, availabilityLines)
                + Environment.NewLine
                + BuildInformativeMessage(catalog, listinoId, tipoGruppo);
        }

        private static string BuildSlotDisponibileLabel(CampoFasciaViewModel fascia)
        {
            var maxDurationHours = Math.Floor(fascia.DurataOre * 2) / 2;
            var maxDurationLabel = maxDurationHours switch
            {
                >= 2 => "durata massima 2 ore",
                >= 1.5 => "durata massima 1 ora e mezza",
                >= 1 => "durata massima 1 ora",
                _ => "durata inferiore a 1 ora"
            };

            return $"Dalle {fascia.Inizio:hh\\:mm} alle {fascia.Fine:hh\\:mm} ({maxDurationLabel})";
        }

        private static string BuildInformativeMessage(PricingCatalog catalog, short listinoId, string tipoGruppo)
        {
            string Price(string code) =>
                catalog.GetEntry(code)?.GetPrice(listinoId).ToString("0", CultureInfo.InvariantCulture) ?? "0";

            var rabbitSingle = Price(PricingEntryCodes.RabbitSingle);
            var normalizedTipoGruppo = (tipoGruppo ?? "Completo").Trim().ToLowerInvariant();
            var showAdultPrices = normalizedTipoGruppo is "completo" or "adulti" or "caccia";
            var showKidsPrices = normalizedTipoGruppo is "completo" or "kids";
            var showRabbit = normalizedTipoGruppo is "completo" or "caccia";
            var showAdultAge = normalizedTipoGruppo is "completo" or "adulti" or "caccia";
            var showKidsAge = normalizedTipoGruppo is "completo" or "kids";

            var priceLines = new List<string>();
            if (showAdultPrices)
            {
                priceLines.Add($@"Costi Adulti (10 anni in su):
- un'ora al costo di {Price(PricingEntryCodes.AdultStandard1Hour)} euro a testa comprensiva di tesseramento e 200 colpi a testa (oppure {Price(PricingEntryCodes.AdultUnlimited1Hour)}€ con colpi illimitati)
- un'ora e mezza al costo di  {Price(PricingEntryCodes.AdultStandard90Minutes)} euro a testa comprensiva di tesseramento e 300 colpi a testa (oppure {Price(PricingEntryCodes.AdultUnlimited90Minutes)}€ con colpi illimitati)
- due ore al costo di {Price(PricingEntryCodes.AdultStandard2Hours)} euro a testa comprensiva di tesseramento e 400 colpi a testa");
            }

            if (showKidsPrices)
            {
                priceLines.Add($@"Costi Kids (8-13 anni):
- un'ora al costo di {Price(PricingEntryCodes.Kids1Hour)} euro a testa comprensiva di tesseramento e colpi illimitati
- un'ora e mezza al costo di  {Price(PricingEntryCodes.Kids90Minutes)} euro a testa comprensiva di tesseramento e colpi illimitati
- due ore al costo di {Price(PricingEntryCodes.Kids2Hours)} euro a testa comprensiva di tesseramento e colpi illimitati");
            }

            var rabbitBlock = showRabbit
                ? $@"

Solo per adulti:
Per addio al celibato/nubilato e compleanni proponiamo inoltre una caccia al coniglio ({rabbitSingle}€ da dividere tra tutti - se si prendono due costumi, faremo un po' di sconto) :
Daremo in dotazione un costume da coniglio per il festeggiato e faremo un round introduttivo o finale dove nasconderemo il fucile del festeggiato nel campo il quale avrà tempo 1 minuto per poterlo trovare. Una volta scaduto il tempo, gli altri partecipanti entreranno in campo con i propri fucili e una manciata di colpi da utilizzare contro il coniglio.
Il tempo rimanente verrà utilizzato per fare dei round da 7 minuti l'uno dove una squadra si scontrerà contro l'altra. Le regole ve le spiegheremo sul posto nel caso in cui confermerete la prenotazione (possibilità di giocare modalità miste)."
                : string.Empty;

            var ageLines = new List<string>();
            if (showAdultAge)
                ageLines.Add("- [x] Età minima richiesta per la modalità adulti 9/10 anni (anche più piccoli se alti almeno 130 cm)");
            if (showKidsAge)
                ageLines.Add("- [x] Età minima richiesta per la modalità kids 8 anni e massimo 13");

            return $@"
La prenotazione deve avvenire via SMS o WhatsApp al numero 3468741192 (reperibile telefonicamente dal lunedì al venerdì dalle 18:00 alle 21:00) almeno 24 ore prima (salvo disponibilità del campo) per turni del weekend. Per turni infrasettimanali almeno 7 giorni di preavviso (salvo disponibilità del campo e dello staff) specificando:
- numero indicativo di partecipanti (Min 8 e max 16 - se si è meno del numero minimo richiesto, occorre pagare la differenza per le persone mancanti ad esclusione del tesseramento 5€) - se di più si dovrà organizzare un torneo a squadre (il torneo ha una durata fissa di un'ora e mezza al prezzo di un'ora)
- orario di inizio partita
- ore di gioco
- nome e cognome di una persona di riferimento della prenotazione
{string.Join($"{Environment.NewLine}{Environment.NewLine}", priceLines)}

I prezzi includono inoltre l'affitto dell'attrezzatura protettiva (pettorine, para collo, guanti e casco).

Per chi è alla prima esperienza, consigliamo di iniziare con il classico deathmatch a squadre per poi valutare con i ragazzi del nostro staff una variazione di modalità durante lo svolgimento della partita.
{rabbitBlock}

🚨ATTENZIONE🚨
In caso di mal tempo la partita verrà disputata ugualmente salvo completa impraticabilità del campo. Questo sarà di completa valutazione da parte dello staff.

N.B. :
- [x] È richiesto il pagamento di una caparra tramite Satispay o bonifico pari ad una quota di partecipazione della prenotazione. Questa è valida come impegno da parte del cliente a voler prenotare il campo e non verrà rimborsata nel caso di disdetta della prenotazione da parte del cliente stesso.
- [x] È preferibile non eseguire conti separati
- [x] Saldo in contanti o Satispay a fine partita
- [x] Il campo è sprovvisto di spogliatoi e docce
- [x] È richiesto l'arrivo al campo 15/20 minuti prima della prenotazione. L'ora o le ore di gioco verranno conteggiate dall'orario della prenotazione a prescindere da eventuali ritardi
- [x] Per chi usa occhiali da vista, si richiede l'utilizzo di lenti a contatto
{string.Join(Environment.NewLine, ageLines)}
- [x] Il campo è a disposizione per rinfreschi/taglio torta. Nel caso, potreste utilizzare l'area preposta a patto che i rifiuti vengano portati via lasciando l'area come l'avete trovata.

Restiamo a disposizione.
Cordiali saluti".TrimStart();
        }

        private static TimeSpan GetSunsetTime(DateTime date)
        {
            var dayOfYear = date.DayOfYear;
            var longitudeHour = CarmagnolaLongitude / 15.0;
            var approximateTime = dayOfYear + ((18.0 - longitudeHour) / 24.0);
            var meanAnomaly = (0.9856 * approximateTime) - 3.289;
            var trueLongitude = meanAnomaly
                + (1.916 * Math.Sin(ToRadians(meanAnomaly)))
                + (0.020 * Math.Sin(ToRadians(2 * meanAnomaly)))
                + 282.634;
            trueLongitude = NormalizeDegrees(trueLongitude);

            var rightAscension = ToDegrees(Math.Atan(0.91764 * Math.Tan(ToRadians(trueLongitude))));
            rightAscension = NormalizeDegrees(rightAscension);
            var longitudeQuadrant = Math.Floor(trueLongitude / 90.0) * 90.0;
            var rightAscensionQuadrant = Math.Floor(rightAscension / 90.0) * 90.0;
            rightAscension = (rightAscension + longitudeQuadrant - rightAscensionQuadrant) / 15.0;

            var sinDeclination = 0.39782 * Math.Sin(ToRadians(trueLongitude));
            var cosDeclination = Math.Cos(Math.Asin(sinDeclination));
            var cosHourAngle = (Math.Cos(ToRadians(90.833)) - (sinDeclination * Math.Sin(ToRadians(CarmagnolaLatitude)))) /
                               (cosDeclination * Math.Cos(ToRadians(CarmagnolaLatitude)));

            if (cosHourAngle < -1 || cosHourAngle > 1)
                return new TimeSpan(18, 0, 0);

            var hourAngle = ToDegrees(Math.Acos(cosHourAngle)) / 15.0;
            var localMeanTime = hourAngle + rightAscension - (0.06571 * approximateTime) - 6.622;
            var utcHours = NormalizeHours(localMeanTime - longitudeHour);
            var utcDate = DateTime.SpecifyKind(date.Date.AddHours(utcHours), DateTimeKind.Utc);
            var localDate = TimeZoneInfo.ConvertTimeFromUtc(utcDate, GetRomeTimeZone());

            return new TimeSpan(localDate.Hour, localDate.Minute, 0);
        }

        private static TimeZoneInfo GetRomeTimeZone()
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Europe/Rome");
            }
            catch (TimeZoneNotFoundException)
            {
                return TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");
            }
        }

        private static TimeSpan RoundDownToHalfHour(TimeSpan value)
        {
            var minutes = Math.Floor(value.TotalMinutes / 30.0) * 30;
            return TimeSpan.FromMinutes(minutes);
        }

        private static TimeSpan Min(TimeSpan a, TimeSpan b) => a <= b ? a : b;

        private static TimeSpan Max(TimeSpan a, TimeSpan b) => a >= b ? a : b;

        private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;

        private static double ToDegrees(double radians) => radians * 180.0 / Math.PI;

        private static double NormalizeDegrees(double value) => (value % 360.0 + 360.0) % 360.0;

        private static double NormalizeHours(double value) => (value % 24.0 + 24.0) % 24.0;
    }
}
