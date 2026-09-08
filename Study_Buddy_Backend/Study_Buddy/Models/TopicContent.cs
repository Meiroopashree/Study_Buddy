namespace StudyBuddy.Models
{
    public class TopicContent
    {
        public int Id { get; set; }
        public int? UserId { get; set; }
        public User? User { get; set; }
        public int TopicId { get; set; }
        public Topic Topic { get; set; }
        public string LessonContent { get; set; } = "";
        public string NotesContent { get; set; } = "";
        public string RevisionContent { get; set; } = "";
        public string FormulaSheet { get; set; } = "";
        public string ConceptMap { get; set; } = "";
        public DateTime UpdatedAt { get; set; }
    }
}