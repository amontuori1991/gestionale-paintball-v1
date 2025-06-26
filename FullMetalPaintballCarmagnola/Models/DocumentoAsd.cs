using System;
using System.ComponentModel.DataAnnotations;

namespace Full_Metal_Paintball_Carmagnola.Models
{
    public class DocumentoAsd
    {
        public int Id { get; set; }

        [Required]
        public string OriginalFileName { get; set; }

        [Required]
        public string StoredFileName { get; set; }

        [Required]
        [StringLength(200)]
        public string Descrizione { get; set; }

        public DateTime DataCaricamento { get; set; }
    }
}
