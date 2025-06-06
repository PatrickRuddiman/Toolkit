using System.Text.Json;
using AnalyzeLogs.Data;
using AnalyzeLogs.Models;
using AnalyzeLogs.Models.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AnalyzeLogs.Services;

public class DatabaseService
{
    private readonly ILogger<DatabaseService> _logger;
    private readonly LogAnalysisDbContext _context;

    public DatabaseService(ILogger<DatabaseService> logger, LogAnalysisDbContext context)
    {
        _logger = logger;
        _context = context;
    }

    /// <summary>
    /// Initialize the database by ensuring it's created and migrated
    /// </summary>
    public async Task InitializeAsync()
    {
        try
        {
            await _context.Database.EnsureCreatedAsync();
            _logger.LogInformation("Database initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize database");
            throw;
        }
    }

    public void Dispose()
    {
        _context?.Dispose();
    }

    #region Project Management

    public async Task<Project> CreateProjectAsync(string name, string? description = null)
    {
        var existingProject = await _context.Projects.FirstOrDefaultAsync(p => p.Name == name);

        if (existingProject != null)
        {
            throw new InvalidOperationException($"Project '{name}' already exists.");
        }

        var project = new Project
        {
            Name = name,
            Description = description,
            CreatedAt = DateTime.UtcNow,
        };

        _context.Projects.Add(project);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Created project '{ProjectName}' with ID {ProjectId}",
            name,
            project.Id
        );
        return project;
    }

    public async Task<Project?> GetProjectByNameAsync(string name)
    {
        return await _context
            .Projects.Include(p => p.AnalysisSessions)
            .FirstOrDefaultAsync(p => p.Name == name);
    }

    public async Task<Project?> GetProjectByIdAsync(int id)
    {
        return await _context
            .Projects.Include(p => p.AnalysisSessions)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<List<Project>> ListProjectsAsync()
    {
        return await _context
            .Projects.Include(p => p.AnalysisSessions)
            .OrderByDescending(p => p.LastAnalyzedAt ?? p.CreatedAt)
            .ToListAsync();
    }

    public async Task DeleteProjectAsync(int projectId)
    {
        var project = await _context.Projects.FindAsync(projectId);
        if (project != null)
        {
            _context.Projects.Remove(project);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Deleted project with ID {ProjectId}", projectId);
        }
    }

    #endregion

    #region Session Management

    public async Task<LogAnalysisSession> CreateSessionAsync(int projectId, string sessionName)
    {
        var session = new LogAnalysisSession
        {
            ProjectId = projectId,
            SessionName = sessionName,
            StartTime = DateTime.UtcNow,
            Status = "InProgress",
        };

        _context.Sessions.Add(session);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Created session '{SessionName}' for project {ProjectId}",
            sessionName,
            projectId
        );
        return session;
    }

    public async Task<LogAnalysisSession?> GetSessionByIdAsync(int sessionId)
    {
        return await _context.Sessions.FindAsync(sessionId);
    }

    public async Task<List<LogAnalysisSession>> GetSessionsByProjectIdAsync(int projectId)
    {
        return await _context
            .Sessions.Where(s => s.ProjectId == projectId)
            .OrderByDescending(s => s.StartTime)
            .ToListAsync();
    }

    public async Task CompleteSessionAsync(
        int sessionId,
        int totalEntries,
        int anomaliesFound,
        int correlationsFound,
        List<string>? sourceFiles = null
    )
    {
        var session = await _context.Sessions.FindAsync(sessionId);
        if (session != null)
        {
            session.Status = "Completed";
            session.EndTime = DateTime.UtcNow;
            session.TotalEntries = totalEntries;
            session.AnomaliesFound = anomaliesFound;
            session.CorrelationsFound = correlationsFound;
            session.SourceFiles =
                sourceFiles != null ? JsonSerializer.Serialize(sourceFiles) : null;

            // Update project's last analyzed time
            var project = await _context.Projects.FindAsync(session.ProjectId);
            if (project != null)
            {
                project.LastAnalyzedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Completed session {SessionId}", sessionId);
        }
    }

    public async Task<List<LogAnalysisSession>> GetSessionsForProjectAsync(int projectId)
    {
        return await _context
            .Sessions.Where(s => s.ProjectId == projectId)
            .OrderByDescending(s => s.StartTime)
            .ToListAsync();
    }

    #endregion

    #region Log Entry Storage

    public async Task StoreLogEntriesAsync(
        int projectId,
        List<LogEntry> logEntries,
        int? sessionId = null
    )
    {
        var storedEntries = logEntries
            .Select(entry => StoredLogEntry.FromLogEntry(entry, projectId, sessionId))
            .ToList();

        _context.LogEntries.AddRange(storedEntries);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Stored {Count} log entries for project {ProjectId}, session {SessionId}",
            storedEntries.Count,
            projectId,
            sessionId
        );
    }

    public async Task StoreAnomaliesAsync(
        int projectId,
        List<Anomaly> anomalies,
        int? sessionId = null
    )
    {
        var storedAnomalies = anomalies
            .Select(anomaly => StoredAnomaly.FromAnomaly(anomaly, projectId, sessionId))
            .ToList();

        _context.Anomalies.AddRange(storedAnomalies);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Stored {Count} anomalies for project {ProjectId}, session {SessionId}",
            storedAnomalies.Count,
            projectId,
            sessionId
        );
    }

    public async Task StoreCorrelationsAsync(
        int projectId,
        List<LogCorrelation> correlations,
        int? sessionId = null
    )
    {
        var storedCorrelations = correlations
            .Select(correlation =>
                StoredCorrelation.FromLogCorrelation(correlation, projectId, sessionId)
            )
            .ToList();

        _context.Correlations.AddRange(storedCorrelations);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Stored {Count} correlations for project {ProjectId}, session {SessionId}",
            storedCorrelations.Count,
            projectId,
            sessionId
        );
    }

    #endregion

    #region Query Operations

    public async Task<List<StoredLogEntry>> QueryLogEntriesAsync(
        int? projectId = null,
        int? sessionId = null,
        DateTime? startTime = null,
        DateTime? endTime = null,
        string? service = null,
        Models.LogLevel? level = null,
        string? correlationId = null,
        int limit = 1000
    )
    {
        var query = _context.LogEntries.AsQueryable();

        if (projectId.HasValue)
        {
            query = query.Where(e => e.ProjectId == projectId.Value);
        }

        if (sessionId.HasValue)
        {
            query = query.Where(e => e.SessionId == sessionId.Value);
        }

        if (startTime.HasValue)
        {
            query = query.Where(e => e.Timestamp >= startTime.Value);
        }

        if (endTime.HasValue)
        {
            query = query.Where(e => e.Timestamp <= endTime.Value);
        }

        if (!string.IsNullOrEmpty(service))
        {
            query = query.Where(e => e.Service == service);
        }

        if (level.HasValue)
        {
            query = query.Where(e => e.Level == (int)level.Value);
        }

        if (!string.IsNullOrEmpty(correlationId))
        {
            query = query.Where(e => e.CorrelationId == correlationId);
        }

        return await query.OrderByDescending(e => e.Timestamp).Take(limit).ToListAsync();
    }

    public async Task<List<StoredAnomaly>> GetAnomaliesAsync(
        int? projectId = null,
        int? sessionId = null,
        string? category = null,
        double? minConfidence = null,
        int limit = 100
    )
    {
        var query = _context.Anomalies.AsQueryable();

        if (projectId.HasValue)
        {
            query = query.Where(a => a.ProjectId == projectId.Value);
        }

        if (sessionId.HasValue)
        {
            query = query.Where(a => a.SessionId == sessionId.Value);
        }

        if (!string.IsNullOrEmpty(category))
        {
            query = query.Where(a => a.Type == category);
        }

        if (minConfidence.HasValue)
        {
            query = query.Where(a => a.Confidence >= minConfidence.Value);
        }

        return await query.OrderByDescending(a => a.Confidence).Take(limit).ToListAsync();
    }

    public async Task<List<StoredCorrelation>> GetCorrelationsAsync(
        int? projectId = null,
        int? sessionId = null
    )
    {
        var query = _context.Correlations.AsQueryable();

        if (projectId.HasValue)
        {
            query = query.Where(c => c.ProjectId == projectId.Value);
        }

        if (sessionId.HasValue)
        {
            query = query.Where(c => c.SessionId == sessionId.Value);
        }

        return await query.OrderByDescending(c => c.EntryCount).ToListAsync();
    }

    public async Task<Dictionary<string, int>> GetServiceStatisticsAsync(
        int? projectId = null,
        int? sessionId = null
    )
    {
        var query = _context.LogEntries.AsQueryable();

        if (projectId.HasValue)
        {
            query = query.Where(e => e.ProjectId == projectId.Value);
        }

        if (sessionId.HasValue)
        {
            query = query.Where(e => e.SessionId == sessionId.Value);
        }

        return await query
            .Where(e => !string.IsNullOrEmpty(e.Service))
            .GroupBy(e => e.Service)
            .Select(g => new { Service = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Service!, x => x.Count);
    }

    public async Task<Dictionary<Models.LogLevel, int>> GetLogLevelStatisticsAsync(
        int? projectId = null,
        int? sessionId = null
    )
    {
        var query = _context.LogEntries.AsQueryable();

        if (projectId.HasValue)
        {
            query = query.Where(e => e.ProjectId == projectId.Value);
        }

        if (sessionId.HasValue)
        {
            query = query.Where(e => e.SessionId == sessionId.Value);
        }

        return await query
            .GroupBy(e => e.Level)
            .Select(g => new { Level = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => (Models.LogLevel)x.Level, x => x.Count);
    }

    #endregion
}
