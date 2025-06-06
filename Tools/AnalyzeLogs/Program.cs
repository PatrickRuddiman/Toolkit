using System.CommandLine;
using System.Text;
using AnalyzeLogs.Models;
using AnalyzeLogs.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MsLogLevel = Microsoft.Extensions.Logging.LogLevel;

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

        var verboseOption = new Option<bool>("--verbose", "Enable verbose logging");

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

        var enableTaggingOption = new Option<bool>("--tagging", () => true, "Enable log tagging");
        var enableEmbeddingsOption = new Option<bool>(
            "--embeddings",
            () => true,
            "Enable semantic embeddings analysis"
        );

        // Main analyze command
        var analyzeCommand = new Command("analyze", "Analyze log files");
        analyzeCommand.AddOption(pathOption);
        analyzeCommand.AddOption(outputOption);
        analyzeCommand.AddOption(verboseOption);
        analyzeCommand.AddOption(modelOption);
        analyzeCommand.AddOption(enableCoherenceOption);
        analyzeCommand.AddOption(enableAnomalyOption);
        analyzeCommand.AddOption(enableTaggingOption);
        analyzeCommand.AddOption(enableEmbeddingsOption);

        analyzeCommand.SetHandler(
            async (
                string path,
                string? output,
                bool verbose,
                string model,
                bool enableCoherence,
                bool enableAnomaly,
                bool enableTagging,
                bool enableEmbeddings
            ) =>
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
            enableEmbeddingsOption
        );

        // Setup command
        var setupCommand = new Command("setup", "Configure API keys and settings");
        setupCommand.SetHandler(async () =>
        {
            await SetupConfiguration();
        });

        var rootCommand = new RootCommand("AI-powered log analysis tool for microservice systems")
        {
            analyzeCommand,
            setupCommand,
        };

        // If no subcommand is provided, default to analyze with current options for backward compatibility
        if (args.Length == 0 || !args.Any(arg => arg == "analyze" || arg == "setup"))
        {
            // Add options to root command for backward compatibility
            rootCommand.AddOption(pathOption);
            rootCommand.AddOption(outputOption);
            rootCommand.AddOption(verboseOption);
            rootCommand.AddOption(modelOption);
            rootCommand.AddOption(enableCoherenceOption);
            rootCommand.AddOption(enableAnomalyOption);
            rootCommand.AddOption(enableTaggingOption);
            rootCommand.AddOption(enableEmbeddingsOption);

            rootCommand.SetHandler(
                async (
                    string path,
                    string? output,
                    bool verbose,
                    string model,
                    bool enableCoherence,
                    bool enableAnomaly,
                    bool enableTagging,
                    bool enableEmbeddings
                ) =>
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
                enableEmbeddingsOption
            );
        }

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
        bool enableEmbeddings
    )
    {
        // Setup DI container
        var services = new ServiceCollection();
        // Configure logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            if (verbose)
                builder.SetMinimumLevel(MsLogLevel.Debug);
            else
                builder.SetMinimumLevel(MsLogLevel.Information);
        });
        
        // Register services
        services.AddSingleton<Configuration>(provider => new Configuration
        {
            Model = model,
            EnableCoherenceAnalysis = enableCoherence,
            EnableAnomalyDetection = enableAnomaly,
            EnableTagging = enableTagging,
            EnableEmbeddings = enableEmbeddings,
            Verbose = verbose,
        });
        services.AddScoped<ConfigurationService>();
        services.AddScoped<FileIngestionService>();
        services.AddScoped<LogParsingService>();
        services.AddScoped<FabricService>();
        services.AddScoped<EmbeddingService>(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<EmbeddingService>>();
            var configService = provider.GetRequiredService<ConfigurationService>();
            return new EmbeddingService(logger, configService);
        });
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

    private static async Task SetupConfiguration()
    {
        Console.WriteLine("=== AnalyzeLogs Configuration Setup ===");
        Console.WriteLine();
        
        // Setup basic logging for setup
        using var loggerFactory = LoggerFactory.Create(builder =>
            builder.AddConsole().SetMinimumLevel(MsLogLevel.Information));
        
        var logger = loggerFactory.CreateLogger<ConfigurationService>();
        var configService = new ConfigurationService(logger);

        // Check current configuration status
        var hasApiKey = await configService.HasApiKeyAsync();
        
        Console.WriteLine($"Configuration Status:");
        Console.WriteLine($"- Configuration file: {configService.GetConfigFilePath()}");
        Console.WriteLine($"- API key configured: {(hasApiKey ? "Yes" : "No")}");
        Console.WriteLine();

        // Main setup menu
        while (true)
        {
            Console.WriteLine("Setup Options:");
            Console.WriteLine("1. Configure OpenAI API Key");
            Console.WriteLine("2. View current configuration");
            Console.WriteLine("3. Test API key");
            Console.WriteLine("4. Remove configuration");
            Console.WriteLine("5. Exit setup");
            Console.WriteLine();
            Console.Write("Select an option (1-5): ");

            var choice = Console.ReadLine()?.Trim();

            switch (choice)
            {
                case "1":
                    await ConfigureApiKey(configService);
                    break;
                case "2":
                    await ViewConfiguration(configService);
                    break;
                case "3":
                    await TestApiKey(configService);
                    break;
                case "4":
                    await RemoveConfiguration(configService);
                    break;
                case "5":
                    Console.WriteLine("Setup complete!");
                    return;
                default:
                    Console.WriteLine("Invalid option. Please select 1-5.");
                    break;
            }

            Console.WriteLine();
        }
    }

    private static async Task ConfigureApiKey(ConfigurationService configService)
    {
        Console.WriteLine("=== Configure OpenAI API Key ===");
        Console.WriteLine();
        Console.WriteLine("You can obtain an API key from: https://platform.openai.com/api-keys");
        Console.WriteLine("The API key will be stored securely in your user profile.");
        Console.WriteLine();

        Console.Write("Enter your OpenAI API key (input will be hidden): ");
        
        // Hide input for security
        var apiKey = ReadPasswordFromConsole();
        Console.WriteLine();

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Console.WriteLine("No API key entered. Configuration cancelled.");
            return;
        }

        // Basic validation
        if (!apiKey.StartsWith("sk-"))
        {
            Console.WriteLine("Warning: OpenAI API keys typically start with 'sk-'");
            Console.Write("Continue anyway? (y/N): ");
            var confirm = Console.ReadLine()?.Trim().ToLowerInvariant();
            if (confirm != "y" && confirm != "yes")
            {
                Console.WriteLine("Configuration cancelled.");
                return;
            }
        }

        try
        {
            await configService.SaveApiKeyAsync(apiKey);
            Console.WriteLine("✓ API key saved successfully!");
            Console.WriteLine($"Configuration saved to: {configService.GetConfigFilePath()}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Failed to save API key: {ex.Message}");
        }
    }

    private static async Task ViewConfiguration(ConfigurationService configService)
    {
        Console.WriteLine("=== Current Configuration ===");
        Console.WriteLine();
        
        var hasApiKey = await configService.HasApiKeyAsync();
        var configPath = configService.GetConfigFilePath();
        
        Console.WriteLine($"Configuration file: {configPath}");
        Console.WriteLine($"File exists: {File.Exists(configPath)}");
        Console.WriteLine($"API key configured: {(hasApiKey ? "Yes" : "No")}");
        
        if (hasApiKey)
        {
            var apiKey = await configService.GetApiKeyAsync();
            if (!string.IsNullOrEmpty(apiKey))
            {
                // Show only first few and last few characters for security
                var maskedKey = $"{apiKey[..Math.Min(8, apiKey.Length)]}...{apiKey[^4..]}";
                Console.WriteLine($"API key (masked): {maskedKey}");
            }
        }

        // Show environment variable status
        var envApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        Console.WriteLine(
            $"Environment variable OPENAI_API_KEY: {(!string.IsNullOrEmpty(envApiKey) ? "Set" : "Not set")}");
    }

    private static async Task TestApiKey(ConfigurationService configService)
    {
        Console.WriteLine("=== Test API Key ===");
        Console.WriteLine();
        
        using var loggerFactory = LoggerFactory.Create(builder =>
            builder.AddConsole().SetMinimumLevel(MsLogLevel.Warning));
        
        var logger = loggerFactory.CreateLogger<EmbeddingService>();
        
        var embeddingService = new EmbeddingService(logger, configService);

        try
        {
            var hasKey = await embeddingService.HasApiKeyConfiguredAsync();
            if (!hasKey)
            {
                Console.WriteLine("✗ No API key configured.");
                return;
            }

            Console.WriteLine("Testing API key by generating a simple embedding...");
            // Create a test log entry
            var testEntries = new List<LogEntry>
            {
                new LogEntry
                {
                    Id = Guid.NewGuid().ToString(),
                    Timestamp = DateTime.UtcNow,
                    Level = Models.LogLevel.Info,
                    Message = "Test log entry for API validation",                    Service = "test-service",
                },
            };

            await embeddingService.GenerateEmbeddingsAsync(testEntries, false);

            if (testEntries[0].Embedding != null)
            {
                Console.WriteLine("✓ API key is working correctly!");
                Console.WriteLine(
                    $"Generated embedding with {testEntries[0].Embedding.Length} dimensions");
            }
            else
            {
                Console.WriteLine("✗ API key test failed - no embedding was generated");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ API key test failed: {ex.Message}");
        }
        finally
        {
            embeddingService.Dispose();
        }
    }

    private static async Task RemoveConfiguration(ConfigurationService configService)
    {
        Console.WriteLine("=== Remove Configuration ===");
        Console.WriteLine();
        
        var hasApiKey = await configService.HasApiKeyAsync();
        if (!hasApiKey)
        {
            Console.WriteLine("No configuration found to remove.");
            return;
        }

        Console.Write("Are you sure you want to remove the saved configuration? (y/N): ");        var confirm = Console.ReadLine()?.Trim().ToLowerInvariant();
        
        if (confirm != "y" && confirm != "yes")
        {
            Console.WriteLine("Configuration removal cancelled.");
            return;
        }

        try
        {
            await configService.RemoveConfigAsync();
            Console.WriteLine("✓ Configuration removed successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Failed to remove configuration: {ex.Message}");
        }
    }

    private static string ReadPasswordFromConsole()
    {
        var password = new StringBuilder();
        ConsoleKeyInfo keyInfo;

        do
        {
            keyInfo = Console.ReadKey(true);

            if (keyInfo.Key == ConsoleKey.Backspace)
            {
                if (password.Length > 0)
                {
                    password.Length--;
                    Console.Write("\b \b");
                }
            }
            else if (keyInfo.Key != ConsoleKey.Enter)
            {
                password.Append(keyInfo.KeyChar);
                Console.Write("*");
            }
        } while (keyInfo.Key != ConsoleKey.Enter);

        return password.ToString();
    }
}
