using Full_Metal_Paintball_Carmagnola.Controllers;
using Full_Metal_Paintball_Carmagnola.Models;

namespace Full_Metal_Paintball_Carmagnola.Services;

public record EventProposal(DateTime Data, string Inizio, string Fine, string Tramonto,
    string Chiusura, int AnnoStorico, int Campioni, double? Occupazione, int Partite,
    string[] DateConfronto);

public static class EventPlanner
{
    public static List<EventProposal> Calculate(DateTime from, DateTime to, int minutes, string days,
        DateTime today, List<Partita> bookings, List<CampoChiusura> closures,
        HashSet<DateTime> unknownDays)
    {
        var result = new List<EventProposal>();
        var active = bookings.Where(p => !p.IsDeleted).ToList();
        var confirmed = active.Where(p => p.CaparraConfermata).ToList();
        var firstRecorded = bookings.Count == 0 ? DateTime.MaxValue : bookings.Min(p => p.Data.Date);
        for (var date = from.Date; date <= to.Date; date = date.AddDays(1))
        {
            if (date < today.Date || date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)
                || days == "sabato" && date.DayOfWeek != DayOfWeek.Saturday
                || days == "domenica" && date.DayOfWeek != DayOfWeek.Sunday
                || unknownDays.Contains(date)) continue;
            var available = DisponibilitaCampoController.BuildGiorno(date,
                active.Where(p => p.Data.Date == date).ToList(), closures);
            var anchor = date.AddYears(-1);
            var historicalDays = Enumerable.Range(-3, 7).Select(offset => anchor.AddDays(offset))
                .Where(d => d.Year == anchor.Year && d.DayOfWeek == date.DayOfWeek
                    && d >= firstRecorded && d < today.Date && !unknownDays.Contains(d)).ToList();
            for (var start = available.Apertura; start.Add(TimeSpan.FromMinutes(minutes)) <= available.UltimaFinePartita;
                start = start.Add(TimeSpan.FromMinutes(30)))
            {
                var end = start.Add(TimeSpan.FromMinutes(minutes));
                if (date == today.Date && start <= today.TimeOfDay || !available.IsAvailable(start, end)) continue;
                var samples = new List<DateTime>();
                double occupied = 0;
                int games = 0;
                foreach (var past in historicalDays)
                {
                    // A closed or unlit interval is not evidence of low customer demand.
                    var daylight = DisponibilitaCampoController.BuildGiorno(past, new(), closures);
                    if (!daylight.IsAvailable(start, end)) continue;
                    var matches = confirmed.Where(p => p.Data.Date == past
                        && p.OraInizio < end && p.OraInizio.Add(TimeSpan.FromHours(p.Durata)) > start).ToList();
                    games += matches.Count;
                    var intervals = matches.Select(p => (Start: Math.Max(start.TotalMinutes, p.OraInizio.TotalMinutes),
                        End: Math.Min(end.TotalMinutes, p.OraInizio.TotalMinutes + p.Durata * 60)))
                        .OrderBy(x => x.Start);
                    double cursor = start.TotalMinutes;
                    foreach (var interval in intervals)
                    {
                        occupied += Math.Max(0, interval.End - Math.Max(cursor, interval.Start));
                        cursor = Math.Max(cursor, interval.End);
                    }
                    samples.Add(past);
                }
                result.Add(new(date, start.ToString(@"hh\:mm"), end.ToString(@"hh\:mm"),
                    available.Tramonto.ToString(@"hh\:mm"), available.UltimaFinePartita.ToString(@"hh\:mm"),
                    anchor.Year, samples.Count, samples.Count == 0 ? null : Math.Round(occupied / (samples.Count * minutes) * 100, 1),
                    games, samples.Select(d => d.ToString("dd/MM/yyyy")).ToArray()));
            }
        }
        // One best continuous interval per day; never promote missing history as zero demand.
        return result.GroupBy(p => p.Data).Select(g => g.OrderBy(p => p.Campioni == 0)
            .ThenBy(p => p.Occupazione ?? double.MaxValue).ThenByDescending(p => p.Campioni).ThenBy(p => p.Inizio).First())
            .OrderBy(p => p.Campioni == 0).ThenBy(p => p.Occupazione ?? double.MaxValue)
            .ThenByDescending(p => p.Campioni).ThenBy(p => p.Data).ToList();
    }
}
