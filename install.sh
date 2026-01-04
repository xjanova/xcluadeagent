#!/bin/bash
#
# XcluadeAgent - One-Click Installer
# Developed by xman studio | https://xman4289.com
#
# Usage: curl -fsSL https://raw.githubusercontent.com/xjanova/xcluadeagent/main/install.sh | sudo bash
#

set -e

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
NC='\033[0m'

# Variables
INSTALL_DIR="/opt/xcluadeagent"
DATA_DIR="/var/lib/xcluadeagent"
SERVICE_USER="xcluade"
REPO_URL="https://github.com/xjanova/xcluadeagent.git"

# Spinner function for long-running tasks
spinner() {
    local pid=$1
    local msg=$2
    local spin='⠋⠙⠹⠸⠼⠴⠦⠧⠇⠏'
    local i=0
    while kill -0 $pid 2>/dev/null; do
        printf "\r${YELLOW}${spin:$i:1}${NC} $msg"
        i=$(( (i+1) % ${#spin} ))
        sleep 0.1
    done
    wait $pid
    local status=$?
    printf "\r"
    if [ $status -eq 0 ]; then
        echo -e "${GREEN}✓${NC} $msg"
    else
        echo -e "${RED}✗${NC} $msg"
        exit 1
    fi
}

# Check root
if [[ $EUID -ne 0 ]]; then
    echo -e "${RED}Error: Run with sudo${NC}"
    echo "Usage: curl -fsSL https://raw.githubusercontent.com/xjanova/xcluadeagent/main/install.sh | sudo bash"
    exit 1
fi

echo -e "${CYAN}"
echo "╔═══════════════════════════════════════════╗"
echo "║       XcluadeAgent Quick Installer        ║"
echo "║         by xman studio                    ║"
echo "╚═══════════════════════════════════════════╝"
echo -e "${NC}"

# Step 1: Dependencies
echo -e "${BLUE}[1/6]${NC} Installing dependencies..."
apt-get update -qq &>/dev/null &
spinner $! "Updating package list..."
apt-get install -y -qq curl wget git unzip apt-transport-https software-properties-common &>/dev/null &
spinner $! "Installing required packages..."

# Step 2: .NET 8
echo -e "${BLUE}[2/6]${NC} Installing .NET 8..."
if ! command -v dotnet &> /dev/null || [[ ! $(dotnet --version 2>/dev/null) == 8.* ]]; then
    . /etc/os-release
    (wget -q "https://packages.microsoft.com/config/$ID/$VERSION_ID/packages-microsoft-prod.deb" -O /tmp/ms-prod.deb && \
     dpkg -i /tmp/ms-prod.deb &>/dev/null && rm /tmp/ms-prod.deb) &
    spinner $! "Adding Microsoft repository..."
    apt-get update -qq &>/dev/null &
    spinner $! "Updating package list..."
    apt-get install -y -qq dotnet-sdk-8.0 &>/dev/null &
    spinner $! "Installing .NET 8 SDK (this may take 2-5 minutes)..."
else
    echo -e "${GREEN}✓${NC} .NET 8 already installed"
fi

# Step 3: Create user & directories
echo -e "${BLUE}[3/6]${NC} Setting up directories..."
id -u $SERVICE_USER &>/dev/null || useradd -r -s /bin/false $SERVICE_USER
mkdir -p $DATA_DIR/logs $DATA_DIR/backups
chown -R $SERVICE_USER:$SERVICE_USER $DATA_DIR
echo -e "${GREEN}✓${NC} Directories ready"

# Step 4: Download
echo -e "${BLUE}[4/6]${NC} Downloading XcluadeAgent..."
rm -rf $INSTALL_DIR
git clone -q --depth 1 $REPO_URL $INSTALL_DIR &
spinner $! "Cloning repository..."

# Step 5: Build (as root, then fix permissions)
echo -e "${BLUE}[5/6]${NC} Building application..."
cd $INSTALL_DIR
dotnet restore &>/dev/null &
spinner $! "Restoring packages (this may take 2-5 minutes)..."
dotnet build -c Release --no-restore &>/dev/null &
spinner $! "Building project..."
chown -R $SERVICE_USER:$SERVICE_USER $INSTALL_DIR

# Step 6: Generate config & service
echo -e "${BLUE}[6/6]${NC} Configuring service..."

# Generate JWT secret
JWT_SECRET=$(openssl rand -base64 32)

# Create config
mkdir -p $INSTALL_DIR/config
cat > $INSTALL_DIR/config/config.yaml << EOF
server:
  host: 0.0.0.0
  port: 5000

database:
  provider: sqlite
  connectionString: Data Source=$DATA_DIR/xcluadeagent.db

security:
  jwtSecret: $JWT_SECRET
  jwtExpirationMinutes: 1440

logging:
  level: Information
  path: $DATA_DIR/logs
EOF

chown -R $SERVICE_USER:$SERVICE_USER $INSTALL_DIR/config

# Create systemd service
cat > /etc/systemd/system/xcluadeagent.service << EOF
[Unit]
Description=XcluadeAgent - GitHub Sync Dashboard
After=network.target

[Service]
Type=notify
User=$SERVICE_USER
Group=$SERVICE_USER
WorkingDirectory=$INSTALL_DIR
ExecStart=/usr/bin/dotnet $INSTALL_DIR/src/XcluadeAgent.Api/bin/Release/net8.0/XcluadeAgent.Api.dll
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://0.0.0.0:5000
Restart=always
RestartSec=10

[Install]
WantedBy=multi-user.target
EOF

systemctl daemon-reload
systemctl enable xcluadeagent -q
systemctl start xcluadeagent

# Wait for startup
sleep 3

# Get IP
SERVER_IP=$(hostname -I | awk '{print $1}')

echo ""
echo -e "${GREEN}╔═══════════════════════════════════════════╗"
echo -e "║     ✓ Installation Complete!              ║"
echo -e "╚═══════════════════════════════════════════╝${NC}"
echo ""
echo -e "Dashboard: ${CYAN}http://${SERVER_IP}:5000${NC}"
echo ""
echo -e "Default Login:"
echo -e "  Username: ${YELLOW}admin${NC}"
echo -e "  Password: ${YELLOW}admin123${NC}"
echo ""
echo -e "Commands:"
echo -e "  ${GREEN}systemctl status xcluadeagent${NC}"
echo -e "  ${GREEN}journalctl -u xcluadeagent -f${NC}"
echo ""
echo -e "${YELLOW}⚠ Change admin password after first login!${NC}"
