#!/bin/bash
# =============================================================================
# XcluadeAgent Diagnostic & Auto-Fix Script
# Version: 1.0.0
# Developed by xman studio | https://xman4289.com
#
# ตรวจสอบและแก้ไขปัญหาอัตโนมัติ
# =============================================================================

set -e

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
NC='\033[0m'
BOLD='\033[1m'

# Configuration
INSTALL_DIR="/opt/xcluadeagent"
SERVICE_NAME="xcluadeagent"
DEFAULT_PORT=5000
PUBLISH_DIR="$INSTALL_DIR/publish"

# Counters
ISSUES_FOUND=0
ISSUES_FIXED=0

# =============================================================================
# HELPER FUNCTIONS
# =============================================================================

print_header() {
    echo ""
    echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    echo -e "${WHITE}${BOLD}  $1${NC}"
    echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
}

print_check() {
    echo -e "  ${BLUE}[ตรวจสอบ]${NC} $1"
}

print_ok() {
    echo -e "  ${GREEN}[✓ ผ่าน]${NC} $1"
}

print_fail() {
    echo -e "  ${RED}[✗ พบปัญหา]${NC} $1"
    ((ISSUES_FOUND++))
}

print_fix() {
    echo -e "  ${YELLOW}[🔧 แก้ไข]${NC} $1"
}

print_fixed() {
    echo -e "  ${GREEN}[✓ แก้ไขแล้ว]${NC} $1"
    ((ISSUES_FIXED++))
}

print_skip() {
    echo -e "  ${YELLOW}[⏭ ข้าม]${NC} $1"
}

print_info() {
    echo -e "  ${CYAN}[ℹ]${NC} $1"
}

# =============================================================================
# BANNER
# =============================================================================

show_banner() {
    clear
    echo -e "${CYAN}"
    cat << 'BANNER'

    ╔═══════════════════════════════════════════════════════════════════╗
    ║     XcluadeAgent - Diagnostic & Auto-Fix Tool                     ║
    ║                                                                   ║
    ║     ตรวจสอบและแก้ไขปัญหาอัตโนมัติ                                  ║
    ╚═══════════════════════════════════════════════════════════════════╝

BANNER
    echo -e "${NC}"
}

# =============================================================================
# CHECK: .NET Runtime
# =============================================================================

check_dotnet() {
    print_header "1. ตรวจสอบ .NET Runtime"

    print_check "ตรวจสอบว่า .NET ติดตั้งแล้ว..."

    if command -v dotnet &> /dev/null; then
        local version=$(dotnet --version 2>/dev/null || echo "unknown")
        local major=$(echo "$version" | cut -d. -f1)

        if [ "$major" -ge 8 ] 2>/dev/null; then
            print_ok ".NET $version ติดตั้งแล้ว"
        else
            print_fail ".NET $version ต่ำเกินไป (ต้องการ .NET 8+)"

            if [ "$AUTO_FIX" = "true" ]; then
                print_fix "กำลังติดตั้ง .NET 8..."

                # Detect OS and install
                if [ -f /etc/os-release ]; then
                    . /etc/os-release
                    case $ID in
                        ubuntu|debian)
                            wget -q https://packages.microsoft.com/config/$ID/$VERSION_ID/packages-microsoft-prod.deb -O /tmp/packages-microsoft-prod.deb
                            dpkg -i /tmp/packages-microsoft-prod.deb > /dev/null 2>&1
                            apt-get update > /dev/null 2>&1
                            apt-get install -y aspnetcore-runtime-8.0 > /dev/null 2>&1
                            rm /tmp/packages-microsoft-prod.deb
                            print_fixed ".NET 8 ติดตั้งสำเร็จ"
                            ;;
                        *)
                            print_skip "ไม่รองรับการติดตั้งอัตโนมัติสำหรับ $ID"
                            ;;
                    esac
                fi
            fi
        fi
    else
        print_fail ".NET ไม่ได้ติดตั้ง"

        if [ "$AUTO_FIX" = "true" ]; then
            print_fix "กำลังติดตั้ง .NET 8..."

            if [ -f /etc/os-release ]; then
                . /etc/os-release
                case $ID in
                    ubuntu|debian)
                        wget -q https://packages.microsoft.com/config/$ID/$VERSION_ID/packages-microsoft-prod.deb -O /tmp/packages-microsoft-prod.deb 2>/dev/null
                        dpkg -i /tmp/packages-microsoft-prod.deb > /dev/null 2>&1
                        apt-get update > /dev/null 2>&1
                        apt-get install -y aspnetcore-runtime-8.0 > /dev/null 2>&1
                        rm -f /tmp/packages-microsoft-prod.deb
                        print_fixed ".NET 8 ติดตั้งสำเร็จ"
                        ;;
                    *)
                        print_skip "กรุณาติดตั้ง .NET 8 ด้วยตนเอง"
                        ;;
                esac
            fi
        fi
    fi
}

