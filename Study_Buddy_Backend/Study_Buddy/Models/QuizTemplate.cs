namespace StudyBuddy.Models
{
    public class QuizTemplate
    {
        public int Id { get; set; }
        public int? UserId { get; set; }
        public User? User { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? ScopeType { get; set; }
        public string? Exam { get; set; }
        public string? Subject { get; set; }
        public string? Chapter { get; set; }
        public string? Topic { get; set; }
        public int QuestionCount { get; set; }
        public string? Difficulty { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
