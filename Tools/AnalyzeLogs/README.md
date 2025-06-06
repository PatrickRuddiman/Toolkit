# AnalyzeLogs

AI-powered log analysis tool for microservice systems that provides intelligent anomaly detection, pattern recognition, and coherence analysis across distributed systems.

## Features

- **Multi-format Log Parsing**: Supports JSON, structured text, Apache access logs, and unstructured log formats
- **AI-Powered Analysis**: Uses OpenAI embeddings and Microsoft Fabric for intelligent log analysis
- **Anomaly Detection**: Identifies unusual patterns and outliers in log data
- **Coherence Analysis**: Detects inconsistencies and correlation issues across services
- **Semantic Clustering**: Groups similar log entries using embeddings
- **Cross-Service Correlation**: Analyzes relationships between different services
- **Flexible Input**: Supports glob patterns for analyzing multiple log files
- **Rich Reporting**: Generates comprehensive analysis reports with metrics and insights

## Quick Start

### 1. Setup API Key

Before using AnalyzeLogs, configure your OpenAI API key:

```bash
AnalyzeLogs setup
```

This will guide you through:
- Configuring your OpenAI API key
- Testing the API connection
- Viewing current configuration
- Managing stored credentials

The API key is securely stored in your user profile at:
- Windows: `%APPDATA%\AnalyzeLogs\config.json`

### 2. Analyze Logs

Analyze log files in your directory:

```bash
# Analyze all .log files in current directory
AnalyzeLogs

# Analyze specific log files
AnalyzeLogs --path "logs/*.json"

# Analyze with verbose output
AnalyzeLogs --path "logs/**/*.log" --verbose

# Save report to file
AnalyzeLogs --path "logs/*.log" --output "report.txt"
```

### 3. Use Subcommands (Optional)

For explicit command structure:

```bash
# Configure API keys and settings
AnalyzeLogs setup

# Analyze logs (same as default behavior)
AnalyzeLogs analyze --path "logs/*.log" --verbose
```

## Command Line Options

### Global Options

- `--path <pattern>` - Glob pattern for log files (default: "*.log")
- `--output <file>` - Output file path (prints to console if not specified)
- `--verbose` - Enable verbose logging
- `--model <model>` - AI model to use (default: "gpt-4o-mini")
- `--coherence` - Enable coherence analysis (default: true)
- `--anomaly` - Enable anomaly detection (default: true)
- `--tagging` - Enable log tagging (default: true)
- `--embeddings` - Enable semantic embeddings analysis (default: true)

### Commands

- `setup` - Configure API keys and settings
- `analyze` - Analyze log files (default if no command specified)

## Configuration

### API Key Sources (Priority Order)

1. **Environment Variable**: `OPENAI_API_KEY`
2. **Configuration File**: Stored via `AnalyzeLogs setup`

### Configuration Management

```bash
# Configure new API key
AnalyzeLogs setup

# View current configuration
AnalyzeLogs setup
# Select option 2: "View current configuration"

# Test API connectivity
AnalyzeLogs setup
# Select option 3: "Test API key"

# Remove stored configuration
AnalyzeLogs setup
# Select option 4: "Remove configuration"
```

## Sample Output

```
================================================================================
LOG ANALYSIS REPORT
================================================================================

SUMMARY
----------------------------------------
Analysis Date: 2025-06-06 13:19:14 UTC
Log Files Analyzed: 3
Total Log Entries: 1,247
Time Range: 24.5 hours
Services Monitored: 5
Anomalies Found: 7
Correlations Detected: 12
Overall Health: ⚠️ ISSUES DETECTED

SERVICE METRICS
----------------------------------------
Service: API Gateway
  Total Entries: 542
  Error Count: 23
  Error Rate: 4.24%
  Request Rate: 156.3 requests/min

Service: Database
  Total Entries: 385
  Error Count: 5
  Error Rate: 1.30%
  Request Rate: 89.2 requests/min

ANOMALIES DETECTED
----------------------------------------
🔍 High Error Rate Spike
  Service: API Gateway
  Time: 2025-06-06 08:15:30 UTC
  Description: Error rate increased to 15.2% (normal: 2.1%)
  
🔍 Response Time Anomaly
  Service: Database
  Time: 2025-06-06 09:22:45 UTC
  Description: Average response time: 2.3s (normal: 120ms)
```

## Supported Log Formats

### JSON Logs
```json
{
  "timestamp": "2024-01-15T10:30:15Z",
  "level": "ERROR",
  "service": "api-gateway",
  "message": "Connection timeout to user service",
  "userId": "usr_12345",
  "requestId": "req_98765",
  "httpStatus": 500,
  "responseTime": 5000
}
```

### Structured Text Logs
```
2024-01-15 10:30:15 [ERROR] api-gateway: Connection timeout to user service (userId=usr_12345, requestId=req_98765)
```

### Apache Access Logs
```
192.168.1.100 - - [15/Jan/2024:10:30:15 +0000] "GET /api/users/12345 HTTP/1.1" 500 2326 "https://example.com" "Mozilla/5.0..."
```

## Requirements

- .NET 8.0 or later
- OpenAI API key
- Internet connection for AI analysis

## Installation

1. Download the latest release
2. Extract to desired location
3. Run `AnalyzeLogs setup` to configure API key
4. Start analyzing logs!

## Examples

### Basic Analysis
```bash
# Analyze logs in current directory
AnalyzeLogs

# Analyze specific service logs
AnalyzeLogs --path "logs/api-gateway-*.log"
```

### Advanced Analysis
```bash
# Full analysis with all features
AnalyzeLogs --path "logs/**/*.json" --verbose --output "analysis.txt"

# Focus on anomaly detection only
AnalyzeLogs --path "logs/*.log" --coherence false --tagging false
```

### Multiple Log Sources
```bash
# Analyze all log files recursively
AnalyzeLogs --path "**/*.log"

# Analyze mixed formats
AnalyzeLogs --path "logs/*.{log,json}"
```

## Troubleshooting

### API Key Issues
- Run `AnalyzeLogs setup` and select "Test API key" to verify connectivity
- Ensure your OpenAI API key has sufficient credits
- Check your internet connection

### No Logs Found
- Verify the `--path` pattern matches your log files
- Use absolute paths if relative paths aren't working
- Check file permissions

### Performance
- Large log files are processed in chunks
- Use `--embeddings false` to skip semantic analysis for faster processing
- Consider filtering logs by date range before analysis

## License

[Add your license information here]