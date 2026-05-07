using System.Collections.Generic;

namespace Full_Metal_Paintball_Carmagnola.Models
{
    public class DocumentoFornitore
    {
        public int Id { get; set; }
        public string? Nome { get; set; }
        public ICollection<Documento>? Documenti { get; set; }
    }
}
