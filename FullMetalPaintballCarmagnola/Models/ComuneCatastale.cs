using System.ComponentModel.DataAnnotations;

namespace Full_Metal_Paintball_Carmagnola.Models
{
    public class ComuneCatastale
    {
        public int Id { get; set; }

        [Required]
        [StringLength(150)]
        public string Nome { get; set; } = string.Empty;

        [StringLength(2)]
        public string? Provincia { get; set; }

        [Required]
        [StringLength(4)]
        public string CodiceCatastale { get; set; } = string.Empty;

        [StringLength(10)]
        public string? CodiceIstat { get; set; }

        public bool Attivo { get; set; } = true;
    }
}
