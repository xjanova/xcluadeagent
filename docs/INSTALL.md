# คู่มือติดตั้ง XcluadeAgent

## ภาพรวม

XcluadeAgent คือระบบ sync ไฟล์จาก GitHub Releases ไปยัง Web Server โดยอัตโนมัติ ออกแบบมาเพื่อให้ deploy เว็บแอปพลิเคชันได้ง่ายและปลอดภัย

## ความต้องการของระบบ

### ขั้นต่ำ
- **OS**: Ubuntu 20.04+ หรือ Debian 10+
- **RAM**: 512 MB
- **Disk**: 1 GB พื้นที่ว่าง
- **สิทธิ์**: root access

### แนะนำ
- **RAM**: 1 GB+
- **Disk**: 5 GB+
- **Web Server**: Nginx หรือ Apache ที่ติดตั้งไว้แล้ว

---

## วิธีติดตั้ง (แบบง่าย)

### ขั้นตอนที่ 1: ดาวน์โหลด

```bash
# ดาวน์โหลด installer
curl -fsSL https://raw.githubusercontent.com/xjanova/xcluadeagent/main/scripts/install.sh -o install.sh

# หรือ clone repository
git clone https://github.com/xjanova/xcluadeagent.git
cd xcluadeagent/scripts
```

### ขั้นตอนที่ 2: รัน Installer

```bash
# ต้องรันด้วย root เท่านั้น!
sudo ./install.sh
```

### ขั้นตอนที่ 3: ทำตามคำถาม

Installer จะถามคำถามทีละข้อ:

1. **Port**: พอร์ตสำหรับ Dashboard (ค่าเริ่มต้น: 5000)
2. **Domain**: โดเมนที่จะใช้ (ถ้ามี)
3. **SSL**: ต้องการติดตั้ง SSL อัตโนมัติหรือไม่
4. **GitHub Token**: Personal Access Token
5. **Admin**: ชื่อผู้ใช้และรหัสผ่าน admin
6. **AI**: ต้องการเปิดใช้งาน AI หรือไม่

---

## สิ่งที่ Installer ทำให้อัตโนมัติ

### ตรวจจับและติดตั้ง Dependencies
- .NET 8 Runtime
- curl, wget, git, unzip, jq, openssl

### สแกนระบบ
- ตรวจจับ Web Server (Nginx, Apache, Caddy, LiteSpeed)
- ค้นหาเว็บไซต์ทั้งหมดที่มีอยู่
- ตรวจจับ Framework (Laravel, Node.js, React, Vue, Django)
- ตรวจสอบ Git repositories

### ตั้งค่า
- สร้าง systemd service
- ตั้งค่า reverse proxy (ถ้าระบุ domain)
- ติดตั้ง SSL Certificate (ถ้าต้องการ)
- สร้างไฟล์ configuration

---

## หลังติดตั้งเสร็จ

### เข้าถึง Dashboard

```
http://[IP-ของ-Server]:5000
# หรือ
https://[domain-ที่ระบุ]
```

### คำสั่งที่ใช้บ่อย

```bash
# ดูสถานะ
sudo systemctl status xcluadeagent

# Restart service
sudo systemctl restart xcluadeagent

# ดู logs
sudo journalctl -u xcluadeagent -f

# หยุด service
sudo systemctl stop xcluadeagent

# เปิด service
sudo systemctl start xcluadeagent
```

### ตำแหน่งไฟล์สำคัญ

| ไฟล์ | ตำแหน่ง |
|------|---------|
| Application | `/opt/xcluadeagent/app` |
| Configuration | `/opt/xcluadeagent/config/config.yaml` |
| Database | `/opt/xcluadeagent/data/xcluadeagent.db` |
| Logs | `/opt/xcluadeagent/data/logs` |
| Backups | `/opt/xcluadeagent/data/backups` |

---

## สร้าง GitHub Token

1. ไปที่ [GitHub Settings](https://github.com/settings/tokens)
2. คลิก "Generate new token (classic)"
3. ตั้งชื่อ เช่น "XcluadeAgent"
4. เลือก scopes:
   - ✅ `repo` (Full control)
   - ✅ `read:packages`
5. คลิก "Generate token"
6. คัดลอก token (จะแสดงครั้งเดียว!)

---

## การแก้ปัญหา

### Service ไม่ start

```bash
# ดู error logs
sudo journalctl -u xcluadeagent -n 50

# ตรวจสอบ config file
sudo cat /opt/xcluadeagent/config/config.yaml

# ตรวจสอบ port ว่าถูกใช้หรือไม่
sudo ss -tlnp | grep 5000
```

### ไม่สามารถ sync ได้

1. ตรวจสอบว่า GitHub Token ถูกต้อง
2. ตรวจสอบ permissions ของ directory
3. ดู logs ใน Dashboard

### Permission denied

XcluadeAgent ต้องรันด้วย root เพื่อเข้าถึง web directories ทั้งหมด
แต่จะรักษา ownership เดิมของไฟล์ไว้

---

## ถอนการติดตั้ง

```bash
# หยุดและ disable service
sudo systemctl stop xcluadeagent
sudo systemctl disable xcluadeagent

# ลบ service file
sudo rm /etc/systemd/system/xcluadeagent.service

# ลบ application
sudo rm -rf /opt/xcluadeagent

# ลบ user
sudo userdel xcluade

# ลบ nginx config (ถ้ามี)
sudo rm /etc/nginx/sites-enabled/[your-domain]
sudo rm /etc/nginx/sites-available/[your-domain]
sudo systemctl reload nginx
```

---

## ติดต่อ

- **Website**: [xman4289.com](https://xman4289.com)
- **GitHub**: [github.com/xjanova/xcluadeagent](https://github.com/xjanova/xcluadeagent)
- **Developer**: xman studio

---

## License

XcluadeAgent เป็นซอฟต์แวร์ที่พัฒนาโดย xman studio
