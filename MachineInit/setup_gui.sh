#!/usr/bin/env bash
set -e

# Basic GUI setup script for Debian-based systems (XFCE)

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

# Make sure we have XFCE installed
if ! dpkg -l | grep -q xfce4; then
    echo "XFCE not detected. Would you like to install it? (y/N)"
    read -r response
    if [[ "$response" =~ ^([yY][eE][sS]|[yY])$ ]]; then
        sudo apt update
        sudo apt install -y task-xfce-desktop
    else
        echo "XFCE is required for this GUI setup. Exiting..."
        exit 1
    fi
fi

# Install packages
sudo apt update
sudo apt install -y conky parted gparted git apt-transport-https curl \
    software-properties-common ca-certificates gnupg2 plymouth wget \
    xfce4-terminal xfce4-goodies lightdm

# Function to safely add GPG key
add_gpg_key() {
    local key_url=$1
    local key_path=$2
    local repo_name=$3
    
    echo "Setting up $repo_name repository..."
    # Check if key already exists
    if [ -f "$key_path" ]; then
        echo "GPG key for $repo_name already exists, skipping key download."
    else
        echo "Downloading GPG key for $repo_name..."
        curl -fsSL "$key_url" | sudo gpg --dearmor -o "$key_path"
    fi
}

# Microsoft Edge repository
if ! grep -q "packages.microsoft.com" /etc/apt/sources.list.d/microsoft-edge.list 2>/dev/null; then
    add_gpg_key "https://packages.microsoft.com/keys/microsoft.asc" "/usr/share/keyrings/microsoft.gpg" "Microsoft Edge"
    echo "deb [arch=$(dpkg --print-architecture) signed-by=/usr/share/keyrings/microsoft.gpg] https://packages.microsoft.com/repos/edge stable main" | sudo tee /etc/apt/sources.list.d/microsoft-edge.list
fi

# VS Code repositories
if ! grep -q "packages.microsoft.com" /etc/apt/sources.list.d/vscode.list 2>/dev/null; then
    add_gpg_key "https://packages.microsoft.com/keys/microsoft.asc" "/usr/share/keyrings/vscode.gpg" "Visual Studio Code"
    echo "deb [arch=$(dpkg --print-architecture) signed-by=/usr/share/keyrings/vscode.gpg] https://packages.microsoft.com/repos/code stable main" | sudo tee /etc/apt/sources.list.d/vscode.list
fi

sudo apt update
sudo apt install -y microsoft-edge-stable code code-insiders docker.io git

# Install Dotnet
if ! command -v dotnet >/dev/null 2>&1; then
    echo "Installing .NET..."
    wget https://dot.net/v1/dotnet-install.sh -O /tmp/dotnet-install.sh
    chmod +x /tmp/dotnet-install.sh
    bash /tmp/dotnet-install.sh --channel LTS
    
    # Add dotnet to PATH in .bashrc if not already there
    if ! grep -q 'export PATH=$PATH:$HOME/.dotnet' "$HOME/.bashrc"; then
        echo 'export PATH=$PATH:$HOME/.dotnet' >> "$HOME/.bashrc"
        echo 'export DOTNET_ROOT=$HOME/.dotnet' >> "$HOME/.bashrc"
    fi
    echo ".NET installed."
else
    echo ".NET already installed, skipping."
fi

# Install Python and Node (via nvm)
sudo apt install -y python3 python3-pip

# Install Node.js via NVM if not already installed
if ! command -v nvm >/dev/null 2>&1; then
    echo "Installing NVM (Node Version Manager)..."
    export NVM_DIR="$HOME/.nvm"
    
    # Check if .nvm directory already exists
    if [ ! -d "$NVM_DIR" ]; then
        curl -o- https://raw.githubusercontent.com/nvm-sh/nvm/v0.39.7/install.sh | bash
        
        # Source NVM
        [ -s "$NVM_DIR/nvm.sh" ] && . "$NVM_DIR/nvm.sh"
        
        # Install latest LTS version of Node.js
        nvm install --lts
        
        echo "NVM and Node.js LTS installed."
    else
        echo "NVM directory exists, sourcing and installing Node.js..."
        [ -s "$NVM_DIR/nvm.sh" ] && . "$NVM_DIR/nvm.sh"
        nvm install --lts
    fi
else
    echo "NVM already installed, skipping."
fi

# Purge bloatware
sudo apt purge -y libreoffice* firefox* chromium*

