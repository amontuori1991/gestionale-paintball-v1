using Full_Metal_Paintball_Carmagnola.Models;
using Microsoft.EntityFrameworkCore;

namespace Full_Metal_Paintball_Carmagnola.Services;

public sealed class PhotoAlbumService(TesseramentoDbContext db, IPhotoStorage storage, PhotoWatermarker watermarker)
{
    public const int MaxPhotos = 60;
    // Bound native decoding memory and concurrent writes on this Render instance.
    private static readonly SemaphoreSlim UploadGate = new(1, 1);

    public async Task<PhotoAlbum> GetOrCreate(int partitaId, CancellationToken ct = default)
    {
        var album = await db.PhotoAlbums.FirstOrDefaultAsync(a => a.PartitaId == partitaId, ct);
        if (album != null) return album;
        album = new PhotoAlbum { PartitaId = partitaId };
        db.PhotoAlbums.Add(album);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            db.Entry(album).State = EntityState.Detached;
            var existing = await db.PhotoAlbums.FirstOrDefaultAsync(a => a.PartitaId == partitaId, ct);
            if (existing == null) throw;
            return existing;
        }
        return album;
    }

    public async Task Upload(Guid album, IFormFile file, CancellationToken ct)
    {
        if (file.Length <= 0 || file.Length > PhotoWatermarker.MaxFileBytes)
            throw new InvalidDataException("Ogni foto deve pesare al massimo 15 MB.");
        if (!await UploadGate.WaitAsync(TimeSpan.FromSeconds(5), ct))
            throw new InvalidDataException("Un caricamento e' gia' in corso. Riprova tra pochi secondi.");
        try
        {
            if ((await storage.List(album, ct)).Count >= MaxPhotos)
                throw new InvalidDataException($"Limite di {MaxPhotos} foto disponibili per partita raggiunto.");
            using var source = file.OpenReadStream();
            var jpeg = watermarker.Process(source);
            ct.ThrowIfCancellationRequested();
            await storage.Put(album, Guid.NewGuid(), jpeg, ct);
        }
        finally { UploadGate.Release(); }
    }

    public static string PublicUrl(HttpRequest request, Guid token) =>
        $"{(request.Host.Host is "localhost" or "127.0.0.1" ? request.Scheme : "https")}://{request.Host}{request.PathBase}/Foto/Album/{token:N}";
}
