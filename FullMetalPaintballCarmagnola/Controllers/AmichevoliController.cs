using Full_Metal_Paintball_Carmagnola.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Full_Metal_Paintball_Carmagnola.Controllers;

[Authorize(Policy = "Statistiche")]
public sealed class AmichevoliController(TesseramentoDbContext db) : Controller
{
    public async Task<IActionResult> Index(Guid? modifica)
    {
        var form = new AmichevoleInput();
        if (modifica.HasValue && User.IsInRole("Admin"))
        {
            var row = await db.PartiteAmichevoli.FindAsync(modifica.Value);
            if (row == null || row.IsDeleted) return NotFound();
            form = new() { Id = row.Id, Data = row.Data, Tipo = row.Tipo, ColpiIllimitati = row.ColpiIllimitati, Note = row.Note };
        }
        return View(await Registro(form));
    }

    [HttpPost, Authorize(Roles = "Admin"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Salva([Bind(Prefix = "Form")] AmichevoleInput form)
    {
        if (form.Id == Guid.Empty) ModelState.AddModelError("Form.Id", "Identificativo non valido.");
        if (form.Data.HasValue && form.Data.Value.Year < 2000)
            ModelState.AddModelError("Form.Data", "Inserisci una data dal 2000 in avanti.");
        if (!ModelState.IsValid) return View("Index", await Registro(form));
        var row = await db.PartiteAmichevoli.FindAsync(form.Id);
        if (row?.IsDeleted == true) return Conflict("Questa partita e' stata rimossa.");
        if (row == null)
        {
            row = new() { Id = form.Id, CreatedAtUtc = DateTime.UtcNow };
            db.PartiteAmichevoli.Add(row);
        }
        row.Data = DateTime.SpecifyKind(form.Data!.Value.Date, DateTimeKind.Utc);
        row.Tipo = form.Tipo;
        row.ColpiIllimitati = form.ColpiIllimitati;
        row.Note = form.Note?.Trim();
        row.UpdatedAtUtc = DateTime.UtcNow;
        row.UpdatedBy = User.Identity?.Name;
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            return Conflict("Invio gia' ricevuto. Ricarica lo storico prima di riprovare.");
        }
        TempData["AmichevoliMessage"] = "Partita amichevole salvata. Statistiche aggiornate.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, Authorize(Roles = "Admin"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Rimuovi(Guid id)
    {
        var row = await db.PartiteAmichevoli.FindAsync(id);
        if (row == null) return NotFound();
        row.IsDeleted = true;
        row.UpdatedAtUtc = DateTime.UtcNow;
        row.UpdatedBy = User.Identity?.Name;
        await db.SaveChangesAsync();
        TempData["AmichevoliMessage"] = "Partita esclusa dal conteggio e conservata nello storico.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<RegistroAmichevoli> Registro(AmichevoleInput form) => new()
    {
        Form = form,
        Partite = await db.PartiteAmichevoli.AsNoTracking().OrderByDescending(p => p.Data).ThenByDescending(p => p.CreatedAtUtc).ToListAsync()
    };
}
