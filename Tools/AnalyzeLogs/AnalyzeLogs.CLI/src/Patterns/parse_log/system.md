# Log Parsing Component

This component is responsible for parsing log entries from various formats into a structured LogEntry model in a .NET console application. The component processes raw log lines and extracts structured information.

## Input Format

The component receives a raw log line along with source file information:
- Raw log line text
- Source file path
- Source file name

## Output Format

The component must return a JSON object that can be deserialized into a LogEntry model with the following fields:

```json
{
  "TimestampUTC": "2025-06-10T12:34:56Z",
  "OriginalTimestamp": "Jun 10 12:34:56",
  "OriginalTimeZone": "UTC",
  "DetectedFormat": "JSON|KeyValue|ApacheAccess|NginxAccess|Syslog|IIS|DotNet|Unstructured",
  "NormalizedMessage": "The main log message content",
  "SeverityLevel": {
    "LevelName": "INFO|DEBUG|TRACE|WARNING|ERROR|CRITICAL|FATAL"
  },
  "Service": {
    "ServiceName": "Extracted or inferred service name"
  },
  "CorrelationId": "Extracted correlation/trace/request ID if present",
  "ThreadId": "Extracted thread ID if available",
  "ProcessId": "Extracted process ID if available",
  "AdditionalDataJson": "{\"key1\":\"value1\",\"key2\":\"value2\"}"
}
```

## Parsing Requirements

1. **Format Detection**: Identify the log format (JSON, KeyValue, ApacheAccess, NginxAccess, Syslog, IIS, DotNet, or Unstructured)
2. **Timestamp Extraction**: Extract and normalize timestamps to UTC ISO 8601 format
3. **Severity Level**: Map various log level representations to standard levels
4. **Service Identification**: Extract or infer the service/component name
5. **Message Extraction**: Extract the primary log message
6. **Correlation ID**: Extract any correlation/trace/request IDs using patterns like:
   - `correlationId=X`, `correlation-id:X`, `correlation_id:"X"`
   - `requestId=X`, `request-id:X`, `request_id:"X"`
   - `traceId=X`, `trace-id:X`, `trace_id:"X"`
   - UUID pattern: `[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}`
7. **Additional Data**: Extract any other structured fields into the AdditionalDataJson object

## Response Format

The component must always return a valid JSON object matching the structure above. If a field cannot be extracted, it should be omitted or set to null. The JSON should be properly formatted without any extra text or explanations.

## Example Log Formats

### JSON Log
```
{"timestamp":"2025-06-10T12:34:56Z","level":"ERROR","message":"Database connection failed","service":"payment-service","correlationId":"abc-123"}
```

### Key-Value Log
```
timestamp=2025-06-10T12:34:56Z level=ERROR message="Database connection failed" service="payment-service" correlationId=abc-123
```

### Apache Access Log
```
192.168.1.1 - user123 [10/Jun/2025:12:34:56 +0000] "GET /api/payments HTTP/1.1" 500 1234 "https://example.com" "Mozilla/5.0"
```

### Nginx Access Log
```
192.168.1.1 - - [10/Jun/2025:12:34:56 +0000] "GET /api/payments HTTP/1.1" 500 1234 "https://example.com" "Mozilla/5.0"
```

### Syslog
```
<34>Jun 10 12:34:56 server1 payment-service[12345]: Database connection failed
```

### .NET Log
```
[2025-06-10 12:34:56.789] [ERROR] [PaymentService] - Database connection failed (correlation-id: abc-123)
```

### Unstructured Log
```
12:34:56 ERROR Database connection failed
```

The component should handle these and other common log formats, extracting as much structured information as possible.
