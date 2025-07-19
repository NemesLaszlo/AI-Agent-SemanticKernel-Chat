using AI_Agent_SemanticKernel_Chat.Models;

namespace AI_Agent_SemanticKernel_Chat.Services
{
    public interface IChatService
    {
        Task<ChatSession> StartNewSessionAsync(string userId, string title = "New Chat", CancellationToken cancellationToken = default);
        Task<ChatSession?> GetSessionAsync(Guid sessionId, CancellationToken cancellationToken = default);
        Task<List<ChatSession>> GetUserSessionsAsync(string userId, CancellationToken cancellationToken = default);
        Task<string> SendMessageAsync(Guid sessionId, string message, CancellationToken cancellationToken = default);
        IAsyncEnumerable<string> SendMessageStreamAsync(Guid sessionId, string message, CancellationToken cancellationToken = default);
        Task DeleteSessionAsync(Guid sessionId, CancellationToken cancellationToken = default);
    }
}
