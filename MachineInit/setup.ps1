# NewMachineSetup.ps1
# Script to set up a brand new Windows machine with required tools and packages
# This script installs Scoop, Chocolatey, and all required packages

# Show execution status
$ProgressPreference    = 'Continue'
$ErrorActionPreference = 'Stop'

function Write-ColorOutput {
    param(
        [Parameter(Mandatory=$true)]
        [string]$Message,
        
        [Parameter(Mandatory=$false)]
        [string]$ForegroundColor = "White"
    )
    Write-Host $Message -ForegroundColor $ForegroundColor
}

function Test-CommandExists {
    param($Command)
    try {
        return [bool](Get-Command $Command -ErrorAction Stop)
    }
    catch {
        return $false
    }
}

Write-ColorOutput "Starting New Machine Setup..." "Cyan"
Write-ColorOutput "This script will install Scoop, Chocolatey, and required packages." "Yellow"
Write-ColorOutput "-------------------------------------------------------------" "Cyan"

# Step 1: Install Scoop (no admin)
Write-ColorOutput "Step 1: Installing Scoop..." "Green"
if (-not (Test-CommandExists "scoop")) {
    try {
        Write-ColorOutput "Scoop not found. Installing Scoop..." "Yellow"
        Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
        Invoke-Expression (New-Object System.Net.WebClient).DownloadString('https://get.scoop.sh')
        # reload PATH
        $env:Path = [System.Environment]::GetEnvironmentVariable("Path","User") + ";" +
                    [System.Environment]::GetEnvironmentVariable("Path","Machine")
        Write-ColorOutput "Scoop installed successfully!" "Green"
    }
    catch {
        Write-ColorOutput "Failed to install Scoop: $_" "Red"
        exit 1
    }
}
else {
    Write-ColorOutput "Scoop is already installed." "Green"
}

# Step 2: Install Chocolatey bootstrap (requires admin via PowerShell)
Write-ColorOutput "Step 2: Installing Chocolatey..." "Green"
if (-not (Test-CommandExists "choco")) {
    try {
        Write-ColorOutput "Chocolatey not found. Bootstrapping via sudo powershell -c ..." "Yellow"
        sudo powershell -NoProfile -ExecutionPolicy Bypass -Command `
          "Set-ExecutionPolicy Bypass -Scope Process -Force; `
           [Net.ServicePointManager]::SecurityProtocol = `
             [Net.ServicePointManager]::SecurityProtocol -bor 3072; `
           Invoke-Expression ((New-Object Net.WebClient).DownloadString(`
             'https://community.chocolatey.org/install.ps1'`
           ))"
        # reload PATH after install
        $env:Path = [System.Environment]::GetEnvironmentVariable('Path','User') + ';' +
                    [System.Environment]::GetEnvironmentVariable('Path','Machine')
        Write-ColorOutput "Chocolatey installed successfully!" "Green"
    }
    catch {
        Write-ColorOutput "Failed to install Chocolatey: $_" "Red"
        exit 1
    }
}
else {
    Write-ColorOutput "Chocolatey is already installed." "Green"
}

# Step 3: Add Scoop buckets (no admin)
Write-ColorOutput "Step 3: Adding Scoop buckets..." "Green"
try {
    Write-ColorOutput "Adding Scoop buckets..." "Yellow"
    scoop bucket add extras
    scoop bucket add versions
    scoop bucket add java
    scoop bucket add personal https://github.com/PatrickRuddiman/PersonalScoopBucket
    Write-ColorOutput "Scoop buckets added successfully!" "Green"
}
catch {
    Write-ColorOutput "Warning: Failed to add some Scoop buckets: $_" "Yellow"
}

# Step 4: Install Scoop packages with retry logic (no admin)
Write-ColorOutput "Step 4: Installing Scoop packages..." "Green"
$packages = @(
    "7zip","azcopy","azure-functions-core-tools","caddy","concfg","dark","docfx",
    "fabric","ffmpeg","git","grep","ImageMagick","innounp","miniconda3","nano",
    "nuget","nvm","openapi-generator-cli","openjdk","openjdk22","openssh","pshazz",
    "python","python310","python38","rufus","sudo","sysinternals","vlc","office365business"
)
$maxRetries = 3
$attempt = 0
$success = $false

while (-not $success -and $attempt -lt $maxRetries) {
    $attempt++
    try {
        Write-ColorOutput "Installing Scoop packages (attempt $attempt of $maxRetries)..." "Yellow"
        & scoop install -y $packages
        Write-ColorOutput "Scoop packages installed successfully!" "Green"
        $success = $true
    }
    catch {
        Write-ColorOutput "Attempt $attempt failed: $_" "Red"
        if ($attempt -lt $maxRetries) {
            Write-ColorOutput "Retrying in 5 seconds..." "Yellow"
            Start-Sleep -Seconds 5
        }
        else {
            Write-ColorOutput "Failed to install Scoop packages after $maxRetries attempts." "Red"
        }
    }
}

# Step 5: Install Chocolatey packages (requires admin via sudo choco)
Write-ColorOutput "Step 5: Installing Chocolatey packages..." "Green"
try {
    Write-ColorOutput "Installing Chocolatey packages via sudo choco (this will take a while)..." "Yellow"
    sudo choco install -y `
        7zip.install autohotkey.install azure-cli docker-desktop dotnet `
        DotNet4.5.2 DotNet4.6.1 dotnet-6.0-runtime dotnet-6.0-sdk `
        dotnet-7.0-runtime dotnet-8.0-runtime dotnet-9.0-runtime `
        git git.install git-lfs git-lfs.install KB2919355 KB2919442 `
        microsoft-teams notepadplusplus notepadplusplus.install powertoys `
        visualstudio2022enterprise visualstudio2022-workload-azure `
        visualstudio2022-workload-netweb visualstudio2022-workload-node `
        vscode.install vscode-insiders.install WinDirStat
    Write-ColorOutput "Chocolatey packages installed successfully!" "Green"
}
catch {
    Write-ColorOutput "Warning: Some Chocolatey packages may have failed to install: $_" "Yellow"
}

# Step 6: Common environment configurations (no admin)
Write-ColorOutput "Step 6: Setting up common environment configurations..." "Green"
try {
    if (Test-CommandExists "git") {
        Write-ColorOutput "Configuring Git..." "Yellow"
        git config --global core.autocrlf input
    }

    if (Test-CommandExists "nvm") {
        Write-ColorOutput "Setting up Node.js via NVM..." "Yellow"
        nvm install latest
        nvm use latest
    }
}
catch {
    Write-ColorOutput "Warning: Some configurations may have failed: $_" "Yellow"
}

Write-ColorOutput "-------------------------------------------------------------" "Cyan"
Write-ColorOutput "Setup complete!" "Green"
Write-ColorOutput "You may need to restart your computer for some changes to take effect." "Yellow"
Write-ColorOutput "-------------------------------------------------------------" "Cyan"