# Enable non-free firmware repository for Debian
if [ -f /etc/apt/sources.list ]; then
    echo "Checking for non-free firmware repository..."
    if ! grep -q "non-free" /etc/apt/sources.list; then
        echo "Enabling non-free firmware repository..."
        # Safely modify the sources.list file
        sudo cp /etc/apt/sources.list /etc/apt/sources.list.bak
        # Different versions of Debian have different repository names (non-free vs firmware)
        if grep -q "bookworm\|bullseye" /etc/apt/sources.list; then
            # Debian 11 (Bullseye) or 12 (Bookworm)
            sudo sed -i 's/^deb \(.*\) main$/deb \1 main non-free-firmware non-free contrib/' /etc/apt/sources.list
        else
            # Older Debian versions
            sudo sed -i 's/^deb \(.*\) main$/deb \1 main non-free contrib/' /etc/apt/sources.list
        fi
        sudo apt update
        sudo apt install -y firmware-linux
        echo "Non-free firmware repository enabled."
    else
        echo "Non-free firmware repository already enabled, skipping."
    fi
else
    echo "Warning: /etc/apt/sources.list not found, skipping firmware repository configuration."
fi

# Add current user to sudo and docker groups
TARGET_USER="${SUDO_USER:-$USER}"
if command -v usermod >/dev/null; then
    sudo usermod -aG sudo "$TARGET_USER"
    sudo usermod -aG docker "$TARGET_USER"
fi

# Install Chicago95 XFCE theme
echo "Setting up Chicago95 theme..."
if [ ! -d "$HOME/.themes/Chicago95" ]; then
    THEME_TMP="$(mktemp -d)"
    echo "Cloning Chicago95 theme repository..."
    git clone --depth 1 https://github.com/grassmunk/Chicago95 "$THEME_TMP/Chicago95"
    mkdir -p "$HOME/.themes" "$HOME/.icons"
    cp -r "$THEME_TMP/Chicago95/Theme/Chicago95" "$HOME/.themes/"
    cp -r "$THEME_TMP/Chicago95/Icons/"* "$HOME/.icons/"
    sudo cp "$THEME_TMP/Chicago95/Fonts/vga_font/LessPerfectDOSVGA.ttf" /usr/share/fonts/
    sudo fc-cache -f
    rm -rf "$THEME_TMP"
    echo "Chicago95 theme installed."
else
    echo "Chicago95 theme already installed, skipping."
fi

# Apply the theme if XFCE is running
if command -v xfconf-query >/dev/null 2>&1; then
    echo "Applying Chicago95 theme..."
    xfconf-query -c xsettings -p /Net/ThemeName -s "Chicago95" 2>/dev/null || true
    xfconf-query -c xsettings -p /Net/IconThemeName -s "Chicago95" 2>/dev/null || true
    xfconf-query -c xfwm4 -p /general/theme -s "Chicago95" 2>/dev/null || true
fi

