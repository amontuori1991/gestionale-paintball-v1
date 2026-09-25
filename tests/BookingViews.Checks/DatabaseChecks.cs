using Full_Metal_Paintball_Carmagnola.Controllers;
using Full_Metal_Paintball_Carmagnola.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

internal static class DatabaseChecks
{
    public static async Task Run()
    {
        // Dedicated local test database; never load application connection strings.
        var options = new DbContextOptionsBuilder<TesseramentoDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=55439;Database=compact_deposits_checks;Username=bonus_tests;SSL Mode=Disable").Options;
        await using var db = new TesseramentoDbContext(options);
        await db.Database.EnsureCreatedAsync();
        if (await db.Partite.AnyAsync()) throw new Exception("Use a fresh compact_deposits_checks database.");
        var date = new DateTime(2026,9,26,0,0,0,DateTimeKind.Utc);
        var cancelled = new Partita { Data = date, IsDeleted = true, Caparra = 30, Tipo = "Adulti" };
        var active = new Partita { Data = date.AddDays(1), Caparra = 20, Tipo = "Kids" };
        db.Partite.AddRange(cancelled, active);
        await db.SaveChangesAsync();
        var controller = new PartiteController(db, null!, null!, null!, null!, null!, null!, null!, null!);
        if (typeof(PartiteController).GetMethod("AggiornaDettagliCaparra")!.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>() == null)
            throw new Exception("Missing antiforgery protection.");
        await controller.AggiornaDettagliCaparra(cancelled.Id, "Rimborso effettuato", "SI");
        db.ChangeTracker.Clear();
        var saved = await db.Partite.SingleAsync(p => p.Id == cancelled.Id);
        if (!saved.IsDeleted || saved.Caparra != 30 || saved.Rimborso != "SI" || saved.Annotazioni != "Rimborso effettuato")
            throw new Exception("Cancelled deposit save failed.");
        var result = (ViewResult)await controller.Caparre("cancellata", date, date);
        if (((List<Partita>)result.Model!).Count != 1) throw new Exception("Date/state filter failed.");
        var empty = (ViewResult)await controller.Caparre("attiva", date, date);
        if (((List<Partita>)empty.Model!).Count != 0) throw new Exception("State filtering failed.");
        if (await controller.AggiornaDettagliCaparra(active.Id, "bad", "INVALID") is not BadRequestObjectResult)
            throw new Exception("Invalid refund accepted.");
        await controller.AggiornaDettagliCaparra(active.Id, "Nota attiva", "NO");
        db.ChangeTracker.Clear();
        if ((await db.Partite.SingleAsync(p => p.Id == active.Id)).Annotazioni != "Nota attiva")
            throw new Exception("Active deposit save failed.");
        Console.WriteLine("PASS: actual PostgreSQL persistence for active/cancelled deposits, state/date filters, refund validation.");
    }
}
