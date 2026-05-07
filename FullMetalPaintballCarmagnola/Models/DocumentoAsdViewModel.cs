using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http; // Assicurati che questo sia presente

namespace Full_Metal_Paintball_Carmagnola.Models
{
    public class DocumentoAsdViewModel
    {
        public int Id { get; set; }

        // Queste proprietà non vengono inviate dal form di Create,
        // quindi devono essere nullable per non invalidare il ModelState.
        public string? OriginalFileName { get; set; }
        public string? StoredFileName { get; set; }
        public DateTime? DataCaricamento { get; set; } // Potrebbe essere nullable se non sempre presente nel form

        [Required(ErrorMessage = "La descrizione è obbligatoria")]
        [StringLength(200, ErrorMessage = "La descrizione non può superare i 200 caratteri.")] // Aggiunto messaggio specifico
        public string? Descrizione { get; set; } // Reso nullable per coerenza con Required, anche se [Required] gestisce già il null

        // [Required(ErrorMessage = "Devi selezionare un file.")] // Potresti anche aggiungere Required qui per il file
        public IFormFile? File { get; set; } // Reso nullable per coerenza. La validazione del file è già nel controller.
    }
}