# Install adi1090x Plymouth themes and set 'pixels'
echo "Setting up Plymouth boot screen..."
if [ ! -d /usr/share/plymouth/adi1090x-plymouth-themes ]; then
    echo "Installing Plymouth themes..."
    sudo git clone --depth 1 https://github.com/adi1090x/plymouth-themes \
        /usr/share/plymouth/adi1090x-plymouth-themes
    sudo cp -r /usr/share/plymouth/adi1090x-plymouth-themes/pack_*/* \
        /usr/share/plymouth/themes/
    echo "Plymouth themes installed."
else
    echo "Plymouth themes already installed, skipping."
fi

if [ -f /usr/share/plymouth/themes/pixels/pixels.plymouth ]; then
    echo "Setting up 'pixels' Plymouth theme..."
    sudo update-alternatives --install /usr/share/plymouth/themes/default.plymouth \
        default.plymouth /usr/share/plymouth/themes/pixels/pixels.plymouth 100
    sudo update-alternatives --set default.plymouth \
        /usr/share/plymouth/themes/pixels/pixels.plymouth
    
    echo "Configuring Plymouth..."
    
    # Update GRUB configuration to show Plymouth
    if [ -f /etc/default/grub ]; then
        # Add or update the GRUB_CMDLINE_LINUX_DEFAULT to include splash and quiet
        if grep -q "GRUB_CMDLINE_LINUX_DEFAULT" /etc/default/grub; then
            # Check if it already has splash and quiet
            if ! grep -q "splash" /etc/default/grub || ! grep -q "quiet" /etc/default/grub; then
                # Extract the current value of GRUB_CMDLINE_LINUX_DEFAULT
                current_value=$(grep '^GRUB_CMDLINE_LINUX_DEFAULT=' /etc/default/grub | cut -d'"' -f2)
                
                # Create a clean updated value
                updated_value=""
                # Add quiet if it's not already present
                if ! echo "$current_value" | grep -qw "quiet"; then
                    updated_value="$updated_value quiet"
                else
                    # Keep existing quiet param
                    updated_value="$updated_value quiet"
                fi
                
                # Add splash if it's not already present
                if ! echo "$current_value" | grep -qw "splash"; then
                    updated_value="$updated_value splash"
                else
                    # Keep existing splash param
                    updated_value="$updated_value splash"
                fi
                
                # Add any other existing parameters that aren't quiet or splash
                for param in $current_value; do
                    if [ "$param" != "quiet" ] && [ "$param" != "splash" ]; then
                        updated_value="$updated_value $param"
                    fi
                done
                
                # Trim extra spaces
                updated_value=$(echo "$updated_value" | xargs)
                
                # Update the GRUB_CMDLINE_LINUX_DEFAULT value with properly quoted string
                sudo sed -i "s|^GRUB_CMDLINE_LINUX_DEFAULT=.*|GRUB_CMDLINE_LINUX_DEFAULT=\"$updated_value\"|" /etc/default/grub
                
                # Verify the update was successful
                echo "Updated GRUB_CMDLINE_LINUX_DEFAULT to: \"$updated_value\""
            fi
        else
            # Add the line if it doesn't exist
            echo 'GRUB_CMDLINE_LINUX_DEFAULT="quiet splash"' | sudo tee -a /etc/default/grub
        fi
        
        # Enable graphical mode for GRUB - detect optimal resolution
        echo "Detecting optimal display resolution for GRUB..."
        
        # Install hwinfo if not already installed
        if ! command -v hwinfo >/dev/null 2>&1; then
            sudo apt update
            sudo apt install -y hwinfo
        fi
        
        # Try to detect optimal resolution using hwinfo
        OPTIMAL_RES=$(sudo hwinfo --monitor | grep -oP 'Mode:\s+\K[0-9]+x[0-9]+' | sort -nr | head -1)
        
        # If we couldn't detect resolution with hwinfo, try with xrandr
        if [ -z "$OPTIMAL_RES" ] && command -v xrandr >/dev/null 2>&1; then
            OPTIMAL_RES=$(xrandr | grep -oP 'current\s+\K[0-9]+\s+x\s+[0-9]+')
        fi
        
        # If we still couldn't detect, try with xdpyinfo
        if [ -z "$OPTIMAL_RES" ]; then
            if ! command -v xdpyinfo >/dev/null 2>&1; then
                sudo apt update
                sudo apt install -y xdpyinfo
            fi
            if command -v xdpyinfo >/dev/null 2>&1; then
                OPTIMAL_RES=$(xdpyinfo | grep -oP 'dimensions:\s+\K[0-9]+x[0-9]+' | awk -Fx '{print $1 " " $2}' | sort -k1,1nr -k2,2nr | awk '{print $1 "x" $2}' | head -1)
            fi
        fi
        
        # Fallback to safe resolution if we couldn't detect
        if [ -z "$OPTIMAL_RES" ]; then
            echo "Could not detect display resolution, using safe default 1024x768"
            OPTIMAL_RES="1024x768"
        else
            echo "Detected optimal resolution: $OPTIMAL_RES"
        fi
        
        # Configure GRUB with the detected resolution
        if grep -q "^#GRUB_GFXMODE=" /etc/default/grub; then
            # Uncomment GRUB_GFXMODE and set to optimal resolution if commented
            sudo sed -i "s|^#GRUB_GFXMODE=.*|GRUB_GFXMODE=\"$OPTIMAL_RES\"|" /etc/default/grub
        elif grep -q "^GRUB_GFXMODE=" /etc/default/grub; then
            # Update existing GRUB_GFXMODE setting
            sudo sed -i "s|^GRUB_GFXMODE=.*|GRUB_GFXMODE=\"$OPTIMAL_RES\"|" /etc/default/grub
        else
            # Add GRUB_GFXMODE if it doesn't exist
            echo "GRUB_GFXMODE=\"$OPTIMAL_RES\"" | sudo tee -a /etc/default/grub
        fi
        
        # Verify GRUB configuration before updating
        echo "Verifying GRUB configuration..."
        if ! sudo grep -v "^#" /etc/default/grub | grep -q "x:" && grep -q "GRUB_CMDLINE_LINUX_DEFAULT" /etc/default/grub; then
            echo "GRUB configuration looks valid, updating GRUB..."
            # Update GRUB
            sudo update-grub
        else
            echo "WARNING: GRUB configuration may have issues. Skipping update-grub."
            echo "You may need to manually edit /etc/default/grub and run 'sudo update-grub'"
        fi
    fi
    
    # Configure Plymouth with standard settings
    if [ -d /etc/plymouth ]; then
        echo "Configuring Plymouth with standard settings..."
        # Modify or create plymouthd.conf 
        if [ -f /etc/plymouth/plymouthd.conf ]; then
            # Update existing file
            if grep -q "^\[Daemon\]" /etc/plymouth/plymouthd.conf; then
                # If [Daemon] section exists, ensure Theme is set to pixels
                if grep -q "^Theme" /etc/plymouth/plymouthd.conf; then
                    sudo sed -i 's/^Theme=.*/Theme=pixels/' /etc/plymouth/plymouthd.conf
                else
                    sudo sed -i '/^\[Daemon\]/a Theme=pixels' /etc/plymouth/plymouthd.conf
                fi
                
                # Set DeviceTimeout to default
                if grep -q "^DeviceTimeout" /etc/plymouth/plymouthd.conf; then
                    sudo sed -i 's/^DeviceTimeout=.*/DeviceTimeout=5/' /etc/plymouth/plymouthd.conf
                else
                    sudo sed -i '/^\[Daemon\]/a DeviceTimeout=5' /etc/plymouth/plymouthd.conf
                fi
            else
                # If no [Daemon] section, add it with settings
                echo -e "[Daemon]\nTheme=pixels\nDeviceTimeout=5" | sudo tee /etc/plymouth/plymouthd.conf >/dev/null
            fi
        else
            # Create new config file
            echo -e "[Daemon]\nTheme=pixels\nDeviceTimeout=5" | sudo tee /etc/plymouth/plymouthd.conf >/dev/null
        fi
    else
        sudo mkdir -p /etc/plymouth
        echo -e "[Daemon]\nTheme=pixels\nDeviceTimeout=5" | sudo tee /etc/plymouth/plymouthd.conf >/dev/null
    fi
    
    # Apply changes to initramfs
    if command -v update-initramfs >/dev/null 2>&1; then
        echo "Updating initramfs..."
        sudo update-initramfs -u
    fi
    
    echo "Plymouth configuration completed."
