namespace AnalyzeLogs.Models;

/// <summary>
/// Represents a log analysis project.
/// </summary>
public class Project
{
    /// <summary>
    /// Unique identifier for the project.
    /// </summary>
    public Guid ProjectId { get; set; }

    /// <summary>
    /// User-defined name for the project.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Optional description of the project.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Timestamp of project creation.
    /// </summary>
    public DateTime CreationDate { get; set; }

    /// <summary>
    /// Timestamp of last project access.
    /// </summary>
    public DateTime? LastAccessedDate { get; set; }

    /// <summary>
    /// Default glob pattern for log files associated with this project.
    /// </summary>
    public string? DefaultLogPathPattern { get; set; }

    /// <summary>
    /// Navigation property for sessions belonging to this project.
    /// </summary>
    public virtual ICollection<Session> Sessions { get; set; } = new List<Session>();
}
