# Natural Language Query Engine

## Query Processing Architecture

The query engine transforms natural language questions into database operations and analytical insights through an intelligent processing pipeline.

## Intent Analysis Pipeline

### 1. Query Preprocessing
- **Text Normalization**: Lowercase conversion, punctuation handling
- **Typo Correction**: Basic spelling correction for common technical terms
- **Input Validation**: Ensure query is meaningful and processable
- **Context Extraction**: Extract implicit context from previous queries

### 2. Intent Classification
AI determines the type of query and appropriate processing strategy:

#### Query Types
- **Aggregation Queries**: "Show me error counts by service", "What's the average response time?"
- **Filtering Queries**: "Find all timeout errors", "Show logs from payment service"
- **Pattern Queries**: "Show me correlation patterns", "Find retry sequences"
- **Analytical Queries**: "What caused the outage?", "Are there any performance bottlenecks?"
- **Comparative Queries**: "Compare this run to previous ones", "How has error rate changed?"
- **Trend Queries**: "Show me error trends over time", "Which services are improving?"

#### Intent Classification Implementation
```csharp
public class QueryIntentClassifier
{
    public async Task<QueryIntent> ClassifyAsync(string query)
    {
        var pattern = await _patternLoader.LoadPatternAsync("query_intent");
        var response = await _openAiService.AnalyzeAsync(pattern, query);
        
        return new QueryIntent
        {
            Type = ParseQueryType(response),
            Entities = ExtractEntities(response),
            Parameters = ExtractParameters(response),
            Confidence = CalculateConfidence(response)
        };
    }
}
```

### 3. Parameter Extraction
Extract key entities and parameters from natural language queries:

#### Entity Types
- **Service Names**: "payment service", "user authentication", "api gateway"
- **Time Ranges**: "last hour", "yesterday", "between 2pm and 4pm"
- **Error Types**: "timeout errors", "database failures", "authentication issues"
- **Metrics**: "error rate", "response time", "throughput", "p95 latency"
- **Identifiers**: User IDs, correlation IDs, transaction IDs
- **Severity Levels**: "critical errors", "warnings", "debug logs"

#### Parameter Extraction Example
```csharp
public class ParameterExtractor
{
    public QueryParameters ExtractParameters(string query, QueryIntent intent)
    {
        return new QueryParameters
        {
            Services = ExtractServiceNames(query),
            TimeRange = ExtractTimeRange(query),
            SeverityLevels = ExtractSeverityLevels(query),
            Keywords = ExtractKeywords(query),
            Metrics = ExtractMetrics(query),
            Filters = ExtractFilters(query)
        };
    }
}
```

### 4. Query Generation and Execution

#### SQL Query Generation
For structured data queries against SQLite:

```csharp
public class SqlQueryGenerator
{
    public string GenerateQuery(QueryIntent intent, QueryParameters parameters)
    {
        var query = new StringBuilder("SELECT ");
        
        // Build SELECT clause based on intent
        query.Append(BuildSelectClause(intent, parameters));
        query.Append(" FROM LogEntry le ");
        
        // Add JOINs if needed
        query.Append(BuildJoinClauses(intent, parameters));
        
        // Build WHERE clause
        query.Append(BuildWhereClause(parameters));
        
        // Add GROUP BY, ORDER BY as needed
        query.Append(BuildGroupByClause(intent, parameters));
        query.Append(BuildOrderByClause(intent, parameters));
        
        return query.ToString();
    }
}
```

#### Vector Search Integration
For semantic similarity queries:

```csharp
public class SemanticQueryProcessor
{
    public async Task<List<LogEntry>> ProcessSemanticQuery(
        string query, QueryParameters parameters)
    {
        // Generate embedding for the query
        var queryEmbedding = await _openAiService.GenerateEmbeddingAsync(query);
        
        // Perform vector search
        var similarLogs = _vectorSearchService.FindSimilar(
            queryEmbedding, topK: parameters.MaxResults ?? 50);
        
        // Apply additional filters
        return ApplyFilters(similarLogs, parameters);
    }
}
```

