using System.ComponentModel.DataAnnotations;
using Full_Metal_Paintball_Carmagnola.Models;
using Full_Metal_Paintball_Carmagnola.Services;

var passed = 0;
var failed = 0;
void Run(string name, Action test)
{
    try { test(); passed++; }
    catch (Exception ex) { failed++; Console.Error.WriteLine($"FAIL {name}: {ex.Message}"); }
}

for (var n = 2; n <= 64; n++)
for (var g = 1; g <= n / 2; g++)
foreach (var ar in new[] { false, true })
    Run($"round-robin n={n} groups={g} AR={ar}", () => Checks.Schedule(n, g, ar));

for (var n = 2; n <= 64; n++)
foreach (var third in new[] { false, true }.Where(x => !x || n >= 4))
foreach (var away in new[] { false, true })
    Run($"finals qualifiers={n} third={third} away={away}", () => Checks.Finals(n, third, away));

Run("decimal standings, counters, pending and non-group exclusion", Checks.Standings);
Run("ranking score toggles and deterministic fallback", Checks.Ranking);
Run("generation guards are atomic", Checks.Guards);
Run("qualification tie guard and explicit priorities", Checks.Ties);
Run("multi-group serpentine qualification", Checks.Seeding);
for (var groups = 2; groups <= 32; groups++)
for (var qualified = 1; qualified <= 32 && groups * Math.Max(2, qualified) <= 64; qualified++)
    Run($"cross-group opening round groups={groups} qualified={qualified}", () => Checks.CrossGroupSeeding(groups, qualified));
Run("form boundary validation", Checks.Forms);
// Invalid knockout draws must never silently select the away side.
Run("knockout draw cannot advance", Checks.KnockoutDraw);

if (args.Contains("--postgres"))
{
    try { await DatabaseChecks.Run(); passed++; Console.WriteLine("PASS PostgreSQL schema/persistence/concurrency/cascade"); }
    catch (Exception ex) { failed++; Console.Error.WriteLine($"FAIL PostgreSQL: {ex}"); }
}
else Console.WriteLine("SKIP PostgreSQL (enable with --postgres; local disposable test database only).");
Console.WriteLine($"Tornei.Checks: {passed} passed, {failed} failed.");
return failed == 0 ? 0 : 1;

internal static class Checks
{
    public static void That(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException(message);
    }

    public static Torneo Tournament(int n, int groups = 1) => new()
    {
        Nome = "Engine checks", Data = new DateTime(2026, 10, 8, 0, 0, 0, DateTimeKind.Utc),
        NumeroSquadre = n, NumeroGironi = groups, PuntiVittoria = 2.75m,
        PuntiPareggio = 1.125m, PuntiSconfitta = 0.125m,
        Squadre = Enumerable.Range(1, n).Select(i => new TorneoSquadra
        {
            Id = 100 + i * 7, Nome = $"Team {i}", Girone = (i - 1) % groups + 1, OrdineSpareggio = i
        }).Reverse().ToList()
    };

    static string Snapshot(Torneo t) => string.Join(";", t.Incontri.Select(m =>
        $"{m.Fase}/{m.Girone}/{m.Turno}/{m.Posizione}/{m.CasaId}/{m.OspiteId}/{m.Esito}"));

    static void Reject(Torneo t, Action action)
    {
        var before = Snapshot(t);
        var rejected = false;
        try { action(); } catch (InvalidOperationException) { rejected = true; }
        That(rejected, "Expected InvalidOperationException");
        That(Snapshot(t) == before, "Rejected operation partially mutated matches");
    }

