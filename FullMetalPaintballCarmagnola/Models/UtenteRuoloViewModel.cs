namespace Full_Metal_Paintball_Carmagnola.Models
{
    public class UtenteRuoloViewModel
    {
        public string Id { get; set; }
        public string NomeCompleto { get; set; }
        public string Email { get; set; }
        public string Ruolo { get; set; }
        public bool IsApproved { get; set; }  // <-- aggiunta
    }
}
