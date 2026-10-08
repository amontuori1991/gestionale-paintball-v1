using System.IO.Compression;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Full_Metal_Paintball_Carmagnola.Controllers;
using Full_Metal_Paintball_Carmagnola.Models;
using Microsoft.EntityFrameworkCore;

internal static class TournamentPhotoChecks
{
    public static async Task Run(TesseramentoDbContext db, HttpClient staff, HttpClient anonymous,
        TestStorage storage, TestClock clock, byte[] source)
    {
        static void Check(bool condition, string message)
        {
            if (!condition) throw new Exception("Tournament photos: " + message);
        }

        var gameCount = await db.Partite.CountAsync();
        var albumCount = await db.PhotoAlbums.CountAsync();
        var tournament = new Torneo { Nome = "Cup <photo>", Data = DateTime.UtcNow.Date };
        var other = new Torneo { Nome = "Other cup", Data = tournament.Data };
        db.Tornei.AddRange(tournament, other);
        await db.SaveChangesAsync();
        var publicUrl = $"/Foto/Album/{tournament.PhotoToken:N}";
        var html = await anonymous.GetStringAsync(publicUrl);
        Check(html.Contains("Cup &lt;photo&gt;") && !html.Contains("album-upload"), "public name escaping / controls");
        Check((await anonymous.GetAsync($"/FotoTorneo/Gestisci/{tournament.Id}")).StatusCode == HttpStatusCode.Unauthorized, "anonymous manager");
        using var viewer = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = anonymous.BaseAddress };
        viewer.DefaultRequestHeaders.Add("X-Test-Role", "Viewer");
        Check((await viewer.GetAsync($"/FotoTorneo/Gestisci/{tournament.Id}")).StatusCode == HttpStatusCode.Forbidden, "viewer manager");
        Check((await viewer.PostAsync("/FotoTorneo/Elimina", new FormUrlEncodedContent([]))).StatusCode == HttpStatusCode.Forbidden, "viewer mutation");
        Check((await anonymous.PostAsync("/FotoTorneo/Carica", new MultipartFormDataContent())).StatusCode == HttpStatusCode.Unauthorized, "anonymous mutation");
        Check((await staff.PostAsync("/FotoTorneo/Carica", new MultipartFormDataContent())).StatusCode == HttpStatusCode.BadRequest, "upload CSRF");
        Check((await staff.PostAsync("/FotoTorneo/Elimina", new FormUrlEncodedContent([]))).StatusCode == HttpStatusCode.BadRequest, "delete CSRF");

