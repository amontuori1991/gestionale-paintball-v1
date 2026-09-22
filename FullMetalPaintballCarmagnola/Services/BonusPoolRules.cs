using Full_Metal_Paintball_Carmagnola.Models;

namespace Full_Metal_Paintball_Carmagnola.Services;

public static class BonusPoolRules
{
    public static DateOnly Today => DateOnly.FromDateTime(
        TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "Europe/Rome"));

    public static DateOnly Monday(DateOnly date) => date.AddDays(-((int)date.DayOfWeek + 6) % 7);
    public static bool IsWeekday(DateOnly date) => date.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday;
    public static bool Contributes(BonusRequest r) => r.Staff == BonusAnswer.Yes
        && r.Customer == BonusAnswer.Yes && r.Outcome == BonusGameOutcome.Played;

    public static BonusCalculation Calculate(BonusWeek week)
    {
        var result = new BonusCalculation
        {
            Blocked = week.Requests.Any(r => r.Staff == BonusAnswer.No),
            Pending = week.Requests.Count(r => r.Staff == BonusAnswer.Pending
                || (r.Staff == BonusAnswer.Yes && (r.Customer == BonusAnswer.Pending
                    || (r.Customer == BonusAnswer.Yes && r.Outcome == BonusGameOutcome.Pending))))
        };
        var played = week.Requests.Where(Contributes).ToList();
        result.Pool = played.Sum(r => week.Rates.ForDuration(r.Minutes));
        result.Shares = played.SelectMany(r => r.Attendees.Distinct(StringComparer.OrdinalIgnoreCase))
            .GroupBy(n => n, StringComparer.OrdinalIgnoreCase)
            .Select(g => new BonusShare { Name = g.Key, Attendances = g.Count() })
            .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase).ToList();
        var count = result.Shares.Sum(s => s.Attendances);
        if (result.Blocked || count == 0) return result;

        // Largest-remainder allocation preserves every cent, including uneven splits.
        var cents = decimal.ToInt64(result.Pool * 100);
        foreach (var share in result.Shares)
            share.Amount = decimal.Floor((decimal)cents * share.Attendances / count) / 100;
        var remainder = cents - decimal.ToInt64(result.Shares.Sum(s => s.Amount) * 100);
        foreach (var share in result.Shares.OrderByDescending(s => (decimal)cents * s.Attendances / count % 1)
                     .ThenBy(s => s.Name, StringComparer.OrdinalIgnoreCase).Take((int)remainder))
            share.Amount += 0.01m;
        return result;
    }
}
