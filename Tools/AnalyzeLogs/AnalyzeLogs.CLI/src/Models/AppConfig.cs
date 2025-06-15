using System.Text.Json.Serialization;

namespace AnalyzeLogs.Models;

/// <summary>
/// Global application configuration settings.
/// </summary>
public class AppConfig
{
    /// <summary>
    /// Path to the SQLite database file.
    /// </summary>
    public string DatabasePath { get; set; } =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".loganalyzer",
            "data",
            "loganalysis.db"
        );

    /// <summary>
    /// Default AI configuration to use if a project doesn't specify one.
    /// </summary>
    public string DefaultAiConfigName { get; set; } = "DefaultOpenAI";

    /// <summary>
    /// Verbosity level for the LogAnalyzer tool's own operational logs.
    /// </summary>
    public string ApplicationLogLevel { get; set; } = "INFO";

    /// <summary>
    /// Default directory where reports are saved if --output-path is not specified.
    /// </summary>
    public string DefaultReportOutputPath { get; set; } =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "LogAnalyzerReports"
        );

    /// <summary>
    /// Settings for OpenAI API integration.
    /// </summary>
    public OpenAISettings OpenAI { get; set; } = new();

    /// <summary>
    /// The path to the application configuration file.
    /// </summary>
    [JsonIgnore]
    public static string ConfigFilePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".loganalyzer",
            "config.json"
        );
}

/// <summary>
/// Configuration settings for OpenAI API integration.
/// </summary>
public class OpenAISettings
{
    /// <summary>
    /// OpenAI API key. If not provided, the OPENAI_API_KEY environment variable will be used.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Base URL for the OpenAI API. Use this to specify a different API endpoint (like Azure OpenAI Service).
    /// </summary>
    public string? BaseUrl { get; set; }

    /// <summary>
    /// The model to use for general-purpose analysis tasks.
    /// </summary>
    public string GeneralModel { get; set; } = "gpt-4o";

    /// <summary>
    /// The model to use for research tasks.
    /// </summary>
    public string ResearchModel { get; set; } = "gpt-4o-search-preview";

    /// <summary>
    /// The model to use for generating embeddings.
    /// </summary>
    public string EmbeddingModel { get; set; } = "text-embedding-3-small";

    /// <summary>
    /// Maximum number of retries for API calls.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Whether to enable rate limiting for API calls.
    /// </summary>
    public bool EnableRateLimiting { get; set; } = true;

    /// <summary>
    /// Maximum number of requests per minute when rate limiting is enabled.
    /// </summary>
    public int RequestsPerMinute { get; set; } = 60;

    /// <summary>
    /// Whether to enable streaming responses for chat completions.
    /// </summary>
    public bool EnableStreaming { get; set; } = false;
}
