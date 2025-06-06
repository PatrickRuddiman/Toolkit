using AnalyzeLogs.Models;
using AnalyzeLogs.Services.Parsing;
using Microsoft.Extensions.Logging;

namespace AnalyzeLogs.Services;

/// <summary>
/// Service for parsing log files
/// </summary>
public class LogParsingService
{
    private readonly ILogger<LogParsingService> _logger;
    private readonly List<ILogParser> _parsers;
    private readonly OpenAIService _openAiService;

    public LogParsingService(ILogger<LogParsingService> logger, OpenAIService openAiService, ILoggerFactory loggerFactory)
    {
        _logger = logger;
        _openAiService = openAiService;
        _parsers = new List<ILogParser>
        {
            new JsonLogParser(loggerFactory.CreateLogger<JsonLogParser>()),
            new StructuredTextLogParser(loggerFactory.CreateLogger<StructuredTextLogParser>()),
            new AccessLogParser(loggerFactory.CreateLogger<AccessLogParser>()),
            new AiLogParser(_openAiService, loggerFactory.CreateLogger<AiLogParser>()),
            new UnstructuredLogParser(loggerFactory.CreateLogger<UnstructuredLogParser>()),
        };

        // Sort by priority (higher first)
        _parsers.Sort((a, b) => b.Priority.CompareTo(a.Priority));
    }

    /// <summary>
    /// Parses a single log file
    /// </summary>
    public async Task<List<LogEntry>> ParseFileAsync(string filePath)
    {
        var entries = new List<LogEntry>();
        var lineNumber = 0;

        try
        {
            await foreach (var line in File.ReadLinesAsync(filePath))
            {
                lineNumber++;

                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var entry = ParseLine(line, filePath, lineNumber);
                if (entry != null)
                {
                    // Derive service name from filename if not set
                    if (string.IsNullOrEmpty(entry.Service))
                    {
                        entry.Service = DeriveServiceName(filePath);
                    }

                    entries.Add(entry);
                }
                else
                {
                    _logger.LogWarning(
                        "Failed to parse line {LineNumber} in file {FilePath}: {Line}",
                        lineNumber,
                        filePath,
                        line
                    );
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing file {FilePath}", filePath);
        }

        _logger.LogInformation(
            "Parsed {EntryCount} entries from {FilePath}",
            entries.Count,
            filePath
        );
        return entries;
    }

    /// <summary>
    /// Parses multiple log files
    /// </summary>
    public async Task<List<LogEntry>> ParseFilesAsync(IEnumerable<string> filePaths)
    {
        var allEntries = new List<LogEntry>();
        var tasks = filePaths.Select(ParseFileAsync);

        var results = await Task.WhenAll(tasks);

        foreach (var entries in results)
        {
            allEntries.AddRange(entries);
        }

        // Sort by timestamp
        allEntries.Sort((a, b) => a.Timestamp.CompareTo(b.Timestamp));

        _logger.LogInformation(
            "Parsed {TotalEntries} total entries from {FileCount} files",
            allEntries.Count,
            filePaths.Count()
        );

        return allEntries;
    }

    /// <summary>
    /// Parses a single log line
    /// </summary>
    private LogEntry? ParseLine(string line, string filePath, int lineNumber)
    {
        foreach (var parser in _parsers)
        {
            if (parser.CanParse(line))
            {
                try
                {
                    return parser.Parse(line, filePath, lineNumber);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Parser {ParserType} failed on line {LineNumber}",
                        parser.GetType().Name,
                        lineNumber
                    );
                    continue;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Derives service name from file path
    /// </summary>
    private string DeriveServiceName(string filePath)
    {
        var fileName = Path.GetFileNameWithoutExtension(filePath);

        // Remove common log file suffixes
        fileName = fileName
            .Replace("-service", "")
            .Replace("_service", "")
            .Replace("-log", "")
            .Replace("_log", "")
            .Replace(".log", "");

        // Convert to title case
        if (!string.IsNullOrEmpty(fileName))
        {
            return char.ToUpperInvariant(fileName[0]) + fileName[1..].ToLowerInvariant();
        }

        return "Unknown";
    }

    /// <summary>
    /// Normalizes log entries (additional processing after parsing)
    /// </summary>
    public void NormalizeEntries(List<LogEntry> entries)
    {
        foreach (var entry in entries)
        {
            // Extract correlation IDs from message if not already present
            if (string.IsNullOrEmpty(entry.CorrelationId))
            {
                entry.CorrelationId = ExtractCorrelationId(entry.Message);
            }

            // Extract trace IDs from message if not already present
            if (string.IsNullOrEmpty(entry.TraceId))
            {
                entry.TraceId = ExtractTraceId(entry.Message);
            }

            // Extract user IDs from message if not already present
            if (string.IsNullOrEmpty(entry.UserId))
            {
                entry.UserId = ExtractUserId(entry.Message);
            }

            // Parse HTTP status from message if not already present
            if (entry.HttpStatus == null)
            {
                entry.HttpStatus = ExtractHttpStatus(entry.Message);
            }
        }

        _logger.LogInformation("Normalized {EntryCount} log entries", entries.Count);
    }

    private string? ExtractCorrelationId(string message)
    {
        var patterns = new[]
        {
            @"correlationId[=:\s]+([a-fA-F0-9\-]+)",
            @"correlation[_-]?id[=:\s]+([a-fA-F0-9\-]+)",
            @"reqId[=:\s]+([a-fA-F0-9\-]+)",
            @"requestId[=:\s]+([a-fA-F0-9\-]+)",
        };

        return ExtractValueByPatterns(message, patterns);
    }

    private string? ExtractTraceId(string message)
    {
        var patterns = new[]
        {
            @"traceId[=:\s]+([a-fA-F0-9\-]+)",
            @"trace[_-]?id[=:\s]+([a-fA-F0-9\-]+)",
            @"spanId[=:\s]+([a-fA-F0-9\-]+)",
        };

        return ExtractValueByPatterns(message, patterns);
    }

    private string? ExtractUserId(string message)
    {
        var patterns = new[]
        {
            @"userId[=:\s]+([a-zA-Z0-9\-_@.]+)",
            @"user[_-]?id[=:\s]+([a-zA-Z0-9\-_@.]+)",
            @"user[=:\s]+([a-zA-Z0-9\-_@.]+)",
        };

        return ExtractValueByPatterns(message, patterns);
    }

    private int? ExtractHttpStatus(string message)
    {
        var pattern = @"\b([1-5]\d{2})\b";
        var match = System.Text.RegularExpressions.Regex.Match(message, pattern);

        if (match.Success && int.TryParse(match.Groups[1].Value, out var status))
        {
            return status;
        }

        return null;
    }

    private string? ExtractValueByPatterns(string text, string[] patterns)
    {
        foreach (var pattern in patterns)
        {
            var match = System.Text.RegularExpressions.Regex.Match(
                text,
                pattern,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );

            if (match.Success)
            {
                return match.Groups[1].Value;
            }
        }

        return null;
    }
}
