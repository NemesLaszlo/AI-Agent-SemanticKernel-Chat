using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using AI_Agent_SemanticKernel_Chat.Services;
using AI_Agent_SemanticKernel_Chat.Repositories;
using AI_Agent_SemanticKernel_Chat.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using OllamaSharp;

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development"}.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/ai-agent-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

try
{
    var host = Host.CreateDefaultBuilder(args)
        .UseSerilog()
        .ConfigureServices((context, services) =>
        {
            var appSettings = new AppSettings();
            configuration.Bind(appSettings);
            services.AddSingleton(appSettings);

            // Register Semantic Kernel
            var builder = Kernel.CreateBuilder();
            builder.Services.AddScoped<IOllamaApiClient>(_ => new OllamaApiClient(appSettings.OllamaApiUrl));
            builder.Services.AddScoped<IChatCompletionService, OllamaChatCompletionService>();

            services.AddSingleton(provider => builder.Build());

            // Register repositories and services
            services.AddScoped<IChatHistoryRepository, PostgreSqlChatHistoryRepository>();
            services.AddScoped<IChatService, ChatService>();
            services.AddHostedService<ChatConsoleService>();
        })
        .Build();

    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}