#!/usr/bin/env bash
# Usage:
#   curl -sSL https://raw.githubusercontent.com/PatrickRuddiman/Toolkit/main/MachineInit/setup.sh | bash -s -- gui
# Wrapper to call GUI or server setup and clone the repo if needed
set -e

REPO_URL="https://github.com/PatrickRuddiman/Toolkit"

# Check for required dependencies
check_dependencies() {
    local missing_deps=()
    
    for cmd in git curl sudo; do
        if ! command -v "$cmd" >/dev/null 2>&1; then
            missing_deps+=("$cmd")
        fi
    done
    
    if [ ${#missing_deps[@]} -gt 0 ]; then
        echo "Missing required dependencies: ${missing_deps[*]}"
        echo "Installing dependencies..."
        apt update 2>/dev/null || { echo "Please run: sudo apt update"; exit 1; }
        apt install -y "${missing_deps[@]}" 2>/dev/null || { 
            echo "Please run: sudo apt install -y ${missing_deps[*]}"; 
            exit 1; 
        }
    fi
}

# Check if we're on Debian
check_debian() {
    if [ -f /etc/os-release ]; then
        . /etc/os-release
        if [ "$ID" != "debian" ] && [ "$ID_LIKE" != "debian" ]; then
            echo "Warning: This script is designed for Debian-based systems."
            echo "Current OS: $PRETTY_NAME"
            echo "Continue anyway? (y/N)"
            read -r response
            if [[ ! "$response" =~ ^([yY][eE][sS]|[yY])$ ]]; then
                exit 1
            fi
        fi
    fi
}

# If not running as root, ensure we have sudo access
if [ "$(id -u)" -ne 0 ]; then
    if ! command -v sudo >/dev/null 2>&1; then
        echo "sudo not installed. Installing sudo with root password..."
        su -c "apt update && apt install -y sudo && usermod -aG sudo $USER"
        echo "sudo installed. You need to log out and log back in for changes to take effect."
        echo "After logging back in, run this script again."
        exit 0
    elif ! groups | grep -q sudo; then
        echo "User $USER is not in the sudo group. Adding user to sudo group..."
        su -c "usermod -aG sudo $USER"
        echo "User added to sudo group."
        echo "You need to log out and log back in for changes to take effect."
        echo "After logging back in, run this script again."
        exit 0
    else
        SUDO="sudo"
    fi
else
    SUDO=""
fi

# Check dependencies and Debian
check_dependencies
check_debian

# If the script isn't running from within the repository, clone it and re-exec
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
if [ ! -d "$SCRIPT_DIR/.git" ] && [ ! -d "$SCRIPT_DIR/../.git" ]; then
    echo "Cloning repository..."
    TMP_DIR="$(mktemp -d)"
    cd "$TMP_DIR" || exit 1
    git clone --depth 1 "$REPO_URL" "$TMP_DIR"
    cd "$TMP_DIR/MachineInit" || { 
        cd "$TMP_DIR/Toolkit/MachineInit" || {
            echo "Failed to find MachineInit directory in cloned repository"
            exit 1
        }
    }
    exec bash "./setup.sh" "$@"
fi

# Display a welcome message
echo "==================================="
echo "Debian Machine Setup"
echo "==================================="
echo "This script will configure your Debian system."
echo "It may take some time to complete."
echo

case "$1" in
    gui)
        echo "Setting up GUI environment (XFCE)..."
        bash "$(dirname "$0")/setup_gui.sh" ;;
    server|non-gui|headless)
        echo "Setting up server/headless environment..."
        bash "$(dirname "$0")/setup_server.sh" ;;
    *)
        echo "Usage: $0 {gui|server}" >&2
        exit 1
        ;;
esac

