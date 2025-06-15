# Log Analysis Tool - Complete Specification

## Overview

This document serves as the master specification for a comprehensive .NET command-line application for analyzing log files from microservice-based systems using AI. The specification has been broken down into focused modules for easier navigation and AI-assisted development.

## Specification Modules

This complete specification is organized into the following focused modules:

### [01. Overview](01-overview.md)
Project overview, key features, primary goals, target users, and architecture philosophy.

### [02. Project Management](02-project-management.md)
Unified project-based organization, analysis run tracking, persistent storage, and project lifecycle management.

### [03. CLI Commands](03-cli-commands.md)
Complete command structure, user interface design, interactive prompts, and usage examples.

### [04. Configuration](04-configuration.md)
Global configuration system, environment variables, security considerations, and configuration management.

### [05. Log Ingestion](05-log-ingestion.md)
File input handling, glob pattern support, format detection, parsing pipeline, and data normalization.

### [06. Correlation Analysis](06-correlation-analysis.md)
Cross-service log correlation, time-based and semantic correlation strategies, and relationship analysis.

### [07. Vector Database](07-vector-database.md)
In-memory vector database setup, semantic search capabilities, embedding generation, and clustering.

### [08. AI Integration](08-ai-integration.md)
OpenAI integration architecture, pattern-based prompt system, specialized analysis agents, and response processing.

### [09. Query Engine](09-query-engine.md)
Natural language query processing, intent analysis pipeline, interactive sessions, and AI-powered understanding.

### [10. Reporting System](10-reporting-system.md)
DocFX-compatible reporting, Mermaid diagram generation, data validation files, and interactive session reports.

### [11. Architecture](11-architecture.md)
Modular architecture design, extensibility framework, best practices implementation, and testing strategy.

### [12. Data Models](12-data-models.md)
Complete database schema, entity models, relationships, indexing strategy, and data transfer objects.

## How to Use This Specification

Each module is designed to be:
- **Self-contained**: Can be read independently for specific topics
- **AI-friendly**: Optimized for AI-assisted development and code generation
- **Cross-referenced**: Links to related modules where appropriate
- **Implementation-ready**: Contains concrete examples and code patterns

## Development Approach

When implementing this system:
1. Start with the foundational modules (01-04) for understanding scope and architecture
2. Implement core functionality following modules 05-08 for data processing and AI integration
3. Add advanced features from modules 09-12 for querying, reporting, and data management
4. Use module 11 (Architecture) as a guide for best practices throughout development

This modular approach enables incremental development while maintaining architectural coherence and system quality.

## Core Features

### Project Management System
- **Unified Project-Based Organization**: Create and manage analysis projects that track all analysis runs for different systems or environments.
    - **Project Data Fields**:
        - `ProjectId` (PK, GUID): Unique identifier for the project.
        - `Name` (TEXT, NOT NULL): User-defined name for the project.
        - `Description` (TEXT): Optional description of the project.
        - `CreationDate` (DATETIME, NOT NULL): Timestamp of project creation.
        - `LastAccessedDate` (DATETIME): Timestamp of last project access.
        - `DefaultLogPathPattern` (TEXT): Default glob pattern for log files associated with this project.
- **Analysis Run Tracking**: Track individual analysis runs within projects for historical comparison and trend analysis.
    - **Analysis Run Data Fields**:
        - `AnalysisRunId` (PK, GUID): Unique identifier for the analysis run.
        - `ProjectId` (FK, GUID, NOT NULL): Foreign key linking to the parent project.
        - `StartTime` (DATETIME, NOT NULL): Timestamp when the analysis run began.
        - `EndTime` (DATETIME): Timestamp when the analysis run completed or was terminated.
        - `Status` (TEXT, NOT NULL): Current status of the run (e.g., "Initialized", "Ingesting", "Parsing", "Analyzing", "Reporting", "Completed", "Failed", "Cancelled").
        - `LogFileCount` (INTEGER): Number of log files processed in the run.
        - `AnalyzedLogEntryCount` (INTEGER): Total number of log entries analyzed.
        - `RawInputGlobPattern` (TEXT): The glob pattern used for this specific run.
        - `ReportFilePath` (TEXT): Path to the generated report file for this run.
        - `RunName` (TEXT): Optional user-defined name for the analysis run (e.g., "Morning Incident Analysis", "Weekly Review").
- **Persistent Storage**: SQLite database for storing projects, analysis runs, log entries, analysis results, and related metadata.
    - **`LogEntry` Table Schema**:
        - `LogEntryId` (PK, INTEGER, AUTOINCREMENT): Unique identifier for each log entry.
        - `AnalysisRunId` (FK, GUID, NOT NULL): Foreign key linking to the analysis run this log entry belongs to.
        - `TimestampUTC` (DATETIME, NOT NULL): The normalized timestamp of the log entry in UTC.
        - `OriginalTimestamp` (TEXT): The timestamp string as it appeared in the raw log.
        - `OriginalTimeZone` (TEXT): The original timezone of the timestamp, if detected.
        - `DetectedFormat` (TEXT): The format detected for this log entry (e.g., "JSON", "NginxAccess", "Syslog", "Unstructured").
        - `SourceFileName` (TEXT): The name of the file this log entry originated from.
        - `SourceFilePath` (TEXT): The full path to the source log file.
        - `RawMessage` (TEXT, NOT NULL): The original, unaltered log line.
        - `NormalizedMessage` (TEXT): The processed and potentially cleaned-up log message content.
        - `SeverityLevelId` (FK, INTEGER): Foreign key linking to the `SeverityLevel` table.
        - `ServiceId` (FK, INTEGER): Foreign key linking to the `Service` table.
        - `CorrelationId` (TEXT): Extracted correlation ID (e.g., trace ID, request ID).
        - `ThreadId` (TEXT): Extracted thread ID, if available.
        - `ProcessId` (TEXT): Extracted process ID, if available.
        - `AdditionalDataJson` (TEXT): A JSON string storing other structured fields extracted from the log entry (e.g., user ID, session ID, custom key-value pairs).
        - `EmbeddingVector` (BLOB): Optional, stores the semantic vector embedding of the `NormalizedMessage`.
    - **`SeverityLevel` Table Schema**:
        - `SeverityLevelId` (PK, INTEGER, AUTOINCREMENT): Unique ID for the severity level.
        - `LevelName` (TEXT, UNIQUE, NOT NULL): Standardized severity level name (e.g., "TRACE", "DEBUG", "INFO", "WARNING", "ERROR", "CRITICAL", "FATAL").
    - **`Service` Table Schema**:
        - `ServiceId` (PK, INTEGER, AUTOINCREMENT): Unique ID for the service.
        - `ServiceName` (TEXT, UNIQUE, NOT NULL): Name of the microservice or component derived from log source or content.
    - **Other potential tables**: `Anomaly` (to store detected anomalies with links to `LogEntry` and `AnalysisRun`), `CorrelationGroup` (to group `LogEntry` items by `CorrelationId`).

