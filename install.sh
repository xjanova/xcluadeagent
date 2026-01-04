#!/bin/bash
#
# XcluadeAgent - One-Click Installer
# Developed by xman studio | https://xman4289.com
#
# Usage: curl -fsSL https://raw.githubusercontent.com/xjanova/xcluadeagent/main/install.sh | bash
#

set -e

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
PURPLE='\033[0;35m'
CYAN='\033[0;36m'
WHITE='\033[1;37m'
NC='\033[0m' # No Color

# Variables
INSTALL_DIR="/opt/xcluadeagent"
SERVICE_USER="xcluade"
REPO_URL="https://github.com/xjanova/xcluadeagent.git"
BRANCH="main"

# Functions
print_banner() {
    clear
    echo -e "${CYAN}"
    cat << 'EOF'
    ╔═══════════════════════════════════════════════════════════════════╗
    ║     ██╗  ██╗ ██████╗██╗     ██╗   ██╗ █████╗ ██████╗ ███████╗     ║
    ║     ╚██╗██╔╝██╔════╝██║     ██║   ██║██╔══██╗██╔══██╗██╔════╝     ║
    ║      ╚███╔╝ ██║     ██║     ██║   ██║███████║██║  ██║█████╗       ║
    ║      ██╔██╗ ██║     ██║     ██║   ██║██╔══██║██║  ██║██╔══╝       ║
    ║     ██╔╝ ██╗╚██████╗███████╗╚██████╔╝██║  ██║██████╔╝███████╗     ║
    ║     ╚═╝  ╚═╝ ╚═════╝╚══════╝ ╚═════╝ ╚═╝  ╚═╝╚═════╝ ╚══════╝     ║
    ║                         AGENT                                      ║
    ║                                                                    ║
    ║     Developed by: xman studio                                      ║
    ║     Website: https://xman4289.com                                  ║
    ╚═══════════════════════════════════════════════════════════════════╝
EOF
    echo -e "${NC}"
    echo -e "${WHITE}    One-Click Installer for Ubuntu/Debian${NC}\n"
}

print_step() {
    echo -e "\n${BLUE}[$(date +%H:%M:%S)]${NC} ${WHITE}$1${NC}"
}

print_success() {
    echo -e "${GREEN}✓${NC} $1"
}

print_error() {
    echo -e "${RED}✗${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}!${NC} $1"
}

print_info() {
    echo -e "${CYAN}ℹ${NC} $1"
}

prompt_yes_no() {
    local prompt="$1"
    local default="${2:-y}"
    local response

    if [[ "$default" == "y" ]]; then
        prompt="$prompt [Y/n]: "
    else
        prompt="$prompt [y/N]: "
    fi

    read -p "$prompt" response
    response=${response:-$default}
    [[ "$response" =~ ^[Yy] ]]
}

prompt_input() {
    local prompt="$1"
    local default="$2"
    local response

    if [[ -n "$default" ]]; then
        read -p "$prompt [$default]: " response
        echo "${response:-$default}"
    else
        read -p "$prompt: " response
        echo "$response"
    fi
}

prompt_password() {
    local prompt="$1"
    local password
    read -sp "$prompt: " password
    echo
    echo "$password"
}

check_root() {
    if [[ $EUID -ne 0 ]]; then
        print_error "This script must be run as root (use sudo)"
        exit 1
    fi
}

check_os() {
    if [[ ! -f /etc/os-release ]]; then
        print_error "Cannot detect OS. This script supports Ubuntu/Debian only."
        exit 1
    fi

    . /etc/os-release

    if [[ "$ID" != "ubuntu" && "$ID" != "debian" ]]; then
        print_error "This script supports Ubuntu/Debian only. Detected: $ID"
        exit 1
    fi

    print_success "Detected OS: $PRETTY_NAME"
}

install_dotnet() {
    print_step "Installing .NET 8 SDK..."

    if command -v dotnet &> /dev/null; then
        local version=$(dotnet --version 2>/dev/null || echo "0")
        if [[ "$version" == 8.* ]]; then
            print_success ".NET 8 SDK already installed (v$version)"
            return 0
        fi
    fi

    # Install prerequisites
    apt-get update -qq
    apt-get install -y -qq wget apt-transport-https software-properties-common

    # Add Microsoft repository
    . /etc/os-release
    wget -q "https://packages.microsoft.com/config/$ID/$VERSION_ID/packages-microsoft-prod.deb" -O /tmp/packages-microsoft-prod.deb
    dpkg -i /tmp/packages-microsoft-prod.deb
    rm /tmp/packages-microsoft-prod.deb

    # Install .NET SDK
    apt-get update -qq
    apt-get install -y -qq dotnet-sdk-8.0

    print_success ".NET 8 SDK installed successfully"
}

