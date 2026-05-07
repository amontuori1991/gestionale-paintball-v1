namespace Full_Metal_Paintball_Carmagnola.Models
{
    public class PrenotazionePubblicaViewModel
    {
        public List<PrenotazionePubblicaGiornoViewModel> Giorni { get; set; } = new();

        public List<PrenotazionePubblicaFaqViewModel> Faq { get; set; } = new();

        public DateTime PrimaDataInfrasettimanale { get; set; }
    }

    public class PrenotazionePubblicaGiornoViewModel
    {
        public DateTime Data { get; set; }
        public string GiornoLabel { get; set; } = string.Empty;
        public string DataLabel { get; set; } = string.Empty;
        public List<PrenotazionePubblicaSlotViewModel> Slot { get; set; } = new();
    }

    public class PrenotazionePubblicaSlotViewModel
    {
        public string Data { get; set; } = string.Empty;
        public string Giorno { get; set; } = string.Empty;
        public string Inizio { get; set; } = string.Empty;
        public string Fine { get; set; } = string.Empty;
        public double DurataMassima { get; set; }
    }

    public class PrenotazionePubblicaFaqViewModel
    {
        public string Domanda { get; set; } = string.Empty;

        public string Risposta { get; set; } = string.Empty;
    }
}
