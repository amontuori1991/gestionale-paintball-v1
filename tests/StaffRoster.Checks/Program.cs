using Full_Metal_Paintball_Carmagnola.Models;
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

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    ApplicationName = typeof(Partita).Assembly.GetName().Name,
    WebRootPath = Path.GetFullPath("FullMetalPaintballCarmagnola/wwwroot")
});
builder.Logging.ClearProviders();
builder.Services.AddControllersWithViews().AddApplicationPart(typeof(Partita).Assembly);
builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
await using var app = builder.Build();
app.Urls.Add("http://127.0.0.1:55441");
app.UseStaticFiles();
app.MapControllerRoute("default", "{controller}/{action=Index}/{id?}");

async Task<string> Render(string controller)
{
    using var scope = app.Services.CreateScope();
    var http = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
    http.Request.Scheme = "http";
    http.Request.Host = new HostString("127.0.0.1", 55441);
    http.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(), "Preview"));
    var route = new RouteData();
    route.Values["controller"] = controller;
    route.Values["action"] = "Index";
    var context = new ActionContext(http, route, new ActionDescriptor());
    var date = new DateTime(2026, 9, 26);
    var dates = new List<DateTime> { date, date.AddDays(1), date.AddDays(4), date.AddDays(7) };
    var absences = dates.Select((day, index) => new AssenzaCalendario
    {
        Id = index + 1, Data = day, Giorno = "test", Reperibile = index == 0 ? "Bosax" : "In attesa", Bosax = "assente"
    }).ToList();
    var data = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary());
    if (controller == "CalendarioAssenze") data.Model = absences;
    data["DateList"] = dates;
    data["ReperibilitaList"] = absences;
    data["StaffList"] = new List<string> { "Simone", "Federico", "Davide", "Alessandro", "Enrico" };
    data["PresenzaList"] = new List<PresenzaStaff>
    {
        new() { Data = date, Giorno = "sabato", NomeStaff = "Simone", Presente = true },
        new() { Data = date, Giorno = "sabato", NomeStaff = "Davide", Presente = false }
    };
    data["ChiusureCampo"] = new Dictionary<DateTime, CampoChiusura>
    {
        [dates.Last()] = new() { DataInizio = dates.Last(), DataFine = dates.Last(), Motivo = "Manutenzione del campo" }
    };
    var view = scope.ServiceProvider.GetRequiredService<IRazorViewEngine>().GetView(null, $"/Views/{controller}/Index.cshtml", false);
    if (!view.Success) throw new Exception("View not found");
    using var writer = new StringWriter();
    var temp = new TempDataDictionary(http, scope.ServiceProvider.GetRequiredService<ITempDataProvider>());
    await view.View.RenderAsync(new ViewContext(context, view.View, data, temp, writer, new HtmlHelperOptions()));
    return writer.ToString();
}
app.MapGet("/preview/{page}", async (string page) =>
{
    if (page != "CalendarioAssenze" && page != "PresenzeStaff") return Results.NotFound();
    return Results.Content("<!doctype html><html lang='it'><head><meta charset='utf-8'><meta name='viewport' content='width=device-width, initial-scale=1'><link rel='stylesheet' href='https://cdn.jsdelivr.net/npm/bootstrap@5.3.0/dist/css/bootstrap.min.css'><link rel='stylesheet' href='/css/site.css'></head><body>" + await Render(page) + "<script src='/js/staff-roster.js'></script></body></html>", "text/html; charset=utf-8");
});
await app.StartAsync();
foreach (var page in new[] { "CalendarioAssenze", "PresenzeStaff" })
{
    var html = await Render(page);
    if (html.Contains("<table") || html.Split("<article").Length - 1 != 4 ||
        !html.Contains("Manutenzione del campo") || !html.Contains("data-search-date=\"26/09/2026\"") ||
        !html.Contains("aria-label="))
        throw new Exception("Card rendering failed: " + page);
    if (page == "PresenzeStaff" && (!html.Contains("value=\"SI\" selected=") || !html.Contains("value=\"NO\" selected=")))
        throw new Exception("Availability values lost");
    Console.WriteLine("PASS: " + page + " cards, date search, closure, accessible inputs and saved states.");
}
if (args.Contains("--preview")) await Task.Delay(Timeout.Infinite);
await app.StopAsync();
