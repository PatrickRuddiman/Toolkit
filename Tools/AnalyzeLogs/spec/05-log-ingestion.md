# Log Ingestion and Parsing

## File Input and Glob Pattern Support

### Accepting Glob Patterns
The CLI accepts file path patterns (glob) for `.log` files as arguments:

```bash
LogAnalyzer "*.log"
LogAnalyzer "logs/**/*.log"
```

**Implementation Details:**
- Use `Directory.EnumerateFiles` or `Directory.GetFiles` with search patterns
- Support recursive patterns (e.g., `**` for all subdirectories)
- Consider using libraries that support advanced glob syntax
- Include validation: gracefully inform user if no files match

### File Reading Considerations
- **Streaming**: Use line-by-line reading (`File.ReadLines` or `StreamReader`) to avoid loading entire files into memory
- **Scalability**: Handle large numbers of files and large file sizes robustly  
- **Parallel Processing**: Incorporate async file I/O for I/O bound operations
- **Ordering**: Be mindful of ordering if logs need to be merged by time later

## Format Detection and Parsing

### Supported Log Formats

#### JSON Logs
- **Detection**: Lines starting with `{` or `[` that can be parsed as JSON
- **Parsing**: Use `System.Text.Json` to deserialize each line into dynamic object
- **Benefits**: Preserves structured fields (timestamp, level, message, etc.)

#### Common Text Logs
- **Examples**: Nginx/Apache access logs, application logs with `[LEVEL] timestamp – message`
- **Parsing**: Use regular expressions and known patterns
- **Configuration**: Support format strings for common patterns
- **Extensibility**: Allow custom parsing rules for known formats

#### Unstructured Logs
- **Fallback Strategy**: When format is not recognizable
- **Basic Parsing**: Attempt to find timestamp substring
- **Default Handling**: Treat whole line as message if no structure detected
- **Goal**: Capture whatever structure is present

### Parsing Pipeline

1. **Format Detection**: Analyze file content to determine log format
2. **Parser Selection**: Choose appropriate parser based on detected format
3. **Line Processing**: Parse each log line according to format rules
4. **Validation**: Basic validation of required fields
5. **Error Handling**: Handle malformed lines gracefully

## Data Normalization

### Common Schema Alignment
All log entries are normalized to a unified `LogEntry` schema:

```csharp
public class LogEntry
{
    public string LogEntryId { get; set; }
    public string AnalysisRunId { get; set; }
    public DateTime Timestamp { get; set; }
    public string ServiceName { get; set; }
    public SeverityLevel Level { get; set; }
    public string Message { get; set; }
    public string CorrelationId { get; set; }
    public string SourceFile { get; set; }
    public int LineNumber { get; set; }
    public string RawLogLine { get; set; }
    public Dictionary<string, object> ParsedFields { get; set; }
    public float[] EmbeddingVector { get; set; }
}
```

### Normalization Process

#### Timestamp Conversion
- Convert all timestamps to consistent format (UTC DateTime)
- Handle different time formats (ISO 8601, epoch, custom formats)
- Use parsing libraries for complex timestamp formats
- Ensure unified timeline for correlation and sequencing

#### Level/Severity Mapping
- Map log levels (DEBUG, INFO, WARN, ERROR, FATAL, etc.) to standard enum
- Handle variations in level naming (e.g., "WARN" and "Warning" both map to `LogLevel.Warning`)
- Support custom level mappings via configuration

#### Service Name Extraction
- Extract or infer service name from log source or content
- Use filename patterns (e.g., `payments-service.log` → Service = "Payments")
- Parse service information from log content when available
- Allow manual service mapping configuration

#### Correlation ID Extraction
- Extract transaction/request IDs if present for cross-service correlation
- Support multiple correlation ID patterns (TraceId, RequestId, etc.)
- Use regex patterns to identify correlation IDs in unstructured text

#### Metadata Handling
- Store additional structured fields in flexible dictionary
- Preserve original data while normalizing core fields
- Support extensibility for format-specific fields

### Verification and Validation

#### Data Quality Checks
- Verify required fields (Timestamp, Message) were extracted
- Count and report unparsed or malformed lines
- Validate data types and ranges
- Generate parsing statistics for analysis quality assessment

#### Error Handling
- Skip malformed lines rather than crashing
- Log parsing errors for troubleshooting
- Maintain count of parsing failures
- Provide detailed error reports for investigation

## Performance Considerations

### Memory Management
- Stream processing for large files
- Batch processing to control memory usage
- Garbage collection optimization
- Progress reporting for long operations

### Parallel Processing
- Concurrent file reading when safe
- Parallel parsing of independent log lines
- Balanced thread usage to avoid resource contention
- Proper error isolation between parallel tasks
