using System;
using System.ComponentModel.DataAnnotations;

namespace Full_Metal_Paintball_Carmagnola.Models
{
    public class PresenzaStaff
    {
        public int Id { get; set; }

        [Required]
        public DateTime Data { get; set; }

        [Required]
        public string Giorno { get; set; }

        [Required]
        public string NomeStaff { get; set; }

        public bool? Presente { get; set; }

    }
}
