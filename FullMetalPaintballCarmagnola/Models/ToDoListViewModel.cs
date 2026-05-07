using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System;

namespace Full_Metal_Paintball_Carmagnola.Models
{
    // ViewModel per visualizzare un singolo elemento ToDo con il nome del Topic
    public class ToDoItemViewModel
    {
        public int Id { get; set; }
        public string Description { get; set; } = string.Empty;
        public bool IsCompleted { get; set; }
        public DateTime CreatedDate { get; set; }
        public int TopicId { get; set; }
        public string TopicName { get; set; } = string.Empty;
        public string? Notes { get; set; }
    }

    // ViewModel per creare una nuova attività
    public class CreateToDoItemViewModel
    {
        [Required(ErrorMessage = "La descrizione dell'attività è obbligatoria.")]
        [StringLength(500, ErrorMessage = "La descrizione non può superare i 500 caratteri.")]
        public string Description { get; set; } = string.Empty;

        [Required(ErrorMessage = "Il topic è obbligatorio.")]
        public int TopicId { get; set; }

        public string? Notes { get; set; } // Note opzionali al momento della creazione
    }

    // ViewModel principale per la pagina della ToDoList
    public class ToDoListIndexViewModel
    {
        public List<Topic> Topics { get; set; } = new List<Topic>();
        public Dictionary<string, List<ToDoItemViewModel>> ToDoItemsByTopic { get; set; } = new Dictionary<string, List<ToDoItemViewModel>>();
        public List<ToDoItemViewModel> CompletedItems { get; set; } = new List<ToDoItemViewModel>();

        // Per il form di creazione di una nuova attività
        public CreateToDoItemViewModel NewItem { get; set; } = new CreateToDoItemViewModel();
    }
}