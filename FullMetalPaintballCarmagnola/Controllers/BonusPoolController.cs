using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Full_Metal_Paintball_Carmagnola.Models;
using Full_Metal_Paintball_Carmagnola.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Full_Metal_Paintball_Carmagnola.Controllers;

[Authorize(Policy = "Bonus Pool")]
[Authorize(Roles = "Admin,Staff")]
public class BonusPoolController : Controller
{
    private const string Key = "BonusPoolWeeklyState";
    private readonly TesseramentoDbContext db;
    private readonly StaffRegistryService registry;
    public BonusPoolController(TesseramentoDbContext db, StaffRegistryService registry)
    {
        this.db = db;
        this.registry = registry;
    }

    private async Task<BonusPoolState> Load() =>
        JsonSerializer.Deserialize<BonusPoolState>((await db.AppSettings.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == Key))?.Value ?? "{}")
        ?? throw new InvalidOperationException("Dati Bonus Pool non leggibili.");

    [HttpGet]
    public async Task<IActionResult> Index(DateOnly? week, Guid? edit)
    {
        var state = await Load();
        var monday = BonusPoolRules.Monday(week ?? BonusPoolRules.Today);
        var current = state.Weeks.FirstOrDefault(w => w.Monday == monday)
            ?? new BonusWeek { Monday = monday, Rates = state.Rates.Copy() };
        var form = current.Requests.FirstOrDefault(r => r.Id == edit)
            ?? new BonusRequest { Date = monday };
        return View(await ViewModel(state, current, form));
    }

    private async Task<BonusPoolViewModel> ViewModel(BonusPoolState state, BonusWeek week, BonusRequest form)
    {
        var start = DateTime.SpecifyKind(week.Monday.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        var end = start.AddDays(5);
        var calculation = week.Snapshot ?? BonusPoolRules.Calculate(week);
        var staff = User.IsInRole("Admin") ? await registry.GetStaffAsync() : new List<string>();
        staff = staff.Concat(form.Attendees).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(n => n).ToList();
        return new BonusPoolViewModel
        {
            State = state, Week = week, Form = form, Calculation = calculation, Staff = staff,
            Games = await db.Partite.AsNoTracking().Where(p => !p.IsDeleted && p.Data >= start && p.Data < end)
                .OrderBy(p => p.Data).ThenBy(p => p.OraInizio).ToListAsync(),
            CanClose = week.ClosedAtUtc == null && week.Requests.Count > 0 && calculation.Pending == 0
                && BonusPoolRules.Today >= week.Monday.AddDays(5)
        };
    }

    // A transaction-scoped database lock protects the JSON document across Render instances.
    private async Task<IActionResult> Change(long version, DateOnly week, Func<BonusPoolState, Task<string?>> update)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(826092201)");
        var state = await Load();
        if (state.Version != version)
        {
            TempData["BonusError"] = "I dati sono stati aggiornati da un altro operatore. Controlla la pagina e ripeti la modifica.";
            return RedirectToAction(nameof(Index), new { week = week.ToString("yyyy-MM-dd") });
        }
        var error = await update(state);
        if (error != null)
        {
            TempData["BonusError"] = error;
            return RedirectToAction(nameof(Index), new { week = week.ToString("yyyy-MM-dd") });
        }
        state.Version++;
        var setting = await db.AppSettings.FirstOrDefaultAsync(s => s.Key == Key);
        if (setting == null) db.AppSettings.Add(new AppSetting { Key = Key, Value = JsonSerializer.Serialize(state) });
        else setting.Value = JsonSerializer.Serialize(state);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        TempData["BonusSuccess"] = "Operazione salvata correttamente.";
        return RedirectToAction(nameof(Index), new { week = week.ToString("yyyy-MM-dd") });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Admin")]
    public async Task<IActionResult> Save(long version, [Bind(Prefix = "Form")] BonusRequest input)
    {
        var monday = BonusPoolRules.Monday(input.Date == default ? BonusPoolRules.Today : input.Date);
        if (!ModelState.IsValid)
        {
            var state = await Load();
            var current = state.Weeks.FirstOrDefault(w => w.Monday == monday)
                ?? new BonusWeek { Monday = monday, Rates = state.Rates.Copy() };
            return View("Index", await ViewModel(state, current, input));
        }
        return await Change(version, monday, async state =>
        {
            if (input.Date == default || !BonusPoolRules.IsWeekday(input.Date))
                return "Scegli una data dal lunedi al venerdi.";
            if (input.Minutes is not (60 or 90 or 120) || !Enum.IsDefined(input.Staff)
                || !Enum.IsDefined(input.Customer) || !Enum.IsDefined(input.Outcome)) return "Durata o stato non valido.";
            var oldWeek = state.Weeks.FirstOrDefault(w => w.Requests.Any(r => r.Id == input.Id));
            var target = state.Weeks.FirstOrDefault(w => w.Monday == monday);
            if (oldWeek?.ClosedAtUtc != null || target?.ClosedAtUtc != null) return "La settimana e' chiusa e non e' modificabile.";
            if (input.Id != Guid.Empty && oldWeek == null) return "Richiesta non trovata.";
            input.Contact = input.Contact.Trim();
            input.Prefix = "+" + Regex.Replace(input.Prefix, "[^0-9]", "");
            var rawPhone = input.Phone.Trim();
            input.Phone = Regex.Replace(rawPhone, "[^0-9]", "");
            if (rawPhone.Contains('+') || input.Phone.StartsWith("00"))
            {
                if (input.Phone.StartsWith("00")) input.Phone = input.Phone[2..];
                var prefixDigits = input.Prefix.TrimStart('+');
                if (prefixDigits.Length == 0 || !input.Phone.StartsWith(prefixDigits))
                    return "Il prefisso del numero incollato non coincide: correggi il campo Prefisso.";
                input.Phone = input.Phone[prefixDigits.Length..];
            }
            if (input.Contact.Length == 0 || !Regex.IsMatch(input.Prefix, @"^\+[1-9][0-9]{0,2}$")
                || input.Phone.Length < 4 || input.Phone.Length + input.Prefix.Length - 1 > 15)
                return "Controlla nome, prefisso internazionale e numero di telefono.";
            input.Attendees = input.Attendees.Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var allowedStaff = (await registry.GetStaffAsync()).Concat(oldWeek?.Requests
                .FirstOrDefault(r => r.Id == input.Id)?.Attendees ?? new List<string>()).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (input.Attendees.Any(n => !allowedStaff.Contains(n))) return "Seleziona i collaboratori dall'anagrafica staff.";
            if (input.PartitaId.HasValue)
            {
                if (state.Weeks.SelectMany(w => w.Requests).Any(r => r.Id != input.Id && r.PartitaId == input.PartitaId))
                    return "Questa partita e' gia' collegata a un'altra richiesta.";
                var game = await db.Partite.AsNoTracking().FirstOrDefaultAsync(p => p.Id == input.PartitaId);
                if (game == null || DateOnly.FromDateTime(game.Data) != input.Date
                    || game.OraInizio != input.Time.ToTimeSpan() || Math.Abs(game.Durata * 60 - input.Minutes) > 0.01)
                    return "Data, orario e durata devono corrispondere alla partita collegata.";
                if (input.Outcome == BonusGameOutcome.Played && game.IsDeleted) return "La partita collegata risulta cancellata.";
            }
            if (input.Outcome == BonusGameOutcome.Played)
            {
                if (input.Staff != BonusAnswer.Yes || input.Customer != BonusAnswer.Yes || !input.PartitaId.HasValue
                    || input.Attendees.Count == 0) return "Per una partita disputata servono disponibilita staff, conferma cliente, partita collegata e presenze effettive.";
                var now = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "Europe/Rome");
                if (input.Date.ToDateTime(input.Time).AddMinutes(input.Minutes) > now) return "La partita non e' ancora terminata.";
            }
            else input.Attendees.Clear();
            if (target == null)
            {
                target = new BonusWeek { Monday = monday, Rates = state.Rates.Copy() };
                state.Weeks.Add(target);
            }
            oldWeek?.Requests.RemoveAll(r => r.Id == input.Id);
            if (input.Id == Guid.Empty) input.Id = Guid.NewGuid();
            input.UpdatedAtUtc = DateTime.UtcNow;
            input.UpdatedBy = User.Identity?.Name ?? "Admin";
            target.Requests.Add(input);
            return null;
        });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Admin")]
    public Task<IActionResult> Close(long version, DateOnly week) => Change(version, week, async state =>
    {
        var current = state.Weeks.FirstOrDefault(w => w.Monday == BonusPoolRules.Monday(week));
        if (current == null || current.Requests.Count == 0 || current.ClosedAtUtc != null) return "Settimana vuota o gia' chiusa.";
        if (BonusPoolRules.Today < current.Monday.AddDays(5)) return "La chiusura e' disponibile dal sabato successivo.";
        var result = BonusPoolRules.Calculate(current);
        if (result.Pending > 0) return "Completa prima gli esiti di tutte le richieste.";
        foreach (var request in current.Requests.Where(BonusPoolRules.Contributes))
        {
            var game = await db.Partite.AsNoTracking().FirstOrDefaultAsync(p => p.Id == request.PartitaId);
            if (game == null || game.IsDeleted || DateOnly.FromDateTime(game.Data) != request.Date
                || game.OraInizio != request.Time.ToTimeSpan() || Math.Abs(game.Durata * 60 - request.Minutes) > 0.01
                || request.Attendees.Count == 0) return "Una partita collegata e' cambiata. Verifica date, durata e presenze prima di chiudere.";
        }
        current.Snapshot = result;
        current.ClosedAtUtc = DateTime.UtcNow;
        current.ClosedBy = User.Identity?.Name ?? "Admin";
        return null;
    });

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Admin")]
    public Task<IActionResult> SaveRates(long version, DateOnly week, string hour, string ninety, string twoHours)
        => Change(version, week, state =>
        {
            bool Parse(string? value, out decimal amount) => decimal.TryParse(value?.Trim().Replace(',', '.'),
                NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out amount)
                && amount >= 0 && amount <= 10000 && decimal.Round(amount, 2) == amount;
            if (!Parse(hour, out var h) || !Parse(ninety, out var n) || !Parse(twoHours, out var t))
                return Task.FromResult<string?>("Inserisci importi tra 0 e 10.000 euro, con massimo due decimali.");
            state.Rates = new BonusRates { Hour = h, NinetyMinutes = n, TwoHours = t };
            // A week keeps the rates captured when its first request was registered.
            return Task.FromResult<string?>(null);
        });
}
