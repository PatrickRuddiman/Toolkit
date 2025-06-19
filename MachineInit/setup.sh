#!/usr/bin/env bash
# Usage:
#   curl -sSL https://raw.githubusercontent.com/PatrickRuddiman/Toolkit/main/MachineInit/setup.sh | bash -s -- gui
# Wrapper to call GUI or server setup and clone the repo if needed
set -e

REPO_URL="https://github.com/PatrickRuddiman/Toolkit"

# If the script isn't running from within the repository, clone it and re-exec
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
if [ ! -d "$SCRIPT_DIR/.git" ]; then
    TMP_DIR="$(mktemp -d)"
    git clone --depth 1 "$REPO_URL" "$TMP_DIR"
    exec bash "$TMP_DIR/MachineInit/setup.sh" "$@"
fi

case "$1" in
    gui)
        bash "$(dirname "$0")/setup_gui.sh" ;;
    server|non-gui|headless)
        bash "$(dirname "$0")/setup_server.sh" ;;
    *)
        echo "Usage: $0 {gui|server}" >&2
        exit 1
        ;;
esac