install_dependencies() {
    print_step "Installing dependencies..."

    apt-get install -y -qq git curl unzip

    print_success "Dependencies installed"
}

create_user() {
    print_step "Creating service user..."

    if id "$SERVICE_USER" &>/dev/null; then
        print_success "User '$SERVICE_USER' already exists"
    else
        useradd -r -s /bin/false -d "$INSTALL_DIR" "$SERVICE_USER"
        print_success "Created user '$SERVICE_USER'"
    fi
}

clone_repository() {
    print_step "Downloading XcluadeAgent..."

    if [[ -d "$INSTALL_DIR" ]]; then
        print_warning "Directory $INSTALL_DIR already exists"
        if prompt_yes_no "Do you want to remove it and reinstall?"; then
            rm -rf "$INSTALL_DIR"
        else
            print_info "Updating existing installation..."
            cd "$INSTALL_DIR"
            git pull origin "$BRANCH"
            return 0
        fi
    fi

    git clone -q --branch "$BRANCH" "$REPO_URL" "$INSTALL_DIR"
    print_success "Downloaded to $INSTALL_DIR"
}

build_application() {
    print_step "Building application..."

    cd "$INSTALL_DIR"
    dotnet restore -q
    dotnet build -c Release -q

    print_success "Build completed"
}

