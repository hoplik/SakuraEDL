# MultiFlash Admin 部署指南

## 目录

- [快速部署 (Docker)](#快速部署-docker)
- [手动部署 (二进制)](#手动部署-二进制)
- [Nginx 反向代理](#nginx-反向代理)
- [HTTPS 配置](#https-配置)
- [环境变量](#环境变量)
- [常见问题](#常见问题)

---

## 快速部署 (Docker)

### 前置要求

- Docker 20.10+
- Docker Compose 2.0+

### 步骤

1. **上传文件到服务器**

```bash
# 将以下文件上传到服务器
scp -r admin-panel/backend user@server:/opt/multiflash-admin/
```

2. **创建环境配置**

```bash
cd /opt/multiflash-admin
cat > .env << EOF
ADMIN_TOKEN=your-secure-token-$(openssl rand -hex 16)
ADMIN_USER=admin
ADMIN_PASS=$(openssl rand -base64 12)
EOF

# 查看生成的密码
cat .env
```

3. **启动服务**

```bash
# 仅后端服务
docker-compose up -d

# 包含 Nginx 反向代理
docker-compose --profile with-nginx up -d
```

4. **验证部署**

```bash
curl http://localhost:8082/api/loaders/list
```

### Docker 常用命令

```bash
# 查看日志
docker-compose logs -f multiflash-admin

# 重启服务
docker-compose restart

# 停止服务
docker-compose down

# 更新部署
docker-compose pull
docker-compose up -d --build
```

---

## 手动部署 (二进制)

### 前置要求

- Linux 服务器 (Ubuntu 20.04+ / CentOS 7+)
- Go 1.21+ (仅编译时需要)

### 步骤

#### 1. 在本地构建

**Windows:**
```powershell
cd admin-panel/backend
.\build.ps1
```

**Linux/Mac:**
```bash
cd admin-panel/backend
chmod +x build.sh
./build.sh
```

#### 2. 上传到服务器

```bash
# 上传构建产物
scp dist/multiflash-admin-linux-amd64 user@server:/opt/multiflash-admin/
scp -r dist/static user@server:/opt/multiflash-admin/

# 或上传整个部署包
scp dist/multiflash-admin-linux-amd64-3.0.0.tar.gz user@server:/opt/
ssh user@server "cd /opt && tar -xzvf multiflash-admin-linux-amd64-3.0.0.tar.gz"
```

#### 3. 配置 systemd 服务

```bash
# 复制服务文件
sudo cp deploy/multiflash-admin.service /etc/systemd/system/

# 编辑配置 (修改密码等)
sudo nano /etc/systemd/system/multiflash-admin.service

# 创建目录和权限
sudo mkdir -p /opt/multiflash-admin/{data,uploads}
sudo chown -R www-data:www-data /opt/multiflash-admin

# 启动服务
sudo systemctl daemon-reload
sudo systemctl enable multiflash-admin
sudo systemctl start multiflash-admin

# 查看状态
sudo systemctl status multiflash-admin
```

#### 4. 配置防火墙

```bash
# Ubuntu (ufw)
sudo ufw allow 8082/tcp

# CentOS (firewalld)
sudo firewall-cmd --permanent --add-port=8082/tcp
sudo firewall-cmd --reload
```

---

## Nginx 反向代理

### 安装 Nginx

```bash
# Ubuntu
sudo apt update && sudo apt install nginx

# CentOS
sudo yum install nginx
```

### 配置

```bash
# 复制配置文件
sudo cp nginx.conf /etc/nginx/sites-available/multiflash-admin

# 编辑配置 (修改域名)
sudo nano /etc/nginx/sites-available/multiflash-admin

# 启用站点
sudo ln -s /etc/nginx/sites-available/multiflash-admin /etc/nginx/sites-enabled/

# 测试配置
sudo nginx -t

# 重载 Nginx
sudo systemctl reload nginx
```

### 配置示例

```nginx
server {
    listen 80;
    server_name api.your-domain.com;

    location / {
        proxy_pass http://127.0.0.1:8082;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        
        # 上传文件大小限制
        client_max_body_size 100M;
    }
}
```

---

## HTTPS 配置

### 使用 Let's Encrypt (推荐)

```bash
# 安装 Certbot
sudo apt install certbot python3-certbot-nginx

# 获取证书
sudo certbot --nginx -d api.your-domain.com

# 自动续期测试
sudo certbot renew --dry-run
```

### 手动配置 SSL

```nginx
server {
    listen 443 ssl http2;
    server_name api.your-domain.com;

    ssl_certificate /etc/ssl/certs/your-cert.pem;
    ssl_certificate_key /etc/ssl/private/your-key.pem;
    ssl_protocols TLSv1.2 TLSv1.3;
    ssl_ciphers HIGH:!aNULL:!MD5;

    location / {
        proxy_pass http://127.0.0.1:8082;
        # ... 其他配置
    }
}

# HTTP 重定向到 HTTPS
server {
    listen 80;
    server_name api.your-domain.com;
    return 301 https://$server_name$request_uri;
}
```

---

## 环境变量

| 变量名 | 默认值 | 说明 |
|--------|--------|------|
| `ADMIN_TOKEN` | `multiflash-admin-2024` | API 认证 Token |
| `ADMIN_USER` | `admin` | 管理员用户名 |
| `ADMIN_PASS` | `admin123` | 管理员密码 |
| `DB_PATH` | `./multiflash.db` | SQLite 数据库路径 |
| `PORT` | `8082` | 服务端口 |
| `TZ` | `Asia/Shanghai` | 时区 |

---

## API 端点

### 公开 API

| 方法 | 端点 | 说明 |
|------|------|------|
| GET | `/api/loaders/list` | 获取 Loader 列表 |
| GET | `/api/loaders/{id}/download` | 下载 Loader |
| GET | `/api/loaders/{id}/digest` | 下载 Digest 文件 |
| GET | `/api/loaders/{id}/sign` | 下载 Sign 文件 |
| POST | `/api/loaders/match` | 匹配 Loader |

### 管理 API (需要 `X-Admin-Token` 头)

| 方法 | 端点 | 说明 |
|------|------|------|
| POST | `/api/admin/login` | 管理员登录 |
| GET | `/api/admin/stats` | 获取统计数据 |
| GET | `/api/admin/loaders` | 获取 Loader 列表 |
| POST | `/api/admin/loaders/upload` | 上传 Loader |
| PUT | `/api/admin/loaders/{id}` | 更新 Loader |
| DELETE | `/api/admin/loaders/{id}` | 删除 Loader |
| GET | `/api/admin/logs` | 获取设备日志 |

---

## 客户端配置

部署完成后，修改 MultiFlash 客户端的 API 地址：

**文件:** `Qualcomm/Services/cloud_loader_service.cs`

```csharp
// 修改为你的服务器地址
private const string API_BASE_PROD = "https://api.your-domain.com/api";
```

---

## 常见问题

### Q: 服务启动失败

```bash
# 查看详细日志
sudo journalctl -u multiflash-admin -f

# 检查端口占用
sudo netstat -tlnp | grep 8082
```

### Q: 数据库权限错误

```bash
# 确保目录权限正确
sudo chown -R www-data:www-data /opt/multiflash-admin
```

### Q: Docker 容器无法访问

```bash
# 检查容器状态
docker ps -a

# 查看容器日志
docker logs multiflash-admin
```

### Q: Nginx 502 错误

```bash
# 检查后端服务是否运行
curl http://127.0.0.1:8082/api/loaders/list

# 检查 Nginx 错误日志
sudo tail -f /var/log/nginx/error.log
```

---

## 备份与恢复

### 备份

```bash
# 备份数据库和上传文件
tar -czvf multiflash-backup-$(date +%Y%m%d).tar.gz \
    /opt/multiflash-admin/data \
    /opt/multiflash-admin/uploads
```

### 恢复

```bash
# 停止服务
sudo systemctl stop multiflash-admin

# 恢复备份
tar -xzvf multiflash-backup-YYYYMMDD.tar.gz -C /

# 启动服务
sudo systemctl start multiflash-admin
```

---

## 联系支持

- QQ 群: https://qm.qq.com/q/z3iVnkm22c
- GitHub: https://github.com/your-repo/multiflash
