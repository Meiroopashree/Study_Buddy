namespace StudyBuddy.Models
{
    public class QuestionPaper
    {
        public int Id { get; set; }
        public int? UserId { get; set; }
        public User? User { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Status { get; set; } = "parsing";
        public bool IsPublic { get; set; }
        public string? Exam { get; set; }
        public int? Year { get; set; }
        public int TotalQuestions { get; set; }
        public int ChunksTotal { get; set; }
        public int ChunksDone { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<PaperQuestion> Questions { get; set; } = new();
    }
}
