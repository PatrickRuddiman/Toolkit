# Data Models and Database Schema

## Core Domain Models

### Project Model
Represents a log analysis project for organizing related analysis runs.

```csharp
public class Project
{
    public string ProjectId { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string LogPattern { get; set; } = "logs/**/*.log";
    public string? ConfigurationSettings { get; set; } // JSON blob
    
    // Navigation properties
    public virtual ICollection<AnalysisRun> AnalysisRuns { get; set; } = new List<AnalysisRun>();
}
```

### AnalysisRun Model
Represents an individual analysis run within a project.

```csharp
public class AnalysisRun
{
    public string AnalysisRunId { get; set; } = Guid.NewGuid().ToString();
    public string ProjectId { get; set; } = string.Empty;
    public string? Name { get; set; }
    public DateTime StartTime { get; set; } = DateTime.UtcNow;
    public DateTime? EndTime { get; set; }
    public AnalysisStatus Status { get; set; } = AnalysisStatus.Running;
    public string? LogPattern { get; set; }
    public string? AnalysisParameters { get; set; } // JSON blob
    public string? Summary { get; set; }
    public int LogEntryCount { get; set; }
    public int AnomalyCount { get; set; }
    public TimeSpan? Duration => EndTime?.Subtract(StartTime);
    
    // Navigation properties
    public virtual Project Project { get; set; } = null!;
    public virtual ICollection<LogEntry> LogEntries { get; set; } = new List<LogEntry>();
    public virtual ICollection<Anomaly> Anomalies { get; set; } = new List<Anomaly>();
    public virtual ICollection<CorrelationGroup> CorrelationGroups { get; set; } = new List<CorrelationGroup>();
    public virtual ICollection<ServiceHealth> ServiceHealthMetrics { get; set; } = new List<ServiceHealth>();
}

public enum AnalysisStatus
{
    Running,
    Completed,
    Failed,
    Cancelled
}
```

### LogEntry Model
Core model representing a normalized log entry.

```csharp
public class LogEntry
{
    public string LogEntryId { get; set; } = Guid.NewGuid().ToString();
    public string AnalysisRunId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string? ServiceName { get; set; }
    public SeverityLevel Level { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? CorrelationId { get; set; }
    public string SourceFile { get; set; } = string.Empty;
    public int LineNumber { get; set; }
    public string? RawLogLine { get; set; }
    public string? ParsedFields { get; set; } // JSON blob for additional fields
    public byte[]? EmbeddingVector { get; set; } // Serialized vector
    public List<string> Tags { get; set; } = new List<string>();
    
    // Navigation properties
    public virtual AnalysisRun AnalysisRun { get; set; } = null!;
    public virtual ICollection<AnomalyLogEntry> AnomalyLogEntries { get; set; } = new List<AnomalyLogEntry>();
}

public enum SeverityLevel
{
    Trace = 0,
    Debug = 1,
    Information = 2,
    Warning = 3,
    Error = 4,
    Critical = 5,
    Fatal = 6
}
```

### Anomaly Model
Represents detected anomalies in log data.

```csharp
public class Anomaly
{
    public string AnomalyId { get; set; } = Guid.NewGuid().ToString();
    public string AnalysisRunId { get; set; } = string.Empty;
    public AnomalyType Type { get; set; }
    public AnomalySeverity Severity { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
    public DateTime? FirstOccurrence { get; set; }
    public DateTime? LastOccurrence { get; set; }
    public int OccurrenceCount { get; set; } = 1;
    public string? ServiceName { get; set; }
    public string? CorrelationId { get; set; }
    public string? Context { get; set; } // JSON blob with additional context
    public string? Recommendation { get; set; }
    public AnomalyStatus Status { get; set; } = AnomalyStatus.Open;
    
    // Navigation properties
    public virtual AnalysisRun AnalysisRun { get; set; } = null!;
    public virtual ICollection<AnomalyLogEntry> AnomalyLogEntries { get; set; } = new List<AnomalyLogEntry>();
}

public enum AnomalyType
{
    Error,
    Performance,
    Security,
    Behavioral,
    Pattern,
    Coherence
}

public enum AnomalySeverity
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

public enum AnomalyStatus
{
    Open,
    Investigating,
    Resolved,
    Ignored
}
```

