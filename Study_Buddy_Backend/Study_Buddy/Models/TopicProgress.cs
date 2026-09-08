namespace StudyBuddy.Models
{
    public class TopicProgress
    {
        public int Id { get; set; }
        public int? UserId { get; set; }
        public User? User { get; set; }
        public int TopicId { get; set; }
        public Topic Topic { get; set; }
        public string Status { get; set; } = "not_started";
        public DateTime UpdatedAt { get; set; }
    }
}