    public static void Schedule(int n, int groups, bool ar)
    {
        var t = Tournament(n, groups); t.AndataRitorno = ar;
        TorneoEngine.GenerateGroups(t);
        foreach (var group in t.Squadre.GroupBy(s => s.Girone))
        {
            var ids = group.Select(s => s.Id).ToHashSet();
            var matches = t.Incontri.Where(m => m.Girone == group.Key).ToList();
            var k = ids.Count; var legs = ar ? 2 : 1; var rounds = k % 2 == 0 ? k - 1 : k;
            That(matches.Count == k * (k - 1) / 2 * legs, "Wrong match count");
            That(matches.All(m => m.Fase == "Girone" && m.Esito == null && m.CasaId != m.OspiteId
                && ids.Contains(m.CasaId ?? -1) && ids.Contains(m.OspiteId ?? -1)), "Self match, cross-group team or bad initial state");
            That(matches.Select(m => m.Turno).Distinct().Order().SequenceEqual(Enumerable.Range(1, rounds * legs)), "Wrong round numbers");
            foreach (var round in matches.GroupBy(m => m.Turno))
            {
                That(round.Count() == k / 2, "Round missing fixtures");
                That(round.SelectMany(m => new[] { m.CasaId, m.OspiteId }).Distinct().Count() == round.Count() * 2, "Team plays twice in round");
                That(round.Select(m => m.Posizione).Distinct().Count() == round.Count(), "Duplicate fixture position");
            }
            foreach (var a in ids)
            foreach (var b in ids.Where(b => b > a))
            {
                var pair = matches.Where(m => m.CasaId == a && m.OspiteId == b || m.CasaId == b && m.OspiteId == a).ToList();
                That(pair.Count == legs, $"Pair {a}/{b} missing or repeated");
                if (ar) That(pair[0].CasaId == pair[1].OspiteId && pair[0].OspiteId == pair[1].CasaId
                    && Math.Abs(pair[0].Turno - pair[1].Turno) == rounds, "Return fixture not reversed/offset");
            }
            foreach (var id in ids)
                That(matches.Count(m => m.CasaId == id || m.OspiteId == id) == (k - 1) * legs, "Incorrect games per team");
        }
        var copy = Tournament(n, groups); copy.AndataRitorno = ar; TorneoEngine.GenerateGroups(copy);
        That(Snapshot(copy) == Snapshot(t), "Generation is not deterministic");
        Reject(t, () => TorneoEngine.GenerateGroups(t));
    }

    public static Torneo Ready(int n, int groups = 1, int? qualified = null)
    {
        var t = Tournament(n, groups); t.FasiFinali = true; t.QualificatePerGirone = qualified ?? n;
        TorneoEngine.GenerateGroups(t);
        foreach (var m in t.Incontri) m.Esito = "Pareggio";
        return t;
    }

