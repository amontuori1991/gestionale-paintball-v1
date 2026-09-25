using System.ComponentModel.DataAnnotations;

namespace Full_Metal_Paintball_Carmagnola.Models;

public class PhotoAlbum
{
    [Key]
    public int PartitaId { get; set; }
    public Guid Token { get; set; } = Guid.NewGuid();
    public Partita Partita { get; set; } = null!;
}

public record AlbumPhoto(Guid Id, DateTimeOffset UploadedAt, long Size)
{
    public DateTimeOffset ExpiresAt => UploadedAt.AddDays(7);
    public bool IsAvailable(DateTimeOffset now) => now < ExpiresAt;
}

public class PhotoAlbumViewModel
{
    public int PartitaId { get; set; }
    public Guid Token { get; set; }
    public bool Manage { get; set; }
    public bool English { get; set; }
    public DateTime Date { get; set; }
    public string PublicUrl { get; set; } = "";
    public string? WhatsappUrl { get; set; }
    public string? Error { get; set; }
    public IReadOnlyList<AlbumPhoto> Photos { get; set; } = [];
}
