using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace Full_Metal_Paintball_Carmagnola.Models
{
    public class TesseramentoViewModel : IValidatableObject
    {
        public TesseramentoViewModel()
        {
            Nome = string.Empty;
            Cognome = string.Empty;
            // DataNascita = DateTime.Today; // <<< Mantieni commentato o rimosso
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
        public DateTime? DataNascita { get; set; } // <<< MODIFICATO QUI: RESO NULLABLE CON '?'

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

        public string? Tessera { get; set; }


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

        // Campo invisibile al form, ma usato per associare il tesseramento alla partita
        [HiddenInput(DisplayValue = false)]
        public int? PartitaId { get; set; }

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

            // AGGIUNTA VALIDAZIONE PER DataNascita, ORA CHE È NULLABLE
            if (!DataNascita.HasValue)
            {
                yield return new ValidationResult("La data di nascita è obbligatoria.", new[] { nameof(DataNascita) });
            }
            // Puoi aggiungere altre validazioni qui, ad esempio per età minima/massima.
            // else if (DataNascita.Value > DateTime.Now.AddYears(-18))
            // {
            //     yield return new ValidationResult("Devi avere almeno 18 anni.", new[] { nameof(DataNascita) });
            // }
        }

        public Tesseramento ToEntity(string firmaPath)
        {
            return new Tesseramento
            {
                Id = this.Id,
                Nome = this.Nome,
                Cognome = this.Cognome,
                DataNascita = this.DataNascita.GetValueOrDefault(), // <<< USA GetValueOrDefault() ORA CHE È NULLABLE
                Genere = this.Genere,
                ComuneNascita = this.ComuneNascita,
                ComuneResidenza = this.ComuneResidenza,
                Email = this.Email,
                CodiceFiscale = this.CodiceFiscale,
                Minorenne = this.Minorenne,
                NomeGenitore = this.NomeGenitore,
                CognomeGenitore = this.CognomeGenitore,
                TerminiAccettati = this.TerminiAccettati,
                Firma = firmaPath,
                DataCreazione = this.DataCreazione,
                PartitaId = this.PartitaId
            };
        }
    }
}