using System.Text;
using System.Text.Json;
using AnalyzeLogs.Data;
using AnalyzeLogs.Models;
using AnalyzeLogs.Models.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AnalyzeLogs.Services;

/// <summary>
/// Service for handling natural language queries against log analysis data
/// </summary>
public class QueryService
{
    private readonly ILogger<QueryService> _logger;
    private readonly LogAnalysisDbContext _context;
    private readonly OpenAIService _openAIService;
    private readonly MarkdownReportService _reportService;

    public QueryService(
        ILogger<QueryService> logger,
        LogAnalysisDbContext context,
        OpenAIService openAIService,
        MarkdownReportService reportService
    )
    {
        _logger = logger;
        _context = context;
        _openAIService = openAIService;
        _reportService = reportService;
    }

    /// <summary>
    /// Process a natural language query and return structured results
    /// </summary>
    public async Task<QueryResult> ProcessQueryAsync(
        string projectName,
        string query,
        int? sessionId = null
    )
    {
        _logger.LogInformation(
            "Processing query for project '{ProjectName}': {Query}",
            projectName,
            query
        );

        try
        {
            // Get project
            var project = await _context
                .Projects.Include(p => p.Sessions)
                .FirstOrDefaultAsync(p => p.Name == projectName);

            if (project == null)
            {
                throw new InvalidOperationException($"Project '{projectName}' not found.");
            }

            // Analyze the query to determine intent and required data
            var queryAnalysis = await AnalyzeQueryIntentAsync(query, project, sessionId);

            // Execute the appropriate data retrieval based on intent
            var data = await ExecuteDataRetrievalAsync(queryAnalysis, project, sessionId);

            // Generate response based on the data
            var response = await GenerateQueryResponseAsync(query, queryAnalysis, data);

            return new QueryResult
            {
                Query = query,
                ProjectName = projectName,
                SessionId = sessionId,
                Intent = queryAnalysis.Intent,
                DataType = queryAnalysis.DataType,
                Response = response,
                Data = data,
                ExecutedAt = DateTime.UtcNow,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing query: {Query}", query);
            throw;
        }
    }

    /// <summary>
    /// Analyze the natural language query to determine intent and data requirements
    /// </summary>
    private async Task<QueryAnalysis> AnalyzeQueryIntentAsync(
        string query,
        Project project,
        int? sessionId
    )
    {
        var prompt = BuildQueryAnalysisPrompt(query, project, sessionId);
        var content = await _openAIService.CallPatternAsync("query_analysis", prompt);

        if (string.IsNullOrEmpty(content))
        {
            _logger.LogWarning("AI service returned null or empty response for query analysis");
            return FallbackQueryAnalysis(query);
        }

        try
        {
            var analysis = JsonSerializer.Deserialize<QueryAnalysis>(content);
            return analysis
                ?? throw new InvalidOperationException("Failed to parse query analysis");
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to parse AI response as JSON, falling back to text analysis"
            );
            return FallbackQueryAnalysis(query);
        }
    }

    /// <summary>
    /// Execute data retrieval based on the query analysis
    /// </summary>
    private async Task<QueryData> ExecuteDataRetrievalAsync(
        QueryAnalysis analysis,
        Project project,
        int? sessionId
    )
    {
        var data = new QueryData();

        switch (analysis.DataType)
        {
            case QueryDataType.LogEntries:
                data.LogEntries = await GetLogEntriesAsync(project, sessionId, analysis);
                break;

            case QueryDataType.Anomalies:
                data.Anomalies = await GetAnomaliesAsync(project, sessionId, analysis);
                break;

            case QueryDataType.ServiceMetrics:
                data.ServiceMetrics = await GetServiceMetricsAsync(project, sessionId, analysis);
                break;

            case QueryDataType.Correlations:
                data.Correlations = await GetCorrelationsAsync(project, sessionId, analysis);
                break;

            case QueryDataType.Sessions:
                data.Sessions = await GetSessionsAsync(project, analysis);
                break;

            case QueryDataType.Combined:
                // Retrieve multiple types of data
                data.LogEntries = await GetLogEntriesAsync(project, sessionId, analysis);
                data.Anomalies = await GetAnomaliesAsync(project, sessionId, analysis);
                data.ServiceMetrics = await GetServiceMetricsAsync(project, sessionId, analysis);
                data.Correlations = await GetCorrelationsAsync(project, sessionId, analysis);
                break;
        }

        return data;
    }

