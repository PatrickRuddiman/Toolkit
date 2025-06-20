#!/usr/bin/env bash
# Quick Start script for fresh Debian installations
# Usage: curl -sSL https://raw.githubusercontent.com/PatrickRuddiman/Toolkit/main/MachineInit/quickstart.sh | bash

set -e

echo "======================================"
echo "Debian Quick Start Setup"
echo "======================================"
echo

# Detect if we're in a GUI environment
if [ -n "$DISPLAY" ] || [ -n "$WAYLAND_DISPLAY" ] || ps -e | grep -E -q "Xorg|xfce|gnome|kde|mate|cinnamon"; then
    HAS_GUI=true
    echo "GUI environment detected."
else
    HAS_GUI=false
    echo "No GUI environment detected. Assuming server/headless setup."
fi

# Ensure we have sudo and user is in sudo group
if ! command -v sudo >/dev/null; then
    echo "sudo not installed. Installing sudo using root password..."
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
else
    sudo_or_su() {
        sudo "$@"
    }
fi

# Ensure basic tools are installed
echo "Installing git and curl..."
sudo_or_su "apt update && apt install -y git curl"

# Ask user which setup to run
if $HAS_GUI; then
    echo
    echo "Which setup would you like to run?"
    echo "1) GUI setup (XFCE)"
    echo "2) Server/headless setup"
    read -p "Enter your choice (1/2) [default: 1]: " choice
    
    case "$choice" in
        2) SETUP_TYPE="server" ;;
        *) SETUP_TYPE="gui" ;;
    esac
else
    SETUP_TYPE="server"
fi

# Run the setup script
echo "Running $SETUP_TYPE setup..."
bash -c "$(curl -fsSL https://raw.githubusercontent.com/PatrickRuddiman/Toolkit/main/MachineInit/setup.sh)" -- "$SETUP_TYPE"