using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Full_Metal_Paintball_Carmagnola.Models
{
    public class PresenzaStaff
    {
        public int Id { get; set; }

        [Required]
        [Column(TypeName = "date")]
        public DateTime Data { get; set; }

        [Required]
        public string Giorno { get; set; }

        [Required]
        public string NomeStaff { get; set; }

        public bool? Presente { get; set; }

    }
}
