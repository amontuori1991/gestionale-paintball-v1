using System.Globalization;
using Full_Metal_Paintball_Carmagnola.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[Authorize(Policy = "Calendario Assenze")]
public class CalendarioAssenzeController : Controller
{
    private readonly TesseramentoDbContext _context;

    public CalendarioAssenzeController(TesseramentoDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var oggi = DateTime.SpecifyKind(DateTime.Today, DateTimeKind.Utc);
        var fine = oggi.AddMonths(3);

        var weekendDates = Enumerable.Range(0, (fine - oggi).Days + 1)
            .Select(offset => oggi.AddDays(offset))
            .Where(date => date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
            .Select(date => DateTime.SpecifyKind(date, DateTimeKind.Utc)) // Fondamentale su ogni data generata
            .ToList();
    


        foreach (var data in weekendDates)
        {
            if (!_context.AssenzeCalendario.Any(a => a.Data.Date == data.Date)) // CORRETTO: .Date
            {
                _context.AssenzeCalendario.Add(new AssenzaCalendario
                {
                    Data = data,
                    Giorno = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(data.ToString("dddd")),
                    Reperibile = "In attesa"
                });
            }
        }

        await _context.SaveChangesAsync();

        var assenze = await _context.AssenzeCalendario
            .Where(a => a.Data >= oggi && a.Data <= fine)
            .OrderBy(a => a.Data)
            .ToListAsync();

        return View(assenze);
    }

    [HttpPost]
    public async Task<IActionResult> AggiornaReperibile(int id, string reperibile)
    {
        var record = await _context.AssenzeCalendario.FindAsync(id);
        if (record == null)
            return BadRequest("Record non trovato");

        record.Reperibile = reperibile;
        await _context.SaveChangesAsync();
        return Ok();
    }

    [HttpPost]
    public async Task<IActionResult> AggiornaAssenza(int id, string nome, string stato)
    {
        var record = await _context.AssenzeCalendario.FindAsync(id);
        if (record == null)
            return BadRequest("Record non trovato");

        switch (nome)
        {
            case "Montuo": record.Montuo = stato; break;
            case "Flavio": record.Flavio = stato; break;
            case "Bosax": record.Bosax = stato; break;
        }

        await _context.SaveChangesAsync();
        return Ok();
    }
}
