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
    private readonly string _apiKey;
    private readonly string _model;

    public EmbeddingService(ILogger<EmbeddingService> logger, string model = "text-embedding-3-small")
    {
        _logger = logger;
        _httpClient = new HttpClient();
        _model = model;
        
        _apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") 
                  ?? throw new InvalidOperationException("OPENAI_API_KEY environment variable is required");

        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
    }

    /// <summary>
    /// Generates embeddings for log entries
    /// </summary>
    public async Task GenerateEmbeddingsAsync(List<LogEntry> entries, bool verbose = false)
    {
        if (verbose)
        {
            _logger.LogInformation("Generating embeddings for {EntryCount} log entries", entries.Count);
        }

        var tasks = new List<Task>();
        var semaphore = new SemaphoreSlim(10); // Limit concurrent requests

        foreach (var batch in entries.Chunk(100)) // Process in batches
        {
            tasks.Add(ProcessBatchAsync(batch, semaphore, verbose));
        }

        await Task.WhenAll(tasks);

        var embeddedCount = entries.Count(e => e.Embedding != null);
        _logger.LogInformation("Generated embeddings for {EmbeddedCount} out of {TotalCount} entries", 
            embeddedCount, entries.Count);
    }

    /// <summary>
    /// Processes a batch of log entries for embedding generation
    /// </summary>
    private async Task ProcessBatchAsync(LogEntry[] batch, SemaphoreSlim semaphore, bool verbose)
    {
        await semaphore.WaitAsync();
        
        try
        {
            var texts = batch.Select(GetEmbeddingText).ToArray();
            var embeddings = await GetEmbeddingsAsync(texts);

            for (int i = 0; i < batch.Length && i < embeddings.Length; i++)
            {
                batch[i].Embedding = embeddings[i];
            }

            if (verbose)
            {
                _logger.LogDebug("Generated embeddings for batch of {BatchSize} entries", batch.Length);
            }
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
            text.Append($"[{entry.Service}] ");
        }
        
        // Include main message
        text.Append(entry.Message);
        
        // Include relevant additional data
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
        try
        {
            var request = new
            {
                input = texts,
                model = _model
            };

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("https://api.openai.com/v1/embeddings", content);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(responseJson);
            
            var dataArray = document.RootElement.GetProperty("data");
            var embeddings = new float[dataArray.GetArrayLength()][];

            for (int i = 0; i < embeddings.Length; i++)
            {
                var embeddingArray = dataArray[i].GetProperty("embedding");
                embeddings[i] = new float[embeddingArray.GetArrayLength()];
                
                for (int j = 0; j < embeddings[i].Length; j++)
                {
                    embeddings[i][j] = embeddingArray[j].GetSingle();
                }
            }

            return embeddings;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating embeddings for {TextCount} texts", texts.Length);
            return new float[texts.Length][];
        }
    }

    /// <summary>
    /// Finds similar log entries using cosine similarity
    /// </summary>
    public List<(LogEntry Entry, double Similarity)> FindSimilar(LogEntry target, List<LogEntry> candidates, int topK = 10)
    {
        if (target.Embedding == null)
            return new List<(LogEntry, double)>();

        var similarities = new List<(LogEntry Entry, double Similarity)>();

        foreach (var candidate in candidates)
        {
            if (candidate.Embedding == null || candidate.Id == target.Id)
                continue;

            var similarity = CosineSimilarity(target.Embedding, candidate.Embedding);
            similarities.Add((candidate, similarity));
        }

        return similarities
            .OrderByDescending(s => s.Similarity)
            .Take(topK)
            .ToList();
    }

    /// <summary>
    /// Clusters log entries using simple k-means clustering
    /// </summary>
    public List<List<LogEntry>> ClusterEntries(List<LogEntry> entries, int maxClusters = 10)
    {
        var entriesWithEmbeddings = entries.Where(e => e.Embedding != null).ToList();
        
        if (entriesWithEmbeddings.Count == 0)
            return new List<List<LogEntry>>();

        var k = Math.Min(maxClusters, entriesWithEmbeddings.Count);
        var clusters = new List<List<LogEntry>>();

        // Simple clustering by similarity
        var processed = new HashSet<string>();
        
        foreach (var entry in entriesWithEmbeddings)
        {
            if (processed.Contains(entry.Id))
                continue;

            var cluster = new List<LogEntry> { entry };
            processed.Add(entry.Id);

            // Find similar entries
            var similar = FindSimilar(entry, entriesWithEmbeddings, 20)
                .Where(s => s.Similarity > 0.8 && !processed.Contains(s.Entry.Id))
                .Select(s => s.Entry)
                .ToList();

            foreach (var similarEntry in similar)
            {
                cluster.Add(similarEntry);
                processed.Add(similarEntry.Id);
            }

            clusters.Add(cluster);
        }

        _logger.LogInformation("Clustered {EntryCount} entries into {ClusterCount} clusters", 
            entriesWithEmbeddings.Count, clusters.Count);

        return clusters.OrderByDescending(c => c.Count).ToList();
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
            
            if (similarities.Count > 0)
            {
                var avgSimilarity = similarities.Average(s => s.Similarity);
                
                if (avgSimilarity < threshold)
                {
                    entry.IsAnomaly = true;
                    entry.AnomalyScore = 1.0 - avgSimilarity;
                    outliers.Add(entry);
                }
            }
        }

        _logger.LogInformation("Detected {OutlierCount} outliers out of {TotalCount} entries", 
            outliers.Count, entriesWithEmbeddings.Count);

        return outliers;
    }

    /// <summary>
    /// Calculates cosine similarity between two vectors
    /// </summary>
    private double CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length)
            return 0;

        double dotProduct = 0;
        double normA = 0;
        double normB = 0;

        for (int i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        if (normA == 0 || normB == 0)
            return 0;

        return dotProduct / (Math.Sqrt(normA) * Math.Sqrt(normB));
    }

    /// <summary>
    /// Disposes the HttpClient and other resources
    /// </summary>
    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}
