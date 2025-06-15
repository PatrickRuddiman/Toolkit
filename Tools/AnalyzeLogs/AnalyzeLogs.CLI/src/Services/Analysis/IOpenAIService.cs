using AnalyzeLogs.Models;

namespace AnalyzeLogs.Services.Analysis;

/// <summary>
/// Interface for the OpenAI service used for AI-powered analysis and processing.
/// </summary>
public interface IOpenAIService
{
    /// <summary>
    /// Analyzes log entries for anomalies.
    /// </summary>
    /// <param name="logEntries">The log entries to analyze.</param>
    /// <returns>A string containing the analysis results.</returns>
    Task<string> AnalyzeAnomaliesAsync(IEnumerable<LogEntry> logEntries);

    /// <summary>
    /// Analyzes the coherence of log entries.
    /// </summary>
    /// <param name="logEntries">The log entries to analyze.</param>
    /// <returns>A string containing the coherence analysis.</returns>
    Task<string> AnalyzeCoherenceAsync(IEnumerable<LogEntry> logEntries);

    /// <summary>
    /// Generates tags for log entries.
    /// </summary>
    /// <param name="logEntries">The log entries to tag.</param>
    /// <returns>A string containing the tagging results in JSON format.</returns>
    Task<string> TagLogsAsync(IEnumerable<LogEntry> logEntries);

    /// <summary>
    /// Summarizes log entries.
    /// </summary>
    /// <param name="logEntries">The log entries to summarize.</param>
    /// <returns>A string containing the summary.</returns>
    Task<string> SummarizeLogsAsync(IEnumerable<LogEntry> logEntries);

    /// <summary>
    /// Researches an anomaly using the LLM.
    /// </summary>
    /// <param name="anomalyLogEntry">The anomalous log entry.</param>
    /// <param name="contextLogEntries">The surrounding context log entries.</param>
    /// <param name="anomalyDescription">A description of the anomaly.</param>
    /// <returns>A string containing the research results.</returns>
    Task<string> ResearchAnomalyAsync(
        LogEntry anomalyLogEntry,
        IEnumerable<LogEntry> contextLogEntries,
        string anomalyDescription
    );

    /// <summary>
    /// Generates a Mermaid diagram based on log data.
    /// </summary>
    /// <param name="logEntries">The log entries to visualize.</param>
    /// <param name="diagramType">The type of diagram to generate (sequence, state, etc.).</param>
    /// <returns>A string containing the Mermaid diagram code.</returns>
    Task<string> GenerateDiagramAsync(IEnumerable<LogEntry> logEntries, string diagramType);

    /// <summary>
    /// Parses a log line into a structured LogEntry using AI.
    /// </summary>
    /// <param name="logLine">The raw log line to parse.</param>
    /// <param name="sourcePath">The path to the source log file.</param>
    /// <param name="sourceFile">The name of the source log file.</param>
    /// <returns>A JSON string representing the parsed LogEntry.</returns>
    Task<string> ParseLogAsync(string logLine, string sourcePath, string sourceFile);

    /// <summary>
    /// Generates text embeddings for a list of text strings.
    /// </summary>
    /// <param name="texts">The text strings to embed.</param>
    /// <returns>A list of float arrays representing the embeddings.</returns>
    Task<IList<float[]>> GenerateEmbeddingsAsync(IEnumerable<string> texts);

    /// <summary>
    /// Generates text embeddings for a list of log entries.
    /// </summary>
    /// <param name="logEntries">The log entries to embed.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A list of float arrays representing the embeddings.</returns>
    Task<IList<float[]>> GenerateEmbeddingsAsync(
        IEnumerable<LogEntry> logEntries,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Interprets a natural language query and converts it to a structured query intent.
    /// </summary>
    /// <param name="query">The natural language query.</param>
    /// <param name="projectContext">Optional context about the current project.</param>
    /// <param name="sessionContext">Optional context about the current session.</param>
    /// <returns>A JSON string representing the structured query intent.</returns>
    Task<string> InterpretQueryAsync(
        string query,
        string? projectContext = null,
        string? sessionContext = null
    );
}
