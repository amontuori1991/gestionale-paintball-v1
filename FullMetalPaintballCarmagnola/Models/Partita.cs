using System.ComponentModel.DataAnnotations;
using Full_Metal_Paintball_Carmagnola.Models;
using Microsoft.EntityFrameworkCore;
using System; // Assicurati che System sia importato per TimeSpan e DateTime

namespace Full_Metal_Paintball_Carmagnola.Models;


public class Partita
{
    public int Id { get; set; }

    [Required]
    public DateTime Data { get; set; }

    public string? Riferimento { get; set; }

    [Required]
    [DataType(DataType.Time)]
    [DisplayFormat(DataFormatString = @"{0:hh\:mm}", ApplyFormatInEditMode = true)]
    public TimeSpan OraInizio { get; set; }

    [Required]
    public double Durata { get; set; }

    [Required]
    public int NumeroPartecipanti { get; set; }

    [Required]
    [DataType(DataType.Currency)]
    [Precision(10, 2)]
    public decimal Caparra { get; set; }

    public bool CaparraConfermata { get; set; }

    public string? MetodoPagamentoCaparra { get; set; }

    public bool Torneo { get; set; }
    public bool ColpiIllimitati { get; set; }
    public bool Caccia { get; set; }

    public string? LinkTesseramento { get; set; }

    // Nuovi campi staff e reperibile
    public string? Staff1 { get; set; }
    public string? Staff2 { get; set; }
    public string? Staff3 { get; set; }
    public string? Staff4 { get; set; }

    public string? Reperibile { get; set; }

    public virtual ICollection<Tesseramento>? Tesseramenti { get; set; }

    public bool IsDeleted { get; set; } = false;

    public string? Annotazioni { get; set; }

    public string? Rimborso { get; set; }  // Puoi usare "SI" o "NO"
}