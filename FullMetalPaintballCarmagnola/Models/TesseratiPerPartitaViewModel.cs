// Percorso: Models/ViewModels/TesseratiPerPartitaViewModel.cs

using System;
using System.Collections.Generic;

namespace Full_Metal_Paintball_Carmagnola.Models
{
    public class TesseratiPerPartitaViewModel
    {
        public int PartitaId { get; set; } // <-- QUESTA PROPRIETÀ È MANCANTE

        public DateTime DataPartita { get; set; }

        public string OraPartita { get; set; }

        public List<TesseramentoViewModel> Tesserati { get; set; }
    }
}