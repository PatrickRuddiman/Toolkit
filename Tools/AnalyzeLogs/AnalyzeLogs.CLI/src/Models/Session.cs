namespace AnalyzeLogs.Models;

/// <summary>
/// Represents an analysis session within a project.
/// </summary>
public class Session
{
    /// <summary>
    /// Unique identifier for the analysis session.
    /// </summary>
    public Guid SessionId { get; set; }

    /// <summary>
    /// Foreign key linking to the parent project.
    /// </summary>
    public Guid ProjectId { get; set; }

    /// <summary>
    /// Timestamp when the analysis session began.
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// Timestamp when the analysis session completed or was terminated.
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// Current status of the session (e.g., "Initialized", "Ingesting", "Parsing", "Analyzing", "Reporting", "Completed", "Failed", "Cancelled").
    /// </summary>
    public required string Status { get; set; }

    /// <summary>
    /// Number of log files processed in the session.
    /// </summary>
    public int? LogFileCount { get; set; }

    /// <summary>
    /// Total number of log entries analyzed.
    /// </summary>
    public int? AnalyzedLogEntryCount { get; set; }

    /// <summary>
    /// The glob pattern used for this specific session.
    /// </summary>
    public string? RawInputGlobPattern { get; set; }

    /// <summary>
    /// Path to the generated report file for this session.
    /// </summary>
    public string? ReportFilePath { get; set; }

    /// <summary>
    /// Navigation property for the parent project.
    /// </summary>
    public virtual Project Project { get; set; } = null!;

    /// <summary>
    /// Navigation property for log entries belonging to this session.
    /// </summary>
    public virtual ICollection<LogEntry> LogEntries { get; set; } = new List<LogEntry>();

    /// <summary>
    /// Navigation property for anomalies detected in this session.
    /// </summary>
    public virtual ICollection<Anomaly> Anomalies { get; set; } = new List<Anomaly>();
}
