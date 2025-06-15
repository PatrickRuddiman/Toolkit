using System.Diagnostics;
using System.Text;
using System.Text.Json;
using AnalyzeLogs.Data;
using AnalyzeLogs.Models;
using AnalyzeLogs.Services.Analysis;
using AnalyzeLogs.Services.Reporting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AnalyzeLogs.Services.Query;

/// <summary>
/// Implementation of the query service for natural language queries.
/// </summary>
public class QueryService : IQueryService
{
    private readonly LogAnalyzerDbContext _dbContext;
    private readonly IOpenAIService _openAIService;
    private readonly IVectorSearchService _vectorSearchService;
    private readonly IReportService _reportService;
    private readonly ILogger<QueryService> _logger;

    // Simple in-memory store for conversations (in a real app, this would be persisted)
    private readonly Dictionary<Guid, List<(string Query, string Response)>> _conversations = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryService"/> class.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    /// <param name="openAIService">The OpenAI service.</param>
    /// <param name="vectorSearchService">The vector search service.</param>
    /// <param name="reportService">The report service.</param>
    /// <param name="logger">The logger.</param>
    public QueryService(
        LogAnalyzerDbContext dbContext,
        IOpenAIService openAIService,
        IVectorSearchService vectorSearchService,
        IReportService reportService,
        ILogger<QueryService> logger
    )
    {
        _dbContext = dbContext;
        _openAIService = openAIService;
        _vectorSearchService = vectorSearchService;
        _reportService = reportService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<QueryResult> ExecuteQueryAsync(
        Guid sessionId,
        string query,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogInformation(
            "Executing query for session {SessionId}: {Query}",
            sessionId,
            query
        );

        var stopwatch = Stopwatch.StartNew();
        var result = new QueryResult
        {
            Metadata = new QueryMetadata { ExecutedAt = DateTime.UtcNow },
        };

        try
        {
            // Validate the session exists
            var session = await _dbContext
                .Sessions.Include(s => s.Project)
                .FirstOrDefaultAsync(s => s.SessionId == sessionId, cancellationToken);

            if (session == null)
            {
                result.ErrorMessage = $"Session with ID {sessionId} not found.";
                _logger.LogWarning("Session with ID {SessionId} not found", sessionId);
                return result;
            }

            // Get session context for the AI
            string sessionContext = await GetSessionContextAsync(session, cancellationToken);

            // Interpret the query
            string queryIntentJson = await _openAIService.InterpretQueryAsync(
                query,
                session.Project.Name,
                sessionContext
            );

            // Parse the query intent
            var queryIntent = JsonSerializer.Deserialize<QueryIntent>(
                queryIntentJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );

            if (queryIntent == null)
            {
                result.ErrorMessage = "Failed to interpret query intent.";
                _logger.LogError("Failed to interpret query intent for query: {Query}", query);
                return result;
            }

            result.Metadata.QueryType = queryIntent.QueryType;

            // Execute the query based on its type
            switch (queryIntent.QueryType.ToLowerInvariant())
            {
                case "filter":
                    await ExecuteFilterQueryAsync(queryIntent, result, session, cancellationToken);
                    break;
                case "aggregate":
                    await ExecuteAggregateQueryAsync(
                        queryIntent,
                        result,
                        session,
                        cancellationToken
                    );
                    break;
                case "vector":
                    await ExecuteVectorQueryAsync(queryIntent, result, session, cancellationToken);
                    break;
                case "hybrid":
                    await ExecuteHybridQueryAsync(queryIntent, result, session, cancellationToken);
                    break;
                case "anomaly":
                    await ExecuteAnomalyQueryAsync(queryIntent, result, session, cancellationToken);
                    break;
                case "trend":
                    await ExecuteTrendQueryAsync(queryIntent, result, session, cancellationToken);
                    break;
                case "comparative":
                    await ExecuteComparativeQueryAsync(
                        queryIntent,
                        result,
                        session,
                        cancellationToken
                    );
                    break;
                default:
                    result.ErrorMessage = $"Unsupported query type: {queryIntent.QueryType}";
                    _logger.LogWarning(
                        "Unsupported query type: {QueryType}",
                        queryIntent.QueryType
                    );
                    break;
            }

            // Store the query in history
            await StoreQueryHistoryAsync(sessionId, query, result, cancellationToken);
        }
        catch (Exception ex)
        {
            result.ErrorMessage = $"Error executing query: {ex.Message}";
            _logger.LogError(ex, "Error executing query: {Query}", query);
        }

        stopwatch.Stop();
        result.Metadata.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;

        return result;
    }

    /// <inheritdoc/>
    public async Task<(QueryResult Result, Guid ConversationId)> ExecuteInteractiveQueryAsync(
        Guid sessionId,
        string query,
        Guid? conversationId = null,
        CancellationToken cancellationToken = default
    )
    {
        // Create a new conversation if none exists
        var actualConversationId = conversationId ?? Guid.NewGuid();

        if (!_conversations.ContainsKey(actualConversationId))
        {
            _conversations[actualConversationId] = new List<(string, string)>();
        }

        // Execute the query
        var result = await ExecuteQueryAsync(sessionId, query, cancellationToken);

        // Store the conversation
        _conversations[actualConversationId].Add((query, result.TextResponse));

        // Generate a markdown report for this conversation
        await _reportService.AppendToQueryReportAsync(
            sessionId,
            actualConversationId,
            query,
            result,
            cancellationToken
        );

        return (result, actualConversationId);
    }

    /// <inheritdoc/>
    public async Task<List<QueryHistoryItem>> GetQueryHistoryAsync(
        Guid sessionId,
        int limit = 10,
        CancellationToken cancellationToken = default
    )
    {
        // In a real implementation, this would query a database table of query history
        // For now, we'll return a placeholder
        return new List<QueryHistoryItem>
        {
            new()
            {
                QueryId = Guid.NewGuid(),
                SessionId = sessionId,
                QueryText = "Show me all errors from yesterday",
                ExecutedAt = DateTime.UtcNow.AddDays(-1),
                ResultSummary = "Found 15 error logs",
            },
        };
    }

    /// <inheritdoc/>
    public async Task<List<string>> GetQuerySuggestionsAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default
    )
    {
        var session = await _dbContext.Sessions.FindAsync(
            new object[] { sessionId },
            cancellationToken
        );

        if (session == null)
        {
            return new List<string>();
        }

        // In a real implementation, these would be generated based on the session data
        // For now, return some generic suggestions
        return new List<string>
        {
            "Show me all error logs from the last hour",
            "What was the p95 latency for auth-service today?",
            "Find anomalies in payment-service logs",
            "Compare error rates between yesterday and today",
            "Show me logs with correlation ID abc-123",
        };
    }

