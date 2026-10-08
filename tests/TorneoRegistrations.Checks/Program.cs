using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using Full_Metal_Paintball_Carmagnola.Data;
using Full_Metal_Paintball_Carmagnola.Models;
using Full_Metal_Paintball_Carmagnola.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

var checks = 0;
void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
    checks++;
    Console.WriteLine("PASS: " + message);
}

var adminCs = Environment.GetEnvironmentVariable("TORNEO_TEST_POSTGRES") ??
    "Host=127.0.0.1;Port=55439;Database=postgres;Username=bonus_tests;SSL Mode=Disable";
var database = "torneo_registration_checks_" + Guid.NewGuid().ToString("N");
var originalDirectory = Directory.GetCurrentDirectory();
var contentRoot = Path.GetFullPath("FullMetalPaintballCarmagnola");
var testFiles = Path.Combine(Path.GetTempPath(), database);
Directory.CreateDirectory(testFiles);
await using var admin = new NpgsqlConnection(adminCs);
await admin.OpenAsync();
await new NpgsqlCommand($"CREATE DATABASE {database}", admin).ExecuteNonQueryAsync();
var cs = new NpgsqlConnectionStringBuilder(adminCs) { Database = database }.ConnectionString;
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    ApplicationName = typeof(Tesseramento).Assembly.GetName().Name,
    ContentRootPath = contentRoot, WebRootPath = "wwwroot", EnvironmentName = "Development"
});
builder.Logging.ClearProviders();
builder.Services.AddControllersWithViews().AddApplicationPart(typeof(Tesseramento).Assembly);
builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
builder.Services.AddDbContext<TesseramentoDbContext>(o => o.UseNpgsql(cs));
builder.Services.AddDbContext<ApplicationDbContext>(o => o.UseNpgsql(cs));
builder.Services.AddIdentity<ApplicationUser, IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>();
builder.Services.AddAuthentication(o =>
{
    o.DefaultAuthenticateScheme = "Test"; o.DefaultChallengeScheme = "Test"; o.DefaultForbidScheme = "Test";
}).AddScheme<AuthenticationSchemeOptions, TestAuth>("Test", _ => { });
builder.Services.AddAuthorization(o => o.AddPolicy("Tesserati", p => p.RequireRole("Admin")));
builder.Services.AddScoped<TournamentRegistrationService>();
builder.Services.AddScoped<AcsiOdsExportService>();
builder.Services.AddSingleton<RecordingEmail>();
builder.Services.AddSingleton<IEmailService>(s => s.GetRequiredService<RecordingEmail>());
await using var app = builder.Build();
app.Urls.Add("http://127.0.0.1:0");
app.UseRouting(); app.UseAuthentication(); app.UseAuthorization();
app.MapControllerRoute("default", "{controller}/{action=Index}/{id?}");
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<TesseramentoDbContext>();
    var service = scope.ServiceProvider.GetRequiredService<TournamentRegistrationService>();
    await db.Database.EnsureCreatedAsync();
    // Simulate the deployed pre-tournament membership table, not only EF's newest schema.
    await db.Database.ExecuteSqlRawAsync("""
        ALTER TABLE "Tesseramenti" DROP COLUMN "TorneoOrigineId";
        ALTER TABLE "Tesseramenti" DROP COLUMN "EsportatoAcsiIl";
        ALTER TABLE "Tesseramenti" DROP COLUMN "AnnoValiditaTesseramento";
        """);
    await TorneoSchema.EnsureAsync(db);
    await TorneoSchema.EnsureAsync(db);
    var today = DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow,
        TimeZoneInfo.FindSystemTimeZoneById("Europe/Rome")).Date, DateTimeKind.Utc);
    async Task<TorneoSquadra> Team(string name, DateTime? date = null)
    {
        var team = new TorneoSquadra
        {
            Nome = name, Referente = "PRIVATE-TEAM-CONTACT", Telefono = "PRIVATE-TEAM-PHONE",
            Torneo = new Torneo { Nome = "Tournament " + name, Data = date ?? today }
        };
        db.TorneoSquadre.Add(team); await db.SaveChangesAsync(); return team;
    }
    Tesseramento Person(string name) => new()
    {
        Nome = name, Cognome = "PrivateSurname", DataNascita = new DateTime(1990, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        Genere = "Maschio", ComuneNascita = "Torino", ComuneResidenza = "Torino (TO)",
        Email = "private-member@example.test", Cellulare = "PRIVATE-MEMBER-PHONE", Minorenne = "No",
        Firma = "/Firme/PRIVATE-SIGNATURE.png", TerminiAccettati = true, DataCreazione = today,
        CodiceFiscale = "CF" + name[..Math.Min(name.Length, 14)]
    };
    async Task<int> Delete(TorneoSquadra team, bool rollback = false)
    {
        db.ChangeTracker.Clear();
        await using var tx = await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Tornei\" WHERE \"Id\" = {team.TorneoId} FOR UPDATE");
        var count = await service.DeleteRegistrationsAsync(team.TorneoId);
        db.Tornei.Remove(await db.Tornei.SingleAsync(t => t.Id == team.TorneoId));
        await db.SaveChangesAsync();
        if (rollback) await tx.RollbackAsync(); else await tx.CommitAsync();
        db.ChangeTracker.Clear();
        return count;
    }
    async Task<bool> Exists(int id) => await db.Tesseramenti.AsNoTracking().AnyAsync(t => t.Id == id);

    var exclusiveTeam = await Team("Exclusive");
    var exclusive = await service.RegisterAsync(exclusiveTeam.Token, Person("Exclusive"));
    Check(!exclusive.Existing && !exclusive.Duplicate && exclusive.Membership.PartitaId == null,
        "new tournament membership is not a Partita");
    Check(exclusive.Membership.TorneoOrigineId == exclusiveTeam.TorneoId &&
        exclusive.Membership.AnnoValiditaTesseramento == today.Year, "ownership and membership year persisted");
    var repeated = await service.RegisterAsync(exclusiveTeam.Token, Person("Exclusive"));
    Check(repeated.Duplicate && repeated.Membership.Id == exclusive.Membership.Id &&
        await db.TorneoIscrizioni.CountAsync() == 1, "duplicate signup through same Guid token is idempotent");
    var otherTeam = new TorneoSquadra { Nome = "Other squad", TorneoId = exclusiveTeam.TorneoId };
    db.TorneoSquadre.Add(otherTeam); await db.SaveChangesAsync();
    Check((await service.RegisterAsync(otherTeam.Token, Person("Exclusive"))).Duplicate &&
        !await db.TorneoIscrizioni.AnyAsync(i => i.TorneoSquadraId == otherTeam.Id),
        "same person cannot register into another squad in the same tournament");
    var duplicateToken = new TorneoSquadra { Nome = "Invalid token", Token = exclusiveTeam.Token, TorneoId = exclusiveTeam.TorneoId };
    db.TorneoSquadre.Add(duplicateToken);
    var uniqueTokenRejected = false;
    try { await db.SaveChangesAsync(); } catch (DbUpdateException) { uniqueTokenRejected = true; }
    db.Entry(duplicateToken).State = EntityState.Detached;
    Check(uniqueTokenRejected, "duplicate team Guid rejected by database unique index");
    var missingRejected = false;
    try { await service.RegisterAsync(Guid.NewGuid(), Person("Missing")); }
    catch (InvalidOperationException) { missingRejected = true; }
    Check(missingRejected, "unknown token cannot create membership");
    var noTransactionRejected = false;
    try { await service.DeleteRegistrationsAsync(exclusiveTeam.TorneoId); }
    catch (InvalidOperationException) { noTransactionRejected = true; }
    Check(noTransactionRejected, "deletion without caller transaction rejected");
    Check(await Delete(exclusiveTeam, rollback: true) == 1 && await Exists(exclusive.Membership.Id),
        "parent transaction rollback restores membership and registrations");
    Check(await Delete(exclusiveTeam) == 1 && !await Exists(exclusive.Membership.Id), "exclusive unexported new membership removed");
    Check(!await db.TorneoIscrizioni.AnyAsync(), "registrations unlinked before tournament removal");

    var existingTeam = await Team("Existing");
    var existing = Person("Existing"); db.Tesseramenti.Add(existing); await db.SaveChangesAsync();
    var oldSignature = existing.Firma;
    var submitted = Person("Existing"); submitted.Firma = "/Firme/new-consent.png"; submitted.Email = "submitted@example.test";
    var reused = await service.RegisterAsync(existingTeam.Token, submitted);
    Check(reused.Existing && reused.Membership.Id == existing.Id && reused.Membership.Email == "private-member@example.test" &&
        reused.Membership.Firma == oldSignature, "existing membership reused without overwriting private identity or signature");
    Check((await db.TorneoIscrizioni.SingleAsync()).Firma == submitted.Firma, "new event consent signature retained separately");
    Check(await Delete(existingTeam) == 0 && await Exists(existing.Id), "existing membership retained and unlinked");

    var cardTeam = await Team("Card");
    var card = await service.RegisterAsync(cardTeam.Token, Person("Card"));
    card.Membership.Tessera = "123456789"; await db.SaveChangesAsync();
    Check(await Delete(cardTeam) == 0 && await Exists(card.Membership.Id), "historical card conservatively protects without export marker");

    var a = await Team("ReuseA"); var b = await Team("ReuseB");
    var owned = await service.RegisterAsync(a.Token, Person("Reused"));
    var otherEvent = await service.RegisterAsync(b.Token, Person("Reused"));
    Check(otherEvent.Existing && otherEvent.Membership.Id == owned.Membership.Id &&
        otherEvent.Membership.TorneoOrigineId == null, "reuse in another event permanently removes exclusive ownership");
    Check(await Delete(b) == 0 && await Delete(a) == 0 && await Exists(owned.Membership.Id),
        "reused membership survives deletion of both events in reverse order");

    var legacyTeam = await Team("LegacyReuse");
    var legacy = await service.RegisterAsync(legacyTeam.Token, Person("LegacyReuse"));
    var legacyCopy = Person("LegacyReuse");
    legacyCopy.Partita = new Partita { Data = today, Tipo = "Adulti", NomeRiferimento = "Test", TelefonoRiferimento = "Test" };
    db.Tesseramenti.Add(legacyCopy); await db.SaveChangesAsync();
    Check(await Delete(legacyTeam) == 0 && await Exists(legacy.Membership.Id), "legacy separate match attendance protects original membership");

    var priorTeam = await Team("PriorYear");
    var prior = Person("PriorYear"); prior.DataCreazione = today.AddYears(-1); db.Tesseramenti.Add(prior); await db.SaveChangesAsync();
    var renewed = await service.RegisterAsync(priorTeam.Token, Person("PriorYear"));
    Check(!renewed.Existing && renewed.Membership.Id != prior.Id, "expired prior-year membership not reused");
    var futureDate = new DateTime(today.Year + 1, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    var futureTeam = await Team("FutureYear", futureDate);
    var future = await service.RegisterAsync(futureTeam.Token, Person("FutureYear"));
    future.Membership.EsportatoAcsiIl = DateTime.UtcNow; await db.SaveChangesAsync();
    Check(await Delete(futureTeam) == 0, "future-year protected membership survives event deletion");
    var futureAgain = await Team("FutureAgain", futureDate);
    Check((await service.RegisterAsync(futureAgain.Token, Person("FutureYear"))).Membership.Id == future.Membership.Id,
        "future-year validity survives removal of originating event");
    var thisYear = await Team("CurrentYear");
    Check(!(await service.RegisterAsync(thisYear.Token, Person("FutureYear"))).Existing,
        "next-year membership cannot cover current year despite creation date");
    var expiredTeam = await Team("Expired", today.AddDays(-1));
    Check(await service.FindTeamAsync(expiredTeam.Token) != null, "past tournament remains available without an explicit closing rule");
    Check(!(await service.RegisterAsync(expiredTeam.Token, Person("Expired"))).Duplicate,
        "past tournament registration remains supported");

    var publicTeam = await Team("Public");
    await app.StartAsync();
    Directory.SetCurrentDirectory(testFiles);
    using var anon = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = new Uri(app.Urls.Single()) };
    using var staff = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = anon.BaseAddress };
    staff.DefaultRequestHeaders.Add("X-Test-Role", "Admin");
    string Token(string html) => WebUtility.HtmlDecode(Regex.Match(html,
        "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
    var signupUrl = "/Tesseramento?torneoSquadraToken=" + publicTeam.Token;
    var html = await anon.GetStringAsync(signupUrl);
    Check(html.Contains(publicTeam.Nome) && html.Contains(publicTeam.Token.ToString()), "public signup renders team and retains token");
    Check(!new[] { "PRIVATE-TEAM-CONTACT", "PRIVATE-TEAM-PHONE", "private-member@example.test", "PRIVATE-SIGNATURE", "PrivateSurname" }
        .Any(secret => html.Contains(secret)), "anonymous signup does not expose team contacts or stored member data");
    Check((await anon.GetStringAsync(signupUrl + "&lang=en")).Contains(publicTeam.Token.ToString()), "language switch retains team Guid");
    Check(!(await anon.GetStringAsync("/Tesseramento?torneoSquadraToken=malformed")).Contains("id=\"tesseramentoForm\""),
        "malformed Guid does not fall back to free signup");
    Check((await anon.GetStringAsync("/Tesseramento?torneoSquadraToken=" + expiredTeam.Token)).Contains("id=\"tesseramentoForm\""),
        "past public signup renders form without arbitrary expiry");
    Check((await anon.GetAsync("/Tesseramento/ListaTesseramenti")).StatusCode == HttpStatusCode.Unauthorized,
        "anonymous membership list blocked");
    Check((await anon.PostAsync("/Tesseramento/ExportAcsiOds", new FormUrlEncodedContent(new Dictionary<string, string>()))).StatusCode == HttpStatusCode.Unauthorized,
        "anonymous export blocked");
    var form = new Dictionary<string, string>
    {
        ["TorneoSquadraToken"] = publicTeam.Token.ToString(), ["Nome"] = "PublicSignup", ["Cognome"] = "Test",
        ["DataNascita"] = "1990-01-01", ["Genere"] = "Maschio", ["NatoEstero"] = "true", ["NazioneNascita"] = "Francia",
        ["CittaNascita"] = "Paris", ["NazioneCittadinanza"] = "Francia", ["NazioneResidenza"] = "Francia",
        ["ComuneResidenza"] = "Paris", ["TipoDocumentoEstero"] = "Passaporto", ["NumeroDocumentoEstero"] = "TEST-DOC",
        ["Email"] = "publicsignup@example.test", ["NewsletterConsent"] = "false", ["Minorenne"] = "No",
        ["TerminiAccettati"] = "true", ["Firma"] = "data:image/png;base64,AQID", ["Lingua"] = "it"
    };
    Check((await anon.PostAsync("/Tesseramento", new FormUrlEncodedContent(form))).StatusCode == HttpStatusCode.BadRequest,
        "anonymous registration still requires antiforgery token");
    form["__RequestVerificationToken"] = Token(html);
    var post = await anon.PostAsync("/Tesseramento", new FormUrlEncodedContent(form));
    Check(post.StatusCode == HttpStatusCode.Redirect && post.Headers.Location!.ToString().EndsWith("Successo"),
        "actual public MVC signup accepts validated form");
    db.ChangeTracker.Clear();
    var publicMember = await db.Tesseramenti.SingleAsync(t => t.Nome == "PublicSignup");
    Check(publicMember.PartitaId == null && publicMember.TorneoOrigineId == publicTeam.TorneoId &&
        await db.TorneoIscrizioni.AnyAsync(i => i.TesseramentoId == publicMember.Id), "HTTP signup creates separate tournament relation");
    Check(app.Services.GetRequiredService<RecordingEmail>().Count == 1 &&
        File.Exists(Path.Combine(testFiles, "wwwroot", publicMember.Firma.TrimStart('/'))), "existing notification and signature flow exercised without real mail");

    var match = new Partita { Data = today, Tipo = "Adulti", NomeRiferimento = "Match", TelefonoRiferimento = "Test" };
    db.Partite.Add(match); await db.SaveChangesAsync();
    var matchForm = new Dictionary<string, string>(form) { ["PartitaId"] = match.Id.ToString() };
    matchForm.Remove("TorneoSquadraToken");
    var matchResponse = await anon.PostAsync("/Tesseramento", new FormUrlEncodedContent(matchForm));
    Check(matchResponse.StatusCode == HttpStatusCode.Redirect && matchResponse.Headers.Location!.ToString().EndsWith("Successo"),
        "existing tournament member can register for a match");
    db.ChangeTracker.Clear();
    Check((await db.Tesseramenti.SingleAsync(t => t.PartitaId == match.Id)).NoTesseramento &&
        (await db.Tesseramenti.SingleAsync(t => t.Id == publicMember.Id)).TorneoOrigineId == null,
        "same-year match skips duplicate membership and permanently protects tournament original");
    var nextMatch = new Partita { Data = futureDate, Tipo = "Adulti", NomeRiferimento = "Next year", TelefonoRiferimento = "Test" };
    db.Partite.Add(nextMatch); await db.SaveChangesAsync();
    matchForm["PartitaId"] = nextMatch.Id.ToString();
    await anon.PostAsync("/Tesseramento", new FormUrlEncodedContent(matchForm));
    db.ChangeTracker.Clear();
    Check(!(await db.Tesseramenti.SingleAsync(t => t.PartitaId == nextMatch.Id)).NoTesseramento,
        "next-year match cannot reuse expired tournament membership");

    var filterDate = today.AddDays(10);
    var filterTeam = await Team("Filter", filterDate);
    var included = await service.RegisterAsync(filterTeam.Token, Person("FilterIncluded"));
    var unrelatedTeam = await Team("Unrelated", filterDate);
    var unrelated = await service.RegisterAsync(unrelatedTeam.Token, Person("FilterUnrelated"));
    var listUrl = $"/Tesseramento/ListaTesseramenti?torneoId={filterTeam.TorneoId}&dataDa={filterDate:yyyy-MM-dd}&dataA={filterDate:yyyy-MM-dd}";
    var filteredHtml = await staff.GetStringAsync(listUrl);
    Check(filteredHtml.Contains("FilterIncluded") && !filteredHtml.Contains("FilterUnrelated"),
        "tournament list date filter uses event date rather than creation date and excludes unrelated tournament");
    Check(Regex.IsMatch(filteredHtml, $"type=\"hidden\" name=\"torneoId\" value=\"{filterTeam.TorneoId}\""),
        "bulk export form preserves tournament filter");
    Check(!(await staff.GetStringAsync($"/Tesseramento/ListaTesseramenti?torneoId={filterTeam.TorneoId}&dataDa={filterDate.AddDays(1):yyyy-MM-dd}")).Contains("FilterIncluded"),
        "tournament outside requested date range excluded");
    var filteredExport = await staff.PostAsync("/Tesseramento/ExportAcsiOds", new FormUrlEncodedContent(new Dictionary<string, string>
    {
        ["__RequestVerificationToken"] = Token(filteredHtml), ["torneoId"] = filterTeam.TorneoId.ToString(),
        ["dataDa"] = filterDate.ToString("yyyy-MM-dd"), ["dataA"] = filterDate.ToString("yyyy-MM-dd")
    }));
    db.ChangeTracker.Clear();
    Check(filteredExport.IsSuccessStatusCode &&
        (await db.Tesseramenti.SingleAsync(t => t.Id == included.Membership.Id)).EsportatoAcsiIl.HasValue &&
        !(await db.Tesseramenti.SingleAsync(t => t.Id == unrelated.Membership.Id)).EsportatoAcsiIl.HasValue,
        "ACSI export honors tournament/date filters and marks only selected event memberships");

    var exportTeam = await Team("Export");
    var export = await service.RegisterAsync(exportTeam.Token, Person("ExportOnly"));
    var excluded = Person("ExportOnlySkipped"); excluded.NoTesseramento = true; db.Tesseramenti.Add(excluded); await db.SaveChangesAsync();
    var adminHtml = await staff.GetStringAsync(signupUrl);
    var exportForm = new Dictionary<string, string> { ["__RequestVerificationToken"] = Token(adminHtml), ["searchNome"] = "ExportOnly" };
    var archive = await staff.PostAsync("/Tesseramento/ExportAcsiOds", new FormUrlEncodedContent(exportForm));
    Check(archive.IsSuccessStatusCode && archive.Content.Headers.ContentType?.MediaType == "application/zip" &&
        (await archive.Content.ReadAsByteArrayAsync()).Length > 100, "real ACSI templates produce export archive");
    db.ChangeTracker.Clear();
    var marker = (await db.Tesseramenti.SingleAsync(t => t.Id == export.Membership.Id)).EsportatoAcsiIl;
    Check(marker.HasValue && !(await db.Tesseramenti.SingleAsync(t => t.Id == excluded.Id)).EsportatoAcsiIl.HasValue,
        "export marks included memberships only before response");
    Check((await staff.PostAsync("/Tesseramento/ExportAcsiOds", new FormUrlEncodedContent(exportForm))).IsSuccessStatusCode,
        "repeat export succeeds");
    db.ChangeTracker.Clear();
    Check((await db.Tesseramenti.SingleAsync(t => t.Id == export.Membership.Id)).EsportatoAcsiIl == marker,
        "first export timestamp is stable on repeat export");
    Check(await Delete(exportTeam) == 0 && await Exists(export.Membership.Id), "actual export marker protects event-owned membership on deletion");

    var failedExportTeam = await Team("FailedExport");
    var failedExport = await service.RegisterAsync(failedExportTeam.Token, Person("FailedExport"));
    app.Environment.ContentRootPath = testFiles;
    var failedExportForm = new Dictionary<string, string>(exportForm) { ["searchNome"] = "FailedExport" };
    var failedArchive = await staff.PostAsync("/Tesseramento/ExportAcsiOds", new FormUrlEncodedContent(failedExportForm));
    app.Environment.ContentRootPath = contentRoot;
    db.ChangeTracker.Clear();
    Check(failedArchive.StatusCode == HttpStatusCode.InternalServerError &&
        !(await db.Tesseramenti.SingleAsync(t => t.Id == failedExport.Membership.Id)).EsportatoAcsiIl.HasValue,
        "failed export does not mark membership exported");

    string Signature()
    {
        var relative = "/Firme/firma_" + Guid.NewGuid() + ".png";
        File.WriteAllBytes(Path.Combine(testFiles, "wwwroot", relative.TrimStart('/')), [1, 2, 3]);
        return relative;
    }
    bool SignatureExists(string relative) => File.Exists(Path.Combine(testFiles, "wwwroot", relative.TrimStart('/')));
    var documentTeam = await Team("Documents");
    var documentPerson = Person("Documents"); documentPerson.Firma = Signature();
    await service.RegisterAsync(documentTeam.Token, documentPerson);
    var candidates = await service.GetSignatureCandidatesAsync(documentTeam.TorneoId);
    Check(candidates.Contains(documentPerson.Firma), "collect exclusive membership signature before unlinking");
    await Delete(documentTeam, rollback: true);
    Check(await service.CleanupSignaturesAsync(candidates, Path.Combine(testFiles, "wwwroot")) == 0 && SignatureExists(documentPerson.Firma),
        "rollback and remaining membership references protect signature files");
    await Delete(documentTeam);
    Check(await service.CleanupSignaturesAsync(candidates, Path.Combine(testFiles, "wwwroot")) == 1 && !SignatureExists(documentPerson.Firma),
        "postcommit cleanup removes exclusively deleted event signature");

    var consentTeam = await Team("Consent");
    var consentOtherTeam = await Team("ConsentOther");
    var consentMember = Person("Consent"); consentMember.Firma = Signature();
    db.Tesseramenti.Add(consentMember); await db.SaveChangesAsync();
    var consent = Person("Consent"); consent.Firma = Signature();
    await service.RegisterAsync(consentTeam.Token, consent);
    await service.RegisterAsync(consentOtherTeam.Token, consent);
    var consentCandidates = await service.GetSignatureCandidatesAsync(consentTeam.TorneoId);
    Check(consentCandidates.Contains(consent.Firma) && !consentCandidates.Contains(consentMember.Firma),
        "collect event consent but never original preexisting membership signature");
    await Delete(consentTeam);
    Check(await service.CleanupSignaturesAsync(consentCandidates, Path.Combine(testFiles, "wwwroot")) == 0 && SignatureExists(consent.Firma),
        "another event consent reference protects file even without membership signature reference");
    await Delete(consentOtherTeam);
    Check(await service.CleanupSignaturesAsync(consentCandidates, Path.Combine(testFiles, "wwwroot")) == 1 && SignatureExists(consentMember.Firma),
        "last event cleanup removes event consent and retains original signature");
    Check(await service.CleanupSignaturesAsync([consentMember.Firma], Path.Combine(testFiles, "wwwroot")) == 0,
        "cleanup rechecks membership references even for manually supplied candidates");

    var outside = Path.Combine(testFiles, "outside.png"); File.WriteAllBytes(outside, [1]);
    Check(await service.CleanupSignaturesAsync(["/Firme/../../outside.png", outside, "/Firme/../outside.png"],
        Path.Combine(testFiles, "wwwroot")) == 0 && File.Exists(outside), "signature cleanup rejects path traversal and absolute paths");
    Check(await service.CleanupSignaturesAsync(candidates, Path.Combine(testFiles, "wwwroot")) == 0,
        "signature cleanup is idempotent");
    Console.WriteLine($"PASS: {checks} checks against disposable PostgreSQL database {database}.");
}
finally
{
    Directory.SetCurrentDirectory(originalDirectory);
    await app.StopAsync();
    NpgsqlConnection.ClearAllPools();
    await new NpgsqlCommand($"DROP DATABASE {database} WITH (FORCE)", admin).ExecuteNonQueryAsync();
    Directory.Delete(testFiles, recursive: true);
}

sealed class RecordingEmail : IEmailService
{
    public int Count { get; private set; }
    public Task SendEmailAsync(string email, string subject, string htmlMessage) => Task.CompletedTask;
    public Task SendTesseramentoNotification(TesseramentoViewModel model, string firmaAbsoluteUrl)
    { Count++; return Task.CompletedTask; }
}

sealed class TestAuth(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var role = Request.Headers["X-Test-Role"].ToString();
        if (string.IsNullOrEmpty(role)) return Task.FromResult(AuthenticateResult.NoResult());
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, role)], "Test")), "Test")));
    }
    protected override Task HandleChallengeAsync(AuthenticationProperties properties) { Response.StatusCode = 401; return Task.CompletedTask; }
    protected override Task HandleForbiddenAsync(AuthenticationProperties properties) { Response.StatusCode = 403; return Task.CompletedTask; }
}
