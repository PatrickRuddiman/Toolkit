using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace AnalyzeLogs.Models.Database;

/// <summary>
/// Represents a project for organizing log analysis sessions
/// </summary>
public class Project
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastAnalyzedAt { get; set; }

    /// <summary>
    /// Navigation properties
    /// </summary>
    public List<LogAnalysisSession> AnalysisSessions { get; set; } = new();
    public List<LogAnalysisSession> Sessions { get; set; } = new();
    public List<StoredLogEntry> LogEntries { get; set; } = new();
    public List<StoredAnomaly> Anomalies { get; set; } = new();
    public List<StoredCorrelation> Correlations { get; set; } = new();
}

/// <summary>
/// Represents an analysis session within a project
/// </summary>
public class LogAnalysisSession
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int ProjectId { get; set; }

    [Required]
    [MaxLength(500)]
    public string SessionName { get; set; } = string.Empty;

    [Required]
    public DateTime StartTime { get; set; }

    public DateTime? EndTime { get; set; }

    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAt { get; set; }

    [Required]
    public int TotalEntries { get; set; }

    public int AnomaliesFound { get; set; }

    public int CorrelationsFound { get; set; }

    [MaxLength(2000)]
    public string? SourceFiles { get; set; } // JSON array of file paths

    [MaxLength(50)]
    public string Status { get; set; } = "InProgress"; // InProgress, Completed, Failed

    [MaxLength(1000)]
    public string? Description { get; set; }

    public string? ConfigurationJson { get; set; } // JSON serialized configuration

    public string? SummaryJson { get; set; } // JSON serialized summary

    /// <summary>
    /// Navigation properties
    /// </summary>
    [ForeignKey("ProjectId")]
    public Project Project { get; set; } = null!;

    public List<StoredLogEntry> LogEntries { get; set; } = new();
    public List<StoredAnomaly> Anomalies { get; set; } = new();
    public List<StoredCorrelation> Correlations { get; set; } = new();
    public List<StoredServiceMetrics> ServiceMetrics { get; set; } = new();
}

