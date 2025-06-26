using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace Full_Metal_Paintball_Carmagnola.Models
{
    public class Topic
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Il nome del topic è obbligatorio.")]
        [StringLength(50, ErrorMessage = "Il nome del topic non può superare i 50 caratteri.")]
        public string Name { get; set; } = string.Empty;

        // Relazione uno a molti: un Topic può avere molte attività (ToDoItems)
        public ICollection<ToDoItem>? ToDoItems { get; set; }
    }
}