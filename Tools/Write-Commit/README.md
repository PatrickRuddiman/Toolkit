# WriteCommit

A cross-platform .NET tool that generates AI-powered commit messages using [fabric](https://github.com/danielmiessler/fabric).

This tool replicates the functionality of the PowerShell `Write-CommitMessage` function as a cross-platform .NET console application.

## Features

- 🤖 AI-powered commit message generation using fabric
- 🔄 Cross-platform support (Windows, macOS, Linux)
- 🎛️ Configurable fabric parameters (temperature, top-p, presence, frequency)
- 🧪 Dry-run mode for testing without committing
- 📝 Verbose output for debugging
- 🎯 Custom fabric patterns support

## Prerequisites

- [.NET 8.0 or later](https://dotnet.microsoft.com/download)
- [fabric](https://github.com/danielmiessler/fabric) installed and accessible in PATH
- Git repository with staged changes

## Installation

### Option 1: Install as a global .NET tool

```bash
# Build and install from source
git clone <repository-url>
cd WriteCommit
dotnet pack --configuration Release --output packages
dotnet tool install -g WriteCommit --add-source packages
```

### Option 2: Use installation scripts

**Windows (PowerShell):**
```powershell
.\install.ps1
```

**Linux/macOS (Bash):**
```bash
chmod +x install.sh
./install.sh
```

### Option 3: Run from source

```bash
git clone <repository-url>
cd WriteCommit
dotnet build
dotnet run -- --help
```

## Usage

### Basic usage

```bash
# Stage your changes first
git add .

# Generate and commit with AI message
write-commit
```

### Advanced usage

```bash
# Dry run (generate message without committing)
write-commit --dry-run

# Verbose output
write-commit --verbose

# Custom fabric parameters
write-commit --temperature 2 --topp 0.9 --pattern custom_pattern

# Combine options
write-commit --dry-run --verbose --temperature 0.5
```

## Command Line Options

| Option | Default | Description |
|--------|---------|-------------|
| `--dry-run` | `false` | Generate commit message without committing |
| `--verbose` | `false` | Show detailed output |
| `--pattern` | `write_commit_message` | Fabric pattern to use |
| `--temperature` | `1` | Temperature setting for fabric (0-2) |
| `--topp` | `1` | Top-p setting for fabric (0-1) |
| `--model` | `gpt-4o` | AI model to use |
| `--presence` | `0` | Presence penalty for fabric |
| `--frequency` | `0` | Frequency penalty for fabric |

## How it works

1. **Checks git repository**: Ensures you're in a git repository
2. **Gets staged changes**: Retrieves `git diff --staged` output
3. **Calls fabric**: Passes the diff to fabric with specified parameters
4. **Displays message**: Shows the generated commit message
5. **Commits changes**: Uses the generated message for `git commit` (unless `--dry-run`)

## Error Handling

The tool handles various error scenarios:

- Not in a git repository
- No staged changes
- Fabric not installed or accessible
- Git command failures
- Network issues with AI services

## PowerShell Function Equivalent

This .NET tool replicates the functionality of this PowerShell function:

```powershell
function Write-CommitMessage {
    $OutputEncoding = [System.Text.Encoding]::UTF8
    $CHANGES = (git --no-pager diff --staged) -replace '"','\"'
    $tempCommitMsgFile = [System.IO.Path]::GetTempFileName()
    try {
        $commitMessage = fabric -t 1 -T 1 -P 0 -F 0 -p write_commit_message "$CHANGES"
    } catch {
        Write-Error "Failed to generate commit message."
        return
    }
    $commitMessage | Out-File -FilePath $tempCommitMsgFile -Encoding utf8
    Write-Host "Generated commit message:"
    Write-Host (Get-Content $tempCommitMsgFile -Raw)
    git commit -F $tempCommitMsgFile
    Remove-Item $tempCommitMsgFile
}
```

### Key Improvements

- **Cross-platform**: Works on Windows, macOS, and Linux
- **Better error handling**: More robust error detection and reporting  
- **Command-line interface**: Proper CLI with help, options, and validation
- **Package distribution**: Install as a global .NET tool
- **Configurable parameters**: All fabric settings can be customized
- **Dry-run mode**: Test message generation without committing
- **Verbose output**: Detailed logging for debugging

## Comparison with PowerShell Version

This .NET version provides several advantages over the original PowerShell function:

- **Cross-platform**: Works on Windows, macOS, and Linux
- **Better error handling**: More robust error detection and reporting
- **Command-line interface**: Proper CLI with help and options
- **Package distribution**: Can be installed as a global .NET tool
- **Performance**: Generally faster startup and execution

## CI/CD and Releases

This project includes GitHub Actions workflows for automated builds and releases:

### Continuous Integration (`ci-cd.yml`)
- **Triggers**: Push to `main`/`develop` branches, pull requests to `main`
- **Actions**: 
  - Runs tests across multiple platforms (Ubuntu, Windows, macOS)
  - Creates build artifacts
  - Packages NuGet tool on `main` branch updates

### Automated Releases (`release.yml`)
- **Triggers**: Push to `main` branch (excluding documentation changes)
- **Actions**:
  - Uses GitVersion for semantic versioning
  - Creates cross-platform binaries (Linux x64, Windows x64, macOS x64/ARM64)
  - Generates changelog from commit history
  - Creates GitHub releases with downloadable assets
  - Publishes NuGet package

### Version Management
- Uses GitVersion for automatic semantic versioning
- Follows conventional commit patterns
- Generates releases only when `main` branch is updated with actual code changes

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests if applicable
5. Submit a pull request

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Acknowledgments

- [fabric](https://github.com/danielmiessler/fabric) for the AI-powered commit message generation
- [System.CommandLine](https://github.com/dotnet/command-line-api) for the CLI framework
