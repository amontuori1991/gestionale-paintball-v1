using System.ComponentModel.DataAnnotations;
namespace Full_Metal_Paintball_Carmagnola.Models;

public sealed class FlyerRequest
{
    [Required, StringLength(65)] public string Title { get; set; } = "KIDS O ADULTI?\nLA SFIDA INIZIA QUI.";
    [Required, StringLength(120)] public string Subtitle { get; set; } = "Divertimento per i pi\u00f9 giovani. Adrenalina e strategia per i pi\u00f9 grandi.";
    [StringLength(40)] public string? Badge { get; set; } = "PAINTBALL EXPERIENCE";
    [StringLength(60)] public string? Offer { get; set; } = "DUE MODI DI GIOCARE. UN'UNICA PASSIONE.";
    [StringLength(90)] public string? Event { get; set; }
    [StringLength(320)] public string? Details { get; set; } = "Compleanni, feste e sfide tra amici. Porta la tua squadra e vivi un'esperienza fuori dall'ordinario.";
    [Required, StringLength(45)] public string CallToAction { get; set; } = "PRENOTA LA TUA PARTITA";
    [Required, RegularExpression("^(impact|sun|ice)$")] public string Theme { get; set; } = "impact";
    [Required, RegularExpression("^(preview|pdf|jpg)$")] public string Format { get; set; } = "preview";
}
