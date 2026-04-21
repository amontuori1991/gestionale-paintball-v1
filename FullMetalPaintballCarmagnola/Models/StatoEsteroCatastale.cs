using System.ComponentModel.DataAnnotations;

namespace Full_Metal_Paintball_Carmagnola.Models
{
    public class StatoEsteroCatastale
    {
        public int Id { get; set; }

        [Required]
        [StringLength(150)]
        public string Nome { get; set; } = string.Empty;

        [Required]
        [StringLength(4)]
        public string CodiceCatastale { get; set; } = string.Empty;

        public bool Attivo { get; set; } = true;
    }
}
