using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Full_Metal_Paintball_Carmagnola.Models // Assicurati che il namespace sia corretto
{
    public class Tesseramento
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Nome { get; set; } = string.Empty; // Inizializzato

        [Required]
        [StringLength(100)]
        public string Cognome { get; set; } = string.Empty; // Inizializzato

        [Required]
        public DateTime DataNascita { get; set; }

        [Required]
        [StringLength(50)]
        public string Genere { get; set; } = string.Empty; // Inizializzato

        [Required]
        [StringLength(100)]
        public string ComuneNascita { get; set; } = string.Empty; // Inizializzato

        [Required]
        [StringLength(100)]
        public string ComuneResidenza { get; set; } = string.Empty; // Inizializzato

        [Required]
        [EmailAddress] // Aggiunto per consistenza con ViewModel se vuoi la validazione DB
        [StringLength(255)]
        public string Email { get; set; } = string.Empty; // Inizializzato

        [StringLength(16)]
        public string? CodiceFiscale { get; set; }

        [Required]
        [StringLength(3)]
        public string Minorenne { get; set; } = string.Empty; // Inizializzato

        [StringLength(100)]
        public string? NomeGenitore { get; set; }

        [StringLength(100)]
        public string? CognomeGenitore { get; set; }

        [Required]
        public bool TerminiAccettati { get; set; }

        [Required]
        [StringLength(255)] // Salva il percorso del file
        public string Firma { get; set; } = string.Empty; // Inizializzato

        public DateTime DataCreazione { get; set; } = DateTime.UtcNow;


        public int? PartitaId { get; set; }  // Nullable perché non tutti i tesseramenti sono collegati a una partita
        public Partita? Partita { get; set; }  // Navigazione opzionale
        public string? Tessera { get; set; }
    }
}