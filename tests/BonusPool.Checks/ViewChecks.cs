using System.Security.Claims;
using Full_Metal_Paintball_Carmagnola.Controllers;
using Full_Metal_Paintball_Carmagnola.Models;
using Full_Metal_Paintball_Carmagnola.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

internal static class ViewChecks
{
    public static async Task Run(bool preview)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(BonusPoolController).Assembly.GetName().Name,
            ContentRootPath = Directory.GetCurrentDirectory(),
            WebRootPath = Path.GetFullPath("FullMetalPaintballCarmagnola/wwwroot")
        });
        builder.Logging.ClearProviders();
        builder.Services.AddControllersWithViews().AddApplicationPart(typeof(BonusPoolController).Assembly);
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        await using var app = builder.Build();
        app.Urls.Add("http://127.0.0.1:55440");
        app.UseStaticFiles();
        app.MapControllerRoute("default", "{controller}/{action=Index}/{id?}");
        async Task<string> Render(string role)
        {
            using var scope = app.Services.CreateScope();
            var http = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
            http.Request.Scheme = "http";
            http.Request.Host = new HostString("127.0.0.1", 55440);
            http.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "Preview"), new Claim(ClaimTypes.Role, role) }, "Preview"));
            http.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(), "Preview"));
            var route = new RouteData();
            route.Values["controller"] = "BonusPool"; route.Values["action"] = "Index";
            var context = new ActionContext(http, route, new ActionDescriptor());
            var week = new BonusWeek { Monday = new DateOnly(2026, 9, 14) };
            var request = new BonusRequest
            {
                Id = Guid.NewGuid(), Contact = "Cliente di prova", Date = week.Monday, Phone = "3331234567",
                Staff = BonusAnswer.Yes, Customer = BonusAnswer.Yes, Outcome = BonusGameOutcome.Played,
                Attendees = new() { "Simone", "Davide" }, PartitaId = 10
            };
            week.Requests.Add(request);
            var model = new BonusPoolViewModel
            {
                Week = week, State = new BonusPoolState { Weeks = new() { week } }, Form = request,
                Staff = new() { "Simone", "Davide", "Enrico" }, Calculation = BonusPoolRules.Calculate(week), CanClose = true,
                Games = new() { new Partita { Id = 10, Data = week.Monday.ToDateTime(TimeOnly.MinValue), OraInizio = new TimeSpan(10,0,0), Durata = 1, NomeRiferimento = "Cliente di prova", Staff1 = "Simone", Staff2 = "Davide" } }
            };
            var engine = scope.ServiceProvider.GetRequiredService<IRazorViewEngine>();
            var view = engine.GetView(null, "/Views/BonusPool/Index.cshtml", false);
            if (!view.Success) throw new Exception(string.Join(", ", view.SearchedLocations));
            using var writer = new StringWriter();
            var data = new ViewDataDictionary<BonusPoolViewModel>(new EmptyModelMetadataProvider(), new ModelStateDictionary()) { Model = model };
            var tempData = new TempDataDictionary(http, scope.ServiceProvider.GetRequiredService<ITempDataProvider>());
            await view.View.RenderAsync(new ViewContext(context, view.View, data, tempData, writer, new HtmlHelperOptions()));
            return writer.ToString();
        }
        app.MapGet("/", async () => Results.Content("<!doctype html><html lang='it'><head><meta charset='utf-8'><meta name='viewport' content='width=device-width, initial-scale=1'><link rel='stylesheet' href='https://cdn.jsdelivr.net/npm/bootstrap@5.3.0/dist/css/bootstrap.min.css'><link rel='stylesheet' href='https://cdn.jsdelivr.net/npm/bootstrap-icons@1.10.5/font/bootstrap-icons.css'></head><body style='background:#e2e5cb'>" + await Render("Admin") + "<script src='/js/bonus-pool.js'></script></body></html>", "text/html; charset=utf-8"));
        await app.StartAsync();
        var admin = await Render("Admin");
        var staff = await Render("Staff");
        if (!admin.Contains("name=\"Form.Date\"") || !admin.Contains("value=\"2026-09-14\"")
            || !admin.Contains("value=\"10:00\"") || !admin.Contains("action=\"/BonusPool/Save\""))
            throw new Exception("Admin form rendering failed");
        if (staff.Contains("<form action=\"/BonusPool/Save") || staff.Contains("name=\"Form.Contact\"") || !staff.Contains("Consultazione in sola lettura"))
            throw new Exception("Staff view must be read only");
        Console.WriteLine("PASS: compiled Razor view renders dates, time, Admin actions and read-only Staff view.");
        if (preview)
        {
            Console.WriteLine("Preview only, sample data: http://127.0.0.1:55440/");
            await Task.Delay(Timeout.Infinite);
        }
        await app.StopAsync();
    }
}
