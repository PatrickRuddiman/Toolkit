using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AnalyzeLogs.Data;
using AnalyzeLogs.Models;
using AnalyzeLogs.Services.Analysis;
using AnalyzeLogs.Services.Query;
using AnalyzeLogs.Services.Reporting;
using AnalyzeLogs.Tests.Helpers;
using AnalyzeLogs.Tests.Mocks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AnalyzeLogs.Tests.Services.Query
{
    public class QueryServiceTests
    {
        private readonly Mock<IOpenAIService> _openAIServiceMock;
        private readonly Mock<IVectorSearchService> _vectorSearchServiceMock;
        private readonly Mock<IReportService> _reportServiceMock;
        private readonly Mock<ILogger<QueryService>> _loggerMock;
        private readonly LogAnalyzerDbContext _dbContext;
        private readonly QueryService _service;
        private readonly Guid _testSessionId;
        private readonly Guid _testProjectId;

        public QueryServiceTests()
        {
            _openAIServiceMock = new Mock<IOpenAIService>();
            _vectorSearchServiceMock = new Mock<IVectorSearchService>();
            _reportServiceMock = new Mock<IReportService>();
            _loggerMock = new Mock<ILogger<QueryService>>();

            // Create an in-memory database for testing
            _dbContext = InMemoryDbContextFactory.CreateDbContext(Guid.NewGuid().ToString());

            // Seed the database with test data
            InMemoryDbContextFactory.SeedTestData(_dbContext);

            // Get a test project and create a test session
            var project = _dbContext.Projects.First();
            _testProjectId = project.ProjectId;

            _testSessionId = Guid.NewGuid();
            var session = new Session
            {
                SessionId = _testSessionId,
                ProjectId = _testProjectId,
                StartTime = DateTime.UtcNow,
                Status = "Completed",
                LogFileCount = 3,
                AnalyzedLogEntryCount = 10,
                RawInputGlobPattern = "*.log",
            };
            _dbContext.Sessions.Add(session);

            // Add sample log entries
            var severityLevels = _dbContext.SeverityLevels.ToList();
            var services = _dbContext.Services.ToList();

            var logEntries = CreateSampleLogEntries(_testSessionId, severityLevels, services);
            _dbContext.LogEntries.AddRange(logEntries);
            _dbContext.SaveChanges();

            // Setup the OpenAI service mock to return sample query interpretations
            _openAIServiceMock
                .Setup(m =>
                    m.InterpretQueryAsync(
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<string>()
                    )
                )
                .ReturnsAsync(
                    (string query, string projectContext, string sessionContext) =>
                    {
                        if (query.Contains("error") && query.Contains("yesterday"))
                        {
                            return GetSampleTimeRangeQueryResponse();
                        }
                        else if (query.Contains("payment") && query.Contains("between"))
                        {
                            return GetSampleServiceQueryResponse();
                        }
                        else if (query.Contains("similar") && query.Contains("database"))
                        {
                            return GetSampleSemanticQueryResponse();
                        }
                        else
                        {
                            return GetSampleTimeRangeQueryResponse(); // Default
                        }
                    }
                );

            // Setup the vector search service mock
            _vectorSearchServiceMock
                .Setup(m =>
                    m.FindSimilarLogEntriesAsync(
                        It.IsAny<Guid>(),
                        It.IsAny<float[]>(),
                        It.IsAny<float>(),
                        It.IsAny<int>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(
                    new List<VectorSearchResult>
                    {
                        new VectorSearchResult { LogEntryId = 1, SimilarityScore = 0.9f },
                        new VectorSearchResult { LogEntryId = 2, SimilarityScore = 0.8f },
                    }
                );

            // Setup the OpenAI service mock for embeddings
            _openAIServiceMock
                .Setup(m => m.GenerateEmbeddingsAsync(It.IsAny<IEnumerable<string>>()))
                .ReturnsAsync(new List<float[]> { new float[1536] });

            // Create the service
            _service = new QueryService(
                _dbContext,
                _openAIServiceMock.Object,
                _vectorSearchServiceMock.Object,
                _reportServiceMock.Object,
                _loggerMock.Object
            );
        }

        [Fact]
        public async Task ExecuteQueryAsync_TimeRangeQuery_ReturnsExpectedResults()
        {
            // Arrange
            string query = "Show me all errors from yesterday";

            // Act
            var result = await _service.ExecuteQueryAsync(_testSessionId, query);

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.LogEntries);
            Assert.Equal("filter", result.Metadata.QueryType);
            Assert.Contains("Time range filter", result.TextResponse);

            // Verify the OpenAI service was called correctly
            _openAIServiceMock.Verify(
                m => m.InterpretQueryAsync(query, It.IsAny<string>(), It.IsAny<string>()),
                Times.Once
            );
        }

        [Fact]
        public async Task ExecuteQueryAsync_ServiceQuery_ReturnsExpectedResults()
        {
            // Arrange
            string query = "Show me logs from payment service between 2PM and 3PM";

            // Act
            var result = await _service.ExecuteQueryAsync(_testSessionId, query);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("filter", result.Metadata.QueryType);
            Assert.Contains("service", result.TextResponse.ToLower());

            // Verify the OpenAI service was called correctly
            _openAIServiceMock.Verify(
                m => m.InterpretQueryAsync(query, It.IsAny<string>(), It.IsAny<string>()),
                Times.Once
            );
        }

        [Fact]
        public async Task ExecuteQueryAsync_SemanticQuery_ReturnsExpectedResults()
        {
            // Arrange
            string query = "Find logs similar to 'database connection timeout'";

            // Act
            var result = await _service.ExecuteQueryAsync(_testSessionId, query);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("vector", result.Metadata.QueryType);
            Assert.Contains("similar", result.TextResponse.ToLower());

            // Verify the OpenAI service and vector search service were called correctly
            _openAIServiceMock.Verify(
                m => m.InterpretQueryAsync(query, It.IsAny<string>(), It.IsAny<string>()),
                Times.Once
            );

            _openAIServiceMock.Verify(
                m => m.GenerateEmbeddingsAsync(It.IsAny<IEnumerable<string>>()),
                Times.Once
            );

            _vectorSearchServiceMock.Verify(
                m =>
                    m.FindSimilarLogEntriesAsync(
                        _testSessionId,
                        It.IsAny<float[]>(),
                        It.IsAny<float>(),
                        It.IsAny<int>(),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once
            );
        }

        [Fact]
        public async Task ExecuteQueryAsync_InvalidSessionId_ReturnsErrorMessage()
        {
            // Arrange
            var invalidSessionId = Guid.NewGuid();
            string query = "Show me all errors";

            // Act
            var result = await _service.ExecuteQueryAsync(invalidSessionId, query);

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.ErrorMessage);
            Assert.Contains("not found", result.ErrorMessage);
        }

        [Fact]
        public async Task ExecuteQueryAsync_EmptyQuery_ThrowsArgumentException()
        {
            // Arrange
            string query = "";

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _service.ExecuteQueryAsync(_testSessionId, query)
            );
        }

        [Fact]
        public async Task ExecuteQueryAsync_NullQuery_ThrowsArgumentNullException()
        {
            // Arrange
            string query = null;

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _service.ExecuteQueryAsync(_testSessionId, query)
            );
        }

        private List<LogEntry> CreateSampleLogEntries(
            Guid sessionId,
            List<SeverityLevel> severityLevels,
            List<Service> services
        )
        {
            return new List<LogEntry>
            {
                new LogEntry
                {
                    LogEntryId = 1,
                    SessionId = sessionId,
                    TimestampUTC = DateTime.UtcNow.AddHours(-1),
                    OriginalTimestamp = DateTime.UtcNow.AddHours(-1).ToString("o"),
                    DetectedFormat = "JSON",
                    RawMessage =
                        "{\"timestamp\": \"2025-06-10T14:30:00Z\", \"level\": \"INFO\", \"service\": \"user-service\", \"message\": \"User login successful\", \"userId\": \"user123\"}",
                    NormalizedMessage = "User login successful",
                    SeverityLevel = severityLevels.First(s => s.LevelName == "INFO"),
                    Service = services.First(s => s.ServiceName == "user-service"),
                    SourceFilePath = "/logs/services",
                    SourceFileName = "user-service.log",
                },
                new LogEntry
                {
                    LogEntryId = 2,
                    SessionId = sessionId,
                    TimestampUTC = DateTime.UtcNow.AddHours(-2),
                    OriginalTimestamp = DateTime.UtcNow.AddHours(-2).ToString("o"),
                    DetectedFormat = "JSON",
                    RawMessage =
                        "{\"timestamp\": \"2025-06-10T14:30:05Z\", \"level\": \"ERROR\", \"service\": \"payment-service\", \"message\": \"Payment processing failed\", \"orderId\": \"order456\", \"errorCode\": \"PAYMENT_DECLINED\"}",
                    NormalizedMessage = "Payment processing failed",
                    SeverityLevel = severityLevels.First(s => s.LevelName == "ERROR"),
                    Service = services.First(s => s.ServiceName == "payment-service"),
                    SourceFilePath = "/logs/services",
                    SourceFileName = "payment-service.log",
                    AdditionalDataJson =
                        "{\"orderId\": \"order456\", \"errorCode\": \"PAYMENT_DECLINED\"}",
                },
                new LogEntry
                {
                    LogEntryId = 3,
                    SessionId = sessionId,
                    TimestampUTC = DateTime.UtcNow.AddHours(-3),
                    OriginalTimestamp = DateTime.UtcNow.AddHours(-3).ToString("o"),
                    DetectedFormat = "Syslog",
                    RawMessage =
                        "[2025-06-10 14:31:30] ERROR [order-service] - Failed to retrieve product information: Connection timeout",
                    NormalizedMessage =
                        "Failed to retrieve product information: Connection timeout",
                    SeverityLevel = severityLevels.First(s => s.LevelName == "ERROR"),
                    Service = services.First(s => s.ServiceName == "order-service"),
                    SourceFilePath = "/logs/services",
                    SourceFileName = "order-service.log",
                },
            };
        }

        private string GetSampleTimeRangeQueryResponse()
        {
            return JsonSerializer.Serialize(
                new
                {
                    queryType = "filter",
                    parameters = new
                    {
                        timeRange = new
                        {
                            start = DateTime.UtcNow.AddDays(-1).ToString("o"),
                            end = DateTime.UtcNow.ToString("o"),
                        },
                        severityLevel = "ERROR",
                    },
                    description = "Time range filter for ERROR logs from yesterday",
                    sqlQuery = "SELECT * FROM LogEntry WHERE TimestampUTC >= @startTime AND TimestampUTC <= @endTime AND SeverityLevelId IN (SELECT SeverityLevelId FROM SeverityLevel WHERE LevelName = 'ERROR')",
                }
            );
        }

        private string GetSampleServiceQueryResponse()
        {
            return JsonSerializer.Serialize(
                new
                {
                    queryType = "filter",
                    parameters = new
                    {
                        serviceName = "payment-service",
                        timeRange = new
                        {
                            start = DateTime.UtcNow.AddHours(-2).ToString("o"),
                            end = DateTime.UtcNow.AddHours(-1).ToString("o"),
                        },
                    },
                    description = "Filter for payment-service logs within specified time range",
                    sqlQuery = "SELECT * FROM LogEntry WHERE ServiceId IN (SELECT ServiceId FROM Service WHERE ServiceName = 'payment-service') AND TimestampUTC >= @startTime AND TimestampUTC <= @endTime",
                }
            );
        }

        private string GetSampleSemanticQueryResponse()
        {
            return JsonSerializer.Serialize(
                new
                {
                    queryType = "vector_search",
                    parameters = new
                    {
                        searchText = "database connection timeout",
                        similarityThreshold = 0.7,
                        limit = 10,
                    },
                    description = "Find logs semantically similar to 'database connection timeout'",
                    vectorOperation = "Find logEntries similar to embedding(database connection timeout) with cosine similarity > 0.7",
                    vectorSearch = new
                    {
                        required = true,
                        searchText = "database connection timeout",
                        similarityThreshold = 0.7,
                    },
                }
            );
        }

        public void Dispose()
        {
            _dbContext.Dispose();
        }
    }
}
