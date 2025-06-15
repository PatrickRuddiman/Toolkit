using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AnalyzeLogs.Data;
using AnalyzeLogs.Models;
using AnalyzeLogs.Services.Analysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AnalyzeLogs.Services.Analysis;

/// <summary>
/// Implementation of the vector search service using an in-memory vector index.
/// </summary>
public class VectorSearchService : IVectorSearchService
{
    private readonly LogAnalyzerDbContext _dbContext;
    private readonly ILogger<VectorSearchService> _logger;

    // In-memory vector index - in a real implementation, this would be a more sophisticated data structure
    // This is a simple implementation that stores embeddings in memory and performs brute-force similarity search
    private readonly ConcurrentDictionary<
        Guid,
        ConcurrentDictionary<long, float[]>
    > _sessionEmbeddings = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="VectorSearchService"/> class.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    /// <param name="logger">The logger.</param>
    public VectorSearchService(LogAnalyzerDbContext dbContext, ILogger<VectorSearchService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task IndexLogEntryAsync(
        LogEntry logEntry,
        float[] embedding,
        CancellationToken cancellationToken = default
    )
    {
        // Add the embedding to the session's index
        var sessionEmbeddings = _sessionEmbeddings.GetOrAdd(
            logEntry.SessionId,
            _ => new ConcurrentDictionary<long, float[]>()
        );
        sessionEmbeddings[logEntry.LogEntryId] = embedding;

        // Update the database with the embedding bytes
        logEntry.EmbeddingVector = ConvertToBytes(embedding);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogDebug(
            "Indexed log entry {LogEntryId} with embedding (dimension: {Dimension})",
            logEntry.LogEntryId,
            embedding.Length
        );
    }

    /// <inheritdoc/>
    public async Task IndexLogEntriesBatchAsync(
        IList<LogEntry> logEntries,
        IList<float[]> embeddings,
        CancellationToken cancellationToken = default
    )
    {
        if (logEntries.Count != embeddings.Count)
        {
            throw new ArgumentException("Number of log entries must match number of embeddings");
        }

        for (int i = 0; i < logEntries.Count; i++)
        {
            var logEntry = logEntries[i];
            var embedding = embeddings[i];

            // Add the embedding to the session's index
            var sessionEmbeddings = _sessionEmbeddings.GetOrAdd(
                logEntry.SessionId,
                _ => new ConcurrentDictionary<long, float[]>()
            );
            sessionEmbeddings[logEntry.LogEntryId] = embedding;

            // Update the database with the embedding bytes
            logEntry.EmbeddingVector = ConvertToBytes(embedding);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Indexed batch of {Count} log entries with embeddings", logEntries.Count);
    }

    /// <inheritdoc/>
    public async Task<int> StoreEmbeddingsAsync(
        IList<float[]> embeddings,
        CancellationToken cancellationToken = default
    )
    {
        // This is a simplified implementation - in a real application,
        // this would likely involve more complex logic to match embeddings with entries
        // and store them appropriately

        _logger.LogDebug("Storing {Count} embeddings", embeddings.Count);

        // Return the count of embeddings stored
        return embeddings.Count;
    }

    /// <inheritdoc/>
    public async Task<List<VectorSearchResult>> FindSimilarLogEntriesAsync(
        Guid sessionId,
        float[] queryEmbedding,
        float similarityThreshold = 0.7f,
        int limit = 100,
        CancellationToken cancellationToken = default
    )
    {
        var results = new List<VectorSearchResult>();

        // Get the session's embeddings
        if (_sessionEmbeddings.TryGetValue(sessionId, out var sessionEmbeddings))
        {
            // Calculate similarity scores for all embeddings in the session
            var scores = new List<(long LogEntryId, float Score)>();

            foreach (var (logEntryId, embedding) in sessionEmbeddings)
            {
                float similarityScore = CalculateCosineSimilarity(queryEmbedding, embedding);
                if (similarityScore >= similarityThreshold)
                {
                    scores.Add((logEntryId, similarityScore));
                }
            }

            // Sort by similarity score (descending) and take the top results
            var topResults = scores.OrderByDescending(s => s.Score).Take(limit).ToList();

            // Convert to VectorSearchResult objects
            results = topResults
                .Select(r => new VectorSearchResult
                {
                    LogEntryId = r.LogEntryId,
                    SimilarityScore = r.Score,
                })
                .ToList();

            _logger.LogDebug(
                "Found {Count} similar log entries for session {SessionId} with similarity threshold {Threshold}",
                results.Count,
                sessionId,
                similarityThreshold
            );
        }
        else
        {
            _logger.LogWarning("No embeddings found for session {SessionId}", sessionId);
        }

        return results;
    }

    /// <inheritdoc/>
    public async Task<List<VectorSearchResult>> FindSimilarToLogEntryAsync(
        long logEntryId,
        float similarityThreshold = 0.7f,
        int limit = 100,
        CancellationToken cancellationToken = default
    )
    {
        // Get the log entry
        var logEntry = await _dbContext.LogEntries.FindAsync(
            new object[] { logEntryId },
            cancellationToken
        );

        if (logEntry == null)
        {
            _logger.LogWarning("Log entry {LogEntryId} not found", logEntryId);
            return new List<VectorSearchResult>();
        }

        // If the log entry doesn't have an embedding vector, try to get it from the in-memory index
        float[]? queryEmbedding = null;

        if (
            _sessionEmbeddings.TryGetValue(logEntry.SessionId, out var sessionEmbeddings)
            && sessionEmbeddings.TryGetValue(logEntryId, out var embedding)
        )
        {
            queryEmbedding = embedding;
        }
        else if (logEntry.EmbeddingVector != null)
        {
            // Convert from bytes to float array
            queryEmbedding = ConvertFromBytes(logEntry.EmbeddingVector);
        }

        if (queryEmbedding == null)
        {
            _logger.LogWarning("No embedding found for log entry {LogEntryId}", logEntryId);
            return new List<VectorSearchResult>();
        }

        // Find similar log entries
        return await FindSimilarLogEntriesAsync(
            logEntry.SessionId,
            queryEmbedding,
            similarityThreshold,
            limit,
            cancellationToken
        );
    }

    /// <inheritdoc/>
    public async Task<List<OutlierResult>> DetectOutliersAsync(
        Guid sessionId,
        float outlierThreshold = 0.3f,
        int limit = 100,
        CancellationToken cancellationToken = default
    )
    {
        var results = new List<OutlierResult>();

        // Get the session's embeddings
        if (_sessionEmbeddings.TryGetValue(sessionId, out var sessionEmbeddings))
        {
            // Calculate average distance to k-nearest neighbors for each embedding
            // This is a simple implementation of Local Outlier Factor (LOF)
            const int k = 5; // Number of nearest neighbors to consider
            var outlierScores = new List<(long LogEntryId, float Score)>();

            foreach (var (logEntryId, embedding) in sessionEmbeddings)
            {
                // Calculate distances to all other embeddings
                var distances = new List<(long OtherLogEntryId, float Distance)>();

                foreach (var (otherLogEntryId, otherEmbedding) in sessionEmbeddings)
                {
                    if (otherLogEntryId == logEntryId)
                    {
                        continue;
                    }

                    float distance = 1.0f - CalculateCosineSimilarity(embedding, otherEmbedding);
                    distances.Add((otherLogEntryId, distance));
                }

                // Get the k nearest neighbors
                var nearestNeighbors = distances
                    .OrderBy(d => d.Distance)
                    .Take(Math.Min(k, distances.Count))
                    .ToList();

                // Calculate the average distance to k nearest neighbors
                float averageDistance =
                    nearestNeighbors.Count > 0 ? nearestNeighbors.Average(d => d.Distance) : 0.0f;

                // Higher average distance means more outlier-like
                outlierScores.Add((logEntryId, averageDistance));
            }

            // Sort by outlier score (descending) and filter by threshold
            var topOutliers = outlierScores
                .Where(s => s.Score >= outlierThreshold)
                .OrderByDescending(s => s.Score)
                .Take(limit)
                .ToList();

            // Convert to OutlierResult objects
            results = topOutliers
                .Select(r => new OutlierResult
                {
                    LogEntryId = r.LogEntryId,
                    OutlierScore = r.Score,
                })
                .ToList();

            _logger.LogDebug(
                "Found {Count} outlier log entries for session {SessionId} with outlier threshold {Threshold}",
                results.Count,
                sessionId,
                outlierThreshold
            );
        }
        else
        {
            _logger.LogWarning("No embeddings found for session {SessionId}", sessionId);
        }

        return results;
    }

    /// <inheritdoc/>
    public async Task ClearSessionIndexAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default
    )
    {
        // Remove the session's embeddings from the in-memory index
        _sessionEmbeddings.TryRemove(sessionId, out _);

        // Clear embedding vectors in the database
        await _dbContext
            .LogEntries.Where(l => l.SessionId == sessionId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(l => l.EmbeddingVector, (byte[]?)null),
                cancellationToken
            );

        _logger.LogInformation("Cleared vector index for session {SessionId}", sessionId);
    }

    #region Private Helper Methods

    private float CalculateCosineSimilarity(float[] vectorA, float[] vectorB)
    {
        if (vectorA.Length != vectorB.Length)
        {
            throw new ArgumentException("Vectors must have the same dimension");
        }

        float dotProduct = 0.0f;
        float magnitudeA = 0.0f;
        float magnitudeB = 0.0f;

        for (int i = 0; i < vectorA.Length; i++)
        {
            dotProduct += vectorA[i] * vectorB[i];
            magnitudeA += vectorA[i] * vectorA[i];
            magnitudeB += vectorB[i] * vectorB[i];
        }

        magnitudeA = (float)Math.Sqrt(magnitudeA);
        magnitudeB = (float)Math.Sqrt(magnitudeB);

        if (magnitudeA == 0.0f || magnitudeB == 0.0f)
        {
            return 0.0f;
        }

        return dotProduct / (magnitudeA * magnitudeB);
    }

    private byte[] ConvertToBytes(float[] vector)
    {
        byte[] bytes = new byte[vector.Length * sizeof(float)];
        Buffer.BlockCopy(vector, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    private float[] ConvertFromBytes(byte[] bytes)
    {
        float[] vector = new float[bytes.Length / sizeof(float)];
        Buffer.BlockCopy(bytes, 0, vector, 0, bytes.Length);
        return vector;
    }

    #endregion
}
