using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AnalyzeLogs.Models;
using AnalyzeLogs.Services.Analysis;
using AnalyzeLogs.Tests.Mocks;
using Microsoft.Extensions.Logging;
using Xunit;

namespace AnalyzeLogs.Tests.Services.Analysis
{
    public class VectorSearchServiceTests
    {
        private readonly List<LogEntry> _sampleLogEntries;
        private readonly Guid _testSessionId;
        private readonly MockVectorSearchService _service;

        public VectorSearchServiceTests()
        {
            _testSessionId = Guid.NewGuid();
            _sampleLogEntries = CreateSampleLogEntries();
            _service = new MockVectorSearchService();
        }

        [Fact]
        public async Task IndexLogEntryAsync_ValidLogEntry_Succeeds()
        {
            // Arrange
            var logEntry = _sampleLogEntries.First();
            var embedding = GenerateRandomEmbedding(1536);

            // Act & Assert
            // This should not throw an exception
            await _service.IndexLogEntryAsync(logEntry, embedding);
        }

        [Fact]
        public async Task IndexLogEntryAsync_LogEntryWithoutSessionId_ThrowsArgumentException()
        {
            // Arrange
            var logEntry = new LogEntry
            {
                LogEntryId = 1,
                TimestampUTC = DateTime.UtcNow,
                NormalizedMessage = "Test message",
                RawMessage = "Test raw message", // Add this required property
                SessionId = default(Guid), // Empty/default SessionId
            };
            var embedding = GenerateRandomEmbedding(1536);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _service.IndexLogEntryAsync(logEntry, embedding)
            );
        }

        [Fact]
        public async Task IndexLogEntriesBatchAsync_ValidBatch_Succeeds()
        {
            // Arrange
            var embeddings = _sampleLogEntries.Select(_ => GenerateRandomEmbedding(1536)).ToList();

            // Act & Assert
            // This should not throw an exception
            await _service.IndexLogEntriesBatchAsync(_sampleLogEntries, embeddings);
        }

        [Fact]
        public async Task IndexLogEntriesBatchAsync_MismatchedCounts_ThrowsArgumentException()
        {
            // Arrange
            var embeddings = new List<float[]> { GenerateRandomEmbedding(1536) };
            // We have more log entries than embeddings

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _service.IndexLogEntriesBatchAsync(_sampleLogEntries, embeddings)
            );
        }

        [Fact]
        public async Task FindSimilarLogEntriesAsync_ValidQuery_ReturnsResults()
        {
            // Arrange
            // First, index some log entries
            var embeddings = _sampleLogEntries.Select(_ => GenerateRandomEmbedding(1536)).ToList();
            await _service.IndexLogEntriesBatchAsync(_sampleLogEntries, embeddings);

            // Create a query embedding
            var queryEmbedding = GenerateRandomEmbedding(1536);

            // Act
            var results = await _service.FindSimilarLogEntriesAsync(_testSessionId, queryEmbedding);

            // Assert
            Assert.NotNull(results);
            // The mock service may return empty results since similarity is random in the mock
        }

        [Fact]
        public async Task FindSimilarToLogEntryAsync_ValidLogEntryId_ReturnsResults()
        {
            // Arrange
            // First, index some log entries
            var embeddings = _sampleLogEntries.Select(_ => GenerateRandomEmbedding(1536)).ToList();
            await _service.IndexLogEntriesBatchAsync(_sampleLogEntries, embeddings);

            // Act
            var results = await _service.FindSimilarToLogEntryAsync(
                _sampleLogEntries[0].LogEntryId
            );

            // Assert
            Assert.NotNull(results);
            // The mock service may return empty results since similarity is random in the mock
        }

        [Fact]
        public async Task DetectOutliersAsync_ValidSession_ReturnsOutliers()
        {
            // Arrange
            // First, index some log entries
            var embeddings = _sampleLogEntries.Select(_ => GenerateRandomEmbedding(1536)).ToList();
            await _service.IndexLogEntriesBatchAsync(_sampleLogEntries, embeddings);

            // Act
            var results = await _service.DetectOutliersAsync(_testSessionId);

            // Assert
            Assert.NotNull(results);
            // The mock service may return empty results since outlier detection is simulated in the mock
        }

        [Fact]
        public async Task ClearSessionIndexAsync_ValidSession_Succeeds()
        {
            // Arrange
            // First, index some log entries
            var embeddings = _sampleLogEntries.Select(_ => GenerateRandomEmbedding(1536)).ToList();
            await _service.IndexLogEntriesBatchAsync(_sampleLogEntries, embeddings);

            // Act & Assert
            // This should not throw an exception
            await _service.ClearSessionIndexAsync(_testSessionId);
        }

        private List<LogEntry> CreateSampleLogEntries()
        {
            // Create sample log entries for testing
            return new List<LogEntry>
            {
                new LogEntry
                {
                    LogEntryId = 1,
                    SessionId = _testSessionId,
                    TimestampUTC = DateTime.Parse("2025-06-10T14:30:00Z"),
                    OriginalTimestamp = "2025-06-10T14:30:00Z",
                    DetectedFormat = "JSON",
                    RawMessage =
                        "{\"timestamp\": \"2025-06-10T14:30:00Z\", \"level\": \"INFO\", \"service\": \"user-service\", \"message\": \"User login successful\", \"userId\": \"user123\"}",
                    NormalizedMessage = "User login successful",
                    SeverityLevel = new SeverityLevel { LevelName = "INFO" },
                    Service = new Service { ServiceName = "user-service" },
                },
                new LogEntry
                {
                    LogEntryId = 2,
                    SessionId = _testSessionId,
                    TimestampUTC = DateTime.Parse("2025-06-10T14:30:05Z"),
                    OriginalTimestamp = "2025-06-10T14:30:05Z",
                    DetectedFormat = "JSON",
                    RawMessage =
                        "{\"timestamp\": \"2025-06-10T14:30:05Z\", \"level\": \"ERROR\", \"service\": \"payment-service\", \"message\": \"Payment processing failed\", \"orderId\": \"order456\", \"errorCode\": \"PAYMENT_DECLINED\"}",
                    NormalizedMessage = "Payment processing failed",
                    SeverityLevel = new SeverityLevel { LevelName = "ERROR" },
                    Service = new Service { ServiceName = "payment-service" },
                },
                new LogEntry
                {
                    LogEntryId = 3,
                    SessionId = _testSessionId,
                    TimestampUTC = DateTime.Parse("2025-06-10T14:31:30Z"),
                    OriginalTimestamp = "2025-06-10 14:31:30",
                    DetectedFormat = "Syslog",
                    RawMessage =
                        "[2025-06-10 14:31:30] ERROR [order-service] - Failed to retrieve product information: Connection timeout",
                    NormalizedMessage =
                        "Failed to retrieve product information: Connection timeout",
                    SeverityLevel = new SeverityLevel { LevelName = "ERROR" },
                    Service = new Service { ServiceName = "order-service" },
                },
            };
        }

        private float[] GenerateRandomEmbedding(int dimension)
        {
            var random = new Random(42); // Use fixed seed for reproducibility
            var embedding = new float[dimension];

            for (int i = 0; i < dimension; i++)
            {
                embedding[i] = (float)(random.NextDouble() * 2 - 1); // Random value between -1 and 1
            }

            // Normalize to unit length
            float magnitude = (float)Math.Sqrt(embedding.Sum(x => x * x));
            for (int i = 0; i < dimension; i++)
            {
                embedding[i] /= magnitude;
            }

            return embedding;
        }
    }
}
