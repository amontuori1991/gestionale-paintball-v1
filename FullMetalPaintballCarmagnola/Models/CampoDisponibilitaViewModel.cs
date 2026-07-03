namespace Full_Metal_Paintball_Carmagnola.Models
{
    public class CampoDisponibilitaViewModel
    {
        public List<CampoDisponibilitaGiornoViewModel> Giorni { get; set; } = new();

        public List<CampoChiusura> Chiusure { get; set; } = new();
    }

    public class CampoChiusuraRequest
    {
        public string DataInizio { get; set; } = string.Empty;

        public string DataFine { get; set; } = string.Empty;

        public string? Motivo { get; set; }
    }

    public class CampoDisponibilitaRequest
    {
        public string Data { get; set; } = string.Empty;

        public string? OraInizio { get; set; }

        public string Durata { get; set; } = "1";

        public string TipoGruppo { get; set; } = "Completo";
    }

    public class CampoDisponibilitaGiornoViewModel
    {
        public DateTime Data { get; set; }
        public string GiornoLabel { get; set; } = string.Empty;
        public TimeSpan Apertura { get; set; }
        public TimeSpan Tramonto { get; set; }
        public TimeSpan UltimaFinePartita { get; set; }
        public bool CampoChiuso { get; set; }
        public string? ChiusuraMotivo { get; set; }
        public List<CampoFasciaViewModel> Fasce { get; set; } = new();
    }

    public class CampoFasciaViewModel
    {
        public TimeSpan Inizio { get; set; }
        public TimeSpan Fine { get; set; }
        public string Stato { get; set; } = string.Empty;
        public string? Dettaglio { get; set; }
        public bool Prenotabile { get; set; }

        public double DurataOre => Math.Round((Fine - Inizio).TotalHours, 2);
    }
}
