namespace AnalyzeLogs.Models;

/// <summary>
/// Represents a microservice or component derived from log source or content.
/// </summary>
public class Service
{
    /// <summary>
    /// Unique ID for the service.
    /// </summary>
    public int ServiceId { get; set; }

    /// <summary>
    /// Name of the microservice or component derived from log source or content.
    /// </summary>
    public required string ServiceName { get; set; }

    /// <summary>
    /// Navigation property for log entries from this service.
    /// </summary>
    public virtual ICollection<LogEntry> LogEntries { get; set; } = new List<LogEntry>();
}
