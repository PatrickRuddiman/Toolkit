using System.Text;
using System.Text.Json;
using AnalyzeLogs.Models;
using AnalyzeLogs.Models.Database;
using Markdig;
using Microsoft.Extensions.Logging;

namespace AnalyzeLogs.Services;

/// <summary>
/// Service for generating rich markdown analysis reports compatible with DocFX
/// </summary>
public class MarkdownReportService
{
    private readonly ILogger<MarkdownReportService> _logger;
    private readonly MarkdownPipeline _markdownPipeline;

    public MarkdownReportService(ILogger<MarkdownReportService> logger)
    {
        _logger = logger;
        _markdownPipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
    }

    /// <summary>
    /// Generates a comprehensive DocFX-compatible markdown analysis report
    /// </summary>
    public async Task<string> GenerateMarkdownReportAsync(
        AnalysisResult analysisResult,
        List<string> logFiles,
        Project project,
        LogAnalysisSession session
    )
    {
        _logger.LogInformation(
            "Generating markdown analysis report for project '{ProjectName}', session '{SessionId}'",
            project.Name,
            session.Id
        );

        var report = new StringBuilder();

        // Document metadata for DocFX
        GenerateDocFXMetadata(report, project, session);

        // Title and overview
        GenerateTitle(report, project, session);

        // Executive summary
        await GenerateExecutiveSummary(report, analysisResult, logFiles);

        // Analysis overview table
        GenerateAnalysisOverview(report, analysisResult, logFiles, session);

        // Service health dashboard
        GenerateServiceHealthDashboard(report, analysisResult);

        // Anomalies section with detailed breakdown
        GenerateAnomaliesSection(report, analysisResult);

        // Correlations and patterns
        GenerateCorrelationsSection(report, analysisResult);

        // Time series analysis
        GenerateTimeSeriesAnalysis(report, analysisResult);

        // Service metrics deep dive
        GenerateServiceMetricsSection(report, analysisResult);

        // Error analysis
        if (analysisResult.Errors.Any())
        {
            GenerateErrorAnalysisSection(report, analysisResult);
        }

        // Embedding outliers
        if (analysisResult.EmbeddingOutliers.Any())
        {
            GenerateEmbeddingOutliersSection(report, analysisResult);
        }

        // Recommendations
        GenerateRecommendationsSection(report, analysisResult);

        // Appendices
        GenerateAppendicesSection(report, logFiles, session);

        _logger.LogInformation("Markdown report generation completed");
        return report.ToString();
    }

    /// <summary>
    /// Generate project-level report
    /// </summary>
    public async Task<string> GenerateProjectReportAsync(
        Project project,
        List<LogAnalysisSession> sessions
    )
    {
        var report = new StringBuilder();

        report.AppendLine($"# Project Analysis Report: {project.Name}");
        report.AppendLine();
        report.AppendLine($"**Created:** {project.CreatedAt:yyyy-MM-dd HH:mm:ss} UTC");
        if (project.LastAnalyzedAt.HasValue)
        {
            report.AppendLine(
                $"**Last Analyzed:** {project.LastAnalyzedAt.Value:yyyy-MM-dd HH:mm:ss} UTC"
            );
        }
        report.AppendLine();

        if (sessions.Any())
        {
            report.AppendLine("## Analysis Sessions");
            report.AppendLine();
            report.AppendLine("| Session | Date | Duration | Status | Entries | Anomalies |");
            report.AppendLine("|---------|------|----------|--------|---------|-----------|");

            foreach (var session in sessions.OrderByDescending(s => s.StartTime))
            {
                var duration = session.EndTime?.Subtract(session.StartTime) ?? TimeSpan.Zero;
                report.AppendLine(
                    $"| {session.SessionName} | {session.StartTime:yyyy-MM-dd} | {FormatDuration(duration)} | {session.Status} | {session.TotalEntries:N0} | {session.AnomaliesFound:N0} |"
                );
            }
        }

        return report.ToString();
    }

    /// <summary>
    /// Generate session-level report
    /// </summary>
    public async Task<string> GenerateSessionReportAsync(LogAnalysisSession session)
    {
        var report = new StringBuilder();

        report.AppendLine($"# Session Report: {session.SessionName}");
        report.AppendLine();
        report.AppendLine($"**Session ID:** {session.Id}");
        report.AppendLine($"**Started:** {session.StartTime:yyyy-MM-dd HH:mm:ss} UTC");
        if (session.EndTime.HasValue)
        {
            report.AppendLine($"**Completed:** {session.EndTime.Value:yyyy-MM-dd HH:mm:ss} UTC");
            var duration = session.EndTime.Value - session.StartTime;
            report.AppendLine($"**Duration:** {FormatDuration(duration)}");
        }
        report.AppendLine($"**Status:** {session.Status}");
        report.AppendLine($"**Total Entries:** {session.TotalEntries:N0}");
        report.AppendLine($"**Anomalies Found:** {session.AnomaliesFound:N0}");
        report.AppendLine($"**Correlations Found:** {session.CorrelationsFound:N0}");

        return report.ToString();
    }

