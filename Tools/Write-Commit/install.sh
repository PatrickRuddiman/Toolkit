#!/bin/bash
# Installation script for WriteCommit tool

echo "Building WriteCommit..."
dotnet build WriteCommit.csproj --configuration Release

if [ $? -eq 0 ]; then
    echo "Build successful!"
    
    echo "Packing as NuGet tool..."
    dotnet pack WriteCommit.csproj --configuration Release --output ./packages
    
    if [ $? -eq 0 ]; then
        echo "Installing as global tool..."
        dotnet tool uninstall -g WriteCommit 2>/dev/null
        dotnet tool install -g WriteCommit --add-source ./packages
        
        if [ $? -eq 0 ]; then
            echo "✅ WriteCommit installed successfully!"
            echo "You can now use 'write-commit' command from anywhere."
            echo ""
            echo "Example usage:"
            echo "  git add ."
            echo "  write-commit"
            echo "  write-commit --dry-run"
            echo "  write-commit --verbose"
        else
            echo "❌ Failed to install as global tool"
        fi
    else
        echo "❌ Failed to pack"
    fi
else
    echo "❌ Build failed"
fi
