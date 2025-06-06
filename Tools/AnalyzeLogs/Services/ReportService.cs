using System.Text;
using AnalyzeLogs.Models;
using Microsoft.Extensions.Logging;

namespace AnalyzeLogs.Services;

/// <summary>
/// Service for generating analysis reports in text format
/// </summary>
public class ReportService
{
    private readonly ILogger<ReportService> _logger;

    public ReportService(ILogger<ReportService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Generates a comprehensive analysis report
    /// </summary>
    public async Task<string> GenerateReportAsync(
        AnalysisResult analysisResult,
        List<string> logFiles
    )
    {
        _logger.LogInformation(
            "Generating analysis report for {FileCount} log files",
            logFiles.Count
        );

        var report = new StringBuilder();

        // Report Header
        report.AppendLine("=".PadRight(80, '='));
        report.AppendLine("LOG ANALYSIS REPORT");
        report.AppendLine("=".PadRight(80, '='));
        report.AppendLine();

        // Summary Section
        GenerateSummarySection(report, analysisResult, logFiles);

        // File Information Section
        GenerateFileInformationSection(report, logFiles);

        // Time Range Section
        GenerateTimeRangeSection(report, analysisResult);

        // Service Metrics Section
        GenerateServiceMetricsSection(report, analysisResult);

        // Anomalies Section
        GenerateAnomaliesSection(report, analysisResult);

        // Correlations Section
        GenerateCorrelationsSection(report, analysisResult);

        // Embedding Outliers Section
        if (analysisResult.EmbeddingOutliers.Any())
        {
            GenerateEmbeddingOutliersSection(report, analysisResult);
        }

        // Errors Section
        if (analysisResult.Errors.Any())
        {
            GenerateErrorsSection(report, analysisResult);
        }

        // Footer
        GenerateFooter(report);

        _logger.LogInformation("Report generation completed");
        return report.ToString();
    }

    private void GenerateSummarySection(
        StringBuilder report,
        AnalysisResult analysisResult,
        List<string> logFiles
    )
    {
        report.AppendLine("SUMMARY");
        report.AppendLine("-".PadRight(40, '-'));
        report.AppendLine();

        var duration = analysisResult.EndTime - analysisResult.StartTime;
        var healthStatus = DetermineHealthStatus(analysisResult);

        report.AppendLine($"Analysis Date: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        report.AppendLine($"Log Files Analyzed: {logFiles.Count}");
        report.AppendLine($"Total Log Entries: {analysisResult.TotalEntries:N0}");
        report.AppendLine($"Time Range: {duration.TotalHours:F1} hours");
        report.AppendLine($"Services Monitored: {analysisResult.ServiceMetrics.Count}");
        report.AppendLine($"Anomalies Found: {analysisResult.Anomalies.Count}");
        report.AppendLine($"Correlations Detected: {analysisResult.Correlations.Count}");
        report.AppendLine($"Overall Health: {healthStatus}");
        report.AppendLine();
    }

    private void GenerateFileInformationSection(StringBuilder report, List<string> logFiles)
    {
        report.AppendLine("FILE INFORMATION");
        report.AppendLine("-".PadRight(40, '-'));
        report.AppendLine();

        for (int i = 0; i < logFiles.Count && i < 10; i++)
        {
            var fileName = Path.GetFileName(logFiles[i]);
            var fileSize = GetFileSizeDescription(logFiles[i]);
            report.AppendLine($"• {fileName} ({fileSize})");
        }

        if (logFiles.Count > 10)
        {
            report.AppendLine($"... and {logFiles.Count - 10} more files");
        }

        report.AppendLine();
    }

    private void GenerateTimeRangeSection(StringBuilder report, AnalysisResult analysisResult)
    {
        report.AppendLine("TIME RANGE");
        report.AppendLine("-".PadRight(40, '-'));
        report.AppendLine();

        report.AppendLine($"Start Time: {analysisResult.StartTime:yyyy-MM-dd HH:mm:ss} UTC");
        report.AppendLine($"End Time: {analysisResult.EndTime:yyyy-MM-dd HH:mm:ss} UTC");
        report.AppendLine(
            $"Duration: {(analysisResult.EndTime - analysisResult.StartTime).TotalHours:F1} hours"
        );
        report.AppendLine();
    }

    private void GenerateServiceMetricsSection(StringBuilder report, AnalysisResult analysisResult)
    {
        report.AppendLine("SERVICE METRICS");
        report.AppendLine("-".PadRight(40, '-'));
        report.AppendLine();

        if (!analysisResult.ServiceMetrics.Any())
        {
            report.AppendLine("No service metrics available.");
            report.AppendLine();
            return;
        }

        foreach (var metrics in analysisResult.ServiceMetrics.OrderBy(m => m.ServiceName))
        {
            report.AppendLine($"Service: {metrics.ServiceName}");
            report.AppendLine($"  Total Entries: {metrics.TotalRequests:N0}");
            report.AppendLine($"  Error Count: {metrics.ErrorCount:N0}");
            report.AppendLine($"  Error Rate: {metrics.ErrorRate:P2}");

            if (metrics.AverageResponseTime.HasValue)
            {
                report.AppendLine($"  Avg Response Time: {metrics.AverageResponseTime.Value:F0}ms");
            }

            if (metrics.P95ResponseTime.HasValue)
            {
                report.AppendLine($"  95th Percentile: {metrics.P95ResponseTime.Value:F0}ms");
            }

            report.AppendLine($"  Request Rate: {metrics.RequestRate:F1} requests/min");

            if (metrics.UniqueUsers > 0)
            {
                report.AppendLine($"  Unique Users: {metrics.UniqueUsers:N0}");
            }

            // HTTP Status Distribution
            if (metrics.HttpStatusDistribution.Any())
            {
                report.AppendLine("  HTTP Status Distribution:");
                foreach (var status in metrics.HttpStatusDistribution.OrderBy(kvp => kvp.Key))
                {
                    var percentage = (double)status.Value / metrics.TotalRequests * 100;
                    report.AppendLine($"    {status.Key}: {status.Value:N0} ({percentage:F1}%)");
                }
            }

            // Tag Distribution
            if (metrics.TagDistribution.Any())
            {
                report.AppendLine("  Top Log Categories:");
                var topTags = metrics.TagDistribution.OrderByDescending(kvp => kvp.Value).Take(5);
                foreach (var tag in topTags)
                {
                    report.AppendLine($"    {tag.Key}: {tag.Value:N0}");
                }
            }

            report.AppendLine();
        }
    }

    private void GenerateAnomaliesSection(StringBuilder report, AnalysisResult analysisResult)
    {
        report.AppendLine("ANOMALIES DETECTED");
        report.AppendLine("-".PadRight(40, '-'));
        report.AppendLine();

        if (!analysisResult.Anomalies.Any())
        {
            report.AppendLine("No anomalies detected.");
            report.AppendLine();
            return;
        }

        // Group anomalies by severity
        var anomaliesBySeverity = analysisResult
            .Anomalies.GroupBy(a => a.Severity)
            .OrderByDescending(g => g.Key);

        foreach (var severityGroup in anomaliesBySeverity)
        {
            report.AppendLine(
                $"{severityGroup.Key.ToString().ToUpper()} SEVERITY ({severityGroup.Count()} anomalies):"
            );
            report.AppendLine();

            var anomalies = severityGroup.OrderBy(a => a.Timestamp).Take(10);
            foreach (var anomaly in anomalies)
            {
                report.AppendLine(
                    $"• [{anomaly.Timestamp:HH:mm:ss}] {anomaly.Service} - {anomaly.Type}"
                );
                report.AppendLine($"  {anomaly.Description}");

                if (!string.IsNullOrEmpty(anomaly.Recommendation))
                {
                    report.AppendLine($"  Recommendation: {anomaly.Recommendation}");
                }

                if (anomaly.RelatedLogIds.Any())
                {
                    report.AppendLine(
                        $"  Related logs: {string.Join(", ", anomaly.RelatedLogIds.Take(3))}"
                    );
                }

                report.AppendLine();
            }

            if (severityGroup.Count() > 10)
            {
                report.AppendLine(
                    $"  ... and {severityGroup.Count() - 10} more {severityGroup.Key.ToString().ToLower()} severity anomalies"
                );
                report.AppendLine();
            }
        }
    }

    private void GenerateCorrelationsSection(StringBuilder report, AnalysisResult analysisResult)
    {
        report.AppendLine("CROSS-SERVICE CORRELATIONS");
        report.AppendLine("-".PadRight(40, '-'));
        report.AppendLine();

        if (!analysisResult.Correlations.Any())
        {
            report.AppendLine("No cross-service correlations detected.");
            report.AppendLine();
            return;
        }

        var significantCorrelations = analysisResult
            .Correlations.Where(c => c.Services.Count > 1)
            .OrderByDescending(c => c.Services.Count)
            .Take(20);

        foreach (var correlation in significantCorrelations)
        {
            var duration = correlation.EndTime - correlation.StartTime;
            report.AppendLine($"Correlation ID: {correlation.Id}");
            report.AppendLine($"  Services: {string.Join(", ", correlation.Services)}");
            report.AppendLine($"  Duration: {duration.TotalMilliseconds:F0}ms");
            report.AppendLine($"  Start: {correlation.StartTime:HH:mm:ss.fff}");
            report.AppendLine($"  End: {correlation.EndTime:HH:mm:ss.fff}");
            report.AppendLine($"  Log Entries: {correlation.Entries.Count}");

            // Show first few log entries
            var firstLogs = correlation.Entries.OrderBy(e => e.Timestamp).Take(3);
            foreach (var log in firstLogs)
            {
                report.AppendLine(
                    $"    [{log.Timestamp:HH:mm:ss}] {log.Service}: {TruncateMessage(log.Message, 80)}"
                );
            }

            if (correlation.Entries.Count > 3)
            {
                report.AppendLine($"    ... and {correlation.Entries.Count - 3} more entries");
            }

            report.AppendLine();
        }
    }

    private void GenerateEmbeddingOutliersSection(
        StringBuilder report,
        AnalysisResult analysisResult
    )
    {
        report.AppendLine("SEMANTIC OUTLIERS");
        report.AppendLine("-".PadRight(40, '-'));
        report.AppendLine();

        var outliers = analysisResult.EmbeddingOutliers.Take(10);
        foreach (var outlier in outliers)
        {
            report.AppendLine($"• [{outlier.Timestamp:HH:mm:ss}] {outlier.Service}");
            report.AppendLine($"  Level: {outlier.Level}");
            report.AppendLine($"  Message: {TruncateMessage(outlier.Message, 100)}");
            report.AppendLine();
        }

        if (analysisResult.EmbeddingOutliers.Count > 10)
        {
            report.AppendLine(
                $"... and {analysisResult.EmbeddingOutliers.Count - 10} more outliers"
            );
        }

        report.AppendLine();
    }

    private void GenerateErrorsSection(StringBuilder report, AnalysisResult analysisResult)
    {
        report.AppendLine("ANALYSIS ERRORS");
        report.AppendLine("-".PadRight(40, '-'));
        report.AppendLine();

        foreach (var error in analysisResult.Errors)
        {
            report.AppendLine($"• {error}");
        }

        report.AppendLine();
    }

    private void GenerateFooter(StringBuilder report)
    {
        report.AppendLine("=".PadRight(80, '='));
        report.AppendLine($"Report generated by AnalyzeLogs v1.0");
        report.AppendLine($"Generated at: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        report.AppendLine("=".PadRight(80, '='));
    }

    private string DetermineHealthStatus(AnalysisResult analysisResult)
    {
        var criticalAnomalies = analysisResult.Anomalies.Count(a =>
            a.Severity == AnomalySeverity.Critical
        );
        var highAnomalies = analysisResult.Anomalies.Count(a => a.Severity == AnomalySeverity.High);

        if (criticalAnomalies > 0)
            return "🔴 CRITICAL - Immediate attention required";

        if (highAnomalies > 5)
            return "🟠 DEGRADED - Multiple high-severity issues detected";

        if (highAnomalies > 0)
            return "🟡 WARNING - Some issues detected";

        if (analysisResult.Anomalies.Any())
            return "🟢 HEALTHY - Minor issues detected";

        return "✅ EXCELLENT - No issues detected";
    }

    private string GetFileSizeDescription(string filePath)
    {
        try
        {
            var fileInfo = new FileInfo(filePath);
            var sizeInBytes = fileInfo.Length;

            if (sizeInBytes > 1024 * 1024)
                return $"{sizeInBytes / (1024.0 * 1024.0):F1} MB";

            if (sizeInBytes > 1024)
                return $"{sizeInBytes / 1024.0:F1} KB";

            return $"{sizeInBytes} bytes";
        }
        catch
        {
            return "Unknown size";
        }
    }

    private string TruncateMessage(string message, int maxLength)
    {
        if (string.IsNullOrEmpty(message))
            return string.Empty;

        if (message.Length <= maxLength)
            return message;

        return message.Substring(0, maxLength - 3) + "...";
    }
}
