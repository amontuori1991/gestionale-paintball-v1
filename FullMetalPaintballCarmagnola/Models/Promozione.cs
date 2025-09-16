using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Full_Metal_Paintball_Carmagnola.Models
{
    [Table("Promozioni")]
    public class Promozione
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("nome")]
        public string Nome { get; set; } = string.Empty;

        [Column("alias")]
        public string Alias { get; set; } = string.Empty;

        [Column("descrizione")]
        public string? Descrizione { get; set; }

        [Column("datascadenza", TypeName = "date")] // <— è un DATE puro
        public DateTime DataScadenza { get; set; }

        [Column("datacreazione", TypeName = "timestamp without time zone")]
        public DateTime DataCreazione { get; set; } = DateTime.UtcNow;

        // NEW — mappa la colonna text 'promotiontype' (valori: 'Instagram' | 'EventoRichiedeDati')
        [Column("promotiontype")]
        public string PromotionType { get; set; } = "Instagram";

        // NEW — anno edizione (es. 2025) opzionale
        [Column("editionyear")]
        public int? EditionYear { get; set; }

        // Comodità per la UI/validazioni condizionali
        [NotMapped]
        public bool RequiresPersonalData => string.Equals(PromotionType, "EventoRichiedeDati", StringComparison.OrdinalIgnoreCase);
    }
}
