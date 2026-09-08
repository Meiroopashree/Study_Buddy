namespace StudyBuddy.DTOs
{
    using StudyBuddy.Models;

    public class SaveQuizRequest
    {
        public string Topic { get; set; } = string.Empty;
        public string Difficulty { get; set; } = "medium";
        public int Score { get; set; }
        public int TotalQuestions { get; set; }
        public int TimeSpentSeconds { get; set; }
        public List<UserAnswer>? Answers { get; set; }
        public List<Question>? Questions { get; set; }
    }

    public class UserAnswer
    {
        public string Question { get; set; } = string.Empty;
        public string? YourAnswer { get; set; }
        public string CorrectAnswer { get; set; } = string.Empty;
        public bool IsCorrect { get; set; }
    }
}