/// <summary>
/// Stored log entry in the database
/// </summary>
public class StoredLogEntry
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int ProjectId { get; set; }

    public int? SessionId { get; set; }

    [Required]
    [MaxLength(36)]
    public string LogId { get; set; } = string.Empty; // Original LogEntry.Id

    [Required]
    public DateTime Timestamp { get; set; }

    [Required]
    public int Level { get; set; } // LogLevel enum value

    [MaxLength(100)]
    public string? Service { get; set; }

    [Required]
    public string Message { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? CorrelationId { get; set; }

    [MaxLength(100)]
    public string? TraceId { get; set; }

    [MaxLength(100)]
    public string? UserId { get; set; }

    public int? HttpStatus { get; set; }

    public double? ResponseTimeMs { get; set; }

    [MaxLength(500)]
    public string? SourceFile { get; set; }

    public int LineNumber { get; set; }

    public string? AdditionalDataJson { get; set; } // JSON serialized AdditionalData

    public string? TagsJson { get; set; } // JSON serialized Tags

    public bool IsAnomaly { get; set; }

    public double AnomalyScore { get; set; }

    public string? EmbeddingJson { get; set; } // JSON serialized Embedding

    public string RawContent { get; set; } = string.Empty;

    [Required]
    public DateTime StoredAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Navigation properties
    /// </summary>
    [ForeignKey("ProjectId")]
    public Project Project { get; set; } = null!;

    [ForeignKey("SessionId")]
    public LogAnalysisSession? Session { get; set; }

    /// <summary>
    /// Convert to LogEntry model
    /// </summary>
    public LogEntry ToLogEntry()
    {
        var logEntry = new LogEntry
        {
            Id = LogId,
            Timestamp = Timestamp,
            Level = (LogLevel)Level,
            Service = Service,
            Message = Message,
            CorrelationId = CorrelationId,
            TraceId = TraceId,
            UserId = UserId,
            HttpStatus = HttpStatus,
            ResponseTimeMs = ResponseTimeMs,
            SourceFile = SourceFile ?? string.Empty,
            LineNumber = LineNumber,
            IsAnomaly = IsAnomaly,
            AnomalyScore = AnomalyScore,
            RawContent = RawContent,
        };

        // Deserialize JSON fields
        if (!string.IsNullOrEmpty(AdditionalDataJson))
        {
            try
            {
                logEntry.AdditionalData =
                    JsonSerializer.Deserialize<Dictionary<string, object?>>(AdditionalDataJson)
                    ?? new();
            }
            catch
            {
                logEntry.AdditionalData = new();
            }
        }

        if (!string.IsNullOrEmpty(TagsJson))
        {
            try
            {
                logEntry.Tags = JsonSerializer.Deserialize<List<string>>(TagsJson) ?? new();
            }
            catch
            {
                logEntry.Tags = new();
            }
        }

        if (!string.IsNullOrEmpty(EmbeddingJson))
        {
            try
            {
                logEntry.Embedding = JsonSerializer.Deserialize<float[]>(EmbeddingJson);
            }
            catch
            {
                logEntry.Embedding = null;
            }
        }

        return logEntry;
    }

    /// <summary>
    /// Create from LogEntry model
    /// </summary>
    public static StoredLogEntry FromLogEntry(
        LogEntry logEntry,
        int projectId,
        int? sessionId = null
    )
    {
        var stored = new StoredLogEntry
        {
            ProjectId = projectId,
            SessionId = sessionId,
            LogId = logEntry.Id,
            Timestamp = logEntry.Timestamp,
            Level = (int)logEntry.Level,
            Service = logEntry.Service,
            Message = logEntry.Message,
            CorrelationId = logEntry.CorrelationId,
            TraceId = logEntry.TraceId,
            UserId = logEntry.UserId,
            HttpStatus = logEntry.HttpStatus,
            ResponseTimeMs = logEntry.ResponseTimeMs,
            SourceFile = logEntry.SourceFile,
            LineNumber = logEntry.LineNumber,
            IsAnomaly = logEntry.IsAnomaly,
            AnomalyScore = logEntry.AnomalyScore,
            RawContent = logEntry.RawContent,
        };

        // Serialize JSON fields
        if (logEntry.AdditionalData.Any())
        {
            stored.AdditionalDataJson = JsonSerializer.Serialize(logEntry.AdditionalData);
        }

        if (logEntry.Tags.Any())
        {
            stored.TagsJson = JsonSerializer.Serialize(logEntry.Tags);
        }

        if (logEntry.Embedding != null)
        {
            stored.EmbeddingJson = JsonSerializer.Serialize(logEntry.Embedding);
        }

        return stored;
    }
}

