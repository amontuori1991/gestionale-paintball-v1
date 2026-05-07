using System;
using System.ComponentModel.DataAnnotations;

namespace Full_Metal_Paintball_Carmagnola.Models
{
    public class MovimentoExtra
    {
        public int Id { get; set; }
        public DateTime Data { get; set; } = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);

        public TimeSpan Ora { get; set; }

        public decimal? Dare { get; set; }
        public decimal? Avere { get; set; }
        public decimal? DareBis { get; set; }
        public decimal? AvereBis { get; set; }

        public string? Note { get; set; }
    }

}
