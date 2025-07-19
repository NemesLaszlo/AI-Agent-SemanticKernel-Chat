using AI_Agent_SemanticKernel_Chat.Models;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using OllamaSharp;
using OllamaSharp.Models.Chat;
using System.Runtime.CompilerServices;
using System.Text;

namespace AI_Agent_SemanticKernel_Chat.Services
{
    public class OllamaChatCompletionService : IChatCompletionService
    {
        private readonly IOllamaApiClient _ollamaApiClient;
        private readonly AppSettings _appSettings;
        private readonly ILogger<OllamaChatCompletionService> _logger;

        public OllamaChatCompletionService(
            IOllamaApiClient ollamaApiClient,
            AppSettings appSettings,
            ILogger<OllamaChatCompletionService> logger)
        {
            _ollamaApiClient = ollamaApiClient ?? throw new ArgumentNullException(nameof(ollamaApiClient));
            _appSettings = appSettings ?? throw new ArgumentNullException(nameof(appSettings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public IReadOnlyDictionary<string, object?> Attributes => new Dictionary<string, object?>
        {
            { "model", _appSettings.DefaultModel },
            { "provider", "Ollama" }
        };

        public async Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(
            ChatHistory chatHistory,
            PromptExecutionSettings? executionSettings = null,
            Kernel? kernel = null,
            CancellationToken cancellationToken = default
        )
        {
            try
            {
                _logger.LogDebug("Processing non-streaming chat completion request");

                if (!_appSettings.EnableStreamingResponse)
                {
                    return await GetDirectChatResponseAsync(chatHistory, executionSettings, cancellationToken);
                }

                // Use streaming API internally for better performance
                var content = new StringBuilder();
                AuthorRole authorRole = AuthorRole.Assistant;

                await foreach (var response in GetStreamingChatMessageContentsAsync(
                    chatHistory, executionSettings, kernel, cancellationToken))
                {
                    if (response.Role.HasValue)
                    {
                        content.Append(response.Content);
                        authorRole = response.Role.Value;
                    }
                }

                return new List<ChatMessageContent>
                {
                    new ChatMessageContent(authorRole, content.ToString())
                    {
                        ModelId = _appSettings.DefaultModel
                    }
                };
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Chat completion request was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing chat completion request");
                throw;
            }
        }

        private async Task<IReadOnlyList<ChatMessageContent>> GetDirectChatResponseAsync(
            ChatHistory chatHistory,
            PromptExecutionSettings? executionSettings,
            CancellationToken cancellationToken)
        {
            var request = CreateChatRequest(chatHistory, streaming: false);
            var messages = new List<ChatMessageContent>();

            try
            {
                await foreach (var response in _ollamaApiClient.ChatAsync(request, cancellationToken))
                {
                    if (response?.Message?.Content != null)
                    {
                        messages.Add(new ChatMessageContent(
                            GetAuthorRole(response.Message?.Role),
                            response.Message?.Content ?? string.Empty)
                        {
                            ModelId = _appSettings.DefaultModel,
                            InnerContent = response
                        });
                    }
                }
                return messages;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in direct chat response");
                throw;
            }
        }

        public async IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(
            ChatHistory chatHistory,
            PromptExecutionSettings? executionSettings = null,
            Kernel? kernel = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var request = CreateChatRequest(chatHistory, streaming: true);
            _logger.LogDebug("Starting streaming chat completion with model {Model}", _appSettings.DefaultModel);

            var messageCount = 0;
            var startTime = DateTime.UtcNow;

            IAsyncEnumerable<ChatResponseStream?> responseStream;
            try
            {
                responseStream = _ollamaApiClient.ChatAsync(request, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing streaming chat completion");
                throw;
            }

            await foreach (var response in responseStream.WithCancellation(cancellationToken))
            {
                if (response?.Message?.Content == null)
                {
                    continue; // Skip empty responses
                }

                messageCount++;
                yield return new StreamingChatMessageContent(
                    role: GetAuthorRole(response.Message.Role),
                    content: response.Message.Content,
                    innerContent: response,
                    modelId: _appSettings.DefaultModel
                );
            }

            var duration = DateTime.UtcNow - startTime;
            _logger.LogDebug("Completed streaming response in {Duration}ms with {MessageCount} chunks",
                duration.TotalMilliseconds, messageCount);
        }

        private static AuthorRole GetAuthorRole(ChatRole? role)
        {
            return role?.ToString().ToUpperInvariant() switch
            {
                "USER" => AuthorRole.User,
                "ASSISTANT" => AuthorRole.Assistant,
                "SYSTEM" => AuthorRole.System,
                _ => AuthorRole.Assistant // Default to Assistant
            };
        }

        private ChatRequest CreateChatRequest(ChatHistory chatHistory, bool streaming = true)
        {
            var messages = new List<Message>();

            // Limit history to prevent context overflow
            var messagesToInclude = chatHistory.Count > _appSettings.MaxHistoryMessages
                ? chatHistory.Skip(chatHistory.Count - _appSettings.MaxHistoryMessages)
                : chatHistory;

            foreach (var message in messagesToInclude)
            {
                ChatRole role;

                if (message.Role == AuthorRole.User)
                    role = ChatRole.User;
                else if (message.Role == AuthorRole.System)
                    role = ChatRole.System;
                else
                    role = ChatRole.Assistant;

                messages.Add(new Message
                {
                    Role = role,
                    Content = message.Content ?? string.Empty,
                });
            }

            return new ChatRequest
            {
                Messages = messages,
                Stream = streaming,
                Model = _appSettings.DefaultModel
            };
        }
    }
}