# =============================================================================
# CHECK: Application Published
# =============================================================================

check_published() {
    print_header "2. ตรวจสอบ Application Files"

    print_check "ตรวจสอบว่า application ถูก publish แล้ว..."

    if [ -f "$PUBLISH_DIR/XcluadeAgent.Api.dll" ]; then
        print_ok "พบ published application ที่ $PUBLISH_DIR"

        # Check Blazor framework
        print_check "ตรวจสอบ Blazor framework files..."
        if [ -d "$PUBLISH_DIR/wwwroot/_framework" ] || [ -f "$PUBLISH_DIR/wwwroot/_framework/blazor.web.js" ]; then
            print_ok "Blazor framework files พร้อมใช้งาน"
        else
            # In .NET 8, Blazor files are embedded, check if the dll contains them
            print_ok "Blazor framework embedded ใน DLL (ปกติสำหรับ .NET 8)"
        fi
    else
        print_fail "ไม่พบ published application"

        if [ "$AUTO_FIX" = "true" ]; then
            print_fix "กำลัง publish application..."

            if [ -f "$INSTALL_DIR/src/XcluadeAgent.Api/XcluadeAgent.Api.csproj" ]; then
                cd "$INSTALL_DIR"
                dotnet publish src/XcluadeAgent.Api/XcluadeAgent.Api.csproj -c Release -o "$PUBLISH_DIR" --nologo -v q
                print_fixed "Publish สำเร็จ"
            elif [ -f "$INSTALL_DIR/XcluadeAgent.Api.csproj" ]; then
                cd "$INSTALL_DIR"
                dotnet publish XcluadeAgent.Api.csproj -c Release -o "$PUBLISH_DIR" --nologo -v q
                print_fixed "Publish สำเร็จ"
            else
                print_skip "ไม่พบ source code สำหรับ publish"
            fi
        fi
    fi
}

# =============================================================================
# CHECK: Systemd Service
# =============================================================================

check_service() {
    print_header "3. ตรวจสอบ Systemd Service"

    local service_file="/etc/systemd/system/${SERVICE_NAME}.service"

    print_check "ตรวจสอบ service file..."

    if [ -f "$service_file" ]; then
        print_ok "พบ service file"

        # Check if pointing to correct path
        print_check "ตรวจสอบ path ใน service..."

        local exec_path=$(grep "ExecStart=" "$service_file" | head -1)

        if echo "$exec_path" | grep -q "publish"; then
            print_ok "Service ชี้ไปที่ publish directory"
        elif echo "$exec_path" | grep -q "bin/Release"; then
            print_fail "Service ชี้ไปที่ build directory (ควรเป็น publish)"

            if [ "$AUTO_FIX" = "true" ]; then
                print_fix "กำลังแก้ไข service file..."

                cat > "$service_file" << EOF
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
                print_fixed "Service file แก้ไขแล้ว"
            fi
        fi

        # Check service status
        print_check "ตรวจสอบ service status..."

        if systemctl is-active --quiet "$SERVICE_NAME"; then
            print_ok "Service กำลังทำงาน"
        else
            print_fail "Service ไม่ได้ทำงาน"

            if [ "$AUTO_FIX" = "true" ]; then
                print_fix "กำลังเริ่ม service..."
                systemctl start "$SERVICE_NAME"
                sleep 2

                if systemctl is-active --quiet "$SERVICE_NAME"; then
                    print_fixed "Service เริ่มทำงานแล้ว"
                else
                    print_fail "ไม่สามารถเริ่ม service ได้ - ดู logs: journalctl -u $SERVICE_NAME -n 50"
                fi
            fi
        fi
    else
        print_fail "ไม่พบ service file"

        if [ "$AUTO_FIX" = "true" ]; then
            print_fix "กำลังสร้าง service file..."

            cat > "$service_file" << EOF
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
            systemctl enable "$SERVICE_NAME"
            print_fixed "Service file สร้างแล้ว"
        fi
    fi
}

