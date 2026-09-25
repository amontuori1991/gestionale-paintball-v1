using Full_Metal_Paintball_Carmagnola.Models;
using Full_Metal_Paintball_Carmagnola.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace Full_Metal_Paintball_Carmagnola.Controllers;

[Authorize(Policy = "Prenotazioni")]
[Authorize(Roles = "Admin,Staff")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class FotoController(TesseramentoDbContext db, PhotoAlbumService albums, IPhotoStorage storage,
    ILogger<FotoController> logger) : Controller
{
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
        var album = await db.PhotoAlbums.AsNoTracking().Include(a => a.Partita)
            .FirstOrDefaultAsync(a => a.Token == token && !a.Partita.IsDeleted, ct);
        if (album == null) return NotFound();
        return View(await Model(album, album.Partita, false, ct));
    }

    [AllowAnonymous]
    [HttpGet("Foto/Immagine/{token:guid}/{photo:guid}")]
    public async Task<IActionResult> Immagine(Guid token, Guid photo, bool download, CancellationToken ct)
    {
        if (!await db.PhotoAlbums.AnyAsync(a => a.Token == token && !a.Partita.IsDeleted, ct)) return NotFound();
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
                var message = string.Equals(game.Nazionalita, "ENG", StringComparison.OrdinalIgnoreCase)
                    ? $"Hi! Here is your photo album:\n{model.PublicUrl}\nPhotos will appear after upload and can be downloaded for 7 days from upload. Share this link only with your group!"
                    : $"Ciao! Qui trovi l'album della vostra partita:\n{model.PublicUrl}\nLe foto compariranno dopo il caricamento e saranno scaricabili per 7 giorni dal caricamento. Condividi il link solo con il tuo gruppo!";
                model.WhatsappUrl = $"https://wa.me/{prefix}{number}?text={Uri.EscapeDataString(message)}";
            }
        }
        try { model.Photos = await storage.List(album.Token, ct); }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            LogStorageError(e);
            Response.StatusCode = 503;
            model.Error = english ? "The album is temporarily unavailable. Please try again later."
                : manage ? "Archivio foto non disponibile. Verifica le variabili R2 su Render, endpoint EU e permessi sul bucket."
                : "Album temporaneamente non disponibile. Riprova tra poco.";
        }
        if (manage && game.IsDeleted) model.Error = "La partita e' cancellata: caricamento e accesso pubblico disabilitati.";
        return model;
    }

    private void LogStorageError(Exception e)
    {
        // Never include exception messages, signed URLs or credentials in application logs.
        logger.LogError("Photo storage failure: {Type}, code {Code}", e.GetType().Name,
            e is Amazon.S3.AmazonS3Exception s3 ? s3.ErrorCode : "internal");
    }
}
