using AI_Agent_SemanticKernel_Chat.Models;
using AI_Agent_SemanticKernel_Chat.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using System.Text;

namespace AI_Agent_SemanticKernel_Chat.Services
{
    public class ChatService : IChatService
    {
        private readonly IChatHistoryRepository _repository;
        private readonly IChatCompletionService _chatCompletionService;
        private readonly AppSettings _appSettings;
        private readonly ILogger<ChatService> _logger;

        public ChatService(
            IChatHistoryRepository repository,
            IChatCompletionService chatCompletionService,
            AppSettings appSettings,
            ILogger<ChatService> logger)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _chatCompletionService = chatCompletionService ?? throw new ArgumentNullException(nameof(chatCompletionService));
            _appSettings = appSettings ?? throw new ArgumentNullException(nameof(appSettings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<ChatSession> StartNewSessionAsync(string userId, string title = "New Chat", CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Starting new chat session for user {UserId}", userId);

                var session = await _repository.CreateSessionAsync(userId, title, cancellationToken);

                // Add system message
                await _repository.SaveMessageAsync(
                    session.Id,
                    "system",
                    "You are a helpful assistant that will help with questions.",
                    cancellationToken: cancellationToken);

                return session;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start new chat session for user {UserId}", userId);
                throw;
            }
        }

        public async Task<ChatSession?> GetSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
        {
            try
            {
                return await _repository.GetSessionAsync(sessionId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve session {SessionId}", sessionId);
                throw;
            }
        }

        public async Task<List<ChatSession>> GetUserSessionsAsync(string userId, CancellationToken cancellationToken = default)
        {
            try
            {
                return await _repository.GetUserSessionsAsync(userId, cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve sessions for user {UserId}", userId);
                throw;
            }
        }

        public async Task<string> SendMessageAsync(Guid sessionId, string message, CancellationToken cancellationToken = default)
        {
            try
            {
                var response = new StringBuilder();
                await foreach (var chunk in SendMessageStreamAsync(sessionId, message, cancellationToken))
                {
                    response.Append(chunk);
                }
                return response.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send message to session {SessionId}", sessionId);
                throw;
            }
        }

        public async IAsyncEnumerable<string> SendMessageStreamAsync(Guid sessionId, string message, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var session = await _repository.GetSessionAsync(sessionId, cancellationToken);
            if (session == null)
            {
                throw new InvalidOperationException($"Chat session {sessionId} not found");
            }

            // Save user message
            await _repository.SaveMessageAsync(sessionId, "user", message, cancellationToken: cancellationToken);

            // Build chat history from stored messages
            var chatHistory = new ChatHistory();
            foreach (var msg in session.Messages.OrderBy(m => m.Timestamp))
            {
                chatHistory.Add(new ChatMessageContent(
                    msg.Role.ToLowerInvariant() switch
                    {
                        "user" => AuthorRole.User,
                        "system" => AuthorRole.System,
                        _ => AuthorRole.Assistant
                    },
                    msg.Content));
            }

            // Add the new user message to chat history
            chatHistory.AddUserMessage(message);

            var responseBuilder = new StringBuilder();

            // Get streaming response
            await foreach (var response in _chatCompletionService.GetStreamingChatMessageContentsAsync(chatHistory, cancellationToken: cancellationToken))
            {
                if (!string.IsNullOrEmpty(response.Content))
                {
                    responseBuilder.Append(response.Content);
                    yield return response.Content;
                }
            }

            // Save assistant response
            var fullResponse = responseBuilder.ToString();
            if (!string.IsNullOrEmpty(fullResponse))
            {
                await _repository.SaveMessageAsync(sessionId, "assistant", fullResponse, cancellationToken: cancellationToken);
            }

            _logger.LogDebug("Completed message exchange for session {SessionId}", sessionId);
        }

        public async Task DeleteSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
        {
            try
            {
                await _repository.DeleteSessionAsync(sessionId, cancellationToken);
                _logger.LogInformation("Deleted chat session {SessionId}", sessionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete session {SessionId}", sessionId);
                throw;
            }
        }
    }
}