    /// <summary>
    /// Generate a markdown report specifically for query results
    /// </summary>
    public async Task<string> GenerateQueryReportAsync(
        string projectName,
        string query,
        object queryResult,
        int sessionId
    )
    {
        var report = new StringBuilder();

        // DocFX metadata for query report
        report.AppendLine("---");
        report.AppendLine($"title: Query Results - {projectName}");
        report.AppendLine($"description: Results for query: {query}");
        report.AppendLine($"author: AI Log Analysis Tool");
        report.AppendLine($"ms.date: {DateTime.UtcNow:yyyy-MM-dd}");
        report.AppendLine($"ms.topic: query-results");
        report.AppendLine($"ms.service: log-analysis");
        report.AppendLine("---");
        report.AppendLine();

        // Title and query information
        report.AppendLine($"# Query Results: {projectName}");
        report.AppendLine();
        report.AppendLine($"> **Project:** {projectName}");
        report.AppendLine($"> **Query:** \"{query}\"");
        report.AppendLine($"> **Session ID:** `{sessionId}`");
        report.AppendLine($"> **Executed:** {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        report.AppendLine();

        // Query results section
        report.AppendLine("## 📋 Query Results");
        report.AppendLine();

        if (queryResult != null)
        {
            // Format the query result based on its type
            if (queryResult is string stringResult)
            {
                report.AppendLine(stringResult);
            }
            else
            {
                // Convert complex objects to formatted JSON
                var jsonOptions = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                };
                var jsonResult = JsonSerializer.Serialize(queryResult, jsonOptions);

                report.AppendLine("```json");
                report.AppendLine(jsonResult);
                report.AppendLine("```");
            }
        }
        else
        {
            report.AppendLine("No results found for the specified query.");
        }

        report.AppendLine();

        // Query execution metadata
        report.AppendLine("## 📊 Query Metadata");
        report.AppendLine();
        report.AppendLine("| Property | Value |");
        report.AppendLine("|----------|-------|");
        report.AppendLine($"| **Execution Time** | {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC |");
        report.AppendLine($"| **Query Length** | {query.Length} characters |");
        report.AppendLine($"| **Result Type** | {queryResult?.GetType().Name ?? "Null"} |");
        report.AppendLine();

        return report.ToString();
    }

    private void GenerateDocFXMetadata(
        StringBuilder report,
        Project project,
        LogAnalysisSession session
    )
    {
        report.AppendLine("---");
        report.AppendLine($"title: Log Analysis Report - {project.Name}");
        report.AppendLine($"description: Comprehensive analysis report for project {project.Name}");
        report.AppendLine($"author: AI Log Analysis Tool");
        report.AppendLine($"ms.date: {DateTime.UtcNow:yyyy-MM-dd}");
        report.AppendLine($"ms.topic: analysis-report");
        report.AppendLine($"ms.service: log-analysis");        report.AppendLine("---");
        report.AppendLine();
    }

    private void GenerateTitle(StringBuilder report, Project project, LogAnalysisSession session)
    {
        report.AppendLine($"# Log Analysis Report: {project.Name}");
        report.AppendLine();
        
        // Project information table
        report.AppendLine("## 📋 Project Information");
        report.AppendLine();
        report.AppendLine("| Property | Value |");
        report.AppendLine("|----------|-------|");
        report.AppendLine($"| **Project** | {project.Name} |");
        if (!string.IsNullOrEmpty(project.Description))
        {
            report.AppendLine($"| **Description** | {project.Description} |");
        }
        report.AppendLine($"| **Session ID** | `{session.Id}` |");
        report.AppendLine($"| **Analysis Date** | {session.StartTime:yyyy-MM-dd HH:mm:ss} UTC |");
        if (session.EndTime.HasValue)
        {
            var duration = session.EndTime.Value - session.StartTime;
            report.AppendLine($"| **Duration** | {FormatDuration(duration)} |");
        }
        report.AppendLine($"| **Status** | {session.Status} |");
        report.AppendLine();
    }

