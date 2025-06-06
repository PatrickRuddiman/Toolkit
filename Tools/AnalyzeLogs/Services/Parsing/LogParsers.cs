using System.Text.Json;
using System.Text.RegularExpressions;
using AnalyzeLogs.Models;
using AnalyzeLogs.Services.Parsing;

namespace AnalyzeLogs.Services.Parsing;

/// <summary>
/// Parser for JSON log entries
/// </summary>
public partial class JsonLogParser : BaseLogParser
{
    public override int Priority => 100;

    public override bool CanParse(string logLine)
    {
        if (string.IsNullOrWhiteSpace(logLine))
            return false;

        var trimmed = logLine.Trim();
        return trimmed.StartsWith('{') && trimmed.EndsWith('}');
    }

    public override LogEntry? Parse(string logLine, string sourceFile, int lineNumber)
    {
        try
        {
            using var document = JsonDocument.Parse(logLine);
            var root = document.RootElement;

            var entry = new LogEntry
            {
                SourceFile = sourceFile,
                LineNumber = lineNumber,
                RawContent = logLine,
            };

            // Parse common fields
            if (root.TryGetProperty("timestamp", out var timestampProp))
            {
                entry.Timestamp = ParseTimestamp(timestampProp.GetString());
            }
            else if (root.TryGetProperty("@timestamp", out var atTimestampProp))
            {
                entry.Timestamp = ParseTimestamp(atTimestampProp.GetString());
            }
            else if (root.TryGetProperty("time", out var timeProp))
            {
                entry.Timestamp = ParseTimestamp(timeProp.GetString());
            }

            // Parse log level
            if (root.TryGetProperty("level", out var levelProp))
            {
                entry.Level = ParseLogLevel(levelProp.GetString());
            }
            else if (root.TryGetProperty("severity", out var severityProp))
            {
                entry.Level = ParseLogLevel(severityProp.GetString());
            }

            // Parse message
            if (root.TryGetProperty("message", out var messageProp))
            {
                entry.Message = messageProp.GetString() ?? string.Empty;
            }
            else if (root.TryGetProperty("msg", out var msgProp))
            {
                entry.Message = msgProp.GetString() ?? string.Empty;
            }

            // Parse service/source
            if (root.TryGetProperty("service", out var serviceProp))
            {
                entry.Service = serviceProp.GetString();
            }
            else if (root.TryGetProperty("source", out var sourceProp))
            {
                entry.Service = sourceProp.GetString();
            }
            else if (root.TryGetProperty("logger", out var loggerProp))
            {
                entry.Service = loggerProp.GetString();
            }

            // Parse correlation/trace IDs
            if (root.TryGetProperty("correlationId", out var correlationProp))
            {
                entry.CorrelationId = correlationProp.GetString();
            }
            else if (root.TryGetProperty("correlation_id", out var correlation2Prop))
            {
                entry.CorrelationId = correlation2Prop.GetString();
            }

            if (root.TryGetProperty("traceId", out var traceProp))
            {
                entry.TraceId = traceProp.GetString();
            }
            else if (root.TryGetProperty("trace_id", out var trace2Prop))
            {
                entry.TraceId = trace2Prop.GetString();
            }

            // Parse user ID
            if (root.TryGetProperty("userId", out var userProp))
            {
                entry.UserId = userProp.GetString();
            }
            else if (root.TryGetProperty("user_id", out var user2Prop))
            {
                entry.UserId = user2Prop.GetString();
            }

            // Parse HTTP status
            if (root.TryGetProperty("status", out var statusProp))
            {
                entry.HttpStatus = ParseInt(statusProp.GetString()) ?? statusProp.GetInt32();
            }
            else if (root.TryGetProperty("status_code", out var status2Prop))
            {
                entry.HttpStatus = ParseInt(status2Prop.GetString()) ?? status2Prop.GetInt32();
            }

            // Parse response time
            if (root.TryGetProperty("responseTime", out var responseProp))
            {
                entry.ResponseTimeMs =
                    ParseDouble(responseProp.GetString()) ?? responseProp.GetDouble();
            }
            else if (root.TryGetProperty("response_time", out var response2Prop))
            {
                entry.ResponseTimeMs =
                    ParseDouble(response2Prop.GetString()) ?? response2Prop.GetDouble();
            }
            else if (root.TryGetProperty("duration", out var durationProp))
            {
                entry.ResponseTimeMs =
                    ParseDouble(durationProp.GetString()) ?? durationProp.GetDouble();
            }

            // Store additional data
            foreach (var property in root.EnumerateObject())
            {
                var name = property.Name;
                if (!IsKnownProperty(name))
                {
                    entry.AdditionalData[name] = property.Value.ValueKind switch
                    {
                        JsonValueKind.String => property.Value.GetString(),
                        JsonValueKind.Number => property.Value.GetDouble(),
                        JsonValueKind.True => true,
                        JsonValueKind.False => false,
                        JsonValueKind.Null => null,
                        _ => property.Value.GetRawText(),
                    };
                }
            }

            return entry;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool IsKnownProperty(string name)
    {
        return name.ToLowerInvariant() switch
        {
            "timestamp"
            or "@timestamp"
            or "time"
            or "level"
            or "severity"
            or "message"
            or "msg"
            or "service"
            or "source"
            or "logger"
            or "correlationid"
            or "correlation_id"
            or "traceid"
            or "trace_id"
            or "userid"
            or "user_id"
            or "status"
            or "status_code"
            or "responsetime"
            or "response_time"
            or "duration" => true,
            _ => false,
        };
    }
}

/// <summary>
/// Parser for structured text logs (e.g., "[LEVEL] timestamp - message")
/// </summary>
public partial class StructuredTextLogParser : BaseLogParser
{
    [GeneratedRegex(
        @"^\[(\w+)\]\s*(\d{4}-\d{2}-\d{2}[T\s]\d{2}:\d{2}:\d{2}[.\d]*(?:Z|[+-]\d{2}:\d{2})?)\s*[-–]\s*(.+)$"
    )]
    private static partial Regex StructuredLogRegex();

    [GeneratedRegex(
        @"^(\d{4}-\d{2}-\d{2}[T\s]\d{2}:\d{2}:\d{2}[.\d]*(?:Z|[+-]\d{2}:\d{2})?)\s+(\w+)\s+(.+)$"
    )]
    private static partial Regex TimestampLevelMessageRegex();

    public override int Priority => 80;

    public override bool CanParse(string logLine)
    {
        if (string.IsNullOrWhiteSpace(logLine))
            return false;

        return StructuredLogRegex().IsMatch(logLine)
            || TimestampLevelMessageRegex().IsMatch(logLine);
    }

    public override LogEntry? Parse(string logLine, string sourceFile, int lineNumber)
    {
        var entry = new LogEntry
        {
            SourceFile = sourceFile,
            LineNumber = lineNumber,
            RawContent = logLine,
        };

        // Try structured format: [LEVEL] timestamp - message
        var match = StructuredLogRegex().Match(logLine);
        if (match.Success)
        {
            entry.Level = ParseLogLevel(match.Groups[1].Value);
            entry.Timestamp = ParseTimestamp(match.Groups[2].Value);
            entry.Message = match.Groups[3].Value;
            return entry;
        }

        // Try timestamp level message format
        match = TimestampLevelMessageRegex().Match(logLine);
        if (match.Success)
        {
            entry.Timestamp = ParseTimestamp(match.Groups[1].Value);
            entry.Level = ParseLogLevel(match.Groups[2].Value);
            entry.Message = match.Groups[3].Value;
            return entry;
        }

        return null;
    }
}

/// <summary>
/// Parser for Apache/Nginx access logs
/// </summary>
public partial class AccessLogParser : BaseLogParser
{
    [GeneratedRegex(
        @"^(\S+)\s+\S+\s+\S+\s+\[([^\]]+)\]\s+""(\S+)\s+(\S+)\s+\S+""\s+(\d+)\s+(\d+)\s*""([^""]*)""\s*""([^""]*)"""
    )]
    private static partial Regex ApacheLogRegex();

    public override int Priority => 70;

    public override bool CanParse(string logLine)
    {
        if (string.IsNullOrWhiteSpace(logLine))
            return false;

        return ApacheLogRegex().IsMatch(logLine);
    }

    public override LogEntry? Parse(string logLine, string sourceFile, int lineNumber)
    {
        var match = ApacheLogRegex().Match(logLine);
        if (!match.Success)
            return null;

        var entry = new LogEntry
        {
            SourceFile = sourceFile,
            LineNumber = lineNumber,
            RawContent = logLine,
            Service = "web-server",
        };

        // Parse timestamp (Apache format: [25/Dec/2019:10:36:01 +0000])
        var timestampStr = match.Groups[2].Value;
        entry.Timestamp = ParseApacheTimestamp(timestampStr);

        // Parse HTTP method and path
        var method = match.Groups[3].Value;
        var path = match.Groups[4].Value;
        entry.Message = $"{method} {path}";

        // Parse status code
        entry.HttpStatus = ParseInt(match.Groups[5].Value);

        // Parse response size
        var responseSize = match.Groups[6].Value;
        if (ParseInt(responseSize) is int size)
        {
            entry.AdditionalData["response_size"] = size;
        }

        // Parse referer and user agent
        var referer = match.Groups[7].Value;
        var userAgent = match.Groups[8].Value;

        if (!string.IsNullOrEmpty(referer) && referer != "-")
        {
            entry.AdditionalData["referer"] = referer;
        }

        if (!string.IsNullOrEmpty(userAgent))
        {
            entry.AdditionalData["user_agent"] = userAgent;
        }

        // Parse client IP
        var clientIp = match.Groups[1].Value;
        entry.AdditionalData["client_ip"] = clientIp;

        // Set log level based on status code
        if (entry.HttpStatus >= 500)
        {
            entry.Level = LogLevel.Error;
        }
        else if (entry.HttpStatus >= 400)
        {
            entry.Level = LogLevel.Warning;
        }
        else
        {
            entry.Level = LogLevel.Info;
        }

        return entry;
    }

    private DateTime ParseApacheTimestamp(string timestamp)
    {
        // Format: 25/Dec/2019:10:36:01 +0000
        if (
            DateTime.TryParseExact(
                timestamp,
                "dd/MMM/yyyy:HH:mm:ss zzz",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal,
                out var result
            )
        )
        {
            return result.ToUniversalTime();
        }

        return ParseTimestamp(timestamp);
    }
}

/// <summary>
/// Fallback parser for unstructured text logs
/// </summary>
public partial class UnstructuredLogParser : BaseLogParser
{
    [GeneratedRegex(@"\b(\d{4}-\d{2}-\d{2}[T\s]\d{2}:\d{2}:\d{2}[.\d]*(?:Z|[+-]\d{2}:\d{2})?)\b")]
    private static partial Regex TimestampRegex();

    [GeneratedRegex(@"\b(ERROR|WARN|INFO|DEBUG|TRACE|FATAL|CRITICAL)\b", RegexOptions.IgnoreCase)]
    private static partial Regex LogLevelRegex();

    public override int Priority => 10;

    public override bool CanParse(string logLine)
    {
        return !string.IsNullOrWhiteSpace(logLine);
    }

    public override LogEntry? Parse(string logLine, string sourceFile, int lineNumber)
    {
        var entry = new LogEntry
        {
            SourceFile = sourceFile,
            LineNumber = lineNumber,
            RawContent = logLine,
            Message = logLine,
        };

        // Try to extract timestamp
        var timestampMatch = TimestampRegex().Match(logLine);
        if (timestampMatch.Success)
        {
            entry.Timestamp = ParseTimestamp(timestampMatch.Value);
        }
        else
        {
            entry.Timestamp = DateTime.UtcNow;
        }

        // Try to extract log level
        var levelMatch = LogLevelRegex().Match(logLine);
        if (levelMatch.Success)
        {
            entry.Level = ParseLogLevel(levelMatch.Value);
        }
        else
        {
            // Guess level based on keywords
            var lowerMessage = logLine.ToLowerInvariant();
            if (
                lowerMessage.Contains("error")
                || lowerMessage.Contains("exception")
                || lowerMessage.Contains("fail")
            )
            {
                entry.Level = LogLevel.Error;
            }
            else if (lowerMessage.Contains("warn"))
            {
                entry.Level = LogLevel.Warning;
            }
            else
            {
                entry.Level = LogLevel.Info;
            }
        }

        return entry;
    }
}
