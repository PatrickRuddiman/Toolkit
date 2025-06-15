using AnalyzeLogs.Models;

namespace AnalyzeLogs.Services.Query;

/// <summary>
/// Service for querying log data using natural language.
/// </summary>
public interface IQueryService
{
    /// <summary>
    /// Executes a natural language query against log data.
    /// </summary>
    /// <param name="sessionId">The ID of the session to query.</param>
    /// <param name="query">The natural language query to execute.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A QueryResult containing the query response and visualization data.</returns>
    Task<QueryResult> ExecuteQueryAsync(
        Guid sessionId,
        string query,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Executes a natural language query in interactive mode, appending to an ongoing conversation.
    /// </summary>
    /// <param name="sessionId">The ID of the session to query.</param>
    /// <param name="query">The natural language query to execute.</param>
    /// <param name="conversationId">The ID of the conversation to continue, or null to start a new one.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A QueryResult containing the query response and visualization data, along with the conversation ID.</returns>
    Task<(QueryResult Result, Guid ConversationId)> ExecuteInteractiveQueryAsync(
        Guid sessionId,
        string query,
        Guid? conversationId = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Gets the query history for a session.
    /// </summary>
    /// <param name="sessionId">The ID of the session to get query history for.</param>
    /// <param name="limit">The maximum number of queries to return.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A list of recent queries for the session.</returns>
    Task<List<QueryHistoryItem>> GetQueryHistoryAsync(
        Guid sessionId,
        int limit = 10,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Gets the available query suggestions for a session.
    /// </summary>
    /// <param name="sessionId">The ID of the session to get suggestions for.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A list of query suggestions based on the session data.</returns>
    Task<List<string>> GetQuerySuggestionsAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default
    );
}

/// <summary>
/// Represents the result of a query.
/// </summary>
public class QueryResult
{
    /// <summary>
    /// Gets or sets the text response to the query.
    /// </summary>
    public string TextResponse { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the log entries returned by the query, if any.
    /// </summary>
    public List<LogEntry>? LogEntries { get; set; }

    /// <summary>
    /// Gets or sets the anomalies related to the query, if any.
    /// </summary>
    public List<Anomaly>? Anomalies { get; set; }

    /// <summary>
    /// Gets or sets the Mermaid diagram code, if any.
    /// </summary>
    public string? MermaidDiagramCode { get; set; }

    /// <summary>
    /// Gets or sets the SQL query generated from the natural language query, if applicable.
    /// </summary>
    public string? SqlQuery { get; set; }

    /// <summary>
    /// Gets or sets any error message that occurred during query execution.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the metadata about the query execution.
    /// </summary>
    public QueryMetadata Metadata { get; set; } = new QueryMetadata();
}

/// <summary>
/// Metadata about a query execution.
/// </summary>
public class QueryMetadata
{
    /// <summary>
    /// Gets or sets the type of query that was executed.
    /// </summary>
    public string QueryType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp when the query was executed.
    /// </summary>
    public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the number of entries processed during the query.
    /// </summary>
    public int EntriesProcessed { get; set; }

    /// <summary>
    /// Gets or sets the time taken to execute the query in milliseconds.
    /// </summary>
    public long ExecutionTimeMs { get; set; }
}

/// <summary>
/// Represents an item in the query history.
/// </summary>
public class QueryHistoryItem
{
    /// <summary>
    /// Gets or sets the unique identifier for the query.
    /// </summary>
    public Guid QueryId { get; set; }

    /// <summary>
    /// Gets or sets the session ID the query was executed against.
    /// </summary>
    public Guid SessionId { get; set; }

    /// <summary>
    /// Gets or sets the natural language query text.
    /// </summary>
    public string QueryText { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp when the query was executed.
    /// </summary>
    public DateTime ExecutedAt { get; set; }

    /// <summary>
    /// Gets or sets a brief summary of the query result.
    /// </summary>
    public string ResultSummary { get; set; } = string.Empty;
}
