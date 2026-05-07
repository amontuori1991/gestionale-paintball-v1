using System;

namespace Full_Metal_Paintball_Carmagnola.Models
{
    public class Documento
    {
        public int Id { get; set; }
        public string? PdfFileName { get; set; }
        public DateTime DataCaricamento { get; set; }
        public string? TipoDocumento { get; set; }
        public DateTime DataDocumento { get; set; }
        public string? NumeroDocumento { get; set; }
        public int FornitoreId { get; set; }
        public DocumentoFornitore? Fornitore { get; set; }
        public decimal Importo { get; set; }
        public string? Note { get; set; }
    }
}
