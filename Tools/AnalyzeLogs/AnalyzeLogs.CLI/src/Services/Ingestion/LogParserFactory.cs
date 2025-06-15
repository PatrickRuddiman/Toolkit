using Microsoft.Extensions.Logging;

namespace AnalyzeLogs.Services.Ingestion;

/// <summary>
/// Factory implementation that provides the AI log parser.
/// </summary>
public class LogParserFactory : ILogParserFactory
{
    private readonly ILogParser _aiLogParser;
    private readonly ILogger<LogParserFactory> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LogParserFactory"/> class.
    /// </summary>
    /// <param name="aiLogParser">The AI log parser.</param>
    /// <param name="logger">The logger to use.</param>
    public LogParserFactory(AILogParser aiLogParser, ILogger<LogParserFactory> logger)
    {
        _aiLogParser = aiLogParser;
        _logger = logger;
    }

    /// <inheritdoc/>
    public ILogParser GetParser(string line)
    {
        // Always return the AI log parser regardless of the log format
        _logger.LogDebug("Using AI parser for log line");
        return _aiLogParser;
    }
}