else
    echo "Plymouth theme 'pixels' not found, skipping configuration."
fi

# Deploy wallpapers from repository if available
WALL_SRC="$(dirname "$0")/../wallpapers"
WALL_DEST="$HOME/Pictures/wallpapers"

echo "Setting up wallpapers..."
mkdir -p "$WALL_DEST"

if [ -d "$WALL_SRC" ]; then
    echo "Copying wallpapers from repository..."
    # Use find to safely copy only image files
    find "$WALL_SRC" -type f \( -name "*.png" -o -name "*.jpg" -o -name "*.jpeg" -o -name "*.jfif" \) -exec cp -f {} "$WALL_DEST" \; 2>/dev/null || true
    
    # Set a random wallpaper if XFCE is running
    if command -v xfconf-query >/dev/null 2>&1; then
        # Find a random wallpaper from the directory
        if [ -n "$(ls -A "$WALL_DEST" 2>/dev/null)" ]; then
            RANDOM_WALLPAPER=$(find "$WALL_DEST" -type f | shuf -n 1)
            if [ -n "$RANDOM_WALLPAPER" ] && [ -f "$RANDOM_WALLPAPER" ]; then
                echo "Setting random wallpaper: $(basename "$RANDOM_WALLPAPER")"
                xfconf-query -c xfce4-desktop -p /backdrop/screen0/monitor0/workspace0/last-image -s "$RANDOM_WALLPAPER" 2>/dev/null || true
            else
                echo "No valid wallpapers found."
            fi
        else
            echo "No wallpapers found in $WALL_DEST."
        fi
    else
        echo "XFCE not running, skipping wallpaper setting."
    fi
