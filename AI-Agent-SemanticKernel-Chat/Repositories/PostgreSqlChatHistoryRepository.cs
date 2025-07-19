using AI_Agent_SemanticKernel_Chat.Models;
using Microsoft.Extensions.Logging;
using Npgsql;
using System.Data;
using System.Text;
using System.Text.Json;

namespace AI_Agent_SemanticKernel_Chat.Repositories
{
    public class PostgreSqlChatHistoryRepository : IChatHistoryRepository
    {
        private readonly string _connectionString;
        private readonly ILogger<PostgreSqlChatHistoryRepository> _logger;
        private readonly JsonSerializerOptions _jsonOptions;

        public PostgreSqlChatHistoryRepository(AppSettings appSettings, ILogger<PostgreSqlChatHistoryRepository> logger)
        {
            _connectionString = appSettings.ConnectionString;
            _logger = logger;
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };

            EnsureDatabaseTablesExist().GetAwaiter().GetResult();
        }

        private async Task EnsureDatabaseTablesExist()
        {
            try
            {
                using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();

                var createTablesScript = """
                    CREATE TABLE IF NOT EXISTS chat_sessions (
                        id UUID PRIMARY KEY,
                        user_id VARCHAR(255) NOT NULL,
                        title VARCHAR(500) NOT NULL,
                        created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
                        last_message_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
                        messages JSONB DEFAULT '[]'::jsonb,
                        INDEX (user_id, last_message_at DESC)
                    );

                    CREATE INDEX IF NOT EXISTS idx_chat_sessions_user_id_last_message 
                    ON chat_sessions(user_id, last_message_at DESC);
                    """;

                using var command = new NpgsqlCommand(createTablesScript, connection);
                await command.ExecuteNonQueryAsync();

                _logger.LogInformation("Database tables initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize database tables");
                throw;
            }
        }