    private async Task GenerateExecutiveSummary(
        StringBuilder report,
        AnalysisResult analysisResult,
        List<string> logFiles
    )
    {
        report.AppendLine("## 📊 Executive Summary");
        report.AppendLine();

        var healthStatus = DetermineHealthStatus(analysisResult);
        var healthEmoji = GetHealthEmoji(healthStatus);
        var duration = analysisResult.EndTime - analysisResult.StartTime;

        report.AppendLine($"**Overall System Health:** {healthEmoji} **{healthStatus}**");
        report.AppendLine();

        var criticalAnomalies = analysisResult.Anomalies.Count(a => a.Confidence > 0.8);
        var highAnomalies = analysisResult.Anomalies.Count(a =>
            a.Confidence > 0.6 && a.Confidence <= 0.8
        );

        if (criticalAnomalies > 0)
        {
            report.AppendLine(
                $"⚠️ **{criticalAnomalies}** critical anomalies require immediate attention."
            );
        }
        if (highAnomalies > 0)
        {
            report.AppendLine($"⚡ **{highAnomalies}** high-priority anomalies detected.");
        }

        report.AppendLine();
        report.AppendLine("### Key Insights");
        report.AppendLine();
        await GenerateKeyInsights(report, analysisResult);
        report.AppendLine();
    }

    private void GenerateAnalysisOverview(
        StringBuilder report,
        AnalysisResult analysisResult,
        List<string> logFiles,
        LogAnalysisSession session
    )
    {
        report.AppendLine("## 📈 Analysis Overview");
        report.AppendLine();

        var duration = analysisResult.EndTime - analysisResult.StartTime;

        report.AppendLine("| Metric | Value |");
        report.AppendLine("|--------|-------|");
        report.AppendLine($"| **Log Files Analyzed** | {logFiles.Count:N0} |");
        report.AppendLine($"| **Total Log Entries** | {analysisResult.TotalEntries:N0} |");
        report.AppendLine($"| **Time Range Covered** | {FormatDuration(duration)} |");
        report.AppendLine($"| **Services Monitored** | {analysisResult.ServiceMetrics.Count} |");
        report.AppendLine($"| **Anomalies Detected** | {analysisResult.Anomalies.Count} |");
        report.AppendLine(
            $"| **Cross-Service Correlations** | {analysisResult.Correlations.Count} |"
        );
        report.AppendLine($"| **Error Patterns** | {analysisResult.Errors.Count} |");

        if (analysisResult.EmbeddingOutliers.Any())
        {
            report.AppendLine(
                $"| **Semantic Outliers** | {analysisResult.EmbeddingOutliers.Count} |"
            );
        }

        report.AppendLine();
    }

    private void GenerateServiceHealthDashboard(StringBuilder report, AnalysisResult analysisResult)
    {
        report.AppendLine("## 🏥 Service Health Dashboard");
        report.AppendLine();

        if (!analysisResult.ServiceMetrics.Any())
        {
            report.AppendLine("No service metrics available.");
            report.AppendLine();
            return;
        }

        // Generate Mermaid chart for service health visualization
        GenerateServiceHealthChart(report, analysisResult.ServiceMetrics);

        report.AppendLine("### Service Metrics Table");
        report.AppendLine();
        report.AppendLine("| Service | Status | Error Rate | Avg Response Time | Total Entries |");
        report.AppendLine("|---------|--------|------------|-------------------|---------------|");

        foreach (var service in analysisResult.ServiceMetrics.OrderBy(s => s.ServiceName))
        {
            var status = DetermineServiceStatus(service);
            var statusEmoji = GetServiceStatusEmoji(status);
            var errorRate =
                service.ErrorCount > 0
                    ? $"{(service.ErrorCount / (double)service.TotalEntries * 100):F2}%"
                    : "0%";
            var avgResponseTime = service.AverageResponseTime.HasValue
                ? $"{service.AverageResponseTime.Value:F0}ms"
                : "N/A";

            report.AppendLine(
                $"| **{service.ServiceName}** | {statusEmoji} {status} | {errorRate} | {avgResponseTime} | {service.TotalEntries:N0} |"
            );
        }

        report.AppendLine();

        // Generate error rate trend chart
        GenerateErrorRateTrendChart(report, analysisResult.ServiceMetrics);
    }

