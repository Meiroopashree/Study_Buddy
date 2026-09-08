using Microsoft.EntityFrameworkCore;
using StudyBuddy.Data;
using StudyBuddy.Interfaces;
using StudyBuddy.Models;

namespace StudyBuddy.Repositories
{
    public class ChatRepository : IChatRepository
    {
        private readonly StudyBuddyContext _context;

        public ChatRepository(StudyBuddyContext context)
        {
            _context = context;
        }

        public async Task SaveAsync(ChatMessage message)
        {
            _context.ChatHistory.Add(message);
            await _context.SaveChangesAsync();
        }

        public async Task<List<ChatMessage>> GetRecentAsync(int count = 10)
        {
            return await _context.ChatHistory
                .OrderByDescending(c => c.CreatedAt)
                .Take(count)
                .OrderBy(c => c.CreatedAt)
                .ToListAsync();
        }
    }
}