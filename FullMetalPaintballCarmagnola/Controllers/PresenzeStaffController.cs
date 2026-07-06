using System.Globalization;
using Full_Metal_Paintball_Carmagnola.Models;
using Full_Metal_Paintball_Carmagnola.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[Authorize(Policy = "Presenze Staff")]
public class PresenzeStaffController : Controller
{
    private readonly TesseramentoDbContext _context;
    private readonly StaffRegistryService _staffRegistryService;

    public PresenzeStaffController(TesseramentoDbContext context, StaffRegistryService staffRegistryService)
    {
        _context = context;
        _staffRegistryService = staffRegistryService;
    }

    public async Task<IActionResult> Index()
    {
        var oggi = DateTime.UtcNow.Date;
        var fine = oggi.AddMonths(3);
        var staffList = await _staffRegistryService.GetStaffAsync();
        var chiusure = await LoadChiusureCampoAsync(oggi, fine);
        var chiusurePerData = BuildChiusurePerData(chiusure, oggi, fine);

        var weekendDates = Enumerable.Range(0, (fine - oggi).Days + 1)
            .Select(offset => oggi.AddDays(offset))
            .Where(date => date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
            .ToList();

        foreach (var data in weekendDates)
        {
            if (chiusurePerData.ContainsKey(data.Date))
            {
                continue;
            }

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

            await EnsurePresenzeStaffAsync(data, staffList);
        }

        await _context.SaveChangesAsync();

        var tutteDate = await _context.AssenzeCalendario
            .Where(r => r.Data >= oggi && r.Data <= fine)
            .Select(r => r.Data)
            .Distinct()
            .ToListAsync();

        tutteDate = tutteDate
            .Concat(chiusurePerData.Keys)
            .Distinct()
            .OrderBy(d => d)
            .ToList();

        foreach (var data in tutteDate)
        {
            if (chiusurePerData.ContainsKey(data.Date))
            {
                continue;
            }

            await EnsurePresenzeStaffAsync(data, staffList);
        }

        await _context.SaveChangesAsync();

        var presenze = await _context.PresenzaStaff
            .Where(p => p.Data >= oggi && p.Data <= fine)
            .ToListAsync();

        var reperibilita = await _context.AssenzeCalendario
            .Where(r => r.Data >= oggi && r.Data <= fine)
            .ToListAsync();

        ViewBag.DateList = tutteDate;
        ViewBag.PresenzaList = presenze;
        ViewBag.ReperibilitaList = reperibilita;
        ViewBag.StaffList = staffList;
        ViewBag.ChiusureCampo = chiusurePerData;

        return View();
    }

    [HttpPost]
    public async Task<IActionResult> AggiornaPresenza(string data, string nomeStaff, bool? presente)
    {
        try
        {
            if (!DateTime.TryParseExact(data, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var parsed) &&
                !DateTime.TryParseExact(data, "dd/MM/yyyy", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out parsed))
            {
                return Problem(detail: $"Formato data non valido: {data}", statusCode: 400);
            }

            var giorno = parsed.Date;
            if (await IsCampoChiusoAsync(giorno))
            {
                return BadRequest("Campo chiuso in questa data.");
            }

            var giornoStr = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(giorno.ToString("dddd"));

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

            if (presente == false)
            {
                await DissociaStaffDaPartiteAsync(giorno, nomeStaff);
            }

            await _context.SaveChangesAsync();
            return Ok();
        }
        catch (Exception ex)
        {
            return Problem(detail: ex.ToString(), statusCode: 500);
        }
    }

    [HttpPost]
    public async Task<IActionResult> AggiornaReperibilita(int id, string reperibile)
    {
        var record = await _context.AssenzeCalendario.FindAsync(id);
        if (record != null)
        {
            if (await IsCampoChiusoAsync(record.Data.Date))
            {
                return BadRequest("Campo chiuso in questa data.");
            }

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

            if (await IsCampoChiusoAsync(giorno))
            {
                return BadRequest("Campo chiuso in questa data.");
            }

            if (!await _context.AssenzeCalendario.AnyAsync(r => r.Data == giorno))
            {
                _context.AssenzeCalendario.Add(new AssenzaCalendario
                {
                    Data = giorno,
                    Giorno = giornoStr,
                    Reperibile = "In attesa"
                });
            }

            var staffList = await _staffRegistryService.GetStaffAsync();
            await EnsurePresenzeStaffAsync(giorno, staffList);

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

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AggiungiAnagrafica(string nome)
    {
        if (string.IsNullOrWhiteSpace(nome))
        {
            TempData["StaffRegistryError"] = "Inserisci un nome valido.";
            return RedirectToAction(nameof(Index));
        }

        var added = await _staffRegistryService.AddStaffAsync(nome);
        TempData[added ? "StaffRegistryMessage" : "StaffRegistryError"] = added
            ? $"Anagrafica {nome.Trim()} aggiunta."
            : "Nome gia' presente o non valido.";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RimuoviAnagrafica(string nome)
    {
        var removed = await _staffRegistryService.RemoveStaffAsync(nome);
        TempData[removed ? "StaffRegistryMessage" : "StaffRegistryError"] = removed
            ? $"Anagrafica {nome} rimossa dalla lista attiva."
            : "Anagrafica non trovata.";

        return RedirectToAction(nameof(Index));
    }

    private async Task EnsurePresenzeStaffAsync(DateTime data, IEnumerable<string> staffList)
    {
        var giorno = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(data.ToString("dddd"));

        foreach (var nome in staffList)
        {
            if (!await _context.PresenzaStaff.AnyAsync(p => p.Data == data && p.NomeStaff == nome))
            {
                _context.PresenzaStaff.Add(new PresenzaStaff
                {
                    Data = data,
                    Giorno = giorno,
                    NomeStaff = nome,
                    Presente = null
                });
            }
        }
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

    private async Task DissociaStaffDaPartiteAsync(DateTime giorno, string nomeStaff)
    {
        var partite = await _context.Partite
            .Where(p => !p.IsDeleted && p.Data.Date == giorno.Date &&
                (p.Staff1 == nomeStaff || p.Staff2 == nomeStaff || p.Staff3 == nomeStaff || p.Staff4 == nomeStaff))
            .ToListAsync();

        foreach (var partita in partite)
        {
            if (partita.Staff1 == nomeStaff) partita.Staff1 = null;
            if (partita.Staff2 == nomeStaff) partita.Staff2 = null;
            if (partita.Staff3 == nomeStaff) partita.Staff3 = null;
            if (partita.Staff4 == nomeStaff) partita.Staff4 = null;
        }
    }
}
