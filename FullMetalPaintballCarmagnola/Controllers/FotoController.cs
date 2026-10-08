using Full_Metal_Paintball_Carmagnola.Models;
using Full_Metal_Paintball_Carmagnola.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using System.IO.Compression;

namespace Full_Metal_Paintball_Carmagnola.Controllers;

[Authorize(Policy = "Prenotazioni")]
[Authorize(Roles = "Admin,Staff")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class FotoController(TesseramentoDbContext db, PhotoAlbumService albums, IPhotoStorage storage,
    ILogger<FotoController> logger) : Controller
{
    private static readonly SemaphoreSlim ZipGate = new(1, 1);
    public static string ReviewMessage(bool english) => english
        ? "Your opinion means a lot to us and helps us improve! If you would like to tell us about your experience, please leave a review:\nhttps://g.page/r/CSY7ElrZDaxMEBM/review\nThank you for your time!"
        : "Il tuo parere per noi conta molto e ci aiuta a crescere! Se ti va di raccontare la tua esperienza al campo, ci farebbe piacere ricevere una recensione:\nhttps://g.page/r/CSY7ElrZDaxMEBM/review\nGrazie per il tempo che vorrai dedicarci!";

    [HttpGet]
    public async Task<IActionResult> Recensione(int id, CancellationToken ct)
    {
        var game = await db.Partite.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, ct);
        return game == null ? NotFound() : Ok(new { message = ReviewMessage(string.Equals(game.Nazionalita, "ENG", StringComparison.OrdinalIgnoreCase)) });
    }

    [AllowAnonymous]
    [HttpGet("Foto/File/{token:guid}/{photo:guid}")]
    public async Task<IActionResult> FileFoto(Guid token, Guid photo, CancellationToken ct)
    {
        if (!await PublicTokenExists(token, ct)) return NotFound();
        try
        {
            var bytes = await storage.Read(token, photo, ct);
            return bytes == null ? NotFound() : File(bytes, "image/jpeg");
        }
        catch (Exception e) when (e is not OperationCanceledException)
        { LogStorageError(e); return StatusCode(503); }
    }

    [AllowAnonymous]
    [HttpGet("Foto/ScaricaTutte/{token:guid}")]
    public async Task<IActionResult> ScaricaTutte(Guid token, CancellationToken ct)
    {
        if (!await PublicTokenExists(token, ct)) return NotFound();
        if (!await ZipGate.WaitAsync(TimeSpan.FromSeconds(2), ct)) return StatusCode(429, "Download in preparazione. Riprova tra poco.");
        FileStream? file = null;
        try
        {
            var photos = await storage.List(token, ct);
            if (photos.Count == 0) return NotFound("Nessuna foto disponibile.");
            file = new FileStream(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".zip"), FileMode.CreateNew,
                FileAccess.ReadWrite, FileShare.None, 81920, FileOptions.Asynchronous | FileOptions.DeleteOnClose);
            var count = 0;
            using (var zip = new ZipArchive(file, ZipArchiveMode.Create, true))
            {
                foreach (var photo in photos.Take(PhotoAlbumService.MaxPhotos))
                {
                    var bytes = await storage.Read(token, photo.Id, ct);
                    if (bytes == null) continue;
                    await using var entry = zip.CreateEntry($"FullMetal-{++count:000}.jpg", CompressionLevel.NoCompression).Open();
                    await entry.WriteAsync(bytes, ct);
                }
            }
            if (count == 0) { await file.DisposeAsync(); return NotFound("Le foto sono scadute."); }
            file.Position = 0;
            return File(file, "application/zip", "FullMetal-Foto.zip");
        }
        catch (Exception e)
        {
            if (file != null) await file.DisposeAsync();
            if (e is OperationCanceledException) throw;
            LogStorageError(e); return StatusCode(503, "Download non riuscito. Riprova tra poco.");
        }
        finally { ZipGate.Release(); }
    }
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        Response.Headers["X-Robots-Tag"] = "noindex, nofollow, noarchive";
        Response.Headers["Referrer-Policy"] = "no-referrer";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        base.OnActionExecuting(context);
    }

    [HttpGet]
    public async Task<IActionResult> Gestisci(int id, CancellationToken ct)
    {
        var game = await db.Partite.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);
        if (game == null) return NotFound();
        var album = await albums.GetOrCreate(id, ct);
        var model = await Model(album, game, true, ct);
        return View("Album", model);
    }

    [AllowAnonymous]
    [HttpGet("Foto/Album/{token:guid}")]
    public async Task<IActionResult> Album(Guid token, CancellationToken ct)
    {
        if (token == Guid.Empty) return NotFound();
        var album = await db.PhotoAlbums.AsNoTracking().Include(a => a.Partita)
            .FirstOrDefaultAsync(a => a.Token == token && !a.Partita.IsDeleted, ct);
        if (album != null) return View(await Model(album, album.Partita, false, ct));
        var tournament = await db.Tornei.AsNoTracking().FirstOrDefaultAsync(t => t.PhotoToken == token, ct);
        return tournament == null ? NotFound() : View(await TournamentModel(tournament, false, ct));
    }

    [HttpGet]
    public async Task<IActionResult> Messaggio(int id, CancellationToken ct)
    {
        var game = await db.Partite.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, ct);
        if (game == null) return NotFound();
        var album = await albums.GetOrCreate(id, ct);
        return Ok(new { message = PhotoMessage(game, PhotoAlbumService.PublicUrl(Request, album.Token)) });
    }

    private static string PhotoMessage(Partita game, string url) => (
        string.Equals(game.Nazionalita, "ENG", StringComparison.OrdinalIgnoreCase)
            ? $"Hi! Here is the link to download your game photos:\n{url}\n\nEach photo is available for 7 days from its upload, then it is automatically removed. Check the expiry shown below each photo and download it in time. If the album is empty, please check again after upload.\nShare the link only with your group. Thank you!"
            : $"Ciao! Ecco il link per scaricare le foto della vostra partita:\n{url}\n\nOgni foto resta disponibile per 7 giorni dal suo caricamento, poi viene rimossa automaticamente. Controlla la scadenza riportata sotto ogni foto e scaricala in tempo. Se l'album risulta vuoto, riprova dopo il caricamento.\nCondividi il link solo con il tuo gruppo. Grazie!")
            + "\n\n" + ReviewMessage(string.Equals(game.Nazionalita, "ENG", StringComparison.OrdinalIgnoreCase));

    [AllowAnonymous]
    [HttpGet("Foto/Immagine/{token:guid}/{photo:guid}")]
    public async Task<IActionResult> Immagine(Guid token, Guid photo, bool download, CancellationToken ct)
    {
        if (!await PublicTokenExists(token, ct)) return NotFound();
        try
        {
            var url = await storage.DownloadUrl(token, photo, download, ct);
            return url == null ? NotFound() : Redirect(url);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            LogStorageError(e);
            return StatusCode(503, "Foto temporaneamente non disponibile. Riprova tra poco.");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(17 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 16 * 1024 * 1024)]
    public async Task<IActionResult> Carica(int id, IFormFile? foto, bool autorizzato, CancellationToken ct)
    {
        if (foto == null || !autorizzato) return BadRequest(new { message = "Seleziona una foto e conferma di poterla condividere con il gruppo." });
        if (!storage.Configured) return StatusCode(503, new { message = "Archivio foto non configurato. Contatta un amministratore." });
        if (!await db.Partite.AnyAsync(p => p.Id == id && !p.IsDeleted, ct)) return NotFound();
        try
        {
            var album = await albums.GetOrCreate(id, ct);
            await albums.Upload(album.Token, foto, ct);
            return Ok(new { message = "Foto caricata con logo." });
        }
        catch (InvalidDataException e) { return BadRequest(new { message = e.Message }); }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            LogStorageError(e);
            return StatusCode(503, new { message = "Caricamento non riuscito. Verifica l'album prima di riprovare; se il problema persiste contatta un amministratore." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Elimina(int id, Guid photo, CancellationToken ct)
    {
        var album = await db.PhotoAlbums.AsNoTracking().FirstOrDefaultAsync(a => a.PartitaId == id, ct);
        if (album == null) return NotFound();
        try
        {
            await storage.Delete(album.Token, photo, ct);
            return Ok();
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            LogStorageError(e);
            return StatusCode(503, new { message = "Eliminazione non riuscita. Riprova tra poco." });
        }
    }

    private async Task<PhotoAlbumViewModel> Model(PhotoAlbum album, Partita game, bool manage, CancellationToken ct)
    {
        var english = !manage && string.Equals(game.Nazionalita, "ENG", StringComparison.OrdinalIgnoreCase);
        var model = new PhotoAlbumViewModel
        {
            PartitaId = game.Id, Token = album.Token, Date = game.Data, Manage = manage, English = english,
            PublicUrl = PhotoAlbumService.PublicUrl(Request, album.Token)
        };
        if (manage)
        {
            var prefix = new string((game.PrefissoTelefonoRiferimento ?? "").Where(char.IsAsciiDigit).ToArray());
            var number = new string((game.TelefonoRiferimento ?? "").Where(char.IsAsciiDigit).ToArray());
            if (prefix.Length > 0 && number.Length > 0)
            {
                var message = PhotoMessage(game, model.PublicUrl);
                model.WhatsappUrl = $"https://wa.me/{prefix}{number}?text={Uri.EscapeDataString(message)}";
            }
        }
        await LoadPhotos(model, ct);
        if (manage && game.IsDeleted) model.Error = "La partita e' cancellata: caricamento e accesso pubblico disabilitati.";
        return model;
    }

    private async Task<PhotoAlbumViewModel> TournamentModel(Torneo tournament, bool manage, CancellationToken ct)
    {
        var model = FotoTorneoController.CreateModel(tournament, Request, manage);
        await LoadPhotos(model, ct);
        return model;
    }

    private async Task<bool> PublicTokenExists(Guid token, CancellationToken ct) => token != Guid.Empty &&
        (await db.PhotoAlbums.AnyAsync(a => a.Token == token && !a.Partita.IsDeleted, ct) ||
         await db.Tornei.AnyAsync(t => t.PhotoToken == token, ct));

    private async Task LoadPhotos(PhotoAlbumViewModel model, CancellationToken ct)
    {
        try { model.Photos = await storage.List(model.Token, ct); }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            LogStorageError(e);
            Response.StatusCode = 503;
            model.Error = model.English ? "The album is temporarily unavailable. Please try again later."
                : model.Manage ? "Archivio foto non disponibile. Verifica le variabili R2 su Render, endpoint EU e permessi sul bucket."
                : "Album temporaneamente non disponibile. Riprova tra poco.";
        }
    }

    private void LogStorageError(Exception e)
    {
        // Never include exception messages, signed URLs or credentials in application logs.
        logger.LogError("Photo storage failure: {Type}, code {Code}", e.GetType().Name,
            e is Amazon.S3.AmazonS3Exception s3 ? s3.ErrorCode : "internal");
    }
}
