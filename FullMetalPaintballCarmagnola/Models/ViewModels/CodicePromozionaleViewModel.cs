namespace Full_Metal_Paintball_Carmagnola.Models.ViewModels
{
    public class CodicePromozionaleViewModel
    {
        // Identificativi
        public int Id { get; set; }
        public string Codice { get; set; } = string.Empty;
        public string? Alias { get; set; }

        // Dati promo
        public string PromotionType { get; set; } = "Instagram"; // "Instagram" | "EventoRichiedeDati"
        public string? PromozioneNome { get; set; }
        public int? EditionYear { get; set; }

        // Dati IG
        public string? InstagramHandle { get; set; }

        // Dati anagrafici (promo evento)
        public string? Nome { get; set; }
        public string? Cognome { get; set; }
        public DateTime? DataNascita { get; set; }
        public string? ComuneNascita { get; set; }
        public string? ComuneResidenza { get; set; }

        // Meta
        public DateTime DataCreazione { get; set; }
        public DateTime DataScadenza { get; set; }
        public bool Utilizzato { get; set; }

        // Comodità in view
        public bool RequiresPersonalData =>
            string.Equals(PromotionType, "EventoRichiedeDati", StringComparison.OrdinalIgnoreCase);
    }
}
