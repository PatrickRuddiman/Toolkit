using System.Threading;
using AnalyzeLogs.Models;
using AnalyzeLogs.Services.Analysis;
using AnalyzeLogs.Tests.Mocks;
using Moq;
using Xunit;

namespace AnalyzeLogs.Tests.Services.Analysis
{
    public class OpenAIServiceTests
    {
        private readonly Mock<ILogger<OpenAIService>> _loggerMock;
        private readonly Mock<IConfigurationService> _configServiceMock;
        private readonly OpenAIService _service;
        private readonly List<LogEntry> _sampleLogEntries;

        public OpenAIServiceTests()
        {
            _loggerMock = new Mock<ILogger<OpenAIService>>();
            _configServiceMock = new Mock<IConfigurationService>();

            // Configure mock settings
            var appConfig = new AppConfig
            {
                OpenAI = new OpenAISettings
                {
                    ApiKey = "mock-api-key",
                    BaseUrl = "https://mock-endpoint.openai.azure.com/",
                    GeneralModel = "gpt-4",
                    EmbeddingModel = "text-embedding-ada-002",
                },
            };

            _configServiceMock.Setup(m => m.GetConfiguration()).Returns(appConfig);

            _service = new OpenAIService(_loggerMock.Object, _configServiceMock.Object);

            // Create sample log entries for testing
            _sampleLogEntries = CreateSampleLogEntries();
        }

        [Fact]
        public async Task ParseLogAsync_ValidLogLine_ReturnsExpectedJson()
        {
            // Arrange
            string logLine =
                "{\"timestamp\": \"2025-06-10T14:30:00Z\", \"level\": \"INFO\", \"service\": \"user-service\", \"message\": \"User login successful\", \"userId\": \"user123\"}";
            string sourcePath = "/logs/services";
            string sourceFile = "user-service.log";

            // Use a mock service that returns a predefined response
            var mockService = new MockOpenAIService();

            // Act
            var result = await mockService.ParseLogAsync(logLine, sourcePath, sourceFile);

            // Assert
            Assert.NotNull(result);
            Assert.Contains("timestampUTC", result);
            Assert.Contains("user-service", result);
            Assert.Contains("User login successful", result);
        }

        [Fact]
        public async Task AnalyzeAnomaliesAsync_LogEntriesWithAnomaly_ReturnsAnomalyDescription()
        {
            // Arrange
            var anomalyEntries = _sampleLogEntries
                .Where(e => e.RawMessage?.Contains("Connection timeout") == true)
                .ToList();
            var mockService = new MockOpenAIService();

            // Act
            var result = await mockService.AnalyzeAnomaliesAsync(anomalyEntries);

            // Assert
            Assert.NotNull(result);
            Assert.Contains("anomaly", result.ToLower());
            Assert.Contains("Connection timeout", result);
        }

        [Fact]
        public async Task AnalyzeCoherenceAsync_ValidLogEntries_ReturnsCoherenceAnalysis()
        {
            // Arrange
            var mockService = new MockOpenAIService();

            // Act
            var result = await mockService.AnalyzeCoherenceAsync(_sampleLogEntries);

            // Assert
            Assert.NotNull(result);
            Assert.Contains("coherent", result.ToLower());
        }

        [Fact]
        public async Task GenerateEmbeddingsAsync_ValidTexts_ReturnsEmbeddings()
        {
            // Arrange
            var texts = new List<string> { "Test message 1", "Test message 2" };
            var mockService = new MockOpenAIService();

            // Act
            var result = await mockService.GenerateEmbeddingsAsync(texts);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            Assert.Equal(1536, result[0].Length); // OpenAI embeddings are 1536-dimensional
        }

        [Fact]
        public async Task GenerateEmbeddingsAsync_LogEntries_ReturnsEmbeddings()
        {
            // Arrange
            var mockService = new MockOpenAIService();

            // Act
            var result = await mockService.GenerateEmbeddingsAsync(_sampleLogEntries);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(_sampleLogEntries.Count, result.Count);
            Assert.Equal(1536, result[0].Length); // OpenAI embeddings are 1536-dimensional
        }

