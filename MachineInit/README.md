# Machine Initialization Scripts for Debian

These scripts help set up a fresh Debian installation for development work.

## Quick Start

For a fresh Debian install, you'll need to:

1. Install git and curl (if not already installed):
   - If you have sudo:
     ```bash
     sudo apt-get update
     sudo apt-get install -y git curl
     ```
   - If you don't have sudo but know the root password:
     ```bash
     su -c "apt-get update && apt-get install -y git curl"
     ```

2. Clone this repository:
```bash
git clone https://github.com/PatrickRuddiman/Toolkit.git
cd Toolkit/MachineInit
```

3. Run the appropriate setup script:
   - For GUI (XFCE) environments:
     ```bash
     bash setup.sh gui
     ```
   - For headless/server environments:
     ```bash
     bash setup.sh server
     ```

## One-liner Installation

For one-liner install commands:

### With sudo:
```bash
sudo apt-get update && sudo apt-get install -y curl && bash -c "$(curl -fsSL https://raw.githubusercontent.com/PatrickRuddiman/Toolkit/main/MachineInit/setup.sh)" -- gui
```

### Without sudo (using root password):
```bash
su -c "apt-get update && apt-get install -y curl" && bash -c "$(curl -fsSL https://raw.githubusercontent.com/PatrickRuddiman/Toolkit/main/MachineInit/setup.sh)" -- gui
```

For server/headless installation, replace `gui` with `server` in the commands above.

## What These Scripts Do

- **setup_gui.sh**: Configures a Debian XFCE environment with development tools and a customized desktop experience
- **setup_server.sh**: Configures a headless Debian environment with essential development tools
- **setup.sh**: Wrapper script that handles repository cloning and calls the appropriate setup script

## Note on sudo

These scripts are designed to work whether or not you have sudo installed:
- If sudo is installed, they will use it
- If sudo is not installed, they will either:
  - Prompt to install sudo (recommended)
  - Use `su` with the root password when needed

After running these scripts, a reboot is recommended to ensure all changes take effect.