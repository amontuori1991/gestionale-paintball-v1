using System.ComponentModel.DataAnnotations;
using DocumentFormat.OpenXml.Office2013.Excel;

namespace Full_Metal_Paintball_Carmagnola.Models
{
    public class Survey
    {
        public int Id { get; set; }

        [Required, MaxLength(180)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        // Slug pubblico per URL (es. /Survey/{slug})
        [Required, MaxLength(64)]
        public string PublicSlug { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<SurveyQuestion> Questions { get; set; } = new List<SurveyQuestion>();
    }
}