    /// <summary>
    /// Generate a natural language response based on the query and retrieved data
    /// </summary>
    private async Task<string> GenerateQueryResponseAsync(
        string originalQuery,
        QueryAnalysis analysis,
        QueryData data
    )
    {
        var prompt = BuildResponseGenerationPrompt(originalQuery, analysis, data);

        return await _openAIService.CallPatternAsync("query_response", prompt)
            ?? "I'm sorry, I couldn't generate a response for your query.";
    }

    /// <summary>
    /// Retrieve log entries based on query analysis
    /// </summary>
    private async Task<List<StoredLogEntry>> GetLogEntriesAsync(
        Project project,
        int? sessionId,
        QueryAnalysis analysis
    )
    {
        var query = _context.LogEntries.Where(le => le.ProjectId == project.Id);

        if (sessionId.HasValue)
        {
            query = query.Where(le => le.SessionId == sessionId.Value);
        }

        // Apply filters based on analysis
        if (analysis.TimeRange?.StartTime.HasValue == true)
        {
            query = query.Where(le => le.Timestamp >= analysis.TimeRange.StartTime.Value);
        }

        if (analysis.TimeRange?.EndTime.HasValue == true)
        {
            query = query.Where(le => le.Timestamp <= analysis.TimeRange.EndTime.Value);
        }

        if (!string.IsNullOrEmpty(analysis.ServiceFilter))
        {
            query = query.Where(le => le.Service == analysis.ServiceFilter);
        }

        if (analysis.LogLevel.HasValue)
        {
            query = query.Where(le => le.Level >= (int)analysis.LogLevel.Value);
        }

        if (analysis.AnomaliesOnly)
        {
            query = query.Where(le => le.IsAnomaly);
        }

        // Apply ordering and limits
        query = query.OrderByDescending(le => le.Timestamp);

        if (analysis.Limit > 0)
        {
            query = query.Take(analysis.Limit);
        }

        return await query.ToListAsync();
    }

    /// <summary>
    /// Retrieve anomalies based on query analysis
    /// </summary>
    private async Task<List<StoredAnomaly>> GetAnomaliesAsync(
        Project project,
        int? sessionId,
        QueryAnalysis analysis
    )
    {
        var query = _context.Anomalies.Where(a => a.ProjectId == project.Id);

        if (sessionId.HasValue)
        {
            query = query.Where(a => a.SessionId == sessionId.Value);
        }

        if (analysis.TimeRange?.StartTime.HasValue == true)
        {
            query = query.Where(a => a.DetectedAt >= analysis.TimeRange.StartTime.Value);
        }

        if (analysis.TimeRange?.EndTime.HasValue == true)
        {
            query = query.Where(a => a.DetectedAt <= analysis.TimeRange.EndTime.Value);
        }
        if (!string.IsNullOrEmpty(analysis.ServiceFilter))
        {
            query = query.Where(a => a.Service == analysis.ServiceFilter);
        }

        if (analysis.MinConfidence > 0)
        {
            query = query.Where(a => a.Confidence >= analysis.MinConfidence);
        }

        return await query.OrderByDescending(a => a.Confidence).ToListAsync();
    }

    /// <summary>
    /// Retrieve service metrics based on query analysis
    /// </summary>
    private async Task<List<StoredServiceMetrics>> GetServiceMetricsAsync(
        Project project,
        int? sessionId,
        QueryAnalysis analysis
    )
    {
        var query = _context.ServiceMetrics.Where(sm => sm.ProjectId == project.Id);

        if (sessionId.HasValue)
        {
            query = query.Where(sm => sm.SessionId == sessionId.Value);
        }

        if (!string.IsNullOrEmpty(analysis.ServiceFilter))
        {
            query = query.Where(sm => sm.ServiceName == analysis.ServiceFilter);
        }

        return await query.OrderBy(sm => sm.ServiceName).ToListAsync();
    }

    /// <summary>
    /// Retrieve correlations based on query analysis
    /// </summary>
    private async Task<List<StoredCorrelation>> GetCorrelationsAsync(
        Project project,
        int? sessionId,
        QueryAnalysis analysis
    )
    {
        var query = _context.Correlations.Where(c => c.ProjectId == project.Id);

        if (sessionId.HasValue)
        {
            query = query.Where(c => c.SessionId == sessionId.Value);
        } // Remove confidence filtering since StoredCorrelation doesn't have Confidence property
        // if (analysis.MinConfidence > 0)
        // {
        //     query = query.Where(c => c.Confidence >= analysis.MinConfidence);
        // }

        return await query.OrderByDescending(c => c.StartTime).ToListAsync();
    }

