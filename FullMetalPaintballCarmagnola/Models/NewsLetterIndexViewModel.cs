using System.ComponentModel.DataAnnotations;

namespace Full_Metal_Paintball_Carmagnola.Models
{
    public class NewsLetterIndexViewModel
    {
        public List<NewsLetterTemplate> Templates { get; set; } = new();

        public NewsLetterTemplate Template { get; set; } = new();

        public int EmailAttiveUniche { get; set; }

        public int EmailTotaliUniche { get; set; }

        public int TesseramentiDisiscritti { get; set; }

        [Display(Name = "Destinatari")]
        public string SegmentoDestinatari { get; set; } = "all";

        [Display(Name = "Programma invio")]
        public DateTime? DataOraProgrammata { get; set; }

        public int DestinatariStimati { get; set; }

        public List<NewsLetterScheduledSend> InviiProgrammati { get; set; } = new();

        public List<NewsLetterSendHistoryItem> StoricoInvii { get; set; } = new();

        [EmailAddress(ErrorMessage = "Inserisci un indirizzo email valido.")]
        [Display(Name = "Email test")]
        public string? EmailTest { get; set; }
    }

    public class NewsLetterScheduledSend
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        public NewsLetterTemplate Template { get; set; } = new();

        public string SegmentoDestinatari { get; set; } = "all";

        public DateTime DataOraProgrammataUtc { get; set; }

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }

    public class NewsLetterSendHistoryItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        public string TemplateNome { get; set; } = string.Empty;

        public string Oggetto { get; set; } = string.Empty;

        public string SegmentoDestinatari { get; set; } = "all";

        public int Destinatari { get; set; }

        public int Inviate { get; set; }

        public int Errori { get; set; }

        public DateTime SentAtUtc { get; set; } = DateTime.UtcNow;

        public bool Programmato { get; set; }
    }
}