    /// <summary>
    /// Generate a Mermaid chart showing service health status
    /// </summary>
    private void GenerateServiceHealthChart(
        StringBuilder report,
        List<ServiceMetrics> serviceMetrics
    )
    {
        report.AppendLine("### Service Health Overview Chart");
        report.AppendLine();
        report.AppendLine("```mermaid");
        report.AppendLine("graph TD");

        foreach (var service in serviceMetrics.Take(10)) // Limit for readability
        {
            var status = DetermineServiceStatus(service);
            var nodeColor = status switch
            {
                "Critical" => "fill:#ff4444,stroke:#cc0000,color:#fff",
                "Degraded" => "fill:#ff8800,stroke:#cc6600,color:#fff",
                "Warning" => "fill:#ffcc00,stroke:#cc9900,color:#000",
                "Healthy" => "fill:#44ff44,stroke:#00cc00,color:#000",
                _ => "fill:#888888,stroke:#666666,color:#fff",
            };

            var safeServiceName = service.ServiceName.Replace(" ", "_").Replace("-", "_");
            var errorRate =
                service.ErrorCount > 0
                    ? (service.ErrorCount / (double)service.TotalEntries * 100)
                    : 0;

            report.AppendLine(
                $"    {safeServiceName}[\"{service.ServiceName}<br/>Error Rate: {errorRate:F1}%<br/>Entries: {service.TotalEntries:N0}\"]"
            );
            report.AppendLine($"    style {safeServiceName} {nodeColor}");
        }

        report.AppendLine("```");
        report.AppendLine();
    }

    /// <summary>
    /// Generate a chart showing error rate trends
    /// </summary>
    private void GenerateErrorRateTrendChart(
        StringBuilder report,
        List<ServiceMetrics> serviceMetrics
    )
    {
        var servicesWithErrors = serviceMetrics.Where(s => s.ErrorCount > 0).ToList();
        if (!servicesWithErrors.Any())
        {
            return;
        }

        report.AppendLine("### Error Rate Analysis Chart");
        report.AppendLine();
        report.AppendLine("```mermaid");
        report.AppendLine("xychart-beta");
        report.AppendLine("    title \"Service Error Rates\"");
        report.AppendLine(
            "    x-axis ["
                + string.Join(", ", servicesWithErrors.Select(s => $"\"{s.ServiceName}\""))
                + "]"
        );

        var errorRates = servicesWithErrors
            .Select(s => s.ErrorCount > 0 ? (s.ErrorCount / (double)s.TotalEntries * 100) : 0)
            .ToList();

        report.AppendLine(
            "    y-axis \"Error Rate (%)\" 0 --> " + (errorRates.Max() * 1.1).ToString("F1")
        );
        report.AppendLine(
            "    bar [" + String.Join(", ", errorRates.Select(r => r.ToString("F2"))) + "]"
        );
        report.AppendLine("```");
        report.AppendLine();
    }

    private void GenerateAnomaliesSection(StringBuilder report, AnalysisResult analysisResult)
    {
        report.AppendLine("## 🚨 Anomaly Analysis");
        report.AppendLine();

        if (!analysisResult.Anomalies.Any())
        {
            report.AppendLine("✅ No anomalies detected in the analyzed logs.");
            report.AppendLine();
            return;
        }

        // Generate anomaly distribution chart
        GenerateAnomalyDistributionChart(report, analysisResult.Anomalies);

        // Anomaly summary by severity
        var criticalAnomalies = analysisResult.Anomalies.Where(a => a.Confidence > 0.8).ToList();
        var highAnomalies = analysisResult
            .Anomalies.Where(a => a.Confidence > 0.6 && a.Confidence <= 0.8)
            .ToList();
        var mediumAnomalies = analysisResult
            .Anomalies.Where(a => a.Confidence > 0.4 && a.Confidence <= 0.6)
            .ToList();
        var lowAnomalies = analysisResult.Anomalies.Where(a => a.Confidence <= 0.4).ToList();

        report.AppendLine("### Severity Distribution");
        report.AppendLine();
        report.AppendLine("| Severity | Count | Percentage |");
        report.AppendLine("|----------|-------|------------|");
        report.AppendLine(
            $"| 🔴 Critical (>80%) | {criticalAnomalies.Count} | {(criticalAnomalies.Count / (double)analysisResult.Anomalies.Count * 100):F1}% |"
        );
        report.AppendLine(
            $"| 🟠 High (60-80%) | {highAnomalies.Count} | {(highAnomalies.Count / (double)analysisResult.Anomalies.Count * 100):F1}% |"
        );
        report.AppendLine(
            $"| 🟡 Medium (40-60%) | {mediumAnomalies.Count} | {(mediumAnomalies.Count / (double)analysisResult.Anomalies.Count * 100):F1}% |"
        );
        report.AppendLine(
            $"| 🟢 Low (<40%) | {lowAnomalies.Count} | {(lowAnomalies.Count / (double)analysisResult.Anomalies.Count * 100):F1}% |"
        );
        report.AppendLine();

        // Critical anomalies details
        if (criticalAnomalies.Any())
        {
            report.AppendLine("### 🔴 Critical Anomalies (Immediate Action Required)");
            report.AppendLine();
            GenerateAnomalyDetails(report, criticalAnomalies);
        }

        // High priority anomalies
        if (highAnomalies.Any())
        {
            report.AppendLine("### 🟠 High Priority Anomalies");
            report.AppendLine();
            GenerateAnomalyDetails(report, highAnomalies);
        }

        // Category breakdown
        var categories = analysisResult.Anomalies.GroupBy(a => a.Type.ToString()).ToList();
        if (categories.Any())
        {
            report.AppendLine("### Anomaly Categories");
            report.AppendLine();
            report.AppendLine("| Category | Count | Avg Confidence |");
            report.AppendLine("|----------|-------|----------------|");

            foreach (var category in categories.OrderByDescending(c => c.Count()))
            {
                var avgConfidence = category.Average(a => a.Confidence);
                report.AppendLine(
                    $"| **{category.Key}** | {category.Count()} | {avgConfidence:F2} |"
                );
            }
            report.AppendLine();
        }
    }

