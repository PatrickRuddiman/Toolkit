using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AnalyzeLogs.Data;
using AnalyzeLogs.Models;
using AnalyzeLogs.Services.Analysis;
using AnalyzeLogs.Services.Ingestion;
using AnalyzeLogs.Tests.Helpers;
using AnalyzeLogs.Tests.Mocks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AnalyzeLogs.Tests.Services.Ingestion
{
    public class LogIngestionServiceTests
    {
        private readonly Mock<ILogParserFactory> _parserFactoryMock;
        private readonly Mock<IOpenAIService> _openAIServiceMock;
        private readonly Mock<IVectorSearchService> _vectorSearchServiceMock;
        private readonly Mock<ILogger<LogIngestionService>> _loggerMock;
        private readonly LogAnalyzerDbContext _dbContext;
        private readonly LogIngestionService _service;
        private readonly Guid _testSessionId;
        private readonly Guid _testProjectId;

        public LogIngestionServiceTests()
        {
            _parserFactoryMock = new Mock<ILogParserFactory>();
            _openAIServiceMock = new Mock<IOpenAIService>();
            _vectorSearchServiceMock = new Mock<IVectorSearchService>();
            _loggerMock = new Mock<ILogger<LogIngestionService>>();

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
                Status = "Initialized",
                RawInputGlobPattern = "*.log",
            };
            _dbContext.Sessions.Add(session);
            _dbContext.SaveChanges();

            // Set up the mock parser factory to return a parser that returns a valid LogEntry
            var mockParser = new Mock<ILogParser>();
            mockParser
                .Setup(m => m.Parse(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(
                    (string logLine, string sourcePath, string sourceFile) =>
                    {
                        if (string.IsNullOrWhiteSpace(logLine))
                            return null;

                        return new LogEntry
                        {
                            TimestampUTC = DateTime.UtcNow,
                            RawMessage = logLine,
                            NormalizedMessage = logLine,
                            SourceFilePath = sourcePath,
                            SourceFileName = sourceFile,
                            DetectedFormat = "Test",
                            SeverityLevel = _dbContext.SeverityLevels.First(s =>
                                s.LevelName == "INFO"
                            ),
                            Service = _dbContext.Services.First(s =>
                                s.ServiceName == "user-service"
                            ),
                        };
                    }
                );

            _parserFactoryMock
                .Setup(m => m.GetParser(It.IsAny<string>()))
                .Returns(mockParser.Object);

            // Set up the mock OpenAI service to return valid embeddings
            _openAIServiceMock
                .Setup(m =>
                    m.GenerateEmbeddingsAsync(
                        It.IsAny<IEnumerable<LogEntry>>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(new List<float[]> { new float[1536] });

            // Create the service
            _service = new LogIngestionService(
                _dbContext,
                _parserFactoryMock.Object,
                _openAIServiceMock.Object,
                _vectorSearchServiceMock.Object,
                _loggerMock.Object
            );
        }

        [Fact]
        public async Task IngestLogFilesAsync_WithValidGlob_UpdatesSessionAndReturnsLogEntries()
        {
            // Arrange
            // Create a temporary test file
            var tempDir = Path.Combine(
                Path.GetTempPath(),
                "LogAnalyzerTests",
                Guid.NewGuid().ToString()
            );
            Directory.CreateDirectory(tempDir);
            var tempFile = Path.Combine(tempDir, "test.log");
            File.WriteAllLines(tempFile, new[] { "Log line 1", "Log line 2", "Log line 3" });

            try
            {
                // Act
                var result = await _service.IngestLogFilesAsync(
                    _testSessionId,
                    tempDir + "/*.log",
                    true,
                    null,
                    CancellationToken.None
                );

                // Assert
                Assert.NotNull(result);
                Assert.Equal(1, result.FilesProcessed);
                Assert.Equal(3, result.EntriesIngested);

                // Verify session was updated
                var session = await _dbContext.Sessions.FindAsync(_testSessionId);
                Assert.NotNull(session);
                Assert.Equal("Completed", session.Status);
                Assert.Equal(1, session.LogFileCount);
                Assert.Equal(3, session.AnalyzedLogEntryCount);

                // Verify log entries were saved to the database
                var logEntries = await _dbContext
                    .LogEntries.Where(e => e.SessionId == _testSessionId)
                    .ToListAsync();

                Assert.Equal(3, logEntries.Count);
                Assert.All(logEntries, e => Assert.Equal(tempDir, e.SourceFilePath));
                Assert.All(logEntries, e => Assert.Equal("test.log", e.SourceFileName));
            }
            finally
            {
                // Clean up
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [Fact]
        public async Task IngestLogFilesAsync_WithNoMatchingFiles_CompletesWithEmptyResult()
        {
            // Arrange
            var nonExistentPath = Path.Combine(Path.GetTempPath(), "NonExistentDirectory");

            // Act
            var result = await _service.IngestLogFilesAsync(
                _testSessionId,
                nonExistentPath + "/*.log",
                true,
                null,
                CancellationToken.None
            );

            // Assert
            Assert.NotNull(result);
            Assert.Equal(0, result.FilesProcessed);
            Assert.Equal(0, result.EntriesIngested);

            // Verify session was updated
            var session = await _dbContext.Sessions.FindAsync(_testSessionId);
            Assert.NotNull(session);
            Assert.Equal("Completed", session.Status);
            Assert.Equal(0, session.LogFileCount);
            Assert.Equal(0, session.AnalyzedLogEntryCount);
        }

        [Fact]
        public async Task IngestLogFilesAsync_WithCancellation_CancelsOperationAndUpdatesSession()
        {
            // Arrange
            // Create a temporary test file with many lines to ensure processing takes long enough to cancel
            var tempDir = Path.Combine(
                Path.GetTempPath(),
                "LogAnalyzerTests",
                Guid.NewGuid().ToString()
            );
            Directory.CreateDirectory(tempDir);
            var tempFile = Path.Combine(tempDir, "test.log");

            // Create a large file with many lines
            using (var writer = new StreamWriter(tempFile))
            {
                for (int i = 0; i < 1000; i++)
                {
                    writer.WriteLine($"Log line {i}");
                }
            }

            try
            {
                // Create a cancellation token that will be cancelled immediately
                var cts = new CancellationTokenSource();
                cts.Cancel();

                // Act & Assert
                await Assert.ThrowsAsync<OperationCanceledException>(() =>
                    _service.IngestLogFilesAsync(
                        _testSessionId,
                        tempDir + "/*.log",
                        true,
                        null,
                        cts.Token
                    )
                );

                // Verify session was updated
                var session = await _dbContext.Sessions.FindAsync(_testSessionId);
                Assert.NotNull(session);
                Assert.Equal("Failed", session.Status);
            }
            finally
            {
                // Clean up
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [Fact]
        public async Task IngestLogFilesAsync_WithInvalidSessionId_ThrowsException()
        {
            // Arrange
            var invalidSessionId = Guid.NewGuid();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _service.IngestLogFilesAsync(
                    invalidSessionId,
                    "*.log",
                    true,
                    null,
                    CancellationToken.None
                )
            );
        }

        public void Dispose()
        {
            _dbContext.Dispose();
        }
    }
}
