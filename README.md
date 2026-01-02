# XcluadeAgent

<div align="center">

```
╔═══════════════════════════════════════════════════════════════════╗
║     ██╗  ██╗ ██████╗██╗     ██╗   ██╗ █████╗ ██████╗ ███████╗     ║
║     ╚██╗██╔╝██╔════╝██║     ██║   ██║██╔══██╗██╔══██╗██╔════╝     ║
║      ╚███╔╝ ██║     ██║     ██║   ██║███████║██║  ██║█████╗       ║
║      ██╔██╗ ██║     ██║     ██║   ██║██╔══██║██║  ██║██╔══╝       ║
║     ██╔╝ ██╗╚██████╗███████╗╚██████╔╝██║  ██║██████╔╝███████╗     ║
║     ╚═╝  ╚═╝ ╚═════╝╚══════╝ ╚═════╝ ╚═╝  ╚═╝╚═════╝ ╚══════╝     ║
║                         AGENT                                      ║
╚═══════════════════════════════════════════════════════════════════╝
```

**GitHub Sync Service with AI-Powered Auto-Fix**

[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![xman studio](https://img.shields.io/badge/by-xman%20studio-blue)](https://xman4289.com)

</div>

## 🚀 Features

- **📥 GitHub Release Sync** - Automatically download and deploy from GitHub Releases
- **🔄 Multi-Project Support** - Manage unlimited projects from a single dashboard
- **🤖 AI Assistant** - Smart error analysis and fix suggestions (Ollama/Claude/OpenAI)
- **⏪ Auto-Rollback** - Automatic rollback when errors are detected
- **💾 Backup System** - Automatic backups before each sync
- **🔔 Notifications** - Discord, Telegram, LINE OA, Email, Slack
- **🖥️ Web Dashboard** - Beautiful, responsive dashboard with dark mode
- **⌨️ CLI Tool** - Full control from the command line
- **🔒 Security** - JWT authentication, role-based access, 2FA support
- **📊 Monitoring** - Health checks, disk usage, SSL certificate monitoring

## 📋 Requirements

- .NET 8.0 Runtime
- Linux (Ubuntu 20.04+) or Windows Server 2019+
- 1GB RAM minimum, 2GB recommended
- SQLite (default) or PostgreSQL

## ⚡ Quick Start

### Ubuntu/Debian

```bash
curl -fsSL https://raw.githubusercontent.com/xjanova/xmanstudio/main/xcluadeagent/scripts/install.sh | sudo bash
```

### Docker

```bash
docker run -d \
  -p 5000:5000 \
  -v xcluade_data:/app/data \
  -e XCLUADE_SECURITY__JWTSECRET=your_secret \
  -e XCLUADE_GITHUB__ACCESSTOKEN=your_token \
  xmanstudio/xcluadeagent:latest
```

### Docker Compose

```bash
cd docker
cp .env.example .env
# Edit .env with your settings
docker-compose up -d
```

## 🖥️ Dashboard

Access the dashboard at `http://your-server:5000`

**Default credentials:**
- Username: `admin`
- Password: `admin123`

⚠️ **Change the default password immediately!**

## ⚙️ Configuration

Configuration file: `config/config.yaml`

```yaml
# Server
server:
  port: 5000
  externalUrl: https://sync.yourdomain.com

# GitHub
github:
  accessToken: ghp_xxxxxxxxxxxx
  webhookSecret: your_webhook_secret

# AI (optional)
ai:
  enabled: true
  mode: suggest  # off | alert | suggest | review | sandbox | auto
  primary:
    type: ollama
    endpoint: http://localhost:11434
    model: qwen2.5-coder:7b

# Notifications
notifications:
  discord:
    enabled: true
    webhookUrl: https://discord.com/api/webhooks/...
```

## 🤖 AI Modes

| Mode | Description | Risk |
|------|-------------|------|
| `off` | AI disabled | None |
| `alert` | Smart error notifications | Very Low |
| `suggest` | AI suggests fixes | Low |
| `review` | AI creates fixes, requires approval | Medium |
| `sandbox` | AI tests fixes in staging first | Medium |
| `auto` | AI fixes production directly | **HIGH** |

## 📱 CLI Tool (syncctl)

```bash
# Install globally
dotnet tool install -g syncctl

# List projects
syncctl list

# Sync a project
syncctl sync my-project

# Sync with preview (dry run)
syncctl sync my-project --dry-run

# Rollback
syncctl rollback my-project

# Check status
syncctl status
```

## 🔔 Supported Notification Channels

- ✅ Discord (Webhook)
- ✅ Telegram (Bot API)
- ✅ LINE Official Account (Messaging API)
- ✅ Slack (Webhook)
- ✅ Email (SMTP)
- ✅ Custom Webhooks

## 📊 Framework Support

XcluadeAgent automatically detects and runs appropriate commands:

| Framework | Auto Commands |
|-----------|--------------|
| Laravel | `composer install`, `artisan migrate`, cache commands |
| Node.js | `npm ci`, `npm run build` |
| React/Vue | `npm ci`, `npm run build` |
| Django | `pip install`, `manage.py migrate` |
| .NET | `dotnet restore`, `dotnet build` |

## 🏗️ Architecture

```
┌─────────────────────────────────────────────────────┐
│                  XcluadeAgent                        │
├─────────────────────────────────────────────────────┤
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  │
│  │  Dashboard  │  │  REST API   │  │  Webhooks   │  │
│  │  (Blazor)   │  │  (ASP.NET)  │  │  (GitHub)   │  │
│  └─────────────┘  └─────────────┘  └─────────────┘  │
├─────────────────────────────────────────────────────┤
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  │
│  │   GitHub    │  │     AI      │  │ Notifications│  │
│  │   Service   │  │   Service   │  │   Service   │  │
│  └─────────────┘  └─────────────┘  └─────────────┘  │
├─────────────────────────────────────────────────────┤
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  │
│  │   SQLite    │  │   Backup    │  │   License   │  │
│  │  Database   │  │   System    │  │   Manager   │  │
│  └─────────────┘  └─────────────┘  └─────────────┘  │
└─────────────────────────────────────────────────────┘
```

## 📄 License

- **Community Edition**: Free for up to 3 projects
- **Professional**: Up to 10 projects, advanced features
- **Enterprise**: Unlimited projects, priority support

## 🤝 Support

- 📧 Email: support@xman4289.com
- 🌐 Website: [xman4289.com](https://xman4289.com)
- 💬 GitHub: [github.com/xjanova/xmanstudio](https://github.com/xjanova/xmanstudio)

---

<div align="center">

**Developed with ❤️ by [xman studio](https://xman4289.com)**

</div>