        foreach (var role in new[] { "Staff", "Admin" })
        {
            using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new CookieContainer() }) { BaseAddress = anonymous.BaseAddress };
            client.DefaultRequestHeaders.Add("X-Test-Role", role);
            html = await client.GetStringAsync($"/FotoTorneo/Gestisci/{tournament.Id}");
            Check(html.Contains("/FotoTorneo/Carica") && html.Contains("https://wa.me/?text=") && html.Contains(publicUrl), role + " management controls");
            var csrf = WebUtility.HtmlDecode(Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
            Check(csrf.Length > 0, "missing form token");
            async Task<HttpResponseMessage> Upload(bool consent, int id, byte[] bytes)
            {
                using var form = new MultipartFormDataContent();
                form.Add(new StringContent(csrf), "__RequestVerificationToken");
                form.Add(new StringContent(id.ToString()), "id");
                form.Add(new StringContent(consent ? "true" : "false"), "autorizzato");
                form.Add(new ByteArrayContent(bytes), "foto", "test.png");
                return await client.PostAsync("/FotoTorneo/Carica", form);
            }
            Check((await Upload(false, tournament.Id, source)).StatusCode == HttpStatusCode.BadRequest, "consent bypass");
            Check((await Upload(true, int.MaxValue, source)).StatusCode == HttpStatusCode.NotFound, "unknown owner upload");
            Check((await Upload(true, tournament.Id, "not an image"u8.ToArray())).StatusCode == HttpStatusCode.BadRequest, "invalid image accepted");
            Check((await Upload(true, tournament.Id, source)).IsSuccessStatusCode, role + " upload");
            var photo = (await storage.List(tournament.PhotoToken, default)).Single();
            Check((await anonymous.GetAsync($"/Foto/Immagine/{tournament.PhotoToken}/{photo.Id}")).StatusCode == HttpStatusCode.Redirect, "image redirect");
            var file = await anonymous.GetAsync($"/Foto/File/{tournament.PhotoToken}/{photo.Id}");
            Check(file.IsSuccessStatusCode && file.Content.Headers.ContentType?.MediaType == "image/jpeg", "inline file");
            Check(file.Headers.CacheControl?.NoStore == true && file.Headers.Contains("X-Robots-Tag"), "privacy headers");
            Check((await anonymous.GetAsync($"/Foto/File/{other.PhotoToken}/{photo.Id}")).StatusCode == HttpStatusCode.NotFound, "cross-tournament read");
            var bookingToken = await db.PhotoAlbums.Select(a => a.Token).FirstAsync();
            Check((await anonymous.GetAsync($"/Foto/File/{bookingToken}/{photo.Id}")).StatusCode == HttpStatusCode.NotFound, "cross-booking read");
            var zip = await anonymous.GetByteArrayAsync($"/Foto/ScaricaTutte/{tournament.PhotoToken}");
            using (var archive = new ZipArchive(new MemoryStream(zip)))
                Check(archive.Entries.Count == 1, "ZIP content");
            var message = JsonDocument.Parse(await client.GetStringAsync($"/FotoTorneo/Messaggio/{tournament.Id}")).RootElement.GetProperty("message").GetString()!;
            Check(message.Contains(tournament.Nome) && message.Contains(publicUrl) && message.Contains("7 giorni") && message.Contains(FotoController.ReviewMessage(false)), "WhatsApp photo contract");
            var review = JsonDocument.Parse(await client.GetStringAsync($"/FotoTorneo/Recensione/{tournament.Id}")).RootElement.GetProperty("message").GetString();
            Check(review == FotoController.ReviewMessage(false), "review contract");
            var now = clock.Now;
            clock.Now = photo.ExpiresAt;
            Check(!(await anonymous.GetStringAsync(publicUrl)).Contains("album-photo-info"), "expired photo in gallery");
            foreach (var route in new[] { $"File/{tournament.PhotoToken}/{photo.Id}", $"Immagine/{tournament.PhotoToken}/{photo.Id}", $"ScaricaTutte/{tournament.PhotoToken}" })
                Check((await anonymous.GetAsync("/Foto/" + route)).StatusCode == HttpStatusCode.NotFound, "expiry: " + route);
            clock.Now = now;
            async Task<HttpResponseMessage> Delete(int id) => await client.PostAsync("/FotoTorneo/Elimina", new FormUrlEncodedContent(new Dictionary<string, string>
            { ["id"] = id.ToString(), ["photo"] = photo.Id.ToString(), ["__RequestVerificationToken"] = csrf }));
            Check((await Delete(other.Id)).IsSuccessStatusCode && (await storage.List(tournament.PhotoToken, default)).Count == 1, "cross-owner deletion");
            Check((await Delete(tournament.Id)).IsSuccessStatusCode && (await storage.List(tournament.PhotoToken, default)).Count == 0, role + " delete");
        }

        var orphan = Guid.NewGuid();
        await storage.Put(tournament.PhotoToken, orphan, source, default);
        db.Tornei.RemoveRange(tournament, other);
        await db.SaveChangesAsync();
        foreach (var route in new[] { $"Album/{tournament.PhotoToken}", $"File/{tournament.PhotoToken}/{orphan}", $"Immagine/{tournament.PhotoToken}/{orphan}", $"ScaricaTutte/{tournament.PhotoToken}" })
            Check((await anonymous.GetAsync("/Foto/" + route)).StatusCode == HttpStatusCode.NotFound, "deleted tournament public access: " + route);
        Check((await storage.List(tournament.PhotoToken, default)).Count == 1, "test must retain orphan storage");
        await storage.Delete(tournament.PhotoToken, orphan, default);
        Check(await db.Partite.CountAsync() == gameCount && await db.PhotoAlbums.CountAsync() == albumCount, "created fake booking/album");
        Check((await anonymous.GetAsync($"/Foto/Album/{Guid.Empty}")).StatusCode == HttpStatusCode.NotFound, "empty token");
        Console.WriteLine("PASS: tournament photos roles, CSRF, consent, watermark pipeline, shared gallery/files/ZIP, isolation, expiry, WhatsApp/review and deleted owner access.");
    }
}