else
    echo "Wallpapers directory not found at $WALL_SRC, skipping."
fi

# Configure lightdm for user list at login
if [ -f /etc/lightdm/lightdm.conf ]; then
    sudo sed -i 's/^#*greeter-hide-users=.*/greeter-hide-users=false/' /etc/lightdm/lightdm.conf
fi

# Create .face file with a default avatar if it doesn't exist
if [ ! -f "$HOME/.face" ] && command -v curl >/dev/null; then
    curl -fsSL "https://www.gravatar.com/avatar/?d=identicon" -o "$HOME/.face"
fi

# Conky configuration
echo "Setting up Conky configuration..."
mkdir -p "$HOME/.config/conky"

# Get screen resolution to scale Conky appropriately
SCREEN_HEIGHT=768
if command -v xrandr >/dev/null 2>&1; then
    SCREEN_HEIGHT=$(xrandr | grep -oP 'current\s+[0-9]+\s+x\s+\K[0-9]+' || echo 768)
elif command -v xdpyinfo >/dev/null 2>&1; then
    SCREEN_HEIGHT=$(xdpyinfo | grep -oP 'dimensions:\s+[0-9]+x\K[0-9]+' || echo 768)
fi

# Calculate reasonable Conky height (70% of screen height)
CONKY_HEIGHT=$(( SCREEN_HEIGHT * 70 / 100 ))
echo "Screen height: $SCREEN_HEIGHT px, Conky height: $CONKY_HEIGHT px"

# Create Conky dark theme configuration file (for light wallpapers)
cat > "$HOME/.config/conky/conky-dark.conf" << 'EOC'
conky.config = {
    alignment = 'top_right',
    background = true,
    border_width = 0,
    cpu_avg_samples = 4,
    default_color = 'white',
    default_outline_color = 'grey',
    default_shade_color = 'black',
    double_buffer = true,
    draw_borders = false,
    draw_graph_borders = true,
    draw_outline = false,
    draw_shades = false,
    extra_newline = false,
    font = 'Montserrat:size=10',
    gap_x = 30,
    gap_y = 10,
    minimum_height = CONKY_HEIGHT_VALUE,
    minimum_width = 320,
    maximum_width = 320,
    net_avg_samples = 2,
    no_buffers = true,
    out_to_console = false,
    out_to_ncurses = false,
    out_to_stderr = false,
    out_to_x = true,
    own_window = true,
    own_window_class = 'Conky',
    own_window_transparent = true,
    own_window_argb_visual = true,
    own_window_argb_value = 0,
    own_window_type = 'desktop',
    own_window_colour = '202020',
    own_window_hints = 'undecorated,below,sticky,skip_taskbar,skip_pager',
    show_graph_range = false,
    show_graph_scale = false,
    stippled_borders = 0,
    update_interval = 1.0,
    uppercase = false,
    use_spacer = 'none',
    use_xft = true,
    
    color1 = '#FF5555', -- Red
    color2 = '#F8F8F2', -- Light text
    color3 = '#FFB86C', -- Orange
    color4 = '#50FA7B', -- Green
    color5 = '#BD93F9', -- Purple
    color6 = '#8BE9FD', -- Cyan
    color7 = '#F1FA8C', -- Yellow
};

