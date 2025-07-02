namespace Full_Metal_Paintball_Carmagnola.Models
{
    public class TesseratiPerPartitaViewModel
    {
        public DateTime DataPartita { get; set; }
        public string OraPartita { get; set; }
        public List<TesseramentoViewModel> Tesserati { get; set; }
    }
}
