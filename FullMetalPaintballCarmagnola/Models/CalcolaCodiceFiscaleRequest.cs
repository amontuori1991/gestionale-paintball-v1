namespace Full_Metal_Paintball_Carmagnola.Models
{
    public class CalcolaCodiceFiscaleRequest
    {
        public string? Nome { get; set; }
        public string? Cognome { get; set; }
        public DateTime? DataNascita { get; set; }
        public string? Sesso { get; set; }
        public string? CodiceCatastale { get; set; }
    }
}
