using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Full_Metal_Paintball_Carmagnola.Models
{
    public enum QuestionType
    {
        MultipleChoice = 0,
        OpenText = 1
    }

    public class SurveyQuestion
    {
        public int Id { get; set; }

        [ForeignKey(nameof(Survey))]
        public int SurveyId { get; set; }
        public Survey Survey { get; set; } = null!;

        [Required, MaxLength(400)]
        public string Text { get; set; } = string.Empty;

        public QuestionType Type { get; set; } = QuestionType.MultipleChoice;

        // Se MultipleChoice e AllowMultiple = true → checkbox, altrimenti radio
        public bool AllowMultiple { get; set; } = false;

        public int Order { get; set; } = 0;

        public ICollection<SurveyOption> Options { get; set; } = new List<SurveyOption>();
    }
}
