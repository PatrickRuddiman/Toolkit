using AnalyzeLogs.Models;

namespace AnalyzeLogs.Services.Parsing;

/// <summary>
/// Interface for log parsers
/// </summary>
public interface ILogParser
{
    /// <summary>
    /// Determines if this parser can handle the given log line
    /// </summary>
    bool CanParse(string logLine);

    /// <summary>
    /// Parses a log line into a LogEntry
    /// </summary>
    LogEntry? Parse(string logLine, string sourceFile, int lineNumber);

    /// <summary>
    /// Priority of this parser (higher = checked first)
    /// </summary>
    int Priority { get; }
}

/// <summary>
/// Base class for log parsers
/// </summary>
public abstract class BaseLogParser : ILogParser
{
    public abstract bool CanParse(string logLine);
    public abstract LogEntry? Parse(string logLine, string sourceFile, int lineNumber);
    public abstract int Priority { get; }

    protected LogLevel ParseLogLevel(string? levelText)
    {
        if (string.IsNullOrEmpty(levelText))
            return LogLevel.Info;

        levelText = levelText.ToUpperInvariant().Trim();

        return levelText switch
        {
            "TRACE" or "TRC" => LogLevel.Trace,
            "DEBUG" or "DBG" => LogLevel.Debug,
            "INFO" or "INF" or "INFORMATION" => LogLevel.Info,
            "WARN" or "WRN" or "WARNING" => LogLevel.Warning,
            "ERROR" or "ERR" => LogLevel.Error,
            "FATAL" or "CRITICAL" or "CRIT" => LogLevel.Critical,
            _ => LogLevel.Info
        };
    }

    protected DateTime ParseTimestamp(string? timestampText)
    {
        if (string.IsNullOrEmpty(timestampText))
            return DateTime.UtcNow;

        // Try various timestamp formats
        var formats = new[]
        {
            "yyyy-MM-ddTHH:mm:ss.fffZ",
            "yyyy-MM-ddTHH:mm:ss.fff",
            "yyyy-MM-dd HH:mm:ss.fff",
            "yyyy-MM-dd HH:mm:ss",
            "MM/dd/yyyy HH:mm:ss",
            "dd/MM/yyyy HH:mm:ss",
            "yyyy-MM-ddTHH:mm:ssZ",
            "yyyy-MM-ddTHH:mm:ss"
        };

        foreach (var format in formats)
        {
            if (DateTime.TryParseExact(timestampText, format, null, System.Globalization.DateTimeStyles.AssumeUniversal, out var result))
            {
                return result.ToUniversalTime();
            }
        }

        // Try unix timestamp
        if (double.TryParse(timestampText, out var unixTime))
        {
            try
            {
                return DateTimeOffset.FromUnixTimeSeconds((long)unixTime).UtcDateTime;
            }
            catch
            {
                // Try milliseconds
                try
                {
                    return DateTimeOffset.FromUnixTimeMilliseconds((long)unixTime).UtcDateTime;
                }
                catch
                {
                    // Ignore
                }
            }
        }

        // Fallback to standard parsing
        if (DateTime.TryParse(timestampText, out var parsed))
        {
            return parsed.ToUniversalTime();
        }

        return DateTime.UtcNow;
    }

    protected string? ExtractValue(string input, string pattern)
    {
        var match = System.Text.RegularExpressions.Regex.Match(input, pattern);
        return match.Success ? match.Groups[1].Value : null;
    }

    protected int? ParseInt(string? value)
    {
        return int.TryParse(value, out var result) ? result : null;
    }

    protected double? ParseDouble(string? value)
    {
        return double.TryParse(value, out var result) ? result : null;
    }
}
