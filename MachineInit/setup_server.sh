#!/usr/bin/env bash
set -e

echo "Non-GUI development setup for Debian server environments"

# Sudo is required for this script

# Ensure we have sudo
if ! command -v sudo >/dev/null; then
    echo "sudo not installed. Installing sudo..."
    su -c "apt update && apt install -y sudo && usermod -aG sudo $USER"
    echo "sudo has been installed and $USER added to sudo group."
    echo "You need to log out and log back in for changes to take effect."
    echo "After logging back in, run this script again."
    exit 0
elif ! groups | grep -q sudo && [ "$(id -u)" -ne 0 ]; then
    echo "User $USER is not in the sudo group. Adding user to sudo group..."
    su -c "usermod -aG sudo $USER"
    echo "User added to sudo group."
    echo "You need to log out and log back in for changes to take effect."
    echo "After logging back in, run this script again."
    exit 0
fi

# Install essential packages
sudo apt update
sudo apt install -y git curl apt-transport-https ca-certificates gnupg2 \
    software-properties-common build-essential docker.io python3 python3-pip \
    ssh nano vim

# Install dotnet
if ! command -v dotnet >/dev/null; then
    echo "Installing .NET..."
    wget https://dot.net/v1/dotnet-install.sh -O /tmp/dotnet-install.sh
    sudo bash /tmp/dotnet-install.sh --channel LTS
    
    # Add dotnet to PATH if not already there
    if ! grep -q ".dotnet" ~/.bashrc; then
        echo 'export PATH="$HOME/.dotnet:$PATH"' >> ~/.bashrc
        export PATH="$HOME/.dotnet:$PATH"
    fi
fi

# Node.js via nvm for TypeScript
if ! command -v nvm >/dev/null; then
    echo "Installing Node.js via nvm..."
    curl -o- https://raw.githubusercontent.com/nvm-sh/nvm/v0.39.7/install.sh | bash
    export NVM_DIR="$HOME/.nvm"
    [ -s "$NVM_DIR/nvm.sh" ] && . "$NVM_DIR/nvm.sh"
    nvm install node
    npm install -g typescript
    
    # Make sure nvm is loaded in future shells
    if ! grep -q "NVM_DIR" ~/.bashrc; then
        echo 'export NVM_DIR="$HOME/.nvm"' >> ~/.bashrc
        echo '[ -s "$NVM_DIR/nvm.sh" ] && . "$NVM_DIR/nvm.sh"' >> ~/.bashrc
    fi
fi

# Removed redundant .NET installation block.

# Node.js via nvm for TypeScript
if ! command -v nvm >/dev/null; then
    echo "Installing Node Version Manager (nvm)..."
    curl -o- https://raw.githubusercontent.com/nvm-sh/nvm/v0.39.3/install.sh | bash
    
    # Source nvm
    export NVM_DIR="$HOME/.nvm"
    [ -s "$NVM_DIR/nvm.sh" ] && \. "$NVM_DIR/nvm.sh"
    
    # Install latest LTS version of Node.js
    nvm install --lts
    nvm use --lts
    
    # Install TypeScript globally
    npm install -g typescript
else
    echo "nvm already installed, checking for updates..."
    export NVM_DIR="$HOME/.nvm"
    [ -s "$NVM_DIR/nvm.sh" ] && \. "$NVM_DIR/nvm.sh"
    nvm install --lts --reinstall-packages-from=default
fi

# Docker group for current user
if getent group docker >/dev/null; then
    echo "Adding user to docker group..."
    sudo usermod -aG docker "$USER"
    sudo systemctl enable docker
    sudo systemctl start docker
else
    echo "Docker group not found. Docker may not be installed correctly."
fi

# VS Code server (tunnel)
if ! command -v code >/dev/null; then
    echo "Installing VS Code server..."
    curl -fsSL "https://code.visualstudio.com/sha/download?build=stable&os=cli-linux-x64" > /tmp/code.tar.gz
    mkdir -p "$HOME/.vscode-server"
    tar -xzf /tmp/code.tar.gz -C "$HOME/.vscode-server" --strip-components=1
    rm -f /tmp/code.tar.gz
    
    # Add VS Code to PATH if not already there
    if [ -d "$HOME/.vscode-server/bin" ] && ! grep -q "/.vscode-server/bin" ~/.bashrc; then
        echo 'export PATH="$HOME/.vscode-server/bin:$PATH"' >> ~/.bashrc
        export PATH="$HOME/.vscode-server/bin:$PATH"
    fi
else
    echo "VS Code server already installed, checking for updates..."
    code update
fi

# Create development directories if they don't exist
echo "Setting up development directories..."
mkdir -p "$HOME/dev/projects"
mkdir -p "$HOME/dev/repos"

printf '\nNon-GUI setup complete. You may need to log out and back in for group changes to take effect.\n'
echo "Run 'source ~/.bashrc' to apply PATH changes to your current session."
