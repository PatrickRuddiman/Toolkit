using System.Text;
using AnalyzeLogs.Data;
using AnalyzeLogs.Models;
using AnalyzeLogs.Services.Analysis;
using AnalyzeLogs.Services.Data;
using AnalyzeLogs.Services.Query;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AnalyzeLogs.Services.Reporting;

/// <summary>
/// Implementation of the report service for generating rich markdown reports.
/// </summary>
public class ReportService : IReportService
{
    private readonly LogAnalyzerDbContext _dbContext;
    private readonly IConfigurationService _configService;
    private readonly IOpenAIService _openAIService;
    private readonly ILogger<ReportService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportService"/> class.
    /// </summary>
    public ReportService(
        LogAnalyzerDbContext dbContext,
        IConfigurationService configService,
        IOpenAIService openAIService,
        ILogger<ReportService> logger
    )
    {
        _dbContext = dbContext;
        _configService = configService;
        _openAIService = openAIService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<string> GenerateReportAsync(
        Guid sessionId,
        string? outputPath = null,
        string format = "docfx",
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogInformation("Generating report for session {SessionId}", sessionId);

        // Get the session
        var session = await _dbContext
            .Sessions.Include(s => s.Project)
            .Include(s => s.LogEntries)
            .ThenInclude(l => l.Service)
            .Include(s => s.LogEntries)
            .ThenInclude(l => l.SeverityLevel)
            .Include(s => s.Anomalies)
            .FirstOrDefaultAsync(s => s.SessionId == sessionId, cancellationToken);

        if (session == null)
        {
            throw new ArgumentException(
                $"Session with ID {sessionId} not found",
                nameof(sessionId)
            );
        }

        // Determine the output path
        var config = _configService.GetConfiguration();
        string finalOutputPath =
            outputPath
            ?? Path.Combine(
                config.DefaultReportOutputPath,
                $"report_{session.Project.Name}_{session.SessionId:N}.md"
            );

        // Ensure the directory exists
        string? directory = Path.GetDirectoryName(finalOutputPath);
        if (directory != null && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Generate the report content
        var reportContent = new StringBuilder();

        // Add DocFX metadata if requested
        if (format.Equals("docfx", StringComparison.OrdinalIgnoreCase))
        {
            reportContent.AppendLine("---");
            reportContent.AppendLine($"title: Log Analysis Report - {session.Project.Name}");
            reportContent.AppendLine(
                $"description: Comprehensive analysis report for project {session.Project.Name}"
            );
            reportContent.AppendLine("author: AI Log Analysis Tool");
            reportContent.AppendLine($"ms.date: {DateTime.UtcNow:yyyy-MM-dd}");
            reportContent.AppendLine("ms.topic: analysis-report");
            reportContent.AppendLine("ms.service: log-analysis");
            reportContent.AppendLine("---");
            reportContent.AppendLine();
        }

        // Add report header and content
        reportContent.AppendLine($"# Log Analysis Report: {session.Project.Name}");
        reportContent.AppendLine();
        reportContent.AppendLine($"**Session ID**: {session.SessionId}");
        reportContent.AppendLine($"**Generated**: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        reportContent.AppendLine(
            $"**Analysis Period**: {session.StartTime:yyyy-MM-dd HH:mm:ss} to {(session.EndTime.HasValue ? session.EndTime.Value.ToString("yyyy-MM-dd HH:mm:ss") : "Present")}"
        );
        reportContent.AppendLine();

        // Write the report to the file
        await File.WriteAllTextAsync(finalOutputPath, reportContent.ToString(), cancellationToken);

        _logger.LogInformation("Report generated successfully at {ReportPath}", finalOutputPath);

        return finalOutputPath;
    }

    /// <inheritdoc/>
    public async Task<string> GenerateAnomalyReportAsync(
        Guid anomalyId,
        string? outputPath = null,
        string format = "docfx",
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogInformation("Generating report for anomaly {AnomalyId}", anomalyId);

        // Convert the GUID to an int for comparison with AnomalyId
        // This is a temporary solution - in a real implementation we'd need to ensure IDs are consistent
        int anomalyIdInt;
        if (
            !int.TryParse(
                anomalyId.ToString().Substring(0, 8),
                System.Globalization.NumberStyles.HexNumber,
                null,
                out anomalyIdInt
            )
        )
        {
            anomalyIdInt = anomalyId.GetHashCode();
        }

        // Get the anomaly
        var anomaly = await _dbContext
            .Anomalies.Include(a => a.Session)
            .ThenInclude(s => s.Project)
            .Include(a => a.AnomalyLogEntries)
            .ThenInclude(ale => ale.LogEntry)
            .ThenInclude(l => l.Service)
            .Include(a => a.AnomalyLogEntries)
            .ThenInclude(ale => ale.LogEntry)
            .ThenInclude(l => l.SeverityLevel)
            .FirstOrDefaultAsync(a => a.AnomalyId == anomalyIdInt, cancellationToken);

        if (anomaly == null)
        {
            throw new ArgumentException(
                $"Anomaly with ID {anomalyId} not found",
                nameof(anomalyId)
            );
        }

        // Determine the output path
        var config = _configService.GetConfiguration();
        string finalOutputPath =
            outputPath
            ?? Path.Combine(
                config.DefaultReportOutputPath,
                $"anomaly_{anomaly.Session.Project.Name}_{anomaly.AnomalyId:N}.md"
            );

        // Ensure the directory exists
        string? directory = Path.GetDirectoryName(finalOutputPath);
        if (directory != null && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Generate the report content
        var reportContent = new StringBuilder();

        // Add DocFX metadata if requested
        if (format.Equals("docfx", StringComparison.OrdinalIgnoreCase))
        {
            reportContent.AppendLine("---");
            reportContent.AppendLine($"title: Anomaly Analysis - {anomaly.AnomalyType}");
            reportContent.AppendLine(
                $"description: Detailed analysis of anomaly '{anomaly.AnomalyType}' in project {anomaly.Session.Project.Name}"
            );
            reportContent.AppendLine("author: AI Log Analysis Tool");
            reportContent.AppendLine($"ms.date: {DateTime.UtcNow:yyyy-MM-dd}");
            reportContent.AppendLine("ms.topic: anomaly-analysis");
            reportContent.AppendLine("ms.service: log-analysis");
            reportContent.AppendLine("---");
            reportContent.AppendLine();
        }

        // Add report header
        reportContent.AppendLine($"# Anomaly Analysis: {anomaly.AnomalyType}");
        reportContent.AppendLine();
        reportContent.AppendLine($"**Anomaly ID**: {anomaly.AnomalyId}");
        reportContent.AppendLine($"**Project**: {anomaly.Session.Project.Name}");
        reportContent.AppendLine($"**Session ID**: {anomaly.Session.SessionId}");
        reportContent.AppendLine(
            $"**Detected At**: {anomaly.DetectionTime:yyyy-MM-dd HH:mm:ss} UTC"
        );
        reportContent.AppendLine($"**Severity**: {anomaly.Severity}");
        reportContent.AppendLine();

        // Write the report to the file
        await File.WriteAllTextAsync(finalOutputPath, reportContent.ToString(), cancellationToken);

        // We don't need to update the anomaly record with the report path as there's no ReportPath property
        // Just log that the report was generated
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Anomaly report generated successfully at {ReportPath}",
            finalOutputPath
        );

        return finalOutputPath;
    }

    /// <inheritdoc/>
    public async Task<string> AppendToQueryReportAsync(
        Guid sessionId,
        Guid conversationId,
        string query,
        QueryResult result,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogInformation(
            "Appending to query report for session {SessionId}, conversation {ConversationId}",
            sessionId,
            conversationId
        );

        // Get the session
        var session = await _dbContext
            .Sessions.Include(s => s.Project)
            .FirstOrDefaultAsync(s => s.SessionId == sessionId, cancellationToken);

        if (session == null)
        {
            throw new ArgumentException(
                $"Session with ID {sessionId} not found",
                nameof(sessionId)
            );
        }

        // Determine the output path
        var config = _configService.GetConfiguration();
        string reportPath = Path.Combine(
            config.DefaultReportOutputPath,
            $"query_{session.Project.Name}_{sessionId:N}_{conversationId:N}.md"
        );

        // Ensure the directory exists
        string? directory = Path.GetDirectoryName(reportPath);
        if (directory != null && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Check if the file already exists
        bool isNewFile = !File.Exists(reportPath);

        var reportContent = new StringBuilder();

        // If it's a new file, add the header
        if (isNewFile)
        {
            // Add DocFX metadata
            reportContent.AppendLine("---");
            reportContent.AppendLine($"title: Query Log - {session.Project.Name}");
            reportContent.AppendLine("---");
            reportContent.AppendLine();

            reportContent.AppendLine($"# Query Log: {session.Project.Name}");
            reportContent.AppendLine();
        }

        // Add the query and result
        reportContent.AppendLine($"## Query: {query}");
        reportContent.AppendLine();
        reportContent.AppendLine($"*{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC*");
        reportContent.AppendLine();
        reportContent.AppendLine("### Response");
        reportContent.AppendLine();
        reportContent.AppendLine(result.TextResponse);
        reportContent.AppendLine();

        // Write to the file (append if it already exists)
        if (isNewFile)
        {
            await File.WriteAllTextAsync(reportPath, reportContent.ToString(), cancellationToken);
        }
        else
        {
            await File.AppendAllTextAsync(reportPath, reportContent.ToString(), cancellationToken);
        }

        _logger.LogInformation("Query appended to report at {ReportPath}", reportPath);

        return reportPath;
    }

    /// <inheritdoc/>
    public async Task<string> GenerateDataValidationFileAsync(
        Guid sessionId,
        string chartId,
        string chartTitle,
        object data,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogInformation(
            "Generating data validation file for chart {ChartId} in session {SessionId}",
            chartId,
            sessionId
        );

        // Get the session
        var session = await _dbContext
            .Sessions.Include(s => s.Project)
            .FirstOrDefaultAsync(s => s.SessionId == sessionId, cancellationToken);

        if (session == null)
        {
            throw new ArgumentException(
                $"Session with ID {sessionId} not found",
                nameof(sessionId)
            );
        }

        // Determine the output path
        var config = _configService.GetConfiguration();
        string outputPath = Path.Combine(
            config.DefaultReportOutputPath,
            $"data_{session.Project.Name}_{sessionId:N}_{chartId}.md"
        );

        // Ensure the directory exists
        string? directory = Path.GetDirectoryName(outputPath);
        if (directory != null && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Generate the content
        var content = new StringBuilder();

        // Add DocFX metadata and content
        content.AppendLine("---");
        content.AppendLine($"title: Data Validation - {chartTitle}");
        content.AppendLine("---");
        content.AppendLine();
        content.AppendLine($"# Data Validation: {chartTitle}");
        content.AppendLine();

        try
        {
            // Add table content based on data type
            if (data is System.Collections.IEnumerable enumerable && !(data is string))
            {
                RenderCollectionData(content, enumerable);
            }
            else if (data != null)
            {
                RenderObjectData(content, data);
            }
            else
            {
                content.AppendLine("*No data*");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error converting data to markdown for chart {ChartId}", chartId);
            content.AppendLine("*Error converting data to markdown*");
        }

        // Write the content to the file
        await File.WriteAllTextAsync(outputPath, content.ToString(), cancellationToken);

        _logger.LogInformation("Data validation file generated at {FilePath}", outputPath);

        return outputPath;
    }

    private void RenderCollectionData(StringBuilder content, System.Collections.IEnumerable data)
    {
        // Extract the first item to determine properties
        var enumerator = data.GetEnumerator();
        if (enumerator.MoveNext())
        {
            var firstItem = enumerator.Current;
            var properties = firstItem.GetType().GetProperties();

            if (properties.Length > 0)
            {
                // Add table header
                content.Append("| ");
                foreach (var prop in properties)
                {
                    content.Append($"{prop.Name} | ");
                }
                content.AppendLine();

                // Add separator
                content.Append("| ");
                foreach (var _ in properties)
                {
                    content.Append("--- | ");
                }
                content.AppendLine();

                // Add rows
                foreach (var item in data)
                {
                    content.Append("| ");
                    foreach (var prop in properties)
                    {
                        var value = prop.GetValue(item)?.ToString() ?? "null";
                        content.Append($"{value} | ");
                    }
                    content.AppendLine();
                }
            }
            else
            {
                // Simple values
                content.AppendLine("| Value |");
                content.AppendLine("| ----- |");

                // Reset enumerator
                enumerator = data.GetEnumerator();
                while (enumerator.MoveNext())
                {
                    content.AppendLine($"| {enumerator.Current} |");
                }
            }
        }
        else
        {
            content.AppendLine("*Empty collection*");
        }
    }

    private void RenderObjectData(StringBuilder content, object data)
    {
        // Render object properties as a table
        var properties = data.GetType().GetProperties();

        if (properties.Length > 0)
        {
            content.AppendLine("| Property | Value |");
            content.AppendLine("|----------|-------|");

            foreach (var prop in properties)
            {
                var value = prop.GetValue(data)?.ToString() ?? "null";
                content.AppendLine($"| {prop.Name} | {value} |");
            }
        }
        else
        {
            content.AppendLine($"*Object of type {data.GetType().Name} has no properties*");
        }
    }
}
