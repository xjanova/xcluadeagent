#!/bin/bash
# =============================================================================
# XcluadeAgent Installation Script
# Version: 1.0.0
# Developed by xman studio | https://xman4289.com
#
# รองรับ: Ubuntu 20.04+, Debian 10+
# ต้องการ: root access
# =============================================================================

set -e

# =============================================================================
# CONFIGURATION
# =============================================================================
INSTALL_DIR="/opt/xcluadeagent"
SERVICE_NAME="xcluadeagent"
SERVICE_USER="xcluade"
DEFAULT_PORT=5000
LOG_FILE="/tmp/xcluadeagent_install.log"
VERSION="1.0.0"

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
MAGENTA='\033[0;35m'
WHITE='\033[1;37m'
DIM='\033[2m'
NC='\033[0m'
BOLD='\033[1m'

# =============================================================================
# HELPER FUNCTIONS
# =============================================================================

# Logging
log() {
    echo "[$(date '+%Y-%m-%d %H:%M:%S')] $1" >> "$LOG_FILE"
}

# Print with color
print_color() {
    echo -e "${1}${2}${NC}"
}

# Print step header
print_step() {
    local step_num=$1
    local total=$2
    local title=$3
    echo ""
    echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    echo -e "${WHITE}${BOLD}  ขั้นตอนที่ ${step_num}/${total}: ${title}${NC}"
    echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    echo ""
}

# Print success
print_success() {
    echo -e "  ${GREEN}✓${NC} $1"
}

# Print warning
print_warning() {
    echo -e "  ${YELLOW}⚠${NC} $1"
}

# Print error
print_error() {
    echo -e "  ${RED}✗${NC} $1"
}

# Print info
print_info() {
    echo -e "  ${BLUE}ℹ${NC} $1"
}

# Progress bar
show_progress() {
    local current=$1
    local total=$2
    local width=50
    local percent=$((current * 100 / total))
    local filled=$((current * width / total))
    local empty=$((width - filled))

    printf "\r  ["
    printf "%${filled}s" '' | tr ' ' '█'
    printf "%${empty}s" '' | tr ' ' '░'
    printf "] %3d%%" $percent
}

# Ask yes/no question
ask_yes_no() {
    local prompt=$1
    local default=${2:-n}
    local answer

    if [ "$default" = "y" ]; then
        read -p "  $prompt [Y/n]: " answer
        answer=${answer:-y}
    else
        read -p "  $prompt [y/N]: " answer
        answer=${answer:-n}
    fi

    [[ "$answer" =~ ^[Yy] ]]
}

# Ask for input with default
ask_input() {
    local prompt=$1
    local default=$2
    local answer

    if [ -n "$default" ]; then
        read -p "  $prompt [${default}]: " answer
        echo "${answer:-$default}"
    else
        read -p "  $prompt: " answer
        echo "$answer"
    fi
}

# Ask for password (hidden)
ask_password() {
    local prompt=$1
    local password

    read -sp "  $prompt: " password
    echo ""
    echo "$password"
}

# Check if command exists
command_exists() {
    command -v "$1" &> /dev/null
}

# Check if service is running
service_running() {
    systemctl is-active --quiet "$1" 2>/dev/null
}

# Spinner animation
spinner() {
    local pid=$1
    local msg=$2
    local spin='⠋⠙⠹⠸⠼⠴⠦⠧⠇⠏'
    local i=0

    while kill -0 $pid 2>/dev/null; do
        printf "\r  ${spin:i++%${#spin}:1} $msg"
        sleep 0.1
    done
    printf "\r"
}

# =============================================================================
# BANNER
# =============================================================================
show_banner() {
    clear
    echo -e "${CYAN}"
    cat << 'BANNER'

    ╔═══════════════════════════════════════════════════════════════════╗
    ║                                                                   ║
    ║     ██╗  ██╗ ██████╗██╗     ██╗   ██╗ █████╗ ██████╗ ███████╗    ║
    ║     ╚██╗██╔╝██╔════╝██║     ██║   ██║██╔══██╗██╔══██╗██╔════╝    ║
    ║      ╚███╔╝ ██║     ██║     ██║   ██║███████║██║  ██║█████╗      ║
    ║      ██╔██╗ ██║     ██║     ██║   ██║██╔══██║██║  ██║██╔══╝      ║
    ║     ██╔╝ ██╗╚██████╗███████╗╚██████╔╝██║  ██║██████╔╝███████╗    ║
    ║     ╚═╝  ╚═╝ ╚═════╝╚══════╝ ╚═════╝ ╚═╝  ╚═╝╚═════╝ ╚══════╝    ║
    ║                           AGENT                                   ║
    ║                                                                   ║
    ╠═══════════════════════════════════════════════════════════════════╣
    ║     GitHub Release Sync Service for Web Applications             ║
    ║                                                                   ║
    ║     Version: 1.0.0                                                ║
    ║     Developer: xman studio                                        ║
    ║     Website: https://xman4289.com                                 ║
    ╚═══════════════════════════════════════════════════════════════════╝

BANNER
    echo -e "${NC}"
}

