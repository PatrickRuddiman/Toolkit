#!/usr/bin/env bash
set -e

# Basic GUI setup script for Debian-based systems (XFCE)

# Install packages
sudo apt-get update
sudo apt-get install -y conky parted gparted git apt-transport-https curl \
    software-properties-common ca-certificates gnupg2 plymouth

# Microsoft Edge repository
if ! grep -q "packages.microsoft.com" /etc/apt/sources.list.d/microsoft-edge.list 2>/dev/null; then
    curl -fsSL https://packages.microsoft.com/keys/microsoft.asc | sudo gpg --dearmor -o /usr/share/keyrings/microsoft.gpg
    echo "deb [arch=$(dpkg --print-architecture) signed-by=/usr/share/keyrings/microsoft.gpg] https://packages.microsoft.com/repos/edge stable main" | sudo tee /etc/apt/sources.list.d/microsoft-edge.list
fi

# VS Code repositories
if ! grep -q "packages.microsoft.com" /etc/apt/sources.list.d/vscode.list 2>/dev/null; then
    curl -fsSL https://packages.microsoft.com/keys/microsoft.asc | sudo gpg --dearmor -o /usr/share/keyrings/vscode.gpg
    echo "deb [arch=$(dpkg --print-architecture) signed-by=/usr/share/keyrings/vscode.gpg] https://packages.microsoft.com/repos/code stable main" | sudo tee /etc/apt/sources.list.d/vscode.list
fi

sudo apt-get update
sudo apt-get install -y microsoft-edge-stable code code-insiders docker.io git

# Install Dotnet
wget https://dot.net/v1/dotnet-install.sh -O /tmp/dotnet-install.sh
sudo bash /tmp/dotnet-install.sh --channel LTS

# Install Python and Node (via nvm)
sudo apt-get install -y python3 python3-pip
curl -o- https://raw.githubusercontent.com/nvm-sh/nvm/v0.39.7/install.sh | bash
export NVM_DIR="$HOME/.nvm"
[ -s "$NVM_DIR/nvm.sh" ] && . "$NVM_DIR/nvm.sh"
nvm install node

# Purge bloatware
sudo apt-get purge -y libreoffice* firefox* chromium*

# Enable non-free firmware repository
if ! grep -q "non-free" /etc/apt/sources.list; then
    sudo sed -i 's/^deb \(.*\) main$/deb \1 main non-free firmware/' /etc/apt/sources.list
    sudo apt-get update
    sudo apt-get install -y firmware-linux
fi

# Add current user to sudo and docker groups
TARGET_USER="${SUDO_USER:-$USER}"
if command -v sudo >/dev/null; then
    sudo usermod -aG sudo "$TARGET_USER"
    sudo usermod -aG docker "$TARGET_USER"
else
    su -c "usermod -aG sudo $TARGET_USER"
    su -c "usermod -aG docker $TARGET_USER"
fi

# Install Chicago95 XFCE theme
THEME_TMP="$(mktemp -d)"
git clone --depth 1 https://github.com/grassmunk/Chicago95 "$THEME_TMP/Chicago95"
mkdir -p "$HOME/.themes" "$HOME/.icons"
cp -r "$THEME_TMP/Chicago95/Theme/Chicago95" "$HOME/.themes/"
cp -r "$THEME_TMP/Chicago95/Icons/"* "$HOME/.icons/"
sudo cp "$THEME_TMP/Chicago95/Fonts/vga_font/LessPerfectDOSVGA.ttf" /usr/share/fonts/
sudo fc-cache -f
rm -rf "$THEME_TMP"

# Install adi1090x Plymouth themes and set 'pixels'
if [ ! -d /usr/share/plymouth/adi1090x-plymouth-themes ]; then
    sudo git clone --depth 1 https://github.com/adi1090x/plymouth-themes \
        /usr/share/plymouth/adi1090x-plymouth-themes
    sudo cp -r /usr/share/plymouth/adi1090x-plymouth-themes/pack_*/* \
        /usr/share/plymouth/themes/
fi
if [ -f /usr/share/plymouth/themes/pixels/pixels.plymouth ]; then
    sudo update-alternatives --install /usr/share/plymouth/themes/default.plymouth \
        default.plymouth /usr/share/plymouth/themes/pixels/pixels.plymouth 100
    sudo update-alternatives --set default.plymouth \
        /usr/share/plymouth/themes/pixels/pixels.plymouth
    sudo update-initramfs -u
fi

# Deploy wallpapers from repository if available
WALL_SRC="$(dirname "$0")/../wallpapers"
if [ -d "$WALL_SRC" ]; then
    mkdir -p "$HOME/Pictures/wallpapers"
    cp -n "$WALL_SRC"/*.{png,jfif,jpg,jpeg} "$HOME/Pictures/wallpapers" 2>/dev/null || true
fi

# Configure lightdm for user list at login
if [ -f /etc/lightdm/lightdm.conf ]; then
    sudo sed -i 's/^#*greeter-hide-users=.*/greeter-hide-users=false/' /etc/lightdm/lightdm.conf
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

sudo systemctl enable docker

printf '\nSetup complete! Reboot recommended.\n'
