using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Full_Metal_Paintball_Carmagnola.Models
{
    public class AssenzaCalendario
    {
        public int Id { get; set; }

        [Required]
        [Column(TypeName = "date")]
        public DateTime Data { get; set; }

        [Required]
        public string Giorno { get; set; }

        public string Reperibile { get; set; } // Montuo, Flavio, Bosax

        public string? Montuo { get; set; }
        public string? Flavio { get; set; }
        public string? Bosax { get; set; }

    }
}
