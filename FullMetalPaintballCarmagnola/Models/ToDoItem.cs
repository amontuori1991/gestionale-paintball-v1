using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema; // Per [ForeignKey]

namespace Full_Metal_Paintball_Carmagnola.Models
{
    public class ToDoItem
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "La descrizione dell'attività è obbligatoria.")]
        [StringLength(500, ErrorMessage = "La descrizione dell'attività non può superare i 500 caratteri.")]
        public string Description { get; set; } = string.Empty;

        public bool IsCompleted { get; set; } = false; // Stato dell'attività (completata o meno)

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow; // Data di creazione

        // Proprietà per la relazione con Topic
        public int TopicId { get; set; }

        [ForeignKey("TopicId")]
        public Topic? Topic { get; set; } // Navigazione verso il Topic padre

        public string? Notes { get; set; } // Campo per le note aggiuntive
    }
}