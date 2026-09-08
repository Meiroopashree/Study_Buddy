namespace StudyBuddy.Models
{
    public class QuizResponse
    {
        public string Topic { get; set; } = string.Empty;
        public List<Question> Questions { get; set; } = new List<Question>();
    }
}
