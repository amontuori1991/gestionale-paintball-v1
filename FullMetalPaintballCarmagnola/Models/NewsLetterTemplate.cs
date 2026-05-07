using System.ComponentModel.DataAnnotations;

namespace Full_Metal_Paintball_Carmagnola.Models
{
    public class NewsLetterTemplate
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        [Required(ErrorMessage = "Inserisci il nome del template.")]
        [StringLength(80, ErrorMessage = "Il nome non puo superare 80 caratteri.")]
        public string Nome { get; set; } = "Nuovo template";

        [Required(ErrorMessage = "Inserisci l'oggetto della newsletter.")]
        [StringLength(180, ErrorMessage = "L'oggetto non puo superare 180 caratteri.")]
        public string Oggetto { get; set; } = "News da Full Metal Paintball Carmagnola";

        [Required(ErrorMessage = "Inserisci il titolo della newsletter.")]
        [StringLength(120, ErrorMessage = "Il titolo non puo superare 120 caratteri.")]
        public string Titolo { get; set; } = "Novita dal campo";

        [StringLength(80, ErrorMessage = "Il sopratitolo non puo superare 80 caratteri.")]
        public string? Sopratitolo { get; set; } = "Full Metal Paintball Carmagnola";

        [StringLength(220, ErrorMessage = "Il testo anteprima non puo superare 220 caratteri.")]
        public string? Anteprima { get; set; }

        [Required(ErrorMessage = "Inserisci il contenuto della newsletter.")]
        public string Messaggio { get; set; } = string.Empty;

        [StringLength(80, ErrorMessage = "Il testo del pulsante non puo superare 80 caratteri.")]
        public string? TestoPulsante { get; set; }

        [Url(ErrorMessage = "Inserisci un link valido per il pulsante.")]
        public string? LinkPulsante { get; set; }

        [Url(ErrorMessage = "Inserisci un link valido per l'immagine.")]
        public string? ImmagineUrl { get; set; }

        [Url(ErrorMessage = "Inserisci un link valido per il logo.")]
        public string? LogoUrl { get; set; } = "https://i.imgur.com/K9Ugseg.gif";

        public bool MostraLogo { get; set; } = true;

        [StringLength(260, ErrorMessage = "Il footer non puo superare 260 caratteri.")]
        public string? FooterPersonalizzato { get; set; }

        [RegularExpression("^#[0-9a-fA-F]{6}$", ErrorMessage = "Usa un colore in formato #RRGGBB.")]
        public string ColorePrimario { get; set; } = "#e6007e";

        [RegularExpression("^#[0-9a-fA-F]{6}$", ErrorMessage = "Usa un colore in formato #RRGGBB.")]
        public string ColoreSecondario { get; set; } = "#f77f00";

        [RegularExpression("^#[0-9a-fA-F]{6}$", ErrorMessage = "Usa un colore in formato #RRGGBB.")]
        public string ColoreSfondo { get; set; } = "#f4f0df";

        [RegularExpression("^#[0-9a-fA-F]{6}$", ErrorMessage = "Usa un colore in formato #RRGGBB.")]
        public string ColoreScheda { get; set; } = "#fffaf1";

        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