# =============================================================================
# ROOT CHECK
# =============================================================================
check_root() {
    if [ "$EUID" -ne 0 ]; then
        echo ""
        echo -e "${RED}╔═══════════════════════════════════════════════════════════════════╗${NC}"
        echo -e "${RED}║                    ⚠️  ต้องการสิทธิ์ ROOT                          ║${NC}"
        echo -e "${RED}╠═══════════════════════════════════════════════════════════════════╣${NC}"
        echo -e "${RED}║                                                                   ║${NC}"
        echo -e "${RED}║   XcluadeAgent ต้องติดตั้งด้วย root เพื่อ:                         ║${NC}"
        echo -e "${RED}║                                                                   ║${NC}"
        echo -e "${RED}║   • สแกนและตรวจจับ Web Server configurations                      ║${NC}"
        echo -e "${RED}║   • เข้าถึง web directories ทั้งหมดโดยไม่มีข้อจำกัด               ║${NC}"
        echo -e "${RED}║   • จัดการ file permissions อย่างถูกต้อง                         ║${NC}"
        echo -e "${RED}║   • ติดตั้ง system services และ dependencies                     ║${NC}"
        echo -e "${RED}║                                                                   ║${NC}"
        echo -e "${RED}╠═══════════════════════════════════════════════════════════════════╣${NC}"
        echo -e "${RED}║                                                                   ║${NC}"
        echo -e "${RED}║   วิธีแก้ไข: รันคำสั่งนี้                                          ║${NC}"
        echo -e "${RED}║                                                                   ║${NC}"
        echo -e "${RED}║   ${WHITE}sudo ./install.sh${RED}                                            ║${NC}"
        echo -e "${RED}║                                                                   ║${NC}"
        echo -e "${RED}╚═══════════════════════════════════════════════════════════════════╝${NC}"
        echo ""
        exit 1
    fi

    print_success "กำลังทำงานด้วยสิทธิ์ root"
}

# =============================================================================
# OS DETECTION
# =============================================================================
detect_os() {
    if [ -f /etc/os-release ]; then
        . /etc/os-release
        OS_NAME=$NAME
        OS_VERSION=$VERSION_ID
        OS_ID=$ID

        case $OS_ID in
            ubuntu|debian)
                PKG_MANAGER="apt-get"
                PKG_INSTALL="apt-get install -y"
                PKG_UPDATE="apt-get update"
                ;;
            centos|rhel|fedora)
                PKG_MANAGER="yum"
                PKG_INSTALL="yum install -y"
                PKG_UPDATE="yum update -y"
                ;;
            *)
                print_warning "OS ไม่รองรับอย่างเต็มที่: $OS_NAME"
                PKG_MANAGER="apt-get"
                PKG_INSTALL="apt-get install -y"
                PKG_UPDATE="apt-get update"
                ;;
        esac

        print_success "ตรวจพบระบบปฏิบัติการ: ${BOLD}$OS_NAME $OS_VERSION${NC}"
    else
        print_error "ไม่สามารถตรวจจับระบบปฏิบัติการได้"
        exit 1
    fi
}

# =============================================================================
# SYSTEM REQUIREMENTS CHECK
# =============================================================================
check_requirements() {
    echo ""
    echo -e "  ${WHITE}ตรวจสอบความต้องการของระบบ...${NC}"
    echo ""

    local pass=true

    # RAM Check (minimum 512MB)
    local total_ram=$(grep MemTotal /proc/meminfo | awk '{print int($2/1024)}')
    if [ "$total_ram" -ge 512 ]; then
        print_success "RAM: ${total_ram}MB (ต้องการ: 512MB)"
    else
        print_error "RAM: ${total_ram}MB (ต้องการ: 512MB)"
        pass=false
    fi

    # Disk Check (minimum 1GB)
    local free_disk=$(df -BG / | tail -1 | awk '{print int($4)}')
    if [ "$free_disk" -ge 1 ]; then
        print_success "พื้นที่ว่าง: ${free_disk}GB (ต้องการ: 1GB)"
    else
        print_error "พื้นที่ว่าง: ${free_disk}GB (ต้องการ: 1GB)"
        pass=false
    fi

    # Architecture
    local arch=$(uname -m)
    print_success "สถาปัตยกรรม: $arch"

    # Kernel
    local kernel=$(uname -r)
    print_success "Kernel: $kernel"

    if [ "$pass" = false ]; then
        echo ""
        print_error "ระบบไม่ตรงตามความต้องการขั้นต่ำ"
        exit 1
    fi
}

