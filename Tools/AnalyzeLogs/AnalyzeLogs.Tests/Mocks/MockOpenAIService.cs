using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AnalyzeLogs.Models;
using AnalyzeLogs.Services.Analysis;

namespace AnalyzeLogs.Tests.Mocks
{
    /// <summary>
    /// Mock implementation of the OpenAI service for testing.
    /// </summary>
    public class MockOpenAIService : IOpenAIService
    {
        private readonly Dictionary<string, string> _anomalyResponses = new();
        private readonly Dictionary<string, string> _coherenceResponses = new();
        private readonly Dictionary<string, string> _parseResponses = new();
        private readonly Dictionary<string, string> _tagResponses = new();
        private readonly Dictionary<string, string> _queryResponses = new();
        private readonly Dictionary<string, string> _diagramResponses = new();
        private readonly Dictionary<string, float[]> _embeddings = new();

        private readonly Random _random = new Random(42); // Use fixed seed for reproducibility

        /// <summary>
        /// Initializes a new instance of the <see cref="MockOpenAIService"/> class.
        /// </summary>
        /// <param name="responsesPath">Path to the directory containing response files.</param>
        public MockOpenAIService(string responsesPath = "TestData/AiResponses")
        {
            // Load mock responses from JSON files
            LoadResponses(responsesPath);
        }

        /// <inheritdoc/>
        public Task<string> AnalyzeAnomaliesAsync(IEnumerable<LogEntry> logEntries)
        {
            var key = GetResponseKey(logEntries);
            return Task.FromResult(
                _anomalyResponses.GetValueOrDefault(key, DefaultAnomalyResponse())
            );
        }

        /// <inheritdoc/>
        public Task<string> AnalyzeCoherenceAsync(IEnumerable<LogEntry> logEntries)
        {
            var key = GetResponseKey(logEntries);
            return Task.FromResult(
                _coherenceResponses.GetValueOrDefault(key, DefaultCoherenceResponse())
            );
        }

        /// <inheritdoc/>
        public Task<string> TagLogsAsync(IEnumerable<LogEntry> logEntries)
        {
            var key = GetResponseKey(logEntries);
            return Task.FromResult(
                _tagResponses.GetValueOrDefault(key, DefaultTagResponse(logEntries))
            );
        }

        /// <inheritdoc/>
        public Task<string> SummarizeLogsAsync(IEnumerable<LogEntry> logEntries)
        {
            return Task.FromResult($"Summary of {logEntries.Count()} log entries");
        }

        /// <inheritdoc/>
        public Task<string> ResearchAnomalyAsync(
            LogEntry anomalyLogEntry,
            IEnumerable<LogEntry> contextLogEntries,
            string anomalyDescription
        )
        {
            return Task.FromResult($"Research findings for anomaly: {anomalyDescription}");
        }

        /// <inheritdoc/>
        public Task<string> GenerateDiagramAsync(
            IEnumerable<LogEntry> logEntries,
            string diagramType
        )
        {
            return Task.FromResult(
                _diagramResponses.GetValueOrDefault(
                    diagramType,
                    DefaultDiagramResponse(diagramType)
                )
            );
        }

        /// <inheritdoc/>
        public Task<string> ParseLogAsync(string logLine, string sourcePath, string sourceFile)
        {
            // Try to match the log line to a key in our parse responses
            string key = "malformed_log"; // Default to malformed

            if (logLine.StartsWith("{") && logLine.Contains("\"service\""))
            {
                key = "json_log";
            }
            else if (logLine.StartsWith("[") && logLine.Contains("]") && logLine.Contains("INFO"))
            {
                key = "text_log";
            }
            else if (
                !logLine.StartsWith("[")
                && !logLine.StartsWith("{")
                && logLine.Contains("INFO")
            )
            {
                key = "unstructured_log";
            }

            // Get the response from our loaded responses
            var parseResponseJson = _parseResponses.GetValueOrDefault(key);
            if (parseResponseJson != null)
            {
                // Return as-is since it's already in JSON format
                return Task.FromResult(parseResponseJson);
            }

            // If we don't have a specific response, return a basic one
            return Task.FromResult(DefaultParseResponse(logLine, sourcePath, sourceFile));
        }

        /// <inheritdoc/>
        public Task<IList<float[]>> GenerateEmbeddingsAsync(IEnumerable<string> texts)
        {
            // Generate mock embeddings of the correct dimension
            var result = new List<float[]>();

            foreach (var text in texts)
            {
                // If we already have an embedding for this text, reuse it
                if (_embeddings.TryGetValue(text, out var embedding))
                {
                    result.Add(embedding);
                    continue;
                }

                // Otherwise, generate a new random embedding
                var newEmbedding = GenerateRandomEmbedding(1536); // OpenAI embedding dimension
                _embeddings[text] = newEmbedding;
                result.Add(newEmbedding);
            }

            return Task.FromResult((IList<float[]>)result);
        }

        /// <inheritdoc/>
        public Task<IList<float[]>> GenerateEmbeddingsAsync(
            IEnumerable<LogEntry> logEntries,
            CancellationToken cancellationToken = default
        )
        {
            // Extract normalized messages
            var texts = logEntries
                .Select(entry => entry.NormalizedMessage ?? entry.RawMessage ?? string.Empty)
                .ToList();

            // Generate embeddings
            return GenerateEmbeddingsAsync(texts);
        }

        /// <inheritdoc/>
        public Task<string> InterpretQueryAsync(
            string query,
            string? projectContext = null,
            string? sessionContext = null
        )
        {
            // Map the query to a response key
            string key = MapQueryToResponseKey(query);

            return Task.FromResult(_queryResponses.GetValueOrDefault(key, DefaultQueryResponse()));
        }

