using System;
using System.ComponentModel.DataAnnotations;

namespace Full_Metal_Paintball_Carmagnola.Models
{
    public class ReperibilitaStaff
    {
        public int Id { get; set; }

        [Required]
        public DateTime Data { get; set; }

        [Required]
        public string Giorno { get; set; }

        public string Reperibile { get; set; } = "Nessuno";
    }
}