    /// <summary>
    /// Retrieve sessions based on query analysis
    /// </summary>
    private async Task<List<LogAnalysisSession>> GetSessionsAsync(
        Project project,
        QueryAnalysis analysis
    )
    {
        var query = _context.Sessions.Where(s => s.ProjectId == project.Id);

        if (analysis.TimeRange?.StartTime.HasValue == true)
        {
            query = query.Where(s => s.StartTime >= analysis.TimeRange.StartTime.Value);
        }

        if (analysis.TimeRange?.EndTime.HasValue == true)
        {
            query = query.Where(s => s.StartTime <= analysis.TimeRange.EndTime.Value);
        }

        return await query.OrderByDescending(s => s.StartTime).ToListAsync();
    }

    /// <summary>
    /// Build the prompt for query analysis
    /// </summary>
    private string BuildQueryAnalysisPrompt(string query, Project project, int? sessionId)
    {
        var prompt = new StringBuilder();
        prompt.AppendLine($"**User Query:** {query}");
        prompt.AppendLine($"**Project:** {project.Name}");

        if (!string.IsNullOrEmpty(project.Description))
        {
            prompt.AppendLine($"**Project Description:** {project.Description}");
        }

        if (sessionId.HasValue)
        {
            prompt.AppendLine($"**Session ID:** {sessionId}");
        }

        prompt.AppendLine($"**Available Sessions:** {project.Sessions.Count}");
        prompt.AppendLine(
            $"**Last Analyzed:** {project.LastAnalyzedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "Never"}"
        );

        return prompt.ToString();
    }

    /// <summary>
    /// Build the prompt for response generation
    /// </summary>
    private string BuildResponseGenerationPrompt(
        string originalQuery,
        QueryAnalysis analysis,
        QueryData data
    )
    {
        var prompt = new StringBuilder();
        prompt.AppendLine($"**Original Query:** {originalQuery}");
        prompt.AppendLine($"**Query Intent:** {analysis.Intent}");
        prompt.AppendLine($"**Data Type:** {analysis.DataType}");
        prompt.AppendLine();

        // Add data summaries
        if (data.LogEntries?.Any() == true)
        {
            prompt.AppendLine($"**Log Entries Found:** {data.LogEntries.Count}");
            var services = data
                .LogEntries.Select(le => le.Service)
                .Distinct()
                .Where(s => !string.IsNullOrEmpty(s));
            if (services.Any())
            {
                prompt.AppendLine($"**Services:** {string.Join(", ", services)}");
            }
        }

        if (data.Anomalies?.Any() == true)
        {
            prompt.AppendLine($"**Anomalies Found:** {data.Anomalies.Count}");
            var highConfidenceAnomalies = data.Anomalies.Count(a => a.Confidence > 0.7);
            if (highConfidenceAnomalies > 0)
            {
                prompt.AppendLine($"**High Confidence Anomalies:** {highConfidenceAnomalies}");
            }
        }

        if (data.ServiceMetrics?.Any() == true)
        {
            prompt.AppendLine($"**Services with Metrics:** {data.ServiceMetrics.Count}");
        }

        if (data.Correlations?.Any() == true)
        {
            prompt.AppendLine($"**Correlations Found:** {data.Correlations.Count}");
        }

        if (data.Sessions?.Any() == true)
        {
            prompt.AppendLine($"**Sessions:** {data.Sessions.Count}");
        }

        return prompt.ToString();
    }

    /// <summary>
    /// Load the system prompt for query analysis
    /// </summary>
    private async Task<string> LoadQueryAnalysisSystemPromptAsync()
    {
        var promptPath = Path.Combine("patterns", "query_analysis", "system.md");
        if (File.Exists(promptPath))
        {
            return await File.ReadAllTextAsync(promptPath);
        }

        // Fallback inline prompt
        return @"You are an expert log analysis assistant that helps users query their log data.

Analyze the user's natural language query and return a JSON response with the following structure:
{
  ""intent"": ""string - The main intent (e.g., 'find_errors', 'analyze_performance', 'detect_anomalies', 'service_health', 'time_analysis')"",
  ""dataType"": ""string - Primary data type needed (LogEntries, Anomalies, ServiceMetrics, Correlations, Sessions, Combined)"",
  ""timeRange"": {
    ""startTime"": ""ISO date string or null"",
    ""endTime"": ""ISO date string or null""
  },
  ""serviceFilter"": ""string or null - specific service name if mentioned"",
  ""logLevel"": ""string or null - minimum log level (Debug, Info, Warning, Error, Critical)"",
  ""anomaliesOnly"": ""boolean - if query is specifically about anomalies"",
  ""minConfidence"": ""number - minimum confidence threshold (0.0-1.0)"",
  ""limit"": ""number - maximum results to return (default 100)""
}

Consider time references like 'last hour', 'yesterday', 'this week' and convert them to appropriate timestamps.
Consider severity keywords like 'critical', 'errors', 'warnings' to set appropriate log levels.
Consider confidence keywords like 'high confidence', 'likely', 'certain' to set confidence thresholds.";
    }

