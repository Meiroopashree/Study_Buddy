namespace StudyBuddy.Models
{
    public class StudyActivity
    {
        public int Id { get; set; }
        public int? UserId { get; set; }
        public User? User { get; set; }
        public string Type { get; set; } = "chat";
        public DateTime Timestamp { get; set; }
    }
}