    public static void Finals(int n, bool third, bool away)
    {
        var t = Ready(n); t.FinaleTerzoPosto = third;
        TorneoEngine.GenerateFinals(t);
        var size = 2; while (size < n) size *= 2;
        var first = t.Incontri.Where(m => m.Fase != "Girone" && m.Turno == 1).ToList();
        That(first.Count == size / 2, "Bracket size");
        That(first.Count(m => m.OspiteId == null) == size - n, "Wrong bye count");
        That(first.Where(m => m.OspiteId == null).All(m => m.CasaId != null && m.Esito == "Casa"), "Bye not auto-awarded");
        That(first.SelectMany(m => new[] { m.CasaId, m.OspiteId }).Where(id => id != null).Order()
            .SequenceEqual(t.Squadre.Select(s => (int?)s.Id).Order()), "Qualifier missing or duplicated");
        var expectedByes = t.Squadre.OrderBy(s => s.OrdineSpareggio).Take(size - n).Select(s => (int?)s.Id).Order();
        That(first.Where(m => m.OspiteId == null).Select(m => m.CasaId).Order().SequenceEqual(expectedByes), "Byes not assigned to top seeds");
        for (var round = 1; ; round++)
        {
            var current = t.Incontri.Where(m => m.Fase is "Eliminazione" or "Finale" && m.Turno == round).OrderBy(m => m.Posizione).ToList();
            That(current.Count > 0, "Advancement stalled");
            var pending = current.Where(m => m.Esito == null).ToList();
            if (pending.Count > 0)
            {
                foreach (var m in pending.Skip(1)) m.Esito = away ? "Ospite" : "Casa";
                var before = Snapshot(t); TorneoEngine.Advance(t);
                That(before == Snapshot(t), "Advanced incomplete round");
                pending[0].Esito = away ? "Ospite" : "Casa";
            }
            TorneoEngine.Advance(t);
            var after = Snapshot(t); TorneoEngine.Advance(t);
            That(after == Snapshot(t), "Repeated advancement duplicated fixtures");
            if (current.Count == 1) break;
            var next = t.Incontri.Where(m => m.Fase is "Eliminazione" or "Finale" && m.Turno == round + 1).OrderBy(m => m.Posizione).ToList();
            var winners = current.Select(m => m.Esito == "Casa" ? m.CasaId : m.OspiteId).ToArray();
            That(next.SelectMany(m => new[] { m.CasaId, m.OspiteId }).SequenceEqual(winners), "Wrong advancing winners/order");
            That(next.All(m => m.CasaId != null && m.OspiteId != null && m.CasaId != m.OspiteId), "Invalid next-round fixture");
            if (current.Count == 2 && third)
            {
                var bronze = t.Incontri.Single(m => m.Fase == "Terzo posto");
                That(new[] { bronze.CasaId, bronze.OspiteId }.SequenceEqual(current.Select(m => m.Esito == "Casa" ? m.OspiteId : m.CasaId)), "Wrong semifinal losers");
                That(bronze.CasaId != null && bronze.OspiteId != null, "Third-place bye");
            }
        }
        That(t.Incontri.Count(m => m.Fase == "Finale") == 1, "Must have exactly one final");
        That(t.Incontri.Count(m => m.Fase == "Terzo posto") == (third ? 1 : 0), "Third-place toggle ignored");
        That(t.Incontri.Count(m => m.Fase is "Eliminazione" or "Finale" && m.OspiteId != null) == n - 1, "Wrong number of competitive knockout matches");
        Reject(t, () => TorneoEngine.GenerateFinals(t));
    }

    public static void Standings()
    {
        var t = Tournament(4); var ids = t.Squadre.OrderBy(s => s.Id).Select(s => s.Id).ToArray();
        void Add(int a, int b, string? result, int? x, int? y, string phase = "Girone") => t.Incontri.Add(new()
            { CasaId = ids[a], OspiteId = ids[b], Girone = 1, Esito = result, PuntiCasa = x, PuntiOspite = y, Fase = phase });
        Add(0, 1, "Casa", 5, 2); Add(0, 2, "Pareggio", 3, 3); Add(1, 2, "Ospite", null, null);
        Add(0, 3, null, 100, 0); Add(0, 3, "Casa", 100, 0, "Finale");
        var rows = TorneoEngine.Standings(t)[1].ToDictionary(r => r.Squadra.Id);
        var a = rows[ids[0]]; var b = rows[ids[1]]; var c = rows[ids[2]]; var d = rows[ids[3]];
        That(a.Punti == 3.875m && b.Punti == .25m && c.Punti == 3.875m && d.Punti == 0, "Decimal W/D/L awards incorrect");
        That(a.Giocate == 2 && a.Vinte == 1 && a.Pareggi == 1 && a.Perse == 0 && a.Fatti == 8 && a.Subiti == 5, "Home counters");
        That(b.Giocate == 2 && b.Perse == 2 && b.Fatti == 2 && b.Subiti == 5 && c.Vinte == 1 && c.Pareggi == 1 && d.Giocate == 0, "Away/null/pending counters");
        That(rows.Values.Sum(r => r.Fatti) == rows.Values.Sum(r => r.Subiti), "Score totals not conserved");
        t.PuntiVittoria = .1m; t.PuntiPareggio = .2m; t.PuntiSconfitta = .3m;
        That(TorneoEngine.Standings(t)[1][0].Squadra.Id == ids[1], "Configurable loss points not honored");
    }

