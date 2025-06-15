using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AnalyzeLogs.Models;
using AnalyzeLogs.Services.Data;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Logging;

namespace AnalyzeLogs.Services.Analysis;

/// <summary>
/// Implementation of the OpenAI service.
/// </summary>
public class OpenAIService : IOpenAIService
{
    private readonly ILogger<OpenAIService> _logger;
    private readonly IConfigurationService _configService;
    private readonly OpenAIClient _client;
    private readonly Dictionary<string, string> _patterns = new();
    private readonly SemaphoreSlim _rateLimiter;
    private DateTime _lastRequestTime = DateTime.MinValue;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenAIService"/> class.
    /// </summary>
    /// <param name="logger">The logger to use.</param>
    /// <param name="configService">The configuration service.</param>
    public OpenAIService(ILogger<OpenAIService> logger, IConfigurationService configService)
    {
        _logger = logger;
        _configService = configService;

        var config = _configService.GetConfiguration();

        OpenAIClientOptions clientOptions = new()
        {
            // Azure doesn't use RetryPolicy anymore, it uses a RetryOptions pattern
            // Default retry policy is used when not specified
        };

        // Set up the client with API key and optional base URL
        if (!string.IsNullOrEmpty(config.OpenAI.BaseUrl))
        {
            _client = new OpenAIClient(
                new Uri(config.OpenAI.BaseUrl),
                new Azure.AzureKeyCredential(
                    config.OpenAI.ApiKey
                        ?? throw new InvalidOperationException("OpenAI API key is required")
                ),
                clientOptions
            );
        }
        else
        {
            _client = new OpenAIClient(
                config.OpenAI.ApiKey
                    ?? throw new InvalidOperationException("OpenAI API key is required"),
                clientOptions
            );
        }

        // Set up rate limiting
        if (config.OpenAI.EnableRateLimiting)
        {
            int maxConcurrency = Math.Max(1, config.OpenAI.RequestsPerMinute / 60);
            _rateLimiter = new SemaphoreSlim(maxConcurrency);
        }
        else
        {
            _rateLimiter = new SemaphoreSlim(10); // Default concurrency if rate limiting is disabled
        }

        // Load the pattern files
        LoadPatterns();
    }

    /// <inheritdoc/>
    public async Task<string> AnalyzeAnomaliesAsync(IEnumerable<LogEntry> logEntries)
    {
        return await DetectAnomaliesAsync(logEntries);
    }

    /// <inheritdoc/>
    public async Task<string> AnalyzeCoherenceAsync(IEnumerable<LogEntry> logEntries)
    {
        var config = _configService.GetConfiguration();
        string systemPrompt = GetPatternContent("analyze_coherence");
        string userPrompt = FormatLogEntriesForPrompt(logEntries);

        var chatCompletionsOptions = new ChatCompletionsOptions
        {
            DeploymentName = config.OpenAI.GeneralModel,
            Temperature = 0.2f,
            MaxTokens = 2048,
        };

        chatCompletionsOptions.Messages.Add(new ChatRequestSystemMessage(systemPrompt));
        chatCompletionsOptions.Messages.Add(new ChatRequestUserMessage(userPrompt));

        return await SendChatCompletionAsync(chatCompletionsOptions);
    }

