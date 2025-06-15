# Architecture and Implementation Guidelines

## Modular Architecture Design

### Core Module Structure
The application is organized into logical modules with clear separation of concerns:

```
AnalyzeLogs.CLI/
├── src/
│   ├── Commands/           # CLI command implementations
│   ├── Data/              # Data access and persistence
│   ├── Models/            # Domain models and entities
│   ├── Patterns/          # AI prompt patterns
│   ├── Repositories/      # Data repository implementations
│   ├── Services/          # Business logic services
│   │   ├── Analysis/      # AI analysis services
│   │   ├── Data/          # Data management services
│   │   ├── Ingestion/     # Log ingestion services
│   │   ├── Query/         # Query processing services
│   │   └── Reporting/     # Report generation services
│   └── Utilities/         # Helper classes and utilities
```

### Module Responsibilities

#### Log Ingestion Module
**Purpose**: Handle reading files and producing structured log entries
**Key Classes**:
- `ILogReader`: Interface for reading log files with glob pattern support
- `ILogParser`: Interface for parsing different log formats
- `JsonLogParser`, `TextLogParser`: Format-specific parser implementations
- `LogNormalizationService`: Converts parsed data to unified schema

```csharp
public interface ILogParser
{
    bool CanParse(string logLine, string fileName);
    LogEntry Parse(string logLine, string fileName, int lineNumber);
}

public class LogIngestionService
{
    private readonly IEnumerable<ILogParser> _parsers;
    private readonly LogNormalizationService _normalizer;
    
    public async Task<List<LogEntry>> IngestLogsAsync(
        string globPattern, string analysisRunId)
    {
        var files = ResolveGlobPattern(globPattern);
        var logEntries = new List<LogEntry>();
        
        foreach (var file in files)
        {
            await foreach (var entry in ProcessFileAsync(file, analysisRunId))
            {
                logEntries.Add(entry);
            }
        }
        
        return logEntries;
    }
}
```

#### Data Storage Module
**Purpose**: Manage in-memory collections and database operations
**Key Classes**:
- `LogAnalyzerDbContext`: Entity Framework database context
- `IProjectRepository`, `IAnalysisRunRepository`, `ILogEntryRepository`: Data repositories
- `VectorSearchService`: Manage embedding storage and similarity queries

#### AI Analysis Module
**Purpose**: Encapsulate LLM interactions and specialized analysis patterns
**Key Classes**:
- `OpenAIService`: Direct OpenAI API integration
- `ILogAnalysisPattern`: Interface for analysis patterns
- `AnomalyDetectionPattern`, `CoherenceAnalysisPattern`, `LogTaggingPattern`: Specialized analyzers

```csharp
public interface ILogAnalysisPattern
{
    string PatternName { get; }
    Task<IAnalysisResult> AnalyzeAsync(List<LogEntry> logSegment);
}

public class AnomalyDetectionPattern : ILogAnalysisPattern
{
    public string PatternName => "analyze_log_anomalies";
    
    public async Task<IAnalysisResult> AnalyzeAsync(List<LogEntry> logSegment)
    {
        var pattern = await _patternLoader.LoadPatternAsync(PatternName);
        var context = BuildAnalysisContext(logSegment);
        var response = await _openAiService.AnalyzeAsync(pattern, context);
        
        return ParseAnomalyResponse(response);
    }
}
```

#### Reporting Module
**Purpose**: Generate formatted reports and handle output
**Key Classes**:
- `ReportGenerator`: Main report generation orchestrator
- `DocFXReportFormatter`: DocFX-specific markdown formatting
- `DiagramGenerator`: Mermaid diagram generation using AI
- `DataExportService`: Raw data file generation

## Extensibility Framework

### Plugin Architecture
The system supports easy addition of new functionality through interfaces and dependency injection:

#### Adding New Log Formats
```csharp
public class CustomLogParser : ILogParser
{
    public bool CanParse(string logLine, string fileName)
    {
        // Implement format detection logic
        return logLine.StartsWith("[CUSTOM]");
    }
    
    public LogEntry Parse(string logLine, string fileName, int lineNumber)
    {
        // Implement custom parsing logic
        return new LogEntry { /* parsed data */ };
    }
}

// Register in DI container
services.AddTransient<ILogParser, CustomLogParser>();
```

#### Adding New Analysis Patterns
```csharp
public class SecurityAnalysisPattern : ILogAnalysisPattern
{
    public string PatternName => "analyze_security_threats";
    
    public async Task<IAnalysisResult> AnalyzeAsync(List<LogEntry> logSegment)
    {
        // Implement security-specific analysis
        return new SecurityAnalysisResult();
    }
}
```

### Configuration-Driven Analysis
Control which analyses to perform through configuration:

```json
{
  "analysis": {
    "enabledPatterns": [
      "analyze_log_anomalies",
      "analyze_coherence", 
      "tag_logs",
      "analyze_security_threats"
    ],
    "skipEmbeddings": false,
    "batchSize": 100
  }
}
```

## Best Practices Implementation

### Resource Management and Performance

#### Asynchronous Processing
```csharp
public class LogProcessingService
{
    private readonly SemaphoreSlim _semaphore;
    private readonly IMemoryCache _cache;
    
    public async Task<List<LogEntry>> ProcessLogsAsync(
        IEnumerable<string> files, CancellationToken cancellationToken)
    {
        var tasks = files.Select(async file =>
        {
            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                return await ProcessFileAsync(file, cancellationToken);
            }
            finally
            {
                _semaphore.Release();
            }
        });
        
        var results = await Task.WhenAll(tasks);
        return results.SelectMany(r => r).ToList();
    }
}
```