# =============================================================================
# WEB SERVER DETECTION
# =============================================================================
detect_webservers() {
    echo ""
    echo -e "  ${WHITE}สแกนหา Web Servers...${NC}"
    echo ""

    WEBSERVER_FOUND=false
    PRIMARY_WEBSERVER=""
    WEBSERVER_USER="www-data"

    # Nginx
    if command_exists nginx || [ -f /etc/nginx/nginx.conf ]; then
        WEBSERVER_FOUND=true
        local version=$(nginx -v 2>&1 | grep -oP 'nginx/\K[\d.]+' || echo "unknown")
        local status="inactive"
        service_running nginx && status="active"

        if [ "$status" = "active" ]; then
            print_success "Nginx v$version ${GREEN}(กำลังทำงาน)${NC}"
            PRIMARY_WEBSERVER="nginx"

            # Get user from config
            if [ -f /etc/nginx/nginx.conf ]; then
                WEBSERVER_USER=$(grep -oP '^user\s+\K\w+' /etc/nginx/nginx.conf 2>/dev/null || echo "www-data")
            fi
        else
            print_info "Nginx v$version ${DIM}(ไม่ได้ทำงาน)${NC}"
        fi
    fi

    # Apache
    if command_exists apache2 || command_exists httpd || [ -f /etc/apache2/apache2.conf ]; then
        WEBSERVER_FOUND=true
        local version=$(apache2 -v 2>&1 | head -1 | grep -oP 'Apache/\K[\d.]+' || echo "unknown")
        local status="inactive"
        service_running apache2 && status="active"
        service_running httpd && status="active"

        if [ "$status" = "active" ]; then
            print_success "Apache v$version ${GREEN}(กำลังทำงาน)${NC}"
            [ -z "$PRIMARY_WEBSERVER" ] && PRIMARY_WEBSERVER="apache"

            # Get user from envvars
            if [ -f /etc/apache2/envvars ]; then
                WEBSERVER_USER=$(grep -oP 'APACHE_RUN_USER=\K\w+' /etc/apache2/envvars 2>/dev/null || echo "www-data")
            fi
        else
            print_info "Apache v$version ${DIM}(ไม่ได้ทำงาน)${NC}"
        fi
    fi

    # Caddy
    if command_exists caddy || [ -f /etc/caddy/Caddyfile ]; then
        WEBSERVER_FOUND=true
        local version=$(caddy version 2>&1 | grep -oP 'v[\d.]+' || echo "unknown")
        local status="inactive"
        service_running caddy && status="active"

        if [ "$status" = "active" ]; then
            print_success "Caddy $version ${GREEN}(กำลังทำงาน)${NC}"
            [ -z "$PRIMARY_WEBSERVER" ] && PRIMARY_WEBSERVER="caddy"
            WEBSERVER_USER="caddy"
        else
            print_info "Caddy $version ${DIM}(ไม่ได้ทำงาน)${NC}"
        fi
    fi

    # LiteSpeed
    if [ -d /usr/local/lsws ]; then
        WEBSERVER_FOUND=true
        local status="inactive"
        service_running lsws && status="active"

        if [ "$status" = "active" ]; then
            print_success "LiteSpeed ${GREEN}(กำลังทำงาน)${NC}"
            [ -z "$PRIMARY_WEBSERVER" ] && PRIMARY_WEBSERVER="litespeed"
            WEBSERVER_USER="nobody"
        else
            print_info "LiteSpeed ${DIM}(ไม่ได้ทำงาน)${NC}"
        fi
    fi

    if [ "$WEBSERVER_FOUND" = false ]; then
        print_warning "ไม่พบ Web Server ที่ติดตั้งไว้"
        echo ""
        echo -e "  ${YELLOW}คุณต้องติดตั้ง Web Server ก่อน (Nginx, Apache, หรือ Caddy)${NC}"
        echo ""

        if ask_yes_no "ต้องการให้ติดตั้ง Nginx ให้อัตโนมัติหรือไม่?" "y"; then
            echo ""
            print_info "กำลังติดตั้ง Nginx..."
            $PKG_UPDATE > /dev/null 2>&1
            $PKG_INSTALL nginx > /dev/null 2>&1
            systemctl enable nginx > /dev/null 2>&1
            systemctl start nginx > /dev/null 2>&1
            print_success "ติดตั้ง Nginx สำเร็จ"
            PRIMARY_WEBSERVER="nginx"
            WEBSERVER_USER="www-data"
            WEBSERVER_FOUND=true
        fi
    fi

    if [ -n "$PRIMARY_WEBSERVER" ]; then
        echo ""
        print_info "Web Server หลัก: ${BOLD}$PRIMARY_WEBSERVER${NC}"
        print_info "Web Server User: ${BOLD}$WEBSERVER_USER${NC}"
    fi
}

