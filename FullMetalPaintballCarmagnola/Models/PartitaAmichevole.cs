using System.ComponentModel.DataAnnotations;

namespace Full_Metal_Paintball_Carmagnola.Models;

public sealed class PartitaAmichevole
{
    public Guid Id { get; set; }
    public DateTime Data { get; set; }
    public string Tipo { get; set; } = "Adulti";
    public bool ColpiIllimitati { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public string? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
}

public sealed class AmichevoleInput
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [Required, DataType(DataType.Date)] public DateTime? Data { get; set; }
    [Required, RegularExpression("^(Adulti|Kids)$")] public string Tipo { get; set; } = "Adulti";
    public bool ColpiIllimitati { get; set; }
    [StringLength(1000)] public string? Note { get; set; }
}

public sealed class RegistroAmichevoli
{
    public AmichevoleInput Form { get; set; } = new();
    public List<PartitaAmichevole> Partite { get; set; } = new();
}
