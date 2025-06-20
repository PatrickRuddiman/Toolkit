#!/usr/bin/env bash
set -e

echo "Non-GUI development setup for Debian server environments"

# Define a function to execute commands as root
run_as_root() {
    if [ "$(id -u)" -eq 0 ]; then
        # Already running as root
        "$@"
    elif command -v sudo >/dev/null 2>&1; then
        # Use sudo if available
        sudo "$@"
    else
        # Fall back to su
        su -c "$@"
    fi
}

# Ensure we have sudo
if ! command -v sudo >/dev/null; then
    echo "sudo not installed. Would you like to install it? (recommended) (y/N)"
    read -r response
    if [[ "$response" =~ ^([yY][eE][sS]|[yY])$ ]]; then
        echo "Installing sudo..."
        run_as_root apt-get update
        run_as_root apt-get install -y sudo
        if [ -n "$USER" ]; then
            run_as_root usermod -aG sudo "$USER"
            echo "Added $USER to sudo group. You may need to log out and back in."
        fi
    fi
fi

# Install essential packages
run_as_root apt-get update
run_as_root apt-get install -y git curl apt-transport-https ca-certificates gnupg2 \
    software-properties-common build-essential docker.io python3 python3-pip \
    ssh nano vim

# Install dotnet
if ! command -v dotnet >/dev/null; then
    echo "Installing .NET..."
    wget https://dot.net/v1/dotnet-install.sh -O /tmp/dotnet-install.sh
    run_as_root bash /tmp/dotnet-install.sh --channel LTS
    
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

# Docker group for current user
if getent group docker >/dev/null; then
    run_as_root usermod -aG docker "$USER"
    run_as_root systemctl enable docker
    run_as_root systemctl start docker
else
    echo "Docker group not found. Docker may not be installed correctly."
fi

# VS Code server (tunnel)
if ! command -v code >/dev/null; then
    echo "Installing VS Code server..."
    curl -fsSL "https://code.visualstudio.com/sha/download?build=stable&os=cli-linux-x64" > /tmp/code.tar.gz
    mkdir -p "$HOME/.vscode-server"
    tar -xzf /tmp/code.tar.gz -C "$HOME/.vscode-server" --strip-components=1
    
    # Add VS Code to PATH
    if [ -d "$HOME/.vscode-server/bin" ]; then
        echo 'export PATH="$HOME/.vscode-server/bin:$PATH"' >> ~/.bashrc
        export PATH="$HOME/.vscode-server/bin:$PATH"
    fi
fi

printf '\nNon-GUI setup complete. You may need to log out and back in for group changes to take effect.\n'
echo "Run 'source ~/.bashrc' to apply PATH changes to your current session."