    /// <summary>
    /// Load the system prompt for response generation
    /// </summary>
    private async Task<string> LoadResponseGenerationSystemPromptAsync()
    {
        var promptPath = Path.Combine("patterns", "query_response", "system.md");
        if (File.Exists(promptPath))
        {
            return await File.ReadAllTextAsync(promptPath);
        }

        // Fallback inline prompt
        return @"You are an expert log analysis assistant that provides clear, actionable insights based on log data queries.

Generate a natural language response to the user's query based on the provided data. Your response should:

1. **Directly answer the user's question** with specific findings
2. **Provide quantitative insights** with numbers and percentages
3. **Highlight important patterns** or anomalies discovered
4. **Use clear formatting** with bullet points and sections when helpful
5. **Suggest actionable next steps** when appropriate
6. **Be concise but comprehensive** - focus on the most relevant information

When discussing anomalies, always mention confidence levels and potential impact.
When discussing performance, include specific metrics and comparisons.
When discussing errors, categorize by severity and frequency.
When no significant findings are available, clearly state this and suggest alternative queries.

Format your response in markdown for better readability.";
    }

    /// <summary>
    /// Fallback query analysis when AI parsing fails
    /// </summary>
    private QueryAnalysis FallbackQueryAnalysis(string query)
    {
        var analysis = new QueryAnalysis
        {
            Intent = "general_analysis",
            DataType = QueryDataType.Combined,
            Limit = 100,
        };

        var lowerQuery = query.ToLowerInvariant();

        // Simple keyword-based analysis
        if (lowerQuery.Contains("error") || lowerQuery.Contains("fail"))
        {
            analysis.Intent = "find_errors";
            analysis.LogLevel = Models.LogLevel.Error;
        }
        else if (lowerQuery.Contains("anomal") || lowerQuery.Contains("unusual"))
        {
            analysis.Intent = "detect_anomalies";
            analysis.DataType = QueryDataType.Anomalies;
            analysis.AnomaliesOnly = true;
        }
        else if (lowerQuery.Contains("performance") || lowerQuery.Contains("slow"))
        {
            analysis.Intent = "analyze_performance";
            analysis.DataType = QueryDataType.ServiceMetrics;
        }

        // Simple time analysis
        if (lowerQuery.Contains("today"))
        {
            analysis.TimeRange = new QueryTimeRange
            {
                StartTime = DateTime.Today,
                EndTime = DateTime.Now,
            };
        }
        else if (lowerQuery.Contains("yesterday"))
        {
            analysis.TimeRange = new QueryTimeRange
            {
                StartTime = DateTime.Today.AddDays(-1),
                EndTime = DateTime.Today,
            };
        }

        return analysis;
    }
}

/// <summary>
/// Result of a natural language query
/// </summary>
public class QueryResult
{
    public string Query { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public int? SessionId { get; set; }
    public string Intent { get; set; } = string.Empty;
    public QueryDataType DataType { get; set; }
    public string Response { get; set; } = string.Empty;
    public QueryData Data { get; set; } = new();
    public DateTime ExecutedAt { get; set; }
}

/// <summary>
/// Analysis of a natural language query
/// </summary>
public class QueryAnalysis
{
    public string Intent { get; set; } = string.Empty;
    public QueryDataType DataType { get; set; }
    public QueryTimeRange? TimeRange { get; set; }
    public string? ServiceFilter { get; set; }
    public Models.LogLevel? LogLevel { get; set; }
    public bool AnomaliesOnly { get; set; }
    public double MinConfidence { get; set; }
    public int Limit { get; set; } = 100;
}

/// <summary>
/// Time range for queries
/// </summary>
public class QueryTimeRange
{
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
}

/// <summary>
/// Data retrieved for a query
/// </summary>
public class QueryData
{
    public List<StoredLogEntry>? LogEntries { get; set; }
    public List<StoredAnomaly>? Anomalies { get; set; }
    public List<StoredServiceMetrics>? ServiceMetrics { get; set; }
    public List<StoredCorrelation>? Correlations { get; set; }
    public List<LogAnalysisSession>? Sessions { get; set; }
}

/// <summary>
/// Types of data that can be queried
/// </summary>
public enum QueryDataType
{
    LogEntries,
    Anomalies,
    ServiceMetrics,
    Correlations,
    Sessions,
    Combined,
}