    public static void Ranking()
    {
        var t = Tournament(4); var s = t.Squadre.OrderBy(x => x.Id).ToArray();
        t.PuntiVittoria = t.PuntiPareggio = t.PuntiSconfitta = 0;
        t.Incontri.Add(new() { Girone = 1, CasaId = s[0].Id, OspiteId = s[1].Id, Esito = "Casa", PuntiCasa = 2, PuntiOspite = 1 });
        t.Incontri.Add(new() { Girone = 1, CasaId = s[2].Id, OspiteId = s[3].Id, Esito = "Casa", PuntiCasa = 5, PuntiOspite = 4 });
        t.RegistraPunteggio = true;
        That(TorneoEngine.Standings(t)[1].Select(r => r.Squadra.Id).SequenceEqual(new[] { s[2].Id, s[0].Id, s[3].Id, s[1].Id }), "Difference then goals ordering");
        t.RegistraPunteggio = false;
        That(TorneoEngine.Standings(t)[1].Select(r => r.Squadra.Id).SequenceEqual(s.Select(x => x.Id)), "Scores used when disabled");
        foreach (var team in s) team.OrdineSpareggio = 0;
        That(TorneoEngine.Standings(t)[1].Select(r => r.Squadra.Id).SequenceEqual(s.Select(x => x.Id)), "ID fallback unstable");
    }

    public static void Guards()
    {
        var t = Tournament(4); t.NumeroSquadre = 5; Reject(t, () => TorneoEngine.GenerateGroups(t));
        t.NumeroSquadre = 4; t.Squadre[0].Girone = 0; Reject(t, () => TorneoEngine.GenerateGroups(t));
        t.Squadre[0].Girone = 2; Reject(t, () => TorneoEngine.GenerateGroups(t));
        t.NumeroGironi = 2; Reject(t, () => TorneoEngine.GenerateGroups(t));
        t = Tournament(4, 2); t.FasiFinali = true; t.QualificatePerGirone = 3; Reject(t, () => TorneoEngine.GenerateGroups(t));
        t = Tournament(4); t.FasiFinali = true; Reject(t, () => TorneoEngine.GenerateFinals(t));
        TorneoEngine.GenerateGroups(t); Reject(t, () => TorneoEngine.GenerateFinals(t));
        t = Ready(4); t.FasiFinali = false; Reject(t, () => TorneoEngine.GenerateFinals(t));
        t = Tournament(2); TorneoEngine.Advance(t); That(t.Incontri.Count == 0, "Empty advance mutated tournament");
    }

    public static void Ties()
    {
        foreach (var score in new[] { false, true })
        foreach (var priority in new[] { 0, -1, 1 })
        {
            var t = Ready(4); t.RegistraPunteggio = score;
            foreach (var s in t.Squadre) s.OrdineSpareggio = priority;
            Reject(t, () => TorneoEngine.GenerateFinals(t));
        }
        var resolved = Ready(4); TorneoEngine.GenerateFinals(resolved);
        That(resolved.Incontri.Any(m => m.Fase == "Eliminazione"), "Explicit distinct priorities rejected");
        var cutoff = Ready(4, 1, 2); foreach (var s in cutoff.Squadre) s.OrdineSpareggio = 0;
        Reject(cutoff, () => TorneoEngine.GenerateFinals(cutoff));
    }

    public static void Seeding()
    {
        var t = Ready(12, 3, 2); TorneoEngine.GenerateFinals(t);
        var standings = TorneoEngine.Standings(t);
        var seeds = new[] { standings[1][0], standings[2][0], standings[3][0], standings[3][1], standings[2][1], standings[1][1] }.Select(r => (int?)r.Squadra.Id).ToArray();
        var actual = t.Incontri.Where(m => m.Fase != "Girone" && m.Turno == 1).OrderBy(m => m.Posizione).SelectMany(m => new[] { m.CasaId, m.OspiteId });
        That(actual.SequenceEqual(new[] { seeds[0], null, seeds[3], seeds[4], seeds[1], null, seeds[2], seeds[5] }), "Serpentine seeds/bracket positions");
    }

