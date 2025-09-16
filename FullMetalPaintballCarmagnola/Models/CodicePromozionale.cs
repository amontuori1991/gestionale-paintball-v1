using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Full_Metal_Paintball_Carmagnola.Models
{
    [Table("codicipromozionali")]
    public class CodicePromozionale
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        // IG — ora nullable perché per promo evento non serve
        [Column("instagramhandle")]
        public string? InstagramAccount { get; set; }

        [Column("codice")]
        public string Codice { get; set; } = string.Empty;

        // Alias della promozione a cui è legato il codice
        [Column("alias")]
        public string? Alias { get; set; }

        // era: [Column("datagenerazione")] —> forziamo timestamptz
        [Column("datagenerazione", TypeName = "timestamp with time zone")]
        public DateTime DataCreazione { get; set; }

        // era: [Column("datascadenza")] —> forziamo timestamptz
        [Column("datascadenza", TypeName = "timestamp with time zone")]
        public DateTime DataScadenza { get; set; }

        [Column("utilizzato")]
        public bool Utilizzato { get; set; }

        // NEW — dati anagrafici per promo non-IG (tutti opzionali)
        [Column("nome")]
        public string? Nome { get; set; }

        [Column("cognome")]
        public string? Cognome { get; set; }

        // per sicurezza: data “pura” senza orario
        [Column("datanascita", TypeName = "date")]
        public DateTime? DataNascita { get; set; }

        [Column("comunenascita")]
        public string? ComuneNascita { get; set; }

        [Column("comuneresidenza")]
        public string? ComuneResidenza { get; set; }

        [Column("email")]
        public string? Email { get; set; }
    }
}
