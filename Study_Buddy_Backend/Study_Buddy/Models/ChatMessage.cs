namespace StudyBuddy.Models
{
    public class ChatMessage
    {
        public int Id { get; set; }
        public string Question { get; set; }
        public string Response { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}