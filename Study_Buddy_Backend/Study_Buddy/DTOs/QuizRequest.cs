namespace StudyBuddy.DTOs
{
    public class QuizRequest
    {
        public string Topic { get; set; }
        public string Difficulty { get; set; } = "medium";
        public int QuestionCount { get; set; } = 10;
        public string? Exam { get; set; }
    }
}