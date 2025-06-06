namespace AnalyzeLogs.Models;

/// <summary>
/// Represents a chunk of logs for analysis
/// </summary>
public class LogChunk
{
    /// <summary>
    /// Unique identifier for this chunk
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Log entries in this chunk
    /// </summary>
    public List<LogEntry> Entries { get; set; } = new();

    /// <summary>
    /// Start timestamp of this chunk
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// End timestamp of this chunk
    /// </summary>
    public DateTime EndTime { get; set; }

    /// <summary>
    /// Services represented in this chunk
    /// </summary>
    public HashSet<string> Services { get; set; } = new();

    /// <summary>
    /// Correlation IDs found in this chunk
    /// </summary>
    public HashSet<string> CorrelationIds { get; set; } = new();

    /// <summary>
    /// Estimated token count for LLM processing
    /// </summary>
    public int EstimatedTokenCount { get; set; }
}

/// <summary>
/// Represents an anomaly detected in logs
/// </summary>
public class Anomaly
{
    /// <summary>
    /// Unique identifier for this anomaly
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Type of anomaly (Error, Pattern, Sequence, etc.)
    /// </summary>
    public AnomalyType Type { get; set; }

    /// <summary>
    /// Severity of the anomaly
    /// </summary>
    public AnomalySeverity Severity { get; set; }

    /// <summary>
    /// Timestamp when the anomaly occurred
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Service where the anomaly was detected
    /// </summary>
    public string? Service { get; set; }

    /// <summary>
    /// Description of the anomaly
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Log entries related to this anomaly
    /// </summary>
    public List<LogEntry> RelatedEntries { get; set; } = new();

    /// <summary>
    /// Confidence score (0.0 to 1.0)
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    /// Tags associated with this anomaly
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// Suggested actions or remediation
    /// </summary>
    public string? Recommendation { get; set; }

    /// <summary>
    /// Detailed information about the anomaly
    /// </summary>
    public string Details => Description;

    /// <summary>
    /// IDs of related log entries
    /// </summary>
    public List<string> RelatedLogIds => RelatedEntries.Select(e => e.Id).ToList();
}

/// <summary>
/// Types of anomalies that can be detected
/// </summary>
public enum AnomalyType
{
    Error,
    Pattern,
    Sequence,
    Performance,
    Security,
    Correlation,
    Volume,
    Other
}

/// <summary>
/// Severity levels for anomalies
/// </summary>
public enum AnomalySeverity
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

/// <summary>
/// Represents correlation between log entries
/// </summary>
public class LogCorrelation
{
    /// <summary>
    /// Correlation ID or trace ID
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Log entries in this correlation group
    /// </summary>
    public List<LogEntry> Entries { get; set; } = new();

    /// <summary>
    /// Services involved in this correlation
    /// </summary>
    public HashSet<string> Services { get; set; } = new();

    /// <summary>
    /// Start time of the correlated transaction
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// End time of the correlated transaction
    /// </summary>
    public DateTime EndTime { get; set; }

    /// <summary>
    /// Total duration of the transaction
    /// </summary>
    public TimeSpan Duration => EndTime - StartTime;

    /// <summary>
    /// Whether this correlation contains errors
    /// </summary>
    public bool HasErrors => Entries.Any(e => e.Level >= LogLevel.Error);

    /// <summary>
    /// Success status of the transaction
    /// </summary>
    public bool IsSuccessful { get; set; } = true;
}

/// <summary>
/// Represents metrics for a service
/// </summary>
public class ServiceMetrics
{
    /// <summary>
    /// Service name
    /// </summary>
    public string ServiceName { get; set; } = string.Empty;

    /// <summary>
    /// Total number of log entries
    /// </summary>
    public int TotalEntries { get; set; }

    /// <summary>
    /// Total number of requests processed
    /// </summary>
    public int TotalRequests { get; set; }

    /// <summary>
    /// Number of error entries
    /// </summary>
    public int ErrorCount { get; set; }

    /// <summary>
    /// Number of warning entries
    /// </summary>
    public int WarningCount { get; set; }

    /// <summary>
    /// Error rate (errors / total entries)
    /// </summary>
    public double ErrorRate => TotalEntries > 0 ? (double)ErrorCount / TotalEntries : 0;

    /// <summary>
    /// Average response time in milliseconds
    /// </summary>
    public double? AverageResponseTime { get; set; }

    /// <summary>
    /// 95th percentile response time
    /// </summary>
    public double? P95ResponseTime { get; set; }

    /// <summary>
    /// Request rate (requests per minute)
    /// </summary>
    public double RequestRate { get; set; }

    /// <summary>
    /// Time range for these metrics
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// End time for these metrics
    /// </summary>
    public DateTime EndTime { get; set; }

    /// <summary>
    /// Unique users (if available)
    /// </summary>
    public int UniqueUsers { get; set; }

    /// <summary>
    /// HTTP status code distribution
    /// </summary>
    public Dictionary<int, int> HttpStatusDistribution { get; set; } = new();

    /// <summary>
    /// Tag distribution
    /// </summary>
    public Dictionary<string, int> TagDistribution { get; set; } = new();
}
