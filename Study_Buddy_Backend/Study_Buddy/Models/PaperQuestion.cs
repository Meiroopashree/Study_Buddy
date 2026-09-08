namespace StudyBuddy.Models
    {
        public class PaperQuestion
        {
            public int Id { get; set; }
            public int PaperId { get; set; }
            public QuestionPaper? Paper { get; set; }
            public int QuestionNumber { get; set; }
            public string Section { get; set; } = string.Empty;
            public string Type { get; set; } = "mcq";
            public string QuestionText { get; set; } = string.Empty;
            public string OptionsJson { get; set; } = "[]";
            public string CorrectAnswer { get; set; } = string.Empty;
            public string CorrectAnswersJson { get; set; } = "[]";
            public string? Assertion { get; set; }
            public string? Reason { get; set; }
            public string RightOptionsJson { get; set; } = "[]";
            public string? Passage { get; set; }
            public string Explanation { get; set; } = string.Empty;
        }
}
