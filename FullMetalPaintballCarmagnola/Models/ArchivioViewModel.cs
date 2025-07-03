namespace Full_Metal_Paintball_Carmagnola.Models
{
    public class ArchivioViewModel
    {
        public Dictionary<int, Dictionary<int, List<Partita>>> Archivio { get; set; } = new();
    }
}
