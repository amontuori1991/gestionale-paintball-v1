// Models/RangeTessereAcsi.cs
using System.ComponentModel.DataAnnotations;

namespace Full_Metal_Paintball_Carmagnola.Models
{
    public class RangeTessereAcsi
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Da Numero")]
        public long NumeroDa { get; set; }

        [Required]
        [Display(Name = "A Numero")]
        public long NumeroA { get; set; }

        public bool Assegnata { get; set; }
    }
}
