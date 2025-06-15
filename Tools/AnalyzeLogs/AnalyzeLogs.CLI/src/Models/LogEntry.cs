using System.ComponentModel.DataAnnotations.Schema;

namespace AnalyzeLogs.Models;

/// <summary>
/// Represents a single log entry processed from a log file.
/// </summary>
public class LogEntry
{
    /// <summary>
    /// Unique identifier for each log entry.
    /// </summary>
    public long LogEntryId { get; set; }

    /// <summary>
    /// Foreign key linking to the session this log entry belongs to.
    /// </summary>
    public Guid SessionId { get; set; }

    /// <summary>
    /// The normalized timestamp of the log entry in UTC.
    /// </summary>
    public DateTime TimestampUTC { get; set; }

    /// <summary>
    /// The timestamp string as it appeared in the raw log.
    /// </summary>
    public string? OriginalTimestamp { get; set; }

    /// <summary>
    /// The original timezone of the timestamp, if detected.
    /// </summary>
    public string? OriginalTimeZone { get; set; }

    /// <summary>
    /// The format detected for this log entry (e.g., "JSON", "NginxAccess", "Syslog", "Unstructured").
    /// </summary>
    public string? DetectedFormat { get; set; }

    /// <summary>
    /// The name of the file this log entry originated from.
    /// </summary>
    public string? SourceFileName { get; set; }

    /// <summary>
    /// The full path to the source log file.
    /// </summary>
    public string? SourceFilePath { get; set; }

    /// <summary>
    /// The original, unaltered log line.
    /// </summary>
    public required string RawMessage { get; set; }

    /// <summary>
    /// The processed and potentially cleaned-up log message content.
    /// </summary>
    public string? NormalizedMessage { get; set; }

    /// <summary>
    /// Foreign key linking to the SeverityLevel table.
    /// </summary>
    public int? SeverityLevelId { get; set; }

    /// <summary>
    /// Foreign key linking to the Service table.
    /// </summary>
    public int? ServiceId { get; set; }

    /// <summary>
    /// Extracted correlation ID (e.g., trace ID, request ID).
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// Extracted thread ID, if available.
    /// </summary>
    public string? ThreadId { get; set; }

    /// <summary>
    /// Extracted process ID, if available.
    /// </summary>
    public string? ProcessId { get; set; }

    /// <summary>
    /// A JSON string storing other structured fields extracted from the log entry (e.g., user ID, session ID, custom key-value pairs).
    /// </summary>
    public string? AdditionalDataJson { get; set; }

    /// <summary>
    /// Stores the semantic vector embedding of the NormalizedMessage.
    /// </summary>
    public byte[]? EmbeddingVector { get; set; }

    /// <summary>
    /// Navigation property for the session this log entry belongs to.
    /// </summary>
    public virtual Session Session { get; set; } = null!;

    /// <summary>
    /// Navigation property for the severity level of this log entry.
    /// </summary>
    public virtual SeverityLevel? SeverityLevel { get; set; }

    /// <summary>
    /// Navigation property for the service this log entry belongs to.
    /// </summary>
    public virtual Service? Service { get; set; }

    /// <summary>
    /// Navigation property for anomalies related to this log entry.
    /// </summary>
    public virtual ICollection<AnomalyLogEntry> AnomalyLogEntries { get; set; } =
        new List<AnomalyLogEntry>();
}
