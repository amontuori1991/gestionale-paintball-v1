using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Full_Metal_Paintball_Carmagnola.Models
{
    public class FieraSportLead
    {
        public int Id { get; set; }

        [Required]
        [EmailAddress]
        [StringLength(255)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(120)]
        public string NomeCognome { get; set; } = string.Empty;

        public bool PrivacyAccepted { get; set; }

        public bool LiabilityAccepted { get; set; }

        [StringLength(80)]
        public string EventCode { get; set; } = "FieraSportCarmagnola2026";

        [Column(TypeName = "timestamp without time zone")]
        public DateTime CreatedAtUtc { get; set; }

        public string? IpAddress { get; set; }

        public string? UserAgent { get; set; }
    }
}
