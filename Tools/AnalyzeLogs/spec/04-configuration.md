# Global Configuration System

## Configuration Overview

A setup command provides configuration of global settings for the LogAnalyzer tool. This allows users to set default paths, AI configurations, and other application-wide settings that apply across all projects and analysis runs.

## Global Settings

### Core Configuration Options

#### Database Location
- **Default Path**: `~/.loganalyzer/data/loganalysis.db`
- **Environment Variable**: `LOGANALYZER_DB_PATH`
- **Description**: The default path for the SQLite database file

#### Default AI Configuration
- **Setting**: `DefaultAiConfigId`
- **Description**: Global default AI configuration to use if a project doesn't specify one
- **Fallback**: Uses built-in defaults if not configured

#### Application Log Level
- **Options**: DEBUG, INFO, WARNING, ERROR
- **Description**: Verbosity level for the LogAnalyzer tool's own operational logs
- **Default**: INFO

#### Default Report Output Directory
- **Setting**: `DefaultReportOutputDirectory`
- **Description**: Default directory where reports are saved if `--output-path` is not specified
- **Default**: `./reports/`

## Configuration File

### File Location
- **Linux/macOS**: `~/.loganalyzer/config.json`
- **Windows**: `%APPDATA%/LogAnalyzer/config.json`

### Configuration Schema

```json
{
  "database": {
    "defaultPath": "~/.loganalyzer/data/loganalysis.db",
    "connectionTimeout": 30
  },
  "ai": {
    "defaultProvider": "openai",
    "defaultModel": "gpt-4",
    "apiKey": "", // Read from environment variable
    "maxTokens": 4000,
    "temperature": 0.1
  },
  "logging": {
    "level": "INFO",
    "logFile": "~/.loganalyzer/logs/app.log",
    "maxFileSizeMB": 10,
    "maxFiles": 5
  },
  "reports": {
    "defaultOutputDirectory": "./reports/",
    "defaultFormat": "docfx",
    "includeRawData": true
  },
  "performance": {
    "maxConcurrentFiles": 5,
    "batchSize": 1000,
    "embeddingCacheSize": 10000
  }
}
```

## Environment Variables

### Security-Sensitive Settings
- **OPENAI_API_KEY**: OpenAI API key for AI services
- **LOGANALYZER_DB_PATH**: Override default database path
- **LOGANALYZER_CONFIG_PATH**: Override default config file location

### Performance Settings
- **LOGANALYZER_MAX_MEMORY_MB**: Maximum memory usage limit
- **LOGANALYZER_PARALLEL_PROCESSING**: Enable/disable parallel processing

## Configuration Management Commands

### Setup Command
```bash
loganalyzer config init
```
Initializes configuration file with default values and prompts for key settings.

### View Configuration
```bash
loganalyzer config show
```
Displays current configuration settings (with sensitive values masked).

### Update Configuration
```bash
loganalyzer config set --key "ai.defaultModel" --value "gpt-3.5-turbo"
```
Updates specific configuration values.

### Validate Configuration
```bash
loganalyzer config validate
```
Validates current configuration and tests connectivity (e.g., AI API access).

## Configuration Precedence

1. **Command-line arguments** (highest priority)
2. **Environment variables**
3. **Project-specific configuration**
4. **Global configuration file**
5. **Built-in defaults** (lowest priority)

## Security Considerations

### API Key Management
- Never store API keys directly in configuration files
- Use environment variables for sensitive credentials
- Support secure credential stores where available
- Provide clear guidance on secure key management

### File Permissions
- Configuration files should have restricted permissions (600)
- Database files should be protected from unauthorized access
- Log files should not contain sensitive information
