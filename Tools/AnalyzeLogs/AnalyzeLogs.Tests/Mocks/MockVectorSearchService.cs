using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AnalyzeLogs.Models;
using AnalyzeLogs.Services.Analysis;

namespace AnalyzeLogs.Tests.Mocks
{
    /// <summary>
    /// Mock implementation of the vector search service for testing.
    /// </summary>
    public class MockVectorSearchService : IVectorSearchService
    {
        private readonly Dictionary<
            Guid,
            List<(LogEntry LogEntry, float[] Embedding)>
        > _sessionIndices = new();
        private readonly Dictionary<long, float[]> _logEntryEmbeddings = new();
        private readonly Random _random = new Random(42); // Use fixed seed for reproducibility

        /// <summary>
        /// Initializes a new instance of the <see cref="MockVectorSearchService"/> class.
        /// </summary>
        public MockVectorSearchService() { }

        /// <inheritdoc/>
        public Task IndexLogEntryAsync(
            LogEntry logEntry,
            float[] embedding,
            CancellationToken cancellationToken = default
        )
        {
            if (logEntry.SessionId == default)
            {
                throw new ArgumentException(
                    "LogEntry must have a valid SessionId",
                    nameof(logEntry)
                );
            }

            // Store the embedding in the session index
            if (!_sessionIndices.TryGetValue(logEntry.SessionId, out var sessionIndex))
            {
                sessionIndex = new List<(LogEntry, float[])>();
                _sessionIndices[logEntry.SessionId] = sessionIndex;
            }

            sessionIndex.Add((logEntry, embedding));

            // Also store by log entry ID
            if (logEntry.LogEntryId > 0)
            {
                _logEntryEmbeddings[logEntry.LogEntryId] = embedding;
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task IndexLogEntriesBatchAsync(
            IList<LogEntry> logEntries,
            IList<float[]> embeddings,
            CancellationToken cancellationToken = default
        )
        {
            if (logEntries.Count != embeddings.Count)
            {
                throw new ArgumentException(
                    "Number of log entries must match number of embeddings"
                );
            }

            for (int i = 0; i < logEntries.Count; i++)
            {
                var logEntry = logEntries[i];
                var embedding = embeddings[i];

                // Store in the session index
                if (logEntry.SessionId != default)
                {
                    if (!_sessionIndices.TryGetValue(logEntry.SessionId, out var sessionIndex))
                    {
                        sessionIndex = new List<(LogEntry, float[])>();
                        _sessionIndices[logEntry.SessionId] = sessionIndex;
                    }

                    sessionIndex.Add((logEntry, embedding));
                }

                // Also store by log entry ID
                if (logEntry.LogEntryId > 0)
                {
                    _logEntryEmbeddings[logEntry.LogEntryId] = embedding;
                }
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task<int> StoreEmbeddingsAsync(
            IList<float[]> embeddings,
            CancellationToken cancellationToken = default
        )
        {
            // This is a simplified implementation - in a real system, this would store
            // embeddings in a database or vector store
            return Task.FromResult(embeddings.Count);
        }

        /// <inheritdoc/>
        public Task<List<VectorSearchResult>> FindSimilarLogEntriesAsync(
            Guid sessionId,
            float[] queryEmbedding,
            float similarityThreshold = 0.7f,
            int limit = 100,
            CancellationToken cancellationToken = default
        )
        {
            var results = new List<VectorSearchResult>();

            if (_sessionIndices.TryGetValue(sessionId, out var sessionIndex))
            {
                // Calculate cosine similarity for each entry in the session
                var similarities = sessionIndex
                    .Select(item =>
                        (
                            LogEntryId: item.LogEntry.LogEntryId,
                            SimilarityScore: CalculateCosineSimilarity(
                                queryEmbedding,
                                item.Embedding
                            )
                        )
                    )
                    .Where(item => item.SimilarityScore >= similarityThreshold)
                    .OrderByDescending(item => item.SimilarityScore)
                    .Take(limit)
                    .ToList();

                // Convert to search results
                results = similarities
                    .Select(item => new VectorSearchResult
                    {
                        LogEntryId = item.LogEntryId,
                        SimilarityScore = item.SimilarityScore,
                    })
                    .ToList();
            }

            return Task.FromResult(results);
        }

        /// <inheritdoc/>
        public Task<List<VectorSearchResult>> FindSimilarToLogEntryAsync(
            long logEntryId,
            float similarityThreshold = 0.7f,
            int limit = 100,
            CancellationToken cancellationToken = default
        )
        {
            if (_logEntryEmbeddings.TryGetValue(logEntryId, out var embedding))
            {
                // Find the session this log entry belongs to
                Guid? sessionId = null;
                foreach (var sessionIndex in _sessionIndices)
                {
                    if (sessionIndex.Value.Any(item => item.LogEntry.LogEntryId == logEntryId))
                    {
                        sessionId = sessionIndex.Key;
                        break;
                    }
                }

                if (sessionId.HasValue)
                {
                    // Use the existing method to find similar entries
                    return FindSimilarLogEntriesAsync(
                        sessionId.Value,
                        embedding,
                        similarityThreshold,
                        limit,
                        cancellationToken
                    );
                }
            }

            // Return empty list if log entry not found
            return Task.FromResult(new List<VectorSearchResult>());
        }

        /// <inheritdoc/>
        public Task<List<OutlierResult>> DetectOutliersAsync(
            Guid sessionId,
            float outlierThreshold = 0.3f,
            int limit = 100,
            CancellationToken cancellationToken = default
        )
        {
            var results = new List<OutlierResult>();

            if (_sessionIndices.TryGetValue(sessionId, out var sessionIndex))
            {
                // In a real implementation, this would use a more sophisticated algorithm
                // For this mock, we'll simulate by calculating average similarity to other entries

                var logEntries = sessionIndex.ToList();
                if (logEntries.Count <= 1)
                {
                    return Task.FromResult(results);
                }

                var outlierScores = new List<(long LogEntryId, float OutlierScore)>();

                // For each entry, calculate average similarity to all other entries
                foreach (var entry in logEntries)
                {
                    float avgSimilarity = 0;
                    int count = 0;

                    foreach (var otherEntry in logEntries)
                    {
                        // Skip comparing to self
                        if (entry.LogEntry.LogEntryId == otherEntry.LogEntry.LogEntryId)
                        {
                            continue;
                        }

                        float similarity = CalculateCosineSimilarity(
                            entry.Embedding,
                            otherEntry.Embedding
                        );
                        avgSimilarity += similarity;
                        count++;
                    }

                    if (count > 0)
                    {
                        avgSimilarity /= count;

                        // Outlier score is the inverse of average similarity
                        float outlierScore = 1 - avgSimilarity;

                        // Only consider as outlier if score is above threshold
                        if (outlierScore >= outlierThreshold)
                        {
                            outlierScores.Add((entry.LogEntry.LogEntryId, outlierScore));
                        }
                    }
                }

                // Sort by outlier score (highest first) and take the requested limit
                results = outlierScores
                    .OrderByDescending(item => item.OutlierScore)
                    .Take(limit)
                    .Select(item => new OutlierResult
                    {
                        LogEntryId = item.LogEntryId,
                        OutlierScore = item.OutlierScore,
                    })
                    .ToList();
            }

            return Task.FromResult(results);
        }

        /// <inheritdoc/>
        public Task ClearSessionIndexAsync(
            Guid sessionId,
            CancellationToken cancellationToken = default
        )
        {
            _sessionIndices.Remove(sessionId);

            // Also remove log entry embeddings for this session
            if (_sessionIndices.TryGetValue(sessionId, out var sessionIndex))
            {
                foreach (var entry in sessionIndex)
                {
                    _logEntryEmbeddings.Remove(entry.LogEntry.LogEntryId);
                }
            }

            return Task.CompletedTask;
        }

        #region Private Helper Methods

        private float CalculateCosineSimilarity(float[] a, float[] b)
        {
            if (a.Length != b.Length)
            {
                throw new ArgumentException("Vectors must have the same dimension");
            }

            float dotProduct = 0;
            float magnitudeA = 0;
            float magnitudeB = 0;

            for (int i = 0; i < a.Length; i++)
            {
                dotProduct += a[i] * b[i];
                magnitudeA += a[i] * a[i];
                magnitudeB += b[i] * b[i];
            }

            magnitudeA = (float)Math.Sqrt(magnitudeA);
            magnitudeB = (float)Math.Sqrt(magnitudeB);

            if (magnitudeA == 0 || magnitudeB == 0)
            {
                return 0;
            }

            return dotProduct / (magnitudeA * magnitudeB);
        }

        #endregion
    }
}