### AnomalyLogEntry Model
Junction table linking anomalies to specific log entries.

```csharp
public class AnomalyLogEntry
{
    public string AnomalyId { get; set; } = string.Empty;
    public string LogEntryId { get; set; } = string.Empty;
    public AnomalyLogRole Role { get; set; } = AnomalyLogRole.Evidence;
    
    // Navigation properties
    public virtual Anomaly Anomaly { get; set; } = null!;
    public virtual LogEntry LogEntry { get; set; } = null!;
}

public enum AnomalyLogRole
{
    Trigger,    // The log entry that triggered the anomaly detection
    Evidence,   // Supporting evidence for the anomaly
    Context     // Contextual information around the anomaly
}
```

## Correlation and Service Models

### CorrelationGroup Model
Groups related log entries by correlation ID or semantic similarity.

```csharp
public class CorrelationGroup
{
    public string CorrelationGroupId { get; set; } = Guid.NewGuid().ToString();
    public string AnalysisRunId { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public CorrelationType Type { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int ServiceCount { get; set; }
    public int LogEntryCount { get; set; }
    public CorrelationStatus Status { get; set; } = CorrelationStatus.Complete;
    public TimeSpan Duration => EndTime.Subtract(StartTime);
    public string? Summary { get; set; }
    
    // Navigation properties
    public virtual AnalysisRun AnalysisRun { get; set; } = null!;
    public virtual ICollection<LogEntry> LogEntries { get; set; } = new List<LogEntry>();
}

public enum CorrelationType
{
    ExplicitId,     // Correlated by explicit correlation ID
    Temporal,       // Correlated by time proximity
    Semantic,       // Correlated by semantic similarity
    Mixed           // Multiple correlation methods used
}

public enum CorrelationStatus
{
    Complete,       // All expected log entries found
    Incomplete,     // Missing expected log entries
    Error,          // Correlation analysis failed
    Partial         // Some correlation found but not complete
}
```

### Service Model
Represents a service in the microservice architecture.

```csharp
public class Service
{
    public string ServiceId { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Version { get; set; }
    public ServiceType Type { get; set; }
    public DateTime FirstSeen { get; set; } = DateTime.UtcNow;
    public DateTime LastSeen { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
    
    // Navigation properties
    public virtual ICollection<ServiceHealth> HealthMetrics { get; set; } = new List<ServiceHealth>();
}

public enum ServiceType
{
    WebApi,
    Database,
    MessageQueue,
    Cache,
    Gateway,
    Authentication,
    Unknown
}
```

### ServiceHealth Model
Tracks health metrics for services across analysis runs.

```csharp
public class ServiceHealth
{
    public string ServiceHealthId { get; set; } = Guid.NewGuid().ToString();
    public string AnalysisRunId { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    public DateTime MeasurementTime { get; set; } = DateTime.UtcNow;
    public long TotalRequests { get; set; }
    public long ErrorCount { get; set; }
    public double ErrorRate => TotalRequests > 0 ? (double)ErrorCount / TotalRequests : 0;
    public double? AverageResponseTime { get; set; }
    public double? P50ResponseTime { get; set; }
    public double? P95ResponseTime { get; set; }
    public double? P99ResponseTime { get; set; }
    public HealthStatus Status { get; set; }
    public string? StatusReason { get; set; }
    
    // Navigation properties
    public virtual AnalysisRun AnalysisRun { get; set; } = null!;
}

public enum HealthStatus
{
    Healthy,
    Degraded,
    Critical,
    Unknown
}
```

## Database Schema Implementation

### Entity Framework Configuration

