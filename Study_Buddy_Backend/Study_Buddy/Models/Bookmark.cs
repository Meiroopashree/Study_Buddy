namespace StudyBuddy.Models
{
    public class Bookmark
    {
        public int Id { get; set; }
        public int? UserId { get; set; }
        public User? User { get; set; }
        public int TopicId { get; set; }
        public Topic Topic { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