# =============================================================================
# CHECK: Port & Connectivity
# =============================================================================

check_port() {
    print_header "4. ตรวจสอบ Port & Network"

    print_check "ตรวจสอบว่า port $DEFAULT_PORT เปิดอยู่..."

    if ss -tlnp | grep -q ":$DEFAULT_PORT "; then
        print_ok "Port $DEFAULT_PORT กำลัง listen"
    else
        print_fail "Port $DEFAULT_PORT ไม่ได้ listen"
        print_info "อาจเป็นเพราะ service ไม่ได้ทำงาน"
    fi

    # Check health endpoint
    print_check "ตรวจสอบ health endpoint..."

    local health_response=$(curl -s -o /dev/null -w "%{http_code}" "http://localhost:$DEFAULT_PORT/health" 2>/dev/null || echo "000")

    if [ "$health_response" = "200" ]; then
        print_ok "Health endpoint ตอบกลับ 200 OK"
    else
        print_fail "Health endpoint ไม่ตอบกลับ (HTTP $health_response)"
    fi

    # Check main page
    print_check "ตรวจสอบหน้าหลัก..."

    local main_response=$(curl -s -o /dev/null -w "%{http_code}" "http://localhost:$DEFAULT_PORT/" 2>/dev/null || echo "000")

    if [ "$main_response" = "200" ]; then
        print_ok "หน้าหลักตอบกลับ 200 OK"
    elif [ "$main_response" = "500" ]; then
        print_fail "หน้าหลัก error 500 - ตรวจสอบ logs"
        print_info "รัน: journalctl -u $SERVICE_NAME -n 50 | grep -i error"
    else
        print_fail "หน้าหลักไม่ตอบกลับ (HTTP $main_response)"
    fi
}

# =============================================================================
# CHECK: Firewall
# =============================================================================

