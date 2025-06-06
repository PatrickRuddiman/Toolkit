using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace AnalyzeLogs.Services;

/// <summary>
/// Service for ingesting log files based on glob patterns
/// </summary>
public class FileIngestionService
{
    private readonly ILogger<FileIngestionService> _logger;

    public FileIngestionService(ILogger<FileIngestionService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Gets log files based on a glob pattern (main entry point)
    /// </summary>
    public async Task<List<string>> GetLogFilesAsync(string pattern)
    {
        var resolvedFiles = await ResolveFilesAsync(pattern);
        return await ValidateFilesAsync(resolvedFiles);
    }

    /// <summary>
    /// Resolves glob patterns to actual file paths
    /// </summary>
    public async Task<List<string>> ResolveFilesAsync(string pattern)
    {
        var files = new List<string>();

        try
        {
            // Handle absolute paths vs relative paths
            var basePath = Path.IsPathRooted(pattern) ? string.Empty : Directory.GetCurrentDirectory();
            
            if (string.IsNullOrEmpty(basePath))
            {
                // For absolute patterns, extract the directory part
                var directoryPart = GetDirectoryPart(pattern);
                if (!string.IsNullOrEmpty(directoryPart))
                {
                    basePath = directoryPart;
                    pattern = pattern.Substring(directoryPart.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                }
                else
                {
                    basePath = Directory.GetCurrentDirectory();
                }
            }

            _logger.LogInformation("Searching for files matching pattern '{Pattern}' in base path '{BasePath}'", 
                pattern, basePath);

            // Simple glob pattern matching (supports * and ? wildcards)
            var regexPattern = "^" + Regex.Escape(pattern)
                .Replace(@"\*\*", "DOUBLESTAR") // Handle ** first
                .Replace(@"\*", "[^/\\\\]*")     // * matches anything except path separators
                .Replace("DOUBLESTAR", ".*")     // ** matches anything including path separators
                .Replace(@"\?", "[^/\\\\]") + "$"; // ? matches single char except path separators
            
            var regex = new Regex(regexPattern, RegexOptions.IgnoreCase);
            
            // If pattern contains directory separators, we need to search recursively
            var searchOption = pattern.Contains("**") || pattern.Contains(Path.DirectorySeparatorChar) || pattern.Contains(Path.AltDirectorySeparatorChar)
                ? SearchOption.AllDirectories 
                : SearchOption.TopDirectoryOnly;

            // Get all files in the search area
            var allFiles = Directory.EnumerateFiles(basePath, "*", searchOption);

            // Filter using glob pattern
            await foreach (var file in ToAsyncEnumerable(allFiles))
            {
                var relativePath = Path.GetRelativePath(basePath, file);
                
                // Normalize path separators for glob matching
                var normalizedPath = relativePath.Replace(Path.DirectorySeparatorChar, '/');
                
                if (regex.IsMatch(normalizedPath) || regex.IsMatch(relativePath))
                {
                    files.Add(file);
                }
            }

            _logger.LogInformation("Found {FileCount} files matching pattern", files.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving file pattern '{Pattern}'", pattern);
        }

        return files;
    }

    /// <summary>
    /// Validates that files exist and are readable
    /// </summary>
    public async Task<List<string>> ValidateFilesAsync(IEnumerable<string> filePaths)
    {
        var validFiles = new List<string>();

        foreach (var filePath in filePaths)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    // Try to read the first few bytes to ensure it's readable
                    using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    var buffer = new byte[1024];
                    await fileStream.ReadAsync(buffer, 0, buffer.Length);
                    
                    validFiles.Add(filePath);
                    _logger.LogDebug("Validated file: {FilePath}", filePath);
                }
                else
                {
                    _logger.LogWarning("File not found: {FilePath}", filePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Cannot read file: {FilePath}", filePath);
            }
        }

        _logger.LogInformation("Validated {ValidCount} out of {TotalCount} files", 
            validFiles.Count, filePaths.Count());

        return validFiles;
    }

    /// <summary>
    /// Gets file information for the resolved files
    /// </summary>
    public async Task<Dictionary<string, FileInfo>> GetFileInfoAsync(IEnumerable<string> filePaths)
    {
        var fileInfos = new Dictionary<string, FileInfo>();

        await Task.Run(() =>
        {
            foreach (var filePath in filePaths)
            {
                try
                {
                    var fileInfo = new FileInfo(filePath);
                    fileInfos[filePath] = fileInfo;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Cannot get info for file: {FilePath}", filePath);
                }
            }
        });

        var totalSize = fileInfos.Values.Sum(f => f.Length);
        _logger.LogInformation("Total size of {FileCount} files: {TotalSize:N0} bytes ({TotalSizeMB:N1} MB)", 
            fileInfos.Count, totalSize, totalSize / (1024.0 * 1024.0));

        return fileInfos;
    }

    /// <summary>
    /// Extracts the directory part from a file pattern
    /// </summary>
    private string GetDirectoryPart(string pattern)
    {
        var lastSeparatorIndex = Math.Max(
            pattern.LastIndexOf(Path.DirectorySeparatorChar),
            pattern.LastIndexOf(Path.AltDirectorySeparatorChar)
        );

        if (lastSeparatorIndex > 0)
        {
            var directoryPart = pattern.Substring(0, lastSeparatorIndex);
            
            // Make sure it's a valid directory (doesn't contain glob patterns)
            if (!directoryPart.Contains('*') && !directoryPart.Contains('?') && Directory.Exists(directoryPart))
            {
                return directoryPart;
            }
        }

        return string.Empty;
    }

    /// <summary>
    /// Converts IEnumerable to IAsyncEnumerable
    /// </summary>
    private static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(IEnumerable<T> enumerable)
    {
        await Task.Yield(); // Make it truly async
        
        foreach (var item in enumerable)
        {
            yield return item;
        }
    }

    /// <summary>
    /// Gets a summary of files to be processed
    /// </summary>
    public string GetFilesSummary(Dictionary<string, FileInfo> fileInfos)
    {
        if (!fileInfos.Any())
            return "No files found.";

        var totalSize = fileInfos.Values.Sum(f => f.Length);
        var totalSizeMB = totalSize / (1024.0 * 1024.0);

        var summary = $"Found {fileInfos.Count} file(s) totaling {totalSizeMB:N1} MB:\n";
        
        foreach (var kvp in fileInfos.OrderBy(x => x.Key))
        {
            var filePath = kvp.Key;
            var fileInfo = kvp.Value;
            var fileName = Path.GetFileName(filePath);
            var fileSizeMB = fileInfo.Length / (1024.0 * 1024.0);
            
            summary += $"  - {fileName} ({fileSizeMB:N1} MB, modified {fileInfo.LastWriteTime:yyyy-MM-dd HH:mm:ss})\n";
        }

        return summary.TrimEnd();
    }
}