    private void GenerateAnomalyDetails(StringBuilder report, List<Anomaly> anomalies)
    {
        foreach (var anomaly in anomalies.OrderByDescending(a => a.Confidence).Take(10))
        {
            report.AppendLine(
                $"#### {GetSeverityEmoji(anomaly.Confidence)} {anomaly.Type} (Confidence: {anomaly.Confidence:F2})"
            );
            report.AppendLine();
            report.AppendLine($"**Description:** {anomaly.Description}");
            report.AppendLine();

            if (!string.IsNullOrEmpty(anomaly.Recommendation))
            {
                report.AppendLine($"**Recommendation:** {anomaly.Recommendation}");
                report.AppendLine();
            }

            if (anomaly.Tags.Any())
            {
                report.AppendLine($"**Tags:** {string.Join(", ", anomaly.Tags)}");
                report.AppendLine();
            }
        }
    }

    private void GenerateCorrelationsSection(StringBuilder report, AnalysisResult analysisResult)
    {
        report.AppendLine("## 🔗 Cross-Service Correlations");
        report.AppendLine();

        if (!analysisResult.Correlations.Any())
        {
            report.AppendLine("No cross-service correlations detected.");
            report.AppendLine();
            return;
        }

        // Generate correlation timeline chart
        GenerateCorrelationTimelineChart(report, analysisResult.Correlations);

        report.AppendLine(
            $"Found **{analysisResult.Correlations.Count}** correlation patterns across services:"
        );
        report.AppendLine();

        foreach (
            var correlation in analysisResult.Correlations.OrderByDescending(c => c.Entries.Count)
        )
        {
            var strengthEmoji = GetCorrelationStrengthEmoji(correlation.Entries.Count);
            report.AppendLine($"### {strengthEmoji} Correlation: {correlation.Id}");
            report.AppendLine();
            report.AppendLine($"**Entry Count:** {correlation.Entries.Count}");
            report.AppendLine(
                $"**Services Involved:** {string.Join(", ", correlation.Services.Select(s => $"`{s}`"))}"
            );
            report.AppendLine($"**Duration:** {FormatDuration(correlation.Duration)}");
            report.AppendLine($"**Success:** {(correlation.IsSuccessful ? "✅" : "❌")}");

            if (correlation.HasErrors)
            {
                report.AppendLine("**⚠️ Contains Errors**");
            }
            report.AppendLine();
        }
    }

    private void GenerateTimeSeriesAnalysis(StringBuilder report, AnalysisResult analysisResult)
    {
        report.AppendLine("## 📊 Time Series Analysis");
        report.AppendLine();

        var duration = analysisResult.EndTime - analysisResult.StartTime;
        report.AppendLine(
            $"**Analysis Period:** {analysisResult.StartTime:yyyy-MM-dd HH:mm:ss} UTC to {analysisResult.EndTime:yyyy-MM-dd HH:mm:ss} UTC"
        );
        report.AppendLine($"**Total Duration:** {FormatDuration(duration)}");
        report.AppendLine();

        // Generate timeline of significant events
        var significantEvents = new List<(DateTime Time, string Event, string Severity)>();

        // Add anomalies as events
        foreach (
            var anomaly in analysisResult.Anomalies.OrderByDescending(a => a.Confidence).Take(20)
        )
        {
            var severity =
                anomaly.Confidence > 0.8 ? "Critical"
                : anomaly.Confidence > 0.6 ? "High"
                : "Medium";
            significantEvents.Add(
                (anomaly.Timestamp, $"{anomaly.Type}: {anomaly.Description}", severity)
            );
        }

        if (significantEvents.Any())
        {
            report.AppendLine("### Timeline of Significant Events");
            report.AppendLine();
            report.AppendLine("| Time | Event | Severity |");
            report.AppendLine("|------|-------|----------|");

            foreach (var evt in significantEvents.OrderBy(e => e.Time))
            {
                var severityEmoji = evt.Severity switch
                {
                    "Critical" => "🔴",
                    "High" => "🟠",
                    "Medium" => "🟡",
                    _ => "🟢",
                };
                report.AppendLine(
                    $"| {evt.Time:HH:mm:ss} | {evt.Event} | {severityEmoji} {evt.Severity} |"
                );
            }
            report.AppendLine();
        }
    }

