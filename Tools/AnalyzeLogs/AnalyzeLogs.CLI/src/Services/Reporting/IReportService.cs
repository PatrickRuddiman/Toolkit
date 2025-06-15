using AnalyzeLogs.Models;
using AnalyzeLogs.Services.Query;

namespace AnalyzeLogs.Services.Reporting;

/// <summary>
/// Service for generating reports from analysis results.
/// </summary>
public interface IReportService
{
    /// <summary>
    /// Generates a complete report for a session.
    /// </summary>
    /// <param name="sessionId">The ID of the session to generate a report for.</param>
    /// <param name="outputPath">The path where the report should be saved. If null, the default location is used.</param>
    /// <param name="format">The format of the report (docfx or markdown).</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The path to the generated report file.</returns>
    Task<string> GenerateReportAsync(
        Guid sessionId,
        string? outputPath = null,
        string format = "docfx",
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Generates a report for a specific anomaly.
    /// </summary>
    /// <param name="anomalyId">The ID of the anomaly to generate a report for.</param>
    /// <param name="outputPath">The path where the report should be saved. If null, the default location is used.</param>
    /// <param name="format">The format of the report (docfx or markdown).</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The path to the generated report file.</returns>
    Task<string> GenerateAnomalyReportAsync(
        Guid anomalyId,
        string? outputPath = null,
        string format = "docfx",
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Appends a query and its result to an existing query report.
    /// </summary>
    /// <param name="sessionId">The ID of the session the query was executed against.</param>
    /// <param name="conversationId">The ID of the conversation to append to.</param>
    /// <param name="query">The query that was executed.</param>
    /// <param name="result">The result of the query.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The path to the updated report file.</returns>
    Task<string> AppendToQueryReportAsync(
        Guid sessionId,
        Guid conversationId,
        string query,
        QueryResult result,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Generates a data validation file for a chart or visualization.
    /// </summary>
    /// <param name="sessionId">The ID of the session the data is from.</param>
    /// <param name="chartId">The ID of the chart or visualization.</param>
    /// <param name="chartTitle">The title of the chart or visualization.</param>
    /// <param name="data">The data used to generate the chart or visualization.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The path to the generated data validation file.</returns>
    Task<string> GenerateDataValidationFileAsync(
        Guid sessionId,
        string chartId,
        string chartTitle,
        object data,
        CancellationToken cancellationToken = default
    );
}
