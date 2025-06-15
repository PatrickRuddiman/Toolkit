using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AnalyzeLogs.Data;
using AnalyzeLogs.Models;
using AnalyzeLogs.Services.Analysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AnalyzeLogs.Services.Ingestion
{
    /// <summary>
    /// Service for ingesting log files into the system.
    /// </summary>
    public interface ILogIngestionService
    {
        /// <summary>
        /// Ingests log files matching the specified glob pattern for a session.
        /// </summary>
        /// <param name="sessionId">The session ID to associate with the ingested logs.</param>
        /// <param name="globPattern">The glob pattern to match log files.</param>
        /// <param name="generateEmbeddings">Whether to generate embeddings for the log entries.</param>
        /// <param name="progress">An optional progress reporter.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A tuple containing the number of files processed and the number of log entries ingested.</returns>
        Task<(int FilesProcessed, int EntriesIngested)> IngestLogFilesAsync(
            Guid sessionId,
            string globPattern,
            bool generateEmbeddings = true,
            IProgress<(int FilesProcessed, int EntriesIngested, string CurrentFile)>? progress =
                null,
            CancellationToken cancellationToken = default
        );
    }

    /// <summary>
    /// Implementation of the log ingestion service.
    /// </summary>
    public class LogIngestionService : ILogIngestionService
    {
        private readonly LogAnalyzerDbContext _dbContext;
        private readonly ILogParserFactory _parserFactory;
        private readonly IOpenAIService _openAIService;
        private readonly IVectorSearchService _vectorSearchService;
        private readonly ILogger<LogIngestionService> _logger;
        private readonly int _batchSize = 100; // Number of log entries to process in a batch

        /// <summary>
        /// Initializes a new instance of the <see cref="LogIngestionService"/> class.
        /// </summary>
        /// <param name="dbContext">The database context.</param>
        /// <param name="parserFactory">The log parser factory.</param>
        /// <param name="openAIService">The OpenAI service for generating embeddings.</param>
        /// <param name="vectorSearchService">The vector search service for storing embeddings.</param>
        /// <param name="logger">The logger to use.</param>
        public LogIngestionService(
            LogAnalyzerDbContext dbContext,
            ILogParserFactory parserFactory,
            IOpenAIService openAIService,
            IVectorSearchService vectorSearchService,
            ILogger<LogIngestionService> logger
        )
        {
            _dbContext = dbContext;
            _parserFactory = parserFactory;
            _openAIService = openAIService;
            _vectorSearchService = vectorSearchService;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<(int FilesProcessed, int EntriesIngested)> IngestLogFilesAsync(
            Guid sessionId,
            string globPattern,
            bool generateEmbeddings = true,
            IProgress<(int FilesProcessed, int EntriesIngested, string CurrentFile)>? progress =
                null,
            CancellationToken cancellationToken = default
        )
        {
            // Verify that the session exists
            var session = await _dbContext.Sessions.FindAsync(
                new object[] { sessionId },
                cancellationToken
            );

            if (session == null)
            {
                throw new ArgumentException(
                    $"Session with ID {sessionId} not found.",
                    nameof(sessionId)
                );
            }

            // Update session status
            session.Status = "Ingesting";
            await _dbContext.SaveChangesAsync(cancellationToken);

            int filesProcessed = 0;
            int entriesIngested = 0;

            try
            {
                // Resolve the glob pattern to file paths
                var filePaths = ResolveGlobPattern(globPattern);

                if (filePaths.Count == 0)
                {
                    _logger.LogWarning(
                        "No files matched the glob pattern: {GlobPattern}",
                        globPattern
                    );

                    // Update session with results
                    session.Status = "Completed";
                    session.LogFileCount = 0;
                    session.AnalyzedLogEntryCount = 0;
                    session.RawInputGlobPattern = globPattern;
                    await _dbContext.SaveChangesAsync(cancellationToken);

                    return (0, 0);
                }

                _logger.LogInformation(
                    "Found {FileCount} files matching pattern {GlobPattern}",
                    filePaths.Count,
                    globPattern
                );

                // Process each log file
                foreach (var filePath in filePaths)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    string fileName = Path.GetFileName(filePath);
                    _logger.LogInformation("Processing file: {FilePath}", filePath);

                    // Report progress
                    progress?.Report((filesProcessed, entriesIngested, filePath));

                    // Process the file
                    int fileEntries = await ProcessLogFileAsync(
                        sessionId,
                        filePath,
                        cancellationToken
                    );
                    entriesIngested += fileEntries;
                    filesProcessed++;

                    _logger.LogInformation(
                        "Processed {EntryCount} entries from file {FileName}",
                        fileEntries,
                        fileName
                    );

                    // Report progress
                    progress?.Report((filesProcessed, entriesIngested, filePath));
                }

                // Generate embeddings if requested
                if (generateEmbeddings && entriesIngested > 0)
                {
                    _logger.LogInformation(
                        "Generating embeddings for {EntryCount} log entries",
                        entriesIngested
                    );

                    // Update session status
                    session.Status = "Generating Embeddings";
                    await _dbContext.SaveChangesAsync(cancellationToken);

                    await GenerateEmbeddingsForSessionAsync(sessionId, cancellationToken);
                }

                // Update session with results
                session.Status = "Completed";
                session.LogFileCount = filesProcessed;
                session.AnalyzedLogEntryCount = entriesIngested;
                session.RawInputGlobPattern = globPattern;
                session.EndTime = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync(cancellationToken);

                return (filesProcessed, entriesIngested);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error ingesting log files");

                // Update session status
                session.Status = "Failed";
                await _dbContext.SaveChangesAsync(cancellationToken);

                throw;
            }
        }

        /// <summary>
        /// Processes a single log file and saves the entries to the database.
        /// </summary>
        /// <param name="sessionId">The session ID to associate with the log entries.</param>
        /// <param name="filePath">The path to the log file.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>The number of log entries processed.</returns>
        private async Task<int> ProcessLogFileAsync(
            Guid sessionId,
            string filePath,
            CancellationToken cancellationToken
        )
        {
            int entriesProcessed = 0;
            var batch = new List<LogEntry>();
            string fileName = Path.GetFileName(filePath);

            try
            {
                // Read the file line by line
                using var reader = new StreamReader(filePath);
                string? line;

                while (
                    (line = await reader.ReadLineAsync()) != null
                    && !cancellationToken.IsCancellationRequested
                )
                {
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    // Get the appropriate parser for this log line
                    var parser = _parserFactory.GetParser(line);

                    // Parse the log line
                    var logEntry = parser.Parse(line, filePath, fileName);
                    if (logEntry != null)
                    {
                        // Associate with the session
                        logEntry.SessionId = sessionId;

                        batch.Add(logEntry);
                        entriesProcessed++;

                        // Process in batches to avoid memory issues
                        if (batch.Count >= _batchSize)
                        {
                            await SaveBatchAsync(batch, cancellationToken);
                            batch.Clear();
                        }
                    }
                }

                // Save any remaining entries
                if (batch.Count > 0)
                {
                    await SaveBatchAsync(batch, cancellationToken);
                }

                return entriesProcessed;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing log file {FilePath}", filePath);

                // Try to save any entries that were processed
                if (batch.Count > 0)
                {
                    await SaveBatchAsync(batch, cancellationToken);
                }

                return entriesProcessed;
            }
        }

        /// <summary>
        /// Saves a batch of log entries to the database.
        /// </summary>
        /// <param name="entries">The log entries to save.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        private async Task SaveBatchAsync(
            List<LogEntry> entries,
            CancellationToken cancellationToken
        )
        {
            try
            {
                // Process service entities
                foreach (var entry in entries.Where(e => e.Service != null))
                {
                    // Check if this service already exists in the database
                    string serviceName = entry.Service!.ServiceName;
                    var existingService = await _dbContext.Services.FirstOrDefaultAsync(
                        s => s.ServiceName == serviceName,
                        cancellationToken
                    );

                    if (existingService != null)
                    {
                        // Use the existing service entity
                        entry.ServiceId = existingService.ServiceId;
                        entry.Service = null; // Remove the navigation property to avoid creating a new entity
                    }
                }

                // Add all entries to the context
                await _dbContext.LogEntries.AddRangeAsync(entries, cancellationToken);

                // Save changes
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving batch of {Count} log entries", entries.Count);
                throw;
            }
        }

        /// <summary>
        /// Generates embeddings for all log entries in a session.
        /// </summary>
        /// <param name="sessionId">The session ID.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        private async Task GenerateEmbeddingsForSessionAsync(
            Guid sessionId,
            CancellationToken cancellationToken
        )
        {
            try
            {
                int totalProcessed = 0;
                bool hasMore = true;
                int skip = 0;

                while (hasMore && !cancellationToken.IsCancellationRequested)
                {
                    // Get a batch of log entries that don't have embeddings yet
                    var entries = await _dbContext
                        .LogEntries.Where(e =>
                            e.SessionId == sessionId && e.EmbeddingVector == null
                        )
                        .OrderBy(e => e.LogEntryId)
                        .Skip(skip)
                        .Take(_batchSize)
                        .ToListAsync(cancellationToken);

                    if (entries.Count == 0)
                    {
                        hasMore = false;
                        continue;
                    }

                    // Generate embeddings for this batch
                    var embeddings = await _openAIService.GenerateEmbeddingsAsync(
                        entries,
                        cancellationToken
                    );

                    // Store embeddings
                    if (embeddings.Count > 0)
                    {
                        int storedCount = await _vectorSearchService.StoreEmbeddingsAsync(
                            embeddings,
                            cancellationToken
                        );
                        totalProcessed += storedCount;

                        _logger.LogInformation(
                            "Generated and stored embeddings for {Count} log entries",
                            storedCount
                        );
                    }

                    skip += _batchSize;
                }

                _logger.LogInformation(
                    "Completed embedding generation for {Count} log entries",
                    totalProcessed
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error generating embeddings for session {SessionId}",
                    sessionId
                );
                // Continue without embeddings
            }
        }

        /// <summary>
        /// Resolves a glob pattern to file paths.
        /// </summary>
        /// <param name="globPattern">The glob pattern to resolve.</param>
        /// <returns>A list of file paths matching the pattern.</returns>
        private List<string> ResolveGlobPattern(string globPattern)
        {
            try
            {
                // Handle ~ in path (home directory)
                if (globPattern.StartsWith("~"))
                {
                    string homeDir = Environment.GetFolderPath(
                        Environment.SpecialFolder.UserProfile
                    );
                    globPattern = Path.Combine(homeDir, globPattern[1..]);
                }

                // If the pattern doesn't contain wildcards, check if it's a direct file path
                if (!globPattern.Contains("*") && !globPattern.Contains("?"))
                {
                    // Check if it's a directory
                    if (Directory.Exists(globPattern))
                    {
                        // If it's a directory, include all .log files in it
                        return Directory
                            .GetFiles(globPattern, "*.log", SearchOption.TopDirectoryOnly)
                            .ToList();
                    }

                    // If it's a file, return it directly
                    if (File.Exists(globPattern))
                    {
                        return new List<string> { globPattern };
                    }

                    // Not found
                    return new List<string>();
                }

                // Get the directory part and the file pattern part
                string? directory = Path.GetDirectoryName(globPattern);
                string filePattern = Path.GetFileName(globPattern);

                // Handle empty directory (pattern is in current directory)
                if (string.IsNullOrEmpty(directory))
                {
                    directory = ".";
                }

                // Handle recursive patterns
                bool recursive = directory.Contains("**");
                SearchOption searchOption = recursive
                    ? SearchOption.AllDirectories
                    : SearchOption.TopDirectoryOnly;

                // Remove the ** from the directory path
                if (recursive)
                {
                    directory = directory.Replace("**", "*");
                }

                // Get all matching directories
                var matchingDirs = new List<string>();
                if (directory.Contains("*") || directory.Contains("?"))
                {
                    // Directory has wildcards, so we need to resolve them
                    string baseDir = Path.GetDirectoryName(directory) ?? ".";
                    string dirPattern = Path.GetFileName(directory);

                    // Get all matching directories
                    try
                    {
                        var dirs = Directory.GetDirectories(baseDir, dirPattern, searchOption);
                        matchingDirs.AddRange(dirs);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(
                            ex,
                            "Error resolving directory pattern: {Pattern}",
                            directory
                        );
                    }

                    // Add the base dir itself if it exists
                    if (Directory.Exists(baseDir))
                    {
                        matchingDirs.Add(baseDir);
                    }
                }
                else
                {
                    // Directory has no wildcards, so just use it directly
                    if (Directory.Exists(directory))
                    {
                        matchingDirs.Add(directory);
                    }
                }

                // Get all matching files from each matching directory
                var matchingFiles = new List<string>();
                foreach (var dir in matchingDirs)
                {
                    try
                    {
                        var files = Directory.GetFiles(dir, filePattern, searchOption);
                        matchingFiles.AddRange(files);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(
                            ex,
                            "Error getting files from directory {Directory} with pattern {Pattern}",
                            dir,
                            filePattern
                        );
                    }
                }

                return matchingFiles;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resolving glob pattern: {Pattern}", globPattern);
                return new List<string>();
            }
        }
    }
}