/// <summary>
/// Stored anomaly in the database
/// </summary>
public class StoredAnomaly
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int ProjectId { get; set; }

    public int? SessionId { get; set; }

    [Required]
    [MaxLength(50)]
    public string Type { get; set; } = string.Empty; // AnomalyType enum value

    [Required]
    [MaxLength(50)]
    public string Severity { get; set; } = string.Empty; // AnomalySeverity enum value

    [Required]
    public DateTime Timestamp { get; set; }

    [MaxLength(100)]
    public string? Service { get; set; }

    [Required]
    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    public double Confidence { get; set; }

    [MaxLength(500)]
    public string? Recommendation { get; set; }

    public string? TagsJson { get; set; } // JSON serialized Tags

    public string? RelatedLogIdsJson { get; set; } // JSON serialized RelatedLogIds

    [Required]
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Navigation properties
    /// </summary>
    [ForeignKey("ProjectId")]
    public Project Project { get; set; } = null!;

    [ForeignKey("SessionId")]
    public LogAnalysisSession? Session { get; set; }

    /// <summary>
    /// Convert to Anomaly model
    /// </summary>
    public Anomaly ToAnomaly()
    {
        var anomaly = new Anomaly
        {
            Type = Enum.Parse<AnomalyType>(Type),
            Severity = Enum.Parse<AnomalySeverity>(Severity),
            Timestamp = Timestamp,
            Service = Service,
            Description = Description,
            Confidence = Confidence,
            Recommendation = Recommendation,
        };

        // Deserialize JSON fields
        if (!string.IsNullOrEmpty(TagsJson))
        {
            try
            {
                anomaly.Tags = JsonSerializer.Deserialize<List<string>>(TagsJson) ?? new();
            }
            catch
            {
                anomaly.Tags = new();
            }
        }

        return anomaly;
    }

    /// <summary>
    /// Create from Anomaly model
    /// </summary>
    public static StoredAnomaly FromAnomaly(Anomaly anomaly, int projectId, int? sessionId = null)
    {
        var stored = new StoredAnomaly
        {
            ProjectId = projectId,
            SessionId = sessionId,
            Type = anomaly.Type.ToString(),
            Severity = anomaly.Severity.ToString(),
            Timestamp = anomaly.Timestamp,
            Service = anomaly.Service,
            Description = anomaly.Description,
            Confidence = anomaly.Confidence,
            Recommendation = anomaly.Recommendation,
        };

        // Serialize JSON fields
        if (anomaly.Tags.Any())
        {
            stored.TagsJson = JsonSerializer.Serialize(anomaly.Tags);
        }

        if (anomaly.RelatedLogIds.Any())
        {
            stored.RelatedLogIdsJson = JsonSerializer.Serialize(anomaly.RelatedLogIds);
        }

        return stored;
    }
}

/// <summary>
/// Stored correlation in the database
/// </summary>
public class StoredCorrelation
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int ProjectId { get; set; }

    public int? SessionId { get; set; }

    [Required]
    [MaxLength(200)]
    public string CorrelationId { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [Required]
    public DateTime StartTime { get; set; }

    [Required]
    public DateTime EndTime { get; set; }

    public string? ServicesJson { get; set; } // JSON serialized Services
    public string? ServiceNamesJson { get; set; } // JSON serialized service names
    public string? LogEntryIdsJson { get; set; } // JSON serialized log entry IDs
    public string? AdditionalDataJson { get; set; } // JSON serialized additional data

    public bool IsSuccessful { get; set; }

    public int EntryCount { get; set; }

    public string? RelatedLogIdsJson { get; set; } // JSON serialized log entry IDs

    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Navigation properties
    /// </summary>
    [ForeignKey("ProjectId")]
    public Project Project { get; set; } = null!;

    [ForeignKey("SessionId")]
    public LogAnalysisSession? Session { get; set; }

    /// <summary>
    /// Convert to LogCorrelation model
    /// </summary>
    public LogCorrelation ToLogCorrelation()
    {
        var correlation = new LogCorrelation
        {
            Id = CorrelationId,
            StartTime = StartTime,
            EndTime = EndTime,
            IsSuccessful = IsSuccessful,
            Entries = new(), // Will need to be populated separately
        };

        // Deserialize JSON fields
        if (!string.IsNullOrEmpty(ServicesJson))
        {
            try
            {
                var services = JsonSerializer.Deserialize<List<string>>(ServicesJson) ?? new();
                correlation.Services = services.ToHashSet();
            }
            catch
            {
                correlation.Services = new();
            }
        }

        return correlation;
    }

    /// <summary>
    /// Create from LogCorrelation model
    /// </summary>
    public static StoredCorrelation FromLogCorrelation(
        LogCorrelation correlation,
        int projectId,
        int? sessionId = null
    )
    {
        var stored = new StoredCorrelation
        {
            ProjectId = projectId,
            SessionId = sessionId,
            CorrelationId = correlation.Id,
            StartTime = correlation.StartTime,
            EndTime = correlation.EndTime,
            IsSuccessful = correlation.IsSuccessful,
            EntryCount = correlation.Entries.Count,
        };

        // Serialize JSON fields
        if (correlation.Services.Any())
        {
            stored.ServicesJson = JsonSerializer.Serialize(correlation.Services.ToList());
        }

        if (correlation.Entries.Any())
        {
            stored.RelatedLogIdsJson = JsonSerializer.Serialize(
                correlation.Entries.Select(e => e.Id).ToList()
            );
        }

        return stored;
    }
}

