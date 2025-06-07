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
    private readonly OpenAIService _openAIService;

    public MarkdownReportService(ILogger<MarkdownReportService> logger, OpenAIService openAIService)
    {
        _logger = logger;
        _openAIService = openAIService;
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
        await GenerateServiceHealthDashboard(report, analysisResult, project, session);

        // Anomalies section with detailed breakdown
        await GenerateAnomaliesSection(report, analysisResult, project, session);

        // Correlations and patterns
        await GenerateCorrelationsSection(report, analysisResult, project, session);

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
        report.AppendLine($"ms.service: log-analysis");
        report.AppendLine("---");
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

    private async Task GenerateServiceHealthDashboard(
        StringBuilder report,
        AnalysisResult analysisResult,
        Project project,
        LogAnalysisSession session
    )
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
        await GenerateEnhancedServiceHealthChart(
            report,
            analysisResult.ServiceMetrics,
            project,
            session
        );

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
        await GenerateEnhancedErrorRateChart(
            report,
            analysisResult.ServiceMetrics,
            project,
            session
        );
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

    private async Task GenerateAnomaliesSection(
        StringBuilder report,
        AnalysisResult analysisResult,
        Project project,
        LogAnalysisSession session
    )
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
        await GenerateEnhancedAnomalyDistributionChart(
            report,
            analysisResult.Anomalies,
            project,
            session
        );

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

    private async Task GenerateCorrelationsSection(
        StringBuilder report,
        AnalysisResult analysisResult,
        Project project,
        LogAnalysisSession session
    )
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
        await GenerateEnhancedCorrelationTimelineChart(
            report,
            analysisResult.Correlations,
            project,
            session
        );

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


    /// <summary>
    /// Generates enhanced service health chart using AI and creates data validation file
    /// </summary>
    private async Task GenerateEnhancedServiceHealthChart(
        StringBuilder report,
        List<ServiceMetrics> serviceMetrics,
        Project project,
        LogAnalysisSession session
    )
    {
        if (!serviceMetrics.Any())
        {
            return;
        }

        try
        {
            // Create data validation table file
            var dataFilePath = await CreateServiceHealthDataFile(serviceMetrics, project, session);

            // Prepare data for AI
            var chartData = PrepareServiceHealthData(serviceMetrics);

            // Generate AI-powered Mermaid diagram
            var aiResponse = await _openAIService.CallPatternAsync(
                "patterns/generate_mermaid_diagrams/system.md",
                chartData,
                "gpt-4o-mini-search-preview-2025-03-11"
            );

            if (!string.IsNullOrEmpty(aiResponse))
            {
                var diagram = ParseMermaidDiagramResponse(aiResponse);
                if (diagram != null)
                {
                    report.AppendLine("### 🎨 AI-Enhanced Service Health Overview");
                    report.AppendLine();
                    report.AppendLine($"**{diagram.Title}**");
                    report.AppendLine();
                    report.AppendLine($"*{diagram.Description}*");
                    report.AppendLine();
                    report.AppendLine("```mermaid");
                    report.AppendLine(diagram.MermaidCode);
                    report.AppendLine("```");
                    report.AppendLine();

                    if (!string.IsNullOrEmpty(diagram.Insights))
                    {
                        report.AppendLine($"**💡 Key Insights:** {diagram.Insights}");
                        report.AppendLine();
                    }

                    // Add link to validation data
                    report.AppendLine(
                        $"📊 [View Raw Data for Validation]({Path.GetFileName(dataFilePath)})"
                    );
                    report.AppendLine();
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to generate AI-enhanced service health chart, falling back to standard chart"
            );
        }

        // Fallback to standard chart generation
        GenerateServiceHealthChart(report, serviceMetrics);
    }

    /// <summary>
    /// Generates enhanced anomaly distribution chart using AI and creates data validation file
    /// </summary>
    private async Task GenerateEnhancedAnomalyDistributionChart(
        StringBuilder report,
        List<Anomaly> anomalies,
        Project project,
        LogAnalysisSession session
    )
    {
        if (!anomalies.Any())
        {
            return;
        }

        try
        {
            // Create data validation table file
            var dataFilePath = await CreateAnomalyDataFile(anomalies, project, session);

            // Prepare data for AI
            var chartData = PrepareAnomalyDistributionData(anomalies);

            // Generate AI-powered Mermaid diagram
            var aiResponse = await _openAIService.CallPatternAsync(
                "patterns/generate_mermaid_diagrams/system.md",
                chartData,
                "gpt-4o-mini-search-preview-2025-03-11"
            );

            if (!string.IsNullOrEmpty(aiResponse))
            {
                var diagram = ParseMermaidDiagramResponse(aiResponse);
                if (diagram != null)
                {
                    report.AppendLine("### 🎨 AI-Enhanced Anomaly Distribution");
                    report.AppendLine();
                    report.AppendLine($"**{diagram.Title}**");
                    report.AppendLine();
                    report.AppendLine($"*{diagram.Description}*");
                    report.AppendLine();
                    report.AppendLine("```mermaid");
                    report.AppendLine(diagram.MermaidCode);
                    report.AppendLine("```");
                    report.AppendLine();

                    if (!string.IsNullOrEmpty(diagram.Insights))
                    {
                        report.AppendLine($"**💡 Key Insights:** {diagram.Insights}");
                        report.AppendLine();
                    }

                    // Add link to validation data
                    report.AppendLine(
                        $"📊 [View Raw Data for Validation]({Path.GetFileName(dataFilePath)})"
                    );
                    report.AppendLine();
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to generate AI-enhanced anomaly chart, falling back to standard chart"
            );
        }

        // Fallback to standard chart generation
        GenerateAnomalyDistributionChart(report, anomalies);
    }

    /// <summary>
    /// Generates enhanced correlation timeline chart using AI and creates data validation file
    /// </summary>
    private async Task GenerateEnhancedCorrelationTimelineChart(
        StringBuilder report,
        List<LogCorrelation> correlations,
        Project project,
        LogAnalysisSession session
    )
    {
        if (!correlations.Any())
        {
            return;
        }

        try
        {
            // Create data validation table file
            var dataFilePath = await CreateCorrelationDataFile(correlations, project, session);

            // Prepare data for AI
            var chartData = PrepareCorrelationTimelineData(correlations);

            // Generate AI-powered Mermaid diagram
            var aiResponse = await _openAIService.CallPatternAsync(
                "patterns/generate_mermaid_diagrams/system.md",
                chartData,
                "gpt-4o-mini-search-preview-2025-03-11"
            );

            if (!string.IsNullOrEmpty(aiResponse))
            {
                var diagram = ParseMermaidDiagramResponse(aiResponse);
                if (diagram != null)
                {
                    report.AppendLine("### 🎨 AI-Enhanced Correlation Timeline");
                    report.AppendLine();
                    report.AppendLine($"**{diagram.Title}**");
                    report.AppendLine();
                    report.AppendLine($"*{diagram.Description}*");
                    report.AppendLine();
                    report.AppendLine("```mermaid");
                    report.AppendLine(diagram.MermaidCode);
                    report.AppendLine("```");
                    report.AppendLine();

                    if (!string.IsNullOrEmpty(diagram.Insights))
                    {
                        report.AppendLine($"**💡 Key Insights:** {diagram.Insights}");
                        report.AppendLine();
                    }

                    // Add link to validation data
                    report.AppendLine(
                        $"📊 [View Raw Data for Validation]({Path.GetFileName(dataFilePath)})"
                    );
                    report.AppendLine();
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to generate AI-enhanced correlation chart, falling back to standard chart"
            );
        }

        // Fallback to standard chart generation
        GenerateCorrelationTimelineChart(report, correlations);
    }

    /// <summary>
    /// Generates enhanced error rate chart using AI and creates data validation file
    /// </summary>
    private async Task GenerateEnhancedErrorRateChart(
        StringBuilder report,
        List<ServiceMetrics> serviceMetrics,
        Project project,
        LogAnalysisSession session
    )
    {
        var servicesWithErrors = serviceMetrics.Where(s => s.ErrorCount > 0).ToList();
        if (!servicesWithErrors.Any())
        {
            return;
        }

        try
        {
            // Create data validation table file
            var dataFilePath = await CreateErrorRateDataFile(servicesWithErrors, project, session);

            // Prepare data for AI
            var chartData = PrepareErrorRateData(servicesWithErrors);

            // Generate AI-powered Mermaid diagram
            var aiResponse = await _openAIService.CallPatternAsync(
                "patterns/generate_mermaid_diagrams/system.md",
                chartData,
                "gpt-4o-mini-search-preview-2025-03-11"
            );

            if (!string.IsNullOrEmpty(aiResponse))
            {
                var diagram = ParseMermaidDiagramResponse(aiResponse);
                if (diagram != null)
                {
                    report.AppendLine("### 🎨 AI-Enhanced Error Rate Analysis");
                    report.AppendLine();
                    report.AppendLine($"**{diagram.Title}**");
                    report.AppendLine();
                    report.AppendLine($"*{diagram.Description}*");
                    report.AppendLine();
                    report.AppendLine("```mermaid");
                    report.AppendLine(diagram.MermaidCode);
                    report.AppendLine("```");
                    report.AppendLine();

                    if (!string.IsNullOrEmpty(diagram.Insights))
                    {
                        report.AppendLine($"**💡 Key Insights:** {diagram.Insights}");
                        report.AppendLine();
                    }

                    // Add link to validation data
                    report.AppendLine(
                        $"📊 [View Raw Data for Validation]({Path.GetFileName(dataFilePath)})"
                    );
                    report.AppendLine();
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to generate AI-enhanced error rate chart, falling back to standard chart"
            );
        }

        // Fallback to standard chart generation
        GenerateErrorRateTrendChart(report, serviceMetrics);
    }

    /// <summary>
    /// Prepares service health data for AI chart generation
    /// </summary>
    private string PrepareServiceHealthData(List<ServiceMetrics> serviceMetrics)
    {
        var dataBuilder = new StringBuilder();
        dataBuilder.AppendLine("SERVICE HEALTH DATA:");
        dataBuilder.AppendLine();
        foreach (var service in serviceMetrics.Take(15)) // Limit for readability
        {
            var errorRate =
                service.ErrorCount > 0
                    ? (service.ErrorCount / (double)service.TotalEntries * 100)
                    : 0;
            var status = DetermineServiceStatus(service);

            dataBuilder.AppendLine($"Service: {service.ServiceName}");
            dataBuilder.AppendLine($"  Status: {status}");
            dataBuilder.AppendLine($"  Error Rate: {errorRate:F1}%");
            dataBuilder.AppendLine($"  Total Entries: {service.TotalEntries:N0}");
            dataBuilder.AppendLine($"  Error Count: {service.ErrorCount:N0}");
            dataBuilder.AppendLine($"  Warning Count: {service.WarningCount:N0}");
            if (service.AverageResponseTime.HasValue)
            {
                dataBuilder.AppendLine(
                    $"  Avg Response Time: {service.AverageResponseTime.Value:F2}ms"
                );
            }
            dataBuilder.AppendLine();
        }

        dataBuilder.AppendLine(
            "Please create a service-health diagram showing the health status of these services with appropriate colors and metrics."
        );

        return dataBuilder.ToString();
    }

    /// <summary>
    /// Prepares anomaly distribution data for AI chart generation
    /// </summary>
    private string PrepareAnomalyDistributionData(List<Anomaly> anomalies)
    {
        var dataBuilder = new StringBuilder();
        dataBuilder.AppendLine("ANOMALY DISTRIBUTION DATA:");
        dataBuilder.AppendLine();
        var anomalyGroups = anomalies
            .GroupBy(a => a.Type.ToString())
            .Select(g => new
            {
                Type = g.Key,
                Count = g.Count(),
                AvgConfidence = g.Average(x => x.Confidence),
            })
            .OrderByDescending(x => x.Count)
            .ToList();

        foreach (var group in anomalyGroups)
        {
            dataBuilder.AppendLine($"Anomaly Type: {group.Type}");
            dataBuilder.AppendLine($"  Count: {group.Count}");
            dataBuilder.AppendLine($"  Average Confidence: {group.AvgConfidence:F2}");
            dataBuilder.AppendLine();
        }

        // Add severity breakdown
        var criticalCount = anomalies.Count(a => a.Confidence > 0.8);
        var highCount = anomalies.Count(a => a.Confidence > 0.6 && a.Confidence <= 0.8);
        var mediumCount = anomalies.Count(a => a.Confidence > 0.4 && a.Confidence <= 0.6);
        var lowCount = anomalies.Count(a => a.Confidence <= 0.4);

        dataBuilder.AppendLine("SEVERITY BREAKDOWN:");
        dataBuilder.AppendLine($"Critical (>80%): {criticalCount}");
        dataBuilder.AppendLine($"High (60-80%): {highCount}");
        dataBuilder.AppendLine($"Medium (40-60%): {mediumCount}");
        dataBuilder.AppendLine($"Low (<40%): {lowCount}");
        dataBuilder.AppendLine();
        dataBuilder.AppendLine(
            "Please create an anomaly-distribution diagram showing both the type distribution and severity breakdown."
        );

        return dataBuilder.ToString();
    }

    /// <summary>
    /// Prepares correlation timeline data for AI chart generation
    /// </summary>
    private string PrepareCorrelationTimelineData(List<LogCorrelation> correlations)
    {
        var dataBuilder = new StringBuilder();
        dataBuilder.AppendLine("CORRELATION TIMELINE DATA:");
        dataBuilder.AppendLine();

        foreach (var correlation in correlations.Take(10)) // Limit for readability
        {
            dataBuilder.AppendLine($"Correlation ID: {correlation.Id}");
            dataBuilder.AppendLine($"  Services: {string.Join(", ", correlation.Services)}");
            dataBuilder.AppendLine($"  Start Time: {correlation.StartTime:HH:mm:ss}");
            dataBuilder.AppendLine($"  End Time: {correlation.EndTime:HH:mm:ss}");
            dataBuilder.AppendLine($"  Duration: {correlation.Duration.TotalSeconds:F1}s");
            dataBuilder.AppendLine($"  Entry Count: {correlation.Entries.Count}");
            dataBuilder.AppendLine($"  Is Successful: {correlation.IsSuccessful}");
            dataBuilder.AppendLine($"  Has Errors: {correlation.HasErrors}");
            dataBuilder.AppendLine();
        }
        dataBuilder.AppendLine(
            "Please create a correlation-timeline diagram showing when services interact and their success/failure patterns."
        );

        return dataBuilder.ToString();
    }

    /// <summary>
    /// Prepares error rate data for AI chart generation
    /// </summary>
    private string PrepareErrorRateData(List<ServiceMetrics> servicesWithErrors)
    {
        var dataBuilder = new StringBuilder();
        dataBuilder.AppendLine("ERROR RATE ANALYSIS DATA:");
        dataBuilder.AppendLine();

        foreach (var service in servicesWithErrors.Take(15)) // Limit for readability
        {
            var errorRate = service.ErrorCount / (double)service.TotalEntries * 100;
            var status = DetermineServiceStatus(service);

            dataBuilder.AppendLine($"Service: {service.ServiceName}");
            dataBuilder.AppendLine($"  Error Rate: {errorRate:F2}%");
            dataBuilder.AppendLine($"  Error Count: {service.ErrorCount:N0}");
            dataBuilder.AppendLine($"  Total Entries: {service.TotalEntries:N0}");
            dataBuilder.AppendLine($"  Status: {status}");
            dataBuilder.AppendLine();
        }
        dataBuilder.AppendLine(
            "Please create a performance-trends diagram showing error rates across services with appropriate thresholds and colors."
        );

        return dataBuilder.ToString();
    }

    /// <summary>
    /// Creates a markdown table file with service health data for validation
    /// </summary>
    private async Task<string> CreateServiceHealthDataFile(
        List<ServiceMetrics> serviceMetrics,
        Project project,
        LogAnalysisSession session
    )
    {
        var fileName =
            $"{project.Name}-service-health-data-{session.Id}-{DateTime.Now:yyyyMMdd-HHmmss}.md";
        var filePath = Path.Combine("docs", fileName);

        var dataTable = new StringBuilder();
        dataTable.AppendLine($"# Service Health Data - {project.Name}");
        dataTable.AppendLine();
        dataTable.AppendLine($"**Generated:** {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        dataTable.AppendLine($"**Session:** {session.Id}");
        dataTable.AppendLine();
        dataTable.AppendLine("## Raw Service Metrics Data");
        dataTable.AppendLine();
        dataTable.AppendLine(
            "| Service | Status | Error Rate (%) | Error Count | Warning Count | Total Entries | Avg Response Time (ms) |"
        );
        dataTable.AppendLine(
            "|---------|--------|----------------|-------------|---------------|---------------|------------------------|"
        );
        foreach (var service in serviceMetrics.OrderBy(s => s.ServiceName))
        {
            var errorRate =
                service.ErrorCount > 0
                    ? (service.ErrorCount / (double)service.TotalEntries * 100)
                    : 0;
            var status = DetermineServiceStatus(service);
            var avgResponseTime = service.AverageResponseTime?.ToString("F2") ?? "N/A";

            dataTable.AppendLine(
                $"| {service.ServiceName} | {status} | {errorRate:F2} | {service.ErrorCount:N0} | {service.WarningCount:N0} | {service.TotalEntries:N0} | {avgResponseTime} |"
            );
        }

        dataTable.AppendLine();
        dataTable.AppendLine("## Status Legend");
        dataTable.AppendLine();
        dataTable.AppendLine("- **Healthy**: Error rate ≤ 1%");
        dataTable.AppendLine("- **Warning**: Error rate 1-5%");
        dataTable.AppendLine("- **Degraded**: Error rate 5-10%");
        dataTable.AppendLine("- **Critical**: Error rate > 10%");

        // Ensure docs directory exists
        Directory.CreateDirectory("docs");
        await File.WriteAllTextAsync(filePath, dataTable.ToString());
        _logger.LogInformation("Created service health data validation file: {FilePath}", filePath);
        return filePath;
    }

    /// <summary>
    /// Creates a markdown table file with anomaly data for validation
    /// </summary>
    private async Task<string> CreateAnomalyDataFile(
        List<Anomaly> anomalies,
        Project project,
        LogAnalysisSession session
    )
    {
        var fileName =
            $"{project.Name}-anomaly-data-{session.Id}-{DateTime.Now:yyyyMMdd-HHmmss}.md";
        var filePath = Path.Combine("docs", fileName);

        var dataTable = new StringBuilder();
        dataTable.AppendLine($"# Anomaly Distribution Data - {project.Name}");
        dataTable.AppendLine();
        dataTable.AppendLine($"**Generated:** {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        dataTable.AppendLine($"**Session:** {session.Id}");
        dataTable.AppendLine();
        dataTable.AppendLine("## Anomaly Type Distribution");
        dataTable.AppendLine();
        dataTable.AppendLine("| Type | Count | Percentage | Avg Confidence |");
        dataTable.AppendLine("|------|-------|------------|----------------|");
        var anomalyGroups = anomalies
            .GroupBy(a => a.Type.ToString())
            .Select(g => new
            {
                Type = g.Key,
                Count = g.Count(),
                Percentage = (g.Count() / (double)anomalies.Count * 100),
                AvgConfidence = g.Average(x => x.Confidence),
            })
            .OrderByDescending(x => x.Count)
            .ToList();
        foreach (var group in anomalyGroups)
        {
            dataTable.AppendLine(
                $"| {group.Type} | {group.Count} | {group.Percentage:F1}% | {group.AvgConfidence:F2} |"
            );
        }

        dataTable.AppendLine();
        dataTable.AppendLine("## Severity Breakdown");
        dataTable.AppendLine();
        dataTable.AppendLine("| Severity | Count | Percentage |");
        dataTable.AppendLine("|----------|-------|------------|");

        var criticalCount = anomalies.Count(a => a.Confidence > 0.8);
        var highCount = anomalies.Count(a => a.Confidence > 0.6 && a.Confidence <= 0.8);
        var mediumCount = anomalies.Count(a => a.Confidence > 0.4 && a.Confidence <= 0.6);
        var lowCount = anomalies.Count(a => a.Confidence <= 0.4);
        dataTable.AppendLine(
            $"| Critical (>80%) | {criticalCount} | {(criticalCount / (double)anomalies.Count * 100):F1}% |"
        );
        dataTable.AppendLine(
            $"| High (60-80%) | {highCount} | {(highCount / (double)anomalies.Count * 100):F1}% |"
        );
        dataTable.AppendLine(
            $"| Medium (40-60%) | {mediumCount} | {(mediumCount / (double)anomalies.Count * 100):F1}% |"
        );
        dataTable.AppendLine(
            $"| Low (<40%) | {lowCount} | {(lowCount / (double)anomalies.Count * 100):F1}% |"
        );

        dataTable.AppendLine();
        dataTable.AppendLine("## Individual Anomalies (Top 20)");
        dataTable.AppendLine();
        dataTable.AppendLine("| ID | Type | Confidence | Description | Timestamp |");
        dataTable.AppendLine("|----|------|------------|-------------|-----------|");
        foreach (var anomaly in anomalies.OrderByDescending(a => a.Confidence).Take(20))
        {
            var description =
                anomaly.Description.Length > 100
                    ? anomaly.Description.Substring(0, 100) + "..."
                    : anomaly.Description;
            dataTable.AppendLine(
                $"| {anomaly.Id} | {anomaly.Type} | {anomaly.Confidence:F2} | {description.Replace("|", "\\|")} | {anomaly.Timestamp:yyyy-MM-dd HH:mm:ss} |"
            );
        }

        // Ensure docs directory exists
        Directory.CreateDirectory("docs");
        await File.WriteAllTextAsync(filePath, dataTable.ToString());
        _logger.LogInformation("Created anomaly data validation file: {FilePath}", filePath);
        return filePath;
    }

    /// <summary>
    /// Creates a markdown table file with correlation data for validation
    /// </summary>
    private async Task<string> CreateCorrelationDataFile(
        List<LogCorrelation> correlations,
        Project project,
        LogAnalysisSession session
    )
    {
        var fileName =
            $"{project.Name}-correlation-data-{session.Id}-{DateTime.Now:yyyyMMdd-HHmmss}.md";
        var filePath = Path.Combine("docs", fileName);

        var dataTable = new StringBuilder();
        dataTable.AppendLine($"# Correlation Timeline Data - {project.Name}");
        dataTable.AppendLine();
        dataTable.AppendLine($"**Generated:** {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        dataTable.AppendLine($"**Session:** {session.Id}");
        dataTable.AppendLine();
        dataTable.AppendLine("## Cross-Service Correlations");
        dataTable.AppendLine();
        dataTable.AppendLine(
            "| Correlation ID | Services | Start Time | End Time | Duration (s) | Entries | Success | Has Errors |"
        );
        dataTable.AppendLine(
            "|----------------|----------|------------|----------|--------------|---------|---------|------------|"
        );

        foreach (var correlation in correlations.OrderBy(c => c.StartTime))
        {
            var services = string.Join(", ", correlation.Services);
            if (services.Length > 50)
            {
                services = services.Substring(0, 50) + "...";
            }
            dataTable.AppendLine(
                $"| {correlation.Id} | {services} | {correlation.StartTime:HH:mm:ss} | {correlation.EndTime:HH:mm:ss} | {correlation.Duration.TotalSeconds:F1} | {correlation.Entries.Count} | {(correlation.IsSuccessful ? "✅" : "❌")} | {(correlation.HasErrors ? "⚠️" : "✅")} |"
            );
        }

        dataTable.AppendLine();
        dataTable.AppendLine("## Summary Statistics");
        dataTable.AppendLine();
        dataTable.AppendLine("| Metric | Value |");
        dataTable.AppendLine("|--------|-------|");
        dataTable.AppendLine($"| Total Correlations | {correlations.Count} |");
        dataTable.AppendLine(
            $"| Successful Correlations | {correlations.Count(c => c.IsSuccessful)} |"
        );
        dataTable.AppendLine(
            $"| Failed Correlations | {correlations.Count(c => !c.IsSuccessful)} |"
        );
        dataTable.AppendLine(
            $"| Correlations with Errors | {correlations.Count(c => c.HasErrors)} |"
        );
        dataTable.AppendLine(
            $"| Average Duration | {(correlations.Any() ? correlations.Average(c => c.Duration.TotalSeconds) : 0):F1}s |"
        );
        dataTable.AppendLine(
            $"| Average Entry Count | {(correlations.Any() ? correlations.Average(c => c.Entries.Count) : 0):F1} |"
        );

        // Ensure docs directory exists
        Directory.CreateDirectory("docs");
        await File.WriteAllTextAsync(filePath, dataTable.ToString());
        _logger.LogInformation("Created correlation data validation file: {FilePath}", filePath);
        return filePath;
    }

    /// <summary>
    /// Creates a markdown table file with error rate data for validation
    /// </summary>
    private async Task<string> CreateErrorRateDataFile(
        List<ServiceMetrics> servicesWithErrors,
        Project project,
        LogAnalysisSession session
    )
    {
        var fileName =
            $"{project.Name}-error-rate-data-{session.Id}-{DateTime.Now:yyyyMMdd-HHmmss}.md";
        var filePath = Path.Combine("docs", fileName);

        var dataTable = new StringBuilder();
        dataTable.AppendLine($"# Error Rate Analysis Data - {project.Name}");
        dataTable.AppendLine();
        dataTable.AppendLine($"**Generated:** {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        dataTable.AppendLine($"**Session:** {session.Id}");
        dataTable.AppendLine();
        dataTable.AppendLine("## Error Rate by Service");
        dataTable.AppendLine();
        dataTable.AppendLine(
            "| Service | Error Rate (%) | Error Count | Total Entries | Status | Error Ratio |"
        );
        dataTable.AppendLine(
            "|---------|----------------|-------------|---------------|--------|-------------|"
        );

        foreach (
            var service in servicesWithErrors.OrderByDescending(s =>
                (s.ErrorCount / (double)s.TotalEntries * 100)
            )
        )
        {
            var errorRate = service.ErrorCount / (double)service.TotalEntries * 100;
            var status = DetermineServiceStatus(service);
            var errorRatio = $"{service.ErrorCount}/{service.TotalEntries}";

            dataTable.AppendLine(
                $"| {service.ServiceName} | {errorRate:F2} | {service.ErrorCount:N0} | {service.TotalEntries:N0} | {status} | {errorRatio} |"
            );
        }

        dataTable.AppendLine();
        dataTable.AppendLine("## Error Rate Categories");
        dataTable.AppendLine();
        dataTable.AppendLine("| Category | Count | Services |");
        dataTable.AppendLine("|----------|-------|----------|");
        var criticalServices = servicesWithErrors
            .Where(s => (s.ErrorCount / (double)s.TotalEntries * 100) > 10)
            .ToList();
        var degradedServices = servicesWithErrors
            .Where(s =>
            {
                var rate = s.ErrorCount / (double)s.TotalEntries * 100;
                return rate > 5 && rate <= 10;
            })
            .ToList();
        var warningServices = servicesWithErrors
            .Where(s =>
            {
                var rate = s.ErrorCount / (double)s.TotalEntries * 100;
                return rate > 1 && rate <= 5;
            })
            .ToList();
        dataTable.AppendLine(
            $"| Critical (>10%) | {criticalServices.Count} | {string.Join(", ", criticalServices.Select(s => s.ServiceName))} |"
        );
        dataTable.AppendLine(
            $"| Degraded (5-10%) | {degradedServices.Count} | {string.Join(", ", degradedServices.Select(s => s.ServiceName))} |"
        );
        dataTable.AppendLine(
            $"| Warning (1-5%) | {warningServices.Count} | {string.Join(", ", warningServices.Select(s => s.ServiceName))} |"
        );

        // Ensure docs directory exists
        Directory.CreateDirectory("docs");
        await File.WriteAllTextAsync(filePath, dataTable.ToString());
        _logger.LogInformation("Created error rate data validation file: {FilePath}", filePath);
        return filePath;
    }

    /// <summary>
    /// Represents a parsed Mermaid diagram response from AI
    /// </summary>
    private class MermaidDiagram
    {
        public string Title { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string MermaidCode { get; set; } = string.Empty;
        public string Insights { get; set; } = string.Empty;
    }

    /// <summary>
    /// Parses the AI response containing a Mermaid diagram
    /// </summary>
    private MermaidDiagram? ParseMermaidDiagramResponse(string aiResponse)
    {
        if (string.IsNullOrEmpty(aiResponse))
        {
            return null;
        }

        try
        {
            // Look for DIAGRAM_START and DIAGRAM_END markers
            var startMarker = "DIAGRAM_START";
            var endMarker = "DIAGRAM_END";

            var startIndex = aiResponse.IndexOf(startMarker);
            var endIndex = aiResponse.IndexOf(endMarker);

            if (startIndex == -1 || endIndex == -1 || endIndex <= startIndex)
            {
                _logger.LogWarning("AI response does not contain valid diagram markers");
                return null;
            }

            var diagramContent = aiResponse
                .Substring(
                    startIndex + startMarker.Length,
                    endIndex - startIndex - startMarker.Length
                )
                .Trim();
            var lines = diagramContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            var diagram = new MermaidDiagram();
            var mermaidCodeBuilder = new StringBuilder();
            bool inMermaidCode = false;

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();

                if (trimmedLine.StartsWith("TITLE:"))
                {
                    diagram.Title = trimmedLine.Substring(6).Trim();
                }
                else if (trimmedLine.StartsWith("TYPE:"))
                {
                    diagram.Type = trimmedLine.Substring(5).Trim();
                }
                else if (trimmedLine.StartsWith("DESCRIPTION:"))
                {
                    diagram.Description = trimmedLine.Substring(12).Trim();
                }
                else if (trimmedLine == "MERMAID_CODE:")
                {
                    inMermaidCode = true;
                }
                else if (trimmedLine.StartsWith("INSIGHTS:"))
                {
                    inMermaidCode = false;
                    diagram.Insights = trimmedLine.Substring(9).Trim();
                }
                else if (inMermaidCode)
                {
                    mermaidCodeBuilder.AppendLine(line);
                }
            }

            diagram.MermaidCode = mermaidCodeBuilder.ToString().Trim();

            // Validate that we have essential components
            if (string.IsNullOrEmpty(diagram.Title) || string.IsNullOrEmpty(diagram.MermaidCode))
            {
                _logger.LogWarning("AI response missing essential diagram components");
                return null;
            }

            return diagram;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse AI Mermaid diagram response");
            return null;
        }
    }

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

}
