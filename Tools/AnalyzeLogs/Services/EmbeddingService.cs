using System.Text;
using System.Text.Json;
using AnalyzeLogs.Models;
using Microsoft.Extensions.Logging;

namespace AnalyzeLogs.Services;

/// <summary>
/// Service for generating embeddings and performing vector operations
/// </summary>
public class EmbeddingService : IDisposable
{
    private readonly ILogger<EmbeddingService> _logger;
    private readonly HttpClient _httpClient;
    private readonly ConfigurationService _configService;
    private string? _apiKey;
    private readonly string _model;

    public EmbeddingService(
        ILogger<EmbeddingService> logger,
        ConfigurationService configService,
        string model = "text-embedding-3-small"
    )
    {
        _logger = logger;
        _configService = configService;
        _httpClient = new HttpClient();
        _model = model;
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

    private async Task EnsureApiKeyConfiguredAsync()
    {
        var apiKey = await GetApiKeyAsync();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("OPENAI_API_KEY not found. Embedding functionality will be disabled.");
        }
    }

    /// <summary>
    /// Generates embeddings for log entries
    /// </summary>
    public async Task GenerateEmbeddingsAsync(List<LogEntry> entries, bool verbose = false)
    {
        var apiKey = await GetApiKeyAsync();
        if (string.IsNullOrEmpty(apiKey))
        {
            _logger.LogWarning("Skipping embedding generation - API key not available");
            return;
        }

        if (verbose)
        {
            _logger.LogInformation(
                "Generating embeddings for {EntryCount} log entries",
                entries.Count
            );
        }

        var tasks = new List<Task>();
        var semaphore = new SemaphoreSlim(10); // Limit concurrent requests

        foreach (var batch in entries.Chunk(100)) // Process in batches
        {
            tasks.Add(ProcessBatchAsync(batch, semaphore, verbose));
        }

        await Task.WhenAll(tasks);

        var embeddedCount = entries.Count(e => e.Embedding != null);
        _logger.LogInformation(
            "Generated embeddings for {EmbeddedCount} out of {TotalCount} entries",
            embeddedCount,
            entries.Count
        );
    }

    /// <summary>
    /// Processes a batch of log entries for embedding generation
    /// </summary>
    private async Task ProcessBatchAsync(LogEntry[] batch, SemaphoreSlim semaphore, bool verbose)
    {
        await semaphore.WaitAsync();
        
        try
        {
            var apiKey = await GetApiKeyAsync();
            if (string.IsNullOrEmpty(apiKey))
            {
                return; // Skip processing if no API key
            }

            var texts = batch.Select(GetEmbeddingText).ToArray();
            var embeddings = await GetEmbeddingsAsync(texts);

            for (int i = 0; i < batch.Length && i < embeddings.Length; i++)
            {
                batch[i].Embedding = embeddings[i];
            }

            if (verbose)
            {
                _logger.LogDebug(
                    "Generated embeddings for batch of {BatchSize} entries",
                    batch.Length
                );
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate embeddings for batch");
        }
        finally
        {
            semaphore.Release();
        }
    }

    /// <summary>
    /// Gets text representation for embedding generation
    /// </summary>
    private string GetEmbeddingText(LogEntry entry)
    {
        var text = new StringBuilder();
        
        // Include log level
        text.Append($"[{entry.Level}] ");
        
        // Include service if available
        if (!string.IsNullOrEmpty(entry.Service))
        {
            text.Append($"{entry.Service}: ");
        }
        
        // Include the main message
        text.Append(entry.Message);
        
        // Include additional structured data
        if (entry.HttpStatus.HasValue)
        {
            text.Append($" Status:{entry.HttpStatus}");
        }
        
        if (entry.ResponseTimeMs.HasValue)
        {
            text.Append($" ResponseTime:{entry.ResponseTimeMs}ms");
        }
        
        return text.ToString();
    }

    /// <summary>
    /// Gets embeddings from OpenAI API
    /// </summary>
    private async Task<float[][]> GetEmbeddingsAsync(string[] texts)
    {
        var request = new { model = _model, input = texts };

        var content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json"
        );
        
        var response = await _httpClient.PostAsync("https://api.openai.com/v1/embeddings", content);
        response.EnsureSuccessStatusCode();

        var responseContent = await response.Content.ReadAsStringAsync();
        var document = JsonDocument.Parse(responseContent);
        
        var embeddings = new List<float[]>();
        
        if (document.RootElement.TryGetProperty("data", out var dataArray))
        {
            for (int i = 0; i < dataArray.GetArrayLength(); i++)
            {
                var embeddingArray = dataArray[i].GetProperty("embedding");
                var embedding = new float[embeddingArray.GetArrayLength()];
                
                for (int j = 0; j < embedding.Length; j++)
                {
                    embedding[j] = embeddingArray[j].GetSingle();
                }
                
                embeddings.Add(embedding);
            }
        }

        return embeddings.ToArray();
    }

