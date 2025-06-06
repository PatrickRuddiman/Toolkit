using System.Diagnostics;
using System.Text;
using AnalyzeLogs.Models;
using Microsoft.Extensions.Logging;

namespace AnalyzeLogs.Services;

/// <summary>
/// Service for interacting with Fabric AI patterns
/// </summary>
public class FabricService
{
    private readonly ILogger<FabricService> _logger;

    public FabricService(ILogger<FabricService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Analyzes logs for anomalies using Fabric
    /// </summary>
    public async Task<List<Anomaly>> DetectAnomaliesAsync(LogChunk chunk, Configuration config)
    {
        var anomalies = new List<Anomaly>();

        try
        {
            var prompt = BuildAnomalyDetectionPrompt(chunk);
            var response = await CallFabricAsync("analyze_log_anomalies", prompt, config);

            if (!string.IsNullOrEmpty(response))
            {
                anomalies = ParseAnomalyResponse(response, chunk);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting anomalies for chunk {ChunkId}", chunk.Id);
        }

        return anomalies;
    }

    /// <summary>
    /// Performs coherence analysis on logs using Fabric
    /// </summary>
    public async Task<List<Anomaly>> AnalyzeCoherenceAsync(LogChunk chunk, Configuration config)
    {
        var coherenceIssues = new List<Anomaly>();

        try
        {
            var prompt = BuildCoherenceAnalysisPrompt(chunk);
            var response = await CallFabricAsync("analyze_log_coherence", prompt, config);

            if (!string.IsNullOrEmpty(response))
            {
                coherenceIssues = ParseCoherenceResponse(response, chunk);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing coherence for chunk {ChunkId}", chunk.Id);
        }

        return coherenceIssues;
    }

    /// <summary>
    /// Tags log entries using Fabric
    /// </summary>
    public async Task TagLogEntriesAsync(LogChunk chunk, Configuration config)
    {
        try
        {
            var prompt = BuildTaggingPrompt(chunk);
            var response = await CallFabricAsync("tag_log_entries", prompt, config);

            if (!string.IsNullOrEmpty(response))
            {
                ParseTaggingResponse(response, chunk);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error tagging entries for chunk {ChunkId}", chunk.Id);
        }
    }

    /// <summary>
    /// Generates summary and recommendations using Fabric
    /// </summary>
    public async Task<string> GenerateSummaryAsync(
        List<ServiceMetrics> metrics,
        List<Anomaly> anomalies,
        Configuration config
    )
    {
        try
        {
            var prompt = BuildSummaryPrompt(metrics, anomalies);
            var response = await CallFabricAsync("summarize_log_analysis", prompt, config);
            return response ?? "Failed to generate summary.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating summary");
            return "Error generating summary: " + ex.Message;
        }
    }

    /// <summary>
    /// Calls Fabric with the specified pattern and prompt
    /// </summary>
    private async Task<string?> CallFabricAsync(string pattern, string prompt, Configuration config)
    {
        try
        {
            var fabricArgs = new StringBuilder();
            fabricArgs.Append($"--pattern {pattern} ");
            fabricArgs.Append($"--model {config.Model} ");
            fabricArgs.Append($"--temperature {config.Temperature} ");

            var processInfo = new ProcessStartInfo
            {
                FileName = "fabric",
                Arguments = fabricArgs.ToString().Trim(),
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = new Process { StartInfo = processInfo };
            process.Start();

            // Write prompt to stdin
            await process.StandardInput.WriteAsync(prompt);
            await process.StandardInput.FlushAsync();
            process.StandardInput.Close();

            // Read response
            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                _logger.LogWarning(
                    "Fabric command failed with exit code {ExitCode}: {Error}",
                    process.ExitCode,
                    error
                );
                return null;
            }

            return output;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Fabric with pattern {Pattern}", pattern);
            return null;
        }
    }

    /// <summary>
    /// Builds prompt for anomaly detection
    /// </summary>
    private string BuildAnomalyDetectionPrompt(LogChunk chunk)
    {
        var prompt = new StringBuilder();

        prompt.AppendLine("# Log Anomaly Detection");
        prompt.AppendLine();
        prompt.AppendLine(
            "Analyze the following log entries for anomalies, errors, and unusual patterns."
        );
        prompt.AppendLine("For each anomaly found, provide:");
        prompt.AppendLine("- Type (Error, Pattern, Performance, Security, etc.)");
        prompt.AppendLine("- Severity (Low, Medium, High, Critical)");
        prompt.AppendLine("- Description of the issue");
        prompt.AppendLine("- Confidence level (0-100%)");
        prompt.AppendLine("- Recommendations if applicable");
        prompt.AppendLine();
        prompt.AppendLine("## Log Entries:");
        prompt.AppendLine();

        foreach (var entry in chunk.Entries.Take(50)) // Limit for token management
        {
            prompt.AppendLine(
                $"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss}] [{entry.Level}] [{entry.Service}] {entry.Message}"
            );
        }

        return prompt.ToString();
    }

    /// <summary>
    /// Builds prompt for coherence analysis
    /// </summary>
    private string BuildCoherenceAnalysisPrompt(LogChunk chunk)
    {
        var prompt = new StringBuilder();

        prompt.AppendLine("# Log Coherence Analysis");
        prompt.AppendLine();
        prompt.AppendLine(
            "Analyze the following sequence of log entries for logical consistency and completeness."
        );
        prompt.AppendLine("Look for:");
        prompt.AppendLine(
            "- Missing expected events (requests without responses, incomplete transactions)"
        );
        prompt.AppendLine("- Events out of order");
        prompt.AppendLine("- Gaps in the flow that might indicate problems");
        prompt.AppendLine();
        prompt.AppendLine("## Log Sequence:");
        prompt.AppendLine();

        var sortedEntries = chunk.Entries.OrderBy(e => e.Timestamp).ToList();
        foreach (var entry in sortedEntries.Take(100)) // More entries for coherence analysis
        {
            var correlationInfo = !string.IsNullOrEmpty(entry.CorrelationId)
                ? $"[{entry.CorrelationId}]"
                : "";
            prompt.AppendLine(
                $"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss}] [{entry.Level}] [{entry.Service}] {correlationInfo} {entry.Message}"
            );
        }

        return prompt.ToString();
    }

    /// <summary>
    /// Builds prompt for tagging
    /// </summary>
    private string BuildTaggingPrompt(LogChunk chunk)
    {
        var prompt = new StringBuilder();

        prompt.AppendLine("# Log Entry Tagging");
        prompt.AppendLine();
        prompt.AppendLine("Assign relevant tags to each log entry based on content and context.");
        prompt.AppendLine(
            "Use tags like: database, authentication, network, performance, error, warning, api, etc."
        );
        prompt.AppendLine("Format response as: LineNumber: tag1, tag2, tag3");
        prompt.AppendLine();
        prompt.AppendLine("## Log Entries:");
        prompt.AppendLine();

        for (int i = 0; i < Math.Min(chunk.Entries.Count, 50); i++)
        {
            var entry = chunk.Entries[i];
            prompt.AppendLine($"{i + 1}: [{entry.Level}] [{entry.Service}] {entry.Message}");
        }

        return prompt.ToString();
    }

    /// <summary>
    /// Builds prompt for summary generation
    /// </summary>
    private string BuildSummaryPrompt(List<ServiceMetrics> metrics, List<Anomaly> anomalies)
    {
        var prompt = new StringBuilder();

        prompt.AppendLine("# Log Analysis Summary");
        prompt.AppendLine();
        prompt.AppendLine("Generate a comprehensive summary of the log analysis results.");
        prompt.AppendLine(
            "Include key insights, system health assessment, and actionable recommendations."
        );
        prompt.AppendLine();
        prompt.AppendLine("## Service Metrics:");
        prompt.AppendLine();

        foreach (var metric in metrics)
        {
            prompt.AppendLine($"- {metric.ServiceName}:");
            prompt.AppendLine($"  - Total entries: {metric.TotalEntries:N0}");
            prompt.AppendLine($"  - Error rate: {metric.ErrorRate:P2}");
            prompt.AppendLine($"  - Average response time: {metric.AverageResponseTime:N0}ms");
            prompt.AppendLine();
        }

        prompt.AppendLine("## Key Anomalies:");
        prompt.AppendLine();

        foreach (var anomaly in anomalies.Take(10))
        {
            prompt.AppendLine($"- [{anomaly.Severity}] {anomaly.Type}: {anomaly.Description}");
        }

        return prompt.ToString();
    }

    /// <summary>
    /// Parses anomaly detection response
    /// </summary>
    private List<Anomaly> ParseAnomalyResponse(string response, LogChunk chunk)
    {
        var anomalies = new List<Anomaly>();

        // Simple parsing - in a real implementation, you'd want more robust parsing
        var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            if (line.Contains("Anomaly:") || line.Contains("Error:") || line.Contains("Issue:"))
            {
                var anomaly = new Anomaly
                {
                    Type = AnomalyType.Error,
                    Severity = AnomalySeverity.Medium,
                    Description = line.Trim(),
                    Timestamp = chunk.StartTime,
                    Confidence = 0.8,
                    RelatedEntries = chunk.Entries.Take(5).ToList(),
                };

                anomalies.Add(anomaly);
            }
        }

        return anomalies;
    }

    /// <summary>
    /// Parses coherence analysis response
    /// </summary>
    private List<Anomaly> ParseCoherenceResponse(string response, LogChunk chunk)
    {
        var coherenceIssues = new List<Anomaly>();

        var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            if (line.Contains("missing") || line.Contains("incomplete") || line.Contains("gap"))
            {
                var anomaly = new Anomaly
                {
                    Type = AnomalyType.Sequence,
                    Severity = AnomalySeverity.Medium,
                    Description = line.Trim(),
                    Timestamp = chunk.StartTime,
                    Confidence = 0.7,
                };

                coherenceIssues.Add(anomaly);
            }
        }

        return coherenceIssues;
    }

    /// <summary>
    /// Parses tagging response and applies tags to entries
    /// </summary>
    private void ParseTaggingResponse(string response, LogChunk chunk)
    {
        var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            var parts = line.Split(':', 2);
            if (parts.Length == 2 && int.TryParse(parts[0].Trim(), out var lineNumber))
            {
                var tags = parts[1]
                    .Split(',')
                    .Select(t => t.Trim())
                    .Where(t => !string.IsNullOrEmpty(t))
                    .ToList();

                if (lineNumber > 0 && lineNumber <= chunk.Entries.Count)
                {
                    chunk.Entries[lineNumber - 1].Tags.AddRange(tags);
                }
            }
        }
    }
}
