namespace AI_Agent_SemanticKernel_Chat.Models
{
    public class AppSettings
    {
        public string OllamaApiUrl { get; set; } = "http://localhost:11434";
        public string DefaultModel { get; set; } = "gemma2:2b";
        public string ConnectionString { get; set; } = string.Empty;
        public int MaxHistoryMessages { get; set; } = 100;
        public int CommandTimeoutSeconds { get; set; } = 30;
        public bool EnableStreamingResponse { get; set; } = true;
    }
}