    #region Private Helper Methods

    private async Task<string> GetSessionContextAsync(
        Session session,
        CancellationToken cancellationToken
    )
    {
        var builder = new StringBuilder();

        builder.AppendLine($"Session ID: {session.SessionId}");
        builder.AppendLine($"Project: {session.Project.Name}");
        builder.AppendLine($"Session Start Time: {session.StartTime}");

        if (session.EndTime.HasValue)
        {
            builder.AppendLine($"Session End Time: {session.EndTime}");
        }

        // Get some basic stats about the session
        int logCount = await _dbContext.LogEntries.CountAsync(
            l => l.SessionId == session.SessionId,
            cancellationToken
        );
        int serviceCount = await _dbContext
            .LogEntries.Where(l => l.SessionId == session.SessionId && l.ServiceId != null)
            .Select(l => l.ServiceId)
            .Distinct()
            .CountAsync(cancellationToken);
        int errorCount = await _dbContext
            .LogEntries.Where(l =>
                l.SessionId == session.SessionId && l.SeverityLevel.LevelName == "ERROR"
                || l.SeverityLevel.LevelName == "CRITICAL"
                || l.SeverityLevel.LevelName == "FATAL"
            )
            .CountAsync(cancellationToken);

        builder.AppendLine($"Log Entry Count: {logCount}");
        builder.AppendLine($"Service Count: {serviceCount}");
        builder.AppendLine($"Error Count: {errorCount}");

        return builder.ToString();
    }