conky.text = [[
${alignc}${color6}${font Montserrat:bold:size=28}${time %H:%M}${font}${color}
${alignc}${color2}${font Montserrat:size=11}${time %A, %B %d, %Y}${font}${color}

${voffset 10}${color6}${font Montserrat:bold:size=12}SYSTEM${font}${color}
${voffset 8}${color2}${font Montserrat:light:size=10}${sysname} ${kernel} on ${machine}${font}
${color2}Uptime: ${uptime}${alignr}Host: ${nodename}${font}

${voffset 15}${color1}${hr 2}

${voffset 10}${color4}${font Montserrat:bold:size=12}PROCESSOR${font}${color}
${voffset 5}${color6}${font Montserrat:medium:size=10}CPU: ${cpu cpu0}% ${alignr}${freq_g} GHz${font}
${color4}${cpubar 5,320}
${voffset 5}${color2}Core 1: ${color3}${cpu cpu1}% ${alignr}${color7}${cpubar cpu1 5,180}
${color2}Core 2: ${color3}${cpu cpu2}% ${alignr}${color7}${cpubar cpu2 5,180}
${if_existing /sys/devices/system/cpu/cpu3}
${color2}Core 3: ${color3}${cpu cpu3}% ${alignr}${color7}${cpubar cpu3 5,180}
${endif}${if_existing /sys/devices/system/cpu/cpu4}
${color2}Core 4: ${color3}${cpu cpu4}% ${alignr}${color7}${cpubar cpu4 5,180}
${endif}

${voffset 10}${color4}${font Montserrat:bold:size=12}MEMORY${font}${color}
${voffset 5}${color6}${font Montserrat:medium:size=10}RAM: ${memperc}% ${alignr}${mem} / ${memmax}${font}
${color4}${membar 5,320}
${voffset 5}${color2}Swap: ${swapperc}% ${alignr}${swap} / ${swapmax}
${color4}${swapbar 5,320}

${voffset 10}${color1}${hr 2}

${voffset 10}${color4}${font Montserrat:bold:size=12}STORAGE${font}${color}
${voffset 5}${color2}Root: ${fs_used_perc /}% ${alignr}${fs_used /} / ${fs_size /}
${color7}${fs_bar 5,320 /}
${voffset 5}${color2}Free: ${fs_free /} ${alignr}Used: ${fs_used_perc /}%

${voffset 10}${color4}${font Montserrat:bold:size=12}PROCESSES${font}${color}
${voffset 5}${color2}Running: ${running_processes} ${alignr}Total: ${processes}

${color6}${font Montserrat:medium:size=10}Top CPU${alignr}Usage${font}
${color2}${top name 1}${alignr}${color3}${top cpu 1}%
${color2}${top name 2}${alignr}${color3}${top cpu 2}%
${color2}${top name 3}${alignr}${color3}${top cpu 3}%

${voffset 5}${color6}${font Montserrat:medium:size=10}Top Memory${alignr}Usage${font}
${color2}${top_mem name 1}${alignr}${color3}${top_mem mem 1}%
${color2}${top_mem name 2}${alignr}${color3}${top_mem mem 2}%
${color2}${top_mem name 3}${alignr}${color3}${top_mem mem 3}%

${voffset 10}${color1}${hr 2}

${voffset 10}${color4}${font Montserrat:bold:size=12}NETWORK${font}${color}
${if_existing /sys/class/net/eth0/operstate up}
${voffset 5}${color6}Ethernet: ${alignr}${addr eth0}
${color2}Down: ${color3}${downspeed eth0}/s ${alignr}${color2}Up: ${color3}${upspeed eth0}/s
${color4}${downspeedgraph eth0 25,150 50FA7B BD93F9} ${alignr}${color1}${upspeedgraph eth0 25,150 BD93F9 FF5555}
${endif}${if_existing /sys/class/net/wlan0/operstate up}
${voffset 5}${color6}Wi-Fi: ${wireless_essid wlan0} ${alignr}${addr wlan0}
${color2}Signal: ${wireless_link_qual_perc wlan0}% ${alignr}${color2}${wireless_bitrate wlan0}
${color2}Down: ${color3}${downspeed wlan0}/s ${alignr}${color2}Up: ${color3}${upspeed wlan0}/s
${color4}${downspeedgraph wlan0 25,150 50FA7B BD93F9} ${alignr}${color1}${upspeedgraph wlan0 25,150 BD93F9 FF5555}
${endif}
]];
EOC

# Replace placeholder with actual height value
sed -i "s/CONKY_HEIGHT_VALUE/$CONKY_HEIGHT/g" "$HOME/.config/conky/conky-dark.conf"

# Create Conky light theme configuration file (for dark wallpapers)
cat > "$HOME/.config/conky/conky-light.conf" << 'EOC'
conky.config = {
    alignment = 'top_right',
    background = true,
    border_width = 0,
    cpu_avg_samples = 4,
    default_color = 'black',
    default_outline_color = 'grey',
    default_shade_color = 'black',
    double_buffer = true,
    draw_borders = false,
    draw_graph_borders = true,
    draw_outline = false,
    draw_shades = false,
    extra_newline = false,
    font = 'Montserrat:size=10',
    gap_x = 30,
    gap_y = 10,
    minimum_height = CONKY_HEIGHT_VALUE,
    minimum_width = 320,
    maximum_width = 320,
    net_avg_samples = 2,
    no_buffers = true,
    out_to_console = false,
    out_to_ncurses = false,
    out_to_stderr = false,
    out_to_x = true,
    own_window = true,
    own_window_class = 'Conky',
    own_window_transparent = true,
    own_window_argb_visual = true,
    own_window_argb_value = 0,
    own_window_type = 'desktop',
    own_window_colour = 'F0F0F0',
    own_window_hints = 'undecorated,below,sticky,skip_taskbar,skip_pager',
    show_graph_range = false,
    show_graph_scale = false,
    stippled_borders = 0,
    update_interval = 1.0,
    uppercase = false,
    use_spacer = 'none',
    use_xft = true,
    
    color1 = '#E32636', -- Red
    color2 = '#2E3440', -- Dark text
    color3 = '#D08770', -- Orange
    color4 = '#3B7A57', -- Green
    color5 = '#5D478B', -- Purple
    color6 = '#1B6AA5', -- Blue
    color7 = '#AF8D13', -- Gold
};