        #region Private Helper Methods

        private void LoadResponses(string path)
        {
            try
            {
                // Load parse responses
                string parseResponsesPath = Path.Combine(path, "parse_responses.json");
                if (File.Exists(parseResponsesPath))
                {
                    var parseResponses = JsonSerializer.Deserialize<JsonElement>(
                        File.ReadAllText(parseResponsesPath)
                    );
                    foreach (var property in parseResponses.EnumerateObject())
                    {
                        _parseResponses[property.Name] = property.Value.ToString();
                    }
                }

                // Load anomaly responses
                string anomalyResponsesPath = Path.Combine(path, "anomaly_responses.json");
                if (File.Exists(anomalyResponsesPath))
                {
                    var anomalyResponses = JsonSerializer.Deserialize<Dictionary<string, string>>(
                        File.ReadAllText(anomalyResponsesPath)
                    );
                    foreach (var kvp in anomalyResponses)
                    {
                        _anomalyResponses[kvp.Key] = kvp.Value;
                    }
                }

                // Load query responses
                string queryResponsesPath = Path.Combine(path, "query_responses.json");
                if (File.Exists(queryResponsesPath))
                {
                    var queryResponses = JsonSerializer.Deserialize<JsonElement>(
                        File.ReadAllText(queryResponsesPath)
                    );
                    foreach (var property in queryResponses.EnumerateObject())
                    {
                        _queryResponses[property.Name] = property.Value.ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading mock responses: {ex.Message}");
                // Initialize with default responses
            }
        }

        private string GetResponseKey(IEnumerable<LogEntry> logEntries)
        {
            var entries = logEntries.ToList();

            // Check for specific patterns to match to pre-defined responses

            // Check for connection timeout
            if (entries.Any(e => e.RawMessage?.Contains("Connection timeout") == true))
            {
                return "connection_timeout";
            }

            // Check for sequence anomaly in the API gateway/database logs
            if (
                entries.Any(e => e.Service?.ServiceName == "api-gateway")
                && entries.Any(e => e.Service?.ServiceName == "database-service")
            )
            {
                return "sequence_anomaly";
            }

            // Check if entries contain no errors or warnings
            if (
                !entries.Any(e =>
                    e.SeverityLevel?.LevelName == "ERROR" || e.SeverityLevel?.LevelName == "WARN"
                )
            )
            {
                return "no_anomalies";
            }

            // Default case - use standard response
            return "standard_logs";
        }

        private string MapQueryToResponseKey(string query)
        {
            query = query.ToLower();

            if (query.Contains("yesterday") && query.Contains("error"))
            {
                return "time_range_query";
            }

            if (query.Contains("payment") && query.Contains("between"))
            {
                return "service_query";
            }

            if (query.Contains("similar") && query.Contains("database"))
            {
                return "semantic_query";
            }

            if (query.Contains("auth") && query.Contains("token"))
            {
                return "hybrid_query";
            }

            if (query.Contains("anomal"))
            {
                return "anomaly_query";
            }

            // Default to time range
            return "time_range_query";
        }

        private string DefaultAnomalyResponse()
        {
            return "No anomalies detected in the provided log entries.";
        }

        private string DefaultCoherenceResponse()
        {
            return "The log sequence appears coherent with no missing steps or unusual patterns.";
        }

        private string DefaultTagResponse(IEnumerable<LogEntry> logEntries)
        {
            var response = new Dictionary<string, string[]>();

            foreach (var entry in logEntries)
            {
                if (entry.LogEntryId > 0)
                {
                    response[entry.LogEntryId.ToString()] = new[] { "generated-tag" };
                }
            }

            return JsonSerializer.Serialize(response);
        }

        private string DefaultDiagramResponse(string diagramType)
        {
            return $"```mermaid\n{diagramType} TD\n    A[Service A] --> B[Service B]\n    B --> C[Service C]\n```";
        }

        private string DefaultParseResponse(string logLine, string sourcePath, string sourceFile)
        {
            var response = new
            {
                timestampUTC = DateTime.UtcNow.ToString("o"),
                originalTimestamp = null as string,
                originalTimeZone = null as string,
                detectedFormat = "Unstructured",
                normalizedMessage = logLine,
                severityLevel = null as object,
                service = null as object,
                correlationId = null as string,
                threadId = null as string,
                processId = null as string,
                additionalDataJson = null as string,
            };

            return JsonSerializer.Serialize(response);
        }

        private string DefaultQueryResponse()
        {
            return JsonSerializer.Serialize(
                new
                {
                    queryType = "filter",
                    parameters = new
                    {
                        timeRange = new
                        {
                            start = DateTime.UtcNow.AddDays(-1).ToString("o"),
                            end = DateTime.UtcNow.ToString("o"),
                        },
                    },
                    description = "Default query for recent logs",
                    sqlQuery = "SELECT * FROM LogEntries ORDER BY TimestampUTC DESC LIMIT 100",
                }
            );
        }

        private float[] GenerateRandomEmbedding(int dimension)
        {
            var embedding = new float[dimension];
            for (int i = 0; i < dimension; i++)
            {
                embedding[i] = (float)(_random.NextDouble() * 2 - 1); // Random value between -1 and 1
            }

            // Normalize to unit length
            float magnitude = (float)Math.Sqrt(embedding.Sum(x => x * x));
            for (int i = 0; i < dimension; i++)
            {
                embedding[i] /= magnitude;
            }

            return embedding;
        }

        #endregion
    }
}
