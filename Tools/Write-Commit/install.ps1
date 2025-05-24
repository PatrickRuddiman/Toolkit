# Installation script for WriteCommit tool

Write-Host "Building WriteCommit..." -ForegroundColor Cyan
dotnet build WriteCommit.csproj --configuration Release

if ($LASTEXITCODE -eq 0) {
    Write-Host "Build successful!" -ForegroundColor Green
    
    Write-Host "Packing as NuGet tool..." -ForegroundColor Cyan
    if (!(Test-Path "packages")) {
        New-Item -ItemType Directory -Name "packages" -Force | Out-Null
    }
    dotnet pack WriteCommit.csproj --configuration Release --output ./packages
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Installing as global tool..." -ForegroundColor Cyan
        dotnet tool uninstall -g WriteCommit 2>$null
        dotnet tool install -g WriteCommit --add-source ./packages
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✅ WriteCommit installed successfully!" -ForegroundColor Green
            Write-Host "You can now use 'write-commit' command from anywhere." -ForegroundColor Yellow
            Write-Host ""
            Write-Host "Example usage:" -ForegroundColor White
            Write-Host "  git add ." -ForegroundColor Gray
            Write-Host "  write-commit" -ForegroundColor Gray
            Write-Host "  write-commit --dry-run" -ForegroundColor Gray
            Write-Host "  write-commit --verbose" -ForegroundColor Gray
        }
        else {
            Write-Host "❌ Failed to install as global tool" -ForegroundColor Red
        }
    }
    else {
        Write-Host "❌ Failed to pack" -ForegroundColor Red
    }
}
else {
    Write-Host "❌ Build failed" -ForegroundColor Red
}