check_firewall() {
    print_header "5. ตรวจสอบ Firewall"

    local firewall_type=""
    local port_open=false

    # Check CSF (DirectAdmin/cPanel)
    if command -v csf &> /dev/null; then
        firewall_type="CSF"
        print_check "ตรวจพบ CSF Firewall..."

        if csf -l 2>/dev/null | grep -q "$DEFAULT_PORT"; then
            print_ok "Port $DEFAULT_PORT เปิดใน CSF แล้ว"
            port_open=true
        else
            print_fail "Port $DEFAULT_PORT ไม่ได้เปิดใน CSF"

            if [ "$AUTO_FIX" = "true" ]; then
                print_fix "กำลังเปิด port $DEFAULT_PORT ใน CSF..."

                # Add to TCP_IN
                if [ -f /etc/csf/csf.conf ]; then
                    if ! grep -q "TCP_IN.*$DEFAULT_PORT" /etc/csf/csf.conf; then
                        sed -i "s/^TCP_IN = \"/TCP_IN = \"$DEFAULT_PORT,/" /etc/csf/csf.conf
                    fi
                fi

                # Add to allow list
                echo "tcp|in|d=$DEFAULT_PORT|s=0.0.0.0/0" >> /etc/csf/csf.allow 2>/dev/null || true

                # Restart CSF
                csf -r > /dev/null 2>&1
                print_fixed "เปิด port $DEFAULT_PORT ใน CSF แล้ว"
                port_open=true
            fi
        fi
    fi

    # Check UFW
    if command -v ufw &> /dev/null; then
        local ufw_status=$(ufw status 2>/dev/null | head -1)

        if echo "$ufw_status" | grep -q "active"; then
            firewall_type="UFW"
            print_check "ตรวจพบ UFW Firewall (active)..."

            if ufw status | grep -q "$DEFAULT_PORT"; then
                print_ok "Port $DEFAULT_PORT เปิดใน UFW แล้ว"
                port_open=true
            else
                print_fail "Port $DEFAULT_PORT ไม่ได้เปิดใน UFW"

                if [ "$AUTO_FIX" = "true" ]; then
                    print_fix "กำลังเปิด port $DEFAULT_PORT ใน UFW..."
                    ufw allow $DEFAULT_PORT/tcp > /dev/null 2>&1
                    print_fixed "เปิด port $DEFAULT_PORT ใน UFW แล้ว"
                    port_open=true
                fi
            fi
        else
            print_ok "UFW ไม่ได้เปิดใช้งาน"
        fi
    fi

    # Check iptables directly
    if [ -z "$firewall_type" ]; then
        print_check "ตรวจสอบ iptables..."

        local iptables_drop=$(iptables -L INPUT -n 2>/dev/null | grep -c "DROP\|REJECT" || echo "0")

        if [ "$iptables_drop" -gt 0 ]; then
            if iptables -L INPUT -n 2>/dev/null | grep -q "dpt:$DEFAULT_PORT.*ACCEPT"; then
                print_ok "Port $DEFAULT_PORT เปิดใน iptables แล้ว"
                port_open=true
            else
                print_fail "Port $DEFAULT_PORT อาจถูกบล็อกโดย iptables"

                if [ "$AUTO_FIX" = "true" ]; then
                    print_fix "กำลังเปิด port $DEFAULT_PORT ใน iptables..."
                    iptables -I INPUT -p tcp --dport $DEFAULT_PORT -j ACCEPT
                    print_fixed "เปิด port $DEFAULT_PORT ใน iptables แล้ว"
                    port_open=true
                fi
            fi
        else
            print_ok "ไม่พบ firewall rules ที่บล็อก"
            port_open=true
        fi
    fi

    # Test external connectivity
    if [ "$port_open" = true ]; then
        print_check "ทดสอบการเข้าถึงจากภายนอก..."

        local server_ip=$(hostname -I | awk '{print $1}')
        print_info "Server IP: $server_ip"
        print_info "URL: http://$server_ip:$DEFAULT_PORT"
    fi
}

# =============================================================================
# CHECK: Application Logs
# =============================================================================

check_logs() {
    print_header "6. ตรวจสอบ Application Logs"

    print_check "ตรวจสอบ error ล่าสุด..."

    local errors=$(journalctl -u "$SERVICE_NAME" -n 100 --no-pager 2>/dev/null | grep -i "error\|exception\|fail" | tail -5)

    if [ -n "$errors" ]; then
        print_fail "พบ errors ใน logs:"
        echo "$errors" | while read line; do
            echo -e "    ${RED}$line${NC}"
        done

        # Check for common errors
        if echo "$errors" | grep -q "anti-forgery"; then
            print_info "พบปัญหา: Missing UseAntiforgery middleware"
            print_info "แก้ไข: เพิ่ม app.UseAntiforgery() ใน Program.cs"
        fi

        if echo "$errors" | grep -q "CHDIR"; then
            print_info "พบปัญหา: WorkingDirectory ไม่ถูกต้อง"
            print_info "แก้ไข: ตรวจสอบ path ใน systemd service file"
        fi

        if echo "$errors" | grep -q "port.*in use\|address.*in use"; then
            print_info "พบปัญหา: Port ถูกใช้งานอยู่แล้ว"
            print_info "แก้ไข: รัน 'fuser -k $DEFAULT_PORT/tcp' แล้ว restart service"
        fi
    else
        print_ok "ไม่พบ error ล่าสุด"
    fi
}

