namespace StudyBuddy.Models
{
    public class Topic
    {
        public int Id { get; set; }
        public int ChapterId { get; set; }
        public Chapter Chapter { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = "";
        public string LessonContent { get; set; } = "";
        public string NotesContent { get; set; } = "";
        public string RevisionContent { get; set; } = "";
        public string FormulaSheet { get; set; } = "";
        public string ConceptMap { get; set; } = "";
        public DateTime UpdatedAt { get; set; }
    }
}
