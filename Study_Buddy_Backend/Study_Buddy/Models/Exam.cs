namespace StudyBuddy.Models
{
    /// <summary>
    /// A user-created exam (for example "NEET", "JEE Main", "MHT-CET", ...).
    /// The full syllabus tree is stored on Subject rows whose Exam matches <see cref="Name"/>.
    /// </summary>
    public class Exam
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Year { get; set; } = DateTime.Now.Year;
        public string Description { get; set; } = "";
        public DateTime CreatedAt { get; set; }
    }
}