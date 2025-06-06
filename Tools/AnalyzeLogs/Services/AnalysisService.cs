using AnalyzeLogs.Models;
using Microsoft.Extensions.Logging;

namespace AnalyzeLogs.Services;

/// <summary>
/// Main service that orchestrates the log analysis process
/// </summary>
public class AnalysisService
{
    private readonly ILogger<AnalysisService> _logger;
    private readonly Configuration _config;
    private readonly FabricService _fabricService;
    private readonly EmbeddingService _embeddingService;

    public AnalysisService(
        ILogger<AnalysisService> logger,
        Configuration config,
        FabricService fabricService,
        EmbeddingService embeddingService)
    {
        _logger = logger;
        _config = config;
        _fabricService = fabricService;
        _embeddingService = embeddingService;
    }

    /// <summary>
    /// Analyzes logs and returns analysis results
    /// </summary>
    public async Task<AnalysisResult> AnalyzeLogsAsync(List<LogEntry> logEntries)
    {
        _logger.LogInformation("Starting analysis of {EntryCount} log entries", logEntries.Count);

        var result = new AnalysisResult
        {
            TotalEntries = logEntries.Count,
            StartTime = logEntries.Count > 0 ? logEntries.Min(e => e.Timestamp) : DateTime.UtcNow,
            EndTime = logEntries.Count > 0 ? logEntries.Max(e => e.Timestamp) : DateTime.UtcNow
        };

        try
        {
            // Step 1: Generate embeddings if enabled
            if (_config.EnableEmbeddings)
            {
                _logger.LogInformation("Generating embeddings...");
                await _embeddingService.GenerateEmbeddingsAsync(logEntries, _config.Verbose);
                
                // Detect outliers using embeddings
                var outliers = _embeddingService.DetectOutliers(logEntries);
                result.EmbeddingOutliers.AddRange(outliers);
            }

            // Step 2: Create chunks for analysis
            var chunks = CreateLogChunks(logEntries);
            _logger.LogInformation("Created {ChunkCount} log chunks for analysis", chunks.Count);

            // Step 3: AI-powered analysis
            foreach (var chunk in chunks)
            {
                await AnalyzeChunkAsync(chunk, result);
            }

            // Step 4: Calculate service metrics
            result.ServiceMetrics = CalculateServiceMetrics(logEntries);

            // Step 5: Find correlations
            result.Correlations = FindCorrelations(logEntries);

            _logger.LogInformation("Analysis completed. Found {AnomalyCount} anomalies across {ServiceCount} services",
                result.Anomalies.Count, result.ServiceMetrics.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during log analysis");
            result.Errors.Add($"Analysis error: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Creates manageable chunks of logs for analysis
    /// </summary>
    private List<LogChunk> CreateLogChunks(List<LogEntry> logEntries)
    {
        var chunks = new List<LogChunk>();
        var currentChunk = new LogChunk();
        var currentTokenCount = 0;

        foreach (var entry in logEntries.OrderBy(e => e.Timestamp))
        {
            // Estimate token count (rough approximation)
            var entryTokens = EstimateTokenCount(entry);

            if (currentTokenCount + entryTokens > _config.MaxChunkSize && currentChunk.Entries.Count > 0)
            {
                // Finalize current chunk
                FinalizeChunk(currentChunk);
                chunks.Add(currentChunk);

                // Start new chunk
                currentChunk = new LogChunk();
                currentTokenCount = 0;
            }

            currentChunk.Entries.Add(entry);
            currentChunk.Services.Add(entry.Service ?? "Unknown");
            
            if (!string.IsNullOrEmpty(entry.CorrelationId))
            {
                currentChunk.CorrelationIds.Add(entry.CorrelationId);
            }

            currentTokenCount += entryTokens;
        }

        // Add final chunk if it has entries
        if (currentChunk.Entries.Count > 0)
        {
            FinalizeChunk(currentChunk);
            chunks.Add(currentChunk);
        }

        return chunks;
    }

    /// <summary>
    /// Finalizes a chunk by setting timestamps and token count
    /// </summary>
    private void FinalizeChunk(LogChunk chunk)
    {
        if (chunk.Entries.Count > 0)
        {
            chunk.StartTime = chunk.Entries.Min(e => e.Timestamp);
            chunk.EndTime = chunk.Entries.Max(e => e.Timestamp);
            chunk.EstimatedTokenCount = chunk.Entries.Sum(EstimateTokenCount);
        }
    }

    /// <summary>
    /// Estimates token count for a log entry
    /// </summary>
    private int EstimateTokenCount(LogEntry entry)
    {
        // Rough approximation: 4 characters per token
        var text = $"{entry.Timestamp} {entry.Level} {entry.Service} {entry.Message}";
        return text.Length / 4;
    }

    /// <summary>
    /// Analyzes a single chunk using AI services
    /// </summary>
    private async Task AnalyzeChunkAsync(LogChunk chunk, AnalysisResult result)
    {
        try
        {
            var tasks = new List<Task>();

            // Anomaly detection
            if (_config.EnableAnomalyDetection)
            {
                tasks.Add(Task.Run(async () =>
                {
                    var anomalies = await _fabricService.DetectAnomaliesAsync(chunk, _config);
                    lock (result.Anomalies)
                    {
                        result.Anomalies.AddRange(anomalies);
                    }
                }));
            }

            // Coherence analysis
            if (_config.EnableCoherenceAnalysis)
            {
                tasks.Add(Task.Run(async () =>
                {
                    var coherenceIssues = await _fabricService.AnalyzeCoherenceAsync(chunk, _config);
                    lock (result.Anomalies)
                    {
                        result.Anomalies.AddRange(coherenceIssues);
                    }
                }));
            }

            // Tagging
            if (_config.EnableTagging)
            {
                tasks.Add(Task.Run(async () =>
                {
                    await _fabricService.TagLogEntriesAsync(chunk, _config);
                }));
            }

            await Task.WhenAll(tasks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing chunk {ChunkId}", chunk.Id);
            result.Errors.Add($"Chunk analysis error: {ex.Message}");
        }
    }

    /// <summary>
    /// Calculates metrics for each service
    /// </summary>
    private List<ServiceMetrics> CalculateServiceMetrics(List<LogEntry> logEntries)
    {
        var serviceGroups = logEntries.GroupBy(e => e.Service ?? "Unknown");
        var metrics = new List<ServiceMetrics>();

        foreach (var group in serviceGroups)
        {
            var entries = group.ToList();
            var timeSpan = entries.Max(e => e.Timestamp) - entries.Min(e => e.Timestamp);
            var requestRate = timeSpan.TotalMinutes > 0 ? entries.Count / timeSpan.TotalMinutes : 0;

            var serviceMetric = new ServiceMetrics
            {
                ServiceName = group.Key,
                TotalEntries = entries.Count,
                ErrorCount = entries.Count(e => e.Level >= LogLevel.Error),
                WarningCount = entries.Count(e => e.Level == LogLevel.Warning),
                StartTime = entries.Min(e => e.Timestamp),
                EndTime = entries.Max(e => e.Timestamp),
                RequestRate = requestRate,
                UniqueUsers = entries.Where(e => !string.IsNullOrEmpty(e.UserId))
                                   .Select(e => e.UserId)
                                   .Distinct()
                                   .Count()
            };

            // Calculate response times if available
            var responseTimes = entries.Where(e => e.ResponseTimeMs.HasValue)
                                     .Select(e => e.ResponseTimeMs!.Value)
                                     .ToList();
            
            if (responseTimes.Count > 0)
            {
                serviceMetric.AverageResponseTime = responseTimes.Average();
                serviceMetric.P95ResponseTime = CalculatePercentile(responseTimes, 0.95);
            }

            // Calculate HTTP status distribution
            var httpStatuses = entries.Where(e => e.HttpStatus.HasValue)
                                    .GroupBy(e => e.HttpStatus!.Value)
                                    .ToDictionary(g => g.Key, g => g.Count());
            serviceMetric.HttpStatusDistribution = httpStatuses;

            // Calculate tag distribution
            var tags = entries.SelectMany(e => e.Tags)
                            .GroupBy(t => t)
                            .ToDictionary(g => g.Key, g => g.Count());
            serviceMetric.TagDistribution = tags;

            metrics.Add(serviceMetric);
        }

        return metrics;
    }

    /// <summary>
    /// Finds correlations between log entries
    /// </summary>
    private List<LogCorrelation> FindCorrelations(List<LogEntry> logEntries)
    {
        var correlations = new List<LogCorrelation>();

        // Group by correlation ID
        var correlationGroups = logEntries
            .Where(e => !string.IsNullOrEmpty(e.CorrelationId))
            .GroupBy(e => e.CorrelationId!);

        foreach (var group in correlationGroups)
        {
            var entries = group.OrderBy(e => e.Timestamp).ToList();
            
            var correlation = new LogCorrelation
            {
                Id = group.Key,
                Entries = entries,
                Services = entries.Select(e => e.Service ?? "Unknown").ToHashSet(),
                StartTime = entries.Min(e => e.Timestamp),
                EndTime = entries.Max(e => e.Timestamp),
                IsSuccessful = !entries.Any(e => e.Level >= LogLevel.Error)
            };

            correlations.Add(correlation);
        }

        // Time-based correlation for entries without correlation IDs
        var uncorrelatedEntries = logEntries
            .Where(e => string.IsNullOrEmpty(e.CorrelationId))
            .OrderBy(e => e.Timestamp)
            .ToList();

        // Group by time windows
        var timeWindowMinutes = _config.CorrelationWindowMinutes;
        var timeGroups = uncorrelatedEntries
            .GroupBy(e => new DateTime(e.Timestamp.Year, e.Timestamp.Month, e.Timestamp.Day,
                                     e.Timestamp.Hour, e.Timestamp.Minute / timeWindowMinutes * timeWindowMinutes, 0))
            .Where(g => g.Count() > 1);

        foreach (var group in timeGroups)
        {
            var entries = group.ToList();
            
            var correlation = new LogCorrelation
            {
                Id = $"time-{group.Key:yyyyMMddHHmm}",
                Entries = entries,
                Services = entries.Select(e => e.Service ?? "Unknown").ToHashSet(),
                StartTime = entries.Min(e => e.Timestamp),
                EndTime = entries.Max(e => e.Timestamp),
                IsSuccessful = !entries.Any(e => e.Level >= LogLevel.Error)
            };

            correlations.Add(correlation);
        }

        return correlations;
    }

    /// <summary>
    /// Calculates percentile value
    /// </summary>
    private double CalculatePercentile(List<double> values, double percentile)
    {
        if (values.Count == 0) return 0;
        
        var sorted = values.OrderBy(x => x).ToList();
        var index = (int)Math.Ceiling(percentile * sorted.Count) - 1;
        index = Math.Max(0, Math.Min(index, sorted.Count - 1));
        
        return sorted[index];
    }
}

/// <summary>
/// Results of log analysis
/// </summary>
public class AnalysisResult
{
    public int TotalEntries { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public List<Anomaly> Anomalies { get; set; } = new();
    public List<ServiceMetrics> ServiceMetrics { get; set; } = new();
    public List<LogCorrelation> Correlations { get; set; } = new();
    public List<LogEntry> EmbeddingOutliers { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}
