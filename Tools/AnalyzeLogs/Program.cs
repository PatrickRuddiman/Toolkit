using System.CommandLine;
using System.Text;
using System.Text.Json;
using AnalyzeLogs.Data;
using AnalyzeLogs.Models;
using AnalyzeLogs.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MsLogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace AnalyzeLogs;

class Program
{
    /// <summary>
    /// Validates that an OpenAI API key is configured before running analysis
    /// </summary>
    private static async Task<bool> ValidateApiKeyAsync(bool verbose = false)
    {
        using var services = CreateServiceProvider(verbose);
        var configService = services.GetRequiredService<ConfigurationService>();
        var openAIService = services.GetRequiredService<OpenAIService>();

        bool hasApiKey = await openAIService.HasApiKeyConfiguredAsync();
        if (!hasApiKey)
        {
            Console.Error.WriteLine("❌ Error: OpenAI API key is not configured!");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Please configure your API key using one of these methods:");
            Console.Error.WriteLine("1. Run: AnalyzeLogs setup");
            Console.Error.WriteLine("2. Set environment variable: OPENAI_API_KEY=your_key_here");
            Console.Error.WriteLine();
            Console.Error.WriteLine(
                "You can get an API key from: https://platform.openai.com/api-keys"
            );
            return false;
        }

        return true;
    }

    static async Task<int> Main(string[] args)
    {
        // Command line options
        Option<string> pathOption = new Option<string>(
            "--path",
            () => "*.log",
            "Glob pattern for log files (e.g., '*.log', 'logs/**/*.log')"
        );

        Option<string?> outputOption = new Option<string?>(
            "--output",
            "Output file path (optional, prints to console if not specified)"
        );

        Option<bool> verboseOption = new Option<bool>("--verbose", "Enable verbose logging");

        Option<string> modelOption = new Option<string>(
            "--model",
            () => "gpt-4o-mini",
            "AI model to use for analysis"
        );

        Option<bool> enableCoherenceOption = new Option<bool>(
            "--coherence",
            () => true,
            "Enable coherence analysis"
        );

        Option<bool> enableAnomalyOption = new Option<bool>(
            "--anomaly",
            () => true,
            "Enable anomaly detection"
        );

        Option<bool> enableTaggingOption = new Option<bool>(
            "--tagging",
            () => true,
            "Enable log tagging"
        );
        Option<bool> enableEmbeddingsOption = new Option<bool>(
            "--embeddings",
            () => true,
            "Enable semantic embeddings analysis"
        );

        // Main analyze command
        Command analyzeCommand = new Command("analyze", "Analyze log files");
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
        ); // Setup command
        Command setupCommand = new Command("setup", "Configure API keys and settings");
        setupCommand.SetHandler(async () =>
        {
            await SetupConfiguration();
        });

        // Project management commands
        Command projectCommand = new Command("project", "Manage analysis projects");

        // Project create command
        Command createProjectCommand = new Command("create", "Create a new project");
        Option<string> projectNameOption = new Option<string>("--name", "Project name")
        {
            IsRequired = true,
        };
        Option<string?> projectDescriptionOption = new Option<string?>(
            "--description",
            "Project description"
        );
        createProjectCommand.AddOption(projectNameOption);
        createProjectCommand.AddOption(projectDescriptionOption);
        createProjectCommand.SetHandler(
            async (string name, string? description) =>
            {
                await CreateProject(name, description);
            },
            projectNameOption,
            projectDescriptionOption
        );

        // Project list command
        Command listProjectsCommand = new Command("list", "List all projects");
        listProjectsCommand.SetHandler(async () =>
        {
            await ListProjects();
        });

        // Project delete command
        Command deleteProjectCommand = new Command("delete", "Delete a project");
        Option<string> deleteProjectNameOption = new Option<string>(
            "--name",
            "Project name to delete"
        )
        {
            IsRequired = true,
        };
        deleteProjectCommand.AddOption(deleteProjectNameOption);
        deleteProjectCommand.SetHandler(
            async (string name) =>
            {
                await DeleteProject(name);
            },
            deleteProjectNameOption
        );

        // Project analyze command (enhanced analyze with project support)
        Command projectAnalyzeCommand = new Command("analyze", "Analyze logs within a project");
        Option<string> analyzeProjectNameOption = new Option<string>("--project", "Project name")
        {
            IsRequired = true,
        };
        Option<string?> sessionNameOption = new Option<string?>(
            "--session",
            "Session name (optional, auto-generated if not provided)"
        );
        Option<bool> generateReportOption = new Option<bool>(
            "--report",
            () => true,
            "Generate markdown report"
        );