    private async Task ExecuteFilterQueryAsync(
        QueryIntent queryIntent,
        QueryResult result,
        Session session,
        CancellationToken cancellationToken
    )
    {
        // Execute the SQL query if provided
        if (!string.IsNullOrEmpty(queryIntent.SqlQuery))
        {
            try
            {
                // CAUTION: In a real implementation, you would need to sanitize this to prevent SQL injection
                // This is just a placeholder for the concept
                var entries = await _dbContext
                    .LogEntries.FromSqlRaw(queryIntent.SqlQuery)
                    .Include(l => l.Service)
                    .Include(l => l.SeverityLevel)
                    .ToListAsync(cancellationToken);

                result.LogEntries = entries;
                result.SqlQuery = queryIntent.SqlQuery;
                result.Metadata.EntriesProcessed = entries.Count;

                // Generate a response
                result.TextResponse =
                    $"Found {entries.Count} log entries matching your filter criteria.";

                if (entries.Count > 0)
                {
                    // Sample a few entries to show in the response
                    var sampleEntries = entries.Take(Math.Min(5, entries.Count)).ToList();
                    result.TextResponse += "\n\nSample entries:";

                    foreach (var entry in sampleEntries)
                    {
                        result.TextResponse +=
                            $"\n- [{entry.TimestampUTC}] [{entry.SeverityLevel?.LevelName}] {entry.NormalizedMessage}";
                    }
                }
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"Error executing SQL query: {ex.Message}";
                _logger.LogError(ex, "Error executing SQL query: {Query}", queryIntent.SqlQuery);
            }
        }
        else
        {
            // Build a query based on parameters
            var query = _dbContext.LogEntries.Where(l => l.SessionId == session.SessionId);

            // Apply filters based on parameters
            if (queryIntent.Parameters?.TimeRange != null)
            {
                if (!string.IsNullOrEmpty(queryIntent.Parameters.TimeRange.Start))
                {
                    DateTime startTime = DateTime.Parse(queryIntent.Parameters.TimeRange.Start);
                    query = query.Where(l => l.TimestampUTC >= startTime);
                }

                if (!string.IsNullOrEmpty(queryIntent.Parameters.TimeRange.End))
                {
                    DateTime endTime = DateTime.Parse(queryIntent.Parameters.TimeRange.End);
                    query = query.Where(l => l.TimestampUTC <= endTime);
                }
            }

            if (queryIntent.Parameters?.Services?.Any() == true)
            {
                query = query.Where(l =>
                    l.Service != null
                    && queryIntent.Parameters.Services.Contains(l.Service.ServiceName)
                );
            }

            if (queryIntent.Parameters?.SeverityLevels?.Any() == true)
            {
                query = query.Where(l =>
                    l.SeverityLevel != null
                    && queryIntent.Parameters.SeverityLevels.Contains(l.SeverityLevel.LevelName)
                );
            }

            if (!string.IsNullOrEmpty(queryIntent.Parameters?.TextSearch))
            {
                query = query.Where(l =>
                    l.NormalizedMessage.Contains(queryIntent.Parameters.TextSearch)
                    || l.RawMessage.Contains(queryIntent.Parameters.TextSearch)
                );
            }

            if (queryIntent.Parameters?.CorrelationIds?.Any() == true)
            {
                query = query.Where(l =>
                    queryIntent.Parameters.CorrelationIds.Contains(l.CorrelationId)
                );
            }

            // Include related entities
            query = query.Include(l => l.Service).Include(l => l.SeverityLevel);

            // Apply ordering if specified
            if (!string.IsNullOrEmpty(queryIntent.Parameters?.OrderBy))
            {
                switch (queryIntent.Parameters.OrderBy.ToLowerInvariant())
                {
                    case "timestamp":
                        query =
                            queryIntent.Parameters.OrderDirection?.ToLowerInvariant()
                            == "descending"
                                ? query.OrderByDescending(l => l.TimestampUTC)
                                : query.OrderBy(l => l.TimestampUTC);
                        break;
                    case "severity":
                        query =
                            queryIntent.Parameters.OrderDirection?.ToLowerInvariant()
                            == "descending"
                                ? query.OrderByDescending(l => l.SeverityLevel.LevelName)
                                : query.OrderBy(l => l.SeverityLevel.LevelName);
                        break;
                    default:
                        query = query.OrderBy(l => l.TimestampUTC);
                        break;
                }
            }
            else
            {
                // Default ordering by timestamp
                query = query.OrderBy(l => l.TimestampUTC);
            }

            // Apply limit if specified
            if (queryIntent.Parameters?.Limit > 0)
            {
                query = query.Take(queryIntent.Parameters.Limit.Value);
            }
            else
            {
                // Default limit
                query = query.Take(100);
            }

            // Execute the query
            var entries = await query.ToListAsync(cancellationToken);

            result.LogEntries = entries;
            result.Metadata.EntriesProcessed = entries.Count;

            // Generate a response
            result.TextResponse =
                $"Found {entries.Count} log entries matching your filter criteria.";

            if (entries.Count > 0)
            {
                // Sample a few entries to show in the response
                var sampleEntries = entries.Take(Math.Min(5, entries.Count)).ToList();
                result.TextResponse += "\n\nSample entries:";

                foreach (var entry in sampleEntries)
                {
                    result.TextResponse +=
                        $"\n- [{entry.TimestampUTC}] [{entry.SeverityLevel?.LevelName}] {entry.NormalizedMessage}";
                }
            }
        }
    }

