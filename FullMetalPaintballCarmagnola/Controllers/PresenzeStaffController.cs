using System.Globalization;
using Full_Metal_Paintball_Carmagnola.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
[Authorize(Policy = "Presenze Staff")]
public class PresenzeStaffController : Controller
{
    private readonly TesseramentoDbContext _context;

    public PresenzeStaffController(TesseramentoDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var oggi = DateTime.Today;
        var fine = oggi.AddMonths(3);

        var dateSalvate = _context.AssenzeCalendario.Select(r => r.Data).ToList();
        var weekendDates = Enumerable.Range(0, (fine - oggi).Days + 1)
            .Select(offset => oggi.AddDays(offset))
            .Where(date => date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
            .ToList();

        foreach (var data in weekendDates)
        {
            if (!dateSalvate.Contains(data))
            {
                _context.AssenzeCalendario.Add(new AssenzaCalendario
                {
                    Data = data,
                    Giorno = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(data.ToString("dddd")),
                    Reperibile = "In attesa"
                });

                foreach (var nome in new[] { "Simone", "Davide", "Andrea", "Federico", "Enrico" })
                {
                    _context.PresenzaStaff.Add(new PresenzaStaff
                    {
                        Data = data,
                        Giorno = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(data.ToString("dddd")),
                        NomeStaff = nome,
                        Presente = null
                    });
                }
            }
        }

        await _context.SaveChangesAsync();

        var tutteDate = _context.AssenzeCalendario
            .Where(r => r.Data >= oggi && r.Data <= fine)
            .Select(r => r.Data)
            .Distinct()
            .OrderBy(d => d)
            .ToList();

        var presenze = await _context.PresenzaStaff
            .Where(p => p.Data >= oggi && p.Data <= fine)
            .ToListAsync();

        var reperibilita = await _context.AssenzeCalendario
            .Where(r => r.Data >= oggi && r.Data <= fine)
            .ToListAsync();

        ViewBag.DateList = tutteDate;
        ViewBag.PresenzaList = presenze;
        ViewBag.ReperibilitaList = reperibilita;

        return View();
    }

    [HttpPost]
    public async Task<IActionResult> AggiornaPresenza(DateTime data, string nomeStaff, bool? presente)
    {
        var presenza = await _context.PresenzaStaff.FirstOrDefaultAsync(p => p.Data == data && p.NomeStaff == nomeStaff);
        if (presenza != null)
        {
            presenza.Presente = presente;
            await _context.SaveChangesAsync();
        }
        return Ok();
    }

    [HttpPost]
    public async Task<IActionResult> AggiornaReperibilita(int id, string reperibile)
    {
        var record = await _context.AssenzeCalendario.FindAsync(id);
        if (record != null)
        {
            record.Reperibile = string.IsNullOrWhiteSpace(reperibile) ? "In attesa" : reperibile;
            await _context.SaveChangesAsync();
        }
        return Ok();
    }

    [HttpPost]
    public async Task<IActionResult> InserisciDataInfrasettimanale(string data)
    {
        if (DateTime.TryParseExact(data, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dataParsed))
        {
            var giorno = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(dataParsed.ToString("dddd"));

            if (!_context.AssenzeCalendario.Any(r => r.Data == dataParsed))
            {
                _context.AssenzeCalendario.Add(new AssenzaCalendario
                {
                    Data = dataParsed,
                    Giorno = giorno,
                    Reperibile = "In attesa"
                });
            }

            foreach (var nome in new[] { "Simone", "Davide", "Andrea", "Federico", "Enrico" })
            {
                if (!_context.PresenzaStaff.Any(p => p.Data == dataParsed && p.NomeStaff == nome))
                {
                    _context.PresenzaStaff.Add(new PresenzaStaff
                    {
                        Data = dataParsed,
                        Giorno = giorno,
                        NomeStaff = nome,
                        Presente = null
                    });
                }
            }

            await _context.SaveChangesAsync();
        }

        return Ok();
    }

    [HttpPost]
    public async Task<IActionResult> EliminaDataInfrasettimanale(string data)
    {
        if (DateTime.TryParseExact(data, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dataParsed))
        {
            // Solo se non è weekend
            if (dataParsed.DayOfWeek != DayOfWeek.Saturday && dataParsed.DayOfWeek != DayOfWeek.Sunday)
            {
                var presenze = _context.PresenzaStaff.Where(p => p.Data == dataParsed);
                var reper = _context.AssenzeCalendario.Where(r => r.Data == dataParsed);

                _context.PresenzaStaff.RemoveRange(presenze);
                _context.AssenzeCalendario.RemoveRange(reper);

                await _context.SaveChangesAsync();
            }
        }

        return Ok();
    }
}
