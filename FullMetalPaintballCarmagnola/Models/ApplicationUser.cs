using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace Full_Metal_Paintball_Carmagnola.Models
{
    public class ApplicationUser : IdentityUser
    {
        // proprietà personalizzate esistenti...
        public string? FirstName { get; set; }
        public string? LastName { get; set; }

        // Aggiungi questa proprietà
        public bool IsApproved { get; set; } = false; // default a false
    }
}