## Interactive Query Session

### Session Management
```csharp
public class InteractiveQuerySession
{
    private readonly QueryProcessor _queryProcessor;
    private readonly ReportGenerator _reportGenerator;
    private readonly string _sessionReportPath;
    
    public async Task StartSessionAsync(string projectName, string runId)
    {
        // Initialize session report
        _sessionReportPath = GenerateSessionReportPath(projectName, runId);
        await InitializeSessionReport();
        
        Console.WriteLine("Interactive query session started. Type 'Exit' to end.");
        
        while (true)
        {
            Console.Write("> ");
            var query = Console.ReadLine();
            
            if (string.IsNullOrWhiteSpace(query) || 
                query.Equals("exit", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }
            
            await ProcessQueryAsync(query);
        }
        
        await FinalizeSessionReport();
    }
}
```

### Dynamic Report Generation
During interactive sessions, queries and results are continuously appended to a DocFX-compatible markdown report:

```csharp
public async Task ProcessQueryAsync(string query)
{
    try
    {
        // Process the query
        var result = await _queryProcessor.ProcessAsync(query);
        
        // Display results in console
        DisplayResults(result);
        
        // Append to session report
        await AppendToSessionReport(query, result);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error processing query: {ex.Message}");
        await AppendErrorToReport(query, ex.Message);
    }
}
```

### Session Report Structure
```markdown
---
title: Interactive Query Session - ProjectName
description: Query session for analysis run RunID
author: AI Log Analysis Tool
ms.date: 2024-01-15
ms.topic: query-session
---

# Interactive Query Session

**Project**: ProjectName  
**Analysis Run**: RunID  
**Session Started**: 2024-01-15 14:30:00 UTC

## Query 1: Show me all errors in the last hour

### Results
- **Total Errors Found**: 23
- **Services Affected**: 3 (payment-service, user-auth, api-gateway)
- **Most Common Error**: Database connection timeout (12 occurrences)

### Error Distribution
| Service | Error Count | Most Common Error |
|---------|-------------|-------------------|
| payment-service | 12 | Database timeout |
| user-auth | 8 | Authentication failure |
| api-gateway | 3 | Rate limit exceeded |

## Query 2: What caused the database timeouts?
...
```

## AI-Powered Query Understanding

### Context Awareness
- **Project Context**: Understand which project and analysis run is being queried
- **Previous Queries**: Maintain context from earlier queries in the session
- **Domain Knowledge**: Apply understanding of log analysis and system operations
- **Implicit Parameters**: Infer missing parameters from context

### Multi-Turn Conversations
```csharp
public class ConversationContext
{
    public List<QueryHistory> PreviousQueries { get; set; } = new();
    public Dictionary<string, object> SessionState { get; set; } = new();
    
    public void AddQuery(string query, QueryResult result)
    {
        PreviousQueries.Add(new QueryHistory
        {
            Query = query,
            Result = result,
            Timestamp = DateTime.UtcNow
        });
        
        // Update session state based on query results
        UpdateSessionState(result);
    }
}
```

### Follow-up Question Handling
- **Reference Resolution**: Handle pronouns and references to previous results
- **Context Continuation**: Build upon previous query results
- **Clarification Requests**: Ask for clarification when queries are ambiguous
- **Suggestion Generation**: Suggest related queries based on current results

## Query Optimization and Performance

### Query Caching
- **Result Caching**: Cache results for identical queries
- **Embedding Caching**: Reuse embeddings for similar semantic queries
- **SQL Query Plan Caching**: Optimize database query performance
- **Session-Level Caching**: Maintain cache during interactive sessions

### Performance Monitoring
- **Query Execution Time**: Track time for different query types
- **Resource Usage**: Monitor memory and CPU usage during queries
- **API Call Optimization**: Minimize unnecessary AI API calls
- **Database Performance**: Optimize SQL queries and indexing