/// <summary>
/// Stored service metrics in the database
/// </summary>
public class StoredServiceMetrics
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int ProjectId { get; set; }

    public int? SessionId { get; set; }

    [Required]
    [MaxLength(100)]
    public string ServiceName { get; set; } = string.Empty;

    [Required]
    public int TotalRequests { get; set; }

    [Required]
    public int ErrorCount { get; set; }

    /// <summary>
    /// Error rate (computed property)
    /// </summary>
    public double ErrorRate => TotalRequests > 0 ? (double)ErrorCount / TotalRequests : 0;

    public double? AverageResponseTime { get; set; }

    public double? P95ResponseTime { get; set; }

    [Required]
    public double RequestRate { get; set; }

    public int UniqueUsers { get; set; }

    public string? HttpStatusDistributionJson { get; set; } // JSON serialized Dictionary<int, int>

    public string? TagDistributionJson { get; set; } // JSON serialized Dictionary<string, int>

    [Required]
    public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Navigation properties
    /// </summary>
    [ForeignKey("ProjectId")]
    public Project Project { get; set; } = null!;

    [ForeignKey("SessionId")]
    public LogAnalysisSession? Session { get; set; }

    /// <summary>
    /// Convert to ServiceMetrics model
    /// </summary>
    public ServiceMetrics ToServiceMetrics()
    {
        var metrics = new ServiceMetrics
        {
            ServiceName = ServiceName,
            TotalRequests = TotalRequests,
            ErrorCount = ErrorCount,
            AverageResponseTime = AverageResponseTime,
            P95ResponseTime = P95ResponseTime,
            RequestRate = RequestRate,
            UniqueUsers = UniqueUsers,
        };

        // Deserialize JSON fields
        if (!string.IsNullOrEmpty(HttpStatusDistributionJson))
        {
            try
            {
                metrics.HttpStatusDistribution =
                    JsonSerializer.Deserialize<Dictionary<int, int>>(HttpStatusDistributionJson)
                    ?? new();
            }
            catch
            {
                metrics.HttpStatusDistribution = new();
            }
        }

        if (!string.IsNullOrEmpty(TagDistributionJson))
        {
            try
            {
                metrics.TagDistribution =
                    JsonSerializer.Deserialize<Dictionary<string, int>>(TagDistributionJson)
                    ?? new();
            }
            catch
            {
                metrics.TagDistribution = new();
            }
        }

        return metrics;
    }

    /// <summary>
    /// Create from ServiceMetrics model
    /// </summary>
    public static StoredServiceMetrics FromServiceMetrics(
        ServiceMetrics metrics,
        int projectId,
        int? sessionId = null
    )
    {
        var stored = new StoredServiceMetrics
        {
            ProjectId = projectId,
            SessionId = sessionId,
            ServiceName = metrics.ServiceName,
            TotalRequests = metrics.TotalRequests,
            ErrorCount = metrics.ErrorCount,
            AverageResponseTime = metrics.AverageResponseTime,
            P95ResponseTime = metrics.P95ResponseTime,
            RequestRate = metrics.RequestRate,
            UniqueUsers = metrics.UniqueUsers,
        };

        // Serialize JSON fields
        if (metrics.HttpStatusDistribution.Any())
        {
            stored.HttpStatusDistributionJson = JsonSerializer.Serialize(
                metrics.HttpStatusDistribution
            );
        }

        if (metrics.TagDistribution.Any())
        {
            stored.TagDistributionJson = JsonSerializer.Serialize(metrics.TagDistribution);
        }

        return stored;
    }
}
