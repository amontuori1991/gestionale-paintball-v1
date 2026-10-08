using Full_Metal_Paintball_Carmagnola.Models;
using Full_Metal_Paintball_Carmagnola.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace Full_Metal_Paintball_Carmagnola.Controllers;

[Authorize(Roles = "Admin,Staff")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class FotoTorneoController(TesseramentoDbContext db, PhotoAlbumService albums, IPhotoStorage storage,
    ILogger<FotoTorneoController> logger) : Controller
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
        var tournament = await db.Tornei.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct);
        if (tournament == null || tournament.PhotoToken == Guid.Empty) return NotFound();
        return View("~/Views/Foto/Album.cshtml", await TournamentModel(tournament, true, ct));
    }

    [HttpGet]
    public async Task<IActionResult> Messaggio(int id, CancellationToken ct)
    {
        var tournament = await db.Tornei.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct);
        if (tournament == null || tournament.PhotoToken == Guid.Empty) return NotFound();
        return Ok(new { message = PhotoMessage(tournament.Nome, PhotoAlbumService.PublicUrl(Request, tournament.PhotoToken)) });
    }

    [HttpGet]
    public async Task<IActionResult> Recensione(int id, CancellationToken ct) =>
        await db.Tornei.AnyAsync(t => t.Id == id, ct) ? Ok(new { message = FotoController.ReviewMessage(false) }) : NotFound();

    private static string PhotoMessage(string name, string url) =>
        $"Ciao! Ecco il link per scaricare le foto del torneo {name}:\n{url}\n\nOgni foto resta disponibile per 7 giorni dal suo caricamento, poi viene rimossa automaticamente. Controlla la scadenza riportata sotto ogni foto e scaricala in tempo. Se l'album risulta vuoto, riprova dopo il caricamento.\nCondividi il link solo con il tuo gruppo. Grazie!\n\n{FotoController.ReviewMessage(false)}";

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(17 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 16 * 1024 * 1024)]
    public async Task<IActionResult> Carica(int id, IFormFile? foto, bool autorizzato, CancellationToken ct)
    {
        if (foto == null || !autorizzato) return BadRequest(new { message = "Seleziona una foto e conferma di poterla condividere con il gruppo." });
        if (!storage.Configured) return StatusCode(503, new { message = "Archivio foto non configurato. Contatta un amministratore." });
        var token = await db.Tornei.Where(t => t.Id == id).Select(t => (Guid?)t.PhotoToken).FirstOrDefaultAsync(ct);
        if (token == null || token == Guid.Empty) return NotFound();
        try
        {
            await albums.Upload(token.Value, foto, ct);
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
        var token = await db.Tornei.Where(t => t.Id == id).Select(t => (Guid?)t.PhotoToken).FirstOrDefaultAsync(ct);
        if (token == null || token == Guid.Empty) return NotFound();
        return await DeletePhoto(token.Value, photo, ct);
    }

    private async Task<IActionResult> DeletePhoto(Guid token, Guid photo, CancellationToken ct)
    {
        try
        {
            await storage.Delete(token, photo, ct);
            return Ok();
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            LogStorageError(e);
            return StatusCode(503, new { message = "Eliminazione non riuscita. Riprova tra poco." });
        }
    }

    private async Task<PhotoAlbumViewModel> TournamentModel(Torneo tournament, bool manage, CancellationToken ct)
    {
        var model = CreateModel(tournament, Request, manage);
        await LoadPhotos(model, ct);
        return model;
    }

    internal static PhotoAlbumViewModel CreateModel(Torneo tournament, HttpRequest request, bool manage)
    {
        var model = new PhotoAlbumViewModel
        {
            TorneoId = tournament.Id, TournamentName = tournament.Nome, Token = tournament.PhotoToken,
            Date = tournament.Data, Manage = manage,
            PublicUrl = PhotoAlbumService.PublicUrl(request, tournament.PhotoToken)
        };
        if (manage)
            model.WhatsappUrl = $"https://wa.me/?text={Uri.EscapeDataString(PhotoMessage(tournament.Nome, model.PublicUrl))}";
        return model;
    }

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


