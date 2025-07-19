using AI_Agent_SemanticKernel_Chat.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AI_Agent_SemanticKernel_Chat.Services
{
    public class ChatConsoleService : BackgroundService
    {
        private readonly IChatService _chatService;
        private readonly ILogger<ChatConsoleService> _logger;
        private readonly IHostApplicationLifetime _applicationLifetime;

        public ChatConsoleService(
            IChatService chatService,
            ILogger<ChatConsoleService> logger,
            IHostApplicationLifetime applicationLifetime)
        {
            _chatService = chatService ?? throw new ArgumentNullException(nameof(chatService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _applicationLifetime = applicationLifetime ?? throw new ArgumentNullException(nameof(applicationLifetime));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                _logger.LogInformation("Starting AI Agent Console Interface");

                Console.WriteLine("=== AI Agent Console Interface ===");
                Console.WriteLine("Commands:");
                Console.WriteLine("  /new <title>     - Start a new chat session");
                Console.WriteLine("  /list            - List your chat sessions");
                Console.WriteLine("  /switch <id>     - Switch to a different session");
                Console.WriteLine("  /delete <id>     - Delete a chat session");
                Console.WriteLine("  /exit            - Exit the application");
                Console.WriteLine("  /help            - Show this help message");
                Console.WriteLine();

                const string defaultUserId = "console-user";
                ChatSession? currentSession = null;

                // Start with a new session
                currentSession = await _chatService.StartNewSessionAsync(defaultUserId, "Console Chat", stoppingToken);
                Console.WriteLine($"Started new session: {currentSession.Id}");
                Console.WriteLine();

                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        Console.Write($"[{currentSession?.Id.ToString()[..8] ?? "No Session"}] You: ");
                        var input = await ReadLineAsync(stoppingToken);

                        if (string.IsNullOrWhiteSpace(input))
                        {
                            continue;
                        }

                        // Handle commands
                        if (input.StartsWith("/"))
                        {
                            var (shouldContinue, updatedSession) = await HandleCommandAsync(input, defaultUserId, currentSession, stoppingToken);
                            currentSession = updatedSession;
                            if (!shouldContinue)
                            {
                                break; // Exit command
                            }
                            continue;
                        }

                        // Regular chat message
                        if (currentSession == null)
                        {
                            Console.WriteLine("No active session. Use /new to start a new chat.");
                            continue;
                        }

                        Console.Write("Bot: ");
                        await foreach (var response in _chatService.SendMessageStreamAsync(currentSession.Id, input, stoppingToken))
                        {
                            Console.Write(response);
                        }
                        Console.WriteLine();
                        Console.WriteLine();
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing user input");
                        Console.WriteLine($"Error: {ex.Message}");
                        Console.WriteLine();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fatal error in console service");
            }
            finally
            {
                _applicationLifetime.StopApplication();
            }
        }

        private async Task<(bool ShouldContinue, ChatSession? UpdatedSession)> HandleCommandAsync(
            string command,
            string userId,
            ChatSession? currentSession,
            CancellationToken cancellationToken)
        {
            var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var cmd = parts[0].ToLower();

            try
            {
                switch (cmd)
                {
                    case "/exit":
                    case "/quit":
                        Console.WriteLine("Goodbye!");
                        return (false, currentSession);

                    case "/help":
                        Console.WriteLine("Commands:");
                        Console.WriteLine("  /new <title>     - Start a new chat session");
                        Console.WriteLine("  /list            - List your chat sessions");
                        Console.WriteLine("  /switch <id>     - Switch to a different session");
                        Console.WriteLine("  /delete <id>     - Delete a chat session");
                        Console.WriteLine("  /exit            - Exit the application");
                        Console.WriteLine("  /help            - Show this help message");
                        break;

                    case "/new":
                        var title = parts.Length > 1 ? string.Join(" ", parts.Skip(1)) : "New Console Chat";
                        var newSession = await _chatService.StartNewSessionAsync(userId, title, cancellationToken);
                        Console.WriteLine($"Started new session: {newSession.Id} - {title}");
                        return (true, newSession);

                    case "/list":
                        var sessions = await _chatService.GetUserSessionsAsync(userId, cancellationToken);
                        Console.WriteLine($"Your chat sessions ({sessions.Count}):");
                        foreach (var session in sessions)
                        {
                            var indicator = session.Id == currentSession?.Id ? "*" : " ";
                            Console.WriteLine($"{indicator} {session.Id} - {session.Title} (Last: {session.LastMessageAt:yyyy-MM-dd HH:mm})");
                        }
                        break;

                    case "/switch":
                        if (parts.Length < 2 || !Guid.TryParse(parts[1], out var sessionId))
                        {
                            Console.WriteLine("Usage: /switch <session-id>");
                            break;
                        }

                        var targetSession = await _chatService.GetSessionAsync(sessionId, cancellationToken);
                        if (targetSession == null)
                        {
                            Console.WriteLine("Session not found.");
                        }
                        else
                        {
                            Console.WriteLine($"Switched to session: {targetSession.Id} - {targetSession.Title}");
                            return (true, targetSession);
                        }
                        break;

                    case "/delete":
                        if (parts.Length < 2 || !Guid.TryParse(parts[1], out var deleteId))
                        {
                            Console.WriteLine("Usage: /delete <session-id>");
                            break;
                        }

                        await _chatService.DeleteSessionAsync(deleteId, cancellationToken);
                        Console.WriteLine($"Deleted session: {deleteId}");

                        if (currentSession?.Id == deleteId)
                        {
                            Console.WriteLine("Current session was deleted. Use /new to start a new chat.");
                            return (true, null);
                        }
                        break;

                    default:
                        Console.WriteLine($"Unknown command: {cmd}. Type /help for available commands.");
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling command {Command}", command);
                Console.WriteLine($"Error executing command: {ex.Message}");
            }

            Console.WriteLine();
            return (true, currentSession);
        }

        private static async Task<string?> ReadLineAsync(CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    if (Console.KeyAvailable)
                    {
                        return Console.ReadLine();
                    }
                    Thread.Sleep(50);
                }
                return null;
            }, cancellationToken);
        }
    }
}
