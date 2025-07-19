using AI_Agent_SemanticKernel_Chat.Models;

namespace AI_Agent_SemanticKernel_Chat.Repositories
{
    public interface IChatHistoryRepository
    {
        Task<ChatSession> CreateSessionAsync(string userId, string title, CancellationToken cancellationToken = default);
        Task<ChatSession?> GetSessionAsync(Guid sessionId, CancellationToken cancellationToken = default);
        Task<List<ChatSession>> GetUserSessionsAsync(string userId, int limit = 50, CancellationToken cancellationToken = default);
        Task SaveMessageAsync(Guid sessionId, string role, string content, Dictionary<string, object>? metadata = null, CancellationToken cancellationToken = default);
        Task UpdateSessionAsync(ChatSession session, CancellationToken cancellationToken = default);
        Task DeleteSessionAsync(Guid sessionId, CancellationToken cancellationToken = default);
        Task<bool> SessionExistsAsync(Guid sessionId, CancellationToken cancellationToken = default);
    }
}
