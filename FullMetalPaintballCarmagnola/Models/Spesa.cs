using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Full_Metal_Paintball_Carmagnola.Models
{
    public class Spesa
    {
        public int Id { get; set; }

        [Required]
        public DateTime Data { get; set; }

        [Required]
        public TimeSpan Ora { get; set; }
        
        [MaxLength(50)]
        public string? Riferimento { get; set; }

        [Required]
        [MaxLength(255)]
        public string? Descrizione { get; set; }

        [Required]
        [Precision(10, 2)]
        public decimal Importo { get; set; }

        public bool Rimborsato { get; set; } = false;
    }
}
