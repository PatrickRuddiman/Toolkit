namespace AnalyzeLogs.Services.Ingestion;

/// <summary>
/// Factory for creating log parsers based on log format.
/// </summary>
public interface ILogParserFactory
{
    /// <summary>
    /// Gets the appropriate parser for the log line.
    /// </summary>
    /// <param name="line">The log line to parse.</param>
    /// <returns>A parser capable of parsing the log line.</returns>
    ILogParser GetParser(string line);
}
