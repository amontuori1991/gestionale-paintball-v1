using System.Security.Claims;
using System.Text.Json;
using Full_Metal_Paintball_Carmagnola.Controllers;
using Full_Metal_Paintball_Carmagnola.Models;
using Full_Metal_Paintball_Carmagnola.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;

internal static class DatabaseChecks
{
    // Dedicated disposable local PostgreSQL only. Never read application credentials.
    private static readonly DbContextOptions<TesseramentoDbContext> Options = new DbContextOptionsBuilder<TesseramentoDbContext>()
        .UseNpgsql("Host=127.0.0.1;Port=55439;Database=bonus_pool_checks;Username=bonus_tests;SSL Mode=Disable").Options;
    public static async Task Run()
    {
        await using var seed = new TesseramentoDbContext(Options);
        await seed.Database.EnsureCreatedAsync();
        if (await seed.AppSettings.AnyAsync(s => s.Key == "BonusPoolWeeklyState"))
            throw new Exception("Use a fresh bonus_pool_checks test database.");
        var monday = BonusPoolRules.Monday(BonusPoolRules.Today).AddDays(-7);
        seed.AppSettings.Add(new AppSetting { Key = "StaffMembers", Value = "[\"Simone\",\"Davide\"]" });
        var game = new Partita
        {
            Data = DateTime.SpecifyKind(monday.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc),
            OraInizio = new TimeSpan(10, 0, 0), Durata = 1, Tipo = "Adulti", Staff1 = "Simone", Staff2 = "Davide"
        };
        seed.Partite.Add(game);
        await seed.SaveChangesAsync();

        BonusRequest Request() => new()
        {
            Contact = "Test", Prefix = "+39", Phone = "+39 333 123 4567", Date = monday,
            Time = new TimeOnly(10, 0), Minutes = 60, Staff = BonusAnswer.Yes, Customer = BonusAnswer.Yes,
            Outcome = BonusGameOutcome.Played, Attendees = new() { "Simone", "Davide" }, PartitaId = game.Id
        };
        async Task<BonusPoolState> State()
        {
            await using var db = new TesseramentoDbContext(Options);
            return JsonSerializer.Deserialize<BonusPoolState>((await db.AppSettings.AsNoTracking().SingleAsync(s => s.Key == "BonusPoolWeeklyState")).Value!)!;
        }
        async Task Invoke(Func<BonusPoolController, Task<IActionResult>> action)
        {
            await using var db = new TesseramentoDbContext(Options);
            var http = new DefaultHttpContext();
            http.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "Test Admin"), new Claim(ClaimTypes.Role, "Admin") }, "Test"));
            var controller = new BonusPoolController(db, new StaffRegistryService(db))
            {
                ControllerContext = new ControllerContext { HttpContext = http },
                TempData = new TempDataDictionary(http, new TestTempData())
            };
            await action(controller);
        }
        void Check(bool value, string message) { if (!value) throw new Exception(message); }
        await Invoke(c => c.Save(0, Request()));
        var state = await State();
        Check(state.Version == 1 && state.Weeks.Single().Requests.Single().Phone == "3331234567", "Save / phone normalization");
        await Invoke(c => c.Save(1, Request()));
        Check((await State()).Version == 1, "Duplicate linked game must be rejected");
        await Invoke(c => c.SaveRates(0, monday, "90", "100", "110"));
        Check((await State()).Rates.Hour == 20, "Stale form cannot overwrite state");
        await Invoke(c => c.SaveRates(1, monday, "20,50", "25.75", "31"));
        state = await State();
        Check(state.Version == 2 && state.Rates.Hour == 20.5m && state.Weeks.Single().Rates.Hour == 20, "Editable rates / frozen weekly rates");
        var weekend = Request(); weekend.Date = monday.AddDays(5); weekend.PartitaId = null;
        await Invoke(c => c.Save(2, weekend));
        Check((await State()).Version == 2, "Weekend rejected");
        await Invoke(c => c.Close(2, monday));
        state = await State();
        Check(state.Version == 3 && state.Weeks.Single().ClosedAtUtc.HasValue
            && state.Weeks.Single().Snapshot!.Shares.Sum(s => s.Amount) == 20, "Closing saves snapshot");
        var change = state.Weeks.Single().Requests.Single(); change.Contact = "Changed";
        await Invoke(c => c.Save(3, change));
        Check((await State()).Version == 3, "Closed request cannot change");
        await Invoke(c => c.Close(3, monday));
        Check((await State()).Version == 3, "Double closure rejected");
        game.Durata = 2;
        await seed.SaveChangesAsync();
        Check((await State()).Weeks.Single().Snapshot!.Pool == 20, "Snapshot unaffected by booking change");

        var next = Request(); next.Date = monday.AddDays(7); next.PartitaId = null;
        next.Outcome = BonusGameOutcome.Pending; next.Staff = BonusAnswer.No;
        await Invoke(c => c.Save(3, next));
        Check((await State()).Version == 4, "Uncovered request recorded without booking");
        await Invoke(c => c.Close(4, next.Date));
        Check((await State()).Version == 4, "Current week cannot close early");

        await Task.WhenAll(Invoke(c => c.SaveRates(4, monday, "21", "26", "31")), Invoke(c => c.SaveRates(4, monday, "22", "27", "32")));
        Check((await State()).Version == 5, "Concurrent changes serialize and only one succeeds");
        Console.WriteLine("PASS: PostgreSQL save, phone, duplicate, weekend, rates, stale forms, closure, history and concurrency checks.");
    }
    private sealed class TestTempData : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
