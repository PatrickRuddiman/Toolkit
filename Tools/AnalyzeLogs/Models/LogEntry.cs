using System.Text.Json.Serialization;

namespace AnalyzeLogs.Models;

/// <summary>
/// Represents a normalized log entry with common fields across different log formats
/// </summary>
public class LogEntry
{
    /// <summary>
    /// Unique identifier for this log entry
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Timestamp when the log event occurred (normalized to UTC)
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Log level (Error, Warning, Info, Debug, etc.)
    /// </summary>
    public LogLevel Level { get; set; }

    /// <summary>
    /// Service or source component that generated the log
    /// </summary>
    public string? Service { get; set; }

    /// <summary>
    /// The main log message
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Correlation ID for tracing requests across services
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// Trace ID for distributed tracing
    /// </summary>
    public string? TraceId { get; set; }

    /// <summary>
    /// User ID if available
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// HTTP status code if applicable
    /// </summary>
    public int? HttpStatus { get; set; }

    /// <summary>
    /// Response time/latency in milliseconds
    /// </summary>
    public double? ResponseTimeMs { get; set; }

    /// <summary>
    /// Source file path where this log was found
    /// </summary>
    public string SourceFile { get; set; } = string.Empty;

    /// <summary>
    /// Line number in the source file
    /// </summary>
    public int LineNumber { get; set; }

    /// <summary>
    /// Additional structured data as key-value pairs
    /// </summary>
    public Dictionary<string, object?> AdditionalData { get; set; } = new();

    /// <summary>
    /// Semantic embedding vector for this log entry (if generated)
    /// </summary>
    [JsonIgnore]
    public float[]? Embedding { get; set; }

    /// <summary>
    /// Tags assigned to this log entry (manually or by AI)
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// Whether this log entry was flagged as an anomaly
    /// </summary>
    public bool IsAnomaly { get; set; }

    /// <summary>
    /// Anomaly score (0.0 to 1.0, higher means more anomalous)
    /// </summary>
    public double AnomalyScore { get; set; }

    /// <summary>
    /// Raw original log line before parsing
    /// </summary>
    public string RawContent { get; set; } = string.Empty;
}

/// <summary>
/// Log levels in order of severity
/// </summary>
public enum LogLevel
{
    Trace = 0,
    Debug = 1,
    Info = 2,
    Warning = 3,
    Error = 4,
    Critical = 5
}
