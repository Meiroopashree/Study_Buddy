namespace StudyBuddy.Models
{
    public class Question
    {
        public string Type { get; set; } = "mcq"; // mcq, multi, numerical, assertion, matching, truefalse
        public string QuestionText { get; set; } = string.Empty;
        public List<string>? Options { get; set; } // only for choice-based types
        public string Answer { get; set; } = string.Empty;
        public List<string>? CorrectAnswers { get; set; } // only for multi
        public string? Assertion { get; set; } // only for assertion
        public string? Reason { get; set; } // only for assertion
        public List<string>? RightOptions { get; set; } // only for matching (List-II)
        public string? Explanation { get; set; }
        public string? Passage { get; set; } // only for passage/comprehension
    }
}