    private async Task ExecuteAggregateQueryAsync(
        QueryIntent queryIntent,
        QueryResult result,
        Session session,
        CancellationToken cancellationToken
    )
    {
        // Placeholder - in a real implementation, this would handle aggregation queries
        // For example, counts, averages, percentiles, etc.
        result.TextResponse = "Aggregate query support is coming soon.";
    }

    private async Task ExecuteVectorQueryAsync(
        QueryIntent queryIntent,
        QueryResult result,
        Session session,
        CancellationToken cancellationToken
    )
    {
        if (
            queryIntent.VectorSearch?.Required == true
            && !string.IsNullOrEmpty(queryIntent.VectorSearch.SearchText)
        )
        {
            try
            {
                // Generate an embedding for the search text
                var embeddings = await _openAIService.GenerateEmbeddingsAsync(
                    new[] { queryIntent.VectorSearch.SearchText }
                );

                if (embeddings.Count > 0)
                {
                    // Perform vector search
                    float threshold = queryIntent.VectorSearch.SimilarityThreshold ?? 0.7f;
                    int limit = 100; // Default limit
                    var vectorResults = await _vectorSearchService.FindSimilarLogEntriesAsync(
                        session.SessionId,
                        embeddings[0],
                        threshold,
                        limit,
                        cancellationToken
                    );

                    // Get full log entries
                    var logEntryIds = vectorResults.Select(r => r.LogEntryId).ToList();
                    var entries = await _dbContext
                        .LogEntries.Where(l => logEntryIds.Contains(l.LogEntryId))
                        .Include(l => l.Service)
                        .Include(l => l.SeverityLevel)
                        .ToListAsync(cancellationToken);

                    // Sort entries by similarity score
                    var sortedEntries = new List<LogEntry>();
                    foreach (var vectorResult in vectorResults)
                    {
                        var entry = entries.FirstOrDefault(e =>
                            e.LogEntryId == vectorResult.LogEntryId
                        );
                        if (entry != null)
                        {
                            sortedEntries.Add(entry);
                        }
                    }

                    result.LogEntries = sortedEntries;
                    result.Metadata.EntriesProcessed = sortedEntries.Count;

                    // Generate a response
                    result.TextResponse =
                        $"Found {sortedEntries.Count} log entries semantically similar to '{queryIntent.VectorSearch.SearchText}'.";

                    if (sortedEntries.Count > 0)
                    {
                        // Sample a few entries to show in the response
                        var sampleEntries = sortedEntries
                            .Take(Math.Min(5, sortedEntries.Count))
                            .ToList();
                        result.TextResponse += "\n\nSample entries:";

                        foreach (var entry in sampleEntries)
                        {
                            result.TextResponse +=
                                $"\n- [{entry.TimestampUTC}] [{entry.SeverityLevel?.LevelName}] {entry.NormalizedMessage}";
                        }
                    }
                }
                else
                {
                    result.ErrorMessage = "Failed to generate embedding for search text.";
                }
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"Error executing vector search: {ex.Message}";
                _logger.LogError(
                    ex,
                    "Error executing vector search for query: {Query}",
                    queryIntent.VectorSearch.SearchText
                );
            }
        }
        else
        {
            result.ErrorMessage = "Vector search parameters are missing or invalid.";
        }
    }

