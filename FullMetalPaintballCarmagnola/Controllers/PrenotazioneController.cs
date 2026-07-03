using System.Globalization;
using Full_Metal_Paintball_Carmagnola.Models;
using Full_Metal_Paintball_Carmagnola.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Full_Metal_Paintball_Carmagnola.Controllers
{
    [AllowAnonymous]
    public class PrenotazioneController : Controller
    {
        private static readonly TimeSpan AperturaCampo = new(9, 0, 0);
        private static readonly TimeSpan MargineCampo = TimeSpan.FromMinutes(30);
        private static readonly TimeSpan DurataMinimaPartita = TimeSpan.FromHours(1);
        private static readonly TimeSpan AnticipoMinimoPrenotazione = TimeSpan.FromHours(4);
        private const double CarmagnolaLatitude = 44.849;
        private const double CarmagnolaLongitude = 7.720;

        private readonly TesseramentoDbContext _dbContext;
        private readonly PricingCatalogService _pricingCatalogService;

        public PrenotazioneController(TesseramentoDbContext dbContext, PricingCatalogService pricingCatalogService)
        {
            _dbContext = dbContext;
            _pricingCatalogService = pricingCatalogService;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, GetRomeTimeZone());
            var oggi = DateTime.SpecifyKind(now.Date, DateTimeKind.Utc);
            var fine = oggi.AddMonths(6);
            var it = CultureInfo.GetCultureInfo("it-IT");

            var weekendDates = Enumerable.Range(0, (fine - oggi).Days + 1)
                .Select(offset => oggi.AddDays(offset))
                .Where(date => date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                .ToList();

            var partiteWeekend = await _dbContext.Partite
                .Where(p => !p.IsDeleted && p.Data >= oggi && p.Data <= fine)
                .OrderBy(p => p.Data)
                .ThenBy(p => p.OraInizio)
                .ToListAsync();

            partiteWeekend = partiteWeekend
                .Where(p => p.Data.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                .ToList();

            var partitePerGiorno = partiteWeekend
                .GroupBy(p => p.Data.Date)
                .ToDictionary(g => g.Key, g => g.ToList());
            var chiusure = await _dbContext.CampoChiusure
                .Where(c => c.DataFine >= oggi && c.DataInizio <= fine)
                .ToListAsync();
            var dateChiusure = chiusure
                .SelectMany(c => Enumerable.Range(0, (c.DataFine.Date - c.DataInizio.Date).Days + 1)
                    .Select(offset => c.DataInizio.Date.AddDays(offset).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)))
                .Distinct()
                .ToList();
            var catalog = await _pricingCatalogService.GetCatalogAsync();
            var currentListinoId = catalog.GetCurrentListino().Id;

            var model = new PrenotazionePubblicaViewModel
            {
                Giorni = weekendDates
                    .Where(data => !IsChiuso(chiusure, data))
                    .Select(data => BuildGiornoPubblico(
                        data,
                        partitePerGiorno.TryGetValue(data.Date, out var partite) ? partite : new List<Partita>(),
                        now,
                        it))
                    .Where(giorno => giorno.Slot.Count > 0)
                    .ToList(),
                Faq = BuildFaq(catalog, currentListinoId),
                PrimaDataInfrasettimanale = now.Date.AddDays(7),
                DateChiusure = dateChiusure
            };

            return View(model);
        }

        private static bool IsChiuso(List<CampoChiusura> chiusure, DateTime data)
        {
            return chiusure.Any(c => c.DataInizio.Date <= data.Date && c.DataFine.Date >= data.Date);
        }

        private static List<PrenotazionePubblicaFaqViewModel> BuildFaq(PricingCatalog catalog, short listinoId)
        {
            string Price(string code) =>
                catalog.GetEntry(code)?.GetPrice(listinoId).ToString("0", CultureInfo.InvariantCulture) ?? "0";

            return new List<PrenotazionePubblicaFaqViewModel>
            {
                new()
                {
                    Domanda = "Il campo viene riservato solo al nostro gruppo?",
                    Risposta = "Sì. Il campo viene riservato al gruppo prenotante per la fascia concordata."
                },
                new()
                {
                    Domanda = "Come si conferma la prenotazione?",
                    Risposta = "La prenotazione deve essere confermata via SMS o WhatsApp al numero 3468741192. Lo staff è reperibile telefonicamente dal lunedì al venerdì dalle 18:00 alle 21:00."
                },
                new()
                {
                    Domanda = "Con quanto anticipo bisogna prenotare?",
                    Risposta = "Per i turni del weekend è richiesto almeno 24 ore di preavviso, salvo disponibilità del campo. Per turni infrasettimanali sono richiesti almeno 7 giorni di preavviso, salvo disponibilità del campo e dello staff."
                },
                new()
                {
                    Domanda = "Quali dati servono per prenotare?",
                    Risposta = "Servono numero indicativo di partecipanti, orario di inizio partita, ore di gioco e nome/cognome di una persona di riferimento della prenotazione."
                },
                new()
                {
                    Domanda = "Quanti partecipanti servono?",
                    Risposta = "Il gruppo minimo è di 8 partecipanti e il massimo consigliato è 16. Se siete meno del minimo richiesto, occorre coprire la differenza per le persone mancanti, escluso il tesseramento da 5€. Se siete più di 16, può essere necessario organizzare un torneo a squadre."
                },
                new()
                {
                    Domanda = "Quanto costa una partita adulti?",
                    Risposta = $"Costi adulti: 1 ora {Price(PricingEntryCodes.AdultStandard1Hour)}€ con tesseramento e 200 colpi a testa, oppure {Price(PricingEntryCodes.AdultUnlimited1Hour)}€ con colpi illimitati. 1 ora e mezza {Price(PricingEntryCodes.AdultStandard90Minutes)}€ con tesseramento e 300 colpi a testa, oppure {Price(PricingEntryCodes.AdultUnlimited90Minutes)}€ con colpi illimitati. 2 ore {Price(PricingEntryCodes.AdultStandard2Hours)}€ con tesseramento e 400 colpi a testa."
                },
                new()
                {
                    Domanda = "Quanto costa una partita kids?",
                    Risposta = $"Costi kids: 1 ora {Price(PricingEntryCodes.Kids1Hour)}€ a testa con tesseramento e colpi illimitati. 1 ora e mezza {Price(PricingEntryCodes.Kids90Minutes)}€ a testa con tesseramento e colpi illimitati. 2 ore {Price(PricingEntryCodes.Kids2Hours)}€ a testa con tesseramento e colpi illimitati."
                },
                new()
                {
                    Domanda = "Cosa include il prezzo?",
                    Risposta = "I prezzi includono tesseramento, colpi indicati o illimitati in base alla formula scelta, e affitto dell'attrezzatura protettiva: pettorine, para collo, guanti e casco."
                },
                new()
                {
                    Domanda = "È adatto a chi gioca per la prima volta?",
                    Risposta = "Sì. Per chi è alla prima esperienza consigliamo di iniziare con il classico deathmatch a squadre, poi lo staff potrà valutare eventuali variazioni di modalità durante la partita."
                },
                new()
                {
                    Domanda = "Cos'è la caccia al coniglio?",
                    Risposta = $"Solo per adulti, per addii al celibato/nubilato e compleanni proponiamo la caccia al coniglio: 1 costume costa {Price(PricingEntryCodes.RabbitSingle)}€ da dividere tra tutti. Con 2 costumi il costo è {Price(PricingEntryCodes.RabbitDouble)}€. Il festeggiato indossa il costume, cerca il fucile nascosto nel campo e poi gli altri partecipanti entrano con una manciata di colpi."
                },
                new()
                {
                    Domanda = "Cosa succede in caso di maltempo?",
                    Risposta = "In caso di maltempo la partita viene disputata ugualmente, salvo completa impraticabilità del campo. La valutazione finale spetta allo staff."
                },
                new()
                {
                    Domanda = "È richiesta una caparra?",
                    Risposta = "Sì. È richiesto il pagamento di una caparra tramite Satispay o bonifico pari a una quota di partecipazione della prenotazione. La caparra conferma l'impegno del cliente e non viene rimborsata in caso di disdetta da parte del cliente."
                },
                new()
                {
                    Domanda = "Si possono fare conti separati?",
                    Risposta = "È preferibile non eseguire conti separati. Il saldo può essere effettuato in contanti o tramite Satispay a fine partita."
                },
                new()
                {
                    Domanda = "Ci sono spogliatoi o docce?",
                    Risposta = "È disponibile uno spogliatoio, ma non sono presenti docce."
                },
                new()
                {
                    Domanda = "A che ora bisogna arrivare?",
                    Risposta = "È richiesto l'arrivo al campo 15/20 minuti prima della prenotazione. L'ora o le ore di gioco vengono conteggiate dall'orario prenotato, anche in caso di ritardo."
                },
                new()
                {
                    Domanda = "Chi usa occhiali da vista può giocare?",
                    Risposta = "Per chi usa occhiali da vista è richiesto, quando possibile, l'utilizzo di lenti a contatto."
                },
                new()
                {
                    Domanda = "Qual è l'età minima?",
                    Risposta = "Per la modalità adulti l'età minima è 9/10 anni, anche più piccoli se alti almeno 130 cm. Per la modalità kids l'età minima è 8 anni e l'età massima è 13 anni."
                },
                new()
                {
                    Domanda = "Si può usare il campo per rinfresco o taglio torta?",
                    Risposta = "Sì, il campo è a disposizione per rinfreschi o taglio torta nell'area preposta. I rifiuti devono essere portati via e l'area deve essere lasciata come trovata."
                }
            };
        }

        private static PrenotazionePubblicaGiornoViewModel BuildGiornoPubblico(
            DateTime data,
            List<Partita> partite,
            DateTime now,
            CultureInfo culture)
        {
            var fasce = BuildFasce(data, partite)
                .Where(f => f.Prenotabile)
                .Select(f => ApplyMinimumNotice(data, f, now))
                .Where(f => f != null && f.Prenotabile)
                .Cast<CampoFasciaViewModel>()
                .Select(f => new PrenotazionePubblicaSlotViewModel
                {
                    Data = data.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    Giorno = data.ToString("dd/MM/yyyy", culture),
                    Inizio = f.Inizio.ToString(@"hh\:mm", CultureInfo.InvariantCulture),
                    Fine = f.Fine.ToString(@"hh\:mm", CultureInfo.InvariantCulture),
                    DurataMassima = Math.Floor(f.DurataOre * 2) / 2
                })
                .ToList();

            return new PrenotazionePubblicaGiornoViewModel
            {
                Data = data,
                GiornoLabel = culture.TextInfo.ToTitleCase(data.ToString("dddd", culture)),
                DataLabel = data.ToString("dd MMMM yyyy", culture),
                Slot = fasce
            };
        }

        private static CampoFasciaViewModel? ApplyMinimumNotice(DateTime data, CampoFasciaViewModel fascia, DateTime now)
        {
            if (data.Date != now.Date || now.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
                return fascia;

            var earliestStart = RoundUpToHalfHour(now.TimeOfDay.Add(AnticipoMinimoPrenotazione));
            if (earliestStart > fascia.Inizio)
                fascia.Inizio = earliestStart;

            if (fascia.Fine - fascia.Inizio < DurataMinimaPartita)
                return null;

            return fascia;
        }

        private static List<CampoFasciaViewModel> BuildFasce(DateTime data, List<Partita> partite)
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
                if (occupata.Fine > cursore)
                    cursore = occupata.Fine;
            }

            AddFasciaLibera(fasce, cursore, ultimaFinePartita);
            return fasce;
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
                Stato = "Libero",
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
            }

            return result;
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
            var minutes = Math.Floor(value.TotalMinutes / 30) * 30;
            return TimeSpan.FromMinutes(minutes);
        }

        private static TimeSpan RoundUpToHalfHour(TimeSpan value)
        {
            var minutes = Math.Ceiling(value.TotalMinutes / 30) * 30;
            return TimeSpan.FromMinutes(minutes);
        }

        private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;

        private static double ToDegrees(double radians) => radians * 180.0 / Math.PI;

        private static double NormalizeDegrees(double degrees)
        {
            degrees %= 360.0;
            return degrees < 0 ? degrees + 360.0 : degrees;
        }

        private static double NormalizeHours(double hours)
        {
            hours %= 24.0;
            return hours < 0 ? hours + 24.0 : hours;
        }

        private static TimeSpan Max(TimeSpan first, TimeSpan second) => first > second ? first : second;

        private static TimeSpan Min(TimeSpan first, TimeSpan second) => first < second ? first : second;
    }
}
