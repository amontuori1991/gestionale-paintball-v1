using Full_Metal_Paintball_Carmagnola.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Full_Metal_Paintball_Carmagnola.Controllers;

// Deliberately has no database dependency: all scenarios live only in the browser.
[AllowAnonymous]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class SimulazioneCampiController : Controller
{
    [HttpGet]
    public IActionResult Index() => View();

    [HttpGet]
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
}