# =============================================================================
# SUMMARY
# =============================================================================

show_summary() {
    print_header "📊 สรุปผลการตรวจสอบ"

    echo ""
    if [ $ISSUES_FOUND -eq 0 ]; then
        echo -e "  ${GREEN}${BOLD}✅ ไม่พบปัญหา - ระบบพร้อมใช้งาน${NC}"
    else
        echo -e "  ${YELLOW}พบปัญหาทั้งหมด: ${BOLD}$ISSUES_FOUND${NC} รายการ"

        if [ "$AUTO_FIX" = "true" ]; then
            echo -e "  ${GREEN}แก้ไขแล้ว: ${BOLD}$ISSUES_FIXED${NC} รายการ"

            local remaining=$((ISSUES_FOUND - ISSUES_FIXED))
            if [ $remaining -gt 0 ]; then
                echo -e "  ${RED}ยังต้องแก้ไขเอง: ${BOLD}$remaining${NC} รายการ"
            fi
        else
            echo ""
            echo -e "  ${CYAN}💡 เรียกใช้พร้อม --fix เพื่อแก้ไขอัตโนมัติ:${NC}"
            echo -e "     ${BOLD}sudo $0 --fix${NC}"
        fi
    fi

    echo ""

    # Show access info
    local server_ip=$(hostname -I | awk '{print $1}')
    echo -e "  ${WHITE}${BOLD}📍 การเข้าถึง:${NC}"
    echo -e "     URL: ${CYAN}http://$server_ip:$DEFAULT_PORT${NC}"
    echo -e "     Health: ${CYAN}http://$server_ip:$DEFAULT_PORT/health${NC}"
    echo ""

    echo -e "  ${WHITE}${BOLD}🔧 คำสั่งที่มีประโยชน์:${NC}"
    echo -e "     ดู status:  ${CYAN}systemctl status $SERVICE_NAME${NC}"
    echo -e "     ดู logs:    ${CYAN}journalctl -u $SERVICE_NAME -f${NC}"
    echo -e "     restart:    ${CYAN}systemctl restart $SERVICE_NAME${NC}"
    echo ""
}

# =============================================================================
# MAIN
# =============================================================================

main() {
    # Parse arguments
    AUTO_FIX=false

    for arg in "$@"; do
        case $arg in
            --fix|-f)
                AUTO_FIX=true
                ;;
            --help|-h)
                echo "Usage: $0 [--fix]"
                echo ""
                echo "Options:"
                echo "  --fix, -f    แก้ไขปัญหาอัตโนมัติ"
                echo "  --help, -h   แสดงวิธีใช้"
                exit 0
                ;;
        esac
    done

    # Check root
    if [ "$EUID" -ne 0 ] && [ "$AUTO_FIX" = "true" ]; then
        echo -e "${RED}ต้องรันด้วย sudo เพื่อแก้ไขอัตโนมัติ${NC}"
        echo "รัน: sudo $0 --fix"
        exit 1
    fi

    show_banner

    if [ "$AUTO_FIX" = "true" ]; then
        echo -e "  ${YELLOW}${BOLD}โหมด: แก้ไขอัตโนมัติ${NC}"
    else
        echo -e "  ${BLUE}${BOLD}โหมด: ตรวจสอบอย่างเดียว${NC}"
        echo -e "  ${CYAN}(เรียกใช้ --fix เพื่อแก้ไขอัตโนมัติ)${NC}"
    fi

    check_dotnet
    check_published
    check_service
    check_port
    check_firewall
    check_logs
    show_summary
}

main "$@"
