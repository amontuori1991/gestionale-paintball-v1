using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Full_Metal_Paintball_Carmagnola.Models
{
    public class SurveyOption
    {
        public int Id { get; set; }

        [ForeignKey(nameof(Question))]
        public int QuestionId { get; set; }
        public SurveyQuestion Question { get; set; } = null!;

        [Required, MaxLength(300)]
        public string Text { get; set; } = string.Empty;

        public int Order { get; set; } = 0;
    }
}