configure_wizard() {
    print_step "Configuration Wizard"
    echo -e "${PURPLE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}\n"

    # Port
    local port=$(prompt_input "Enter server port" "5000")

    # External URL
    local external_url=$(prompt_input "Enter external URL (for webhooks)" "http://localhost:$port")

    # JWT Secret
    local jwt_secret=$(openssl rand -base64 32)
    print_info "Generated JWT Secret automatically"

    # Admin password
    echo -e "\n${YELLOW}Admin Account Setup${NC}"
    local admin_password
    while true; do
        admin_password=$(prompt_password "Enter admin password (min 8 chars)")
        if [[ ${#admin_password} -ge 8 ]]; then
            local confirm_password=$(prompt_password "Confirm admin password")
            if [[ "$admin_password" == "$confirm_password" ]]; then
                break
            else
                print_error "Passwords do not match. Try again."
            fi
        else
            print_error "Password must be at least 8 characters."
        fi
    done

    # GitHub OAuth (Optional)
    local github_client_id=""
    local github_client_secret=""

    echo -e "\n${YELLOW}GitHub OAuth Setup (Optional)${NC}"
    print_info "This enables One-Click GitHub connection on the dashboard"
    print_info "Create an OAuth App at: https://github.com/settings/developers"
    print_info "Set callback URL to: $external_url/api/v1/auth/github/callback"

    if prompt_yes_no "Do you want to configure GitHub OAuth now?" "n"; then
        github_client_id=$(prompt_input "GitHub OAuth Client ID")
        github_client_secret=$(prompt_password "GitHub OAuth Client Secret")
    fi

    # Create config directory
    mkdir -p "$INSTALL_DIR/config"

    # Generate config.yaml
    cat > "$INSTALL_DIR/config/config.yaml" << EOF
# XcluadeAgent Configuration
# Generated by installer on $(date)

app:
  name: XcluadeAgent
  version: 1.0.0

server:
  host: 0.0.0.0
  port: $port
  externalUrl: $external_url

database:
  provider: sqlite
  connectionString: Data Source=$INSTALL_DIR/data/xcluadeagent.db

github:
  oauth:
    clientId: ${github_client_id:-""}
    clientSecret: ${github_client_secret:-""}
    scopes: repo,read:user,user:email

security:
  jwtSecret: $jwt_secret
  jwtExpirationMinutes: 1440
  refreshTokenExpirationDays: 30
  maxFailedLogins: 5
  lockoutMinutes: 15

logging:
  level: Information
  path: $INSTALL_DIR/data/logs
  retentionDays: 30
EOF

    # Create data directories
    mkdir -p "$INSTALL_DIR/data/logs"
    mkdir -p "$INSTALL_DIR/data/backups"

    # Set permissions
    chown -R "$SERVICE_USER:$SERVICE_USER" "$INSTALL_DIR"

    # Store admin password hash (will be set on first run)
    echo "$admin_password" > "$INSTALL_DIR/.admin_password_temp"
    chmod 600 "$INSTALL_DIR/.admin_password_temp"

    print_success "Configuration saved"

    # Return values for later use
    echo "$port" > /tmp/xcluade_port
}

run_migrations() {
    print_step "Setting up database..."

    cd "$INSTALL_DIR/src/XcluadeAgent.Api"

    # Run as service user
    sudo -u "$SERVICE_USER" dotnet ef database update 2>/dev/null || {
        # If EF tools not available, the app will create DB on first run
        print_info "Database will be created on first run"
    }

    print_success "Database ready"
}

create_systemd_service() {
    print_step "Creating systemd service..."

    cat > /etc/systemd/system/xcluadeagent.service << EOF
[Unit]
Description=XcluadeAgent - GitHub Sync Service
Documentation=https://github.com/xjanova/xcluadeagent
After=network.target

[Service]
Type=notify
User=$SERVICE_USER
Group=$SERVICE_USER
WorkingDirectory=$INSTALL_DIR
ExecStart=/usr/bin/dotnet $INSTALL_DIR/src/XcluadeAgent.Api/bin/Release/net8.0/XcluadeAgent.Api.dll
Restart=always
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=xcluadeagent
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=DOTNET_PRINT_TELEMETRY_MESSAGE=false

# Security hardening
NoNewPrivileges=true
PrivateTmp=true
ProtectSystem=strict
ProtectHome=true
ReadWritePaths=$INSTALL_DIR/data

[Install]
WantedBy=multi-user.target
EOF

    systemctl daemon-reload
    systemctl enable xcluadeagent

    print_success "Systemd service created"
}

start_service() {
    print_step "Starting XcluadeAgent..."

    systemctl start xcluadeagent

    # Wait for startup
    sleep 3

    if systemctl is-active --quiet xcluadeagent; then
        print_success "XcluadeAgent is running"
    else
        print_error "Failed to start. Check: journalctl -u xcluadeagent -f"
        exit 1
    fi
}

print_summary() {
    local port=$(cat /tmp/xcluade_port 2>/dev/null || echo "5000")
    rm -f /tmp/xcluade_port

    echo -e "\n${GREEN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    echo -e "${GREEN}    Installation Complete!${NC}"
    echo -e "${GREEN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}\n"

    echo -e "${WHITE}Access your dashboard:${NC}"
    echo -e "  ${CYAN}http://localhost:$port${NC}"
    echo -e "  ${CYAN}http://$(hostname -I | awk '{print $1}'):$port${NC}\n"

    echo -e "${WHITE}Login credentials:${NC}"
    echo -e "  Username: ${CYAN}admin${NC}"
    echo -e "  Password: ${CYAN}(the password you set during installation)${NC}\n"

    echo -e "${WHITE}Useful commands:${NC}"
    echo -e "  ${YELLOW}systemctl status xcluadeagent${NC}  - Check status"
    echo -e "  ${YELLOW}systemctl restart xcluadeagent${NC} - Restart service"
    echo -e "  ${YELLOW}journalctl -u xcluadeagent -f${NC}  - View logs\n"

    echo -e "${WHITE}Configuration:${NC}"
    echo -e "  ${CYAN}$INSTALL_DIR/config/config.yaml${NC}\n"

    echo -e "${PURPLE}Thank you for using XcluadeAgent!${NC}"
    echo -e "${PURPLE}Developed by xman studio | https://xman4289.com${NC}\n"
}

# Uninstall function
uninstall() {
    print_banner
    echo -e "${RED}Uninstall XcluadeAgent${NC}\n"

    if prompt_yes_no "Are you sure you want to uninstall XcluadeAgent?" "n"; then
        print_step "Stopping service..."
        systemctl stop xcluadeagent 2>/dev/null || true
        systemctl disable xcluadeagent 2>/dev/null || true

        print_step "Removing files..."
        rm -f /etc/systemd/system/xcluadeagent.service
        systemctl daemon-reload

        if prompt_yes_no "Remove all data (database, backups, logs)?" "n"; then
            rm -rf "$INSTALL_DIR"
            print_success "All files removed"
        else
            rm -rf "$INSTALL_DIR/src"
            rm -rf "$INSTALL_DIR/config"
            print_success "Application removed (data preserved in $INSTALL_DIR/data)"
        fi

        if prompt_yes_no "Remove service user '$SERVICE_USER'?" "n"; then
            userdel "$SERVICE_USER" 2>/dev/null || true
            print_success "User removed"
        fi

        print_success "XcluadeAgent uninstalled"
    else
        print_info "Uninstall cancelled"
    fi
}

# Main installation flow
main() {
    # Check for uninstall flag
    if [[ "$1" == "--uninstall" || "$1" == "-u" ]]; then
        check_root
        uninstall
        exit 0
    fi

    print_banner

    echo -e "${WHITE}This wizard will guide you through the installation.${NC}\n"

    if ! prompt_yes_no "Do you want to continue with the installation?"; then
        print_info "Installation cancelled"
        exit 0
    fi

    check_root
    check_os
    install_dependencies
    install_dotnet
    create_user
    clone_repository
    build_application
    configure_wizard
    run_migrations
    create_systemd_service
    start_service
    print_summary
}

# Run main
main "$@"