### Natural Language Query Engine
- **AI-Powered Queries**: Use natural language to query log data and analysis results.
- **Intent Analysis**: AI determines query intent and maps to appropriate data operations. The AI will decide whether to use SQL for structured data queries, vector search for semantic similarity queries, or a combination of both.
    - Queries targeting specific, known fields (e.g., timestamps, service names, severity levels, specific error codes) will primarily translate to SQL queries against the structured data in SQLite.
    - Queries focusing on the meaning or similarity of log messages (e.g., "find errors similar to X", "what other logs discuss Y?") will primarily leverage vector search on the stored embeddings.
    - Hybrid queries (e.g., "show critical errors in the 'user-service' last night that are semantically similar to 'authentication token expired'") will first use SQL to filter by service, severity, and time, then use vector search on the resulting subset to find semantically similar messages, or vice-versa.
    - Examples:
        - Query: "Show me all errors from the 'payment-service' between 2 PM and 3 PM yesterday."
            - Intent: Filter log entries (SQL-focused).
            - Parameters: `ServiceName` = 'payment-service', `SeverityLevel` = 'ERROR', `TimeRange` = [yesterday 14:00, yesterday 15:00].
            - Action: Construct and execute a SQL query against the `LogEntry` table joining with `Service` and `SeverityLevel` tables.
        - Query: "What was the p95 latency for 'order-service' today?"
            - Intent: Calculate aggregate metric (SQL-focused, potentially with JSON parsing).
            - Parameters: `Metric` = 'p95 latency', `ServiceName` = 'order-service', `TimeRange` = [today 00:00, today 23:59:59].
            - Action: Query `LogEntry` table for relevant entries, extract latency from `AdditionalDataJson` (assuming it's stored there), and compute the 95th percentile. This might require custom parsing logic if latency isn't a standard field.
        - Query: "Find logs containing 'transaction timed out' for user 'user123' in the last hour."
            - Intent: Text search combined with filtering (SQL-focused with LIKE and JSON path).
            - Parameters: `SearchText` = 'transaction timed out', `User` = 'user123' (from `AdditionalDataJson`), `TimeRange` = [now - 1 hour, now].
            - Action: SQL query with `LIKE` operator on `NormalizedMessage` and JSON path expression on `AdditionalDataJson`, filtered by time.
        - Query: "Are there any unusual error patterns in the 'auth-service' this morning?"
            - Intent: Anomaly detection request (AI analysis, potentially on SQL-filtered data).
            - Parameters: `ServiceName` = 'auth-service', `TimeRange` = [today morning].
            - Action: Trigger a focused AI analysis (using anomaly detection patterns) on the subset of logs for 'auth-service' from that period.
        - Query: "Find other log entries that are semantically similar to the error message 'database connection timeout' that occurred around 3 PM today."
            - Intent: Semantic similarity search + time filter (Vector Search-focused).
            - Parameters: `SearchText` = 'database connection timeout', `TimeRange` = [today 14:55, today 15:05 (example window)], `SimilarityThreshold` = 0.8 (example).
            - Action: Generate an embedding for the `SearchText`. Query the vector index for `LogEntry` vectors within the `TimeRange` that have a cosine similarity score above the `SimilarityThreshold` to the search text's embedding. Retrieve the corresponding full `LogEntry` details from the SQLite database for the matching entries.

### Enhanced Reporting
- **DocFX Compatible**: Rich markdown reports with metadata, charts, and structured content.
- **AI-Powered Mermaid Diagrams**: Dynamic service health visualizations, correlation timelines, error rate charts, and anomaly distributions generated by specialized AI patterns.
    - Specific diagram types to include:
        - **Sequence Diagrams**: For visualizing correlated flows of requests across multiple services, based on `CorrelationId` and timestamps.
        - **State Diagrams**: For illustrating service health transitions (e.g., from "Healthy" to "Degraded" to "Unavailable") based on error rate thresholds or critical anomaly counts over time.
        - **Gantt Charts**: For showing timelines of operations or specific correlated transactions.
- **Data Validation Files**: Raw data markdown tables linked to AI-generated charts for transparency and validation.
    - These will be separate, clearly named markdown files (e.g., `report_projectX_runY_chartZ_data.md`).
    - Each file will contain a markdown table presenting the exact data subset (e.g., specific log entries, aggregated metrics, anomaly details) that was fed into the AI pattern or statistical function to produce a corresponding chart, graph, or insight in the main report. This allows users to manually inspect and verify the basis of the AI's conclusions.

## CLI Input and Log File Ingestion

### 1. Accepting Glob Patterns
Implement the CLI to accept a file path pattern (glob) for `.log` files as an argument. For example, a user might run the tool as: `LogAnalyzer "*.log"` or specify a directory pattern like `logs/**/*.log`. In .NET, the application can use `Directory.EnumerateFiles` or `Directory.GetFiles` with search patterns to resolve the glob into actual file paths. Make sure to handle patterns recursively (e.g. `**` for all subdirectories) if needed, or use a library that supports advanced glob syntax. Include validation: if no files match, inform the user gracefully.

### 2. File Reading Considerations
Once file paths are collected, read each log file efficiently. For potentially large log files, prefer streaming line-by-line reading (e.g. using `File.ReadLines` or `StreamReader`) to avoid loading entire files into memory. Ensure the ingestion handles a large number of files and large sizes robustly. You might incorporate parallel reading if I/O bound (using async file I/O) but be mindful of ordering if logs need to be merged by time later. Each log line (or structured entry) will be parsed and transformed into a common `LogEntry` object as described next.

### Detailed CLI Command Structure and User Interaction

#### Command Suite
The CLI will feature a comprehensive set of commands for managing projects and performing analysis:
- **Project Management**:
    - `loganalyzer project create --name "ProjectName" --description "Optional Description" --log-pattern "logs/**/*.log"`: Creates a new project.
    - `loganalyzer project list`: Lists all available projects.
    - `loganalyzer project update "ProjectName" [--new-name "NewName"] [--description "NewDesc"] [--log-pattern "new/pattern/*.log"]`: Updates project details.
    - `loganalyzer project delete "ProjectName"`: Deletes a project and its associated data after confirmation.
- **Analysis Management**:
    - `loganalyzer analyze --project "ProjectName" [--glob "specific_run_logs/*.log"] [--name "RunName"]`: Starts a new analysis run for the specified project. Uses project's default glob if not provided.
    - `loganalyzer list --project "ProjectName"`: Lists all analysis runs for a project.
    - `loganalyzer status --project "ProjectName" [--run-id <runId>]`: Shows the status and progress of the latest run or a specific run.
    - `loganalyzer delete --project "ProjectName" --run-id <runId>`: Deletes an analysis run and its associated data after confirmation.
- **Querying & Reporting**:
    - `loganalyzer query --project "ProjectName" [--run-id <runId>] "[Optional initial natural language query]"`: Initiates an interactive chat session for querying the specified project's latest run (or specific run if provided). If an initial query is provided, it's executed immediately. Otherwise, the user is prompted. Results are displayed in the console and appended to a continuously updated DocFX-compatible markdown report (e.g., `reports/ProjectName_RunID_QueryLog_YYYYMMDDHHMMSS.md`). Type "Exit" to end the session.
    - `loganalyzer report --project "ProjectName" [--run-id <runId>] --output-path "reports/ProjectName_Report.md" [--format docfx|markdown]`: Generates a report for a completed analysis run.
- **General Options**:
    - `--verbose, -v`: Enables verbose output for CLI operations.
    - `--help, -h`: Displays help information for commands and subcommands.

#### User Prompts & Feedback
- **Interactive Prompts**: For critical operations like deletion, the CLI will require user confirmation (e.g., "Are you sure you want to delete project 'MySystem'? [y/N]").
- **Progress Indication**: For long-running operations (ingestion, AI analysis, report generation), the CLI will display progress indicators (e.g., spinners, progress bars, or percentage completion updates) to provide feedback to the user.
- **Error Messages**: Errors will be reported clearly, indicating the command that failed and a descriptive message of what went wrong and potential next steps.

## Global Application Configuration CLI

A setup command will be provided to configure global settings for the LogAnalyzer tool. This command will allow users to set default paths, AI configurations, and other application-wide settings that apply across all projects and sessions.

### Global Settings
- **Database Location**: The default path for the SQLite database file (e.g., `~/.loganalyzer/data/loganalysis.db`). This should be configurable via an environment variable or a setting in the global configuration file.
- **Default AI Configuration**: A global default `AiConfigId` to use if a project doesn't specify one.
- **Application Log Level**: Verbosity level for the LogAnalyzer tool's own operational logs (e.g., DEBUG, INFO, WARNING, ERROR).
- **Default Report Output Directory**: A default directory where reports are saved if `--output-path` is not specified.

### Configuration File
- **Location**: A global configuration file (e.g., `~/.loganalyzer/config.json` or `%APPDATA%/LogAnalyzer/config.json` on Windows) will store these global settings.
- **Schema**: The file will use a simple JSON or YAML format. Example:
  ```json
  {
    "databasePath": "~/.loganalyzer/data/loganalysis.db",
    "defaultAiConfigName": "DefaultOpenAI",
    "applicationLogLevel": "INFO",
    "defaultReportOutputPath": "~/Documents/LogAnalyzerReports"
  }
  ```

## Parsing Heterogeneous Log Formats

### 3. Format Detection
Logs may come in various formats – plain text, unstructured or free-form text, structured key-value, or JSON. The application should detect or be configured for each format:

#### JSON logs
If a log file’s lines start with `{` or `[` (or can be parsed as JSON), treat it as JSON. Use a JSON parser (e.g. `System.Text.Json`) to deserialize each line into a dynamic object or dictionary. This preserves structured fields (timestamp, level, message, etc.).

#### Common text logs
Many logs are text lines with a known pattern (for example, Nginx/Apache access logs, or application logs with `[LEVEL] timestamp – message`). For known formats, prepare parsing rules or regular expressions. For instance, an Nginx access log might be parsed with a regex to extract client IP, timestamp, HTTP method, status code, etc. If possible, utilize existing patterns or configuration (such as a format string) to parse these lines.

#### Unstructured logs
If the format is not immediately recognizable, fallback to a default parsing: attempt to find a timestamp substring, and otherwise treat the whole line as the message. The goal is to capture whatever structure is present (even if just timestamp and text).

### 4. Normalizing Fields
Once log entries are parsed from their various formats, they need to be normalized to a common schema. This unified schema should include:

- **Timestamp**: Convert all timestamps to a consistent format (e.g., UTC `DateTime`)
- **Level/Severity**: Map log levels (DEBUG, INFO, WARN, ERROR, FATAL, etc.) to a standard enum
- **Service**: Extract or infer the service name from the log source or content
- **Message**: The main log message content
- **Correlation ID**: Extract transaction/request IDs if present for cross-service correlation
- **Additional Metadata**: Store other structured fields (user ID, session ID, etc.) in a flexible dictionary

The normalized `LogEntry` object serves as the foundation for all subsequent analysis and enables consistent processing across heterogeneous log sources.

### 5. Time and Zone Handling
Ensure all timestamps are parsed into a common timezone and format (e.g. UTC `DateTime`). If logs have different time formats (ISO 8601, epoch, custom formats), use parsing libraries or custom formats to convert them. A unified timeline is needed for correlation and sequencing.

### 6. Verification and Validation
Implement basic validation after parsing: e.g., check that required fields like `Timestamp` were extracted. If a log line cannot be parsed or is malformed, handle it (skip or mark it) rather than crashing. You might keep a count of unparsed lines to report later as "X lines could not be parsed."

## Data Normalization and Schema Alignment

### 7. Schema Alignment
The normalization process should align all log entries to the common `LogEntry` schema. This might involve transforming certain fields to fit the schema:
*   Convert numeric strings to numbers (e.g. status code `"500"` to `int 500`).
*   Map custom severity strings to a standard set (e.g. `"WARN"` and `"Warning"` both to an enum `LogLevel.Warning`).
*   If logs contain unique fields (e.g. Nginx logs have `responseTime` or `bytesSent`), you can include those as optional fields or in a generic key-value map inside the `LogEntry` for extensibility.
*   Tag each entry with its source (perhaps derive a `ServiceName` from the filename or from the log content if available). For example, logs from `payments-service.log` get `Service = "Payments"`. This will allow grouping metrics by service.

### 8. Advantages of Normalization
With logs normalized, the system can now aggregate and correlate events reliably. Different log sources “speak the same language” after this step, enabling unified analysis. For instance, a failed request in an Nginx log and a corresponding error in an application log can be linked by timestamp or ID once both entries share common fields (timestamp, request ID, etc.). Normalization dramatically improves the efficiency of querying and analyzing logs from multiple sources. It lays the foundation for building a centralized analysis pipeline akin to what SIEM systems do, but tailored to our needs.

## Correlating Logs Across Services

### 9. Correlation IDs and Trace Linking
In microservice architectures, one request often generates logs in multiple services. Leverage correlation identifiers if they exist. A Correlation ID is typically a unique ID added to a request when it enters the system, and included in all downstream log entries for that request. If your logs have a field like `CorrelationId`, `TraceId`, or similar (either as a JSON field or embedded in the text), use it to group log entries by transaction. This grouping allows the tool to reconstruct an end-to-end flow of each request across services. For example, if a user action passes through Service A, B, and C, all logs carrying the same Correlation ID can be linked to show the complete timeline of that action.

### 10. Time-Based and Semantic Correlation
Not all systems implement correlation IDs. If they’re absent, use temporal and semantic hints to correlate events:
*   **Time proximity**: Sort all log entries (from all files) by timestamp. Events from different services that occur very close in time might be related (especially if one is an error and another is a cause or effect). The tool can look for patterns like an error in Service B occurring a few milliseconds after an error in Service A, and flag them as possibly connected.
*   **Message keywords**: Use key fields or keywords in messages. For example, an order ID or user ID appearing in a log from service A and also in service B’s log could indicate those logs relate to the same entity or request. The parser can be extended to detect such identifiers in text (via regex).
*   **AI-assisted correlation**: An LLM can further help by analyzing log content to infer connections. For example, the AI could notice that a timeout error in one service follows a slow response log in another service and suggest they are linked. This is an advanced approach: feeding a set of temporally adjacent logs from different sources into the LLM and asking it to identify if they are part of one storyline or separate incidents.
*   **Vector-Search-Assisted Semantic Correlation**: When explicit correlation IDs are absent, use vector search on log message embeddings to find semantically similar entries across different services within a close time proximity. This can uncover related events even if their wording differs significantly, complementing keyword and time-based methods.

## Integrating an In-Memory Vector Database

### 12. Setting Up Vector Storage
Incorporate a lightweight vector database in-memory to store semantic embeddings of log entries. A vector database allows similarity search on high-dimensional vectors, which represent the semantic content of text. This component will be used to find logs that are textually or contextually similar to each other, enabling semantic correlation and anomaly detection beyond exact matches.
*   **Embedding generation**: Choose or integrate an embedding model to convert log messages (or log events) into numeric vector form (e.g., 512 or 1536-dimensional float vectors). One approach is to use an API like OpenAI’s embedding model (e.g. `text-embedding-ada-002`) to get embeddings for each log message. Alternatively, use a local model or library (such as a SentenceTransformer model accessible via ML.NET or external ML libraries). Each `LogEntry` will be augmented with its embedding vector.
*   **Indexing**: Use an in-memory index for similarity search. You can use existing .NET libraries (for example, `SharpVector` or `Vektonn` index) that implement efficient nearest-neighbor search. Upon adding all log vectors, the index can answer queries like “find the top-N most similar log messages to this one” quickly. This will enable the tool to retrieve related events across different logs by semantic similarity, not just by common ID.

### 13. Applications of Vector Search
Once embeddings are in place:
*   **Semantic Similarity Queries**: Given a particular log entry (especially an error), find similar occurrences in other services or at other times. For example, if an error message “database connection timeout” appears in one service, a vector search can find log entries in other services that have semantically similar meaning (even if phrased differently). This helps correlate issues that manifest in different systems but have the same root cause. It also helps verify if an anomaly is isolated or widespread by finding if similar logs exist elsewhere.
*   **Clustering & Pattern Discovery**: By running clustering on the vectors (or even using the nearest-neighbor structure), group logs by similarity. The tool can automatically form clusters of log events that frequently occur (representing normal patterns) and isolate outliers that don’t fit any cluster (potential anomalies). This is a powerful way to let patterns emerge from heterogeneous logs: common events cluster together, rare/new events stick out. The LLM or rules can then focus on those outliers as anomalies.
*   **Augmenting LLM analysis (RAG)**: The vector store can be used in a Retrieval-Augmented Generation pattern. For any given log or time period being analyzed by the LLM, you could fetch related log entries (via similarity search) to provide more context. For instance, if asking the LLM to explain an error, you might retrieve past occurrences of similar errors (and their resolutions if logged) to feed the LLM as additional context. This helps the LLM give more informed analysis or even guess the cause of an issue by analogy.
*   **Directly Powering Natural Language Queries**: Enable users to ask for logs based on semantic meaning (e.g., "find logs talking about 'network failures'" even if the exact phrase isn't used) by translating the query into a vector search against `NormalizedMessage` embeddings. The query engine can generate an embedding for the user's natural language query phrase and use that to find relevant log entries.

## AI Model Selection and API Integration

### 14. Choosing an LLM Provider
Select an appropriate Large Language Model accessible via API. Consider factors like context window size, performance, and cost:
*   **OpenAI GPT-4**: Strong reasoning abilities and can handle complex log patterns and long context. With the 8k or 32k token context versions, GPT-4 can analyze larger chunks of logs at once, which is useful for seeing cross-log coherence. It’s more expensive and somewhat slower, but likely to yield the best analytic insights for complex scenarios.
*   **OpenAI GPT-3.5 (Turbo)**: Faster and cheaper, with a smaller context (4k or 16k tokens). It can be used for simpler tasks or quick interim analysis (like tagging). It might miss subtleties in complicated multi-service interactions but is effective for straightforward summary or classification tasks.
*   **Other models**: If using Azure OpenAI, that mirrors the above models with Azure endpoints. Alternatively, open-source LLMs (like LLaMA 2 or Falcon) could be used if hosted either locally or via a service, but integration is more complex and they may require fine-tuning to reach GPT-4’s level in log analysis. Given the requirement of an online LLM agent, using a hosted API like OpenAI is the straightforward approach.

### 15. API Integration in .NET
Integrate the chosen model’s API into the .NET app:
*   Use an official SDK if available. For OpenAI, there are community .NET libraries (like `OpenAI.NET` or `Azure.AI.OpenAI` for Azure) which can simplify API calls. Otherwise, use `HttpClient` to call the REST endpoint directly.
*   Store the API key securely. The CLI can accept an API key via an environment variable or secure store (never hard-code it). For example, instruct users to set `OPENAI_API_KEY` in their env, or use Azure Managed Identity if applicable.
*   Implement a utility class (e.g. `LLMClient`) to encapsulate prompt sending and response handling. This class can manage details like rate limiting (to avoid sending too many requests too fast), exponential backoff on errors, and retries for transient network issues.
*   Model usage optimization: Because API calls have latency and cost, be strategic. Possibly reuse a single model instance for multiple prompts (depending on API, some allow a session or cached context). Use streaming responses if available to start processing output sooner for long texts. Also, monitor token usage: the tool should estimate tokens per request (prompt + expected output) to stay within model limits, splitting input if needed (this ties into chunking, below).

### 16. Handling Responses and Errors
Design how the AI responses will be consumed. Likely, the responses will be parsed (especially if we ask the model to output structured data for easier processing). Implement checks on the AI’s output (for validity, format, etc.), since LLMs might sometimes produce irrelevant or hallucinated info. If the response is not useful or not in expected format, the system can either retry with a adjusted prompt or log a warning and continue. Always fail gracefully – the analysis pipeline should continue even if one AI call fails (maybe skipping that part of analysis in the report with a note).

## Prompt Design for Specialized Analysis Agents
Rather than one monolithic prompt, the application will use multiple specialized prompts for different analysis sub-tasks. Each “AI agent” focuses on a specific concern (semantic chunking, coherence, anomalies, tagging), using a carefully designed prompt or chain of prompts. This modular prompt strategy ensures clarity and relevance in AI output, and makes it easy to add or refine tasks. Below are the key AI-driven tasks and strategies for prompting each:

### 17. Semantic Chunking of Logs
Before sending log data to the LLM for analysis, the logs may need to be chunked into manageable, meaningful segments. The goal is to split the logs into coherent pieces that the model can fully grasp within its context limit.
*   **Chunk by time or transaction**: If correlation grouping is done (step 9), each group of correlated events (all logs for one transaction or request) can form one chunk. This is naturally coherent since it follows one flow. If log volume is high, you might also chunk by time windows (e.g. 5-minute intervals) ensuring each chunk isn’t too large.
*   **AI-assisted chunking**: For uncorrelated or continuous logs, you can use the LLM to find logical breakpoints. For example, prompt the model with a very large log (possibly in streaming fashion) asking it to identify boundaries where the context switches or a new incident starts. However, this can be costly; often simpler heuristics (transaction IDs, time gaps, etc.) suffice.
*   **Maintain context linkage**: If an important event spans chunk boundaries, ensure to carry it over or mention it in the next chunk’s prompt (so the AI knows the prior context). Overlap chunks slightly if needed for continuity.
The output of this stage is a list of log segments (each segment being a list of `LogEntry` or raw lines) that will be fed into subsequent AI analysis steps. Keep chunks size such that they fit well under the model’s token limit including the prompt text and some margin for the model’s output.

### 18. Coherence Analysis Prompt
Coherence analysis checks if events in a chunk or correlated group follow an expected order and whether any steps are missing or inconsistent. This is essentially asking the AI: “Does this sequence of logs make sense as a complete, normal operation flow? If not, what seems out of place or missing?”
*   **Prompt design**: Provide the chunk of logs (with essential fields or a summarized form if very detailed) in the prompt. For example:
    ```
    System message: “You are a system log analyst. You will check a sequence of log events for logical consistency and completeness.”
    User message: “Here is a series of log entries from a single transaction (across multiple services). Analyze the sequence for coherence. Identify if any expected event is missing (e.g., a request with no corresponding response), or if any event seems out of order or unrelated. Reply with any inconsistencies or confirm the sequence is coherent.”
    ```
    The model would then output something like: “Coherence Check: The events appear consistent except Service C never logged a response to the request, which might indicate a missing log or a failure.”
*   **Handling output**: Parse the output (or instruct the model to output in a structured format, e.g. a short list of findings). If the model identifies missing steps (“no log for operation X”), mark that as a potential anomaly to include in the report. Coherence analysis basically surfaces anomalies that are sequence gaps or logical errors (e.g., a user logout occurred before a login in the timeline).

### 19. Anomaly Detection Prompt
This is a core AI task – finding unusual or significant events in the logs. While some anomalies (like missing steps) come from coherence checking, here we target content anomalies and error patterns.
*   **Prompt design**: The prompt should give the AI a chunk of logs and ask for anomalies/unusual patterns. Key is to guide the AI on what constitutes an anomaly (e.g., error messages, unusual delays, rare events). For example:
    ```
    System message: “You are an expert in IT systems analyzing system logs for problems.”
    User message: “Examine the following log entries for any anomalies or errors. An anomaly could be an error log, an unexpected condition, or a rare event that doesn’t normally happen. For each anomaly you find, explain why it’s noteworthy.”
    ```
    Include the log lines (or their important fields) in chronological order after that.
*   **Few-shot examples**: It can help to provide a tiny example before the actual data, to show the format. For instance:
    ```
    Example:
    [Timestamp] Service A: Completed request.
    [Timestamp] Service A: Error connecting to DB.
    Analysis: Anomaly – 'Error connecting to DB' is an error event indicating a database connectivity issue.
    ```
    This teaches the AI to pick out error statements.
*   **Expected output**: The LLM might list anomalies like “Error connecting to DB at 12:00:05 in Service A – this is an error event possibly caused by a database outage” or “Multiple login attempts failed – indicates a possible brute force attempt”. Ensure the prompt asks for explanation or classification of each anomaly (to add value beyond what a simple search could find). The output can be structured (like a bullet list or JSON) so that it’s easy to incorporate into the final report.
*   **Validation**: Cross-check anomalies the AI outputs with the source data to avoid false positives. (For example, if the AI calls something an anomaly that is actually normal, you may filter it out or at least mark it as AI-flagged only.) Over time, prompt tuning or few-shot examples can improve accuracy here.

### 20. Data Tagging Prompt
Data tagging involves categorizing log entries or adding labels (tags) to them for easier grouping and analysis. Tags could include severity labels, component names, error categories, or functional categories (auth, database, network, etc.). Some of this information may be partially present (e.g., logs already have severity), but AI can enrich it by interpreting the message content. Two levels of tagging can be done:
*   **Per-entry tagging**: Have the LLM assign tags to each log entry based on its content. For example, a log `"Failed to connect to database"` might get tags: `["error", "database", "connectivity"]`. A log `"User login successful"` might get `["info", "authentication"]`.
*   **Prompt design**: If tagging each entry individually, you could iterate through logs (but that’s many API calls). More efficiently, you can prompt the model with a batch:
    ```
    “Assign relevant tags to each of the following log lines:
    1. [log entry]
    2. [log entry]...”
    ```
    and so on, and have it output a numbered list of tag sets. This requires careful prompt to ensure it responds in a structured way.
*   **Pattern tagging (clustering-based)**: Alternatively, use the earlier clustering from embeddings. Identify a few major clusters of similar logs, then prompt the LLM to label each cluster (e.g., “Cluster 1 contains 50 similar logs, example: 'Timeout while calling Service X'. Suggest a tag or category for this cluster.”). This way you tag groups of logs by theme, which is efficient for large data.
*   **Using tags**: Once tagged, the tags can be used to aggregate metrics (e.g., count of `"database errors"`) and in the report to highlight prevalent issue categories. It also helps an operator quickly understand what types of events occurred most. Tagging can also help filter noise: e.g., if an entire cluster is `"DEBUG logs"`, you might exclude that from deeper analysis.

Each of these AI prompts (coherence check, anomaly detection, tagging) acts like a specialized “agent” focusing on one aspect. Design the code such that each prompt can be enabled/disabled or adjusted independently (for example, via config flags for whether to do tagging or not). Ensure prompts are stored in an easily editable form (like an external prompt template file or at least constants) so developers or admins can fine-tune wording or examples without changing code logic.

## Leveraging Semantic Embeddings for Analysis

### 21. Embedding-Based Analysis
In addition to direct LLM prompting, use the semantic vector database to enhance analysis:
*   **Outlier detection**: Compute anomaly scores for log entries using embeddings. For example, for each log’s vector, find the distance to its nearest neighbors. If an entry is far from others (beyond some threshold), mark it as a potential anomaly (even before the LLM sees it). This can flag rare events objectively. The LLM can then be asked specifically about those outliers (focused anomaly prompt), improving efficiency. This unsupervised approach complements the LLM’s analysis.
*   **Similarity-based filtering**: If logs are very verbose (e.g., lots of repetitive info logs), the vector clustering can identify those repetitive groups. The tool can decide to summarize or down-sample frequent entries (since they are likely “normal”), and spend more attention (and LLM tokens) on unique clusters. This balances coverage and cost.
*   **Tag propagation**: Once the LLM tags a few representative logs in a cluster, propagate those tags to all logs in that cluster using embedding similarity. This way, not every log line needs AI processing – many can inherit tags from their nearest neighbors or cluster centroid. For instance, if an AI labels one log as `"payment timeout error"`, all logs in that similarity cluster get the same tag automatically.
*   **Building vector search into prompts**: If implementing an interactive or iterative analysis, the tool could use the vector DB to answer questions like “has this error happened before?”. Even in autonomous report generation, the system might do something like: for each anomaly found by LLM, do a vector search to see if similar logs occurred in the past (maybe earlier in the log files) and report “this error occurred 5 times in the last hour”. This adds historical context to anomalies.

All these embedding techniques are highly useful for scaling to large log datasets. They ensure the LLM’s power is used where it’s most needed (understanding context and meaning), while raw computation (clustering, nearest-neighbor math) is used for brute-force scanning. This hybrid approach plays to the strengths of each component.

## Computing Service-Level Metrics and Anomalies

### 22. Calculating Metrics
With all logs in a structured form, the application can compute aggregate metrics that are valuable to operators:
*   **Error rates**: Count the number of error-level logs or failed requests per service, and divide by total requests to get an error percentage. For example, if Service A logs 100 requests and 5 errors in a timeframe, error rate is 5%. This can be done by simple LINQ queries or grouping on the `Level` field and counting. If HTTP status codes are available (e.g., from Nginx logs), compute error rate as fraction of 5xx responses vs total responses.
*   **Request rates and throughput**: Determine how many requests each service handled per minute or second. If using Nginx/access logs, each log line is a request – you can count per time interval (e.g., group by minute). This helps identify peaks in traffic. If logs contain response time or latency, compute average or p95 latency as well for performance metrics.
*   **Other analytics**: Depending on available data, extract other useful stats. Examples: number of unique users (if user IDs in logs), average payload size, most frequent endpoint accessed, distribution of log levels (e.g., 70% info, 25% warning, 5% error). In microservice context, you might also measure inter-service call counts if logs indicate calls (like “Service A called Service B – 200 OK”). Essentially, anything quantifiable in logs can be aggregated for insight.
This metric computation is done with deterministic code, not AI, to ensure accuracy. Use appropriate data structures (e.g., dictionaries keyed by service name for counts). As data is processed, accumulate these counts.

### 23. Integrating AI Findings
Alongside raw metrics, include the AI-derived insights:
*   The anomalies detected by the LLM (from step 19) and by embedding outlier analysis should be collected into a list of notable events. Each anomaly can include details like which service it happened in, timestamp, and the AI’s description of why it’s abnormal.
*   The coherence analysis results might indicate missing logs or misordered events, which are essentially anomalies in flow – include these as well, possibly in a separate section about “Transaction Anomalies” vs “Error Anomalies”.
*   Tagging results can feed metrics too: for example, if logs were tagged by category, you can count occurrences per category (“Database errors: 3, Timeout errors: 5, Authentication failures: 2”, etc.). This gives a high-level view of what types of issues are most common.
By combining computed metrics and AI findings, the tool can identify service-level anomalies — e.g., a spike in error rate for Service B at 2:00 PM, or Service C logging an unusual error message that no other service has. The AI’s pattern recognition augments numeric thresholds with semantic analysis, catching things like “Service A experienced a sequence of retry warnings that is unusual” which pure metrics might not reveal.

## Report Generation and Output

### 24. Compiling the Report
Design the final output report to be clear and informative. As a CLI, the output can be printed to console or saved to a file (perhaps offering a `--output` option for a markdown or text file). Organize the report with headings or clear sections (the output here can use Markdown for formatting if writing to a `.md` file, or just plain text with separators if printing to console). For example:
*   **Summary**: A brief overview stating how many log files were analyzed, the time range of logs, and a high-level assessment (e.g., "No critical anomalies found" or "Multiple errors and anomalies detected"). This summary can even be generated or refined by the LLM – you can prompt the LLM at the end: “Summarize the overall health of the system based on the following metrics and anomalies...” using the computed data as context.
*   **Aggregate Metrics**: Present the key metrics per service. Possibly a small table or list per service, e.g.:
    *   Service A: 1200 requests, 12 errors (1% error rate), avg latency 200ms
    *   Service B: 300 requests, 9 errors (3% error rate), error spike at 12:30 UTC
    *   Service C: 500 requests, 0 errors (0% error rate)
    Include any noteworthy points like “highest throughput at 13:00 was 50 req/sec” etc.
*   **Anomalies and Alerts**: List the anomalies found. This includes errors or unusual events. Each anomaly entry might include a timestamp, the service/component, and a description. For example: “[Service B] 2025-06-01 12:31:45 – Anomaly: Out-of-memory error occurred, not seen in prior logs. This indicates a potential memory leak.” If multiple anomalies were of the same type, note the count (or group them) to avoid repetition.
*   **Correlation Insights**: If the analysis found cross-service issues (like a cascade of failures across services), describe those. E.g., “A timeout in Service A at 12:31 led to a user-facing error in Service B (correlated via trace ID 1234).” These narrative insights can be output by the AI (you can prompt it to explain an anomalous sequence) or constructed from the data collected.
*   **Frequent Patterns / Tags**: If tagging was done, show the results. For instance: “Log Categories: 5 database errors, 3 authentication failures, 20 timeouts, 50 successful operations.” You can highlight if any category spiked abnormally. This gives readers an understanding of what types of events dominate the logs.
*   **Recommendations**: The tool might also output suggestions if applicable. For example, if error rate is high, it might suggest investigating a specific service or scaling resources. This can be hard-coded logic or even an AI-generated suggestion (prompt the LLM: “Given the above findings, suggest possible actions to take.”).

### 25. Formatting and Clarity
Ensure the report is easy to read:
*   Use clear section headers (the CLI can output underlined text or markdown headers if the output medium supports it).
*   Use bullet points or numbered lists for multiple items (the user specifically appreciates well-structured output).
*   If a section has a lot of entries (like hundreds of anomalies), consider summarizing or limiting to top few and then saying “+ X more similar entries.” The detail level can be configurable.
*   Provide timestamps in a uniform format (UTC ideally) in the report.
*   If outputting to a file, ensure proper encoding (UTF-8) especially if log messages contain special characters.

### 26. No diagrams, just text
The report should be textual (per requirements). It can include ASCII tables or simple text charts for metrics if that adds value, but avoid anything that isn’t pure text. The focus is on a narrative and list of findings that a developer or ops engineer can quickly scan.

## Modular Architecture and Best Practices

### 27. Modular Design
Structure the application into logical modules, each responsible for one part of the pipeline. This not only makes the code easier to maintain but also aligns with the requirement of supporting multiple AI patterns (analysis types) in a plug-and-play fashion:
*   **Log Ingestion Module**: Handles reading files and producing raw log lines. Could be a class like `LogReader` that given a glob or list of paths, yields log lines (maybe with source info attached).
*   **Parsing & Normalization Module**: Perhaps a set of parser classes, e.g., `IParser` interface with method `Parse(string line): LogEntry`. Implement subclasses for JSON, Nginx, etc. You can auto-select parser by file type or content. This module yields structured `LogEntry` objects. A normalization sub-component can take a partially parsed entry and polish it (set schema fields, do type conversions). This separation allows adding new log format support by writing a new `IParser` implementation and plugging it in.
*   **Storage/Data Module**: In-memory collections to store the parsed `LogEntry` objects, and manage the vector index. E.g., a class `LogRepository` that holds all entries and provides methods to query by time, by service, etc., and a class `VectorIndex` to manage embedding storage and similarity queries. This abstraction could let you swap out the vector index implementation (for example, use an on-disk index or an external vector DB if needed).
*   **AI Analysis Module**: Encapsulate the LLM interactions. This can further be divided into sub-components or services for each task (coherence, anomaly, tagging). For example, have an `AnomalyDetectorAI` class with a method `FindAnomalies(LogSegment segment): AnomalyReport`. Each such class would construct the prompt for its task, call the `LLMClient`, and parse the result into a structured output (like an `AnomalyReport` object). This way, if you want to add a new analysis pattern (say, a “performance issue detector AI”), you add a new class implementing a common interface (e.g., `ILogAnalyzerPattern`).
*   **Reporting Module**: Responsible for taking all outputs (metrics, anomalies, tags) and formatting the final report. This could be a templating system or just string builders that assemble sections. By isolating reporting, you can later change the output format (say, to JSON or to a different layout) easily without touching analysis logic.

### 28. Extensibility
The modular approach means new log formats or AI patterns can be added with minimal changes. For instance, if another type of log (say, Windows Event Log format) needs support, you implement a parser for it and add it to a parser factory. Or if a new AI analysis (like security intrusion detection) is desired, you add it as a new prompt module and include its results in the report. Design the system to read configuration for what analysis to run. This could be as simple as command-line flags (e.g., `--no-tagging` to skip tagging step) or a config file listing enabled analyses.

### 29. Best Practices in Implementation
*   **Resource Management**: Use `async/await` for I/O and API calls to keep the app responsive. If analyzing very large logs, consider streaming analysis (process entries in batches so you don’t hold millions of logs in memory at once). The vector index can likewise be built in chunks if memory is a concern. Since the requirement is an in-memory DB, we assume logs volume is moderate, but be mindful of memory usage especially with embeddings (each embedding is an array of floats; tens of thousands can consume significant RAM). Optimize by not embedding trivial log lines (you might skip debug/trace level logs for embeddings, for example, to save time and space).
*   **Error Handling & Logging**:
    *   **Operational Logging**: The tool itself will maintain its own operational logs, separate from the logs it analyzes.
        *   **Log Levels**: Support standard log levels (DEBUG, INFO, WARNING, ERROR, CRITICAL).
        *   **Output**: Log to the console (respecting verbosity settings like `--verbose`) and optionally to a rotating file (e.g., `~/.loganalyzer/logs/app.log`).
        *   **Content**: Logs should include timestamps, module/class originating the log, and a clear message. For example: `[2024-07-25 10:00:05 INFO LogIngestionService] Started ingestion for 5 files matching pattern "*.log".`
    *   **Failure Modes**:
        *   **Graceful Exit**: The application should handle critical failures (e.g., invalid API key, database connection issues, unreadable configuration) by exiting gracefully with an informative error message and an appropriate exit code.
        *   **Partial Failures**: For non-critical errors during a session (e.g., a single log file is unparseable, one AI analysis pattern fails), the tool should log the error, report it in the session status/summary, and attempt to continue with other files or analysis patterns. The goal is to provide as much value as possible even if some parts encounter issues.
        *   **Retry Mechanisms**: For transient issues like network errors when calling AI APIs, implement retry logic with exponential backoff as mentioned in API integration.
    *   The tool itself should have its own logging (to console or file) for its operations. For example, log when files are being processed, when AI calls are made, and catch exceptions to log them. If an AI call fails, handle it gracefully (e.g., retry once, then skip that analysis for that chunk with a warning). Use `try-catch` around parsing to handle unexpected formats. Also, do not let a failure in one part (like one bad file) halt the entire analysis of others.
*   **API Usage Considerations**: Respect rate limits of the AI API. If processing many chunks with LLM, throttle the calls (some APIs allow e.g. 60 calls/minute – implement a simple delay or token bucket if needed). Also consider cost: potentially allow the user to specify which analyses to perform (so they can skip expensive ones). Logging the number of tokens used per run (if the API returns that info) can be useful for the user to gauge cost.
*   **Security and Privacy**:
    *   **API Key Security**: API keys are read from environment variables and never stored directly in project configurations or the database.
    *   **Log Content Awareness**: Users must be informed that log content (even if redacted) is sent to an external AI provider. This should be clear in documentation and potentially a one-time warning/confirmation.
    *   **Redaction Strategy**:
        *   **Configurable Patterns**: Allow users to define regular expressions for PII or sensitive data patterns to be redacted (e.g., email addresses, IP addresses, custom tokens). These patterns can be stored globally or per-project.
        *   **Redaction Method**: Replace matched patterns with a placeholder like `[REDACTED_EMAIL]` or `[REDACTED_IP]`.
        *   **Pre-AI Step**: Redaction should occur before log data is sent to the AI for analysis or embedding generation.
    *   **Local Storage Security**:
        *   **File Permissions**: The SQLite database file and any local configuration files should be created with default user-specific file permissions to limit access.
        *   **No Sensitive Data in DB (Ideally)**: While log messages are stored, actively avoid storing raw, unredacted highly sensitive information if it can be processed and discarded or heavily anonymized. The `RawMessage` field should store the original line as ingested, but AI interactions should use a redacted version if configured.
    *   Logs often contain sensitive information (user data, secrets, etc.). When using an online LLM service, ensure the user is aware that log content is being sent to an external API. Provide options to mask or redact sensitive fields before sending to AI. For example, if a log line contains an email or IP address, you might replace it with placeholders in the prompt (and mention `[EMAIL]` instead) to protect privacy. This can be done via simple regex replacements based on a configurable pattern list.
*   **Testing**:
    *   **Unit Tests**:
        *   Cover all parsing logic for different log formats with various valid and malformed sample lines.
        *   Test normalization functions (timestamp conversion, severity mapping).
        *   Test individual AI pattern prompt generation and response parsing logic using mock AI service responses.
        *   Test CLI command argument parsing and validation.
        *   Test database interaction logic (CRUD operations for projects, analysis runs, etc.) with an in-memory SQLite instance.
    *   **Integration Tests**:
        *   Test the full pipeline from CLI command (`loganalyzer analyze`) to report generation for a small, controlled set of log files, using a mock AI service that returns predictable responses.
        *   Verify interactions between modules (e.g., Ingestion -> Parsing -> Storage -> AI Analysis -> Reporting).
        *   Test database schema creation and migration, if applicable.
    *   **End-to-End Tests**:
        *   Develop scripts that execute key CLI workflows against a set of sample log files.
        *   These tests might use a dedicated test AI configuration (e.g., a very cheap/fast model or a mock endpoint that simulates AI behavior) to validate the overall functionality.
        *   Verify the structure and key content of generated reports.
        *   Test error handling paths by providing invalid inputs or simulating failures (e.g., unreadable log files, AI API errors).
    *   Develop unit tests for the parsing logic (feed sample log lines and verify the `LogEntry` output). Also test the analysis modules with controlled inputs – you might simulate the LLM responses for known inputs to test how the parsing of AI output works. This ensures the system is reliable and each component works in isolation.
*   **Performance Tuning**: If the log analysis might be rerun frequently or on similar data, consider caching embeddings or AI results. For instance, you could cache the embedding of each unique log message text (so identical lines aren’t embedded twice). Similarly, if using the AI to analyze identical chunks (unlikely in one run, but maybe across runs), cache results. However, caching is secondary; first make it correct and modular.
*   **Documentation**: Clearly document how to use the CLI (accepted patterns, options) and what each part of the output means. Also document how to extend the system (e.g., how to add a new parser or what prompt templates are used), so future developers or even AI coding agents can easily continue the work.

## Implementation Update: OpenAI Integration with Pattern-Based System

### 30. Architectural Changes
The application has been updated to replace the previous Fabric service dependency with a direct OpenAI integration using a pattern-based system:

#### Fabric Service Replacement
*   **Removed Dependency**: The application no longer requires the external Fabric framework
*   **Direct OpenAI Integration**: A new `OpenAIService` class provides direct access to OpenAI's API
*   **Pattern-Based Prompts**: System prompts are now stored as markdown files in the `patterns/` directory
*   **Self-Contained**: The application is fully self-contained without external tool dependencies

#### OpenAI Service Implementation
The `OpenAIService` class provides the following capabilities:
*   **Multi-Purpose Analysis**: Handles anomaly detection, coherence analysis, log tagging, summarization, and embeddings
*   **Pattern Loading**: Dynamically loads system prompts from pattern directories
*   **Response Parsing**: Structured parsing of OpenAI responses with proper error handling
*   **Configuration Integration**: Uses the existing configuration system for API keys and model settings

#### Pattern System Structure
```
patterns/
├── analyze_log_anomalies/
│   └── system.md
├── analyze_coherence/
│   └── system.md
├── tag_logs/
│   └── system.md
└── summarize_logs/
    └── system.md
```

Each pattern directory contains a `system.md` file with the specialized system prompt for that analysis type.

#### Service Integration
*   **Dependency Injection**: The `OpenAIService` is registered in the DI container
*   **EmbeddingService Update**: The `EmbeddingService` now uses `OpenAIService` for generating embeddings
*   **AnalysisService Update**: The `AnalysisService` uses `OpenAIService` instead of the former Fabric service
*   **Unified Interface**: All AI operations go through a single, consistent service interface

#### Benefits of the New Architecture
*   **Simplified Deployment**: No external dependencies required
*   **Better Control**: Direct control over API calls and response handling
*   **Extensibility**: Easy to add new analysis patterns by creating new pattern directories
*   **Maintainability**: Clear separation of concerns with pattern-based prompts
*   **Performance**: Direct API integration without intermediate layers

This implementation maintains all the original functionality while providing a more streamlined and maintainable architecture.

## Project Management and Data Persistence

### Project Lifecycle Management
The application supports a unified project-based approach where each analysis run is tracked within the project:

#### Project Creation and Configuration
- **Project Metadata**: Name, description, creation timestamp, and configuration settings
- **Project Settings**: Configure analysis options (coherence, anomaly detection, embeddings, etc.)
- **Project Isolation**: Each project maintains separate data and analysis history
- **Configuration Inheritance**: Projects inherit global settings but can override them

#### Analysis Run Management
- **Analysis Runs**: Each log analysis creates a new run within a project
- **Run Metadata**: Track start time, duration, status, analysis parameters, and run name
- **Run Correlation**: Link log entries, anomalies, and correlations to specific runs
- **Historical Tracking**: Compare runs over time to identify trends and changes
- **Latest Run Access**: Commands default to the latest run when run ID is not specified

#### Data Persistence Strategy
- **SQLite Database**: Lightweight, file-based database for local storage
- **Schema Design**: Normalized tables for projects, analysis runs, log entries, anomalies, correlations
- **Performance Optimization**: Indexes on commonly queried fields (timestamp, service, project)
- **Data Integrity**: Foreign key constraints and transaction management


## Natural Language Query Engine

### Query Processing Architecture
The query engine transforms natural language questions into database operations and analytical insights:

#### Intent Analysis Pipeline
1. **Query Preprocessing**: Clean and normalize user input (e.g., lowercasing, removing punctuation, correcting typos if feasible).
2. **Intent Classification**: AI determines query type (e.g., aggregation, filtering, pattern search, semantic similarity, anomaly lookup, trend analysis).
3. **Parameter Extraction**: Extract key entities from the query, such as service names, time ranges (absolute or relative like "yesterday", "last hour"), error types/messages, keywords, user IDs, correlation IDs, and desired metrics (e.g., "count", "p95 latency").
4. **Query Generation**: Map the intent and extracted parameters to appropriate actions:
    - Construct SQL queries for structured data retrieval and aggregation from SQLite tables (`LogEntry`, `Service`, `SeverityLevel`, etc.).
    - Generate vector search parameters (including embedding the query text if necessary) for semantic similarity searches against the `EmbeddingVector` in the `LogEntry` table or a dedicated vector index.
    - Formulate a hybrid approach, which might involve:
        - Filtering a dataset using SQL, then performing a vector search on the results.
        - Performing a vector search, then further refining or augmenting results with SQL queries on the identified log entries.
        - Triggering specific AI analysis patterns (e.g., anomaly detection) on data retrieved via SQL or vector search.
5. **Result Processing**: Format and present results in a user-friendly format, which could be textual summaries, tables, or inputs for generating visualizations. If the query was ambiguous or results are unexpected, the system might prompt the user for clarification.

#### Interactive Query Session
- Upon invoking `loganalyzer query`, the user enters an interactive chat loop.
- The user can type natural language queries. The system processes each query through the Intent Analysis Pipeline.
- **Console Output**: Query results (summaries, tables, textual answers) are displayed directly in the console.
- **Dynamic Report Generation**: Simultaneously, a Rich Markdown document (DocFX compatible) is created or updated for the interactive session (e.g., `reports/ProjectName_RunID_QueryLog_YYYYMMDDHHMMSS.md`). Each query and its corresponding rich result (including Mermaid diagrams, data tables, and textual explanations) are appended to this document.
    - This report will include appropriate DocFX metadata and will be structured to clearly delineate individual queries and their outputs.
- **Mermaid and Data Tables**: Where applicable (e.g., queries asking for trends, distributions, or specific data sets), the markdown report will include AI-generated Mermaid diagrams and formatted data tables.
- **Exiting**: The user can type "Exit" (case-insensitive) at any point to gracefully terminate the interactive chat session. The generated markdown report is finalized and saved.

#### Supported Query Types
- **Aggregation Queries**: "Show me error counts by service", "What's the average response time?"
- **Pattern Queries**: "Find all timeout errors", "Show me correlation patterns"
- **Comparative Queries**: "Compare this session to previous ones", "How has error rate changed?"
- **Analytical Queries**: "What caused the outage?", "Are there any performance bottlenecks?"
- **Trend Queries**: "Show me error trends over time", "Which services are improving?"

### AI-Powered Query Understanding
- **Pattern-Based Prompts**: Specialized prompts for query analysis and response generation
- **Context Awareness**: Understand project context and session-specific data
- **Multi-Turn Conversations**: Support follow-up questions and clarifications
- **Result Explanation**: Provide context and interpretation of query results

## Enhanced DocFX Reporting System

This system is responsible for generating comprehensive, well-structured, and visually rich reports in DocFX-compatible markdown format. Reports are generated in two main scenarios:
1.  By the `loganalyzer report --project "ProjectName" [--run-id <runId>]` command, producing a full analysis report for an analysis run including any queries the user may have run.
2.  Dynamically during an interactive `loganalyzer query` session, where each query and its results are appended to a running markdown document.

### Rich Markdown Generation
The reporting system generates DocFX-compatible markdown with advanced visualizations:

#### DocFX Metadata Integration
```yaml
---
title: Log Analysis Report - ProjectName
description: Comprehensive analysis report for project ProjectName
author: AI Log Analysis Tool
ms.date: 2024-01-15
ms.topic: analysis-report
ms.service: log-analysis
---
```

#### Mermaid Chart Integration
- **AI Generated Diagrams**: Use Mermaid syntax for dynamic diagrams, and use a pattern to generate them based on analysis results
- **Service Health Charts**: Visual representation of service status and error rates
- **Correlation Timelines**: Gantt charts showing cross-service interactions
- **Anomaly Distribution**: Pie charts and bar graphs of anomaly types and severity
- **Error Rate Trends**: Line charts tracking error rates over time

#### Interactive Report Elements
- **Collapsible Sections**: Detailed breakdowns that can be expanded/collapsed
- **Cross-References**: Links between related sections and findings
- **Table Enhancements**: Sortable tables with styling and status indicators
- **Code Blocks**: Properly formatted log excerpts and configuration examples

#### Detailed Anomaly Reports
For each detected anomaly, generate a detailed report section, potentially as its own linked markdown file (e.g., `anomaly_details_XYZ.md`) to keep the main report concise.
The report should use a "Researcher Pattern" with a search-enabled LLM model to research the error and provide insights, remediation steps, and potential impacts.

- **Scope of "Research"**:
    - The primary source for research will be the LLM's general knowledge base.
    - If the "search-enabled LLM model" implies a model with built-in web search capabilities (like some versions of GPT or custom setups with integrated search APIs), it can use that to find publicly available information, documentation, or common solutions related to the error message or observed pattern.
    - The system will NOT be pre-configured to search internal/private knowledge bases unless explicitly extended to do so. The initial scope is general knowledge and public web search if the model supports it.
- **Context for Research**:
    - **Log Context**: Provide the specific `LogEntry` (or `RawMessage`) that triggered the anomaly.
    - **Temporal Context**: Include a segment of logs surrounding the anomaly, e.g., 5 minutes of `NormalizedMessage` data before and 5 minutes after the anomalous entry, from the same `SourceFileName` or `ServiceId` if possible. This helps the LLM understand the immediate sequence of events.
    - **Anomaly Description**: Include any initial assessment of the anomaly (e.g., "Error rate spike", "Unusual error message", "Missing correlation").
- **Output Structure for Detailed Anomaly Report**: Each detailed report should aim for a structured output:
    - **Anomaly Summary**: Brief restatement of the anomaly.
    - **Log Excerpt**: The specific log line(s) identified as anomalous.
    - **Potential Causes**: Based on LLM research, list common or likely causes for this type of error/pattern.
    - **Suggested Remediation Steps**: Actionable steps to diagnose or fix the issue.
    - **Potential Impacts**: What could be the consequences if this anomaly is not addressed (e.g., service degradation, data loss, security vulnerability).
    - **References (if applicable)**: Links to external documentation or articles if the LLM's search capability provided them.
- **Prompt for Researcher Pattern**:
  ```markdown
  System: You are an expert SRE and software diagnostician. Given an anomalous log entry, surrounding log context, and an initial anomaly description, your task is to research this anomaly. Provide potential causes, suggest remediation steps, and outline potential impacts. If you have search capabilities and find relevant external resources, cite them.

  User:
  Anomaly Detected: High rate of '503 Service Unavailable' errors.
  Anomalous Log Entry: `[2024-07-25 14:30:15 ERROR payment-service] Request to downstream inventory-service failed with HTTP 503.`
  Surrounding Log Context (5 mins before/after):
  ... (paste relevant log lines here) ...

  Please provide your analysis.
  ```

With the research pattern, provide the log file along with 5 minutes of context before and after the error to the llm model.