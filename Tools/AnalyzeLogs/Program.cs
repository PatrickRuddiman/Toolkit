using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using AnalyzeLogs.Models;
using AnalyzeLogs.Services;

namespace AnalyzeLogs;

class Program
{
    static async Task<int> Main(string[] args)
    {
        // Command line options
        var pathOption = new Option<string>(
            "--path",
            () => "*.log",
            "Glob pattern for log files (e.g., '*.log', 'logs/**/*.log')"
        );
        
        var outputOption = new Option<string?>(
            "--output",
            "Output file path (optional, prints to console if not specified)"
        );
        
        var verboseOption = new Option<bool>(
            "--verbose", 
            "Enable verbose logging"
        );
        
        var modelOption = new Option<string>(
            "--model",
            () => "gpt-4o-mini",
            "AI model to use for analysis"
        );
        
        var enableCoherenceOption = new Option<bool>(
            "--coherence",
            () => true,
            "Enable coherence analysis"
        );
        
        var enableAnomalyOption = new Option<bool>(
            "--anomaly",
            () => true,
            "Enable anomaly detection"
        );
        
        var enableTaggingOption = new Option<bool>(
            "--tagging",
            () => true,
            "Enable log tagging"
        );
        
        var enableEmbeddingsOption = new Option<bool>(
            "--embeddings",
            () => true,
            "Enable semantic embeddings analysis"
        );

        var rootCommand = new RootCommand("AI-powered log analysis tool for microservice systems")
        {
            pathOption,
            outputOption,
            verboseOption,
            modelOption,
            enableCoherenceOption,
            enableAnomalyOption,
            enableTaggingOption,
            enableEmbeddingsOption
        };

        rootCommand.SetHandler(async (
            string path,
            string? output,
            bool verbose,
            string model,
            bool enableCoherence,
            bool enableAnomaly,
            bool enableTagging,
            bool enableEmbeddings) =>
        {
            try
            {
                await AnalyzeLogs(
                    path,
                    output,
                    verbose,
                    model,
                    enableCoherence,
                    enableAnomaly,
                    enableTagging,
                    enableEmbeddings
                );
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                if (verbose)
                {
                    Console.Error.WriteLine($"Stack trace: {ex.StackTrace}");
                }
                Environment.Exit(1);
            }
        },
        pathOption,
        outputOption,
        verboseOption,
        modelOption,
        enableCoherenceOption,
        enableAnomalyOption,
        enableTaggingOption,
        enableEmbeddingsOption);

        return await rootCommand.InvokeAsync(args);
    }

    private static async Task AnalyzeLogs(
        string path,
        string? output,
        bool verbose,
        string model,
        bool enableCoherence,
        bool enableAnomaly,
        bool enableTagging,
        bool enableEmbeddings)
    {
        // Setup DI container
        var services = new ServiceCollection();
        
        // Configure logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            if (verbose)
                builder.SetMinimumLevel(LogLevel.Debug);
            else
                builder.SetMinimumLevel(LogLevel.Information);
        });

        // Register services
        services.AddSingleton<Configuration>(provider => new Configuration
        {
            Model = model,
            EnableCoherenceAnalysis = enableCoherence,
            EnableAnomalyDetection = enableAnomaly,
            EnableTagging = enableTagging,
            EnableEmbeddings = enableEmbeddings,
            Verbose = verbose
        });
        
        services.AddScoped<FileIngestionService>();
        services.AddScoped<LogParsingService>();
        services.AddScoped<FabricService>();
        services.AddScoped<EmbeddingService>();
        services.AddScoped<AnalysisService>();
        services.AddScoped<ReportService>();

        var serviceProvider = services.BuildServiceProvider();
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

        logger.LogInformation("Starting log analysis for pattern: {Path}", path);

        // Get services
        var ingestionService = serviceProvider.GetRequiredService<FileIngestionService>();
        var parsingService = serviceProvider.GetRequiredService<LogParsingService>();
        var analysisService = serviceProvider.GetRequiredService<AnalysisService>();
        var reportService = serviceProvider.GetRequiredService<ReportService>();

        // Process logs
        var logFiles = await ingestionService.GetLogFilesAsync(path);
        logger.LogInformation("Found {Count} log files", logFiles.Count);

        var logEntries = new List<LogEntry>();
        foreach (var file in logFiles)
        {
            logger.LogDebug("Processing file: {File}", file);
            var entries = await parsingService.ParseLogFileAsync(file);
            logEntries.AddRange(entries);
        }

        logger.LogInformation("Parsed {Count} log entries", logEntries.Count);

        // Perform analysis
        var analysisResult = await analysisService.AnalyzeLogsAsync(logEntries);

        // Generate report
        var report = await reportService.GenerateReportAsync(analysisResult, logFiles);

        // Output report
        if (string.IsNullOrEmpty(output))
        {
            Console.WriteLine(report);
        }
        else
        {
            await File.WriteAllTextAsync(output, report);
            logger.LogInformation("Report saved to: {Output}", output);
        }
    }
}