        projectAnalyzeCommand.AddOption(analyzeProjectNameOption);
        projectAnalyzeCommand.AddOption(sessionNameOption);
        projectAnalyzeCommand.AddOption(pathOption);
        projectAnalyzeCommand.AddOption(outputOption);
        projectAnalyzeCommand.AddOption(verboseOption);
        projectAnalyzeCommand.AddOption(modelOption);
        projectAnalyzeCommand.AddOption(enableCoherenceOption);
        projectAnalyzeCommand.AddOption(enableAnomalyOption);
        projectAnalyzeCommand.AddOption(enableTaggingOption);
        projectAnalyzeCommand.AddOption(enableEmbeddingsOption);
        projectAnalyzeCommand.AddOption(generateReportOption);
        projectAnalyzeCommand.SetHandler(
            async (context) =>
            {
                var projectName = context.ParseResult.GetValueForOption(analyzeProjectNameOption)!;
                var sessionName = context.ParseResult.GetValueForOption(sessionNameOption);
                var path = context.ParseResult.GetValueForOption(pathOption)!;
                var output = context.ParseResult.GetValueForOption(outputOption);
                var verbose = context.ParseResult.GetValueForOption(verboseOption);
                var model = context.ParseResult.GetValueForOption(modelOption)!;
                var enableCoherence = context.ParseResult.GetValueForOption(enableCoherenceOption);
                var enableAnomaly = context.ParseResult.GetValueForOption(enableAnomalyOption);
                var enableTagging = context.ParseResult.GetValueForOption(enableTaggingOption);
                var enableEmbeddings = context.ParseResult.GetValueForOption(
                    enableEmbeddingsOption
                );
                var generateReport = context.ParseResult.GetValueForOption(generateReportOption);

                await AnalyzeLogsWithProject(
                    projectName,
                    sessionName,
                    path,
                    output,
                    verbose,
                    model,
                    enableCoherence,
                    enableAnomaly,
                    enableTagging,
                    enableEmbeddings,
                    generateReport
                );
            }
        );

        // Project report command
        Command reportCommand = new Command("report", "Generate reports from stored data");
        Option<string> reportProjectNameOption = new Option<string>("--project", "Project name")
        {
            IsRequired = true,
        };
        Option<int?> reportSessionIdOption = new Option<int?>(
            "--session",
            "Session ID (optional, generates project report if not provided)"
        );
        Option<string?> reportOutputOption = new Option<string?>(
            "--output",
            "Output file path (optional, prints to console if not specified)"
        );

        reportCommand.AddOption(reportProjectNameOption);
        reportCommand.AddOption(reportSessionIdOption);
        reportCommand.AddOption(reportOutputOption);
        reportCommand.SetHandler(
            async (string projectName, int? sessionId, string? output) =>
            {
                await GenerateReport(projectName, sessionId, output);
            },
            reportProjectNameOption,
            reportSessionIdOption,
            reportOutputOption
        );

        // Project query command
        Command queryCommand = new Command("query", "Query project data using natural language");
        Option<string> queryProjectNameOption = new Option<string>("--project", "Project name")
        {
            IsRequired = true,
        };
        Option<string> queryTextOption = new Option<string>("--query", "Natural language query")
        {
            IsRequired = true,
        };
        Option<int?> querySessionIdOption = new Option<int?>(
            "--session",
            "Session ID (optional, queries entire project if not provided)"
        );
        Option<string?> queryOutputOption = new Option<string?>(
            "--output",
            "Output file path (optional, prints to console if not specified)"
        );
        Option<bool> queryVerboseOption = new Option<bool>("--verbose", "Enable verbose logging");

        queryCommand.AddOption(queryProjectNameOption);
        queryCommand.AddOption(queryTextOption);
        queryCommand.AddOption(querySessionIdOption);
        queryCommand.AddOption(queryOutputOption);
        queryCommand.AddOption(queryVerboseOption);
        queryCommand.SetHandler(
            async (
                string projectName,
                string query,
                int? sessionId,
                string? output,
                bool verbose
            ) =>
            {
                await QueryProject(projectName, query, sessionId, output, verbose);
            },
            queryProjectNameOption,
            queryTextOption,
            querySessionIdOption,
            queryOutputOption,
            queryVerboseOption
        );

