using System.ComponentModel.DataAnnotations.Schema;

namespace Full_Metal_Paintball_Carmagnola.Models
{
    public class SurveyAnswer
    {
        public int Id { get; set; }

        [ForeignKey(nameof(Response))]
        public int ResponseId { get; set; }
        public SurveyResponse Response { get; set; } = null!;

        [ForeignKey(nameof(Question))]
        public int QuestionId { get; set; }
        public SurveyQuestion Question { get; set; } = null!;

        // Per risposte a testo libero
        public string? AnswerText { get; set; }

        // Per scelte (singole o multiple)
        public int? OptionId { get; set; }
    }
}
