# WriteCommit

A cross-platform .NET tool that generates AI-powered commit messages using [fabric](https://github.com/danielmiessler/fabric).

## ✨ Features

- 🤖 **AI-powered commit messages** - Generate meaningful commit messages from your staged changes
- 🔄 **Cross-platform** - Works on Windows, macOS, and Linux
- 🎛️ **Highly configurable** - Adjust AI parameters and patterns to your preference
- 🧪 **Dry-run mode** - Preview generated messages without committing
- 📝 **Verbose output** - Detailed logging for debugging and transparency
- ⚡ **Fast and lightweight** - Quick generation with minimal dependencies

## 🚀 Quick Start

### Prerequisites

- [.NET 8.0 or later](https://dotnet.microsoft.com/download)
- [fabric](https://github.com/danielmiessler/fabric) installed and configured
- Git repository with staged changes

### Installation

**Install as global .NET tool:**
```bash
git clone https://github.com/yourusername/WriteCommit.git
cd WriteCommit
dotnet pack --configuration Release --output packages
dotnet tool install -g WriteCommit --add-source packages
```

**Or use installation scripts:**
```bash
# Windows
.\install.ps1

# Linux/macOS  
chmod +x install.sh && ./install.sh
```

### Basic Usage

```bash
# Stage your changes
git add .

# Generate and commit with AI-powered message
write-commit
```

## 🎯 Advanced Usage

```bash
# Preview message without committing
write-commit --dry-run

# Detailed output for debugging
write-commit --verbose

# Custom AI parameters
write-commit --temperature 0.7 --topp 0.9 --pattern custom_pattern

# Force reinstall all patterns
write-commit --reinstall-patterns

# Combine multiple options
write-commit --dry-run --verbose --temperature 0.5 --reinstall-patterns
```

## ⚙️ Configuration Options

| Option | Default | Description |
|--------|---------|-------------|
| `--dry-run` | `false` | Generate message without committing |
| `--verbose` | `false` | Show detailed output |
| `--pattern` | `write_commit_message` | Fabric pattern to use |
| `--temperature` | `1` | AI creativity level (0-2) |
| `--topp` | `1` | Nucleus sampling parameter (0-1) |
| `--model` | `gpt-4o` | AI model to use |
| `--presence` | `0` | Presence penalty (-2 to 2) |
| `--frequency` | `0` | Frequency penalty (-2 to 2) |
| `--reinstall-patterns` | `false` | Force reinstallation of all patterns |

## 🔧 How It Works

1. **Installs patterns** - Automatically installs/updates fabric patterns on first run
2. **Validates environment** - Checks for git repository and fabric installation
3. **Analyzes changes** - Processes your staged git diff using semantic chunking
4. **Generates message** - Uses fabric AI to create meaningful commit message
5. **Commits changes** - Applies the generated message (unless `--dry-run`)

## 🛠️ Development

### Run from Source
```bash
git clone https://github.com/PatrickRuddiman/Toolkit
cd Tools/Write-Commit
dotnet build
dotnet run -- --help
```

### Contributing
1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## 📝 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

- [fabric](https://github.com/danielmiessler/fabric) - The AI-powered prompt and pattern framework
- [System.CommandLine](https://github.com/dotnet/command-line-api) - Modern CLI framework for .NET

---

**Made with ❤️ for developers who want better commit messages**
