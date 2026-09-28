using System.ComponentModel.DataAnnotations;
namespace Full_Metal_Paintball_Carmagnola.Models;

public sealed class FlyerRequest
{
    [Required, StringLength(65)] public string Title { get; set; } = "SCENDI IN CAMPO.";
    [Required, StringLength(120)] public string Subtitle { get; set; } = "La tua squadra. Una sfida da ricordare.";
    [StringLength(40)] public string? Badge { get; set; } = "PAINTBALL EXPERIENCE";
    [StringLength(60)] public string? Offer { get; set; } = "PREPARATI ALL'AZIONE";
    [StringLength(90)] public string? Event { get; set; }
    [StringLength(320)] public string? Details { get; set; } = "Compleanni, feste e sfide tra amici. Porta la tua squadra e vivi un'esperienza fuori dall'ordinario.";
    [Required, StringLength(45)] public string CallToAction { get; set; } = "PRENOTA LA TUA PARTITA";
    [Required, RegularExpression("^(impact|sun|ice)$")] public string Theme { get; set; } = "impact";
    [Required, RegularExpression("^(preview|pdf|jpg)$")] public string Format { get; set; } = "preview";
}