conky.text = [[
${alignc}${color6}${font Montserrat:bold:size=28}${time %H:%M}${font}${color}
${alignc}${color2}${font Montserrat:size=11}${time %A, %B %d, %Y}${font}${color}

${voffset 10}${color6}${font Montserrat:bold:size=12}SYSTEM${font}${color}
${voffset 8}${color2}${font Montserrat:light:size=10}${sysname} ${kernel} on ${machine}${font}
${color2}Uptime: ${uptime}${alignr}Host: ${nodename}${font}

${voffset 15}${color1}${hr 2}

${voffset 10}${color4}${font Montserrat:bold:size=12}PROCESSOR${font}${color}
${voffset 5}${color6}${font Montserrat:medium:size=10}CPU: ${cpu cpu0}% ${alignr}${freq_g} GHz${font}
${color4}${cpubar 5,320}
${voffset 5}${color2}Core 1: ${color3}${cpu cpu1}% ${alignr}${color7}${cpubar cpu1 5,180}
${color2}Core 2: ${color3}${cpu cpu2}% ${alignr}${color7}${cpubar cpu2 5,180}
${if_existing /sys/devices/system/cpu/cpu3}
${color2}Core 3: ${color3}${cpu cpu3}% ${alignr}${color7}${cpubar cpu3 5,180}
${endif}${if_existing /sys/devices/system/cpu/cpu4}
${color2}Core 4: ${color3}${cpu cpu4}% ${alignr}${color7}${cpubar cpu4 5,180}
${endif}

${voffset 10}${color4}${font Montserrat:bold:size=12}MEMORY${font}${color}
${voffset 5}${color6}${font Montserrat:medium:size=10}RAM: ${memperc}% ${alignr}${mem} / ${memmax}${font}
${color4}${membar 5,320}
${voffset 5}${color2}Swap: ${swapperc}% ${alignr}${swap} / ${swapmax}
${color4}${swapbar 5,320}

${voffset 10}${color1}${hr 2}

${voffset 10}${color4}${font Montserrat:bold:size=12}STORAGE${font}${color}
${voffset 5}${color2}Root: ${fs_used_perc /}% ${alignr}${fs_used /} / ${fs_size /}
${color7}${fs_bar 5,320 /}
${voffset 5}${color2}Free: ${fs_free /} ${alignr}Used: ${fs_used_perc /}%

${voffset 10}${color4}${font Montserrat:bold:size=12}PROCESSES${font}${color}
${voffset 5}${color2}Running: ${running_processes} ${alignr}Total: ${processes}

${color6}${font Montserrat:medium:size=10}Top CPU${alignr}Usage${font}
${color2}${top name 1}${alignr}${color3}${top cpu 1}%
${color2}${top name 2}${alignr}${color3}${top cpu 2}%
${color2}${top name 3}${alignr}${color3}${top cpu 3}%

${voffset 5}${color6}${font Montserrat:medium:size=10}Top Memory${alignr}Usage${font}
${color2}${top_mem name 1}${alignr}${color3}${top_mem mem 1}%
${color2}${top_mem name 2}${alignr}${color3}${top_mem mem 2}%
${color2}${top_mem name 3}${alignr}${color3}${top_mem mem 3}%

${voffset 10}${color1}${hr 2}

${voffset 10}${color4}${font Montserrat:bold:size=12}NETWORK${font}${color}
${if_existing /sys/class/net/eth0/operstate up}
${voffset 5}${color6}Ethernet: ${alignr}${addr eth0}
${color2}Down: ${color3}${downspeed eth0}/s ${alignr}${color2}Up: ${color3}${upspeed eth0}/s
${color4}${downspeedgraph eth0 25,150 3B7A57 5D478B} ${alignr}${color1}${upspeedgraph eth0 25,150 5D478B E32636}
${endif}${if_existing /sys/class/net/wlan0/operstate up}
${voffset 5}${color6}Wi-Fi: ${wireless_essid wlan0} ${alignr}${addr wlan0}
${color2}Signal: ${wireless_link_qual_perc wlan0}% ${alignr}${color2}${wireless_bitrate wlan0}
${color2}Down: ${color3}${downspeed wlan0}/s ${alignr}${color2}Up: ${color3}${upspeed wlan0}/s
${color4}${downspeedgraph wlan0 25,150 3B7A57 5D478B} ${alignr}${color1}${upspeedgraph wlan0 25,150 5D478B E32636}
${endif}
]];
EOC

