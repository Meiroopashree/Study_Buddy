namespace StudyBuddy.Models
{
    public class Subject
    {
        public int Id { get; set; }
        public string Exam { get; set; } = "General";
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = "";
        public List<Chapter> Chapters { get; set; } = new List<Chapter>();
    }
}
