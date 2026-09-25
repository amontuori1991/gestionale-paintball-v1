using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Full_Metal_Paintball_Carmagnola.Controllers;
using Full_Metal_Paintball_Carmagnola.Data;
using Full_Metal_Paintball_Carmagnola.Models;
using Full_Metal_Paintball_Carmagnola.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using SkiaSharp;

void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
if (args.Length == 2 && args[0] == "--heif-only")
{
    var host = WebApplication.CreateBuilder(new WebApplicationOptions
    { ContentRootPath = Path.GetFullPath("FullMetalPaintballCarmagnola"), WebRootPath = "wwwroot" });
    using var sourceHeif = File.OpenRead(args[1]);
    var jpegHeif = new PhotoWatermarker(host.Environment).Process(sourceHeif);
    using var decoded = SKBitmap.Decode(jpegHeif);
    Check(decoded != null && jpegHeif[0] == 0xff && jpegHeif[1] == 0xd8, "Native HEIF decoding failed");
    Console.WriteLine("PASS: native HEIF conversion and watermark on " + System.Runtime.InteropServices.RuntimeInformation.OSDescription);
    return;
}
var database = "photo_checks_" + Guid.NewGuid().ToString("N");
const string adminCs = "Host=127.0.0.1;Port=55439;Database=postgres;Username=bonus_tests;SSL Mode=Disable";
await using var admin = new NpgsqlConnection(adminCs);
await admin.OpenAsync();
await new NpgsqlCommand($"CREATE DATABASE {database}", admin).ExecuteNonQueryAsync();
var cs = new NpgsqlConnectionStringBuilder(adminCs) { Database = database }.ConnectionString;
var root = Path.GetFullPath("FullMetalPaintballCarmagnola");
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{ ApplicationName = typeof(Partita).Assembly.GetName().Name, ContentRootPath = root, WebRootPath = Path.Combine(root, "wwwroot"), EnvironmentName = "Development" });
builder.Logging.ClearProviders();
builder.Services.AddControllersWithViews().AddApplicationPart(typeof(Partita).Assembly);
builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
builder.Services.AddDbContext<TesseramentoDbContext>(o => o.UseNpgsql(cs));
builder.Services.AddDbContext<ApplicationDbContext>(o => o.UseNpgsql(cs));
builder.Services.AddIdentity<ApplicationUser, IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>();
builder.Services.AddAuthentication(o => { o.DefaultAuthenticateScheme = "Test"; o.DefaultChallengeScheme = "Test"; o.DefaultForbidScheme = "Test"; })
    .AddScheme<AuthenticationSchemeOptions, TestAuth>("Test", _ => { });
builder.Services.AddAuthorization(o => o.AddPolicy("Prenotazioni", p => p.RequireAuthenticatedUser()));
var clock = new TestClock();
var storage = new TestStorage(clock);
builder.Services.AddSingleton<TimeProvider>(clock);
builder.Services.AddSingleton<IPhotoStorage>(storage);
builder.Services.AddSingleton<PhotoWatermarker>();
builder.Services.AddScoped<PhotoAlbumService>();
await using var app = builder.Build();
app.Urls.Add("http://127.0.0.1:55443");
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllerRoute("default", "{controller}/{action=Index}/{id?}");
app.MapGet("/fixtures/photo.jpg", () => Results.File(storage.Preview, "image/jpeg"));

async Task<string> RenderTable(HttpContext http)
{
    var services = http.RequestServices;
    http.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, "Admin") }, "Test"));
    var context = new ActionContext(http, new RouteData(), new ActionDescriptor());
    var data = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary())
    { Model = await services.GetRequiredService<TesseramentoDbContext>().Partite.OrderBy(p => p.Id).ToListAsync() };
    data["StaffList"] = new List<string> { "Simone", "Alberto", "Federico", "Enrico" };
    var view = services.GetRequiredService<IRazorViewEngine>().GetView(null, "/Views/Partite/_PartiteTable.cshtml", false);
    if (!view.Success) throw new Exception("Booking view not found");
    using var writer = new StringWriter();
    await view.View.RenderAsync(new ViewContext(context, view.View, data,
        new TempDataDictionary(http, services.GetRequiredService<ITempDataProvider>()), writer, new HtmlHelperOptions()));
    return "<!doctype html><html lang='it'><head><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'><link rel='stylesheet' href='https://cdn.jsdelivr.net/npm/bootstrap@5.3.0/dist/css/bootstrap.min.css'><link rel='stylesheet' href='/css/site.css'><link rel='stylesheet' href='/css/partite-responsive.css'></head><body><main id='partite-page' style='padding:20px'>" + writer + "</main></body></html>";
}
app.MapGet("/preview/table", (Func<HttpContext, Task<IResult>>)(async http => Results.Content(await RenderTable(http), "text/html")));

