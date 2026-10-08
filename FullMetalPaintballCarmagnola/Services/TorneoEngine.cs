using Full_Metal_Paintball_Carmagnola.Models;

namespace Full_Metal_Paintball_Carmagnola.Services;

public static class TorneoEngine
{
    public static Dictionary<int, List<TorneoClassifica>> Standings(Torneo t) => t.Squadre.GroupBy(s => s.Girone).OrderBy(g => g.Key).ToDictionary(g => g.Key, g =>
    {
        var rows = g.ToDictionary(s => s.Id, s => new TorneoClassifica { Squadra = s });
        foreach (var m in t.Incontri.Where(m => m.Fase == "Girone" && m.Girone == g.Key && m.Esito != null))
        {
            var a = rows[m.CasaId!.Value]; var b = rows[m.OspiteId!.Value];
            a.Giocate++; b.Giocate++;
            a.Fatti += m.PuntiCasa ?? 0; a.Subiti += m.PuntiOspite ?? 0;
            b.Fatti += m.PuntiOspite ?? 0; b.Subiti += m.PuntiCasa ?? 0;
            if (m.Esito == "Pareggio") { a.Pareggi++; b.Pareggi++; a.Punti += t.PuntiPareggio; b.Punti += t.PuntiPareggio; }
            else
            {
                var winner = m.Esito == "Casa" ? a : b; var loser = m.Esito == "Casa" ? b : a;
                winner.Vinte++; loser.Perse++; winner.Punti += t.PuntiVittoria; loser.Punti += t.PuntiSconfitta;
            }
        }
        return rows.Values.OrderByDescending(r => r.Punti).ThenByDescending(r => t.RegistraPunteggio ? r.Fatti - r.Subiti : 0)
            .ThenByDescending(r => t.RegistraPunteggio ? r.Fatti : 0).ThenBy(r => r.Squadra.OrdineSpareggio).ThenBy(r => r.Squadra.Id).ToList();
    });

    public static void GenerateGroups(Torneo t)
    {
        if (t.Incontri.Count != 0) throw new InvalidOperationException("Calendario gia generato.");
        if (t.Squadre.Count != t.NumeroSquadre || t.Squadre.Any(s => s.Girone < 1 || s.Girone > t.NumeroGironi)) throw new InvalidOperationException("Controlla le squadre e i gironi.");
        if (Enumerable.Range(1, t.NumeroGironi).Any(g => t.Squadre.Count(s => s.Girone == g) < Math.Max(2, t.FasiFinali ? t.QualificatePerGirone : 2)))
            throw new InvalidOperationException("Ogni girone deve contenere almeno due squadre e tutte le qualificate previste.");
        foreach (var group in t.Squadre.GroupBy(s => s.Girone))
        {
            var ring = group.OrderBy(s => s.Id).Select(s => (int?)s.Id).ToList();
            if (ring.Count % 2 != 0) ring.Add(null);
            var rounds = ring.Count - 1;
            for (var r = 0; r < rounds; r++)
            {
                for (var i = 0; i < ring.Count / 2; i++)
                {
                    var a = ring[i]; var b = ring[ring.Count - 1 - i]; if (a == null || b == null) continue;
                    if (r % 2 == 1) (a, b) = (b, a);
                    t.Incontri.Add(new() { Fase = "Girone", Girone = group.Key, Turno = r + 1, Posizione = i, CasaId = a, OspiteId = b });
                    if (t.AndataRitorno) t.Incontri.Add(new() { Fase = "Girone", Girone = group.Key, Turno = r + 1 + rounds, Posizione = i, CasaId = b, OspiteId = a });
                }
                var last = ring[^1]; ring.RemoveAt(ring.Count - 1); ring.Insert(1, last);
            }
        }
    }

