using System.ComponentModel.DataAnnotations;

namespace Full_Metal_Paintball_Carmagnola.Models;

public class TorneoForm : IValidatableObject
{
    public int Id { get; set; }
    [Required, StringLength(120)] public string Nome { get; set; } = "";
    [StringLength(2000)] public string? Note { get; set; }
    public DateTime Data { get; set; } = DateTime.Today;
    public TimeSpan OraInizio { get; set; } = TimeSpan.FromHours(9);
    [Range(2, 64)] public int NumeroSquadre { get; set; } = 8;
    [Range(1, 32)] public int NumeroGironi { get; set; } = 2;
    public bool AndataRitorno { get; set; }
    public bool FasiFinali { get; set; } = true;
    [Range(1, 32)] public int QualificatePerGirone { get; set; } = 2;
    public bool FinaleTerzoPosto { get; set; }
    public bool RegistraPunteggio { get; set; }
    [Range(0, 100)] public decimal PuntiVittoria { get; set; } = 1;
    [Range(0, 100)] public decimal PuntiPareggio { get; set; } = 0.5m;
    [Range(0, 100)] public decimal PuntiSconfitta { get; set; }
    public Guid Version { get; set; }
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (Data.Year < 2020 || Data.Year > 2100) yield return new("Data non valida.", [nameof(Data)]);
        if (OraInizio < TimeSpan.Zero || OraInizio >= TimeSpan.FromDays(1)) yield return new("Orario non valido.", [nameof(OraInizio)]);
        if (NumeroGironi > NumeroSquadre / 2) yield return new("Servono almeno due squadre per girone.", [nameof(NumeroGironi)]);
        if (FasiFinali && (QualificatePerGirone > NumeroSquadre / Math.Max(1, NumeroGironi) || NumeroGironi * QualificatePerGirone < 2))
            yield return new("Qualificate non compatibili con i gironi: servono almeno due finaliste.", [nameof(QualificatePerGirone)]);
        if (FinaleTerzoPosto && (!FasiFinali || NumeroGironi * QualificatePerGirone < 4)) yield return new("Per il terzo posto servono almeno quattro qualificate e le fasi finali.", [nameof(FinaleTerzoPosto)]);
    }
}

public class Torneo
{
    public int Id { get; set; }
    [MaxLength(120)] public string Nome { get; set; } = "";
    [MaxLength(2000)] public string? Note { get; set; }
    public DateTime Data { get; set; }
    public TimeSpan OraInizio { get; set; }
    public int NumeroSquadre { get; set; }
    public int NumeroGironi { get; set; }
    public bool AndataRitorno { get; set; }
    public bool FasiFinali { get; set; }
    public int QualificatePerGirone { get; set; }
    public bool FinaleTerzoPosto { get; set; }
    public bool RegistraPunteggio { get; set; }
    public decimal PuntiVittoria { get; set; }
    public decimal PuntiPareggio { get; set; }
    public decimal PuntiSconfitta { get; set; }
    public Guid PhotoToken { get; set; } = Guid.NewGuid();
    [ConcurrencyCheck] public Guid Version { get; set; } = Guid.NewGuid();
    public List<TorneoSquadra> Squadre { get; set; } = [];
    public List<TorneoIncontro> Incontri { get; set; } = [];
}

public class TorneoSquadra
{
    public int Id { get; set; }
    public int TorneoId { get; set; }
    public Torneo Torneo { get; set; } = null!;
    public Guid Token { get; set; } = Guid.NewGuid();
    [MaxLength(100)] public string Nome { get; set; } = "";
    [MaxLength(100)] public string Referente { get; set; } = "";
    [MaxLength(30)] public string Telefono { get; set; } = "";
    public int Girone { get; set; }
    public int OrdineSpareggio { get; set; }
}

public class TorneoIncontro
{
    public int Id { get; set; }
    public int TorneoId { get; set; }
    public Torneo Torneo { get; set; } = null!;
    public string Fase { get; set; } = "Girone";
    public int Girone { get; set; }
    public int Turno { get; set; }
    public int Posizione { get; set; }
    public int? CasaId { get; set; }
    public TorneoSquadra? Casa { get; set; }
    public int? OspiteId { get; set; }
    public TorneoSquadra? Ospite { get; set; }
    public int? PuntiCasa { get; set; }
    public int? PuntiOspite { get; set; }
    public string? Esito { get; set; }
}

public class TorneoClassifica
{
    public TorneoSquadra Squadra { get; set; } = null!;
    public int Giocate { get; set; }
    public int Vinte { get; set; }
    public int Pareggi { get; set; }
    public int Perse { get; set; }
    public decimal Punti { get; set; }
    public int Fatti { get; set; }
    public int Subiti { get; set; }
}

public class TorneoDetail
{
    public Torneo Torneo { get; set; } = null!;
    public Dictionary<int, List<TorneoClassifica>> Classifiche { get; set; } = [];
}

public class TorneoIscrizione
{
    public int Id { get; set; }
    public int TorneoSquadraId { get; set; }
    public TorneoSquadra TorneoSquadra { get; set; } = null!;
    public int TesseramentoId { get; set; }
    public Tesseramento Tesseramento { get; set; } = null!;
    public bool NuovoTesseramento { get; set; }
    public string Firma { get; set; } = "";
    public DateTime DataCreazione { get; set; }
}
