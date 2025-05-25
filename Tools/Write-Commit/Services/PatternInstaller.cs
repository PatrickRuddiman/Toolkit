using System;
using System.IO;
using System.Threading.Tasks;

namespace WriteCommit.Services;

public class PatternInstaller
{
    private static readonly string FabricPatternsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".config",
        "fabric",
        "patterns"
    );

    public static async Task EnsurePatternInstalledAsync(string patternName, bool verbose = false)
    {
        var sourcePatternDir = GetSourcePatternDirectory(patternName);
        var targetPatternDir = Path.Combine(FabricPatternsDir, patternName);

        if (!Directory.Exists(sourcePatternDir))
        {
            throw new DirectoryNotFoundException(
                $"Source pattern directory not found: {sourcePatternDir}"
            );
        }

        // Check if pattern already exists and is up to date
        if (Directory.Exists(targetPatternDir))
        {
            if (await IsPatternUpToDateAsync(sourcePatternDir, targetPatternDir))
            {
                if (verbose)
                {
                    Console.WriteLine($"Pattern '{patternName}' is already up to date.");
                }
                return;
            }
            else if (verbose)
            {
                Console.WriteLine($"Pattern '{patternName}' exists but is outdated. Updating...");
            }
        }
        else
        {
            if (verbose)
            {
                Console.WriteLine($"Installing pattern '{patternName}'...");
            }
        }

        // Create fabric patterns directory if it doesn't exist
        Directory.CreateDirectory(FabricPatternsDir);

        // Copy the pattern
        await CopyPatternAsync(sourcePatternDir, targetPatternDir, verbose);

        if (verbose)
        {
            Console.WriteLine(
                $"Pattern '{patternName}' installed successfully to: {targetPatternDir}"
            );
        }
    }

    private static string GetSourcePatternDirectory(string patternName)
    {
        // Get the directory where the executable is located
        var executableDir = AppDomain.CurrentDomain.BaseDirectory;
        // Look for patterns directory relative to executable
        var patternsDir = Path.Combine(executableDir, "patterns", patternName);

        if (Directory.Exists(patternsDir))
        {
            return patternsDir;
        }

        // Fallback: look in the project directory (for development)
        var projectDir = Path.GetDirectoryName(
            System.Reflection.Assembly.GetExecutingAssembly().Location
        );

        while (projectDir != null && !File.Exists(Path.Combine(projectDir, "WriteCommit.csproj")))
        {
            projectDir = Path.GetDirectoryName(projectDir);
        }

        if (projectDir != null)
        {
            var devPatternsDir = Path.Combine(projectDir, "patterns", patternName);
            if (Directory.Exists(devPatternsDir))
            {
                return devPatternsDir;
            }
        }

        throw new DirectoryNotFoundException(
            $"Could not find pattern '{patternName}' in any expected location."
        );
    }

    private static async Task<bool> IsPatternUpToDateAsync(string sourceDir, string targetDir)
    {
        try
        {
            var sourceFiles = Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories);
            var targetFiles = Directory.GetFiles(targetDir, "*", SearchOption.AllDirectories);

            // Quick check: different number of files means not up to date
            if (sourceFiles.Length != targetFiles.Length)
            {
                return false;
            }

            // Check if all source files exist in target and have same content
            foreach (var sourceFile in sourceFiles)
            {
                var relativePath = Path.GetRelativePath(sourceDir, sourceFile);
                var targetFile = Path.Combine(targetDir, relativePath);

                if (!File.Exists(targetFile))
                {
                    return false;
                }

                // Compare file content
                var sourceContent = await File.ReadAllTextAsync(sourceFile);
                var targetContent = await File.ReadAllTextAsync(targetFile);

                if (sourceContent != targetContent)
                {
                    return false;
                }
            }

            return true;
        }
        catch
        {
            // If any error occurs during comparison, assume not up to date
            return false;
        }
    }

    private static async Task CopyPatternAsync(string sourceDir, string targetDir, bool verbose)
    {
        try
        {
            // Remove existing target directory if it exists
            if (Directory.Exists(targetDir))
            {
                Directory.Delete(targetDir, true);
            }

            // Create target directory
            Directory.CreateDirectory(targetDir);

            // Copy all files and subdirectories
            await CopyDirectoryRecursiveAsync(sourceDir, targetDir);

            if (verbose)
            {
                Console.WriteLine($"Copied pattern from {sourceDir} to {targetDir}");
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to copy pattern from {sourceDir} to {targetDir}: {ex.Message}",
                ex
            );
        }
    }

    private static async Task CopyDirectoryRecursiveAsync(string sourceDir, string targetDir)
    {
        // Create target directory
        Directory.CreateDirectory(targetDir);

        // Copy all files
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var targetFile = Path.Combine(targetDir, Path.GetFileName(file));
            File.Copy(file, targetFile, true);
        }

        // Copy all subdirectories
        foreach (var directory in Directory.GetDirectories(sourceDir))
        {
            var targetSubDir = Path.Combine(targetDir, Path.GetFileName(directory));
            await CopyDirectoryRecursiveAsync(directory, targetSubDir);
        }
    }

    public static string GetFabricPatternsDirectory()
    {
        return FabricPatternsDir;
    }

    public static bool IsFabricConfigured()
    {
        var fabricConfigDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".config",
            "fabric"
        );

        return Directory.Exists(fabricConfigDir);
    }
}
