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

## 🆚 XcluadeAgent vs Docker/Watchtower vs GitHub Actions

**ก่อนตัดสินใจใช้ XcluadeAgent ควรพิจารณาว่าเครื่องมือที่มีอยู่แล้วเพียงพอหรือไม่**

### ตารางเปรียบเทียบ (ตรงไปตรงมา)

| คุณสมบัติ | XcluadeAgent | Docker + Watchtower | GitHub Actions |
|----------|--------------|---------------------|----------------|
| **ความซับซ้อนในการตั้งค่า** | ปานกลาง | ง่าย (ถ้าใช้ Docker อยู่แล้ว) | ง่าย |
| **ต้องเรียนรู้ใหม่** | ใช่ | ไม่ (ถ้ารู้ Docker) | ไม่ (ถ้ารู้ YAML) |
| **Dashboard รวมศูนย์** | ✅ มี | ❌ ไม่มี | ❌ ต้องดูทีละ repo |
| **รองรับ Non-Docker apps** | ✅ | ❌ Docker เท่านั้น | ✅ |
| **AI ช่วยแก้ปัญหา** | ✅ | ❌ | ❌ |
| **Rollback ง่าย** | ✅ กดปุ่มเดียว | ⚠️ ต้อง manual | ⚠️ ต้อง manual |
| **Multi-project view** | ✅ หน้าเดียว | ❌ ต้องดูทีละตัว | ❌ ต้องดูทีละ repo |
| **Notification หลายช่อง** | ✅ 5+ ช่อง | ❌ ต้องตั้งเอง | ⚠️ จำกัด |
| **ค่าใช้จ่าย** | ฟรี (self-hosted) | ฟรี | ฟรี (มี limit) |
| **Resource usage** | ~100-200MB RAM | ~50MB RAM | 0 (รันที่ GitHub) |

### เมื่อไหร่ควรใช้ Docker + Watchtower แทน

✅ **ใช้ Docker + Watchtower ถ้า:**
- ทุก project เป็น Docker container อยู่แล้ว
- ไม่ต้องการ dashboard รวมศูนย์
- ทีมคุ้นเคย Docker อยู่แล้ว
- ต้องการความเรียบง่าย ไม่ต้องติดตั้งอะไรเพิ่ม

```bash
# Watchtower ง่ายมาก:
docker run -d --name watchtower \
  -v /var/run/docker.sock:/var/run/docker.sock \
  containrrr/watchtower
```

### เมื่อไหร่ควรใช้ GitHub Actions แทน

✅ **ใช้ GitHub Actions ถ้า:**
- มีแค่ 1-3 projects
- ต้องการ CI/CD ครบวงจร (test, build, deploy)
- ไม่ต้องการติดตั้งอะไรบน server
- ยอมรับ GitHub Actions minutes limit

```yaml
# GitHub Actions ก็ใช้ได้:
on:
  release:
    types: [published]
jobs:
  deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - run: rsync -avz ./ server:/var/www/app/
```

### เมื่อไหร่ควรใช้ XcluadeAgent

✅ **ใช้ XcluadeAgent ถ้า:**
- มีหลาย projects (5+) ที่ต้องจัดการ
- ต้องการ dashboard รวมศูนย์ ดูทุกอย่างหน้าเดียว
- มี projects แบบ Non-Docker (PHP, Node, Python บน bare metal)
- ต้องการ rollback ที่ง่ายและเร็ว
- ต้องการ AI ช่วยวิเคราะห์ปัญหา
- ต้องการ notification หลายช่องทาง (LINE, Discord, Telegram)
- ทีมไม่คุ้นเคย Docker/GitHub Actions

### ข้อเสียของ XcluadeAgent (ตรงๆ)

❌ **สิ่งที่ต้องพิจารณา:**
- ต้องติดตั้ง .NET Runtime บน server
- ใช้ RAM มากกว่า Watchtower (~100-200MB vs ~50MB)
- โปรเจคใหม่ ยังไม่มี community ใหญ่
- ต้องเรียนรู้ระบบใหม่
- ถ้ามีแค่ 1-2 projects อาจเป็น overkill

### สรุป

| สถานการณ์ | แนะนำใช้ |
|----------|---------|
| ทุกอย่างเป็น Docker | **Watchtower** |
| 1-3 projects, ต้องการ CI/CD | **GitHub Actions** |
| 5+ projects, ต้องการ dashboard | **XcluadeAgent** |
| Non-Docker + ต้องการ AI ช่วย | **XcluadeAgent** |
| ต้องการความเรียบง่ายที่สุด | **GitHub Actions** หรือ **Watchtower** |

---

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
