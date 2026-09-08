using StudyBuddy.Models;

namespace StudyBuddy.Interfaces
{
    public interface IMistralService
    {
        Task<string> AskAI(string question, List<ChatMessage>? history = null, string? documentContext = null, string? imageUrl = null, string? mode = null);
        Task<string> SendRawPrompt(string prompt);
        Task<string> TranscribeImage(string imageDataUri);
        Task<Stream> AskAIStream(string question, List<ChatMessage>? history = null, string? documentContext = null, string? imageUrl = null, string? mode = null);
    }
}
