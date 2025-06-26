using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations; // Aggiungi per Data Annotations

namespace Full_Metal_Paintball_Carmagnola.Models
{
    public class DocumentoViewModel
    {
        public int Id { get; set; }

        // Queste proprietà non sono modificate dal form, possono essere nullable
        public string? PdfFileName { get; set; }
        public DateTime DataCaricamento { get; set; } // Lascia così se non viene toccata, altrimenti valuta nullable

        [Required(ErrorMessage = "Il tipo documento è obbligatorio.")]
        public string? TipoDocumento { get; set; } // Reso nullable ma con [Required] per il binding

        [Required(ErrorMessage = "La data documento è obbligatoria.")]
        [DataType(DataType.Date)] // Suggerimento per il tipo di input HTML
        public DateTime DataDocumento { get; set; }

        [Required(ErrorMessage = "Il numero documento è obbligatorio.")]
        [StringLength(100, ErrorMessage = "Il numero documento non può superare i 100 caratteri.")]
        public string? NumeroDocumento { get; set; }

        // FornitoreNome non dovrebbe essere parte del modello di input per la modifica,
        // è solo per visualizzazione. Rimuovilo dal ViewModel o rendilo nullable.
        // Se lo tieni, assicurati che non causi problemi di validazione se non viene inviato.
        public string? FornitoreNome { get; set; }

        [Required(ErrorMessage = "Il fornitore è obbligatorio.")]
        public int FornitoreId { get; set; }

        [Required(ErrorMessage = "L'importo è obbligatorio.")]
        [Range(0.0, double.MaxValue, ErrorMessage = "L'importo deve essere un valore positivo.")]
        public decimal Importo { get; set; }

        public string? Note { get; set; } // Le note possono essere opzionali
    }

    public class DocumentiViewModel
    {
        public List<DocumentoViewModel> Documenti { get; set; }
        public List<DocumentoFornitore> Fornitori { get; set; }
        public string? FiltroTipoDocumento { get; set; }
        public DateTime? FiltroDataDocumento { get; set; }
        public int? FiltroFornitoreId { get; set; }
        public string? FiltroNote { get; set; }
    }
}