    /// <summary>
    /// Finds similar log entries using semantic search
    /// </summary>
    public List<(LogEntry Entry, double Similarity)> FindSimilar(
        LogEntry target,
        List<LogEntry> candidates,
        int topK = 10
    )
    {
        if (target.Embedding == null)
        {
            return new List<(LogEntry, double)>();
        }

        var similarities = new List<(LogEntry Entry, double Similarity)>();

        foreach (var candidate in candidates)
        {
            if (candidate.Embedding == null || candidate.Id == target.Id)
                continue;

            var similarity = CosineSimilarity(target.Embedding, candidate.Embedding);
            similarities.Add((candidate, similarity));
        }

        return similarities.OrderByDescending(s => s.Similarity).Take(topK).ToList();
    }

    /// <summary>
    /// Clusters log entries using simple k-means clustering
    /// </summary>
    public List<List<LogEntry>> ClusterEntries(List<LogEntry> entries, int maxClusters = 10)
    {
        var entriesWithEmbeddings = entries.Where(e => e.Embedding != null).ToList();
        if (entriesWithEmbeddings.Count == 0)
        {
            return new List<List<LogEntry>>();
        }

        var clusters = new List<List<LogEntry>>();
        var visited = new HashSet<string>();

        foreach (var entry in entriesWithEmbeddings)
        {
            if (visited.Contains(entry.Id))
                continue;

            var cluster = new List<LogEntry> { entry };
            visited.Add(entry.Id);

            // Find similar entries
            var similar = FindSimilar(entry, entriesWithEmbeddings, 20)
                .Where(s => s.Similarity > 0.8 && !visited.Contains(s.Entry.Id))
                .Take(50);

            foreach (var (similarEntry, _) in similar)
            {
                if (!visited.Contains(similarEntry.Id))
                {
                    cluster.Add(similarEntry);
                    visited.Add(similarEntry.Id);
                }
            }

            if (cluster.Count > 1)
            {
                clusters.Add(cluster);
            }

            if (clusters.Count >= maxClusters)
                break;
        }

        _logger.LogInformation(
            "Clustered {EntryCount} entries into {ClusterCount} clusters",
            entriesWithEmbeddings.Count,
            clusters.Count
        );

        return clusters;
    }

    /// <summary>
    /// Detects outliers based on embedding distances
    /// </summary>
    public List<LogEntry> DetectOutliers(List<LogEntry> entries, double threshold = 0.3)
    {
        var entriesWithEmbeddings = entries.Where(e => e.Embedding != null).ToList();
        var outliers = new List<LogEntry>();

        foreach (var entry in entriesWithEmbeddings)
        {
            var similarities = FindSimilar(entry, entriesWithEmbeddings, 5);
            
            if (similarities.Count == 0 || similarities.Max(s => s.Similarity) < threshold)
            {
                entry.IsAnomaly = true;
                entry.AnomalyScore = 1.0 - (similarities.Count > 0 ? similarities.Max(s => s.Similarity) : 0.0);
                outliers.Add(entry);
            }
        }

        _logger.LogInformation(
            "Detected {OutlierCount} outliers out of {TotalCount} entries",
            outliers.Count,
            entriesWithEmbeddings.Count
        );

        return outliers;
    }

    /// <summary>
    /// Calculates cosine similarity between two vectors
    /// </summary>
    private double CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length)
            return 0.0;

        double dotProduct = 0.0;
        double normA = 0.0;
        double normB = 0.0;

        for (int i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        if (normA == 0.0 || normB == 0.0)
            return 0.0;

        return dotProduct / (Math.Sqrt(normA) * Math.Sqrt(normB));
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}
