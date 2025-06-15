namespace AnalyzeLogs.Models;

/// <summary>
/// Represents an anomaly detected during log analysis.
/// </summary>
public class Anomaly
{
    /// <summary>
    /// Unique identifier for the anomaly.
    /// </summary>
    public int AnomalyId { get; set; }

    /// <summary>
    /// Foreign key linking to the session where this anomaly was detected.
    /// </summary>
    public Guid SessionId { get; set; }

    /// <summary>
    /// The type of anomaly (e.g., "Error Spike", "Missing Log", "Unusual Pattern").
    /// </summary>
    public required string AnomalyType { get; set; }

    /// <summary>
    /// AI-generated description of the anomaly.
    /// </summary>
    public required string Description { get; set; }

    /// <summary>
    /// AI-generated possible causes of the anomaly.
    /// </summary>
    public string? PossibleCauses { get; set; }

    /// <summary>
    /// AI-generated suggested actions to remediate the anomaly.
    /// </summary>
    public string? SuggestedRemediation { get; set; }

    /// <summary>
    /// AI-assessed severity of the anomaly (e.g., "Low", "Medium", "High", "Critical").
    /// </summary>
    public string? Severity { get; set; }

    /// <summary>
    /// Timestamp when the anomaly was detected.
    /// </summary>
    public DateTime DetectionTime { get; set; }

    /// <summary>
    /// Estimated start time of the anomaly.
    /// </summary>
    public DateTime? StartTime { get; set; }

    /// <summary>
    /// Estimated end time of the anomaly.
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// Confidence score of the anomaly detection (0-100).
    /// </summary>
    public double? ConfidenceScore { get; set; }

    /// <summary>
    /// Navigation property for the session this anomaly belongs to.
    /// </summary>
    public virtual Session Session { get; set; } = null!;

    /// <summary>
    /// Navigation property for log entries related to this anomaly.
    /// </summary>
    public virtual ICollection<AnomalyLogEntry> AnomalyLogEntries { get; set; } =
        new List<AnomalyLogEntry>();
}
