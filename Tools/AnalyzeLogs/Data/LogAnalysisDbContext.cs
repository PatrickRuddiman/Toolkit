using AnalyzeLogs.Models.Database;
using Microsoft.EntityFrameworkCore;

namespace AnalyzeLogs.Data;

public class LogAnalysisDbContext : DbContext
{
    public DbSet<Project> Projects { get; set; }
    public DbSet<LogAnalysisSession> Sessions { get; set; }
    public DbSet<StoredLogEntry> LogEntries { get; set; }
    public DbSet<StoredAnomaly> Anomalies { get; set; }
    public DbSet<StoredCorrelation> Correlations { get; set; }
    public DbSet<StoredServiceMetrics> ServiceMetrics { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        var dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AnalyzeLogs",
            "database.db"
        );

        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        optionsBuilder.UseSqlite($"Data Source={dbPath}");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Configure Project entity
        modelBuilder.Entity<Project>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Name).IsUnique();
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();
        });

        // Configure LogAnalysisSession entity
        modelBuilder.Entity<LogAnalysisSession>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ProjectId);
            entity.HasIndex(e => e.CreatedAt);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.CompletedAt);
            entity.Property(e => e.Status).IsRequired();
            entity.Property(e => e.ConfigurationJson).HasColumnType("TEXT");
            entity.Property(e => e.SummaryJson).HasColumnType("TEXT");

            entity
                .HasOne(e => e.Project)
                .WithMany(p => p.Sessions)
                .HasForeignKey(e => e.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure StoredLogEntry entity
        modelBuilder.Entity<StoredLogEntry>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.SessionId);
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => e.Service);
            entity.HasIndex(e => e.Level);
            entity.HasIndex(e => e.CorrelationId);
            entity.HasIndex(e => e.TraceId);
            entity.Property(e => e.LogId).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Message).IsRequired();
            entity.Property(e => e.Service).HasMaxLength(200);
            entity.Property(e => e.CorrelationId).HasMaxLength(100);
            entity.Property(e => e.TraceId).HasMaxLength(100);
            entity.Property(e => e.UserId).HasMaxLength(100);
            entity.Property(e => e.SourceFile).HasMaxLength(500);
            entity.Property(e => e.AdditionalDataJson).HasColumnType("TEXT");
            entity.Property(e => e.TagsJson).HasColumnType("TEXT");
            entity.Property(e => e.EmbeddingJson).HasColumnType("TEXT");
            entity.Property(e => e.RawContent).IsRequired();

            entity
                .HasOne(e => e.Session)
                .WithMany(s => s.LogEntries)
                .HasForeignKey(e => e.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure StoredAnomaly entity
        modelBuilder.Entity<StoredAnomaly>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.SessionId);
            entity.HasIndex(e => e.Confidence);
            entity.HasIndex(e => e.Type);
            entity.Property(e => e.Type).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Description).IsRequired();
            entity.Property(e => e.Recommendation).HasMaxLength(1000);
            entity.Property(e => e.TagsJson).HasColumnType("TEXT");

            entity
                .HasOne(e => e.Session)
                .WithMany(s => s.Anomalies)
                .HasForeignKey(e => e.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure StoredCorrelation entity
        modelBuilder.Entity<StoredCorrelation>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.SessionId);
            entity.HasIndex(e => e.CorrelationId);
            entity.Property(e => e.CorrelationId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.ServicesJson).HasColumnType("TEXT");
            entity.Property(e => e.RelatedLogIdsJson).HasColumnType("TEXT");

            entity
                .HasOne(e => e.Session)
                .WithMany(s => s.Correlations)
                .HasForeignKey(e => e.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure StoredServiceMetrics entity
        modelBuilder.Entity<StoredServiceMetrics>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.SessionId);
            entity.HasIndex(e => e.ServiceName);
            entity.Property(e => e.ServiceName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.HttpStatusDistributionJson).HasColumnType("TEXT");
            entity.Property(e => e.TagDistributionJson).HasColumnType("TEXT");

            entity
                .HasOne(e => e.Session)
                .WithMany(s => s.ServiceMetrics)
                .HasForeignKey(e => e.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