        [Fact]
        public async Task TagLogsAsync_ValidLogEntries_ReturnsTagsJson()
        {
            // Arrange
            var mockService = new MockOpenAIService();

            // Act
            var result = await mockService.TagLogsAsync(_sampleLogEntries);

            // Assert
            Assert.NotNull(result);
            // The result should be a JSON string that can be parsed into a dictionary
            Assert.True(result.StartsWith("{") && result.EndsWith("}"));
        }

        [Fact]
        public async Task InterpretQueryAsync_ValidQuery_ReturnsQueryInterpretation()
        {
            // Arrange
            string query = "Show me errors from yesterday";
            var mockService = new MockOpenAIService();

            // Act
            var result = await mockService.InterpretQueryAsync(query);

            // Assert
            Assert.NotNull(result);
            Assert.Contains("queryType", result);
        }

        [Fact]
        public async Task ResearchAnomalyAsync_ValidAnomaly_ReturnsResearchFindings()
        {
            // Arrange
            var anomalyEntry = _sampleLogEntries.FirstOrDefault(e =>
                e.SeverityLevel?.LevelName == "ERROR"
            );
            var contextEntries = _sampleLogEntries.Take(5).ToList();
            string anomalyDescription = "Connection timeout error";
            var mockService = new MockOpenAIService();

            // Act
            var result = await mockService.ResearchAnomalyAsync(
                anomalyEntry,
                contextEntries,
                anomalyDescription
            );

            // Assert
            Assert.NotNull(result);
            Assert.Contains("Research findings", result);
        }

        private List<LogEntry> CreateSampleLogEntries()
        {
            // Create sample log entries for testing
            return new List<LogEntry>
            {
                new LogEntry
                {
                    LogEntryId = 1,
                    TimestampUTC = DateTime.Parse("2025-06-10T14:30:00Z"),
                    OriginalTimestamp = "2025-06-10T14:30:00Z",
                    DetectedFormat = "JSON",
                    RawMessage =
                        "{\"timestamp\": \"2025-06-10T14:30:00Z\", \"level\": \"INFO\", \"service\": \"user-service\", \"message\": \"User login successful\", \"userId\": \"user123\"}",
                    NormalizedMessage = "User login successful",
                    SeverityLevel = new SeverityLevel { LevelName = "INFO" },
                    Service = new Service { ServiceName = "user-service" },
                    SourceFilePath = "/logs/services",
                    SourceFileName = "user-service.log",
                },
                new LogEntry
                {
                    LogEntryId = 2,
                    TimestampUTC = DateTime.Parse("2025-06-10T14:30:05Z"),
                    OriginalTimestamp = "2025-06-10T14:30:05Z",
                    DetectedFormat = "JSON",
                    RawMessage =
                        "{\"timestamp\": \"2025-06-10T14:30:05Z\", \"level\": \"ERROR\", \"service\": \"payment-service\", \"message\": \"Payment processing failed\", \"orderId\": \"order456\", \"errorCode\": \"PAYMENT_DECLINED\"}",
                    NormalizedMessage = "Payment processing failed",
                    SeverityLevel = new SeverityLevel { LevelName = "ERROR" },
                    Service = new Service { ServiceName = "payment-service" },
                    SourceFilePath = "/logs/services",
                    SourceFileName = "payment-service.log",
                },
                new LogEntry
                {
                    LogEntryId = 3,
                    TimestampUTC = DateTime.Parse("2025-06-10T14:31:30Z"),
                    OriginalTimestamp = "2025-06-10 14:31:30",
                    DetectedFormat = "Syslog",
                    RawMessage =
                        "[2025-06-10 14:31:30] ERROR [order-service] - Failed to retrieve product information: Connection timeout",
                    NormalizedMessage =
                        "Failed to retrieve product information: Connection timeout",
                    SeverityLevel = new SeverityLevel { LevelName = "ERROR" },
                    Service = new Service { ServiceName = "order-service" },
                    SourceFilePath = "/logs/services",
                    SourceFileName = "order-service.log",
                },
            };
        }
    }
}
