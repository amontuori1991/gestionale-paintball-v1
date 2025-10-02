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
        var oggi = DateTime.UtcNow.Date;           // solo data
        var fine = oggi.AddMonths(3);

        var weekendDates = Enumerable.Range(0, (fine - oggi).Days + 1)
            .Select(offset => oggi.AddDays(offset))
            .Where(date => date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
            .ToList();

        foreach (var data in weekendDates)
        {
            // Evita problemi di Kind/Equals: controlla a DB
            var esisteAssenza = await _context.AssenzeCalendario.AnyAsync(r => r.Data == data);
            if (!esisteAssenza)
            {
                _context.AssenzeCalendario.Add(new AssenzaCalendario
                {
                    Data = data,
                    Giorno = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(data.ToString("dddd")),
                    Reperibile = "In attesa"
                });
            }

            // Per ogni membro, crea la riga se manca (idempotente)
            foreach (var nome in new[] { "Simone", "Davide", "Andrea", "Federico", "Enrico" })
            {
                var esistePresenza = await _context.PresenzaStaff.AnyAsync(p => p.Data == data && p.NomeStaff == nome);
                if (!esistePresenza)
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

        // letture normalizzate a sola data
        var tutteDate = await _context.AssenzeCalendario
            .Where(r => r.Data >= oggi && r.Data <= fine)
            .Select(r => r.Data)
            .Distinct()
            .OrderBy(d => d)
            .ToListAsync();

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
    public async Task<IActionResult> AggiornaPresenza(string data, string nomeStaff, bool? presente)
    {
        try
        {
            // Parsiamo noi la data in formato dalla view ("yyyy-MM-dd")
            if (!DateTime.TryParseExact(data, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                                        DateTimeStyles.None, out var parsed))
            {
                // fallback: accetta anche "dd/MM/yyyy" se mai arrivasse così
                if (!DateTime.TryParseExact(data, "dd/MM/yyyy", CultureInfo.InvariantCulture,
                                            DateTimeStyles.None, out parsed))
                {
                    return Problem(detail: $"Formato data non valido: {data}", statusCode: 400);
                }
            }

            var giorno = parsed.Date;
            var giornoStr = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(giorno.ToString("dddd"));

            // Upsert manuale
            var row = await _context.PresenzaStaff
                .FirstOrDefaultAsync(p => p.Data == giorno && p.NomeStaff == nomeStaff);

            if (row == null)
            {
                _context.PresenzaStaff.Add(new PresenzaStaff
                {
                    Data = giorno,
                    Giorno = giornoStr,
                    NomeStaff = nomeStaff,
                    Presente = presente
                });
            }
            else
            {
                row.Presente = presente;
            }

            await _context.SaveChangesAsync();
            return Ok();
        }
        catch (Exception ex)
        {
            // Così in Network→Response vediamo l'errore vero
            return Problem(detail: ex.ToString(), statusCode: 500);
        }
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
            var giorno = dataParsed.Date;
            var giornoStr = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(giorno.ToString("dddd"));

            if (!await _context.AssenzeCalendario.AnyAsync(r => r.Data == giorno))
            {
                _context.AssenzeCalendario.Add(new AssenzaCalendario
                {
                    Data = giorno,
                    Giorno = giornoStr,
                    Reperibile = "In attesa"
                });
            }

            foreach (var nome in new[] { "Simone", "Davide", "Andrea", "Federico", "Enrico" })
            {
                if (!await _context.PresenzaStaff.AnyAsync(p => p.Data == giorno && p.NomeStaff == nome))
                {
                    _context.PresenzaStaff.Add(new PresenzaStaff
                    {
                        Data = giorno,
                        Giorno = giornoStr,
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
            dataParsed = DateTime.SpecifyKind(dataParsed, DateTimeKind.Utc);

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
