namespace StudyBuddy.Models
{
    public class User
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string? GoogleId { get; set; }
        public string Provider { get; set; } = "email";
        public string Token { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}
