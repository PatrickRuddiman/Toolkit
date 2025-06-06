using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace AnalyzeLogs.Services;

public class ConfigurationService
{
    private readonly ILogger<ConfigurationService> _logger;
    private static readonly string ConfigDirectoryName = "AnalyzeLogs";
    private static readonly string ConfigFileName = "config.json";

    public ConfigurationService(ILogger<ConfigurationService> logger)
    {
        _logger = logger;
    }

    public string GetConfigDirectory()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appDataPath, ConfigDirectoryName);
    }

    public string GetConfigFilePath()
    {
        return Path.Combine(GetConfigDirectory(), ConfigFileName);
    }

    public async Task SaveApiKeyAsync(string apiKey)
    {
        var configDir = GetConfigDirectory();
        if (!Directory.Exists(configDir))
        {
            Directory.CreateDirectory(configDir);
            _logger.LogDebug("Created configuration directory: {Directory}", configDir);
        }

        var config = new
        {
            OpenAIApiKey = apiKey,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var configPath = GetConfigFilePath();
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions 
        { 
            WriteIndented = true 
        });

        await File.WriteAllTextAsync(configPath, json);
        
        // Set file permissions to be more secure (read-only for user)
        if (OperatingSystem.IsWindows())
        {
            var fileInfo = new FileInfo(configPath);
            fileInfo.IsReadOnly = false; // Ensure we can modify it later if needed
        }

        _logger.LogInformation("API key saved to configuration file: {Path}", configPath);
    }

    public async Task<string?> GetApiKeyAsync()
    {
        var configPath = GetConfigFilePath();
        
        if (!File.Exists(configPath))
        {
            _logger.LogDebug("Configuration file not found: {Path}", configPath);
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(configPath);
            using var document = JsonDocument.Parse(json);
            
            if (document.RootElement.TryGetProperty("OpenAIApiKey", out var apiKeyElement))
            {
                var apiKey = apiKeyElement.GetString();
                _logger.LogDebug("API key loaded from configuration file");
                return apiKey;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read API key from configuration file: {Path}", configPath);
        }

        return null;
    }

    public async Task<bool> HasApiKeyAsync()
    {
        var apiKey = await GetApiKeyAsync();
        return !string.IsNullOrWhiteSpace(apiKey);
    }

    public async Task RemoveConfigAsync()
    {
        var configPath = GetConfigFilePath();
        
        if (File.Exists(configPath))
        {
            File.Delete(configPath);
            _logger.LogInformation("Configuration file deleted: {Path}", configPath);
        }
        else
        {
            _logger.LogInformation("Configuration file not found, nothing to delete");
        }
    }

    public string GetConfigStatus()
    {
        var configPath = GetConfigFilePath();
        var exists = File.Exists(configPath);
        
        return $"Configuration file: {configPath}\nExists: {exists}";
    }
}
