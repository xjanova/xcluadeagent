#!/bin/bash
# =============================================================================
# XcluadeAgent Update Script
# Version: 2.0.0
# Developed by xman studio | https://xman4289.com
#
# Usage: sudo ./scripts/update.sh
# =============================================================================

set -e

# Configuration
INSTALL_DIR="/opt/xcluadeagent"
SERVICE_NAME="xcluadeagent"
PUBLISH_DIR="$INSTALL_DIR/publish"
DEFAULT_PORT=5000
REPO_DIR="$(cd "$(dirname "$0")/.." && pwd)"

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m'
BOLD='\033[1m'

echo -e "${CYAN}"
echo "╔════════════════════════════════════════════════════════════════════╗"
echo "║         XcluadeAgent Update Script v2.0                            ║"
echo "║         Developed by xman studio | https://xman4289.com            ║"
echo "╚════════════════════════════════════════════════════════════════════╝"
echo -e "${NC}"

# Check if running as root
if [ "$EUID" -ne 0 ]; then
    echo -e "${RED}❌ ต้องรันด้วย root: sudo $0${NC}"
    exit 1
fi

# =============================================================================
# STEP 1: Pull latest changes
# =============================================================================
echo -e "\n${CYAN}[1/6]${NC} 📥 ดึงโค้ดล่าสุด..."
cd "$REPO_DIR"
git fetch origin 2>/dev/null || true
git pull origin main 2>/dev/null || git pull origin master 2>/dev/null || echo -e "${YELLOW}⚠️  Git pull ข้าม (อาจไม่มีการเปลี่ยนแปลง)${NC}"

# =============================================================================
# STEP 2: Build & Publish
# =============================================================================
echo -e "\n${CYAN}[2/6]${NC} 🔨 Build และ Publish..."

# Ensure publish directory exists
mkdir -p "$PUBLISH_DIR"

# Find the project file
PROJECT_FILE=""
if [ -f "$REPO_DIR/src/XcluadeAgent.Api/XcluadeAgent.Api.csproj" ]; then
    PROJECT_FILE="$REPO_DIR/src/XcluadeAgent.Api/XcluadeAgent.Api.csproj"
elif [ -f "$REPO_DIR/XcluadeAgent.Api.csproj" ]; then
    PROJECT_FILE="$REPO_DIR/XcluadeAgent.Api.csproj"
fi

if [ -n "$PROJECT_FILE" ]; then
    dotnet publish "$PROJECT_FILE" -c Release -o "$PUBLISH_DIR" --nologo -v q
    echo -e "${GREEN}✅ Publish สำเร็จ${NC}"
else
    echo -e "${RED}❌ ไม่พบ project file${NC}"
    exit 1
fi

# =============================================================================
# STEP 3: Update systemd service
# =============================================================================
echo -e "\n${CYAN}[3/6]${NC} ⚙️ ตรวจสอบ systemd service..."

SERVICE_FILE="/etc/systemd/system/${SERVICE_NAME}.service"

# Check if service file needs update
NEEDS_UPDATE=false

if [ ! -f "$SERVICE_FILE" ]; then
    NEEDS_UPDATE=true
elif ! grep -q "WorkingDirectory=$PUBLISH_DIR" "$SERVICE_FILE"; then
    NEEDS_UPDATE=true
fi

if [ "$NEEDS_UPDATE" = true ]; then
    echo -e "${YELLOW}🔧 อัพเดท service file...${NC}"

    cat > "$SERVICE_FILE" << EOF
[Unit]
Description=XcluadeAgent - GitHub Sync Dashboard
After=network.target

[Service]
Type=simple
User=root
WorkingDirectory=$PUBLISH_DIR
ExecStart=/usr/bin/dotnet $PUBLISH_DIR/XcluadeAgent.Api.dll
Restart=on-failure
RestartSec=10
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://0.0.0.0:$DEFAULT_PORT

[Install]
WantedBy=multi-user.target
EOF

    systemctl daemon-reload
    systemctl enable "$SERVICE_NAME" 2>/dev/null || true
    echo -e "${GREEN}✅ Service file อัพเดทแล้ว${NC}"
else
    echo -e "${GREEN}✅ Service file ถูกต้องแล้ว${NC}"
