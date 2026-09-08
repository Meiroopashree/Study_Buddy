namespace StudyBuddy.Models
{
    public class ChapterContent
    {
        public int Id { get; set; }
        public int? UserId { get; set; }
        public User? User { get; set; }
        public int ChapterId { get; set; }
        public Chapter Chapter { get; set; }
        public string Summary { get; set; } = "";
        public DateTime UpdatedAt { get; set; }
    }
}