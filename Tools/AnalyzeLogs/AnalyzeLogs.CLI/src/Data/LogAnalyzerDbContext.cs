using AnalyzeLogs.Models;
using Microsoft.EntityFrameworkCore;

namespace AnalyzeLogs.Data;

/// <summary>
/// Database context for the Log Analyzer application.
/// </summary>
public class LogAnalyzerDbContext : DbContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LogAnalyzerDbContext"/> class.
    /// </summary>
    /// <param name="options">The options to be used by the DbContext.</param>
    public LogAnalyzerDbContext(DbContextOptions<LogAnalyzerDbContext> options)
        : base(options) { }

    /// <summary>
    /// Gets or sets the projects in the database.
    /// </summary>
    public DbSet<Project> Projects { get; set; } = null!;

    /// <summary>
    /// Gets or sets the sessions in the database.
    /// </summary>
    public DbSet<Session> Sessions { get; set; } = null!;

    /// <summary>
    /// Gets or sets the log entries in the database.
    /// </summary>
    public DbSet<LogEntry> LogEntries { get; set; } = null!;

    /// <summary>
    /// Gets or sets the severity levels in the database.
    /// </summary>
    public DbSet<SeverityLevel> SeverityLevels { get; set; } = null!;

    /// <summary>
    /// Gets or sets the services in the database.
    /// </summary>
    public DbSet<Service> Services { get; set; } = null!;

    /// <summary>
    /// Gets or sets the anomalies in the database.
    /// </summary>
    public DbSet<Anomaly> Anomalies { get; set; } = null!;

    /// <summary>
    /// Gets or sets the anomaly log entries in the database.
    /// </summary>
    public DbSet<AnomalyLogEntry> AnomalyLogEntries { get; set; } = null!;

    /// <summary>
    /// Configures the model that was discovered by convention from the entity types.
    /// </summary>
    /// <param name="modelBuilder">The builder being used to construct the model for this context.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure primary keys
        modelBuilder.Entity<Project>().HasKey(p => p.ProjectId);

        modelBuilder.Entity<Session>().HasKey(s => s.SessionId);

        modelBuilder.Entity<LogEntry>().HasKey(l => l.LogEntryId);

        modelBuilder.Entity<SeverityLevel>().HasKey(s => s.SeverityLevelId);

        modelBuilder.Entity<Service>().HasKey(s => s.ServiceId);

        modelBuilder.Entity<Anomaly>().HasKey(a => a.AnomalyId);

        modelBuilder.Entity<AnomalyLogEntry>().HasKey(al => new { al.AnomalyId, al.LogEntryId });

        // Configure relationships
        modelBuilder
            .Entity<Project>()
            .HasMany(p => p.Sessions)
            .WithOne(s => s.Project)
            .HasForeignKey(s => s.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder
            .Entity<Session>()
            .HasMany(s => s.LogEntries)
            .WithOne(l => l.Session)
            .HasForeignKey(l => l.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder
            .Entity<Session>()
            .HasMany(s => s.Anomalies)
            .WithOne(a => a.Session)
            .HasForeignKey(a => a.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder
            .Entity<SeverityLevel>()
            .HasMany(s => s.LogEntries)
            .WithOne(l => l.SeverityLevel)
            .HasForeignKey(l => l.SeverityLevelId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder
            .Entity<Service>()
            .HasMany(s => s.LogEntries)
            .WithOne(l => l.Service)
            .HasForeignKey(l => l.ServiceId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder
            .Entity<Anomaly>()
            .HasMany(a => a.AnomalyLogEntries)
            .WithOne(al => al.Anomaly)
            .HasForeignKey(al => al.AnomalyId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder
            .Entity<LogEntry>()
            .HasMany(l => l.AnomalyLogEntries)
            .WithOne(al => al.LogEntry)
            .HasForeignKey(al => al.LogEntryId)
            .OnDelete(DeleteBehavior.Cascade);

        // Configure unique constraints
        modelBuilder.Entity<SeverityLevel>().HasIndex(s => s.LevelName).IsUnique();

        modelBuilder.Entity<Service>().HasIndex(s => s.ServiceName).IsUnique();

        // Configure indexes for common query patterns
        modelBuilder.Entity<LogEntry>().HasIndex(l => l.TimestampUTC);

        modelBuilder.Entity<LogEntry>().HasIndex(l => l.CorrelationId);

        modelBuilder.Entity<LogEntry>().HasIndex(l => new { l.SessionId, l.TimestampUTC });

        // Configure auto-increment for primary keys
        modelBuilder.Entity<LogEntry>().Property(l => l.LogEntryId).ValueGeneratedOnAdd();

        modelBuilder.Entity<SeverityLevel>().Property(s => s.SeverityLevelId).ValueGeneratedOnAdd();

        modelBuilder.Entity<Service>().Property(s => s.ServiceId).ValueGeneratedOnAdd();

        modelBuilder.Entity<Anomaly>().Property(a => a.AnomalyId).ValueGeneratedOnAdd();

        // Seed common severity levels
        modelBuilder
            .Entity<SeverityLevel>()
            .HasData(
                new SeverityLevel { SeverityLevelId = 1, LevelName = "TRACE" },
                new SeverityLevel { SeverityLevelId = 2, LevelName = "DEBUG" },
                new SeverityLevel { SeverityLevelId = 3, LevelName = "INFO" },
                new SeverityLevel { SeverityLevelId = 4, LevelName = "WARNING" },
                new SeverityLevel { SeverityLevelId = 5, LevelName = "ERROR" },
                new SeverityLevel { SeverityLevelId = 6, LevelName = "CRITICAL" },
                new SeverityLevel { SeverityLevelId = 7, LevelName = "FATAL" }
            );
    }
}