fi

# =============================================================================
# STEP 4: Stop service
# =============================================================================
echo -e "\n${CYAN}[4/6]${NC} ⏹️ หยุด service..."
systemctl stop "$SERVICE_NAME" 2>/dev/null || true

# Kill any zombie processes on port
fuser -k $DEFAULT_PORT/tcp 2>/dev/null || true
sleep 1

echo -e "${GREEN}✅ Service หยุดแล้ว${NC}"

# =============================================================================
# STEP 5: Start service
# =============================================================================
echo -e "\n${CYAN}[5/6]${NC} ▶️ เริ่ม service..."
systemctl start "$SERVICE_NAME"
sleep 3

if systemctl is-active --quiet "$SERVICE_NAME"; then
    echo -e "${GREEN}✅ Service เริ่มทำงานแล้ว${NC}"
else
    echo -e "${RED}❌ Service ไม่สามารถเริ่มได้${NC}"
    echo -e "${YELLOW}ดู logs: journalctl -u $SERVICE_NAME -n 30${NC}"
    exit 1
fi

# =============================================================================
# STEP 6: Verify & Open Firewall
# =============================================================================
echo -e "\n${CYAN}[6/6]${NC} 🔍 ตรวจสอบและเปิด Firewall..."

# Check CSF
if command -v csf &> /dev/null; then
    if ! csf -l 2>/dev/null | grep -q "$DEFAULT_PORT"; then
        echo -e "${YELLOW}🔧 เปิด port $DEFAULT_PORT ใน CSF...${NC}"
        echo "tcp|in|d=$DEFAULT_PORT|s=0.0.0.0/0" >> /etc/csf/csf.allow 2>/dev/null || true
        csf -r > /dev/null 2>&1 || true
        echo -e "${GREEN}✅ CSF firewall อัพเดทแล้ว${NC}"
    fi
fi

# Check UFW
if command -v ufw &> /dev/null; then
    if ufw status 2>/dev/null | grep -q "active"; then
        if ! ufw status | grep -q "$DEFAULT_PORT"; then
            echo -e "${YELLOW}🔧 เปิด port $DEFAULT_PORT ใน UFW...${NC}"
            ufw allow $DEFAULT_PORT/tcp > /dev/null 2>&1 || true
            echo -e "${GREEN}✅ UFW firewall อัพเดทแล้ว${NC}"
        fi
    fi
fi

# Verify health
echo -e "\n${CYAN}🏥 ตรวจสอบ health...${NC}"
sleep 2

HEALTH=$(curl -s "http://localhost:$DEFAULT_PORT/health" 2>/dev/null || echo "")
if echo "$HEALTH" | grep -q "healthy"; then
    echo -e "${GREEN}✅ Health check ผ่าน${NC}"
else
    echo -e "${YELLOW}⚠️ Health check ไม่ตอบกลับ - อาจต้องรอสักครู่${NC}"
fi

# =============================================================================
# SUMMARY
# =============================================================================
echo ""
echo -e "${GREEN}╔════════════════════════════════════════════════════════════════════╗${NC}"
echo -e "${GREEN}║              ✅ อัพเดทเสร็จสิ้น!                                    ║${NC}"
echo -e "${GREEN}╚════════════════════════════════════════════════════════════════════╝${NC}"
echo ""

SERVER_IP=$(hostname -I | awk '{print $1}')
echo -e "  ${BOLD}📍 เข้าถึงได้ที่:${NC}"
echo -e "     URL: ${CYAN}http://$SERVER_IP:$DEFAULT_PORT${NC}"
echo ""
echo -e "  ${BOLD}🔐 Login:${NC}"
echo -e "     Username: ${CYAN}admin${NC}"
echo -e "     Password: ${CYAN}admin123${NC}"
echo ""
echo -e "  ${BOLD}🔧 คำสั่ง:${NC}"
echo -e "     Status:  ${CYAN}systemctl status $SERVICE_NAME${NC}"
echo -e "     Logs:    ${CYAN}journalctl -u $SERVICE_NAME -f${NC}"
echo -e "     Diagnose: ${CYAN}sudo $REPO_DIR/scripts/diagnose.sh --fix${NC}"
echo ""
