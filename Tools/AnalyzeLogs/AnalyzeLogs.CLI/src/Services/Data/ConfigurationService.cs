using System.Text.Json;
using AnalyzeLogs.Models;
using Microsoft.Extensions.Logging;

namespace AnalyzeLogs.Services.Data;

/// <summary>
/// Service for managing application configuration.
/// </summary>
public interface IConfigurationService
{
    /// <summary>
    /// Gets the current application configuration.
    /// </summary>
    /// <returns>The application configuration.</returns>
    AppConfig GetConfiguration();

    /// <summary>
    /// Saves the application configuration.
    /// </summary>
    /// <param name="config">The configuration to save.</param>
    /// <returns>True if the configuration was saved successfully, false otherwise.</returns>
    Task<bool> SaveConfigurationAsync(AppConfig config);
}

/// <summary>
/// Implementation of the configuration service.
/// </summary>
public class ConfigurationService : IConfigurationService
{
    private readonly ILogger<ConfigurationService> _logger;
    private AppConfig? _cachedConfig;
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationService"/> class.
    /// </summary>
    /// <param name="logger">The logger to use.</param>
    public ConfigurationService(ILogger<ConfigurationService> logger)
    {
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System
                .Text
                .Json
                .Serialization
                .JsonIgnoreCondition
                .WhenWritingNull,
        };
    }

    /// <inheritdoc/>
    public AppConfig GetConfiguration()
    {
        if (_cachedConfig != null)
        {
            return _cachedConfig;
        }

        string configPath = AppConfig.ConfigFilePath;
        var config = new AppConfig();

        if (File.Exists(configPath))
        {
            try
            {
                string json = File.ReadAllText(configPath);
                var loadedConfig = JsonSerializer.Deserialize<AppConfig>(json, _jsonOptions);
                if (loadedConfig != null)
                {
                    config = loadedConfig;
                    _logger.LogInformation("Configuration loaded from {ConfigPath}", configPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading configuration from {ConfigPath}", configPath);
                // Continue with default configuration
            }
        }
        else
        {
            _logger.LogInformation(
                "Configuration file not found at {ConfigPath}, using defaults",
                configPath
            );
            // Ensure directory exists
            string? directory = Path.GetDirectoryName(configPath);
            if (directory != null && !Directory.Exists(directory))
            {
                try
                {
                    Directory.CreateDirectory(directory);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Error creating configuration directory {Directory}",
                        directory
                    );
                }
            }

            // Also ensure database directory exists
            string? dbDirectory = Path.GetDirectoryName(config.DatabasePath);
            if (dbDirectory != null && !Directory.Exists(dbDirectory))
            {
                try
                {
                    Directory.CreateDirectory(dbDirectory);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Error creating database directory {Directory}",
                        dbDirectory
                    );
                }
            }

            // Also ensure report directory exists
            if (!Directory.Exists(config.DefaultReportOutputPath))
            {
                try
                {
                    Directory.CreateDirectory(config.DefaultReportOutputPath);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Error creating report directory {Directory}",
                        config.DefaultReportOutputPath
                    );
                }
            }

            // Save the default configuration
            try
            {
                string json = JsonSerializer.Serialize(config, _jsonOptions);
                File.WriteAllText(configPath, json);
                _logger.LogInformation("Default configuration saved to {ConfigPath}", configPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error saving default configuration to {ConfigPath}",
                    configPath
                );
            }
        }

        // Check for environment variable overrides
        string? apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            config.OpenAI.ApiKey = apiKey;
            _logger.LogInformation("Using OpenAI API key from environment variable");
        }

        _cachedConfig = config;
        return config;
    }

    /// <inheritdoc/>
    public async Task<bool> SaveConfigurationAsync(AppConfig config)
    {
        try
        {
            string configPath = AppConfig.ConfigFilePath;
            string? directory = Path.GetDirectoryName(configPath);
            if (directory != null && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string json = JsonSerializer.Serialize(config, _jsonOptions);
            await File.WriteAllTextAsync(configPath, json);
            _cachedConfig = config;
            _logger.LogInformation("Configuration saved to {ConfigPath}", configPath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving configuration");
            return false;
        }
    }
}
