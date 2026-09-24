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
app.Urls.Add("http://127.0.0.1:55442");
app.UseStaticFiles();
app.MapControllerRoute("default", "{controller}/{action=Index}/{id?}");

async Task<string> Render(string page, bool mixed = false)
{
    using var scope = app.Services.CreateScope();
    var http = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
    http.Request.Scheme = "http";
    http.Request.Host = new HostString("127.0.0.1", 55442);
    http.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(), "Preview"));
    var route = new RouteData();
    route.Values["controller"] = "Partite";
    route.Values["action"] = "Index";
    var context = new ActionContext(http, route, new ActionDescriptor());
    var date = new DateTime(2026, 9, 26);
    var games = new List<Partita>
    {
        new() { Id = 1, Data = date, OraInizio = new TimeSpan(10,30,0), Tipo = "Adulti", Durata = 1.5, NumeroPartecipanti = 13, Caparra = 30, Listino = 2, Staff1 = "Simone", Staff3 = "Alessandro", Reperibile = "Bosax", CaparraConfermata = true, Caccia = true, CacciaDoppia = true },
        new() { Id = 2, Data = date.AddDays(1), OraInizio = new TimeSpan(15,0,0), Tipo = "Kids", Durata = 2, NumeroPartecipanti = 8, Caparra = 25, Listino = mixed ? (short)1 : (short)2, Nazionalita = "ENG", ColpiIllimitati = true },
        new() { Id = 3, Data = date.AddDays(2), Tipo = "Adulti", Caparra = 20, Listino = 1, IsDeleted = true, Staff4 = "Hidden", Annotazioni = "Nota prova", Rimborso = "SI" }
    };
    var data = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary()) { Model = games };
    data["CurrentListinoId"] = (short)2;
    data["ListinoLabels"] = new Dictionary<short,string> { [1] = "Vecchio listino", [2] = "Listino attuale" };
    var view = scope.ServiceProvider.GetRequiredService<IRazorViewEngine>().GetView(null, $"/Views/Partite/{page}.cshtml", false);
    if (!view.Success) throw new Exception("View not found");
    using var writer = new StringWriter();
    var temp = new TempDataDictionary(http, scope.ServiceProvider.GetRequiredService<ITempDataProvider>());
    await view.View.RenderAsync(new ViewContext(context, view.View, data, temp, writer, new HtmlHelperOptions()));
    return writer.ToString();
}
app.MapGet("/preview/{page}", async (string page) =>
{
    if (page != "Semplificata" && page != "Caparre") return Results.NotFound();
    return Results.Content("<!doctype html><html lang='it'><head><meta charset='utf-8'><meta name='viewport' content='width=device-width, initial-scale=1'><link rel='stylesheet' href='https://cdn.jsdelivr.net/npm/bootstrap@5.3.0/dist/css/bootstrap.min.css'><link rel='stylesheet' href='/css/site.css'></head><body>" + await Render(page, true) + "</body></html>", "text/html; charset=utf-8");
});
await app.StartAsync();
var compact = await Render("Semplificata");
var mixed = await Render("Semplificata", true);
if (compact.Contains(">Listino</th>") || !mixed.Contains(">Listino</th>") ||
    !compact.Contains(">Staff 2</th>") || compact.Contains(">Staff 3</th>") ||
    compact.Contains("Hidden") || !compact.Contains("Alessandro") ||
    compact.Contains(">Stato</th>") || compact.Contains(">Lingua</th>"))
    throw new Exception("Conditional columns or compact staff failed");
var deposits = await Render("Caparre");
if (!deposits.Contains("Nota prova") || !deposits.Contains("cap-save") ||
    !deposits.Contains("name=\"dataDa\"") || !deposits.Contains("row-cancellata"))
    throw new Exception("Deposits form failed");
Console.WriteLine("PASS: conditional price/staff columns, compact names, deleted exclusion and editable cancelled deposits.");
if (args.Contains("--database")) await DatabaseChecks.Run();
if (args.Contains("--preview")) await Task.Delay(Timeout.Infinite);
await app.StopAsync();