        projectCommand.AddCommand(createProjectCommand);
        projectCommand.AddCommand(listProjectsCommand);
        projectCommand.AddCommand(deleteProjectCommand);
        projectCommand.AddCommand(projectAnalyzeCommand);
        projectCommand.AddCommand(reportCommand);
        projectCommand.AddCommand(queryCommand);

        RootCommand rootCommand = new RootCommand(
            "AI-powered log analysis tool for microservice systems"
        )
        {
            analyzeCommand,
            setupCommand,
            projectCommand,
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
        // Validate API key first
        if (!await ValidateApiKeyAsync(verbose))
        {
            Environment.Exit(1);
            return;
        }
        // Setup DI container
        ServiceCollection services = new ServiceCollection();
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
        services.AddScoped<OpenAIService>(provider =>
        {
            ILogger<OpenAIService> logger = provider.GetRequiredService<ILogger<OpenAIService>>();
            ConfigurationService configService =
                provider.GetRequiredService<ConfigurationService>();
            return new OpenAIService(logger, configService);
        });
        services.AddScoped<EmbeddingService>(provider =>
        {
            ILogger<EmbeddingService> logger = provider.GetRequiredService<
                ILogger<EmbeddingService>
            >();
            OpenAIService openAIService = provider.GetRequiredService<OpenAIService>();
            return new EmbeddingService(logger, openAIService);
        });
        services.AddScoped<AnalysisService>();
        services.AddScoped<ReportService>();

        ServiceProvider serviceProvider = services.BuildServiceProvider();
        ILogger<Program> logger = serviceProvider.GetRequiredService<ILogger<Program>>();

        logger.LogInformation("Starting log analysis for pattern: {Path}", path);

        // Get services
        FileIngestionService ingestionService =
            serviceProvider.GetRequiredService<FileIngestionService>();
        LogParsingService parsingService = serviceProvider.GetRequiredService<LogParsingService>();
        AnalysisService analysisService = serviceProvider.GetRequiredService<AnalysisService>();
        ReportService reportService = serviceProvider.GetRequiredService<ReportService>();

        // Process logs
        List<string> logFiles = await ingestionService.GetLogFilesAsync(path);
        logger.LogInformation("Found {Count} log files", logFiles.Count);

        List<LogEntry> logEntries = new List<LogEntry>();
        foreach (string file in logFiles)
        {
            logger.LogDebug("Processing file: {File}", file);
            List<LogEntry> entries = await parsingService.ParseFileAsync(file);
            logEntries.AddRange(entries);
        }

        logger.LogInformation("Parsed {Count} log entries", logEntries.Count);

        // Perform analysis
        var analysisResult = await analysisService.AnalyzeLogsAsync(logEntries);

        // Generate report
        string report = await reportService.GenerateReportAsync(analysisResult, logFiles);

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
        using ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
            builder.AddConsole().SetMinimumLevel(MsLogLevel.Information)
        );

        ILogger<ConfigurationService> logger = loggerFactory.CreateLogger<ConfigurationService>();
        ConfigurationService configService = new ConfigurationService(logger);

        // Check current configuration status
        bool hasApiKey = await configService.HasApiKeyAsync();

        Console.WriteLine("Configuration Status:");
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

            string? choice = Console.ReadLine()?.Trim();

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
        string apiKey = ReadPasswordFromConsole();
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
            string? confirm = Console.ReadLine()?.Trim().ToLowerInvariant();
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

        bool hasApiKey = await configService.HasApiKeyAsync();
        string configPath = configService.GetConfigFilePath();

        Console.WriteLine($"Configuration file: {configPath}");
        Console.WriteLine($"File exists: {File.Exists(configPath)}");
        Console.WriteLine($"API key configured: {(hasApiKey ? "Yes" : "No")}");

        if (hasApiKey)
        {
            string? apiKey = await configService.GetApiKeyAsync();
            if (!string.IsNullOrEmpty(apiKey))
            {
                // Show only first few and last few characters for security
                string maskedKey = $"{apiKey[..Math.Min(8, apiKey.Length)]}...{apiKey[^4..]}";
                Console.WriteLine($"API key (masked): {maskedKey}");
            }
        }

