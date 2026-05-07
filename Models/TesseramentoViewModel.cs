using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Full_Metal_Paintball_Carmagnola.Models // Assicurati che il namespace sia corretto
{
    public class TesseramentoViewModel : IValidatableObject
    {
        // Costruttore di default per inizializzare le proprietà
        public TesseramentoViewModel()
        {
            Nome = string.Empty;
            Cognome = string.Empty;
            DataNascita = DateTime.Today;
            Genere = string.Empty;
            ComuneNascita = string.Empty;
            ComuneResidenza = string.Empty;
            Email = string.Empty;
            Minorenne = "No";
            TerminiAccettati = false;
            Firma = string.Empty;
            DataCreazione = DateTime.Now;
        }

        public int Id { get; set; }

        [Required(ErrorMessage = "Il nome è obbligatorio")]
        public string Nome { get; set; }

        [Required(ErrorMessage = "Il cognome è obbligatorio")]
        public string Cognome { get; set; }

        [Required(ErrorMessage = "La data di nascita è obbligatoria")]
        [DataType(DataType.Date)]
        [Display(Name = "Data di Nascita")]
        public DateTime DataNascita { get; set; }

        [Required(ErrorMessage = "Il genere è obbligatorio")]
        public string Genere { get; set; }

        [Required(ErrorMessage = "Il comune di nascita è obbligatorio")]
        [Display(Name = "Comune di Nascita")]
        public string ComuneNascita { get; set; }

        [Required(ErrorMessage = "Il comune di residenza è obbligatorio")]
        [Display(Name = "Comune di Residenza")]
        public string ComuneResidenza { get; set; }

        [Required(ErrorMessage = "La mail è obbligatoria")]
        [EmailAddress(ErrorMessage = "L'email non è valida")]
        public string Email { get; set; }

        [Display(Name = "Codice Fiscale")]
        public string? CodiceFiscale { get; set; }

        [Required(ErrorMessage = "Il campo 'Sei minorenne?' è obbligatorio")]
        [Display(Name = "Sei minorenne?")]
        public string Minorenne { get; set; }

        [Display(Name = "Nome Genitore")]
        public string? NomeGenitore { get; set; }

        [Display(Name = "Cognome Genitore")]
        public string? CognomeGenitore { get; set; }

        [Display(Name = "Accetto i termini e le condizioni")]
        [Range(typeof(bool), "true", "true", ErrorMessage = "Devi accettare i termini e le condizioni")]
        public bool TerminiAccettati { get; set; }

        [Required(ErrorMessage = "È obbligatorio firmare il modulo.")]
        public string Firma { get; set; }

        [Display(Name = "Data Creazione")]
        public DateTime DataCreazione { get; set; }

        // Metodo per la validazione condizionale (lato server per il form di input)
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (Minorenne == "Sì")
            {
                if (string.IsNullOrWhiteSpace(NomeGenitore))
                {
                    yield return new ValidationResult("Il nome del genitore è obbligatorio se il tesserato è minorenne.", new[] { nameof(NomeGenitore) });
                }
                if (string.IsNullOrWhiteSpace(CognomeGenitore))
                {
                    yield return new ValidationResult("Il cognome del genitore è obbligatorio se il tesserato è minorenne.", new[] { nameof(CognomeGenitore) });
                }
            }
        }
    }
}