    private async Task ExecuteHybridQueryAsync(
        QueryIntent queryIntent,
        QueryResult result,
        Session session,
        CancellationToken cancellationToken
    )
    {
        // Placeholder - in a real implementation, this would handle hybrid SQL + vector queries
        result.TextResponse = "Hybrid query support is coming soon.";
    }

    private async Task ExecuteAnomalyQueryAsync(
        QueryIntent queryIntent,
        QueryResult result,
        Session session,
        CancellationToken cancellationToken
    )
    {
        // Query the database for anomalies
        var anomalies = await _dbContext
            .Anomalies.Where(a => a.SessionId == session.SessionId)
            .Include(a => a.AnomalyLogEntries)
            .ThenInclude(ale => ale.LogEntry)
            .ThenInclude(l => l.Service)
            .Include(a => a.AnomalyLogEntries)
            .ThenInclude(ale => ale.LogEntry)
            .ThenInclude(l => l.SeverityLevel)
            .ToListAsync(cancellationToken);

        result.Anomalies = anomalies;

        // Generate a response
        if (anomalies.Count > 0)
        {
            result.TextResponse = $"Found {anomalies.Count} anomalies in the session.";

            // Sample a few anomalies to show in the response
            var sampleAnomalies = anomalies.Take(Math.Min(3, anomalies.Count)).ToList();
            result.TextResponse += "\n\nSample anomalies:";

            foreach (var anomaly in sampleAnomalies)
            {
                result.TextResponse += $"\n- {anomaly.AnomalyType}";
                result.TextResponse += $"\n  Severity: {anomaly.Severity}";
                result.TextResponse += $"\n  Detected At: {anomaly.DetectionTime}";
                result.TextResponse += $"\n  Description: {anomaly.Description}";
            }
        }
        else
        {
            result.TextResponse = "No anomalies found in the session.";
        }
    }

    private async Task ExecuteTrendQueryAsync(
        QueryIntent queryIntent,
        QueryResult result,
        Session session,
        CancellationToken cancellationToken
    )
    {
        // Placeholder - in a real implementation, this would handle trend analysis queries
        result.TextResponse = "Trend analysis support is coming soon.";
    }