    private void GenerateServiceMetricsSection(StringBuilder report, AnalysisResult analysisResult)
    {
        report.AppendLine("## 📋 Service Metrics Deep Dive");
        report.AppendLine();

        if (!analysisResult.ServiceMetrics.Any())
        {
            report.AppendLine("No service metrics available.");
            report.AppendLine();
            return;
        }

        foreach (var service in analysisResult.ServiceMetrics.OrderBy(s => s.ServiceName))
        {
            var status = DetermineServiceStatus(service);
            var statusEmoji = GetServiceStatusEmoji(status);

            report.AppendLine($"### {statusEmoji} {service.ServiceName}");
            report.AppendLine();

            report.AppendLine("| Metric | Value |");
            report.AppendLine("|--------|-------|");
            report.AppendLine($"| **Total Log Entries** | {service.TotalEntries:N0} |");
            report.AppendLine($"| **Error Count** | {service.ErrorCount:N0} |");
            report.AppendLine($"| **Warning Count** | {service.WarningCount:N0} |");

            if (service.AverageResponseTime.HasValue)
            {
                report.AppendLine(
                    $"| **Average Response Time** | {service.AverageResponseTime.Value:F2} ms |"
                );
            }

            var errorRate =
                service.ErrorCount > 0
                    ? service.ErrorCount / (double)service.TotalEntries * 100
                    : 0;
            report.AppendLine($"| **Error Rate** | {errorRate:F2}% |");

            report.AppendLine();

            // Service-specific recommendations
            if (errorRate > 5)
            {
                report.AppendLine(
                    $"⚠️ **Recommendation:** Service `{service.ServiceName}` has a high error rate ({errorRate:F2}%). Investigate error logs for root cause analysis."
                );
                report.AppendLine();
            }
        }
    }

    private void GenerateErrorAnalysisSection(StringBuilder report, AnalysisResult analysisResult)
    {
        report.AppendLine("## 🐛 Error Analysis");
        report.AppendLine();

        if (!analysisResult.Errors.Any())
        {
            return;
        }

        report.AppendLine($"Detected **{analysisResult.Errors.Count}** error patterns:");
        report.AppendLine();

        foreach (var error in analysisResult.Errors.Take(20))
        {
            report.AppendLine($"### Error: {error}");
            report.AppendLine();
            report.AppendLine("```");
            report.AppendLine(error);
            report.AppendLine("```");
            report.AppendLine();
        }
    }

    private void GenerateEmbeddingOutliersSection(
        StringBuilder report,
        AnalysisResult analysisResult
    )
    {
        report.AppendLine("## 🎯 Semantic Outliers");
        report.AppendLine();

        if (!analysisResult.EmbeddingOutliers.Any())
        {
            return;
        }

        report.AppendLine(
            $"Detected **{analysisResult.EmbeddingOutliers.Count}** semantic outliers using AI analysis:"
        );
        report.AppendLine();

        report.AppendLine("| Entry ID | Service | Message | Outlier Score |");
        report.AppendLine("|----------|---------|---------|---------------|");

        foreach (var outlier in analysisResult.EmbeddingOutliers.Take(20))
        {
            var truncatedMessage =
                outlier.Message.Length > 100
                    ? outlier.Message.Substring(0, 100) + "..."
                    : outlier.Message;
            var score = outlier.AdditionalData.TryGetValue("OutlierScore", out var scoreStr)
                ? scoreStr
                : "N/A";

            report.AppendLine(
                $"| `{outlier.Id}` | {outlier.Service ?? "Unknown"} | {truncatedMessage.Replace("|", "\\|")} | {score} |"
            );
        }

        report.AppendLine();
    }

