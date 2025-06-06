using System.Text;
using System.Text.Json;
using AnalyzeLogs.Models;
using Microsoft.Extensions.Logging;

namespace AnalyzeLogs.Services;

/// <summary>
/// Service for interacting with OpenAI API for various analysis tasks
/// </summary>
public class OpenAIService : IDisposable
{
    private readonly ILogger<OpenAIService> _logger;
    private readonly HttpClient _httpClient;
    private readonly ConfigurationService _configService;
    private string? _apiKey;

    public OpenAIService(ILogger<OpenAIService> logger, ConfigurationService configService)
    {
        _logger = logger;
        _configService = configService;
        _httpClient = new HttpClient();
    }

    private async Task<string?> GetApiKeyAsync()
    {
        if (_apiKey != null)
            return _apiKey;

        // Try environment variable first
        _apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");

        if (string.IsNullOrEmpty(_apiKey))
        {
            // Try configuration file
            _apiKey = await _configService.GetApiKeyAsync();
        }

        if (!string.IsNullOrEmpty(_apiKey))
        {
            _httpClient.DefaultRequestHeaders.Remove("Authorization");
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
        }

        return _apiKey;
    }

    public async Task<bool> HasApiKeyConfiguredAsync()
    {
        var apiKey = await GetApiKeyAsync();
        return !string.IsNullOrWhiteSpace(apiKey);
    }

