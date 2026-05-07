namespace Full_Metal_Paintball_Carmagnola.Models
{
    public class GestionePermessiViewModel
    {
        public List<RolePermissionViewModel> Permessi { get; set; } = new();
        public List<UtenteRuoloViewModel> Utenti { get; set; } = new();
    }
}
