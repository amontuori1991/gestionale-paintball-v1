namespace Full_Metal_Paintball_Carmagnola.Models
{
    public class TesseramentoPubblicoViewModel
    {
        public string Nome { get; set; } = string.Empty;
        public string Cognome { get; set; } = string.Empty;
    }

    public class PartitaPubblicoViewModel
    {
        public int PartitaId { get; set; }
        public DateTime DataPartita { get; set; }
        public TimeSpan OraPartita { get; set; }
        public List<TesseramentoPubblicoViewModel> Tesserati { get; set; } = new List<TesseramentoPubblicoViewModel>();
    }
}