# Replace placeholder with actual height value
sed -i "s/CONKY_HEIGHT_VALUE/$CONKY_HEIGHT/g" "$HOME/.config/conky/conky-light.conf"

# Create a symlink to the current theme (default to dark)
ln -sf "$HOME/.config/conky/conky-dark.conf" "$HOME/.config/conky/conky.conf"

# Create a script to switch between themes
cat > "$HOME/.config/conky/switch-theme.sh" <<'EOSH'
#!/bin/bash

# Get current theme
CURRENT_THEME=""
if [ -L "$HOME/.config/conky/conky.conf" ]; then
    CURRENT_THEME=$(readlink "$HOME/.config/conky/conky.conf")
fi

# Toggle theme
if [[ "$CURRENT_THEME" == *"dark"* ]]; then
    echo "Switching to light theme (for dark wallpapers)..."
    ln -sf "$HOME/.config/conky/conky-light.conf" "$HOME/.config/conky/conky.conf"
    NEW_THEME="light"
else
    echo "Switching to dark theme (for light wallpapers)..."
    ln -sf "$HOME/.config/conky/conky-dark.conf" "$HOME/.config/conky/conky.conf"
    NEW_THEME="dark"
fi

# Restart Conky if it's running
pkill conky || true
sleep 1
conky -c "$HOME/.config/conky/conky.conf" &

echo "Conky theme switched to $NEW_THEME"
EOSH

# Make the theme switching script executable
chmod +x "$HOME/.config/conky/switch-theme.sh"

# Create Desktop entry for theme switching
cat > "$HOME/.local/share/applications/conky-switch-theme.desktop" <<'EOD'
[Desktop Entry]
Type=Application
Name=Switch Conky Theme
Comment=Toggle between light and dark Conky themes
Exec=bash -c '~/.config/conky/switch-theme.sh'
Icon=conky
Terminal=false
Categories=Utility;
EOD

# Create Conky autostart file
mkdir -p "$HOME/.config/autostart"
cat > "$HOME/.config/autostart/conky.desktop" << 'EOD'
[Desktop Entry]
Type=Application
Name=Conky
Exec=conky -c /home/USER_PLACEHOLDER/.config/conky/conky.conf
StartupNotify=false
Terminal=false
Icon=conky
Comment=Lightweight system monitor
Categories=System;Monitor;
X-GNOME-Autostart-enabled=true
EOD

# Replace placeholders with actual values
sed -i "s/USER_PLACEHOLDER/$USER/g" "$HOME/.config/autostart/conky.desktop"

# Create a readme file with usage instructions
cat > "$HOME/.config/conky/README.txt" <<'EOR'
CONKY THEME INFORMATION

Two themes are included:
1. Dark Theme - Best for light wallpapers (high contrast with dark background)
2. Light Theme - Best for dark wallpapers (high contrast with light background)

To switch between themes:
- Use the "Switch Conky Theme" application in your applications menu
- Or run: ~/.config/conky/switch-theme.sh from terminal

Both themes are automatically scaled to your screen resolution.

The configuration files are located at:
- ~/.config/conky/conky-dark.conf
- ~/.config/conky/conky-light.conf

You can customize these files to your liking.
EOR

# Restart Conky if it's running
pkill conky || true
sleep 1
conky -c "$HOME/.config/conky/conky.conf" &

# Inform the user about the Conky themes
echo "Conky configured with both light and dark themes."
echo "Use the 'Switch Conky Theme' application or run ~/.config/conky/switch-theme.sh to toggle."
echo "See ~/.config/conky/README.txt for more information."
