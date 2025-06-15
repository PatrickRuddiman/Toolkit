using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using AnalyzeLogs.Data;
using AnalyzeLogs.Models;
using AnalyzeLogs.Services.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AnalyzeLogs;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        // Set up services
        var serviceProvider = ConfigureServices();

        // Ensure database and configuration exist
        await EnsureDatabaseCreatedAsync(serviceProvider);

        // Create command line parser
        var parser = BuildCommandLineParser(serviceProvider);

        // Parse command line arguments and execute
        return await parser.InvokeAsync(args);
    }

    private static ServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // Configure logging
        services.AddLogging(configure =>
        {
            configure.AddConsole();
            configure.AddDebug();
            configure.SetMinimumLevel(LogLevel.Information);
        });

        // Add configuration service
        services.AddSingleton<IConfigurationService, ConfigurationService>();

        // Add database context
        services.AddDbContext<LogAnalyzerDbContext>(
            (provider, options) =>
            {
                var configService = provider.GetRequiredService<IConfigurationService>();
                var config = configService.GetConfiguration();
                options.UseSqlite($"Data Source={config.DatabasePath}");
            },
            ServiceLifetime.Transient
        );

        // Note: Additional services will be registered once their interfaces are defined

        return services.BuildServiceProvider();
    }

    private static async Task EnsureDatabaseCreatedAsync(ServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();

        // Ensure configuration exists
        var configService = scope.ServiceProvider.GetRequiredService<IConfigurationService>();
        var config = configService.GetConfiguration();

        // Create database directory if it doesn't exist
        string? dbDirectory = Path.GetDirectoryName(config.DatabasePath);
        if (dbDirectory != null && !Directory.Exists(dbDirectory))
        {
            Directory.CreateDirectory(dbDirectory);
        }

        // Ensure database is created
        var dbContext = scope.ServiceProvider.GetRequiredService<LogAnalyzerDbContext>();
        await dbContext.Database.EnsureCreatedAsync();

        // Create report directory if it doesn't exist
        if (!Directory.Exists(config.DefaultReportOutputPath))
        {
            Directory.CreateDirectory(config.DefaultReportOutputPath);
        }
    }

    private static Parser BuildCommandLineParser(ServiceProvider serviceProvider)
    {
        // Create root command
        var rootCommand = new RootCommand("Log Analyzer - AI-powered log analysis tool");

        // Add subcommands for different areas
        rootCommand.AddCommand(BuildProjectCommand(serviceProvider));
        rootCommand.AddCommand(BuildSessionCommand(serviceProvider));
        rootCommand.AddCommand(BuildQueryCommand(serviceProvider));
        rootCommand.AddCommand(BuildAnalyzeCommand(serviceProvider));
        rootCommand.AddCommand(BuildReportCommand(serviceProvider));

        // Build and return the parser
        return new CommandLineBuilder(rootCommand)
            .UseDefaults()
            .UseExceptionHandler(
                (ex, context) =>
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Error.WriteLine($"Error: {ex.Message}");
                    if (ex.InnerException != null)
                    {
                        Console.Error.WriteLine($"Details: {ex.InnerException.Message}");
                    }
                    Console.ResetColor();

                    context.ExitCode = 1;
                }
            )
            .Build();
    }

    private static Command BuildProjectCommand(ServiceProvider serviceProvider)
    {
        var command = new Command("project", "Manage projects");

        // Create project command
        var createCommand = new Command("create", "Create a new project");
        var nameOption = new Option<string>("--name", "The name of the project")
        {
            IsRequired = true,
        };
        var descriptionOption = new Option<string?>(
            "--description",
            "The description of the project"
        );
        var logPatternOption = new Option<string?>(
            "--log-pattern",
            "The default log pattern for the project"
        );

        createCommand.AddOption(nameOption);
        createCommand.AddOption(descriptionOption);
        createCommand.AddOption(logPatternOption);

        createCommand.SetHandler(
            async (name, description, logPattern) =>
            {
                using var scope = serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<LogAnalyzerDbContext>();

                var project = new Project
                {
                    ProjectId = Guid.NewGuid(),
                    Name = name,
                    Description = description,
                    DefaultLogPathPattern = logPattern,
                    CreationDate = DateTime.UtcNow,
                    LastAccessedDate = DateTime.UtcNow,
                };

                dbContext.Projects.Add(project);
                await dbContext.SaveChangesAsync();

                Console.WriteLine($"Project '{name}' created with ID: {project.ProjectId}");
            },
            nameOption,
            descriptionOption,
            logPatternOption
        );

        // List projects command (simplified)
        var listCommand = new Command("list", "List all projects");

        listCommand.SetHandler(async () =>
        {
            using var scope = serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<LogAnalyzerDbContext>();

            var projects = await dbContext
                .Projects.OrderByDescending(p => p.LastAccessedDate)
                .ToListAsync();

            if (projects.Count == 0)
            {
                Console.WriteLine(
                    "No projects found. Create one with 'loganalyzer project create'."
                );
                return;
            }

            Console.WriteLine("\nProjects:");
            Console.WriteLine(new string('-', 80));
            Console.WriteLine($"{"ID", -36} | {"Name", -20} | {"Description", -20}");
            Console.WriteLine(new string('-', 80));

            foreach (var project in projects)
            {
                Console.WriteLine(
                    $"{project.ProjectId, -36} | {project.Name, -20} | {project.Description ?? "", -20}"
                );
            }

            Console.WriteLine(new string('-', 80));
        });

        // Add subcommands to the project command
        command.AddCommand(createCommand);
        command.AddCommand(listCommand);

        return command;
    }

    private static Command BuildSessionCommand(ServiceProvider serviceProvider)
    {
        var command = new Command("session", "Manage analysis sessions");

        // Start session command
        var startCommand = new Command("start", "Start a new analysis session");
        var projectIdOption = new Option<string>(
            "--project",
            "The ID of the project to create the session in"
        )
        {
            IsRequired = true,
        };
        var globOption = new Option<string?>("--glob", "The glob pattern for log files");
        var nameOption = new Option<string?>("--name", "Optional name for the session");
        var noEmbeddingsOption = new Option<bool>(
            "--no-embeddings",
            "Disable embedding generation"
        );

        startCommand.AddOption(projectIdOption);
        startCommand.AddOption(globOption);
        startCommand.AddOption(nameOption);
        startCommand.AddOption(noEmbeddingsOption);

        startCommand.SetHandler(
            async (projectId, glob, name, noEmbeddings) =>
            {
                if (!Guid.TryParse(projectId, out Guid id))
                {
                    Console.WriteLine($"Invalid project ID format: {projectId}");
                    return;
                }

                using var scope = serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<LogAnalyzerDbContext>();

                // Find the project
                var project = await dbContext.Projects.FindAsync(id);
                if (project == null)
                {
                    Console.WriteLine($"Project with ID '{projectId}' not found.");
                    return;
                }

                // Determine the glob pattern to use
                string globPattern = glob ?? project.DefaultLogPathPattern ?? "**/**.log";

                // Create the session
                var session = new Session
                {
                    SessionId = Guid.NewGuid(),
                    ProjectId = project.ProjectId,
                    StartTime = DateTime.UtcNow,
                    Status = "Initialized",
                    RawInputGlobPattern = globPattern,
                };

                // Set name if provided
                if (!string.IsNullOrEmpty(name))
                {
                    session.ReportFilePath = name;
                }

                // Save the session
                dbContext.Sessions.Add(session);
                await dbContext.SaveChangesAsync();

                Console.WriteLine($"Session created with ID: {session.SessionId}");
                Console.WriteLine("Starting log ingestion...");

                // For now, just report that AI log ingestion will be implemented
                Console.WriteLine(
                    "AI log ingestion using the AILogParser will be implemented in future versions."
                );
                Console.WriteLine(
                    "For now, the session has been created but no logs will be processed."
                );
            },
            projectIdOption,
            globOption,
            nameOption,
            noEmbeddingsOption
        );

        // Add subcommands to the session command
        command.AddCommand(startCommand);

        return command;
    }

    private static Command BuildQueryCommand(ServiceProvider serviceProvider)
    {
        var command = new Command("query", "Query log data using natural language");
        var sessionIdOption = new Option<string>("--session", "The ID of the session to query")
        {
            IsRequired = true,
        };
        var queryOption = new Option<string?>("--text", "The query text");

        command.AddOption(sessionIdOption);
        command.AddOption(queryOption);

        command.SetHandler(
            async (sessionId, queryText) =>
            {
                if (!Guid.TryParse(sessionId, out Guid id))
                {
                    Console.WriteLine($"Invalid session ID format: {sessionId}");
                    return;
                }

                using var scope = serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<LogAnalyzerDbContext>();

                // Find the session
                var session = await dbContext
                    .Sessions.Include(s => s.Project)
                    .FirstOrDefaultAsync(s => s.SessionId == id);

                if (session == null)
                {
                    Console.WriteLine($"Session with ID '{sessionId}' not found.");
                    return;
                }

                Console.WriteLine("Natural language query feature is not yet implemented.");
                Console.WriteLine(
                    "This will allow you to query log data using natural language questions."
                );

                if (!string.IsNullOrEmpty(queryText))
                {
                    Console.WriteLine($"\nYou asked: {queryText}");
                    Console.WriteLine("Sorry, I can't process this query yet.");
                }
            },
            sessionIdOption,
            queryOption
        );

        return command;
    }

    private static Command BuildAnalyzeCommand(ServiceProvider serviceProvider)
    {
        var command = new Command("analyze", "Analyze log data");
        var sessionIdOption = new Option<string>("--session", "The ID of the session to analyze")
        {
            IsRequired = true,
        };
        var analysisTypeOption = new Option<string?>(
            "--type",
            "The type of analysis to perform (anomalies, coherence, summary)"
        );

        command.AddOption(sessionIdOption);
        command.AddOption(analysisTypeOption);

        command.SetHandler(
            async (sessionId, analysisType) =>
            {
                Console.WriteLine("AI analysis feature is not yet fully implemented.");
                Console.WriteLine(
                    "This will allow you to analyze log data using AI-powered techniques."
                );
            },
            sessionIdOption,
            analysisTypeOption
        );

        return command;
    }

    private static Command BuildReportCommand(ServiceProvider serviceProvider)
    {
        var command = new Command("report", "Generate reports");
        var sessionIdOption = new Option<string>(
            "--session",
            "The ID of the session to generate a report for"
        )
        {
            IsRequired = true,
        };
        var outputPathOption = new Option<string?>("--output", "The path to save the report to");
        var formatOption = new Option<string?>("--format", "The report format (docfx or markdown)");

        command.AddOption(sessionIdOption);
        command.AddOption(outputPathOption);
        command.AddOption(formatOption);

        command.SetHandler(
            async (sessionId, outputPath, format) =>
            {
                Console.WriteLine("Report generation feature is not yet implemented.");
                Console.WriteLine("This will allow you to generate rich DocFX-compatible reports.");
            },
            sessionIdOption,
            outputPathOption,
            formatOption
        );

        return command;
    }
}
