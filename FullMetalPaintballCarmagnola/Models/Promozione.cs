using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Full_Metal_Paintball_Carmagnola.Models
{
    [Table("Promozioni")] // tutto minuscolo se creata senza virgolette
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

        [Column("datascadenza")]
        public DateTime DataScadenza { get; set; }

        [Column("datacreazione")]
        public DateTime DataCreazione { get; set; } = DateTime.UtcNow;
    }

}
