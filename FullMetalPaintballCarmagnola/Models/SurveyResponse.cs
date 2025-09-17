using System.ComponentModel.DataAnnotations.Schema;

namespace Full_Metal_Paintball_Carmagnola.Models
{
    public class SurveyResponse
    {
        public int Id { get; set; }

        [ForeignKey(nameof(Survey))]
        public int SurveyId { get; set; }
        public Survey Survey { get; set; } = null!;

        public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

        // Metadati utili (facoltativi)
        public string? SourceIp { get; set; }
        public string? UserAgent { get; set; }

        public ICollection<SurveyAnswer> Answers { get; set; } = new List<SurveyAnswer>();
    }
}