    public static void GenerateFinals(Torneo t)
    {
        if (!t.FasiFinali || t.Incontri.Count == 0 || t.Incontri.Any(m => m.Fase != "Girone" || m.Esito == null))
            throw new InvalidOperationException("Completa tutti i gironi; le fasi finali possono essere generate una sola volta.");
        var standings = Standings(t);
        foreach (var rows in standings.Values)
        {
            var ties = rows.GroupBy(r => (r.Punti, Diff: t.RegistraPunteggio ? r.Fatti - r.Subiti : 0, Fatti: t.RegistraPunteggio ? r.Fatti : 0));
            if (ties.Any(g => g.Count() > 1 && (g.Any(r => r.Squadra.OrdineSpareggio <= 0) || g.Select(r => r.Squadra.OrdineSpareggio).Distinct().Count() != g.Count())))
                throw new InvalidOperationException("Pari merito: assegna priorita di spareggio distinte alle squadre a pari punti (e differenza punti/punti fatti, se attivi). Il valore piu basso ha precedenza.");
        }
        var seeds = Enumerable.Range(0, t.QualificatePerGirone).SelectMany(rank => standings.OrderBy(g => rank % 2 == 0 ? g.Key : -g.Key).Select(g => g.Value[rank].Squadra.Id)).ToList();
        var size = 2; while (size < seeds.Count) size *= 2;
        var order = new List<int> { 1, 2 };
        for (var n = 4; n <= size; n *= 2) order = order.SelectMany(x => new[] { x, n + 1 - x }).ToList();
        if (standings.Count > 1)
        {
            // Keep top seeds and their byes fixed; match lower seeds across groups.
            // Augmenting paths avoid the dead ends of greedy opponent swapping.
            var groups = t.Squadre.ToDictionary(s => s.Id, s => s.Girone);
            var slots = Enumerable.Range(0, size / 2).Where(i => order[2 * i + 1] <= seeds.Count).ToList();
            var opponents = slots.Select(i => order[2 * i + 1]).ToArray();
            var assigned = Enumerable.Repeat(-1, opponents.Length).ToArray();
            bool Assign(int slot, HashSet<int> visited)
            {
                foreach (var j in Enumerable.Range(0, opponents.Length).OrderBy(j => opponents[j] == order[2 * slot + 1] ? 0 : 1))
                {
                    if (groups[seeds[order[2 * slot] - 1]] == groups[seeds[opponents[j] - 1]] || !visited.Add(j)) continue;
                    if (assigned[j] >= 0 && !Assign(assigned[j], visited)) continue;
                    assigned[j] = slot;
                    return true;
                }
                return false;
            }
            foreach (var slot in slots)
                if (!Assign(slot, [])) throw new InvalidOperationException("Impossibile abbinare le qualificate di gironi diversi.");
            for (var j = 0; j < opponents.Length; j++) order[2 * assigned[j] + 1] = opponents[j];
        }
        for (var i = 0; i < size; i += 2)
        {
            int? a = order[i] <= seeds.Count ? seeds[order[i] - 1] : null;
            int? b = order[i + 1] <= seeds.Count ? seeds[order[i + 1] - 1] : null;
            if (a == null) (a, b) = (b, a);
            t.Incontri.Add(new() { Fase = size == 2 ? "Finale" : "Eliminazione", Turno = 1, Posizione = i / 2, CasaId = a, OspiteId = b, Esito = b == null ? "Casa" : null });
        }
        Advance(t);
    }

    public static void Advance(Torneo t)
    {
        var knockout = t.Incontri.Where(m => m.Fase is "Eliminazione" or "Finale").ToList();
        if (knockout.Count == 0) return;
        var round = knockout.Max(m => m.Turno);
        var matches = knockout.Where(m => m.Turno == round).OrderBy(m => m.Posizione).ToList();
        if (matches.Any(m => m.Esito != null && (m.Esito is not ("Casa" or "Ospite") || (m.Esito == "Casa" ? m.CasaId : m.OspiteId) == null)))
            throw new InvalidOperationException("Nelle finali serve una vincitrice valida.");
        if (matches.Count == 1 || matches.Any(m => m.Esito == null)) return;
        int? Winner(TorneoIncontro m) => m.Esito == "Casa" ? m.CasaId : m.OspiteId;
        for (var i = 0; i < matches.Count; i += 2)
            t.Incontri.Add(new() { Fase = matches.Count == 2 ? "Finale" : "Eliminazione", Turno = round + 1, Posizione = i / 2, CasaId = Winner(matches[i]), OspiteId = Winner(matches[i + 1]) });
        if (matches.Count == 2 && t.FinaleTerzoPosto)
            t.Incontri.Add(new() { Fase = "Terzo posto", Turno = round + 1, CasaId = matches[0].Esito == "Casa" ? matches[0].OspiteId : matches[0].CasaId, OspiteId = matches[1].Esito == "Casa" ? matches[1].OspiteId : matches[1].CasaId });
    }
}
