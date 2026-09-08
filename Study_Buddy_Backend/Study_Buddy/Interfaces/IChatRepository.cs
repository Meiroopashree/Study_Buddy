using StudyBuddy.Models;

namespace StudyBuddy.Interfaces
{
    public interface IChatRepository
    {
        Task SaveAsync(ChatMessage message);
        Task<List<ChatMessage>> GetRecentAsync(int count = 10);
    }
}
