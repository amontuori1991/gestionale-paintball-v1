using System.ComponentModel.DataAnnotations;

namespace Full_Metal_Paintball_Carmagnola.Models;

public class CampoChiusura
{
    public int Id { get; set; }

    [Required]
    public DateTime DataInizio { get; set; }

    [Required]
    public DateTime DataFine { get; set; }

    [StringLength(160)]
    public string? Motivo { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
