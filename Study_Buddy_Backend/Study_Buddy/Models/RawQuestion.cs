namespace StudyBuddy.Models
{
    public class RawQuestion
    {
        public int id { get; set; }
        public string question { get; set; }
        public List<string> options { get; set; }
        public string correct_answer { get; set; }
        public string explanation { get; set; }
        public string difficulty { get; set; }
        public string topic { get; set; }
        public string? type { get; set; }
        public List<string>? correct_answers { get; set; }
        public string? assertion { get; set; }
        public string? reason { get; set; }
        public List<string>? right_options { get; set; }
        public string? answer_map { get; set; }
        public string? passage { get; set; }
    }
}