        public async Task<ChatSession> CreateSessionAsync(string userId, string title, CancellationToken cancellationToken = default)
        {
            try
            {
                var session = new ChatSession
                {
                    UserId = userId,
                    Title = title
                };

                using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);

                var sql = """
                    INSERT INTO chat_sessions (id, user_id, title, created_at, last_message_at, messages)
                    VALUES (@id, @userId, @title, @createdAt, @lastMessageAt, @messages::jsonb)
                    """;

                using var command = new NpgsqlCommand(sql, connection);
                command.Parameters.AddWithValue("id", session.Id);
                command.Parameters.AddWithValue("userId", session.UserId);
                command.Parameters.AddWithValue("title", session.Title);
                command.Parameters.AddWithValue("createdAt", session.CreatedAt);
                command.Parameters.AddWithValue("lastMessageAt", session.LastMessageAt);
                command.Parameters.AddWithValue("messages", JsonSerializer.Serialize(session.Messages, _jsonOptions));

                await command.ExecuteNonQueryAsync(cancellationToken);

                _logger.LogInformation("Created new chat session {SessionId} for user {UserId}", session.Id, userId);
                return session;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create chat session for user {UserId}", userId);
                throw;
            }
        }

        public async Task<ChatSession?> GetSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
        {
            try
            {
                using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);

                var sql = """
                    SELECT id, user_id, title, created_at, last_message_at, messages
                    FROM chat_sessions 
                    WHERE id = @sessionId
                    """;

                using var command = new NpgsqlCommand(sql, connection);
                command.Parameters.AddWithValue("sessionId", sessionId);

                using var reader = await command.ExecuteReaderAsync(cancellationToken);

                if (await reader.ReadAsync(cancellationToken))
                {
                    var messagesJson = reader.GetString("messages");
                    var messages = JsonSerializer.Deserialize<List<ChatMessage>>(messagesJson, _jsonOptions) ?? new List<ChatMessage>();

                    return new ChatSession
                    {
                        Id = reader.GetGuid("id"),
                        UserId = reader.GetString("user_id"),
                        Title = reader.GetString("title"),
                        CreatedAt = reader.GetDateTime("created_at"),
                        LastMessageAt = reader.GetDateTime("last_message_at"),
                        Messages = messages
                    };
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve chat session {SessionId}", sessionId);
                throw;
            }
        }

        public async Task<List<ChatSession>> GetUserSessionsAsync(string userId, int limit = 50, CancellationToken cancellationToken = default)
        {
            try
            {
                using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);

                var sql = """
                    SELECT id, user_id, title, created_at, last_message_at, messages
                    FROM chat_sessions 
                    WHERE user_id = @userId
                    ORDER BY last_message_at DESC
                    LIMIT @limit
                    """;

                using var command = new NpgsqlCommand(sql, connection);
                command.Parameters.AddWithValue("userId", userId);
                command.Parameters.AddWithValue("limit", limit);

                using var reader = await command.ExecuteReaderAsync(cancellationToken);
                var sessions = new List<ChatSession>();

                while (await reader.ReadAsync(cancellationToken))
                {
                    var messagesJson = reader.GetString("messages");
                    var messages = JsonSerializer.Deserialize<List<ChatMessage>>(messagesJson, _jsonOptions) ?? new List<ChatMessage>();

                    sessions.Add(new ChatSession
                    {
                        Id = reader.GetGuid("id"),
                        UserId = reader.GetString("user_id"),
                        Title = reader.GetString("title"),
                        CreatedAt = reader.GetDateTime("created_at"),
                        LastMessageAt = reader.GetDateTime("last_message_at"),
                        Messages = messages
                    });
                }

                return sessions;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve chat sessions for user {UserId}", userId);
                throw;
            }
        }

        public async Task SaveMessageAsync(Guid sessionId, string role, string content, Dictionary<string, object>? metadata = null, CancellationToken cancellationToken = default)
        {
            try
            {
                var message = new ChatMessage
                {
                    Role = role,
                    Content = content,
                    Metadata = metadata
                };

                using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);

                var sql = """
                    UPDATE chat_sessions 
                    SET messages = messages || @message::jsonb,
                        last_message_at = @timestamp
                    WHERE id = @sessionId
                    """;

                using var command = new NpgsqlCommand(sql, connection);
                command.Parameters.AddWithValue("sessionId", sessionId);
                command.Parameters.AddWithValue("message", JsonSerializer.Serialize(new[] { message }, _jsonOptions));
                command.Parameters.AddWithValue("timestamp", DateTime.UtcNow);

                var rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken);

                if (rowsAffected == 0)
                {
                    throw new InvalidOperationException($"Chat session {sessionId} not found");
                }

                _logger.LogDebug("Saved message to session {SessionId}", sessionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save message to session {SessionId}", sessionId);
                throw;
            }
        }

        public async Task UpdateSessionAsync(ChatSession session, CancellationToken cancellationToken = default)
        {
            try
            {
                using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);

                var sql = """
                    UPDATE chat_sessions 
                    SET title = @title,
                        last_message_at = @lastMessageAt,
                        messages = @messages::jsonb
                    WHERE id = @sessionId
                    """;

                using var command = new NpgsqlCommand(sql, connection);
                command.Parameters.AddWithValue("sessionId", session.Id);
                command.Parameters.AddWithValue("title", session.Title);
                command.Parameters.AddWithValue("lastMessageAt", session.LastMessageAt);
                command.Parameters.AddWithValue("messages", JsonSerializer.Serialize(session.Messages, _jsonOptions));

                await command.ExecuteNonQueryAsync(cancellationToken);

                _logger.LogDebug("Updated chat session {SessionId}", session.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update chat session {SessionId}", session.Id);
                throw;
            }
        }

        public async Task DeleteSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
        {
            try
            {
                using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);

                var sql = "DELETE FROM chat_sessions WHERE id = @sessionId";

                using var command = new NpgsqlCommand(sql, connection);
                command.Parameters.AddWithValue("sessionId", sessionId);

                await command.ExecuteNonQueryAsync(cancellationToken);

                _logger.LogInformation("Deleted chat session {SessionId}", sessionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete chat session {SessionId}", sessionId);
                throw;
            }
        }

        public async Task<bool> SessionExistsAsync(Guid sessionId, CancellationToken cancellationToken = default)
        {
            try
            {
                using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);

                var sql = "SELECT COUNT(1) FROM chat_sessions WHERE id = @sessionId";

                using var command = new NpgsqlCommand(sql, connection);
                command.Parameters.AddWithValue("sessionId", sessionId);

                var count = await command.ExecuteScalarAsync(cancellationToken);
                return Convert.ToInt32(count) > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check if session exists {SessionId}", sessionId);
                throw;
            }
        }
    }
}
