#!/usr/bin/env bash
# Check if a Debian system is ready for setup scripts
# Usage: curl -sSL https://raw.githubusercontent.com/PatrickRuddiman/Toolkit/main/MachineInit/check_install.sh | bash

set -e

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[0;33m'
NC='\033[0m' # No Color

check_command() {
    if command -v "$1" >/dev/null 2>&1; then
        echo -e "${GREEN}✓${NC} $1 installed"
        return 0
    else
        echo -e "${RED}✗${NC} $1 not installed"
        return 1
    fi
}

check_package() {
    if dpkg -l | grep -q "$1"; then
        echo -e "${GREEN}✓${NC} $1 installed"
        return 0
    else
        echo -e "${RED}✗${NC} $1 not installed"
        return 1
    fi
}

check_network() {
    if ping -c 1 google.com >/dev/null 2>&1; then
        echo -e "${GREEN}✓${NC} Network connection available"
        return 0
    else
        echo -e "${RED}✗${NC} No network connection"
        return 1
    fi
}

echo "======================================"
echo "Debian Installation Check"
echo "======================================"
echo

# Check if we're running Debian
if [ -f /etc/os-release ]; then
    . /etc/os-release
    if [ "$ID" == "debian" ] || [ "$ID_LIKE" == "debian" ]; then
        echo -e "${GREEN}✓${NC} Running Debian-based system: $PRETTY_NAME"
    else
        echo -e "${RED}✗${NC} Not running Debian (detected: $PRETTY_NAME)"
        echo -e "${YELLOW}WARNING:${NC} These scripts are designed for Debian-based systems."
    fi
else
    echo -e "${RED}✗${NC} Could not determine OS type"
fi

# Check for GUI environment
if [ -n "$DISPLAY" ] || [ -n "$WAYLAND_DISPLAY" ] || ps -e | grep -E -q "Xorg|xfce|gnome|kde|mate|cinnamon"; then
    echo -e "${GREEN}✓${NC} GUI environment detected"
    # Check if XFCE is installed
    if ps -e | grep -q "xfce"; then
        echo -e "${GREEN}✓${NC} XFCE desktop environment detected"
    else
        echo -e "${YELLOW}!${NC} XFCE desktop environment not detected"
        echo "   The GUI setup script is optimized for XFCE but may work with other desktops."
    fi
else
    echo -e "${YELLOW}!${NC} No GUI environment detected"
    echo "   If you want a desktop environment, install XFCE before running the GUI setup script."
fi

# Check for sudo or root access
if [ "$(id -u)" -eq 0 ]; then
    echo -e "${GREEN}✓${NC} Running as root"
    HAS_ROOT=true
elif command -v sudo >/dev/null 2>&1; then
    if sudo -n true 2>/dev/null; then
        echo -e "${GREEN}✓${NC} sudo access available"
        HAS_ROOT=true
    else
        echo -e "${YELLOW}!${NC} sudo installed but requires password"
        HAS_ROOT=true
    fi
else
    echo -e "${YELLOW}!${NC} sudo not installed"
    echo "   You will need to use root password for administrative tasks"
    echo "   (The setup scripts will prompt for the root password when needed)"
    HAS_ROOT=false
fi

# Check for essential commands
echo -e "\nChecking essential commands:"
missing_commands=0
for cmd in apt-get wget curl git; do
    check_command "$cmd" || missing_commands=$((missing_commands + 1))
done

# Check if sudo is installed separately
if ! command -v sudo >/dev/null 2>&1; then
    echo -e "${YELLOW}!${NC} sudo not installed"
    SUDO_MISSING=true
else
    SUDO_MISSING=false
fi

# Check network connectivity
echo -e "\nChecking network connectivity:"
check_network

# Output summary and recommendations
echo -e "\n======================================"
if [ $missing_commands -gt 0 ]; then
    echo -e "${YELLOW}You're missing $missing_commands essential commands.${NC}"
    if [ "$HAS_ROOT" = true ]; then
        echo "To install missing commands, run:"
        if [ "$SUDO_MISSING" = true ]; then
            echo "  su -c 'apt-get update && apt-get install -y wget curl git'"
        else
            echo "  sudo apt-get update && sudo apt-get install -y wget curl git"
        fi
    else
        echo "To install missing commands, run as root:"
        echo "  apt-get update && apt-get install -y wget curl git"
    fi
    echo
fi

if [ "$SUDO_MISSING" = true ]; then
    echo -e "${YELLOW}sudo is not installed.${NC}"
    echo "Our scripts can work without sudo, but installing it is recommended."
    echo "To install sudo, run as root:"
    echo "  apt-get update && apt-get install -y sudo && usermod -aG sudo $USER"
    echo "  (Then log out and log back in)"
    echo
fi

echo -e "${GREEN}Ready to proceed with setup!${NC}"
echo "You can run the setup with:"
echo "  curl -sSL https://raw.githubusercontent.com/PatrickRuddiman/Toolkit/main/MachineInit/quickstart.sh | bash"
echo "======================================"