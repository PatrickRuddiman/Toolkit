using System;
using System.Linq;
using AnalyzeLogs.Data;
using AnalyzeLogs.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AnalyzeLogs.Tests.Helpers
{
    /// <summary>
    /// Factory for creating in-memory database contexts for testing.
    /// </summary>
    public static class InMemoryDbContextFactory
    {
        /// <summary>
        /// Creates a new instance of <see cref="LogAnalyzerDbContext"/> with an in-memory database.
        /// </summary>
        /// <param name="databaseName">The name of the in-memory database. Each test should use a unique name.</param>
        /// <returns>A new instance of <see cref="LogAnalyzerDbContext"/>.</returns>
        public static LogAnalyzerDbContext CreateDbContext(string databaseName)
        {
            // Create a service collection and register the DbContext
            var serviceProvider = new ServiceCollection()
                .AddEntityFrameworkInMemoryDatabase()
                .BuildServiceProvider();

            // Create options for the in-memory database
            var options = new DbContextOptionsBuilder<LogAnalyzerDbContext>()
                .UseInMemoryDatabase(databaseName)
                .UseInternalServiceProvider(serviceProvider)
                .Options;

            // Create and return the DbContext
            var dbContext = new LogAnalyzerDbContext(options);

            // Ensure the database is created
            dbContext.Database.EnsureCreated();

            return dbContext;
        }

        /// <summary>
        /// Seeds the database with test data.
        /// </summary>
        /// <param name="dbContext">The database context to seed.</param>
        public static void SeedTestData(LogAnalyzerDbContext dbContext)
        {
            // Add severity levels if they don't exist
            if (!dbContext.SeverityLevels.Any())
            {
                dbContext.SeverityLevels.AddRange(
                    new SeverityLevel { LevelName = "TRACE" },
                    new SeverityLevel { LevelName = "DEBUG" },
                    new SeverityLevel { LevelName = "INFO" },
                    new SeverityLevel { LevelName = "WARN" },
                    new SeverityLevel { LevelName = "ERROR" },
                    new SeverityLevel { LevelName = "CRITICAL" },
                    new SeverityLevel { LevelName = "FATAL" }
                );

                dbContext.SaveChanges();
            }

            // Add services if they don't exist
            if (!dbContext.Services.Any())
            {
                dbContext.Services.AddRange(
                    new Service { ServiceName = "user-service" },
                    new Service { ServiceName = "payment-service" },
                    new Service { ServiceName = "inventory-service" },
                    new Service { ServiceName = "notification-service" },
                    new Service { ServiceName = "order-service" },
                    new Service { ServiceName = "auth-service" },
                    new Service { ServiceName = "api-gateway" },
                    new Service { ServiceName = "database-service" }
                );

                dbContext.SaveChanges();
            }

            // Add a test project if it doesn't exist
            if (!dbContext.Projects.Any())
            {
                var project = new Project
                {
                    ProjectId = Guid.NewGuid(),
                    Name = "Test Project",
                    Description = "Project for unit testing",
                    CreationDate = DateTime.UtcNow,
                    LastAccessedDate = DateTime.UtcNow,
                    DefaultLogPathPattern = "*.log",
                };

                dbContext.Projects.Add(project);
                dbContext.SaveChanges();

                // Add a test session
                var session = new Session
                {
                    SessionId = Guid.NewGuid(),
                    ProjectId = project.ProjectId,
                    StartTime = DateTime.UtcNow,
                    Status = "Completed",
                    LogFileCount = 0,
                    AnalyzedLogEntryCount = 0,
                    RawInputGlobPattern = "*.log",
                };

                dbContext.Sessions.Add(session);
                dbContext.SaveChanges();
            }
        }
    }
}