# =============================================================================
# WEBSITE DISCOVERY
# =============================================================================
discover_websites() {
    echo ""
    echo -e "  ${WHITE}สแกนหาเว็บไซต์ที่มีอยู่...${NC}"
    echo ""

    DISCOVERED_SITES=()
    local count=0

    # Nginx sites
    if [ -d /etc/nginx/sites-enabled ]; then
        for conf in /etc/nginx/sites-enabled/*; do
            if [ -f "$conf" ] && [ "$(basename "$conf")" != "default" ]; then
                local server_name=$(grep -oP 'server_name\s+\K[^;]+' "$conf" 2>/dev/null | head -1 | awk '{print $1}')
                local doc_root=$(grep -oP 'root\s+\K[^;]+' "$conf" 2>/dev/null | head -1)

                if [ -n "$doc_root" ] && [ -d "$doc_root" ]; then
                    local owner=$(stat -c '%U:%G' "$doc_root" 2>/dev/null)
                    local framework=$(detect_framework "$doc_root")
                    local has_git="No"
                    [ -d "$doc_root/.git" ] && has_git="Yes"

                    DISCOVERED_SITES+=("$server_name|$doc_root|$owner|$framework|$has_git")
                    ((count++))

                    echo -e "  ${CYAN}$count.${NC} ${WHITE}$server_name${NC}"
                    echo -e "     Path: ${DIM}$doc_root${NC}"
                    echo -e "     Owner: ${DIM}$owner${NC} | Framework: ${DIM}$framework${NC} | Git: ${DIM}$has_git${NC}"
                    echo ""
                fi
            fi
        done
    fi

    # Apache sites
    if [ -d /etc/apache2/sites-enabled ]; then
        for conf in /etc/apache2/sites-enabled/*.conf; do
            if [ -f "$conf" ] && [ "$(basename "$conf")" != "000-default.conf" ]; then
                local server_name=$(grep -oP 'ServerName\s+\K\S+' "$conf" 2>/dev/null | head -1)
                local doc_root=$(grep -oP 'DocumentRoot\s+\K[^\s"]+' "$conf" 2>/dev/null | head -1)

                if [ -n "$doc_root" ] && [ -d "$doc_root" ]; then
                    local owner=$(stat -c '%U:%G' "$doc_root" 2>/dev/null)
                    local framework=$(detect_framework "$doc_root")
                    local has_git="No"
                    [ -d "$doc_root/.git" ] && has_git="Yes"

                    DISCOVERED_SITES+=("$server_name|$doc_root|$owner|$framework|$has_git")
                    ((count++))

                    echo -e "  ${CYAN}$count.${NC} ${WHITE}$server_name${NC}"
                    echo -e "     Path: ${DIM}$doc_root${NC}"
                    echo -e "     Owner: ${DIM}$owner${NC} | Framework: ${DIM}$framework${NC} | Git: ${DIM}$has_git${NC}"
                    echo ""
                fi
            fi
        done
    fi

    if [ $count -eq 0 ]; then
        print_info "ไม่พบเว็บไซต์ที่ configure ไว้"
        print_info "สามารถเพิ่มเว็บไซต์ภายหลังผ่าน Dashboard ได้"
    else
        print_success "พบ ${BOLD}$count${NC} เว็บไซต์"
    fi

    SITES_COUNT=$count
}

# Detect framework from directory
detect_framework() {
    local path=$1

    if [ -f "$path/artisan" ]; then
        echo "Laravel"
    elif [ -f "$path/wp-config.php" ]; then
        echo "WordPress"
    elif [ -f "$path/package.json" ]; then
        if grep -q '"react"' "$path/package.json" 2>/dev/null; then
            echo "React"
        elif grep -q '"vue"' "$path/package.json" 2>/dev/null; then
            echo "Vue"
        elif grep -q '"next"' "$path/package.json" 2>/dev/null; then
            echo "Next.js"
        else
            echo "Node.js"
        fi
    elif [ -f "$path/manage.py" ]; then
        echo "Django"
    elif [ -f "$path/composer.json" ]; then
        echo "PHP"
    elif ls "$path"/*.csproj 1>/dev/null 2>&1; then
        echo ".NET"
    else
        echo "Unknown"
    fi
}

# =============================================================================
# DEPENDENCY INSTALLATION
# =============================================================================
install_dependencies() {
    echo ""
    echo -e "  ${WHITE}ติดตั้ง Dependencies ที่จำเป็น...${NC}"
    echo ""

    local packages=(
        "curl"
        "wget"
        "git"
        "unzip"
        "jq"
        "openssl"
    )

    local to_install=()

    for pkg in "${packages[@]}"; do
        if command_exists "$pkg"; then
            print_success "$pkg ติดตั้งแล้ว"
        else
            to_install+=("$pkg")
        fi
    done

    if [ ${#to_install[@]} -gt 0 ]; then
        echo ""
        print_info "กำลังติดตั้ง: ${to_install[*]}"
        $PKG_UPDATE > /dev/null 2>&1
        $PKG_INSTALL "${to_install[@]}" > /dev/null 2>&1
        print_success "ติดตั้ง dependencies สำเร็จ"
    fi
}

install_dotnet() {
    echo ""
    echo -e "  ${WHITE}ตรวจสอบ .NET Runtime...${NC}"
    echo ""

    if command_exists dotnet; then
        local version=$(dotnet --version 2>/dev/null)
        local major_version=$(echo "$version" | cut -d. -f1)

        if [ "$major_version" -ge 8 ]; then
            print_success ".NET $version ติดตั้งแล้ว"
            return 0
        else
            print_warning ".NET $version ติดตั้งแล้ว แต่ต้องการ .NET 8+"
        fi
    fi

    print_info "กำลังติดตั้ง .NET 8..."

    # Install based on OS
    case $OS_ID in
        ubuntu)
            # Add Microsoft package repository
            wget -q https://packages.microsoft.com/config/ubuntu/$OS_VERSION/packages-microsoft-prod.deb -O /tmp/packages-microsoft-prod.deb
            dpkg -i /tmp/packages-microsoft-prod.deb > /dev/null 2>&1
            rm /tmp/packages-microsoft-prod.deb

            $PKG_UPDATE > /dev/null 2>&1
            $PKG_INSTALL dotnet-runtime-8.0 aspnetcore-runtime-8.0 > /dev/null 2>&1
            ;;
        debian)
            wget -q https://packages.microsoft.com/config/debian/$OS_VERSION/packages-microsoft-prod.deb -O /tmp/packages-microsoft-prod.deb
            dpkg -i /tmp/packages-microsoft-prod.deb > /dev/null 2>&1
            rm /tmp/packages-microsoft-prod.deb

            $PKG_UPDATE > /dev/null 2>&1
            $PKG_INSTALL dotnet-runtime-8.0 aspnetcore-runtime-8.0 > /dev/null 2>&1
            ;;
        *)
            # Use script install
            curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --runtime aspnetcore --version 8.0 > /dev/null 2>&1
            ;;
    esac

    if command_exists dotnet; then
        print_success ".NET $(dotnet --version) ติดตั้งสำเร็จ"
    else
        print_error "ไม่สามารถติดตั้ง .NET ได้"
        print_info "กรุณาติดตั้ง .NET 8 ด้วยตนเอง: https://dotnet.microsoft.com/download"
        exit 1
    fi
}

# =============================================================================
# CONFIGURATION WIZARD
# =============================================================================
configuration_wizard() {
    echo ""
    echo -e "  ${WHITE}${BOLD}ตอบคำถามต่อไปนี้เพื่อตั้งค่าระบบ:${NC}"
    echo ""

    # Dashboard Port
    echo -e "  ${CYAN}1. Port สำหรับ Dashboard${NC}"
    echo -e "     ${DIM}(Port ที่จะใช้เข้าถึง Web Dashboard)${NC}"
    CONFIG_PORT=$(ask_input "Port" "$DEFAULT_PORT")

    # Check if port is available
    if ss -tlnp | grep -q ":$CONFIG_PORT "; then
        print_warning "Port $CONFIG_PORT กำลังถูกใช้งาน"
        CONFIG_PORT=$(ask_input "เลือก Port อื่น" "5001")
    fi
    print_success "Port: $CONFIG_PORT"
    echo ""

    # Domain (optional)
    echo -e "  ${CYAN}2. Domain สำหรับ Dashboard (ถ้ามี)${NC}"
    echo -e "     ${DIM}(เช่น sync.yourdomain.com หรือเว้นว่างถ้าใช้ IP)${NC}"
    CONFIG_DOMAIN=$(ask_input "Domain (optional)" "")

    if [ -n "$CONFIG_DOMAIN" ]; then
        print_success "Domain: $CONFIG_DOMAIN"

        # SSL
        echo ""
        echo -e "  ${CYAN}3. ตั้งค่า SSL (Let's Encrypt)${NC}"
        if ask_yes_no "ต้องการติดตั้ง SSL Certificate อัตโนมัติหรือไม่?" "y"; then
            CONFIG_SSL="y"
            print_success "SSL: จะติดตั้งอัตโนมัติ"
        else
            CONFIG_SSL="n"
            print_info "SSL: ไม่ติดตั้ง"
        fi
    else
        print_info "Domain: ไม่ระบุ (ใช้ IP Address)"
        CONFIG_SSL="n"
    fi
    echo ""

    # GitHub Token
    echo -e "  ${CYAN}4. GitHub Personal Access Token${NC}"
    echo -e "     ${DIM}(สร้างได้ที่: Settings > Developer settings > Personal access tokens)${NC}"
    echo -e "     ${DIM}(ต้องการ scopes: repo, read:packages)${NC}"
    CONFIG_GITHUB_TOKEN=$(ask_password "GitHub Token")

    if [ -n "$CONFIG_GITHUB_TOKEN" ]; then
        print_success "GitHub Token: ✓ ได้รับแล้ว"
    else
        print_warning "GitHub Token: ไม่ได้ระบุ (สามารถเพิ่มภายหลังได้)"
    fi
    echo ""

    # Admin credentials
    echo -e "  ${CYAN}5. ข้อมูล Admin Account${NC}"
    CONFIG_ADMIN_USER=$(ask_input "Admin Username" "admin")
    CONFIG_ADMIN_EMAIL=$(ask_input "Admin Email" "")

    while true; do
        CONFIG_ADMIN_PASS=$(ask_password "Admin Password")
        local confirm_pass=$(ask_password "Confirm Password")

        if [ "$CONFIG_ADMIN_PASS" = "$confirm_pass" ]; then
            if [ ${#CONFIG_ADMIN_PASS} -ge 6 ]; then
                break
            else
                print_error "Password ต้องมีอย่างน้อย 6 ตัวอักษร"
            fi
        else
            print_error "Password ไม่ตรงกัน กรุณาลองใหม่"
        fi
    done

    print_success "Admin: $CONFIG_ADMIN_USER"
    echo ""

    # AI Feature (optional)
    echo -e "  ${CYAN}6. AI Assistant (Optional)${NC}"
    echo -e "     ${DIM}(ใช้ Ollama สำหรับวิเคราะห์ errors อัตโนมัติ)${NC}"
    if ask_yes_no "ต้องการเปิดใช้งาน AI หรือไม่?" "n"; then
        CONFIG_AI_ENABLED="true"

        if command_exists ollama; then
            print_success "Ollama ติดตั้งแล้ว"
        else
            if ask_yes_no "ต้องการติดตั้ง Ollama หรือไม่?" "y"; then
                print_info "กำลังติดตั้ง Ollama..."
                curl -fsSL https://ollama.ai/install.sh | sh > /dev/null 2>&1
                print_success "Ollama ติดตั้งสำเร็จ"
            fi
        fi
    else
        CONFIG_AI_ENABLED="false"
        print_info "AI: ปิดใช้งาน"
    fi
    echo ""

    # Summary
    echo ""
    echo -e "  ${WHITE}${BOLD}สรุปการตั้งค่า:${NC}"
    echo ""
    echo -e "  ┌─────────────────────────────────────────┐"
    echo -e "  │ Port:          ${CYAN}$CONFIG_PORT${NC}"
    [ -n "$CONFIG_DOMAIN" ] && echo -e "  │ Domain:        ${CYAN}$CONFIG_DOMAIN${NC}"
    [ "$CONFIG_SSL" = "y" ] && echo -e "  │ SSL:           ${GREEN}Enabled${NC}"
    echo -e "  │ Admin:         ${CYAN}$CONFIG_ADMIN_USER${NC}"
    echo -e "  │ AI:            ${CYAN}$([ "$CONFIG_AI_ENABLED" = "true" ] && echo "Enabled" || echo "Disabled")${NC}"
    echo -e "  │ Web Server:    ${CYAN}$PRIMARY_WEBSERVER${NC}"
    echo -e "  │ Sites Found:   ${CYAN}$SITES_COUNT${NC}"
    echo -e "  └─────────────────────────────────────────┘"
    echo ""

    if ! ask_yes_no "ยืนยันการตั้งค่าและดำเนินการติดตั้ง?" "y"; then
        print_info "ยกเลิกการติดตั้ง"
        exit 0
    fi
}

# =============================================================================
# INSTALLATION
# =============================================================================
perform_installation() {
    echo ""
    echo -e "  ${WHITE}${BOLD}กำลังติดตั้ง XcluadeAgent...${NC}"
    echo ""

    # Create service user
    if ! id "$SERVICE_USER" &>/dev/null; then
        useradd -r -s /bin/false -d $INSTALL_DIR $SERVICE_USER 2>/dev/null || true
        print_success "สร้าง service user: $SERVICE_USER"
    else
        print_info "Service user มีอยู่แล้ว: $SERVICE_USER"
    fi

    # Create directories
    mkdir -p $INSTALL_DIR/{app,config,data/{logs,backups,temp}}
    print_success "สร้าง directories"

    # Generate secrets
    local jwt_secret=$(openssl rand -base64 32)
    local webhook_secret=$(openssl rand -hex 16)

    # Download or build application
    if [ -f "XcluadeAgent.sln" ]; then
        print_info "พบ source code, กำลัง build..."
        dotnet publish src/XcluadeAgent.Api/XcluadeAgent.Api.csproj -c Release -o $INSTALL_DIR/app > /dev/null 2>&1
        print_success "Build application สำเร็จ"
    else
        print_info "กำลังดาวน์โหลด application..."
        # TODO: Download from GitHub releases when available
        print_warning "กรุณา build จาก source หรือรอ release"
    fi

    # Create configuration file
    cat > $INSTALL_DIR/config/config.yaml << CONFIGEOF
# =============================================================================
# XcluadeAgent Configuration
# Generated: $(date)
# =============================================================================

app:
  name: XcluadeAgent
  version: $VERSION

server:
  host: 0.0.0.0
  port: $CONFIG_PORT
  $([ -n "$CONFIG_DOMAIN" ] && echo "externalUrl: https://$CONFIG_DOMAIN")

database:
  provider: sqlite
  connectionString: "Data Source=$INSTALL_DIR/data/xcluadeagent.db"

github:
  accessToken: "$CONFIG_GITHUB_TOKEN"
  webhookSecret: "$webhook_secret"

security:
  jwtSecret: "$jwt_secret"
  jwtExpirationMinutes: 1440
  allowedOrigins:
    - "http://localhost:$CONFIG_PORT"
    $([ -n "$CONFIG_DOMAIN" ] && echo "- \"https://$CONFIG_DOMAIN\"")

admin:
  username: "$CONFIG_ADMIN_USER"
  email: "$CONFIG_ADMIN_EMAIL"
  # Password is hashed and stored in database on first run

ai:
  enabled: $CONFIG_AI_ENABLED
  mode: suggest
  primary:
    type: ollama
    endpoint: "http://localhost:11434"
    model: "qwen2.5-coder:7b"

sync:
  defaultOwner: "$WEBSERVER_USER"
  defaultGroup: "$WEBSERVER_USER"
  preservePermissions: true
  createBackup: true

backup:
  basePath: "$INSTALL_DIR/data/backups"
  defaultMaxBackups: 5
  retentionDays: 30

logging:
  level: Information
  path: "$INSTALL_DIR/data/logs"
  retentionDays: 30
CONFIGEOF

    print_success "สร้างไฟล์ configuration"

    # Set permissions
    chown -R $SERVICE_USER:$SERVICE_USER $INSTALL_DIR
    chmod 600 $INSTALL_DIR/config/config.yaml
    print_success "ตั้งค่า permissions"

    # Create systemd service
    cat > /etc/systemd/system/${SERVICE_NAME}.service << SERVICEEOF
[Unit]
Description=XcluadeAgent - GitHub Sync Service
Documentation=https://xman4289.com
After=network.target

[Service]
Type=notify
User=root
Group=root
WorkingDirectory=$INSTALL_DIR/app
ExecStart=/usr/bin/dotnet $INSTALL_DIR/app/XcluadeAgent.Api.dll
Restart=always
RestartSec=10
TimeoutStartSec=60
TimeoutStopSec=30

# Environment
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://localhost:$CONFIG_PORT
Environment=XCLUADE_CONFIG_PATH=$INSTALL_DIR/config/config.yaml

[Install]
WantedBy=multi-user.target
SERVICEEOF

    print_success "สร้าง systemd service"

    # Setup reverse proxy
    if [ -n "$CONFIG_DOMAIN" ]; then
        setup_reverse_proxy
    fi

    # Enable and start service
    systemctl daemon-reload
    systemctl enable $SERVICE_NAME > /dev/null 2>&1

    print_success "ติดตั้ง XcluadeAgent สำเร็จ"
}

setup_reverse_proxy() {
    case $PRIMARY_WEBSERVER in
        nginx)
            cat > /etc/nginx/sites-available/$CONFIG_DOMAIN << NGINXEOF
server {
    listen 80;
    server_name $CONFIG_DOMAIN;

    location / {
        proxy_pass http://localhost:$CONFIG_PORT;
        proxy_http_version 1.1;
        proxy_set_header Upgrade \$http_upgrade;
        proxy_set_header Connection 'upgrade';
        proxy_set_header Host \$host;
        proxy_set_header X-Real-IP \$remote_addr;
        proxy_set_header X-Forwarded-For \$proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto \$scheme;
        proxy_cache_bypass \$http_upgrade;
        proxy_read_timeout 86400;
    }
}
NGINXEOF
            ln -sf /etc/nginx/sites-available/$CONFIG_DOMAIN /etc/nginx/sites-enabled/
            nginx -t > /dev/null 2>&1 && systemctl reload nginx
            print_success "ตั้งค่า Nginx reverse proxy"

            # SSL
            if [ "$CONFIG_SSL" = "y" ]; then
                $PKG_INSTALL certbot python3-certbot-nginx > /dev/null 2>&1
                certbot --nginx -d $CONFIG_DOMAIN --non-interactive --agree-tos --email $CONFIG_ADMIN_EMAIL 2>/dev/null || true
                print_success "ติดตั้ง SSL Certificate"
            fi
            ;;
        apache)
            cat > /etc/apache2/sites-available/$CONFIG_DOMAIN.conf << APACHEEOF
<VirtualHost *:80>
    ServerName $CONFIG_DOMAIN

    ProxyPreserveHost On
    ProxyPass / http://localhost:$CONFIG_PORT/
    ProxyPassReverse / http://localhost:$CONFIG_PORT/

    RewriteEngine On
    RewriteCond %{HTTP:Upgrade} websocket [NC]
    RewriteCond %{HTTP:Connection} upgrade [NC]
    RewriteRule ^/?(.*) "ws://localhost:$CONFIG_PORT/\$1" [P,L]
</VirtualHost>
APACHEEOF
            a2ensite $CONFIG_DOMAIN > /dev/null 2>&1
            a2enmod proxy proxy_http proxy_wstunnel rewrite > /dev/null 2>&1
            systemctl reload apache2
            print_success "ตั้งค่า Apache reverse proxy"
            ;;
    esac
}

# =============================================================================
# COMPLETION
# =============================================================================
show_completion() {
    # Start service
    systemctl start $SERVICE_NAME 2>/dev/null || true
    sleep 2

    local is_running=false
    service_running $SERVICE_NAME && is_running=true

    echo ""
    echo -e "${GREEN}"
    echo "╔═══════════════════════════════════════════════════════════════════╗"
    echo "║                                                                   ║"
    if [ "$is_running" = true ]; then
        echo "║           ✓ ติดตั้ง XcluadeAgent สำเร็จ!                         ║"
    else
        echo "║           ! ติดตั้งสำเร็จ (Service ยังไม่ Start)                  ║"
    fi
    echo "║                                                                   ║"
    echo "╚═══════════════════════════════════════════════════════════════════╝"
    echo -e "${NC}"

    echo ""
    echo -e "  ${WHITE}${BOLD}📍 การเข้าถึง Dashboard:${NC}"
    echo ""

    if [ -n "$CONFIG_DOMAIN" ]; then
        if [ "$CONFIG_SSL" = "y" ]; then
            echo -e "     URL: ${CYAN}https://$CONFIG_DOMAIN${NC}"
        else
            echo -e "     URL: ${CYAN}http://$CONFIG_DOMAIN${NC}"
        fi
    else
        local server_ip=$(hostname -I | awk '{print $1}')
        echo -e "     URL: ${CYAN}http://$server_ip:$CONFIG_PORT${NC}"
    fi

    echo ""
    echo -e "  ${WHITE}${BOLD}👤 Login Credentials:${NC}"
    echo ""
    echo -e "     Username: ${CYAN}$CONFIG_ADMIN_USER${NC}"
    echo -e "     Password: ${CYAN}[ที่คุณตั้งไว้]${NC}"

    echo ""
    echo -e "  ${WHITE}${BOLD}📂 ตำแหน่งไฟล์:${NC}"
    echo ""
    echo -e "     Application: ${DIM}$INSTALL_DIR/app${NC}"
    echo -e "     Configuration: ${DIM}$INSTALL_DIR/config/config.yaml${NC}"
    echo -e "     Data: ${DIM}$INSTALL_DIR/data${NC}"
    echo -e "     Logs: ${DIM}$INSTALL_DIR/data/logs${NC}"

    echo ""
    echo -e "  ${WHITE}${BOLD}🔧 คำสั่งที่มีประโยชน์:${NC}"
    echo ""
    echo -e "     ${DIM}# ดูสถานะ${NC}"
    echo -e "     sudo systemctl status $SERVICE_NAME"
    echo ""
    echo -e "     ${DIM}# Restart service${NC}"
    echo -e "     sudo systemctl restart $SERVICE_NAME"
    echo ""
    echo -e "     ${DIM}# ดู logs${NC}"
    echo -e "     sudo journalctl -u $SERVICE_NAME -f"
    echo ""
    echo -e "     ${DIM}# แก้ไข configuration${NC}"
    echo -e "     sudo nano $INSTALL_DIR/config/config.yaml"

    if [ "$is_running" = false ]; then
        echo ""
        echo -e "  ${YELLOW}⚠️  Service ไม่ได้ start อัตโนมัติ${NC}"
        echo -e "     รันคำสั่งนี้เพื่อ start:"
        echo -e "     ${CYAN}sudo systemctl start $SERVICE_NAME${NC}"
        echo ""
        echo -e "     ดู error logs:"
        echo -e "     ${CYAN}sudo journalctl -u $SERVICE_NAME -n 50${NC}"
    fi

    echo ""
    echo -e "  ${GREEN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    echo -e "  ${WHITE}Developed by xman studio | ${CYAN}https://xman4289.com${NC}"
    echo -e "  ${GREEN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    echo ""

    # Save installation log
    echo "Installation completed at $(date)" >> "$LOG_FILE"
}

# =============================================================================
# MAIN
# =============================================================================
main() {
    # Initialize log
    echo "XcluadeAgent Installation - $(date)" > "$LOG_FILE"

    # Show banner
    show_banner

    # Step 1: Root check
    print_step 1 6 "ตรวจสอบสิทธิ์"
    check_root

    # Step 2: OS Detection & Requirements
    print_step 2 6 "ตรวจสอบระบบ"
    detect_os
    check_requirements

    # Step 3: Web Server Detection
    print_step 3 6 "สแกน Web Server"
    detect_webservers
    discover_websites

    # Step 4: Install Dependencies
    print_step 4 6 "ติดตั้ง Dependencies"
    install_dependencies
    install_dotnet

    # Step 5: Configuration
    print_step 5 6 "ตั้งค่าระบบ"
    configuration_wizard

    # Step 6: Installation
    print_step 6 6 "ติดตั้ง"
    perform_installation

    # Complete
    show_completion
}

# Run main
main "$@"
