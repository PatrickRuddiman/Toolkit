using AnalyzeLogs.Models;

namespace AnalyzeLogs.Services.Analysis;

/// <summary>
/// Service for performing vector-based similarity searches on log entries.
/// </summary>
public interface IVectorSearchService
{
    /// <summary>
    /// Indexes a log entry for vector search.
    /// </summary>
    /// <param name="logEntry">The log entry to index.</param>
    /// <param name="embedding">The embedding vector for the log entry.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task IndexLogEntryAsync(
        LogEntry logEntry,
        float[] embedding,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Indexes a batch of log entries for vector search.
    /// </summary>
    /// <param name="logEntries">The log entries to index.</param>
    /// <param name="embeddings">The embedding vectors for the log entries.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task IndexLogEntriesBatchAsync(
        IList<LogEntry> logEntries,
        IList<float[]> embeddings,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Stores embeddings for log entries.
    /// </summary>
    /// <param name="embeddings">The embedding vectors to store.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The number of embeddings stored.</returns>
    Task<int> StoreEmbeddingsAsync(
        IList<float[]> embeddings,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Finds log entries similar to a query embedding.
    /// </summary>
    /// <param name="sessionId">The session ID to search within.</param>
    /// <param name="queryEmbedding">The query embedding vector.</param>
    /// <param name="similarityThreshold">The minimum similarity score (0-1) for results.</param>
    /// <param name="limit">The maximum number of results to return.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A list of vector search results, ordered by similarity (highest first).</returns>
    Task<List<VectorSearchResult>> FindSimilarLogEntriesAsync(
        Guid sessionId,
        float[] queryEmbedding,
        float similarityThreshold = 0.7f,
        int limit = 100,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Finds log entries similar to a specific log entry.
    /// </summary>
    /// <param name="logEntryId">The ID of the log entry to find similar entries for.</param>
    /// <param name="similarityThreshold">The minimum similarity score (0-1) for results.</param>
    /// <param name="limit">The maximum number of results to return.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A list of vector search results, ordered by similarity (highest first).</returns>
    Task<List<VectorSearchResult>> FindSimilarToLogEntryAsync(
        long logEntryId,
        float similarityThreshold = 0.7f,
        int limit = 100,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Detects outlier log entries based on their vector embeddings.
    /// </summary>
    /// <param name="sessionId">The session ID to analyze.</param>
    /// <param name="outlierThreshold">The threshold for determining outliers (lower means more outliers).</param>
    /// <param name="limit">The maximum number of outliers to return.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A list of outlier log entries, ordered by outlier score (highest first).</returns>
    Task<List<OutlierResult>> DetectOutliersAsync(
        Guid sessionId,
        float outlierThreshold = 0.3f,
        int limit = 100,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Clears the vector index for a session.
    /// </summary>
    /// <param name="sessionId">The session ID to clear.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ClearSessionIndexAsync(Guid sessionId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a result from a vector similarity search.
/// </summary>
public class VectorSearchResult
{
    /// <summary>
    /// Gets or sets the ID of the log entry.
    /// </summary>
    public long LogEntryId { get; set; }

    /// <summary>
    /// Gets or sets the similarity score (0-1) between the query and the log entry.
    /// </summary>
    public float SimilarityScore { get; set; }
}

/// <summary>
/// Represents an outlier log entry detected by vector analysis.
/// </summary>
public class OutlierResult
{
    /// <summary>
    /// Gets or sets the ID of the log entry.
    /// </summary>
    public long LogEntryId { get; set; }

    /// <summary>
    /// Gets or sets the outlier score (higher means more unusual).
    /// </summary>
    public float OutlierScore { get; set; }
}
