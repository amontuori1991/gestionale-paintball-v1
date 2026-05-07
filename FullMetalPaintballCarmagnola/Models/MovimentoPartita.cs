using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Full_Metal_Paintball_Carmagnola.Models
{
    public class MovimentoPartita
    {
        public int Id { get; set; }

        [Required]
        public int PartitaId { get; set; }

        [ForeignKey("PartitaId")]
        public Partita Partita { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? Dare { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? Avere { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? DareBis { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? AvereBis { get; set; }

        [MaxLength(500)]
        public string? Note { get; set; }
    }
}
