namespace AnalyzeLogs.Models;

/// <summary>
/// Represents a standardized log severity level.
/// </summary>
public class SeverityLevel
{
    /// <summary>
    /// Unique ID for the severity level.
    /// </summary>
    public int SeverityLevelId { get; set; }

    /// <summary>
    /// Standardized severity level name (e.g., "TRACE", "DEBUG", "INFO", "WARNING", "ERROR", "CRITICAL", "FATAL").
    /// </summary>
    public required string LevelName { get; set; }

    /// <summary>
    /// Navigation property for log entries with this severity level.
    /// </summary>
    public virtual ICollection<LogEntry> LogEntries { get; set; } = new List<LogEntry>();
}
