using System.Text.Json;
using Full_Metal_Paintball_Carmagnola.Controllers;
using Full_Metal_Paintball_Carmagnola.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;

internal static class ControllerChecks
{
    public static async Task Run(DbContextOptions<TesseramentoDbContext> options, int id)
    {
        async Task<Torneo> Read()
        {
            await using var db = new TesseramentoDbContext(options);
            return await db.Tornei.AsNoTracking().Include(t => t.Squadre).Include(t => t.Incontri).SingleAsync(t => t.Id == id);
        }
        string Snapshot(Torneo t) => JsonSerializer.Serialize(new
        {
            t.Version, t.Nome, t.PuntiVittoria, t.NumeroGironi,
            Teams = t.Squadre.OrderBy(s => s.Id).Select(s => new { s.Id, s.Nome, s.Girone, s.OrdineSpareggio }),
            Matches = t.Incontri.OrderBy(m => m.Id).Select(m => new { m.Id, m.Esito, m.PuntiCasa, m.PuntiOspite })
        });
        async Task Invoke(string name, bool accepted, Func<TorneoController, Torneo, Task<IActionResult>> action)
        {
            var before = await Read();
            await using var db = new TesseramentoDbContext(options);
            var http = new DefaultHttpContext();
            var controller = new TorneoController(db)
            {
                ControllerContext = new ControllerContext { HttpContext = http },
                TempData = new TempDataDictionary(http, new MemoryTempData())
            };
            var result = await action(controller, before);
            Checks.That(result is RedirectToActionResult, name + ": expected redirect");
            var after = await Read();
            Checks.That(controller.TempData.ContainsKey(accepted ? "Success" : "Error"), name + ": wrong feedback");
            Checks.That(accepted ? before.Version != after.Version : Snapshot(before) == Snapshot(after), name + ": persisted state/version mismatch");
        }
        await using (var db = new TesseramentoDbContext(options))
        {
            var t = await db.Tornei.SingleAsync(t => t.Id == id);
            t.RegistraPunteggio = true; await db.SaveChangesAsync();
        }
        await Invoke("group results frozen after finals", false, (c, t) => c.Risultato(t.Incontri.First(m => m.Fase == "Girone").Id, t.Version, "Casa", 3, 0));
        await Invoke("semifinal frozen after advancement", false, (c, t) => c.Risultato(t.Incontri.First(m => m.Fase == "Eliminazione").Id, t.Version, "Casa", 3, 0));
        await Invoke("knockout draw rejected", false, (c, t) => c.Risultato(t.Incontri.Single(m => m.Fase == "Finale").Id, t.Version, "Pareggio", 1, 1));
        await Invoke("outcome must agree with unequal scores", false, (c, t) => c.Risultato(t.Incontri.Single(m => m.Fase == "Finale").Id, t.Version, "Casa", 1, 2));
        await Invoke("negative score rejected", false, (c, t) => c.Risultato(t.Incontri.Single(m => m.Fase == "Finale").Id, t.Version, "Casa", -1, 2));
        await Invoke("excess score rejected", false, (c, t) => c.Risultato(t.Incontri.Single(m => m.Fase == "Finale").Id, t.Version, "Casa", 10001, 2));
        await Invoke("missing score rejected", false, (c, t) => c.Risultato(t.Incontri.Single(m => m.Fase == "Finale").Id, t.Version, "Casa", null, 2));
        await Invoke("explicit winner after tied knockout score", true, (c, t) => c.Risultato(t.Incontri.Single(m => m.Fase == "Finale").Id, t.Version, "Casa", 2, 2));
        Checks.That((await Read()).Incontri.Single(m => m.Fase == "Finale").Esito == "Casa", "Tied-score final winner not persisted");
        await Invoke("stale result rejected", false, (c, t) => c.Risultato(t.Incontri.Single(m => m.Fase == "Finale").Id, Guid.Empty, "Ospite", 1, 2));
        await Invoke("third place remains editable after final", true, (c, t) => c.Risultato(t.Incontri.Single(m => m.Fase == "Terzo posto").Id, t.Version, "Ospite", 1, 2));
        await Invoke("group reassignment frozen", false, (c, t) =>
        {
            var s = t.Squadre.First(); return c.Squadra(s.Id, t.Version, s.Nome, "", "", s.Girone == 1 ? 2 : 1, s.OrdineSpareggio);
        });
        await Invoke("qualification priorities frozen", false, (c, t) =>
        {
            var s = t.Squadre.First(); return c.Squadra(s.Id, t.Version, s.Nome, "", "", s.Girone, s.OrdineSpareggio + 1);
        });
        await Invoke("team contact/name remains editable", true, (c, t) =>
        {
            var s = t.Squadre.First(); return c.Squadra(s.Id, t.Version, "Updated team", "Contact", "+393331234567", s.Girone, s.OrdineSpareggio);
        });
        await Invoke("rules frozen after calendar", false, (c, t) => c.Modifica(new TorneoForm
        {
            Id = t.Id, Version = t.Version, Nome = t.Nome, Data = t.Data, OraInizio = t.OraInizio,
            NumeroSquadre = t.NumeroSquadre, NumeroGironi = t.NumeroGironi, AndataRitorno = t.AndataRitorno,
            FasiFinali = t.FasiFinali, QualificatePerGirone = t.QualificatePerGirone, FinaleTerzoPosto = t.FinaleTerzoPosto,
            RegistraPunteggio = t.RegistraPunteggio, PuntiVittoria = t.PuntiVittoria + 1,
            PuntiPareggio = t.PuntiPareggio, PuntiSconfitta = t.PuntiSconfitta
        }));
        Console.WriteLine("PASS 14 direct controller checks (no HTTP server; MVC filters/model binding not exercised)");
    }

    private sealed class MemoryTempData : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