        // Show environment variable status
        string? envApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        Console.WriteLine(
            $"Environment variable OPENAI_API_KEY: {(!string.IsNullOrEmpty(envApiKey) ? "Set" : "Not set")}"
        );
    }

    private static async Task TestApiKey(ConfigurationService configService)
    {
        Console.WriteLine("=== Test API Key ===");
        Console.WriteLine();
        using ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
            builder.AddConsole().SetMinimumLevel(MsLogLevel.Warning)
        );

        ILogger<EmbeddingService> logger = loggerFactory.CreateLogger<EmbeddingService>();
        ILogger<OpenAIService> openAILogger = loggerFactory.CreateLogger<OpenAIService>();
        OpenAIService openAIService = new OpenAIService(openAILogger, configService);

        EmbeddingService embeddingService = new EmbeddingService(logger, openAIService);

        try
        {
            bool hasKey = await embeddingService.HasApiKeyConfiguredAsync();
            if (!hasKey)
            {
                Console.WriteLine("✗ No API key configured.");
                return;
            }

            Console.WriteLine("Testing API key by generating a simple embedding...");
            // Create a test log entry
            List<LogEntry> testEntries = new List<LogEntry>
            {
                new LogEntry
                {
                    Id = Guid.NewGuid().ToString(),
                    Timestamp = DateTime.UtcNow,
                    Level = Models.LogLevel.Info,
                    Message = "Test log entry for API validation",
                    Service = "test-service",
                },
            };

            await embeddingService.GenerateEmbeddingsAsync(testEntries, false);

            if (testEntries[0].Embedding != null)
            {
                Console.WriteLine("✓ API key is working correctly!");
                Console.WriteLine(
                    $"Generated embedding with {testEntries[0].Embedding.Length} dimensions"
                );
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

        bool hasApiKey = await configService.HasApiKeyAsync();
        if (!hasApiKey)
        {
            Console.WriteLine("No configuration found to remove.");
            return;
        }

        Console.Write("Are you sure you want to remove the saved configuration? (y/N): ");
        string? confirm = Console.ReadLine()?.Trim().ToLowerInvariant();

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
        StringBuilder password = new StringBuilder();
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

    #region Project Management Methods

    private static async Task CreateProject(string name, string? description)
    {
        try
        {
            using var services = CreateServiceProvider(false);
            var dbService = services.GetRequiredService<DatabaseService>();

            await dbService.InitializeAsync();
            var project = await dbService.CreateProjectAsync(name, description);

            Console.WriteLine($"✓ Created project '{project.Name}' with ID {project.Id}");
            if (!string.IsNullOrEmpty(description))
            {
                Console.WriteLine($"  Description: {description}");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"✗ Failed to create project: {ex.Message}");
            Environment.Exit(1);
        }
    }

    private static async Task ListProjects()
    {
        try
        {
            using var services = CreateServiceProvider(false);
            var dbService = services.GetRequiredService<DatabaseService>();

            await dbService.InitializeAsync();
            var projects = await dbService.ListProjectsAsync();

            if (!projects.Any())
            {
                Console.WriteLine(
                    "No projects found. Create a project with: analyze-logs project create --name <name>"
                );
                return;
            }

            Console.WriteLine("Projects:");
            Console.WriteLine();

            foreach (var project in projects)
            {
                Console.WriteLine($"📁 {project.Name} (ID: {project.Id})");
                if (!string.IsNullOrEmpty(project.Description))
                {
                    Console.WriteLine($"   Description: {project.Description}");
                }
                Console.WriteLine($"   Created: {project.CreatedAt:yyyy-MM-dd HH:mm:ss} UTC");
                if (project.LastAnalyzedAt.HasValue)
                {
                    Console.WriteLine(
                        $"   Last Analyzed: {project.LastAnalyzedAt.Value:yyyy-MM-dd HH:mm:ss} UTC"
                    );
                }
                Console.WriteLine($"   Sessions: {project.AnalysisSessions.Count}");
                Console.WriteLine();
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"✗ Failed to list projects: {ex.Message}");
            Environment.Exit(1);
        }
    }

    private static async Task DeleteProject(string name)
    {
        try
        {
            using var services = CreateServiceProvider(false);
            var dbService = services.GetRequiredService<DatabaseService>();

            await dbService.InitializeAsync();
            var project = await dbService.GetProjectByNameAsync(name);

            if (project == null)
            {
                Console.WriteLine($"✗ Project '{name}' not found");
                Environment.Exit(1);
                return;
            }

            Console.Write(
                $"Are you sure you want to delete project '{name}' and all its data? (y/N): "
            );
            string? confirm = Console.ReadLine()?.Trim().ToLowerInvariant();

            if (confirm != "y" && confirm != "yes")
            {
                Console.WriteLine("Project deletion cancelled.");
                return;
            }

            await dbService.DeleteProjectAsync(project.Id);
            Console.WriteLine($"✓ Deleted project '{name}'");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"✗ Failed to delete project: {ex.Message}");
            Environment.Exit(1);
        }
    }

    private static async Task AnalyzeLogsWithProject(
        string projectName,
        string? sessionName,
        string path,
        string? output,
        bool verbose,
        string model,
        bool enableCoherence,
        bool enableAnomaly,
        bool enableTagging,
        bool enableEmbeddings,
        bool generateReport
    )
    {
        // Validate API key first
        if (!await ValidateApiKeyAsync(verbose))
        {
            Environment.Exit(1);
            return;
        }

        try
        {
            using var services = CreateServiceProvider(verbose);
            var dbService = services.GetRequiredService<DatabaseService>();
            var analysisService = services.GetRequiredService<AnalysisService>();
            var reportService = services.GetRequiredService<MarkdownReportService>();

            await dbService.InitializeAsync();

            // Get or create project
            var project = await dbService.GetProjectByNameAsync(projectName);
            if (project == null)
            {
                Console.WriteLine($"Project '{projectName}' not found. Creating new project...");
                project = await dbService.CreateProjectAsync(projectName);
            }

            // Generate session name if not provided
            if (string.IsNullOrEmpty(sessionName))
            {
                sessionName = $"Session-{DateTime.UtcNow:yyyyMMdd-HHmmss}";
            }

            // Create analysis session
            var session = await dbService.CreateSessionAsync(project.Id, sessionName);
            Console.WriteLine($"Created analysis session: {sessionName} (ID: {session.Id})");

            // Perform analysis
            var config = new Configuration
            {
                EnableCoherenceAnalysis = enableCoherence,
                EnableAnomalyDetection = enableAnomaly,
                EnableTagging = enableTagging,
                EnableEmbeddings = enableEmbeddings,
                Model = model,
            };

            Console.WriteLine($"Analyzing logs in project '{projectName}'...");

            // Process log files
            var ingestionService = services.GetRequiredService<FileIngestionService>();
            var parsingService = services.GetRequiredService<LogParsingService>();

            var logFiles = await ingestionService.GetLogFilesAsync(path);
            Console.WriteLine($"Found {logFiles.Count} log files");

            var logEntries = new List<LogEntry>();
            foreach (var file in logFiles)
            {
                var entries = await parsingService.ParseFileAsync(file);
                logEntries.AddRange(entries);
            }

            Console.WriteLine($"Parsed {logEntries.Count} log entries");
            var result = await analysisService.AnalyzeLogsAsync(logEntries);

            // Add processed files to result
            result.ProcessedFiles = logFiles;

            // Store results in database
            if (result.LogEntries?.Any() == true)
            {
                await dbService.StoreLogEntriesAsync(project.Id, result.LogEntries, session.Id);
            }

            if (result.Anomalies?.Any() == true)
            {
                await dbService.StoreAnomaliesAsync(project.Id, result.Anomalies, session.Id);
            }

            if (result.Correlations?.Any() == true)
            {
                await dbService.StoreCorrelationsAsync(project.Id, result.Correlations, session.Id);
            }

            // Complete session
            var sourceFiles = result.ProcessedFiles?.ToList() ?? new List<string>();
            await dbService.CompleteSessionAsync(
                session.Id,
                result.LogEntries?.Count ?? 0,
                result.Anomalies?.Count ?? 0,
                result.Correlations?.Count ?? 0,
                sourceFiles
            );
            Console.WriteLine($"✓ Analysis completed and stored in database");

            // Generate and save report if requested
            if (generateReport)
            {
                Console.WriteLine("Generating comprehensive analysis report...");
                var reportData = await reportService.GenerateMarkdownReportAsync(
                    result,
                    logFiles,
                    project,
                    session
                );

                // Create docs directory and write report
                var docsPath = Path.Combine(Directory.GetCurrentDirectory(), "docs");
                Directory.CreateDirectory(docsPath);

                var reportFileName =
                    $"{project.Name}-analysis-{session.Id}-{DateTime.Now:yyyyMMdd-HHmmss}.md";
                var reportPath = Path.Combine(docsPath, reportFileName);

                if (!string.IsNullOrEmpty(output))
                {
                    await File.WriteAllTextAsync(output, reportData);
                    Console.WriteLine($"✓ Report saved to: {output}");
                }
                else
                {
                    await File.WriteAllTextAsync(reportPath, reportData);
                    Console.WriteLine($"✓ Report saved to: {reportPath}");
                }

                // Generate DocFX configuration and serve the report
                await GenerateDocFXSiteAsync(docsPath, reportPath, project.Name);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"✗ Analysis failed: {ex.Message}");
            Environment.Exit(1);
        }
    }

    private static async Task GenerateReport(string projectName, int? sessionId, string? output)
    {
        try
        {
            using var services = CreateServiceProvider(false);
            var dbService = services.GetRequiredService<DatabaseService>();
            var reportService = services.GetRequiredService<MarkdownReportService>();

            await dbService.InitializeAsync();

            var project = await dbService.GetProjectByNameAsync(projectName);
            if (project == null)
            {
                Console.WriteLine($"✗ Project '{projectName}' not found");
                Environment.Exit(1);
                return;
            }

            string reportData;
            if (sessionId.HasValue)
            {
                var session = await dbService.GetSessionByIdAsync(sessionId.Value);
                if (session == null)
                {
                    Console.WriteLine($"✗ Session {sessionId.Value} not found");
                    Environment.Exit(1);
                    return;
                }
                reportData = await reportService.GenerateSessionReportAsync(session);
                Console.WriteLine($"Generated session report for session {sessionId.Value}");
            }
            else
            {
                var sessions = await dbService.GetSessionsByProjectIdAsync(project.Id);
                reportData = await reportService.GenerateProjectReportAsync(project, sessions);
                Console.WriteLine($"Generated project report for '{projectName}'");
            }

            if (!string.IsNullOrEmpty(output))
            {
                await File.WriteAllTextAsync(output, reportData);
                Console.WriteLine($"✓ Report saved to: {output}");
            }
            else
            {
                Console.WriteLine("\n" + new string('=', 80));
                Console.WriteLine("ANALYSIS REPORT");
                Console.WriteLine(new string('=', 80));
                Console.WriteLine(reportData);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"✗ Failed to generate report: {ex.Message}");
            Environment.Exit(1);
        }
    }

    private static async Task QueryProject(
        string projectName,
        string query,
        int? sessionId,
        string? output,
        bool verbose
    )
    {
        // Validate API key first
        if (!await ValidateApiKeyAsync(verbose))
        {
            Environment.Exit(1);
            return;
        }

        try
        {
            using var services = CreateServiceProvider(verbose);
            var dbService = services.GetRequiredService<DatabaseService>();
            var queryService = services.GetRequiredService<QueryService>();

            await dbService.InitializeAsync();

            var project = await dbService.GetProjectByNameAsync(projectName);
            if (project == null)
            {
                Console.WriteLine($"✗ Project '{projectName}' not found");
                Environment.Exit(1);
                return;
            }

            Console.WriteLine($"🔍 Executing query on project '{projectName}': {query}");

            // Process the natural language query
            var result = await queryService.ProcessQueryAsync(projectName, query, sessionId);

            // Format the response
            var response = new StringBuilder();
            response.AppendLine($"# Query Results");
            response.AppendLine();
            response.AppendLine($"**Query:** {result.Query}");
            response.AppendLine($"**Project:** {result.ProjectName}");
            if (result.SessionId.HasValue)
            {
                response.AppendLine($"**Session:** {result.SessionId}");
            }
            response.AppendLine($"**Intent:** {result.Intent}");
            response.AppendLine($"**Data Type:** {result.DataType}");
            response.AppendLine($"**Executed:** {result.ExecutedAt:yyyy-MM-dd HH:mm:ss} UTC");
            response.AppendLine();
            response.AppendLine("## Response");
            response.AppendLine();
            response.AppendLine(result.Response);

            // Add data summary if available
            if (result.Data.LogEntries?.Count > 0)
            {
                response.AppendLine();
                response.AppendLine($"**Log Entries Found:** {result.Data.LogEntries.Count}");
            }
            if (result.Data.Anomalies?.Count > 0)
            {
                response.AppendLine($"**Anomalies Found:** {result.Data.Anomalies.Count}");
            }
            if (result.Data.ServiceMetrics?.Count > 0)
            {
                response.AppendLine($"**Services Analyzed:** {result.Data.ServiceMetrics.Count}");
            }
            if (result.Data.Correlations?.Count > 0)
            {
                response.AppendLine($"**Correlations Found:** {result.Data.Correlations.Count}");
            }

            var responseText = response.ToString();

            if (!string.IsNullOrEmpty(output))
            {
                await File.WriteAllTextAsync(output, responseText);
                Console.WriteLine($"✓ Query results saved to: {output}");
            }
            else
            {
                Console.WriteLine("\n" + new string('=', 80));
                Console.WriteLine("QUERY RESULTS");
                Console.WriteLine(new string('=', 80));
                Console.WriteLine(responseText);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"✗ Failed to execute query: {ex.Message}");
            if (verbose)
            {
                Console.Error.WriteLine($"Stack trace: {ex.StackTrace}");
            }
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// Generates a DocFX site configuration and serves the report in the browser
    /// </summary>
    private static async Task GenerateDocFXSiteAsync(
        string docsPath,
        string reportPath,
        string projectName
    )
    {
        try
        {
            Console.WriteLine("Setting up DocFX documentation site...");

            // Create docfx.json configuration
            var docfxConfig = new
            {
                metadata = new[]
                {
                    new
                    {
                        src = new[]
                        {
                            new { files = new[] { "*.md" }, exclude = new[] { "_site/**" } },
                        },
                        dest = "api",
                    },
                },
                build = new
                {
                    content = new[] { new { files = new[] { "*.md" } } },
                    resource = new[] { new { files = new[] { "images/**" } } },
                    dest = "_site",
                    template = new[] { "default" },
                    globalMetadata = new
                    {
                        _appTitle = $"Log Analysis Report - {projectName}",
                        _appName = "AnalyzeLogs Documentation",
                        _enableSearch = true,
                    },
                },
                serve = new { port = 8080 },
            };

            var configPath = Path.Combine(docsPath, "docfx.json");
            var configJson = JsonSerializer.Serialize(
                docfxConfig,
                new JsonSerializerOptions { WriteIndented = true }
            );
            await File.WriteAllTextAsync(configPath, configJson);

            // Create index.md that references the report
            var indexContent =
                $@"# Log Analysis Documentation

Welcome to the log analysis documentation for **{projectName}**.

## Latest Analysis Report

[View Latest Analysis Report](./{Path.GetFileName(reportPath)})

## About AnalyzeLogs

AnalyzeLogs is an AI-powered log analysis tool for microservice systems that provides:

- Anomaly detection using machine learning
- Cross-service correlation analysis
- Natural language querying
- Rich markdown reporting with visualizations

Generated on: {DateTime.Now:yyyy-MM-dd HH:mm:ss} UTC
";

            var indexPath = Path.Combine(docsPath, "index.md");
            await File.WriteAllTextAsync(indexPath, indexContent);

            // Create toc.yml for navigation
            var tocContent =
                $@"- name: Home
  href: index.md
- name: Analysis Report
  href: {Path.GetFileName(reportPath)}
";

            var tocPath = Path.Combine(docsPath, "toc.yml");
            await File.WriteAllTextAsync(tocPath, tocContent);

            Console.WriteLine($"✓ DocFX configuration created in: {docsPath}");

            // Check if docfx is installed, install if needed
            await EnsureDocFXInstalledAsync();

            // Build and serve the site
            Console.WriteLine("Building and serving documentation site...");
            var docfxPath = await GetDocFXPathAsync();

            if (docfxPath != null)
            {
                // Build the site
                var buildResult = await RunProcessAsync(
                    docfxPath,
                    $"build \"{configPath}\"",
                    docsPath
                );
                if (buildResult.Success)
                {
                    Console.WriteLine("✓ Documentation site built successfully");

                    // Serve the site
                    Console.WriteLine($"🌐 Starting web server at http://localhost:8080");
                    Console.WriteLine("📖 Opening documentation in your default browser...");
                    Console.WriteLine("Press Ctrl+C to stop the server");

                    // Start the server process in background
                    var serveProcess = new System.Diagnostics.Process
                    {
                        StartInfo = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = docfxPath,
                            Arguments = $"serve \"{Path.Combine(docsPath, "_site")}\" --port 8080",
                            WorkingDirectory = docsPath,
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true,
                        },
                    };

                    serveProcess.Start();

                    // Give the server a moment to start
                    await Task.Delay(2000);

                    // Open browser
                    try
                    {
                        System.Diagnostics.Process.Start(
                            new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = "http://localhost:8080",
                                UseShellExecute = true,
                            }
                        );
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"⚠️ Could not automatically open browser: {ex.Message}");
                        Console.WriteLine("Please manually navigate to: http://localhost:8080");
                    }

                    // Keep the server running until user interrupts
                    Console.CancelKeyPress += (sender, e) =>
                    {
                        e.Cancel = true;
                        serveProcess.Kill();
                        Console.WriteLine("\n📖 Documentation server stopped.");
                        Environment.Exit(0);
                    };

                    // Wait for the process to finish (user interruption)
                    serveProcess.WaitForExit();
                }
                else
                {
                    Console.WriteLine(
                        $"⚠️ Failed to build documentation site: {buildResult.Error}"
                    );
                    Console.WriteLine($"Report is still available at: {reportPath}");
                }
            }
            else
            {
                Console.WriteLine("⚠️ DocFX is not available. Report saved to disk only.");
                Console.WriteLine($"📄 Report available at: {reportPath}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Failed to generate DocFX site: {ex.Message}");
            Console.WriteLine($"📄 Report is still available at: {reportPath}");
        }
    }

    /// <summary>
    /// Ensures DocFX is installed as a .NET tool
    /// </summary>
    private static async Task EnsureDocFXInstalledAsync()
    {
        try
        {
            // Check if docfx is already installed
            var checkResult = await RunProcessAsync(
                "dotnet",
                "tool list --global",
                Directory.GetCurrentDirectory()
            );

            if (!checkResult.Output.Contains("docfx"))
            {
                Console.WriteLine("📦 Installing DocFX as a global .NET tool...");
                var installResult = await RunProcessAsync(
                    "dotnet",
                    "tool install --global docfx",
                    Directory.GetCurrentDirectory()
                );

                if (installResult.Success)
                {
                    Console.WriteLine("✓ DocFX installed successfully");
                }
                else
                {
                    Console.WriteLine($"⚠️ Failed to install DocFX: {installResult.Error}");
                }
            }
            else
            {
                Console.WriteLine("✓ DocFX is already installed");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Error checking/installing DocFX: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets the path to the DocFX executable
    /// </summary>
    private static async Task<string?> GetDocFXPathAsync()
    {
        try
        {
            // Try to find docfx in the global tools
            var result = await RunProcessAsync("where", "docfx", Directory.GetCurrentDirectory());
            if (result.Success && !string.IsNullOrEmpty(result.Output))
            {
                return result.Output.Trim().Split('\n')[0].Trim();
            }

            // Fallback: try direct dotnet tool invocation
            var toolResult = await RunProcessAsync(
                "dotnet",
                "tool run docfx --version",
                Directory.GetCurrentDirectory()
            );
            if (toolResult.Success)
            {
                return "dotnet tool run docfx";
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Runs a process and captures output
    /// </summary>
    private static async Task<(bool Success, string Output, string Error)> RunProcessAsync(
        string fileName,
        string arguments,
        string workingDirectory
    )
    {
        try
        {
            using var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    WorkingDirectory = workingDirectory,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                },
            };

            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            process.OutputDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                    outputBuilder.AppendLine(e.Data);
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                    errorBuilder.AppendLine(e.Data);
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync();

            return (process.ExitCode == 0, outputBuilder.ToString(), errorBuilder.ToString());
        }
        catch (Exception ex)
        {
            return (false, "", ex.Message);
        }
    }

    private static ServiceProvider CreateServiceProvider(bool verbose)
    {
        var services = new ServiceCollection();

        // Configure logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            if (verbose)
            {
                builder.SetMinimumLevel(MsLogLevel.Debug);
            }
            else
            {
                builder.SetMinimumLevel(MsLogLevel.Information);
            }
        }); // Add database context
        services.AddDbContext<LogAnalysisDbContext>();

        // Register Configuration with default values
        services.AddSingleton<Configuration>(provider => new Configuration
        {
            Model = "gpt-4o-mini",
            EnableCoherenceAnalysis = true,
            EnableAnomalyDetection = true,
            EnableTagging = true,
            EnableEmbeddings = true,
            Verbose = verbose,
        });

        // Register services
        services.AddSingleton<ConfigurationService>();
        services.AddTransient<DatabaseService>();
        services.AddTransient<AnalysisService>();
        services.AddTransient<MarkdownReportService>();
        services.AddTransient<OpenAIService>();
        services.AddTransient<EmbeddingService>();
        services.AddTransient<LogParsingService>();
        services.AddTransient<FileIngestionService>();
        services.AddTransient<ReportService>();
        services.AddTransient<QueryService>();

        return services.BuildServiceProvider();
    }

    #endregion
}
