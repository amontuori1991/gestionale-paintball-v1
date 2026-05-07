namespace Full_Metal_Paintball_Carmagnola.Models
{
    public class RolePermissionViewModel
    {
        public string RoleName { get; set; } = null!;
        public string FeatureName { get; set; } = null!;
        public bool? IsAllowed { get; set; } // true = consenti, false = nega, null = non impostato
    }
}