```csharp
public class LogAnalyzerDbContext : DbContext
{
    public DbSet<Project> Projects { get; set; } = null!;
    public DbSet<AnalysisRun> AnalysisRuns { get; set; } = null!;
    public DbSet<LogEntry> LogEntries { get; set; } = null!;
    public DbSet<Anomaly> Anomalies { get; set; } = null!;
    public DbSet<AnomalyLogEntry> AnomalyLogEntries { get; set; } = null!;
    public DbSet<CorrelationGroup> CorrelationGroups { get; set; } = null!;
    public DbSet<Service> Services { get; set; } = null!;
    public DbSet<ServiceHealth> ServiceHealthMetrics { get; set; } = null!;
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Project configuration
        modelBuilder.Entity<Project>(entity =>
        {
            entity.HasKey(e => e.ProjectId);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.HasIndex(e => e.Name).IsUnique();
        });
        
        // AnalysisRun configuration
        modelBuilder.Entity<AnalysisRun>(entity =>
        {
            entity.HasKey(e => e.AnalysisRunId);
            entity.HasOne(e => e.Project)
                  .WithMany(p => p.AnalysisRuns)
                  .HasForeignKey(e => e.ProjectId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.ProjectId, e.StartTime });
        });
        
        // LogEntry configuration
        modelBuilder.Entity<LogEntry>(entity =>
        {
            entity.HasKey(e => e.LogEntryId);
            entity.Property(e => e.Message).IsRequired();
            entity.HasOne(e => e.AnalysisRun)
                  .WithMany(ar => ar.LogEntries)
                  .HasForeignKey(e => e.AnalysisRunId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.AnalysisRunId, e.Timestamp });
            entity.HasIndex(e => e.ServiceName);
            entity.HasIndex(e => e.CorrelationId);
        });
        
        // Anomaly configuration
        modelBuilder.Entity<Anomaly>(entity =>
        {
            entity.HasKey(e => e.AnomalyId);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(500);
            entity.HasOne(e => e.AnalysisRun)
                  .WithMany(ar => ar.Anomalies)
                  .HasForeignKey(e => e.AnalysisRunId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.AnalysisRunId, e.Severity });
        });
        
        // AnomalyLogEntry (many-to-many) configuration
        modelBuilder.Entity<AnomalyLogEntry>(entity =>
        {
            entity.HasKey(e => new { e.AnomalyId, e.LogEntryId });
            entity.HasOne(e => e.Anomaly)
                  .WithMany(a => a.AnomalyLogEntries)
                  .HasForeignKey(e => e.AnomalyId);
            entity.HasOne(e => e.LogEntry)
                  .WithMany(le => le.AnomalyLogEntries)
                  .HasForeignKey(e => e.LogEntryId);
        });
        
        // Additional configurations...
    }
}
```

### Database Indexing Strategy

#### Primary Indexes
- **Projects**: Unique index on Name
- **AnalysisRuns**: Composite index on (ProjectId, StartTime)
- **LogEntries**: Composite index on (AnalysisRunId, Timestamp), single indexes on ServiceName and CorrelationId
- **Anomalies**: Composite index on (AnalysisRunId, Severity)

#### Performance Optimizations
```sql
-- Commonly used query patterns
CREATE INDEX IX_LogEntry_ServiceName_Level ON LogEntry (ServiceName, Level);
CREATE INDEX IX_LogEntry_Timestamp_Level ON LogEntry (Timestamp, Level) WHERE Level >= 3; -- Warnings and above
CREATE INDEX IX_Anomaly_Type_Status ON Anomaly (Type, Status);
CREATE INDEX IX_CorrelationGroup_Type_Status ON CorrelationGroup (Type, Status);
```

## Data Transfer Objects (DTOs)

### API Response Models
```csharp
public class ProjectSummaryDto
{
    public string ProjectId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public int AnalysisRunCount { get; set; }
    public DateTime? LastAnalysisRun { get; set; }
}

public class AnalysisRunSummaryDto
{
    public string AnalysisRunId { get; set; } = string.Empty;
    public string? Name { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public AnalysisStatus Status { get; set; }
    public int LogEntryCount { get; set; }
    public int AnomalyCount { get; set; }
    public TimeSpan? Duration { get; set; }
}

public class AnomalySummaryDto
{
    public string AnomalyId { get; set; } = string.Empty;
    public AnomalyType Type { get; set; }
    public AnomalySeverity Severity { get; set; }
    public string Title { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    public DateTime DetectedAt { get; set; }
    public int OccurrenceCount { get; set; }
    public AnomalyStatus Status { get; set; }
}
```
