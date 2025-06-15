# Log Correlation and Cross-Service Analysis

## Correlation Strategies

### Correlation IDs and Trace Linking
In microservice architectures, one request often generates logs across multiple services. The tool leverages correlation identifiers when available.

#### Correlation ID Types
- **TraceId**: Distributed tracing identifiers
- **CorrelationId**: Custom correlation identifiers  
- **RequestId**: Request-specific identifiers
- **SessionId**: User session identifiers
- **TransactionId**: Business transaction identifiers

#### Implementation Approach
- Extract correlation IDs from JSON fields or embedded text
- Use regex patterns to identify correlation IDs in unstructured logs
- Group log entries by correlation ID to reconstruct end-to-end flows
- Build transaction timelines showing complete request flows across services

### Time-Based Correlation
When correlation IDs are absent, use temporal and semantic hints:

#### Temporal Proximity Analysis
- Sort all log entries by timestamp across all services
- Identify events occurring within close time windows
- Flag potentially related events (especially error sequences)
- Look for cause-and-effect patterns (error in Service A followed by error in Service B)

#### Implementation Strategy
```csharp
// Example: Find events within time window
var timeWindow = TimeSpan.FromSeconds(5);
var relatedEvents = logs
    .Where(log => Math.Abs((log.Timestamp - targetEvent.Timestamp).TotalSeconds) <= timeWindow.TotalSeconds)
    .Where(log => log.ServiceName != targetEvent.ServiceName)
    .ToList();
```

### Semantic Correlation
Identify relationships through content analysis:

#### Message Keyword Analysis
- Extract entities from log messages (order IDs, user IDs, etc.)
- Find common identifiers across different services
- Use regex patterns to identify business entities
- Cross-reference entities to establish relationships

#### AI-Assisted Correlation
- Use LLM to analyze temporally adjacent logs from different services
- Identify narrative connections between events
- Detect cause-and-effect relationships through semantic analysis
- Generate hypotheses about event relationships

## Vector-Search-Assisted Semantic Correlation

### Embedding-Based Correlation
When explicit correlation IDs are absent, use vector search on log message embeddings:

#### Implementation Process
1. **Generate Embeddings**: Convert log messages to semantic vectors
2. **Similarity Search**: Find semantically similar entries across services
3. **Time Proximity Filter**: Limit search to temporally relevant time windows
4. **Relationship Scoring**: Score potential relationships based on semantic similarity and temporal proximity

#### Use Cases
- Find related events with different wording across services
- Identify cascading failures with varying error descriptions
- Correlate user actions across different system components
- Discover patterns in error propagation

## Cross-Service Analysis Features

### Transaction Reconstruction
Build complete transaction flows across microservices:

#### Flow Visualization
- Timeline reconstruction showing complete request paths
- Service interaction mapping
- Identify bottlenecks and failure points
- Measure end-to-end transaction latency

#### Anomaly Detection in Flows
- Identify incomplete transactions (missing expected log entries)
- Detect unusual service interaction patterns
- Flag abnormal timing between service calls
- Identify retry patterns and failure cascades

### Service Dependency Analysis
Analyze relationships between services:

#### Dependency Mapping
- Identify which services call which other services
- Build service dependency graphs
- Detect circular dependencies
- Map data flow patterns

#### Health Correlation
- Correlate health metrics across dependent services
- Identify upstream/downstream impact patterns
- Detect service degradation propagation
- Analyze failure isolation effectiveness

## Correlation Data Models

### CorrelationGroup Table
```sql
CREATE TABLE CorrelationGroup (
    CorrelationGroupId TEXT PRIMARY KEY,
    AnalysisRunId TEXT NOT NULL,
    CorrelationId TEXT NOT NULL,
    StartTime DATETIME NOT NULL,
    EndTime DATETIME NOT NULL,
    ServiceCount INTEGER NOT NULL,
    LogEntryCount INTEGER NOT NULL,
    Status TEXT NOT NULL, -- Complete, Incomplete, Error
    FOREIGN KEY (AnalysisRunId) REFERENCES AnalysisRun(AnalysisRunId)
);
```

### ServiceInteraction Table
```sql
CREATE TABLE ServiceInteraction (
    ServiceInteractionId TEXT PRIMARY KEY,
    AnalysisRunId TEXT NOT NULL,
    FromService TEXT NOT NULL,
    ToService TEXT NOT NULL,
    InteractionCount INTEGER NOT NULL,
    AvgLatencyMs DECIMAL,
    ErrorCount INTEGER NOT NULL,
    FirstSeen DATETIME NOT NULL,
    LastSeen DATETIME NOT NULL,
    FOREIGN KEY (AnalysisRunId) REFERENCES AnalysisRun(AnalysisRunId)
);
```

## Analysis Capabilities

### Correlation Quality Metrics
- **Coverage**: Percentage of logs successfully correlated
- **Accuracy**: Manual validation of correlation accuracy
- **Completeness**: Detection of incomplete transaction flows
- **Timeliness**: Speed of correlation analysis

### Correlation Insights
- **Flow Analysis**: Complete transaction path reconstruction
- **Bottleneck Detection**: Identify performance constraints
- **Failure Analysis**: Root cause analysis across services
- **Pattern Recognition**: Identify recurring interaction patterns
