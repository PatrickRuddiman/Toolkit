# Project Management System

## Unified Project-Based Organization

The application uses a unified project-based approach where analysis runs are tracked within projects for different systems or environments.

### Project Data Fields
- **ProjectId**: Unique identifier (GUID)
- **Name**: Human-readable project name
- **Description**: Optional project description
- **CreatedAt**: Project creation timestamp
- **LogPattern**: Default glob pattern for log files (e.g., "logs/**/*.log")
- **ConfigurationSettings**: JSON blob for project-specific settings

### Analysis Run Tracking

Track individual analysis runs within projects for historical comparison and trend analysis.

#### Analysis Run Data Fields
- **AnalysisRunId**: Unique identifier (GUID)
- **ProjectId**: Foreign key to Project
- **Name**: Optional run name (defaults to timestamp-based name)
- **StartTime**: Analysis start timestamp
- **EndTime**: Analysis completion timestamp
- **Status**: Running, Completed, Failed
- **LogPattern**: Specific glob pattern used for this run
- **AnalysisParameters**: JSON blob storing analysis configuration
- **Summary**: AI-generated summary of findings

## Persistent Storage

SQLite database for storing projects, analysis runs, log entries, analysis results, and related metadata.

### Core Database Schema

#### LogEntry Table Schema
```sql
CREATE TABLE LogEntry (
    LogEntryId TEXT PRIMARY KEY,
    AnalysisRunId TEXT NOT NULL,
    Timestamp DATETIME NOT NULL,
    ServiceName TEXT,
    Level TEXT NOT NULL,
    Message TEXT NOT NULL,
    CorrelationId TEXT,
    SourceFile TEXT NOT NULL,
    LineNumber INTEGER,
    RawLogLine TEXT,
    ParsedFields TEXT, -- JSON blob for additional fields
    EmbeddingVector BLOB, -- Serialized vector for semantic search
    FOREIGN KEY (AnalysisRunId) REFERENCES AnalysisRun(AnalysisRunId)
);
```

#### Additional Tables
- **Anomaly**: Store detected anomalies with links to LogEntry and AnalysisRun
- **CorrelationGroup**: Group LogEntry items by CorrelationId
- **Service**: Track service metadata and health metrics
- **Project**: Store project configuration and metadata
- **AnalysisRun**: Track individual analysis runs within projects

## Project Lifecycle

### Project Creation
- Create with name, description, and default log pattern
- Inherit global configuration settings
- Support project-specific overrides

### Analysis Run Management  
- Each log analysis creates a new run within a project
- Track run metadata (start time, duration, status, parameters)
- Link all analysis artifacts to the specific run
- Support historical comparison between runs

### Data Organization
- Project-centric organization for easy navigation
- Historical tracking of all analysis runs
- Comparison capabilities between runs
- Clean separation of concerns between projects