#### Memory Management
```csharp
public class EmbeddingService
{
    private readonly LRUCache<string, float[]> _embeddingCache;
    private readonly IMemoryMonitor _memoryMonitor;
    
    public async Task<float[]> GetEmbeddingAsync(string text)
    {
        // Check memory usage before processing
        if (_memoryMonitor.AvailableMemory < MinimumMemoryThreshold)
        {
            await _embeddingCache.ClearOldestAsync();
        }
        
        if (_embeddingCache.TryGetValue(text, out var cached))
            return cached;
            
        var embedding = await GenerateEmbeddingAsync(text);
        _embeddingCache.Set(text, embedding);
        
        return embedding;
    }
}
```

### Error Handling and Resilience

#### Graceful Degradation
```csharp
public class ResilientAnalysisService
{
    public async Task<AnalysisResult> AnalyzeAsync(List<LogEntry> logs)
    {
        var result = new AnalysisResult();
        
        try
        {
            result.AnomalyAnalysis = await _anomalyDetector.AnalyzeAsync(logs);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Anomaly detection failed, continuing with other analyses");
            result.AnomalyAnalysis = new FailedAnalysisResult(ex.Message);
        }
        
        try
        {
            result.CoherenceAnalysis = await _coherenceAnalyzer.AnalyzeAsync(logs);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Coherence analysis failed");
            result.CoherenceAnalysis = new FailedAnalysisResult(ex.Message);
        }
        
        return result;
    }
}
```

#### API Rate Limiting and Retry Logic
```csharp
public class OpenAIService
{
    private readonly TokenBucket _rateLimiter;
    private readonly RetryPolicy _retryPolicy;
    
    public async Task<string> CallApiAsync(string prompt)
    {
        await _rateLimiter.WaitForTokenAsync();
        
        return await _retryPolicy.ExecuteAsync(async () =>
        {
            try
            {
                return await _httpClient.PostAsync(/* API call */);
            }
            catch (RateLimitExceededException)
            {
                await Task.Delay(CalculateBackoffDelay());
                throw; // Will trigger retry
            }
        });
    }
}
```

### Security and Privacy

#### Data Sanitization
```csharp
public class LogSanitizer
{
    private readonly List<IDataSanitizer> _sanitizers;
    
    public LogEntry Sanitize(LogEntry entry)
    {
        var sanitized = entry.Clone();
        
        foreach (var sanitizer in _sanitizers)
        {
            sanitized = sanitizer.Sanitize(sanitized);
        }
        
        return sanitized;
    }
}

public class EmailSanitizer : IDataSanitizer
{
    private readonly Regex _emailRegex = new Regex(@"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b");
    
    public LogEntry Sanitize(LogEntry entry)
    {
        entry.Message = _emailRegex.Replace(entry.Message, "[EMAIL]");
        return entry;
    }
}
```

#### API Key Security
```csharp
public class SecureConfigurationService
{
    public string GetApiKey(string keyName)
    {
        // Try environment variable first
        var envKey = Environment.GetEnvironmentVariable($"LOGANALYZER_{keyName}");
        if (!string.IsNullOrEmpty(envKey))
            return envKey;
            
        // Try secure credential store
        var credentialKey = GetFromCredentialStore(keyName);
        if (!string.IsNullOrEmpty(credentialKey))
            return credentialKey;
            
        throw new InvalidOperationException($"API key '{keyName}' not found in secure configuration");
    }
}
```

## Testing Strategy

### Unit Testing Framework
```csharp
[TestClass]
public class LogParsingTests
{
    private readonly JsonLogParser _parser = new();
    
    [TestMethod]
    public void Parse_ValidJsonLog_ReturnsLogEntry()
    {
        // Arrange
        var logLine = """{"timestamp":"2024-06-15T14:30:00Z","level":"ERROR","message":"Test error"}""";
        
        // Act
        var result = _parser.Parse(logLine, "test.log", 1);
        
        // Assert
        Assert.AreEqual(DateTime.Parse("2024-06-15T14:30:00Z"), result.Timestamp);
        Assert.AreEqual(SeverityLevel.Error, result.Level);
        Assert.AreEqual("Test error", result.Message);
    }
}
```

### Integration Testing
```csharp
[TestClass]
public class AnalysisIntegrationTests
{
    private readonly TestServiceProvider _serviceProvider;
    
    [TestInitialize]
    public void Setup()
    {
        _serviceProvider = TestServiceProvider.Create(services =>
        {
            services.AddSingleton<IOpenAIService, MockOpenAIService>();
            services.AddDbContext<LogAnalyzerDbContext>(options =>
                options.UseInMemoryDatabase("TestDb"));
        });
    }
    
    [TestMethod]
    public async Task AnalyzeProject_WithSampleLogs_ProducesExpectedResults()
    {
        // Arrange
        var analysisService = _serviceProvider.GetService<AnalysisService>();
        var sampleLogs = LoadSampleLogs("sample_error_logs.json");
        
        // Act
        var result = await analysisService.AnalyzeAsync(sampleLogs);
        
        // Assert
        Assert.IsTrue(result.AnomaliesFound.Count > 0);
        Assert.IsNotNull(result.Summary);
    }
}
```

### Performance Testing
```csharp
[TestClass]
public class PerformanceTests
{
    [TestMethod]
    public async Task ProcessLargeBatch_Within_PerformanceThreshold()
    {
        // Arrange
        var service = new LogIngestionService(/* dependencies */);
        var largeBatch = GenerateTestLogs(10000);
        
        // Act
        var stopwatch = Stopwatch.StartNew();
        var result = await service.ProcessLogsAsync(largeBatch);
        stopwatch.Stop();
        
        // Assert
        Assert.IsTrue(stopwatch.ElapsedMilliseconds < 30000, "Processing took too long");
        Assert.AreEqual(10000, result.Count);
    }
}
```