    private void GenerateRecommendationsSection(StringBuilder report, AnalysisResult analysisResult)
    {
        report.AppendLine("## 💡 Recommendations");
        report.AppendLine();

        var recommendations = new List<string>();

        // Generate recommendations based on analysis
        var criticalAnomalies = analysisResult.Anomalies.Count(a => a.Confidence > 0.8);
        if (criticalAnomalies > 0)
        {
            recommendations.Add(
                $"🔴 **Immediate Action Required:** {criticalAnomalies} critical anomalies need immediate investigation and resolution."
            );
        }

        var highErrorServices = analysisResult
            .ServiceMetrics.Where(s =>
                s.ErrorCount > 0 && (s.ErrorCount / (double)s.TotalEntries) > 0.05
            )
            .ToList();
        if (highErrorServices.Any())
        {
            recommendations.Add(
                $"⚠️ **Error Rate Alert:** Services with high error rates: {string.Join(", ", highErrorServices.Select(s => $"`{s.ServiceName}`"))}"
            );
        }

        if (analysisResult.Correlations.Any(c => c.Entries.Count > 10))
        {
            recommendations.Add(
                "🔗 **Strong Correlations Detected:** Review cross-service dependencies and potential cascade failure points."
            );
        }

        // Add specific recommendations from anomalies
        var specificRecommendations = analysisResult
            .Anomalies.Where(a => !string.IsNullOrEmpty(a.Recommendation))
            .Select(a => a.Recommendation)
            .Distinct()
            .Take(5);

        recommendations.AddRange(specificRecommendations.Select(r => $"📋 {r}"));

        if (recommendations.Any())
        {
            foreach (var recommendation in recommendations)
            {
                report.AppendLine($"- {recommendation}");
            }
        }
        else
        {
            report.AppendLine(
                "✅ No specific recommendations at this time. System appears to be operating normally."
            );
        }

        report.AppendLine();
    }

    private void GenerateAppendicesSection(
        StringBuilder report,
        List<string> logFiles,
        LogAnalysisSession session
    )
    {
        report.AppendLine("## 📎 Appendices");
        report.AppendLine();

        // Appendix A: Log Files
        report.AppendLine("### Appendix A: Analyzed Log Files");
        report.AppendLine();
        report.AppendLine("| File | Path |");
        report.AppendLine("|------|------|");

        for (int i = 0; i < logFiles.Count; i++)
        {
            var fileName = Path.GetFileName(logFiles[i]);
            report.AppendLine($"| {i + 1}. {fileName} | `{logFiles[i]}` |");
        }
        report.AppendLine();

        // Appendix B: Session Information
        report.AppendLine("### Appendix B: Session Information");
        report.AppendLine();
        report.AppendLine("| Property | Value |");
        report.AppendLine("|----------|-------|");
        report.AppendLine($"| **Session ID** | `{session.Id}` |");
        report.AppendLine($"| **Created** | {session.StartTime:yyyy-MM-dd HH:mm:ss} UTC |");
        if (session.EndTime.HasValue)
        {
            report.AppendLine(
                $"| **Completed** | {session.EndTime.Value:yyyy-MM-dd HH:mm:ss} UTC |"
            );
        }
        report.AppendLine($"| **Status** | {session.Status} |");
        report.AppendLine();
    }

    #region Helper Methods

    private async Task GenerateKeyInsights(StringBuilder report, AnalysisResult analysisResult)
    {
        var insights = new List<string>();

        // Service health insights
        var healthyServices = analysisResult.ServiceMetrics.Count(s => s.ErrorCount == 0);
        var totalServices = analysisResult.ServiceMetrics.Count;
        if (totalServices > 0)
        {
            var healthPercentage = (healthyServices / (double)totalServices) * 100;
            insights.Add(
                $"🏥 {healthyServices}/{totalServices} services ({healthPercentage:F0}%) are operating without errors"
            );
        }

        // Anomaly insights
        if (analysisResult.Anomalies.Any())
        {
            var topType = analysisResult
                .Anomalies.GroupBy(a => a.Type)
                .OrderByDescending(g => g.Count())
                .First();
            insights.Add(
                $"🚨 Most common anomaly type: **{topType.Key}** ({topType.Count()} occurrences)"
            );
        }

        // Correlation insights
        if (analysisResult.Correlations.Any())
        {
            var strongCorrelations = analysisResult.Correlations.Count(c => c.Entries.Count > 5);
            if (strongCorrelations > 0)
            {
                insights.Add($"🔗 {strongCorrelations} strong cross-service correlations detected");
            }
        }

        // Volume insights
        var avgLogsPerHour =
            analysisResult.TotalEntries
            / Math.Max(1, (analysisResult.EndTime - analysisResult.StartTime).TotalHours);
        insights.Add($"📈 Average log volume: {avgLogsPerHour:F0} entries/hour");

        foreach (var insight in insights)
        {
            report.AppendLine($"- {insight}");
        }
    }

