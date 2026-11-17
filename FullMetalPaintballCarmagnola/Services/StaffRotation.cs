namespace Full_Metal_Paintball_Carmagnola.Services;

public record RotationGame(int Id, DateTime Day, TimeSpan Time, double Hours, bool Paid, string? OnCall,
    string? First, string? Second, string? Third, string? Fourth, string? Type, int Players)
{
    public DateTime Start => Day.Date + Time;
    public DateTime End => Start.AddHours(Hours);
    public int Operators => string.Equals(Type?.Trim(), "Kids", StringComparison.OrdinalIgnoreCase) && Players is > 0 and <= 10 ? 1 : 2;
    public IEnumerable<string> Names => new[] { First, Second, Third, Fourth }
        .Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n!.Trim()).Distinct(StringComparer.OrdinalIgnoreCase);
}
public record RotationRow(string Name, RotationGame? Last, RotationGame? Next, int FutureCount);
public record RotationNotice(RotationGame Game, string Text, int Priority);
public record StaffRotation(List<RotationRow> Rows, List<RotationNotice> Notices)
{
    public static StaffRotation Build(DateTime now, List<string> roster, List<RotationGame> games,
        IEnumerable<(DateTime Day, string Name)> present, IEnumerable<(DateTime Day, string? Name)> onCall)
    {
        var cmp = StringComparer.OrdinalIgnoreCase;
        var availability = present.ToLookup(x => x.Day.Date, x => x.Name.Trim());
        var duty = onCall.ToLookup(x => x.Day.Date, x => x.Name?.Trim());
        bool Backup(RotationGame g, string n) => cmp.Equals(g.OnCall?.Trim(), n) || duty[g.Day.Date].Contains(n, cmp);
        var upcoming = games.Where(g => g.End > now).OrderBy(g => g.Start).ThenBy(g => g.Id).ToList();
        var rows = roster.Select(name =>
        {
            var history = games.Where(g => g.Paid && g.End <= now && !cmp.Equals(g.OnCall?.Trim(), name)
                && g.Names.Contains(name, cmp)).MaxBy(g => g.End);
            var future = upcoming.Where(g => g.Start >= now && !Backup(g, name) && g.Names.Contains(name, cmp)).ToList();
            return new RotationRow(name, history, future.FirstOrDefault(), future.Count);
        }).OrderBy(r => r.Last?.End).ThenBy(r => r.Name).ToList();
        var planned = new List<(RotationGame Game, string Name)>();
        var released = new List<string>();
        var notes = new List<RotationNotice>();
        foreach (var g in upcoming.Where(g => g.Start >= now).OrderBy(g => g.Names.Count() >= g.Operators))
        {
            var names = g.Names.ToList();
            var preferred = g.Start.DayOfWeek switch { DayOfWeek.Saturday => "Simone", DayOfWeek.Sunday => "Davide", _ => "" };
            bool Overlaps(RotationGame other) => other.Id != g.Id && other.Start < g.End && other.End > g.Start;
            bool Busy(string n) => upcoming.Any(other => Overlaps(other) && other.Names.Contains(n, cmp))
                || planned.Any(p => Overlaps(p.Game) && cmp.Equals(p.Name, n));
            bool Present(string n) => availability[g.Day.Date].Contains(n, cmp);
            int Load(RotationRow r) => r.FutureCount + planned.Count(p => cmp.Equals(p.Name, r.Name))
                - released.Count(n => cmp.Equals(n, r.Name));
            var candidates = rows.Where(r => !names.Contains(r.Name, cmp) && !Backup(g, r.Name) && Present(r.Name) && !Busy(r.Name))
                .OrderByDescending(r => cmp.Equals(r.Name, preferred)).ThenBy(Load).ThenBy(r => r.Last?.End).ThenBy(r => r.Name).ToList();
            var assigned = rows.Where(r => names.Contains(r.Name, cmp) && !Backup(g, r.Name)).ToList();
            void Suggest(string prefix, int priority, RotationRow? candidate)
            {
                var reason = candidate == null ? "Nessun altro presente e libero." :
                    $"{candidate.Name}: presenza indicata, fascia libera" + (cmp.Equals(candidate.Name, preferred)
                        ? ", preferenza del weekend." : $", {Load(candidate)} incarichi futuri o già suggeriti.");
                notes.Add(new(g, prefix + " " + reason, priority));
                if (candidate != null) planned.Add((g, candidate.Name));
            }
            int missing = g.Operators - names.Count;
            for (int slot = 0; slot < missing; slot++)
            {
                var candidate = candidates.FirstOrDefault();
                Suggest($"Copertura: {names.Count}/{g.Operators} operatori assegnati. Posto {names.Count + slot + 1}:", 0, candidate);
                if (candidate == null) break;
                candidates.Remove(candidate);
            }
            var issues = assigned.Where(r => Busy(r.Name) || !Present(r.Name)).ToList();
            foreach (var r in issues)
            {
                var replacement = candidates.FirstOrDefault();
                Suggest($"{r.Name}: {(Busy(r.Name) ? "incarichi sovrapposti" : "presenza non confermata")}. Alternativa:", 1, replacement);
                if (replacement != null) { candidates.Remove(replacement); released.Add(r.Name); }
            }
            if (issues.Count != 0 || missing > 0) continue;
            var outgoing = assigned.Where(r => !cmp.Equals(r.Name, preferred)).OrderByDescending(Load).FirstOrDefault();
            var alternate = candidates.FirstOrDefault(r => outgoing != null && Load(outgoing) >= Load(r) + 2
                && (r.Last == null || outgoing.Last != null && r.Last.End <= outgoing.Last.End));
            if (alternate != null && outgoing != null)
            {
                Suggest($"Rotazione facoltativa al posto di {outgoing.Name} ({Load(outgoing)} incarichi in programma).", 2, alternate);
                released.Add(outgoing.Name);
            }
        }
        return new(rows, notes.OrderBy(n => n.Priority).ThenBy(n => n.Game.Start).ToList());
    }
}