    /// <inheritdoc/>
    public async Task<string> TagLogsAsync(IEnumerable<LogEntry> logEntries)
    {
        var config = _configService.GetConfiguration();
        string systemPrompt = GetPatternContent("tag_logs");

        var logEntriesList = logEntries.ToList();

        // Process logs in batches to stay within token limits
        const int batchSize = 50;
        var allResults = new Dictionary<long, string[]>();

        for (int i = 0; i < logEntriesList.Count; i += batchSize)
        {
            var batch = logEntriesList.Skip(i).Take(batchSize).ToList();
            string userPrompt = FormatLogEntriesForPrompt(batch, includeIds: true);

            var chatCompletionsOptions = new ChatCompletionsOptions
            {
                DeploymentName = config.OpenAI.GeneralModel,
                Temperature = 0.2f,
                MaxTokens = 2048,
                ResponseFormat = ChatCompletionsResponseFormat.JsonObject,
            };

            chatCompletionsOptions.Messages.Add(new ChatRequestSystemMessage(systemPrompt));
            chatCompletionsOptions.Messages.Add(new ChatRequestUserMessage(userPrompt));

            string response = await SendChatCompletionAsync(chatCompletionsOptions);

            try
            {
                // Parse the JSON response to extract tags
                var batchResults = System.Text.Json.JsonSerializer.Deserialize<
                    Dictionary<string, string[]>
                >(response);
                if (batchResults != null)
                {
                    foreach (var entry in batchResults)
                    {
                        if (long.TryParse(entry.Key, out long logEntryId))
                        {
                            allResults[logEntryId] = entry.Value;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing tag response: {Response}", response);
            }
        }

        // Convert dictionary to JSON string
        return System.Text.Json.JsonSerializer.Serialize(allResults);
    }

    /// <inheritdoc/>
    public async Task<string> SummarizeLogsAsync(IEnumerable<LogEntry> logEntries)
    {
        var config = _configService.GetConfiguration();
        string systemPrompt = GetPatternContent("summarize_logs");
        string userPrompt = FormatLogEntriesForPrompt(logEntries);

        var chatCompletionsOptions = new ChatCompletionsOptions
        {
            DeploymentName = config.OpenAI.GeneralModel,
            Temperature = 0.3f,
            MaxTokens = 2048,
        };

        chatCompletionsOptions.Messages.Add(new ChatRequestSystemMessage(systemPrompt));
        chatCompletionsOptions.Messages.Add(new ChatRequestUserMessage(userPrompt));

        return await SendChatCompletionAsync(chatCompletionsOptions);
    }

    /// <inheritdoc/>
    public async Task<string> ResearchAnomalyAsync(
        LogEntry anomalyLogEntry,
        IEnumerable<LogEntry> contextLogEntries,
        string anomalyDescription
    )
    {
        var config = _configService.GetConfiguration();
        string systemPrompt = GetPatternContent("research_anomaly");

        StringBuilder userPromptBuilder = new();
        userPromptBuilder.AppendLine($"Anomaly Detected: {anomalyDescription}");
        userPromptBuilder.AppendLine($"Anomalous Log Entry: `{anomalyLogEntry.RawMessage}`");

        if (contextLogEntries.Any())
        {
            userPromptBuilder.AppendLine("Surrounding Log Context (5 mins before/after):");
            foreach (var entry in contextLogEntries.OrderBy(e => e.TimestampUTC))
            {
                userPromptBuilder.AppendLine($"[{entry.TimestampUTC}] {entry.RawMessage}");
            }
        }

        userPromptBuilder.AppendLine("\nPlease provide your analysis.");

        var chatCompletionsOptions = new ChatCompletionsOptions
        {
            DeploymentName = config.OpenAI.ResearchModel, // Use the research-enabled model
            Temperature = 0.3f,
            MaxTokens = 4096,
        };

        chatCompletionsOptions.Messages.Add(new ChatRequestSystemMessage(systemPrompt));
        chatCompletionsOptions.Messages.Add(
            new ChatRequestUserMessage(userPromptBuilder.ToString())
        );

        return await SendChatCompletionAsync(chatCompletionsOptions);
    }

    /// <inheritdoc/>
    public async Task<string> GenerateDiagramAsync(
        IEnumerable<LogEntry> logEntries,
        string diagramType
    )
    {
        var config = _configService.GetConfiguration();
        string systemPrompt = GetPatternContent("generate_diagram");

        StringBuilder userPromptBuilder = new();
        userPromptBuilder.AppendLine(
            $"Generate a {diagramType} diagram based on the following log entries."
        );
        userPromptBuilder.AppendLine("\nLog Entries:");
        userPromptBuilder.AppendLine(FormatLogEntriesForPrompt(logEntries));
        userPromptBuilder.AppendLine(
            $"\nPlease create a Mermaid {diagramType} diagram that visualizes the key events and relationships in these logs."
        );

        var chatCompletionsOptions = new ChatCompletionsOptions
        {
            DeploymentName = config.OpenAI.GeneralModel,
            Temperature = 0.3f,
            MaxTokens = 2048,
        };

        chatCompletionsOptions.Messages.Add(new ChatRequestSystemMessage(systemPrompt));
        chatCompletionsOptions.Messages.Add(
            new ChatRequestUserMessage(userPromptBuilder.ToString())
        );

        return await SendChatCompletionAsync(chatCompletionsOptions);
    }

    /// <inheritdoc/>
    public async Task<string> ParseLogAsync(string logLine, string sourcePath, string sourceFile)
    {
        var config = _configService.GetConfiguration();
        string systemPrompt = GetPatternContent("parse_log");

        string userPrompt =
            $"Parse the following log line:\n\nLog line: {logLine}\nSource path: {sourcePath}\nSource file: {sourceFile}";

        var chatCompletionsOptions = new ChatCompletionsOptions
        {
            DeploymentName = config.OpenAI.GeneralModel,
            Temperature = 0.2f,
            MaxTokens = 1024,
            ResponseFormat = ChatCompletionsResponseFormat.JsonObject,
        };

        chatCompletionsOptions.Messages.Add(new ChatRequestSystemMessage(systemPrompt));
        chatCompletionsOptions.Messages.Add(new ChatRequestUserMessage(userPrompt));

        return await SendChatCompletionAsync(chatCompletionsOptions);
    }

    /// <inheritdoc/>
    public async Task<IList<float[]>> GenerateEmbeddingsAsync(IEnumerable<string> texts)
    {
        var config = _configService.GetConfiguration();
        var result = new List<float[]>();
        var textsList = texts.ToList();

        // Process embeddings in batches
        const int batchSize = 100; // Adjust based on API limits
        for (int i = 0; i < textsList.Count; i += batchSize)
        {
            var batch = textsList.Skip(i).Take(batchSize).ToList();

            try
            {
                var embeddingsOptions = new EmbeddingsOptions(config.OpenAI.EmbeddingModel, batch);

                await _rateLimiter.WaitAsync();
                try
                {
                    ThrottleRequestsIfNeeded(config);

                    var response = await _client.GetEmbeddingsAsync(embeddingsOptions);
                    _lastRequestTime = DateTime.UtcNow;

                    foreach (var embedding in response.Value.Data)
                    {
                        result.Add(embedding.Embedding.ToArray());
                    }
                }
                finally
                {
                    _rateLimiter.Release();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error generating embeddings for batch starting at index {Index}",
                    i
                );
            }
        }

        return result;
    }

    /// <inheritdoc/>
    public async Task<IList<float[]>> GenerateEmbeddingsAsync(
        IEnumerable<LogEntry> logEntries,
        CancellationToken cancellationToken = default
    )
    {
        // Extract text to embed from each log entry
        var texts = logEntries
            .Select(entry => entry.NormalizedMessage ?? entry.RawMessage ?? string.Empty)
            .ToList();

        // Generate embeddings for the extracted texts
        return await GenerateEmbeddingsAsync(texts);
    }

    /// <inheritdoc/>
    public async Task<string> InterpretQueryAsync(
        string query,
        string? projectContext = null,
        string? sessionContext = null
    )
    {
        var config = _configService.GetConfiguration();
        string systemPrompt =
            GetPatternContent("query_intent") ?? GetDefaultPattern("query_intent");

        StringBuilder userPromptBuilder = new();
        userPromptBuilder.AppendLine(
            $"Interpret the following natural language query: \"{query}\""
        );

        if (!string.IsNullOrEmpty(projectContext))
        {
            userPromptBuilder.AppendLine($"\nProject Context:\n{projectContext}");
        }

        if (!string.IsNullOrEmpty(sessionContext))
        {
            userPromptBuilder.AppendLine($"\nSession Context:\n{sessionContext}");
        }

        var chatCompletionsOptions = new ChatCompletionsOptions
        {
            DeploymentName = config.OpenAI.GeneralModel,
            Temperature = 0.2f,
            MaxTokens = 1024,
            ResponseFormat = ChatCompletionsResponseFormat.JsonObject,
        };

        chatCompletionsOptions.Messages.Add(new ChatRequestSystemMessage(systemPrompt));
        chatCompletionsOptions.Messages.Add(
            new ChatRequestUserMessage(userPromptBuilder.ToString())
        );

        return await SendChatCompletionAsync(chatCompletionsOptions);
    }

    #region Private Helper Methods

    private async Task<string> DetectAnomaliesAsync(
        IEnumerable<LogEntry> logEntries,
        string? sessionContext = null,
        CancellationToken cancellationToken = default
    )
    {
        var config = _configService.GetConfiguration();
        string systemPrompt = GetPatternContent("analyze_log_anomalies");
        string userPrompt = FormatLogEntriesForPrompt(logEntries);

        if (!string.IsNullOrEmpty(sessionContext))
        {
            userPrompt = $"Session Context:\n{sessionContext}\n\nLog Entries:\n{userPrompt}";
        }

        var chatCompletionsOptions = new ChatCompletionsOptions
        {
            DeploymentName = config.OpenAI.GeneralModel,
            Temperature = 0.2f, // Lower temperature for more deterministic responses
            MaxTokens = 2048,
        };

        chatCompletionsOptions.Messages.Add(new ChatRequestSystemMessage(systemPrompt));
        chatCompletionsOptions.Messages.Add(new ChatRequestUserMessage(userPrompt));

        return await SendChatCompletionAsync(chatCompletionsOptions, cancellationToken);
    }

    private async Task<string> SendChatCompletionAsync(
        ChatCompletionsOptions options,
        CancellationToken cancellationToken = default
    )
    {
        var config = _configService.GetConfiguration();

        await _rateLimiter.WaitAsync(cancellationToken);
        try
        {
            ThrottleRequestsIfNeeded(config);

            var response = await _client.GetChatCompletionsAsync(options, cancellationToken);
            _lastRequestTime = DateTime.UtcNow;

            return response.Value.Choices[0].Message.Content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending chat completion request");
            return $"Error generating response: {ex.Message}";
        }
        finally
        {
            _rateLimiter.Release();
        }
    }

    private void ThrottleRequestsIfNeeded(AppConfig config)
    {
        if (config.OpenAI.EnableRateLimiting && _lastRequestTime != DateTime.MinValue)
        {
            // Calculate minimum time between requests to stay under rate limit
            double minSecondsBetweenRequests = 60.0 / config.OpenAI.RequestsPerMinute;

            var timeSinceLastRequest = (DateTime.UtcNow - _lastRequestTime).TotalSeconds;
            if (timeSinceLastRequest < minSecondsBetweenRequests)
            {
                var delayMs = (int)((minSecondsBetweenRequests - timeSinceLastRequest) * 1000);
                if (delayMs > 0)
                {
                    Thread.Sleep(delayMs);
                }
            }
        }
    }

    private void LoadPatterns()
    {
        try
        {
            // Define the pattern directories to load
            var patternDirectories = new[]
            {
                "analyze_log_anomalies",
                "analyze_coherence",
                "tag_logs",
                "summarize_logs",
                "research_anomaly",
                "generate_diagram",
                "parse_log",
                "query_intent",
            };

            string baseDirectory = Path.Combine(AppContext.BaseDirectory, "src", "Patterns");
            if (!Directory.Exists(baseDirectory))
            {
                baseDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Patterns");
                if (!Directory.Exists(baseDirectory))
                {
                    _logger.LogWarning(
                        "Pattern directory not found at {BaseDirectory}, using default patterns",
                        baseDirectory
                    );
                    InitializeDefaultPatterns();
                    return;
                }
            }

            foreach (var patternDir in patternDirectories)
            {
                string patternPath = Path.Combine(baseDirectory, patternDir, "system.md");
                if (File.Exists(patternPath))
                {
                    _patterns[patternDir] = File.ReadAllText(patternPath);
                    _logger.LogInformation(
                        "Loaded pattern {Pattern} from {Path}",
                        patternDir,
                        patternPath
                    );
                }
                else
                {
                    _logger.LogWarning(
                        "Pattern file not found at {Path}, using default pattern",
                        patternPath
                    );
                    _patterns[patternDir] = GetDefaultPattern(patternDir);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading patterns");
            InitializeDefaultPatterns();
        }
    }

    private void InitializeDefaultPatterns()
    {
        _patterns["analyze_log_anomalies"] = GetDefaultPattern("analyze_log_anomalies");
        _patterns["analyze_coherence"] = GetDefaultPattern("analyze_coherence");
        _patterns["tag_logs"] = GetDefaultPattern("tag_logs");
        _patterns["summarize_logs"] = GetDefaultPattern("summarize_logs");
        _patterns["research_anomaly"] = GetDefaultPattern("research_anomaly");
        _patterns["generate_diagram"] = GetDefaultPattern("generate_diagram");
        _patterns["parse_log"] = GetDefaultPattern("parse_log");
        _patterns["query_intent"] = GetDefaultPattern("query_intent");
    }

    private string GetPatternContent(string patternName)
    {
        return _patterns.TryGetValue(patternName, out var content)
            ? content
            : GetDefaultPattern(patternName);
    }

    private string GetDefaultPattern(string patternName)
    {
        return patternName switch
        {
            "analyze_log_anomalies" =>
                "This component is a log anomaly detector in a .NET console application. "
                    + "It examines log entries and identifies any anomalies or unusual patterns. "
                    + "For each anomaly, it explains why it's noteworthy, its potential impact, and possible causes. "
                    + "It focuses on errors, unexpected conditions, timing issues, unusual sequences, and deviations from normal behavior. "
                    + "The component presents findings in a clear, structured format with detailed explanations.",

            "analyze_coherence" =>
                "This component is a log coherence analyzer in a .NET console application. "
                    + "It checks sequences of log events for logical consistency and completeness. "
                    + "It identifies if expected events are missing (e.g., a request with no corresponding response), "
                    + "if events seem out of order, or if there are gaps in transaction flows. "
                    + "It looks for broken causal chains or timing issues and verifies if services are communicating as expected. "
                    + "The component presents findings in a structured format, highlighting any inconsistencies or confirming coherence.",

            "tag_logs" =>
                "This component is a log classification system in a .NET console application. "
                    + "It assigns relevant tags to each log entry. "
                    + "For each log entry with the provided ID, it returns an array of tags that categorize the entry. "
                    + "It considers categories like error type (e.g., 'database-error', 'network-timeout'), "
                    + "component (e.g., 'authentication', 'payment-processing'), severity ('critical', 'warning'), "
                    + "and action type ('user-login', 'data-query'). "
                    + "The component responds with a JSON object where keys are log entry IDs and values are arrays of tags.",

            "summarize_logs" =>
                "This component is a log summarization system in a .NET console application. "
                    + "It creates concise, informative summaries of provided log entries. "
                    + "It identifies key events, patterns, and significant issues, highlighting the most important information, "
                    + "including critical errors, unusual patterns, and system state changes. "
                    + "The component structures summaries into sections: Overview, Key Events, Issues Detected, and System Health Assessment.",

            "research_anomaly" =>
                "This component is an anomaly research system in a .NET console application. "
                    + "Given an anomalous log entry and surrounding log context, "
                    + "it analyzes what's happening in the log entry, "
                    + "explains potential causes of the issue based on knowledge of software systems, "
                    + "suggests remediation steps that an engineer could take to fix the problem, "
                    + "and outlines potential impacts if this issue is not addressed. "
                    + "The component structures responses as: "
                    + "1. Anomaly Analysis (what's happening) "
                    + "2. Potential Causes (why it might be happening) "
                    + "3. Suggested Remediation Steps (how to fix it) "
                    + "4. Potential Impacts (consequences if not addressed)",

            "generate_diagram" =>
                "This component is a log visualization system in a .NET console application. "
                    + "It analyzes provided log entries and creates clear, informative Mermaid diagrams "
                    + "that illustrate key events, relationships, and flows captured in the logs. "
                    + "Based on the requested diagram type (sequence, state, gantt, etc.), it generates appropriate Mermaid syntax "
                    + "that accurately represents the data. "
                    + "The component ensures diagrams are well-structured, easy to understand, "
                    + "and capture essential information from the logs.",

            "parse_log" => "This component is a log parser in a .NET console application. "
                + "It analyzes raw log lines and extracts structured information into a standardized format. "
                + "The component detects the log format (JSON, KeyValue, etc.), extracts timestamps, "
                + "identifies severity levels, extracts service names, correlation IDs, and other metadata. "
                + "It converts all data into a consistent schema that can be stored in a database. "
                + "The component returns a JSON object matching the LogEntry model structure.",

            "query_intent" =>
                "This component is a natural language query interpreter in a .NET console application. "
                    + "It analyzes user queries about log data and translates them into structured representations "
                    + "that can be used to execute database queries or other operations. "
                    + "The component identifies the intent of queries (filtering, aggregating, comparing), "
                    + "extracts parameters like time ranges, service names, and error types, "
                    + "and determines whether to use SQL, vector search, or hybrid approaches to retrieve results. "
                    + "It returns a JSON representation of the structured query intent.",

            _ => "This component is a log analysis system in a .NET console application.",
        };
    }

    private string FormatLogEntriesForPrompt(
        IEnumerable<LogEntry> logEntries,
        bool includeIds = false
    )
    {
        var builder = new StringBuilder();

        foreach (var entry in logEntries)
        {
            if (includeIds)
            {
                builder.AppendLine($"ID: {entry.LogEntryId}");
            }

            builder.AppendLine($"Timestamp: {entry.TimestampUTC}");

            if (entry.Service != null)
            {
                builder.AppendLine($"Service: {entry.Service.ServiceName}");
            }
            else if (!string.IsNullOrEmpty(entry.SourceFileName))
            {
                builder.AppendLine($"Source: {entry.SourceFileName}");
            }

            if (entry.SeverityLevel != null)
            {
                builder.AppendLine($"Level: {entry.SeverityLevel.LevelName}");
            }

            builder.AppendLine($"Message: {entry.NormalizedMessage ?? entry.RawMessage}");

            if (!string.IsNullOrEmpty(entry.CorrelationId))
            {
                builder.AppendLine($"CorrelationId: {entry.CorrelationId}");
            }

            if (!string.IsNullOrEmpty(entry.AdditionalDataJson))
            {
                builder.AppendLine($"Additional Data: {entry.AdditionalDataJson}");
            }

            builder.AppendLine(); // Empty line between entries
        }

        return builder.ToString();
    }

    #endregion
}
