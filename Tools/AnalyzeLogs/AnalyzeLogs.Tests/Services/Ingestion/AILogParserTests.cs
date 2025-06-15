using System;
using System.Text.Json;
using AnalyzeLogs.Models;
using AnalyzeLogs.Services.Ingestion;
using AnalyzeLogs.Tests.Mocks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AnalyzeLogs.Tests.Services.Ingestion
{
    public class AILogParserTests
    {
        private readonly Mock<ILogger<AILogParser>> _loggerMock;
        private readonly MockOpenAIService _mockOpenAIService;
        private readonly AILogParser _parser;

        public AILogParserTests()
        {
            _loggerMock = new Mock<ILogger<AILogParser>>();
            _mockOpenAIService = new MockOpenAIService();
            _parser = new AILogParser(_mockOpenAIService, _loggerMock.Object);
        }

        [Fact]
        public void Parse_JsonLogLine_ReturnsCorrectlyParsedLogEntry()
        {
            // Arrange
            string logLine =
                "{\"timestamp\": \"2025-06-10T14:30:00Z\", \"level\": \"INFO\", \"service\": \"user-service\", \"message\": \"User login successful\", \"userId\": \"user123\"}";
            string sourcePath = "/logs/services";
            string sourceFile = "user-service.log";

            // Act
            var result = _parser.Parse(logLine, sourcePath, sourceFile);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("2025-06-10T14:30:00Z", result.TimestampUTC.ToString("o"));
            Assert.Equal("INFO", result.SeverityLevel?.LevelName);
            Assert.Equal("user-service", result.Service?.ServiceName);
            Assert.Equal("User login successful", result.NormalizedMessage);
            Assert.Equal(sourcePath, result.SourceFilePath);
            Assert.Equal(sourceFile, result.SourceFileName);
            Assert.Equal(logLine, result.RawMessage);
        }

        [Fact]
        public void Parse_TextLogLine_ReturnsCorrectlyParsedLogEntry()
        {
            // Arrange
            string logLine =
                "[2025-06-10 14:31:00] INFO [auth-service] - Authentication token issued for user user123";
            string sourcePath = "/logs/services";
            string sourceFile = "auth-service.log";

            // Act
            var result = _parser.Parse(logLine, sourcePath, sourceFile);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("2025-06-10T14:31:00Z", result.TimestampUTC.ToString("o"));
            Assert.Equal("INFO", result.SeverityLevel?.LevelName);
            Assert.Equal("auth-service", result.Service?.ServiceName);
            Assert.Equal("Authentication token issued for user user123", result.NormalizedMessage);
            Assert.Equal(sourcePath, result.SourceFilePath);
            Assert.Equal(sourceFile, result.SourceFileName);
            Assert.Equal(logLine, result.RawMessage);
        }

        [Fact]
        public void Parse_UnstructuredLogLine_ReturnsBasicLogEntry()
        {
            // Arrange
            string logLine =
                "2025-06-10 14:35:20 INFO - Service health check completed successfully. All systems operational.";
            string sourcePath = "/logs/services";
            string sourceFile = "health.log";

            // Act
            var result = _parser.Parse(logLine, sourcePath, sourceFile);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("2025-06-10T14:35:20Z", result.TimestampUTC.ToString("o"));
            Assert.Equal("INFO", result.SeverityLevel?.LevelName);
            Assert.Null(result.Service?.ServiceName); // No service name in this log
            Assert.Equal(
                "Service health check completed successfully. All systems operational.",
                result.NormalizedMessage
            );
            Assert.Equal(sourcePath, result.SourceFilePath);
            Assert.Equal(sourceFile, result.SourceFileName);
            Assert.Equal(logLine, result.RawMessage);
        }

        [Fact]
        public void Parse_MalformedLogLine_ReturnsBasicLogEntry()
        {
            // Arrange
            string logLine = "This is not a properly formatted log entry";
            string sourcePath = "/logs/services";
            string sourceFile = "malformed.log";

            // Act
            var result = _parser.Parse(logLine, sourcePath, sourceFile);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(logLine, result.NormalizedMessage);
            Assert.Equal(sourcePath, result.SourceFilePath);
            Assert.Equal(sourceFile, result.SourceFileName);
            Assert.Equal(logLine, result.RawMessage);
            Assert.Equal("Unstructured", result.DetectedFormat);
        }

        [Fact]
        public void Parse_EmptyLogLine_ReturnsNull()
        {
            // Arrange
            string logLine = "";
            string sourcePath = "/logs/services";
            string sourceFile = "empty.log";

            // Act
            var result = _parser.Parse(logLine, sourcePath, sourceFile);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void Parse_WhitespaceLogLine_ReturnsNull()
        {
            // Arrange
            string logLine = "   ";
            string sourcePath = "/logs/services";
            string sourceFile = "whitespace.log";

            // Act
            var result = _parser.Parse(logLine, sourcePath, sourceFile);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void Parse_SameLogLineMultipleTimes_UsesCacheForSubsequentCalls()
        {
            // Arrange
            string logLine =
                "{\"timestamp\": \"2025-06-10T14:30:00Z\", \"level\": \"INFO\", \"service\": \"user-service\", \"message\": \"User login successful\", \"userId\": \"user123\"}";
            string sourcePath = "/logs/services";
            string sourceFile = "user-service.log";

            // Act
            var result1 = _parser.Parse(logLine, sourcePath, sourceFile);
            var result2 = _parser.Parse(logLine, sourcePath, sourceFile);

            // Assert
            Assert.NotNull(result1);
            Assert.NotNull(result2);
            Assert.Equal(result1.TimestampUTC, result2.TimestampUTC);
            Assert.Equal(result1.NormalizedMessage, result2.NormalizedMessage);
            Assert.Equal(result1.SeverityLevel?.LevelName, result2.SeverityLevel?.LevelName);

            // Note: We're testing for equality of objects, not reference equality
            // because the cache should create a clone to avoid modifying the cached entry
            Assert.NotSame(result1, result2);
        }

        [Fact]
        public void Parse_OpenAIServiceThrowsException_FallsBackToBasicLogEntry()
        {
            // Arrange
            var mockFailingOpenAIService = new Mock<AnalyzeLogs.Services.Analysis.IOpenAIService>();
            mockFailingOpenAIService
                .Setup(m =>
                    m.ParseLogAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())
                )
                .ThrowsAsync(new Exception("Test exception"));

            var parserWithFailingService = new AILogParser(
                mockFailingOpenAIService.Object,
                _loggerMock.Object
            );

            string logLine = "This log will cause an exception";
            string sourcePath = "/logs/services";
            string sourceFile = "error.log";

            // Act
            var result = parserWithFailingService.Parse(logLine, sourcePath, sourceFile);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(logLine, result.NormalizedMessage);
            Assert.Equal(sourcePath, result.SourceFilePath);
            Assert.Equal(sourceFile, result.SourceFileName);
            Assert.Equal(logLine, result.RawMessage);
            Assert.Equal("Unstructured", result.DetectedFormat);

            // Verify that the logger was called with an error
            _loggerMock.Verify(
                x =>
                    x.Log(
                        LogLevel.Error,
                        It.IsAny<EventId>(),
                        It.Is<It.IsAnyType>(
                            (v, t) => v.ToString().Contains("Error parsing log line with AI parser")
                        ),
                        It.IsAny<Exception>(),
                        It.IsAny<Func<It.IsAnyType, Exception, string>>()
                    ),
                Times.Once
            );
        }
    }
}
