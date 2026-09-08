namespace StudyBuddy.Models
{
    public class QuizResult
    {
        public int Id { get; set; }
        public int? UserId { get; set; }
        public User? User { get; set; }
        public string Topic { get; set; } = string.Empty;
        public string Difficulty { get; set; } = "medium";
        public int Score { get; set; }
        public int TotalQuestions { get; set; }
        public string AnswersJson { get; set; } = "[]";
        public string QuestionsJson { get; set; } = "[]";
        public int TimeSpentSeconds { get; set; }
        public DateTime CompletedAt { get; set; }
    }
}
