#!/usr/bin/env bash
set -e

# Basic GUI setup script for Debian-based systems (XFCE)

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
        su -c "$*"
    fi
}

# Make sure we have XFCE installed
if ! dpkg -l | grep -q xfce4; then
    echo "XFCE not detected. Would you like to install it? (y/N)"
    read -r response
    if [[ "$response" =~ ^([yY][eE][sS]|[yY])$ ]]; then
        run_as_root apt-get update
        run_as_root apt-get install -y task-xfce-desktop
    else
        echo "XFCE is required for this GUI setup. Exiting..."
        exit 1
    fi
fi

# Install packages
run_as_root apt-get update
run_as_root apt-get install -y conky parted gparted git apt-transport-https curl \
    software-properties-common ca-certificates gnupg2 plymouth wget \
    xfce4-terminal xfce4-goodies lightdm

# Microsoft Edge repository
if ! grep -q "packages.microsoft.com" /etc/apt/sources.list.d/microsoft-edge.list 2>/dev/null; then
    curl -fsSL https://packages.microsoft.com/keys/microsoft.asc | run_as_root gpg --dearmor -o /usr/share/keyrings/microsoft.gpg
    echo "deb [arch=$(dpkg --print-architecture) signed-by=/usr/share/keyrings/microsoft.gpg] https://packages.microsoft.com/repos/edge stable main" | run_as_root tee /etc/apt/sources.list.d/microsoft-edge.list
fi

# VS Code repositories
if ! grep -q "packages.microsoft.com" /etc/apt/sources.list.d/vscode.list 2>/dev/null; then
    curl -fsSL https://packages.microsoft.com/keys/microsoft.asc | run_as_root gpg --dearmor -o /usr/share/keyrings/vscode.gpg
    echo "deb [arch=$(dpkg --print-architecture) signed-by=/usr/share/keyrings/vscode.gpg] https://packages.microsoft.com/repos/code stable main" | run_as_root tee /etc/apt/sources.list.d/vscode.list
fi

run_as_root apt-get update
run_as_root apt-get install -y microsoft-edge-stable code code-insiders docker.io git

# Install Dotnet
wget https://dot.net/v1/dotnet-install.sh -O /tmp/dotnet-install.sh
run_as_root bash /tmp/dotnet-install.sh --channel LTS

# Install Python and Node (via nvm)
run_as_root apt-get install -y python3 python3-pip
curl -o- https://raw.githubusercontent.com/nvm-sh/nvm/v0.39.7/install.sh | bash
export NVM_DIR="$HOME/.nvm"
[ -s "$NVM_DIR/nvm.sh" ] && . "$NVM_DIR/nvm.sh"
nvm install node

# Purge bloatware
run_as_root apt-get purge -y libreoffice* firefox* chromium*

# Enable non-free firmware repository
if ! grep -q "non-free" /etc/apt/sources.list; then
    run_as_root sed -i 's/^deb \(.*\) main$/deb \1 main non-free firmware/' /etc/apt/sources.list
    run_as_root apt-get update
    run_as_root apt-get install -y firmware-linux
fi

# Add current user to sudo and docker groups
TARGET_USER="${SUDO_USER:-$USER}"
if command -v usermod >/dev/null; then
    run_as_root usermod -aG sudo "$TARGET_USER"
    run_as_root usermod -aG docker "$TARGET_USER"
fi

# Install Chicago95 XFCE theme
THEME_TMP="$(mktemp -d)"
git clone --depth 1 https://github.com/grassmunk/Chicago95 "$THEME_TMP/Chicago95"
mkdir -p "$HOME/.themes" "$HOME/.icons"
cp -r "$THEME_TMP/Chicago95/Theme/Chicago95" "$HOME/.themes/"
cp -r "$THEME_TMP/Chicago95/Icons/"* "$HOME/.icons/"
run_as_root cp "$THEME_TMP/Chicago95/Fonts/vga_font/LessPerfectDOSVGA.ttf" /usr/share/fonts/
run_as_root fc-cache -f
rm -rf "$THEME_TMP"

# Install adi1090x Plymouth themes and set 'pixels'
if [ ! -d /usr/share/plymouth/adi1090x-plymouth-themes ]; then
    run_as_root git clone --depth 1 https://github.com/adi1090x/plymouth-themes \
        /usr/share/plymouth/adi1090x-plymouth-themes
    run_as_root cp -r /usr/share/plymouth/adi1090x-plymouth-themes/pack_*/* \
        /usr/share/plymouth/themes/
fi
if [ -f /usr/share/plymouth/themes/pixels/pixels.plymouth ]; then
    run_as_root update-alternatives --install /usr/share/plymouth/themes/default.plymouth \
        default.plymouth /usr/share/plymouth/themes/pixels/pixels.plymouth 100
    run_as_root update-alternatives --set default.plymouth \
        /usr/share/plymouth/themes/pixels/pixels.plymouth
    run_as_root update-initramfs -u
fi

# Deploy wallpapers from repository if available
WALL_SRC="$(dirname "$0")/../wallpapers"
if [ -d "$WALL_SRC" ]; then
    mkdir -p "$HOME/Pictures/wallpapers"
    cp -n "$WALL_SRC"/*.{png,jfif,jpg,jpeg} "$HOME/Pictures/wallpapers" 2>/dev/null || true
fi

# Configure lightdm for user list at login
if [ -f /etc/lightdm/lightdm.conf ]; then
    run_as_root sed -i 's/^#*greeter-hide-users=.*/greeter-hide-users=false/' /etc/lightdm/lightdm.conf
fi

# Download GitHub profile photo and set as user picture
if command -v curl >/dev/null; then
    GH_USER=$(git config --global user.name | awk '{print $1}')
    if [ -n "$GH_USER" ]; then
        curl -fsSL "https://github.com/$GH_USER.png" -o "$HOME/.face"
    fi
fi

# Conky configuration (mid-century modern style dock)
mkdir -p "$HOME/.config/conky"
cat > "$HOME/.config/conky/conky.conf" <<'EOC'
conky.config = {
    alignment = 'top_right',
    background = true,
    double_buffer = true,
    own_window = true,
    own_window_type = 'dock',
    own_window_argb_visual = true,
    own_window_argb_value = 150,
    gap_x = 20,
    gap_y = 40,
    minimum_width = 260,
    minimum_height = 620,
    use_xft = true,
    font = 'DejaVu Sans:size=10',
    default_color = '#333333',
    color1 = '#ff6600',
};

conky.text = [[
${font DejaVu Sans:bold:size=20}${color1}${time %H:%M}${color}${font}
${font DejaVu Sans:size=11}${time %A %d %B %Y}${font}

${color1}System${color} ${hr 1}
Host: ${nodename}
Kernel: ${kernel}
Uptime: ${uptime}

${color1}CPU (${cpus} cores)${color} ${hr 1}
${cpugraph 20,240}

${color1}Memory${color} ${hr 1}
RAM ${mem} / ${memmax} (${memperc}%)
${membar 4}

${color1}Disks${color} ${hr 1}
${execi 60 lsblk -dn -o NAME,SIZE | awk '{printf "%s %s\n",$1,$2}'}

${color1}Network${color} ${hr 1}
${execi 30 bash -c 'for i in $(ls /sys/class/net | grep -v lo); do ip=$(ip -o -4 addr show $i | awk "{print \$4}"); echo "$i $ip"; done'}
]];
EOC

run_as_root systemctl enable docker

printf '\nSetup complete! Reboot recommended.\n'
printf 'Note: If this is your first time installing sudo, you may need to log out and log back in for sudo access.\n'