    public static void Forms()
    {
        bool Valid(TorneoForm f) => Validator.TryValidateObject(f, new ValidationContext(f), new List<ValidationResult>(), true);
        That(Valid(new() { Nome = "Valid" }), "Default form invalid");
        That(Valid(new() { Nome = "Minimum", NumeroSquadre = 2, NumeroGironi = 1, QualificatePerGirone = 2 }), "Two-team form invalid");
        That(!Valid(new() { Nome = "Invalid", NumeroSquadre = 65 }), "65 teams accepted");
        That(!Valid(new() { Nome = "Invalid", NumeroSquadre = 1 }), "One team accepted");
        That(!Valid(new() { Nome = "Invalid", NumeroGironi = 5 }), "Singleton groups accepted");
        That(!Valid(new() { Nome = "Invalid", NumeroGironi = 1, QualificatePerGirone = 1 }), "One finalist accepted");
        That(!Valid(new() { Nome = "Invalid", FinaleTerzoPosto = true, FasiFinali = false }), "Third place without finals accepted");
        That(!Valid(new() { Nome = "Invalid", NumeroGironi = 1, QualificatePerGirone = 3, FinaleTerzoPosto = true }), "Three qualifiers with third place accepted");
        That(Valid(new() { Nome = "Decimal", PuntiVittoria = 2.75m, PuntiPareggio = 1.125m, PuntiSconfitta = .125m }), "Decimal points rejected");
    }

    public static void CrossGroupSeeding(int groups, int qualified)
    {
        var t = Ready(groups * Math.Max(2, qualified), groups, qualified);
        TorneoEngine.GenerateFinals(t);
        var map = t.Squadre.ToDictionary(s => s.Id, s => s.Girone);
        var opening = t.Incontri.Where(m => m.Fase != "Girone" && m.Turno == 1).ToList();
        That(opening.Where(m => m.OspiteId != null).All(m => map[m.CasaId!.Value] != map[m.OspiteId!.Value]), "Same-group opening fixture");
        var seeds = Enumerable.Range(0, qualified).SelectMany(rank => TorneoEngine.Standings(t).OrderBy(g => rank % 2 == 0 ? g.Key : -g.Key).Select(g => (int?)g.Value[rank].Squadra.Id)).ToList();
        var size = 2; while (size < seeds.Count) size *= 2;
        That(opening.SelectMany(m => new[] { m.CasaId, m.OspiteId }).Where(id => id != null).Order().SequenceEqual(seeds.Order()), "Qualifier lost or duplicated by matching");
        That(opening.Where(m => m.OspiteId == null).Select(m => m.CasaId).Order().SequenceEqual(seeds.Take(size - seeds.Count).Order()), "Matching changed top-seed byes");
        That(opening.Select(m => m.CasaId).Order().SequenceEqual(seeds.Take(size / 2).Order()), "Matching moved a higher seed");
        var again = Ready(groups * Math.Max(2, qualified), groups, qualified); TorneoEngine.GenerateFinals(again);
        That(Snapshot(t) == Snapshot(again), "Cross-group matching is not deterministic");
        if (groups == 2 && qualified == 2)
        {
            var standings = TorneoEngine.Standings(t);
            That(opening[0].CasaId == standings[1][0].Squadra.Id && opening[0].OspiteId == standings[2][1].Squadra.Id
                && opening[1].CasaId == standings[2][0].Squadra.Id && opening[1].OspiteId == standings[1][1].Squadra.Id, "Expected A1-B2 and B1-A2 semifinals");
        }
    }

    public static void KnockoutDraw()
    {
        foreach (var result in new[] { "Pareggio", "", "invalid" })
        {
            var t = Ready(4); TorneoEngine.GenerateFinals(t);
            foreach (var m in t.Incontri.Where(m => m.Fase == "Eliminazione")) m.Esito = result;
            Reject(t, () => TorneoEngine.Advance(t));
        }
    }
}
