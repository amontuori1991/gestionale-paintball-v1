namespace Full_Metal_Paintball_Carmagnola.Models
{
    public class RolePermission
    {
        public int Id { get; set; }

        public string RoleName { get; set; } = null!; // es. "Admin", "Staff"

        public string FeatureName { get; set; } = null!; // es. "Tesserati", "Prenotazioni"

        public bool IsAllowed { get; set; } // true = consenti, false = nega
    }
}
