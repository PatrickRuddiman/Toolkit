# CLI Commands and User Interface

## Command Structure

The CLI features a comprehensive set of commands for managing projects and performing analysis.

### Project Management Commands

#### Create Project
```bash
loganalyzer project create --name "ProjectName" --description "Optional Description" --log-pattern "logs/**/*.log"
```
Creates a new project with specified configuration.

#### Delete Project
```bash
loganalyzer project delete "ProjectName"
```
Deletes a project and its associated data after confirmation.

### Analysis Management Commands

#### Run Analysis
```bash
loganalyzer analyze --project "ProjectName" [--glob "specific_run_logs/*.log"] [--name "RunName"]
```
Starts a new analysis run for the specified project. Uses project's default glob if not provided.

#### Delete Analysis Run
```bash
loganalyzer delete --project "ProjectName" --run-id <runId>
```
Deletes an analysis run and its associated data after confirmation.

### Querying & Reporting Commands

#### Interactive Query Session
```bash
loganalyzer query --project "ProjectName" [--run-id <runId>] "[Optional initial natural language query]"
```
Initiates an interactive chat session for querying the specified project's latest run (or specific run if provided). 

**Features:**
- Execute immediate query if provided, otherwise prompt user
- Results displayed in console
- Continuously updated DocFX-compatible markdown report generated
- Type "Exit" to end session

#### Generate Report
```bash
loganalyzer report --project "ProjectName" [--run-id <runId>] --output-path "reports/ProjectName_Report.md" [--format docfx|markdown]
```
Generates a comprehensive report for a completed analysis run.

## General Options

### Global Flags
- `--verbose, -v`: Enables verbose output for CLI operations
- `--help, -h`: Displays help information for commands and subcommands

## User Experience Features

### Interactive Prompts
For critical operations like deletion, the CLI requires user confirmation:
```
Are you sure you want to delete project 'MySystem'? [y/N]
```

### Progress Indication
For long-running operations (ingestion, AI analysis, report generation):
- Spinners for indeterminate progress
- Progress bars for measurable operations  
- Percentage completion updates
- Clear status messages

### Error Handling
- Clear error messages indicating failed command
- Descriptive explanation of what went wrong
- Suggested next steps for resolution
- Graceful degradation when possible

## Usage Examples

### Typical Workflow
```bash
# Create a new project
loganalyzer project create --name "WebAPI" --log-pattern "logs/webapi/**/*.log"

# Run analysis
loganalyzer analyze --project "WebAPI"

# Query the results interactively
loganalyzer query --project "WebAPI" "Show me all errors in the last hour"

# Generate final report
loganalyzer report --project "WebAPI" --output-path "reports/webapi-analysis.md"
```

### Advanced Usage
```bash
# Run analysis with specific logs
loganalyzer analyze --project "WebAPI" --glob "incident-logs/*.log" --name "Incident-2024-06-15"

# Query specific run
loganalyzer query --project "WebAPI" --run-id "12345" "What caused the timeout errors?"

# Verbose analysis
loganalyzer analyze --project "WebAPI" --verbose
```
