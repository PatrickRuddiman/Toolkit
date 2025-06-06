namespace AnalyzeLogs.Models;

/// <summary>
/// Configuration for the log analysis application
/// </summary>
public class Configuration
{
    /// <summary>
    /// LLM model to use
    /// </summary>
    public string Model { get; set; } = "gpt-4o-mini";

    /// <summary>
    /// Whether to enable coherence analysis
    /// </summary>
    public bool EnableCoherenceAnalysis { get; set; } = true;

    /// <summary>
    /// Whether to enable anomaly detection
    /// </summary>
    public bool EnableAnomalyDetection { get; set; } = true;

    /// <summary>
    /// Whether to enable tagging
    /// </summary>
    public bool EnableTagging { get; set; } = true;

    /// <summary>
    /// Whether to enable embedding generation
    /// </summary>
    public bool EnableEmbeddings { get; set; } = true;

    /// <summary>
    /// Whether to enable verbose logging
    /// </summary>
    public bool Verbose { get; set; }

    /// <summary>
    /// Temperature for LLM calls
    /// </summary>
    public double Temperature { get; set; } = 0.3;

    /// <summary>
    /// Embedding model to use
    /// </summary>
    public string EmbeddingModel { get; set; } = "text-embedding-3-small";

    /// <summary>
    /// Maximum chunk size for LLM processing (in tokens)
    /// </summary>
    public int MaxChunkSize { get; set; } = 8000;

    /// <summary>
    /// Patterns to redact from logs before sending to AI
    /// </summary>
    public List<string> RedactionPatterns { get; set; } =
        new()
        {
            @"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b", // Email addresses
            @"\b\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}\b", // IP addresses
            @"\b\d{4}-\d{4}-\d{4}-\d{4}\b", // Credit card patterns
            @"password[=:\s]+\S+", // Password fields
            @"token[=:\s]+\S+", // Token fields
            @"key[=:\s]+\S+", // Key fields
        };

    /// <summary>
    /// Minimum anomaly confidence threshold
    /// </summary>
    public double AnomalyThreshold { get; set; } = 0.7;

    /// <summary>
    /// Maximum number of API calls per minute
    /// </summary>
    public int MaxApiCallsPerMinute { get; set; } = 60;

    /// <summary>
    /// Time window for correlation analysis (in minutes)
    /// </summary>
    public int CorrelationWindowMinutes { get; set; } = 5;
}

/// <summary>
/// Configuration for the analysis process
/// </summary>
public class AnalysisConfig
{
    /// <summary>
    /// Input file pattern (glob)
    /// </summary>
    public string FilePattern { get; set; } = string.Empty;

    /// <summary>
    /// Output file path (optional)
    /// </summary>
    public string? OutputFile { get; set; }

    /// <summary>
    /// Whether to enable verbose logging
    /// </summary>
    public bool Verbose { get; set; }

    /// <summary>
    /// Maximum chunk size for LLM processing (in tokens)
    /// </summary>
    public int MaxChunkSize { get; set; } = 8000;

    /// <summary>
    /// Whether to enable embedding generation
    /// </summary>
    public bool EnableEmbeddings { get; set; } = true;

    /// <summary>
    /// Whether to enable AI analysis
    /// </summary>
    public bool EnableAI { get; set; } = true;

    /// <summary>
    /// Whether to enable anomaly detection
    /// </summary>
    public bool EnableAnomalyDetection { get; set; } = true;

    /// <summary>
    /// Whether to enable coherence analysis
    /// </summary>
    public bool EnableCoherenceAnalysis { get; set; } = true;

    /// <summary>
    /// Whether to enable tagging
    /// </summary>
    public bool EnableTagging { get; set; } = true;

    /// <summary>
    /// LLM model to use
    /// </summary>
    public string Model { get; set; } = "gpt-4o-mini";

    /// <summary>
    /// Temperature for LLM calls
    /// </summary>
    public double Temperature { get; set; } = 0.3;

    /// <summary>
    /// Embedding model to use
    /// </summary>
    public string EmbeddingModel { get; set; } = "text-embedding-3-small";

    /// <summary>
    /// Patterns to redact from logs before sending to AI
    /// </summary>
    public List<string> RedactionPatterns { get; set; } =
        new()
        {
            @"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b", // Email addresses
            @"\b\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}\b", // IP addresses
            @"\b\d{4}-\d{4}-\d{4}-\d{4}\b", // Credit card patterns
            @"password[=:\s]+\S+", // Password fields
            @"token[=:\s]+\S+", // Token fields
            @"key[=:\s]+\S+", // Key fields
        };

    /// <summary>
    /// Minimum anomaly confidence threshold
    /// </summary>
    public double AnomalyThreshold { get; set; } = 0.7;

    /// <summary>
    /// Maximum number of API calls per minute
    /// </summary>
    public int MaxApiCallsPerMinute { get; set; } = 60;

    /// <summary>
    /// Time window for correlation analysis (in minutes)
    /// </summary>
    public int CorrelationWindowMinutes { get; set; } = 5;
}

/// <summary>
/// Report configuration
/// </summary>
public class ReportConfig
{
    /// <summary>
    /// Report format (markdown, text, json)
    /// </summary>
    public string Format { get; set; } = "markdown";

    /// <summary>
    /// Maximum number of anomalies to include in report
    /// </summary>
    public int MaxAnomalies { get; set; } = 50;

    /// <summary>
    /// Whether to include detailed correlation information
    /// </summary>
    public bool IncludeCorrelations { get; set; } = true;

    /// <summary>
    /// Whether to include service metrics
    /// </summary>
    public bool IncludeMetrics { get; set; } = true;

    /// <summary>
    /// Whether to include AI recommendations
    /// </summary>
    public bool IncludeRecommendations { get; set; } = true;

    /// <summary>
    /// Time zone for timestamp formatting
    /// </summary>
    public string TimeZone { get; set; } = "UTC";
}
