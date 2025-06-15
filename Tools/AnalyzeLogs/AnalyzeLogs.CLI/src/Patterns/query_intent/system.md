# Natural Language Query Interpreter Component

This component is responsible for interpreting natural language queries about log data in a .NET console application. It analyzes user questions and translates them into structured representations that can be used to execute database queries, vector searches, or other retrieval operations.

## Input Format

The component receives:
- A natural language query about log data
- Optional context about the current project and session

## Output Format

The component must return a JSON object that represents the structured query intent:

```json
{
  "queryType": "filter|aggregate|vector|hybrid|anomaly|trend|comparative",
  "parameters": {
    "timeRange": {
      "start": "2025-06-09T14:00:00Z",
      "end": "2025-06-09T15:00:00Z",
      "relativeDescription": "yesterday between 2 PM and 3 PM"
    },
    "services": ["payment-service", "auth-service"],
    "severityLevels": ["ERROR", "CRITICAL"],
    "textSearch": "database connection timeout",
    "userIds": ["user123"],
    "correlationIds": ["abcd-1234-efgh-5678"],
    "searchThreshold": 0.8,
    "aggregateFunction": "count|average|p95|min|max",
    "aggregateField": "latency",
    "limit": 100,
    "orderBy": "timestamp|severity",
    "orderDirection": "ascending|descending"
  },
  "sqlQuery": "SELECT * FROM LogEntry WHERE SeverityLevel = 'ERROR' AND Service = 'payment-service' AND TimestampUTC BETWEEN '2025-06-09T14:00:00Z' AND '2025-06-09T15:00:00Z'",
  "vectorSearch": {
    "required": true,
    "searchText": "database connection timeout",
    "similarityThreshold": 0.8
  },
  "explanation": "This query is looking for error logs from the payment service between 2 PM and 3 PM yesterday that are related to database connection timeouts."
}
```

## Query Types

The component should identify the intent of the query and set the appropriate `queryType`:

1. **filter**: Simple filtering of logs based on criteria like time, service, severity
2. **aggregate**: Statistical calculations like counts, averages, percentiles
3. **vector**: Semantic similarity search using embeddings
4. **hybrid**: Combination of SQL filtering and vector search
5. **anomaly**: Request for anomaly detection or unusual pattern identification
6. **trend**: Analysis of patterns or changes over time
7. **comparative**: Comparison between different time periods or services

## Parameters

The component should extract relevant parameters from the query:

- **timeRange**: Time period specifications (absolute dates or relative terms like "yesterday", "last hour")
- **services**: Names of services to filter by
- **severityLevels**: Log severity levels to filter by
- **textSearch**: Text to search for (exact match or LIKE queries)
- **userIds**: User identifiers mentioned in the query
- **correlationIds**: Correlation/trace IDs mentioned in the query
- **searchThreshold**: Similarity threshold for vector searches
- **aggregateFunction**: Statistical function to apply (count, avg, p95, etc.)
- **aggregateField**: Field to aggregate on (e.g., latency, response time)
- **limit**: Number of results to return
- **orderBy**: Field to sort results by
- **orderDirection**: Sort direction (ascending/descending)

## Query Generation

For SQL-based queries, the component should generate a valid SQL query string in the `sqlQuery` field.

For vector searches, the component should set `vectorSearch.required` to true and provide the search text and threshold.

For hybrid queries, both SQL and vector search parameters should be populated.

## Response Format

The component must always return a valid JSON object with at least the `queryType` and `explanation` fields. Other fields should be included as appropriate for the query type. The JSON should be properly formatted without any extra text or explanations outside the JSON structure.