    private async Task ExecuteComparativeQueryAsync(
        QueryIntent queryIntent,
        QueryResult result,
        Session session,
        CancellationToken cancellationToken
    )
    {
        // Placeholder - in a real implementation, this would handle comparative queries
        result.TextResponse = "Comparative query support is coming soon.";
    }

    private async Task StoreQueryHistoryAsync(
        Guid sessionId,
        string query,
        QueryResult result,
        CancellationToken cancellationToken
    )
    {
        // In a real implementation, this would store the query in a database table
        // For now, this is just a placeholder
        _logger.LogInformation("Storing query in history: {Query}", query);
    }

    #endregion
}

/// <summary>
/// Represents the parsed intent of a natural language query.
/// </summary>
internal class QueryIntent
{
    /// <summary>
    /// Gets or sets the type of query.
    /// </summary>
    public string QueryType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the parameters for the query.
    /// </summary>
    public QueryParameters? Parameters { get; set; }

    /// <summary>
    /// Gets or sets the SQL query to execute.
    /// </summary>
    public string? SqlQuery { get; set; }

    /// <summary>
    /// Gets or sets the vector search parameters.
    /// </summary>
    public VectorSearchParameters? VectorSearch { get; set; }

    /// <summary>
    /// Gets or sets the explanation of the query.
    /// </summary>
    public string? Explanation { get; set; }
}

/// <summary>
/// Represents the parameters for a query.
/// </summary>
internal class QueryParameters
{
    /// <summary>
    /// Gets or sets the time range for the query.
    /// </summary>
    public TimeRangeParameter? TimeRange { get; set; }

    /// <summary>
    /// Gets or sets the services to filter by.
    /// </summary>
    public List<string>? Services { get; set; }

    /// <summary>
    /// Gets or sets the severity levels to filter by.
    /// </summary>
    public List<string>? SeverityLevels { get; set; }

    /// <summary>
    /// Gets or sets the text to search for.
    /// </summary>
    public string? TextSearch { get; set; }

    /// <summary>
    /// Gets or sets the user IDs to filter by.
    /// </summary>
    public List<string>? UserIds { get; set; }

    /// <summary>
    /// Gets or sets the correlation IDs to filter by.
    /// </summary>
    public List<string>? CorrelationIds { get; set; }

    /// <summary>
    /// Gets or sets the similarity threshold for vector searches.
    /// </summary>
    public float? SearchThreshold { get; set; }

    /// <summary>
    /// Gets or sets the aggregate function to apply.
    /// </summary>
    public string? AggregateFunction { get; set; }

    /// <summary>
    /// Gets or sets the field to aggregate on.
    /// </summary>
    public string? AggregateField { get; set; }

    /// <summary>
    /// Gets or sets the limit on the number of results to return.
    /// </summary>
    public int? Limit { get; set; }

    /// <summary>
    /// Gets or sets the field to order by.
    /// </summary>
    public string? OrderBy { get; set; }

    /// <summary>
    /// Gets or sets the direction to order in.
    /// </summary>
    public string? OrderDirection { get; set; }
}

/// <summary>
/// Represents a time range parameter.
/// </summary>
internal class TimeRangeParameter
{
    /// <summary>
    /// Gets or sets the start time.
    /// </summary>
    public string? Start { get; set; }

    /// <summary>
    /// Gets or sets the end time.
    /// </summary>
    public string? End { get; set; }

    /// <summary>
    /// Gets or sets the relative description of the time range.
    /// </summary>
    public string? RelativeDescription { get; set; }
}

/// <summary>
/// Represents vector search parameters.
/// </summary>
internal class VectorSearchParameters
{
    /// <summary>
    /// Gets or sets a value indicating whether vector search is required.
    /// </summary>
    public bool Required { get; set; }

    /// <summary>
    /// Gets or sets the text to search for.
    /// </summary>
    public string? SearchText { get; set; }

    /// <summary>
    /// Gets or sets the similarity threshold.
    /// </summary>
    public float? SimilarityThreshold { get; set; }
}
