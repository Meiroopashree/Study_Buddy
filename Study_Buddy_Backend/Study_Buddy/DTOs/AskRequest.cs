namespace StudyBuddy.DTOs
{
    public class AskRequest
    {
        public string Question { get; set; } = string.Empty;
        public List<HistoryItem>? History { get; set; }
        public List<int>? DocumentIds { get; set; }
        public string? ImageUrl { get; set; }
        public string? Mode { get; set; }
    }

    public class HistoryItem
    {
        public string Role { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }
}