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
        var chiusure = await LoadChiusureCampoAsync(oggi, fine);
        var chiusurePerData = BuildChiusurePerData(chiusure, oggi, fine);

        var weekendDates = Enumerable.Range(0, (fine - oggi).Days + 1)
            .Select(offset => oggi.AddDays(offset))
            .Where(date => date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
            .Select(date => DateTime.SpecifyKind(date, DateTimeKind.Utc)) // Fondamentale su ogni data generata
            .ToList();
    


        foreach (var data in weekendDates)
        {
            if (chiusurePerData.ContainsKey(data.Date))
            {
                continue;
            }

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

        var dateEsistenti = assenze.Select(a => a.Data.Date).ToHashSet();
        foreach (var data in chiusurePerData.Keys.Where(d => !dateEsistenti.Contains(d.Date)))
        {
            assenze.Add(new AssenzaCalendario
            {
                Data = data,
                Giorno = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(data.ToString("dddd")),
                Reperibile = "Campo chiuso"
            });
        }

        assenze = assenze
            .OrderBy(a => a.Data)
            .ToList();

        ViewBag.ChiusureCampo = chiusurePerData;

        return View(assenze);
    }

    [HttpPost]
    public async Task<IActionResult> AggiornaReperibile(int id, string reperibile)
    {
        var record = await _context.AssenzeCalendario.FindAsync(id);
        if (record == null)
            return BadRequest("Record non trovato");

        if (await IsCampoChiusoAsync(record.Data.Date))
            return BadRequest("Campo chiuso in questa data.");

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

        if (await IsCampoChiusoAsync(record.Data.Date))
            return BadRequest("Campo chiuso in questa data.");

        switch (nome)
        {
            case "Montuo": record.Montuo = stato; break;
            case "Flavio": record.Flavio = stato; break;
            case "Bosax": record.Bosax = stato; break;
        }

        await _context.SaveChangesAsync();
        return Ok();
    }

    private async Task<List<CampoChiusura>> LoadChiusureCampoAsync(DateTime inizio, DateTime fine)
    {
        return await _context.CampoChiusure
            .Where(c => c.DataInizio <= fine && c.DataFine >= inizio)
            .ToListAsync();
    }

    private static Dictionary<DateTime, CampoChiusura> BuildChiusurePerData(
        IEnumerable<CampoChiusura> chiusure,
        DateTime inizio,
        DateTime fine)
    {
        var result = new Dictionary<DateTime, CampoChiusura>();

        foreach (var chiusura in chiusure)
        {
            var start = chiusura.DataInizio.Date < inizio.Date ? inizio.Date : chiusura.DataInizio.Date;
            var end = chiusura.DataFine.Date > fine.Date ? fine.Date : chiusura.DataFine.Date;

            for (var data = start; data <= end; data = data.AddDays(1))
            {
                result[data] = chiusura;
            }
        }

        return result;
    }

    private async Task<bool> IsCampoChiusoAsync(DateTime data)
    {
        data = data.Date;

        return await _context.CampoChiusure
            .AnyAsync(c => c.DataInizio <= data && c.DataFine >= data);
    }
}
