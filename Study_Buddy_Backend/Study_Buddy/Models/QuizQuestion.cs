namespace StudyBuddy.Models
{
    public class QuizQuestion
    {
        public int Id { get; set; }
        public int TopicId { get; set; }
        public Topic Topic { get; set; }
        public int? UserId { get; set; }
        public User? User { get; set; }
        public string Type { get; set; } = "mcq";
        public string QuestionText { get; set; } = string.Empty;
        public string OptionsJson { get; set; } = "[]";
        public string CorrectAnswer { get; set; } = string.Empty;
        public string CorrectAnswersJson { get; set; } = "[]";
        public string? Assertion { get; set; }
        public string? Reason { get; set; }
        public string RightOptionsJson { get; set; } = "[]";
        public string? Passage { get; set; }
        public string Explanation { get; set; } = "";
        public string Difficulty { get; set; } = "medium";
    }
}
