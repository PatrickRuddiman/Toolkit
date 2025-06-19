#!/usr/bin/env bash
set -e

# Non-GUI development setup for short-lived environments

sudo apt-get update
sudo apt-get install -y git curl apt-transport-https ca-certificates gnupg2 \
    software-properties-common build-essential docker.io python3 python3-pip

# Install dotnet
wget https://dot.net/v1/dotnet-install.sh -O /tmp/dotnet-install.sh
sudo bash /tmp/dotnet-install.sh --channel LTS

# Node.js via nvm for TypeScript
curl -o- https://raw.githubusercontent.com/nvm-sh/nvm/v0.39.7/install.sh | bash
export NVM_DIR="$HOME/.nvm"
[ -s "$NVM_DIR/nvm.sh" ] && . "$NVM_DIR/nvm.sh"
nvm install node
npm install -g typescript

# Docker group for current user
sudo usermod -aG docker "$USER"
sudo systemctl enable docker

# VS Code server (tunnel)
if ! command -v code >/dev/null; then
    curl -fsSL "https://code.visualstudio.com/sha/download?build=stable&os=cli-linux-x64" > /tmp/code.tar.gz
    mkdir -p "$HOME/.vscode-server"
    tar -xzf /tmp/code.tar.gz -C "$HOME/.vscode-server" --strip-components=1
fi

printf '\nNon-GUI setup complete.\n'
