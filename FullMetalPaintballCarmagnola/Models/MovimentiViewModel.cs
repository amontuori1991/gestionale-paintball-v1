using System;

namespace Full_Metal_Paintball_Carmagnola.Models
{
    public class MovimentiViewModel
    {
        public int PartitaId { get; set; }

        public string? Stato { get; set; } 

        public DateTime Data { get; set; }

        public TimeSpan Ora { get; set; }

        public decimal Caparra { get; set; }

        public string? MetodoCaparra { get; set; }

        public decimal? Dare { get; set; }

        public decimal? Avere { get; set; }

        public decimal? DareBis { get; set; }

        public decimal? AvereBis { get; set; }

        public string? Note { get; set; }
    }
}