    private string DetermineHealthStatus(AnalysisResult analysisResult)
    {
        var criticalAnomalies = analysisResult.Anomalies.Count(a => a.Confidence > 0.8);
        var highAnomalies = analysisResult.Anomalies.Count(a =>
            a.Confidence > 0.6 && a.Confidence <= 0.8
        );

        var errorRate = 0.0;
        if (analysisResult.ServiceMetrics.Any())
        {
            var totalLogs = analysisResult.ServiceMetrics.Sum(s => s.TotalEntries);
            var totalErrors = analysisResult.ServiceMetrics.Sum(s => s.ErrorCount);
            errorRate = totalErrors / (double)Math.Max(1, totalLogs) * 100;
        }

        if (criticalAnomalies > 0 || errorRate > 10)
            return "Critical";
        if (highAnomalies > 0 || errorRate > 5)
            return "Degraded";
        if (analysisResult.Anomalies.Any() || errorRate > 1)
            return "Warning";

        return "Healthy";
    }

    private string DetermineServiceStatus(ServiceMetrics service)
    {
        var errorRate = service.ErrorCount / (double)Math.Max(1, service.TotalEntries) * 100;

        if (errorRate > 10)
            return "Critical";
        if (errorRate > 5)
            return "Degraded";
        if (errorRate > 1)
            return "Warning";
        return "Healthy";
    }

    private string GetHealthEmoji(string health) =>
        health switch
        {
            "Critical" => "🔴",
            "Degraded" => "🟠",
            "Warning" => "🟡",
            "Healthy" => "🟢",
            _ => "⚫",
        };

    private string GetServiceStatusEmoji(string status) =>
        status switch
        {
            "Critical" => "🔴",
            "Degraded" => "🟠",
            "Warning" => "🟡",
            "Healthy" => "🟢",
            _ => "⚫",
        };

    private string GetSeverityEmoji(double confidence) =>
        confidence switch
        {
            > 0.8 => "🔴",
            > 0.6 => "🟠",
            > 0.4 => "🟡",
            _ => "🟢",
        };

    private string GetCorrelationStrengthEmoji(int entryCount) =>
        entryCount switch
        {
            > 20 => "🔴",
            > 10 => "🟠",
            > 5 => "🟡",
            _ => "🟢",
        };

    private string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalDays >= 1)
            return $"{duration.TotalDays:F1} days";
        if (duration.TotalHours >= 1)
            return $"{duration.TotalHours:F1} hours";
        if (duration.TotalMinutes >= 1)
            return $"{duration.TotalMinutes:F1} minutes";
        return $"{duration.TotalSeconds:F1} seconds";
    }

    /// <summary>
    /// Generate Mermaid chart for anomaly distribution
    /// </summary>
    private void GenerateAnomalyDistributionChart(StringBuilder report, List<Anomaly> anomalies)
    {
        if (!anomalies.Any())
            return;

        report.AppendLine("### Anomaly Distribution");
        report.AppendLine();
        report.AppendLine("```mermaid");
        report.AppendLine("pie title Anomaly Types");

        var anomalyGroups = anomalies
            .GroupBy(a => a.Type.ToString())
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count);

        foreach (var group in anomalyGroups)
        {
            report.AppendLine($"    \"{group.Type}\" : {group.Count}");
        }

        report.AppendLine("```");
        report.AppendLine();
    }

    /// <summary>
    /// Generate Mermaid timeline chart for correlations
    /// </summary>
    private void GenerateCorrelationTimelineChart(
        StringBuilder report,
        List<LogCorrelation> correlations
    )
    {
        if (!correlations.Any())
            return;

        report.AppendLine("### Correlation Timeline");
        report.AppendLine();
        report.AppendLine("```mermaid");
        report.AppendLine("gantt");
        report.AppendLine("    title Correlation Timeline");
        report.AppendLine("    dateFormat  HH:mm:ss");
        report.AppendLine("    axisFormat %H:%M");

        foreach (var correlation in correlations.Take(10)) // Limit to avoid clutter
        {
            var section = String.Join(", ", correlation.Services.Take(2));
            if (correlation.Services.Count > 2)
                section += "...";

            var startTime = correlation.StartTime.ToString("HH:mm:ss");
            var endTime = correlation.EndTime.ToString("HH:mm:ss");
            var status = correlation.IsSuccessful ? "done" : "crit";

            report.AppendLine($"    section {section}");
            report.AppendLine($"    Correlation : {status}, {startTime}, {endTime}");
        }

        report.AppendLine("```");
        report.AppendLine();
    }

    #endregion
}
