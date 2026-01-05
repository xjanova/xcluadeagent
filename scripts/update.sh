#!/bin/bash
# XcluadeAgent Update Script
# Usage: sudo ./scripts/update.sh

set -e

INSTALL_DIR="/opt/xcluadeagent"
SERVICE_NAME="xcluadeagent"
REPO_DIR="$(cd "$(dirname "$0")/.." && pwd)"

echo "╔════════════════════════════════════════╗"
echo "║     XcluadeAgent Update Script         ║"
echo "╚════════════════════════════════════════╝"

# Check if running as root
if [ "$EUID" -ne 0 ]; then
    echo "❌ Please run as root: sudo $0"
    exit 1
fi

# Pull latest changes
echo "📥 Pulling latest changes..."
cd "$REPO_DIR"
git pull origin main 2>/dev/null || git pull origin master 2>/dev/null || echo "⚠️  Git pull skipped"

# Build
echo "🔨 Building..."
dotnet publish src/XcluadeAgent.Api -c Release -o "$INSTALL_DIR/publish" --nologo -v q

# Stop service
echo "⏹️  Stopping service..."
systemctl stop $SERVICE_NAME 2>/dev/null || true

# Copy files
echo "📦 Updating files..."
cp -r "$INSTALL_DIR/publish/"* "$INSTALL_DIR/"

# Start service
echo "▶️  Starting service..."
systemctl start $SERVICE_NAME

# Check status
sleep 2
if systemctl is-active --quiet $SERVICE_NAME; then
    echo "✅ Update complete! Service is running."
    systemctl status $SERVICE_NAME --no-pager -l | head -15
else
    echo "❌ Service failed to start. Check logs:"
    journalctl -u $SERVICE_NAME -n 20 --no-pager
    exit 1
fi
