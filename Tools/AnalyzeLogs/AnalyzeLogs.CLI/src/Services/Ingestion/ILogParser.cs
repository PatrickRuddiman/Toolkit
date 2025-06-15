using AnalyzeLogs.Models;

namespace AnalyzeLogs.Services.Ingestion;

/// <summary>
/// Interface for log parsing services.
/// </summary>
public interface ILogParser
{
    /// <summary>
    /// Parses a log line into a structured LogEntry.
    /// </summary>
    /// <param name="line">The raw log line.</param>
    /// <param name="sourcePath">The path to the source log file.</param>
    /// <param name="sourceFile">The name of the source log file.</param>
    /// <returns>A parsed log entry or null if parsing failed.</returns>
    LogEntry? Parse(string line, string sourcePath, string sourceFile);
}
