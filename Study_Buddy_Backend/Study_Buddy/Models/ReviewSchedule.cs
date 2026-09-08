namespace StudyBuddy.Models
{
    public class ReviewSchedule
    {
        public int Id { get; set; }
        public int? UserId { get; set; }
        public User? User { get; set; }
        public int TopicId { get; set; }
        public Topic Topic { get; set; }
        public DateTime DueDate { get; set; }
        public int IntervalDays { get; set; } = 1;
        public double EaseFactor { get; set; } = 2.5;
        public DateTime? LastReviewDate { get; set; }
        public int ReviewCount { get; set; } = 0;
        public DateTime CreatedAt { get; set; }
    }
}