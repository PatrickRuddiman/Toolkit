namespace AnalyzeLogs.Models;

/// <summary>
/// Junction table for the many-to-many relationship between Anomaly and LogEntry.
/// </summary>
public class AnomalyLogEntry
{
    /// <summary>
    /// Foreign key to the Anomaly.
    /// </summary>
    public int AnomalyId { get; set; }

    /// <summary>
    /// Foreign key to the LogEntry.
    /// </summary>
    public long LogEntryId { get; set; }

    /// <summary>
    /// Relevance score indicating how strongly this log entry is related to the anomaly (0-100).
    /// </summary>
    public double? RelevanceScore { get; set; }

    /// <summary>
    /// Optional notes about why this log entry is related to the anomaly.
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Navigation property for the anomaly.
    /// </summary>
    public virtual Anomaly Anomaly { get; set; } = null!;

    /// <summary>
    /// Navigation property for the log entry.
    /// </summary>
    public virtual LogEntry LogEntry { get; set; } = null!;
}
