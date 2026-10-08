using Full_Metal_Paintball_Carmagnola.Models;
using Full_Metal_Paintball_Carmagnola.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Full_Metal_Paintball_Carmagnola.Controllers;

[Authorize(Roles = "Admin,Staff")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class TorneoController(TesseramentoDbContext db) : Controller
{
    public async Task<IActionResult> Index() => View(await db.Tornei.AsNoTracking().OrderByDescending(t => t.Data).ToListAsync());
    public IActionResult Crea() => View(new TorneoForm());
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Crea(TorneoForm model)
    {
        if (!ModelState.IsValid) return View(model);
        var t = new Torneo(); Apply(t, model);
        for (var i = 0; i < t.NumeroSquadre; i++) t.Squadre.Add(new() { Nome = $"Squadra {i + 1}", Girone = i % t.NumeroGironi + 1 });
        db.Tornei.Add(t); await db.SaveChangesAsync();
        TempData["Success"] = "Torneo creato. Completa i referenti e genera il calendario. Ricorda la chiusura campo.";
        return RedirectToAction(nameof(Dettaglio), new { id = t.Id });
    }

    public async Task<IActionResult> Dettaglio(int id)
    {
        var t = await Load(id); if (t == null) return NotFound();
        ViewBag.RegistrationCounts = await db.TorneoIscrizioni.Where(i => i.TorneoSquadra.TorneoId == id).GroupBy(i => i.TorneoSquadraId).ToDictionaryAsync(g => g.Key, g => g.Count());
        ViewBag.Iscrizioni = await db.TorneoIscrizioni.AsNoTracking().Include(i => i.Tesseramento).Where(i => i.TorneoSquadra.TorneoId == id).ToListAsync();
        return View(new TorneoDetail { Torneo = t, Classifiche = TorneoEngine.Standings(t) });
    }

    public async Task<IActionResult> Modifica(int id)
    {
        var t = await Load(id); if (t == null) return NotFound();
        ViewBag.Locked = t.Incontri.Count > 0;
        return View(Form(t));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Modifica(TorneoForm model)
    {
        if (!ModelState.IsValid) { ViewBag.Locked = await db.TorneoIncontri.AnyAsync(m => m.TorneoId == model.Id); return View(model); }
        return await Change(model.Id, model.Version, t =>
        {
            if (model.Data.Year != t.Data.Year && db.TorneoIscrizioni.Any(i => i.TorneoSquadra.TorneoId == t.Id))
                throw new InvalidOperationException("Sono gia presenti tesseramenti: non puoi spostare l'evento in un altro anno di validita.");
            if (model.NumeroSquadre != t.NumeroSquadre) throw new InvalidOperationException("Il numero di squadre non puo cambiare dopo la creazione: i link sono gia assegnati.");
            if (t.Incontri.Count > 0 && (model.NumeroGironi != t.NumeroGironi || model.AndataRitorno != t.AndataRitorno || model.FasiFinali != t.FasiFinali || model.QualificatePerGirone != t.QualificatePerGirone || model.FinaleTerzoPosto != t.FinaleTerzoPosto || model.RegistraPunteggio != t.RegistraPunteggio || model.PuntiVittoria != t.PuntiVittoria || model.PuntiPareggio != t.PuntiPareggio || model.PuntiSconfitta != t.PuntiSconfitta))
                throw new InvalidOperationException("Le regole sono bloccate dopo la generazione del calendario.");
            if (t.NumeroGironi != model.NumeroGironi)
                foreach (var (s, i) in t.Squadre.OrderBy(s => s.Id).Select((s, i) => (s, i))) s.Girone = i % model.NumeroGironi + 1;
            Apply(t, model);
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Squadra(int id, Guid version, string nome, string? referente, string? telefono, int girone, int ordineSpareggio)
    {
        if (!ModelState.IsValid) return BadRequest("Dati squadra non validi.");
        var torneoId = await db.TorneoSquadre.Where(s => s.Id == id).Select(s => (int?)s.TorneoId).FirstOrDefaultAsync();
        if (torneoId == null) return NotFound();
        return await Change(torneoId.Value, version, t =>
        {
            var s = t.Squadre.Single(s => s.Id == id);
            if (string.IsNullOrWhiteSpace(nome) || nome.Trim().Length > 100 || (referente?.Length ?? 0) > 100 || (telefono?.Length ?? 0) > 30 || girone < 1 || girone > t.NumeroGironi || ordineSpareggio < 0 || ordineSpareggio > 64)
                throw new InvalidOperationException("Controlla nome, referente, telefono, girone e priorita (0-64).");
            if (t.Incontri.Count > 0 && girone != s.Girone) throw new InvalidOperationException("Non puoi spostare una squadra dopo la generazione degli incontri.");
            if (t.Incontri.Any(m => m.Fase != "Girone") && ordineSpareggio != s.OrdineSpareggio) throw new InvalidOperationException("Le qualificazioni sono gia state definite.");
            s.Nome = nome.Trim(); s.Referente = referente?.Trim() ?? ""; s.Telefono = telefono?.Trim() ?? ""; s.Girone = girone; s.OrdineSpareggio = ordineSpareggio;
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> GeneraGironi(int id, Guid version) => Change(id, version, TorneoEngine.GenerateGroups);
    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> GeneraFinali(int id, Guid version) => Change(id, version, TorneoEngine.GenerateFinals);

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Risultato(int id, Guid version, string esito, int? puntiCasa, int? puntiOspite)
    {
        if (!ModelState.IsValid) return BadRequest("Risultato non valido.");
        var torneoId = await db.TorneoIncontri.Where(m => m.Id == id).Select(m => (int?)m.TorneoId).FirstOrDefaultAsync();
        if (torneoId == null) return NotFound();
        return await Change(torneoId.Value, version, t =>
        {
            var m = t.Incontri.Single(m => m.Id == id);
            if (m.CasaId == null || m.OspiteId == null) throw new InvalidOperationException("Passaggio automatico: nessun risultato da inserire.");
            if (m.Fase == "Girone" ? t.Incontri.Any(x => x.Fase != "Girone") : t.Incontri.Any(x => x.Fase != "Girone" && x.Turno > m.Turno))
                throw new InvalidOperationException("Il turno successivo e gia stato generato: il risultato non puo essere modificato.");
            if (esito is not ("Casa" or "Ospite" or "Pareggio") || (m.Fase != "Girone" && esito == "Pareggio")) throw new InvalidOperationException("Seleziona un esito valido; nelle finali serve una vincitrice.");
            if (t.RegistraPunteggio)
            {
                if (puntiCasa is null or < 0 or > 10000 || puntiOspite is null or < 0 or > 10000) throw new InvalidOperationException("Inserisci entrambi i punteggi (0-10000).");
                var scoreOutcome = puntiCasa == puntiOspite ? "Pareggio" : puntiCasa > puntiOspite ? "Casa" : "Ospite";
                if (esito != scoreOutcome && !(m.Fase != "Girone" && scoreOutcome == "Pareggio")) throw new InvalidOperationException("Esito e punteggi non corrispondono.");
            }
            m.Esito = esito; m.PuntiCasa = t.RegistraPunteggio ? puntiCasa : null; m.PuntiOspite = t.RegistraPunteggio ? puntiOspite : null;
            TorneoEngine.Advance(t);
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Elimina(int id, Guid version, [FromServices] TournamentRegistrationService registrations)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var t = (await db.Tornei.FromSqlInterpolated($"SELECT * FROM \"Tornei\" WHERE \"Id\" = {id} FOR UPDATE").ToListAsync()).SingleOrDefault();
        if (t == null) return NotFound();
        if (t.Version != version) { TempData["Error"] = "Il torneo e cambiato. Riapri la scheda prima di eliminarlo."; return RedirectToAction(nameof(Dettaglio), new { id }); }
        var signatures = await registrations.GetSignatureCandidatesAsync(id);
        var removed = await registrations.DeleteRegistrationsAsync(id);
        await db.TorneoIncontri.Where(m => m.TorneoId == id).ExecuteDeleteAsync();
        db.Tornei.Remove(t); await db.SaveChangesAsync(); await tx.CommitAsync();
        var webRoot = HttpContext.RequestServices.GetService<IWebHostEnvironment>()?.WebRootPath;
        if (!string.IsNullOrWhiteSpace(webRoot))
        {
            try { await registrations.CleanupSignaturesAsync(signatures, webRoot); }
            catch (Exception ex) { HttpContext.RequestServices.GetService<ILogger<TorneoController>>()?.LogWarning(ex, "Tournament removed; signature cleanup needs attention for tournament {Id}.", id); }
        }
        TempData["Success"] = $"Evento eliminato. Rimossi {removed} nuovi tesseramenti esclusivi non esportati; conservati quelli gia validi o utilizzati altrove. Verifica separatamente la chiusura campo.";
        return RedirectToAction(nameof(Index));
    }

    private Task<Torneo?> Load(int id) => db.Tornei.Include(t => t.Squadre).Include(t => t.Incontri).AsSplitQuery().FirstOrDefaultAsync(t => t.Id == id);

    private async Task<IActionResult> Change(int id, Guid version, Action<Torneo> action)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var locked = await db.Tornei.FromSqlInterpolated($"SELECT * FROM \"Tornei\" WHERE \"Id\" = {id} FOR UPDATE").ToListAsync();
        if (locked.Count == 0) return NotFound();
        var t = await Load(id);
        if (t!.Version != version) { TempData["Error"] = "Un altro operatore ha aggiornato il torneo. Controlla i dati e riprova."; return RedirectToAction(nameof(Dettaglio), new { id }); }
        try
        {
            action(t); t.Version = Guid.NewGuid(); await db.SaveChangesAsync(); await tx.CommitAsync();
            TempData["Success"] = "Torneo aggiornato.";
        }
        catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Dettaglio), new { id });
    }

    private static void Apply(Torneo t, TorneoForm m)
    {
        t.Nome = m.Nome.Trim(); t.Note = m.Note?.Trim(); t.Data = DateTime.SpecifyKind(m.Data.Date, DateTimeKind.Utc); t.OraInizio = m.OraInizio;
        t.NumeroSquadre = m.NumeroSquadre; t.NumeroGironi = m.NumeroGironi; t.AndataRitorno = m.AndataRitorno;
        t.FasiFinali = m.FasiFinali; t.QualificatePerGirone = m.QualificatePerGirone; t.FinaleTerzoPosto = m.FinaleTerzoPosto;
        t.RegistraPunteggio = m.RegistraPunteggio; t.PuntiVittoria = m.PuntiVittoria; t.PuntiPareggio = m.PuntiPareggio; t.PuntiSconfitta = m.PuntiSconfitta;
    }

    private static TorneoForm Form(Torneo t) => new()
    {
        Id = t.Id, Version = t.Version, Nome = t.Nome, Note = t.Note, Data = t.Data, OraInizio = t.OraInizio,
        NumeroSquadre = t.NumeroSquadre, NumeroGironi = t.NumeroGironi, AndataRitorno = t.AndataRitorno,
        FasiFinali = t.FasiFinali, QualificatePerGirone = t.QualificatePerGirone, FinaleTerzoPosto = t.FinaleTerzoPosto,
        RegistraPunteggio = t.RegistraPunteggio, PuntiVittoria = t.PuntiVittoria, PuntiPareggio = t.PuntiPareggio, PuntiSconfitta = t.PuntiSconfitta
    };
}
