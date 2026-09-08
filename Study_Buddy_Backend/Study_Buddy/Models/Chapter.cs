namespace StudyBuddy.Models
{
    public class Chapter
    {
        public int Id { get; set; }
        public int SubjectId { get; set; }
        public Subject Subject { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Summary { get; set; } = "";
        public List<Topic> Topics { get; set; } = new List<Topic>();
    }
}
