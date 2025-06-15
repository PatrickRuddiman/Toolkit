using System.Text.Json;
using AnalyzeLogs.Models;
using AnalyzeLogs.Services.Analysis;
using Microsoft.Extensions.Logging;

namespace AnalyzeLogs.Services.Ingestion;

/// <summary>
/// AI-powered log parser that uses OpenAI to parse log entries of any format.
/// </summary>
public class AILogParser : ILogParser
{
    private readonly IOpenAIService _openAIService;
    private readonly ILogger<AILogParser> _logger;
    private readonly Dictionary<string, LogEntry> _parseCache = new();
    private readonly int _maxCacheSize = 10000; // Limit cache size to avoid memory issues

    /// <summary>
    /// Initializes a new instance of the <see cref="AILogParser"/> class.
    /// </summary>
    /// <param name="openAIService">The OpenAI service to use for parsing.</param>
    /// <param name="logger">The logger to use.</param>
    public AILogParser(IOpenAIService openAIService, ILogger<AILogParser> logger)
    {
        _openAIService = openAIService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public LogEntry? Parse(string line, string sourcePath, string sourceFile)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        try
        {
            // Check cache first to avoid redundant AI calls for similar log entries
            if (_parseCache.TryGetValue(line, out var cachedEntry))
            {
                // Create a clone of the cached entry to avoid modifying the cached version
                return CloneLogEntry(cachedEntry, sourcePath, sourceFile);
            }

            // Prepare the user message for the AI
            string userMessage =
                $"Parse the following log line:\n\nLog line: {line}\nSource path: {sourcePath}\nSource file: {sourceFile}";

            // Send to OpenAI for parsing
            var response = _openAIService
                .ParseLogAsync(line, sourcePath, sourceFile)
                .GetAwaiter()
                .GetResult();

            if (string.IsNullOrWhiteSpace(response))
            {
                _logger.LogWarning("AI parser returned empty response for log line: {Line}", line);
                return CreateBasicLogEntry(line, sourcePath, sourceFile);
            }

            try
            {
                // Deserialize the AI response to a LogEntry
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

                var logEntry = JsonSerializer.Deserialize<LogEntry>(response, options);

                if (logEntry == null)
                {
                    _logger.LogWarning("Failed to deserialize AI parser response to LogEntry");
                    return CreateBasicLogEntry(line, sourcePath, sourceFile);
                }

                // Fill in the source information and raw message
                logEntry.SourceFilePath = sourcePath;
                logEntry.SourceFileName = sourceFile;
                logEntry.RawMessage = line;

                // Add to cache if not too large
                if (_parseCache.Count < _maxCacheSize)
                {
                    _parseCache[line] = CloneLogEntry(logEntry, sourcePath, sourceFile);
                }
                else if (_parseCache.Count == _maxCacheSize)
                {
                    _logger.LogInformation(
                        "AI parser cache reached maximum size of {MaxSize}",
                        _maxCacheSize
                    );
                    // Clear half the cache when it gets full
                    var keysToRemove = _parseCache.Keys.Take(_maxCacheSize / 2).ToList();
                    foreach (var key in keysToRemove)
                    {
                        _parseCache.Remove(key);
                    }
                    _parseCache[line] = CloneLogEntry(logEntry, sourcePath, sourceFile);
                }

                return logEntry;
            }
            catch (JsonException ex)
            {
                _logger.LogError(
                    ex,
                    "Error deserializing AI parser response: {Response}",
                    response
                );
                return CreateBasicLogEntry(line, sourcePath, sourceFile);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing log line with AI parser: {Line}", line);
            return CreateBasicLogEntry(line, sourcePath, sourceFile);
        }
    }

    private LogEntry CreateBasicLogEntry(string line, string sourcePath, string sourceFile)
    {
        // Create a basic log entry with minimal information when AI parsing fails
        return new LogEntry
        {
            RawMessage = line,
            NormalizedMessage = line,
            SourceFilePath = sourcePath,
            SourceFileName = sourceFile,
            TimestampUTC = DateTime.UtcNow,
            DetectedFormat = "Unstructured",
        };
    }

    private LogEntry CloneLogEntry(LogEntry original, string sourcePath, string sourceFile)
    {
        // Create a copy of a log entry with updated source information
        var clone = new LogEntry
        {
            TimestampUTC = original.TimestampUTC,
            OriginalTimestamp = original.OriginalTimestamp,
            OriginalTimeZone = original.OriginalTimeZone,
            DetectedFormat = original.DetectedFormat,
            SourceFilePath = sourcePath,
            SourceFileName = sourceFile,
            RawMessage = original.RawMessage,
            NormalizedMessage = original.NormalizedMessage,
            CorrelationId = original.CorrelationId,
            ThreadId = original.ThreadId,
            ProcessId = original.ProcessId,
            AdditionalDataJson = original.AdditionalDataJson,
        };

        // Copy the SeverityLevel if present
        if (original.SeverityLevel != null)
        {
            clone.SeverityLevel = new SeverityLevel
            {
                LevelName = original.SeverityLevel.LevelName,
            };
        }

        // Copy the Service if present
        if (original.Service != null)
        {
            clone.Service = new Service { ServiceName = original.Service.ServiceName };
        }

        return clone;
    }
}