    /// <summary>
    /// Analyzes logs for anomalies using OpenAI
    /// </summary>
    public async Task<List<Anomaly>> DetectAnomaliesAsync(LogChunk chunk, Configuration config)
    {
        var anomalies = new List<Anomaly>();

        try
        {
            var systemPrompt = await LoadPatternSystemPromptAsync("analyze_log_anomalies");
            var prompt = BuildAnomalyDetectionPrompt(chunk);

            var response = await CallOpenAIAsync(systemPrompt, prompt, config);

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
    /// Performs coherence analysis on logs using OpenAI
    /// </summary>
    public async Task<List<Anomaly>> AnalyzeCoherenceAsync(LogChunk chunk, Configuration config)
    {
        var coherenceIssues = new List<Anomaly>();

        try
        {
            var systemPrompt = await LoadPatternSystemPromptAsync("analyze_coherence");
            var prompt = BuildCoherenceAnalysisPrompt(chunk);

            var response = await CallOpenAIAsync(systemPrompt, prompt, config);

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
    /// Tags log entries using OpenAI
    /// </summary>
    public async Task TagLogEntriesAsync(LogChunk chunk, Configuration config)
    {
        try
        {
            var systemPrompt = await LoadPatternSystemPromptAsync("tag_logs");
            var prompt = BuildTaggingPrompt(chunk);

            var response = await CallOpenAIAsync(systemPrompt, prompt, config);

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
    /// Generates summary and recommendations using OpenAI
    /// </summary>
    public async Task<string> GenerateSummaryAsync(
        List<ServiceMetrics> metrics,
        List<Anomaly> anomalies,
        Configuration config
    )
    {
        try
        {
            var systemPrompt = await LoadPatternSystemPromptAsync("summarize_logs");
            var prompt = BuildSummaryPrompt(metrics, anomalies);

            var response = await CallOpenAIAsync(systemPrompt, prompt, config);
            return response ?? "Failed to generate summary.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating summary");
            return "Error generating summary: " + ex.Message;
        }
    }

    /// <summary>
    /// Generates embeddings for log entries using OpenAI
    /// </summary>
    public async Task<float[][]> GetEmbeddingsAsync(
        string[] texts,
        string model = "text-embedding-3-small"
    )
    {
        var apiKey = await GetApiKeyAsync();
        if (string.IsNullOrEmpty(apiKey))
        {
            throw new InvalidOperationException("OpenAI API key not configured");
        }

        var request = new { model = model, input = texts };

        var content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json"
        );        var response = await _httpClient.PostAsync("https://api.openai.com/v1/embeddings", content);
        
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError(
                "OpenAI embeddings API request failed with status {StatusCode} ({ReasonPhrase}). Response: {ErrorContent}",
                response.StatusCode,
                response.ReasonPhrase,
                errorContent
            );
            response.EnsureSuccessStatusCode();
        }

        var responseContent = await response.Content.ReadAsStringAsync();
        var document = JsonDocument.Parse(responseContent);

        var embeddings = new List<float[]>();

        if (document.RootElement.TryGetProperty("data", out var dataArray))
        {
            for (int i = 0; i < dataArray.GetArrayLength(); i++)
            {
                var embeddingElement = dataArray[i];
                if (embeddingElement.TryGetProperty("embedding", out var embeddingArray))
                {
                    var embedding = new float[embeddingArray.GetArrayLength()];
                    for (int j = 0; j < embeddingArray.GetArrayLength(); j++)
                    {
                        embedding[j] = embeddingArray[j].GetSingle();
                    }
                    embeddings.Add(embedding);
                }
            }
        }

        return embeddings.ToArray();
    }

    /// <summary>
    /// Calls OpenAI API with the specified system prompt and user prompt
    /// </summary>
    private async Task<string?> CallOpenAIAsync(
        string systemPrompt,
        string userPrompt,
        Configuration config
    )
    {
        try
        {
            var apiKey = await GetApiKeyAsync();
            if (string.IsNullOrEmpty(apiKey))
            {
                _logger.LogWarning("OpenAI API key not configured");
                return null;
            }

            var messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt },
            };

            var request = new
            {
                model = config.Model,
                messages = messages,
                temperature = config.Temperature,
                max_tokens = 4000,
            };

            var content = new StringContent(
                JsonSerializer.Serialize(request),
                Encoding.UTF8,
                "application/json"
            );            var response = await _httpClient.PostAsync(
                "https://api.openai.com/v1/chat/completions",
                content
            );
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError(
                    "OpenAI chat completions API request failed with status {StatusCode} ({ReasonPhrase}). Response: {ErrorContent}",
                    response.StatusCode,
                    response.ReasonPhrase,
                    errorContent
                );
                response.EnsureSuccessStatusCode();
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var document = JsonDocument.Parse(responseContent);

            if (
                document.RootElement.TryGetProperty("choices", out var choices)
                && choices.GetArrayLength() > 0
            )
            {
                var firstChoice = choices[0];
                if (
                    firstChoice.TryGetProperty("message", out var message)
                    && message.TryGetProperty("content", out var responseText)
                )
                {
                    return responseText.GetString();
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling OpenAI API");
            return null;
        }
    }

    /// <summary>
    /// Calls OpenAI with a pattern and user input for general purpose parsing
    /// </summary>
    public async Task<string?> CallPatternAsync(
        string patternPath,
        string userInput,
        string model = "gpt-4o-mini"
    )
    {
        try
        {
            var systemPrompt = await LoadPatternSystemPromptFromPathAsync(patternPath);

            var config = new Configuration
            {
                Model = model,
                Temperature = 0.1, // Low temperature for consistent parsing
            };

            return await CallOpenAIAsync(systemPrompt, userInput, config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling pattern {PatternPath}", patternPath);
            return null;
        }
    }

    /// <summary>
    /// Loads system prompt from pattern file
    /// </summary>
    private async Task<string> LoadPatternSystemPromptAsync(string patternName)
    {
        var executableDir = AppDomain.CurrentDomain.BaseDirectory;
        var patternFile = Path.Combine(executableDir, "patterns", patternName, "system.md");
        if (!File.Exists(patternFile))
        {
            // Fallback: look in the project directory (for development)
            var projectDir = Path.GetDirectoryName(
                System.Reflection.Assembly.GetExecutingAssembly().Location
            );
            while (
                projectDir != null && !File.Exists(Path.Combine(projectDir, "AnalyzeLogs.csproj"))
            )
            {
                projectDir = Path.GetDirectoryName(projectDir);
            }

            if (projectDir != null)
            {
                var devPatternFile = Path.Combine(projectDir, "patterns", patternName, "system.md");
                if (File.Exists(devPatternFile))
                {
                    patternFile = devPatternFile;
                }
            }
        }

        if (!File.Exists(patternFile))
        {
            throw new FileNotFoundException($"Pattern file not found: {patternName}/system.md");
        }

        return await File.ReadAllTextAsync(patternFile);
    }

    /// <summary>
    /// Loads system prompt from a specific file path
    /// </summary>
    private async Task<string> LoadPatternSystemPromptFromPathAsync(string patternPath)
    {
        var fullPath = Path.IsPathRooted(patternPath)
            ? patternPath
            : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, patternPath);

        if (!File.Exists(fullPath))
        {
            // Fallback: look in the project directory (for development)
            var projectDir = Path.GetDirectoryName(
                System.Reflection.Assembly.GetExecutingAssembly().Location
            );
            while (
                projectDir != null && !File.Exists(Path.Combine(projectDir, "AnalyzeLogs.csproj"))
            )
            {
                projectDir = Path.GetDirectoryName(projectDir);
            }

            if (projectDir != null)
            {
                var devPatternFile = Path.Combine(projectDir, patternPath);
                if (File.Exists(devPatternFile))
                {
                    fullPath = devPatternFile;
                }
            }
        }

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"Pattern file not found: {patternPath}");
        }

        return await File.ReadAllTextAsync(fullPath);
    }

    /// <summary>
    /// Builds prompt for anomaly detection
    /// </summary>
    private string BuildAnomalyDetectionPrompt(LogChunk chunk)
    {
        var prompt = new StringBuilder();

        prompt.AppendLine("## Log Entries to Analyze:");
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

        prompt.AppendLine("## Log Sequence to Analyze:");
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

        prompt.AppendLine("## Log Entries to Tag:");
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
    /// Parses anomaly detection response from OpenAI
    /// </summary>
    private List<Anomaly> ParseAnomalyResponse(string response, LogChunk chunk)
    {
        var anomalies = new List<Anomaly>();

        try
        {
            var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            Anomaly? currentAnomaly = null;

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();

                if (trimmedLine == "ANOMALY_START")
                {
                    currentAnomaly = new Anomaly();
                    continue;
                }

                if (trimmedLine == "ANOMALY_END" && currentAnomaly != null)
                {
                    anomalies.Add(currentAnomaly);
                    currentAnomaly = null;
                    continue;
                }

                if (currentAnomaly != null && trimmedLine.Contains(':'))
                {
                    var parts = trimmedLine.Split(':', 2);
                    if (parts.Length == 2)
                    {
                        var key = parts[0].Trim();
                        var value = parts[1].Trim();

                        switch (key.ToUpper())
                        {
                            case "TYPE":
                                if (Enum.TryParse<AnomalyType>(value, out var type))
                                    currentAnomaly.Type = type;
                                break;
                            case "SEVERITY":
                                if (Enum.TryParse<AnomalySeverity>(value, out var severity))
                                    currentAnomaly.Severity = severity;
                                break;
                            case "CONFIDENCE":
                                if (double.TryParse(value, out var confidence))
                                    currentAnomaly.Confidence = confidence;
                                break;
                            case "TIMESTAMP":
                                if (DateTime.TryParse(value, out var timestamp))
                                    currentAnomaly.Timestamp = timestamp;
                                break;
                            case "DESCRIPTION":
                                currentAnomaly.Description = value;
                                break;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error parsing anomaly response");
        }

        return anomalies;
    }

    /// <summary>
    /// Parses coherence analysis response from OpenAI
    /// </summary>
    private List<Anomaly> ParseCoherenceResponse(string response, LogChunk chunk)
    {
        var coherenceIssues = new List<Anomaly>();

        try
        {
            var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                if (
                    trimmedLine.StartsWith("MISSING_EVENTS:")
                    || trimmedLine.StartsWith("INCOMPLETE_TRANSACTIONS:")
                    || trimmedLine.StartsWith("OUT_OF_ORDER_EVENTS:")
                )
                {
                    var parts = trimmedLine.Split(':', 2);
                    if (parts.Length == 2 && !string.IsNullOrWhiteSpace(parts[1]))
                    {
                        var issues = parts[1].Split(';', StringSplitOptions.RemoveEmptyEntries);
                        foreach (var issue in issues)
                        {
                            var anomaly = new Anomaly
                            {
                                Type = AnomalyType.Sequence,
                                Severity = AnomalySeverity.Medium,
                                Description = issue.Trim(),
                                Timestamp = chunk.StartTime,
                                Confidence = 0.7,
                            };

                            coherenceIssues.Add(anomaly);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error parsing coherence response");
        }

        return coherenceIssues;
    }

    /// <summary>
    /// Parses tagging response and applies tags to entries
    /// </summary>
    private void ParseTaggingResponse(string response, LogChunk chunk)
    {
        try
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
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error parsing tagging response");
        }
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}
