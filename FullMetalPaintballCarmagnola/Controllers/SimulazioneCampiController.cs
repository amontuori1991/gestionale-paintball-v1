using Full_Metal_Paintball_Carmagnola.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Full_Metal_Paintball_Carmagnola.Controllers;

// Real bookings are read only; simulated assignments live exclusively in the browser.
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class SimulazioneCampiController : Controller
{
    [HttpGet, AllowAnonymous]
    public IActionResult Index() => View();

    [HttpGet, AllowAnonymous]
    public IActionResult Orari(DateOnly date)
    {
        if (date.Year is < 2020 or > 2100) return BadRequest();
        var day = DisponibilitaCampoController.BuildGiorno(date.ToDateTime(TimeOnly.MinValue),
            new List<Partita>(), new List<CampoChiusura>());
        var now = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "Europe/Rome");
        return Json(new { opening = day.Apertura.TotalMinutes, closing = day.UltimaFinePartita.TotalMinutes,
            sunset = day.Tramonto.ToString(@"hh\:mm"), today = now.ToString("yyyy-MM-dd"),
            nowMinutes = now.TimeOfDay.TotalMinutes });
    }

    [HttpGet, Authorize(Roles = "Admin")]
    public async Task<IActionResult> Prenotazioni(DateOnly date, [FromServices] TesseramentoDbContext db)
    {
        if (date.Year is < 2020 or > 2100) return BadRequest();
        var from = DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        var to = from.AddDays(1);
        var rows = await db.Partite.AsNoTracking().Where(p => !p.IsDeleted && p.Data >= from && p.Data < to)
            .OrderBy(p => p.OraInizio).ThenBy(p => p.Id)
            .Select(p => new { p.Id, p.Tipo, p.NumeroPartecipanti, p.OraInizio, p.Durata, p.CaparraConfermata }).ToListAsync();
        var closures = await db.CampoChiusure.AsNoTracking().Where(c => c.DataInizio < to && c.DataFine >= from)
            .Select(c => new { c.OraInizio, c.OraFine }).ToListAsync();
        return Json(new {
            bookings = rows.Select(p => new { id = p.Id, type = p.Tipo, people = p.NumeroPartecipanti,
                start = p.OraInizio.TotalMinutes, end = p.OraInizio.TotalMinutes + p.Durata * 60,
                paid = p.CaparraConfermata }),
            closures = closures.Select(c => new { start = c.OraInizio?.TotalMinutes ?? 0, end = c.OraFine?.TotalMinutes ?? 1440 })
        });
    }
}