try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<TesseramentoDbContext>();
    await db.Database.EnsureCreatedAsync();
    var game = new Partita { Data = DateTime.SpecifyKind(DateTime.Today, DateTimeKind.Utc), Tipo = "Adulti", Durata = 1.5,
        NumeroPartecipanti = 8, Staff1 = "Simone", Staff2 = "Alberto", Reperibile = "Bosax", Caparra = 30,
        NomeRiferimento = "Test", PrefissoTelefonoRiferimento = "+39", TelefonoRiferimento = "3330000000" };
    var other = new Partita { Data = game.Data, Tipo = "Kids", Durata = 1, NumeroPartecipanti = 10, Caparra = 25, CaparraConfermata = true };
    db.Partite.AddRange(game, other);
    await db.SaveChangesAsync();
    var albums = scope.ServiceProvider.GetRequiredService<PhotoAlbumService>();
    var album = await albums.GetOrCreate(game.Id);
    Check(album.Token == (await albums.GetOrCreate(game.Id)).Token, "Album token changed");
    var otherAlbum = await albums.GetOrCreate(other.Id);
    Check(album.Token != otherAlbum.Token, "Albums share token");
    var parallel = await Task.WhenAll(Enumerable.Range(0, 6).Select(async _ =>
    {
        using var isolated = app.Services.CreateScope();
        return (await isolated.ServiceProvider.GetRequiredService<PhotoAlbumService>().GetOrCreate(game.Id)).Token;
    }));
    Check(parallel.All(t => t == album.Token), "Concurrent token lookup changed link");

    var marker = scope.ServiceProvider.GetRequiredService<PhotoWatermarker>();
    if (args.Length > 0 && File.Exists(args[0]))
    {
        using var heif = File.OpenRead(args[0]);
        var converted = marker.Process(heif);
        using var decodedHeif = SKBitmap.Decode(converted);
        Check(decodedHeif != null && decodedHeif.Width <= PhotoWatermarker.MaxOutputSide, "HEIF conversion failed");
        Check(converted[0] == 0xff && converted[1] == 0xd8, "HEIF output is not JPEG");
        Console.WriteLine("PASS: real HEIC/HEIF decoded, converted and watermarked.");
    }
    var movieHeif = new byte[24];
    System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(movieHeif, 16);
    "ftypheic"u8.CopyTo(movieHeif.AsSpan(4));
    System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(movieHeif.AsSpan(16), 8);
    "moov"u8.CopyTo(movieHeif.AsSpan(20));
    try { marker.Process(new MemoryStream(movieHeif)); throw new Exception("Accepted HEIF movie track"); }
    catch (InvalidDataException) { }
    using var bitmap = new SKBitmap(3000, 2000);
    bitmap.Erase(SKColors.CornflowerBlue);
    using var image = SKImage.FromBitmap(bitmap);
    using var png = image.Encode(SKEncodedImageFormat.Png, 100);
    var source = png.ToArray();
    using var input = new MemoryStream(source);
    var jpeg = marker.Process(input);
    storage.Preview = jpeg;
    using var result = SKBitmap.Decode(jpeg);
    Check(result.Width == 2200 && result.Height <= 2200, "Resize failed");
    Check(result.GetPixel(result.Width - 100, result.Height - 100) != result.GetPixel(100, 100), "Watermark missing");
    Check(jpeg[0] == 0xff && jpeg[1] == 0xd8, "Not JPEG");
    using (var orientationBitmap = new SKBitmap(400, 240))
    {
        using var canvas = new SKCanvas(orientationBitmap);
        using var paint = new SKPaint();
        var colors = new[] { SKColors.Red, SKColors.Lime, SKColors.Blue, SKColors.Yellow };
        for (var i = 0; i < 4; i++) { paint.Color = colors[i]; canvas.DrawRect(i % 2 * 200, i / 2 * 120, 200, 120, paint); }
        using var orientationImage = SKImage.FromBitmap(orientationBitmap);
        using var raw = orientationImage.Encode(SKEncodedImageFormat.Jpeg, 95);
        var rawBytes = raw.ToArray();
        var expected = new[] { 0, 1, 3, 2, 0, 2, 3, 1 };
        for (byte orientation = 1; orientation <= 8; orientation++)
        {
            byte[] exif = [0xff,0xe1,0,34,69,120,105,102,0,0,73,73,42,0,8,0,0,0,1,0,18,1,3,0,1,0,0,0,orientation,0,0,0,0,0,0,0];
            var withExif = rawBytes.Take(2).Concat(exif).Concat(rawBytes.Skip(2)).ToArray();
            var normalized = marker.Process(new MemoryStream(withExif));
            using var oriented = SKBitmap.Decode(normalized);
            Check(oriented.Width == (orientation >= 5 ? 240 : 400), "EXIF dimensions wrong");
            var pixel = oriented.GetPixel(20,20);
            var wanted = colors[expected[orientation - 1]];
            Check(Math.Abs(pixel.Red-wanted.Red)<20 && Math.Abs(pixel.Green-wanted.Green)<20 && Math.Abs(pixel.Blue-wanted.Blue)<20, "EXIF orientation wrong: " + orientation);
            Check(!System.Text.Encoding.Latin1.GetString(normalized).Contains("Exif"), "Source EXIF retained");
        }
    }
    try { marker.Process(new MemoryStream("<svg></svg>"u8.ToArray())); throw new Exception("Accepted non-photo"); }
    catch (InvalidDataException) { }
    Directory.CreateDirectory(".codex-build/photo-fixtures");
    await File.WriteAllBytesAsync(".codex-build/photo-fixtures/watermarked.jpg", jpeg);
    await File.WriteAllBytesAsync(".codex-build/photo-fixtures/source.png", source);
    Console.WriteLine("PASS: logo, resize, all 8 EXIF orientations, metadata removal, JPEG and unsupported-file rejection.");

    var file = new FormFile(new MemoryStream(source), 0, source.Length, "foto", "test.png");
    await albums.Upload(album.Token, file, default);
    var fullAlbum = Guid.NewGuid();
    for (var i = 0; i < PhotoAlbumService.MaxPhotos; i++) await storage.Put(fullAlbum, Guid.NewGuid(), jpeg, default);
    try { await albums.Upload(fullAlbum, file, default); throw new Exception("Album cap bypassed"); }
    catch (InvalidDataException) { }
    foreach (var p in await storage.List(fullAlbum, default)) await storage.Delete(fullAlbum, p.Id, default);
    try { await albums.Upload(album.Token, new FormFile(Stream.Null, 0, PhotoWatermarker.MaxFileBytes + 1, "foto", "large.jpg"), default); throw new Exception("File cap bypassed"); }
    catch (InvalidDataException) { }
    var uploaded = (await storage.List(album.Token, default)).Single();
    Check(uploaded.IsAvailable(clock.Now.AddDays(7).AddTicks(-1)) && !uploaded.IsAvailable(clock.Now.AddDays(7)), "Seven day boundary failed");
    using var s3 = new FakeS3 { Uploaded = clock.Now.UtcDateTime };
    using var r2 = new R2PhotoStorage(s3, "test", clock);
    Check((await r2.List(album.Token, default)).Count == 1, "R2 list failed");
    Check((await r2.DownloadUrl(album.Token, uploaded.Id, true, default))!.Contains("X-Amz-Expires="), "Signed URL failed");
    s3.Uploaded = clock.Now.AddDays(-7).UtcDateTime;
    Check((await r2.List(album.Token, default)).Count == 0 && await r2.DownloadUrl(album.Token, uploaded.Id, false, default) == null,
        "Expired R2 object accessible before physical deletion");
    Console.WriteLine("PASS: expiry boundary, R2 listing and signed download denial for expired files.");

    var controller = new PartiteController(db, null!, null!, null!, null!, new PricingCatalogService(db), null!, null!, albums)
    { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
    controller.Request.Scheme = "http";
    controller.Request.Host = new HostString("example.test");
    foreach (var language in new[] { "ITA", "ENG" })
    {
        game.Nazionalita = language;
        await db.SaveChangesAsync();
        var message = (JsonResult)await controller.GeneraMessaggioPrenotazione(game.Id);
        var json = JsonSerializer.SerializeToElement(message.Value);
        var link = $"https://example.test/Foto/Album/{album.Token:N}";
        Check(json.GetProperty("messaggio").GetString()!.Contains(link) && json.GetProperty("messaggioWhatsapp").GetString()!.Contains(link), "Summary missing stable HTTPS album link");
    }
    game.Nazionalita = "ITA";
    await db.SaveChangesAsync();
    Console.WriteLine("PASS: Italian/English HTML and WhatsApp summary links, stable token across requests.");

    if (args.Contains("--preview"))
        app.MapGet("/preview/login", (HttpContext http) =>
        {
            http.Response.Cookies.Append("PhotoTestRole", "Staff");
            return Results.Redirect($"/Foto/Gestisci/{game.Id}");
        });
    await app.StartAsync();
    using var anonymous = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = new Uri("http://127.0.0.1:55443") };
    using var staff = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new CookieContainer() }) { BaseAddress = anonymous.BaseAddress };
    staff.DefaultRequestHeaders.Add("X-Test-Role", "Staff");
    Check((await anonymous.GetAsync($"/Foto/Messaggio/{game.Id}")).StatusCode == HttpStatusCode.Unauthorized, "Anonymous photo message access");
    game.Nazionalita = "ITA"; await db.SaveChangesAsync();
    var photoMessageIt = await staff.GetStringAsync($"/Foto/Messaggio/{game.Id}");
    Check(photoMessageIt.Contains(album.Token.ToString("N")) && photoMessageIt.Contains("7 giorni"), "Italian photo message missing link or expiry");
    game.Nazionalita = "ENG"; await db.SaveChangesAsync();
    var photoMessageEn = await staff.GetStringAsync($"/Foto/Messaggio/{game.Id}");
    Check(photoMessageEn.Contains("7 days") && photoMessageEn.Contains("download"), "English photo message missing expiry");
    Check((await anonymous.GetAsync($"/Foto/Gestisci/{game.Id}")).StatusCode == HttpStatusCode.Unauthorized, "Anonymous staff access");
    Check((await anonymous.PostAsync($"/Foto/Carica/{game.Id}", new StringContent(""))).StatusCode == HttpStatusCode.Unauthorized, "Anonymous upload access");
    var publicResponse = await anonymous.GetAsync($"/Foto/Album/{album.Token:N}");
    Check(publicResponse.IsSuccessStatusCode, "Public album inaccessible");
    var html = await publicResponse.Content.ReadAsStringAsync();
    Check(!html.Contains("album-upload") && !html.Contains("3330000000") && !html.Contains("album-delete"), "Public data leak");
    Check(publicResponse.Headers.CacheControl!.NoStore && publicResponse.Headers.Contains("Referrer-Policy"), "Missing privacy headers");
    Check((await anonymous.GetAsync($"/Foto/Album/{Guid.NewGuid():N}")).StatusCode == HttpStatusCode.NotFound, "Forged token accepted");
    Check((await anonymous.GetAsync($"/Foto/Immagine/{otherAlbum.Token:N}/{uploaded.Id}")).StatusCode == HttpStatusCode.NotFound, "Cross-album photo leak");
    Check((await anonymous.GetAsync($"/Foto/Immagine/{album.Token:N}/{uploaded.Id}")).StatusCode == HttpStatusCode.Redirect, "Download unavailable");
    var manageResponse = await staff.GetAsync($"/Foto/Gestisci/{game.Id}");
    Check(manageResponse.IsSuccessStatusCode, "Staff manage unavailable: " + await manageResponse.Content.ReadAsStringAsync());
    var manageHtml = await manageResponse.Content.ReadAsStringAsync();
    var token = WebUtility.HtmlDecode(Regex.Match(manageHtml, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
    Check(token.Length > 0, "No antiforgery token");
    Check((await staff.PostAsync("/Foto/Carica", new MultipartFormDataContent())).StatusCode == HttpStatusCode.BadRequest, "Missing antiforgery accepted");
    using var form = new MultipartFormDataContent();
    form.Add(new StringContent(token), "__RequestVerificationToken"); form.Add(new StringContent(game.Id.ToString()), "id");
    form.Add(new StringContent("true"), "autorizzato");
    var binary = new ByteArrayContent(source); binary.Headers.ContentType = new MediaTypeHeaderValue("image/png"); form.Add(binary, "foto", "test.png");
    var post = await staff.PostAsync("/Foto/Carica", form);
    Check(post.IsSuccessStatusCode, "Staff upload failed: " + await post.Content.ReadAsStringAsync());
    Check(storage.Count == 2, "Upload not stored");
    using var videoForm = new MultipartFormDataContent();
    videoForm.Add(new StringContent(token), "__RequestVerificationToken");
    videoForm.Add(new StringContent(game.Id.ToString()), "id");
    videoForm.Add(new StringContent("true"), "autorizzato");
    var disguisedVideo = new ByteArrayContent(new byte[] { 0, 0, 0, 24, 102, 116, 121, 112, 109, 112, 52, 50, 0, 0, 0, 0 });
    disguisedVideo.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
    videoForm.Add(disguisedVideo, "foto", "video.jpg");
    Check((await staff.PostAsync("/Foto/Carica", videoForm)).StatusCode == HttpStatusCode.BadRequest && storage.Count == 2, "Disguised video accepted");
    using var deletion = new FormUrlEncodedContent(new Dictionary<string,string> { ["id"] = game.Id.ToString(), ["photo"] = uploaded.Id.ToString(), ["__RequestVerificationToken"] = token });
    Check((await staff.PostAsync("/Foto/Elimina", deletion)).IsSuccessStatusCode && storage.Count == 1, "Staff delete failed");
    Check((await anonymous.GetAsync($"/Foto/Immagine/{album.Token:N}/{uploaded.Id}")).StatusCode == HttpStatusCode.NotFound, "Deleted photo available");
    game.IsDeleted = true; await db.SaveChangesAsync();
    Check((await anonymous.GetAsync($"/Foto/Album/{album.Token:N}")).StatusCode == HttpStatusCode.NotFound, "Cancelled album public");
    game.IsDeleted = false; await db.SaveChangesAsync();
    staff.DefaultRequestHeaders.Remove("X-Test-Role"); staff.DefaultRequestHeaders.Add("X-Test-Role", "Viewer");
    Check((await staff.GetAsync($"/Foto/Gestisci/{game.Id}")).StatusCode == HttpStatusCode.Forbidden, "Other role allowed");
    Console.WriteLine("PASS: staff authorization, anonymous denial, antiforgery, real multipart upload, album isolation, private headers.");

    if (args.Contains("--preview"))
    {
        Console.WriteLine($"Preview: http://127.0.0.1:55443/Foto/Album/{album.Token:N} | /preview/table");
        Console.WriteLine($"Manage with X-Test-Role: Staff at /Foto/Gestisci/{game.Id}");
        try { await Task.Delay(Timeout.Infinite, app.Lifetime.ApplicationStopping); }
        catch (OperationCanceledException) { }
    }
    await app.StopAsync();
}
finally
{
    NpgsqlConnection.ClearAllPools();
    await new NpgsqlCommand($"DROP DATABASE {database} WITH (FORCE)", admin).ExecuteNonQueryAsync();
}

sealed class TestClock : TimeProvider
{
    public DateTimeOffset Now = DateTimeOffset.UtcNow;
    public override DateTimeOffset GetUtcNow() => Now;
}
sealed class TestStorage(TestClock clock) : IPhotoStorage
{
    private readonly Dictionary<(Guid, Guid), AlbumPhoto> photos = new();
    public byte[] Preview = [];
    public int Count => photos.Count;
    public bool Configured => true;
    public Task<IReadOnlyList<AlbumPhoto>> List(Guid album, CancellationToken ct) => Task.FromResult<IReadOnlyList<AlbumPhoto>>(photos.Where(p => p.Key.Item1 == album && p.Value.IsAvailable(clock.Now)).Select(p => p.Value).ToList());
    public Task Put(Guid album, Guid id, byte[] jpeg, CancellationToken ct) { photos[(album,id)] = new(id, clock.Now, jpeg.Length); return Task.CompletedTask; }
    public Task Delete(Guid album, Guid id, CancellationToken ct) { photos.Remove((album,id)); return Task.CompletedTask; }
    public Task<string?> DownloadUrl(Guid album, Guid id, bool attachment, CancellationToken ct) => Task.FromResult(photos.TryGetValue((album,id), out var photo) && photo.IsAvailable(clock.Now) ? "/fixtures/photo.jpg" : null);
}
sealed class FakeS3() : AmazonS3Client(new BasicAWSCredentials("test", "test"), new AmazonS3Config { ServiceURL = "https://test.eu.r2.cloudflarestorage.com", AuthenticationRegion = "auto", ForcePathStyle = true })
{
    public DateTime Uploaded;
    public override Task<GetObjectMetadataResponse> GetObjectMetadataAsync(string bucketName, string key, CancellationToken ct = default) => Task.FromResult(new GetObjectMetadataResponse { LastModified = Uploaded });
    public override Task<ListObjectsV2Response> ListObjectsV2Async(ListObjectsV2Request request, CancellationToken ct = default) => Task.FromResult(new ListObjectsV2Response { S3Objects = new List<S3Object> { new() { Key = request.Prefix + Guid.NewGuid().ToString("N") + ".jpg", LastModified = Uploaded, Size = 100 } } });
}
sealed class TestAuth(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var role = Request.Headers["X-Test-Role"].ToString();
        if (string.IsNullOrEmpty(role)) role = Request.Cookies["PhotoTestRole"] ?? "";
        if (string.IsNullOrEmpty(role)) return Task.FromResult(AuthenticateResult.NoResult());
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "Test"), new Claim(ClaimTypes.Role, role) }, "Test"));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, "Test")));
    }
}
