# SakuraEDL Admin - 宝塔面板部署指南

## 前置要求

- 宝塔面板 7.0+
- 服务器系统：CentOS 7+ / Ubuntu 18.04+

---

## 方式一：使用 Supervisor 进程守护（推荐）

### 步骤 1：上传文件

1. 打开宝塔面板 → **文件**
2. 进入 `/www/wwwroot/` 目录
3. 创建文件夹 `sakuraedl-admin`
4. 上传以下文件到该目录：
   - `sakuraedl-admin-linux-amd64` (重命名为 `sakuraedl-admin`)
   - `static/` 文件夹

目录结构：
```
/www/wwwroot/sakuraedl-admin/
├── sakuraedl-admin          # 主程序
├── static/
│   └── index.html            # 前端页面
├── uploads/                  # 自动创建
│   ├── loaders/
│   ├── digest/
│   └── sign/
└── data/                     # 自动创建
    └── sakuraedl.db
```

### 步骤 2：设置执行权限

1. 在宝塔文件管理器中，右键点击 `sakuraedl-admin`
2. 选择 **权限** → 设置为 `755`

或使用终端：
```bash
chmod +x /www/wwwroot/sakuraedl-admin/sakuraedl-admin
```

### 步骤 3：安装 Supervisor

1. 打开宝塔面板 → **软件商店**
2. 搜索 **Supervisor管理器**
3. 点击 **安装**

### 步骤 4：添加守护进程

1. 打开 **Supervisor管理器**
2. 点击 **添加守护进程**
3. 填写配置：

| 字段 | 值 |
|------|-----|
| 名称 | `sakuraedl-admin` |
| 启动用户 | `root` |
| 运行目录 | `/www/wwwroot/sakuraedl-admin` |
| 启动命令 | `/www/wwwroot/sakuraedl-admin/sakuraedl-admin` |
| 进程数量 | `1` |

4. 点击 **确定**

### 步骤 5：配置环境变量（可选）

如需修改默认配置，编辑 Supervisor 配置文件：

1. 打开 `/etc/supervisor/conf.d/sakuraedl-admin.conf`
2. 添加环境变量：

```ini
[program:sakuraedl-admin]
command=/www/wwwroot/sakuraedl-admin/sakuraedl-admin
directory=/www/wwwroot/sakuraedl-admin
user=root
autostart=true
autorestart=true
environment=ADMIN_TOKEN="your-secure-token",ADMIN_USER="admin",ADMIN_PASS="your-password"
```

3. 重启 Supervisor

### 步骤 6：配置反向代理

1. 打开宝塔面板 → **网站** → **添加站点**
2. 填写域名（如 `api.your-domain.com`）
3. 选择 **纯静态** 或 **PHP** 都可以
4. 点击 **提交**

5. 点击刚创建的站点 → **设置** → **反向代理**
6. 点击 **添加反向代理**：

| 字段 | 值 |
|------|-----|
| 代理名称 | `sakuraedl` |
| 目标URL | `http://127.0.0.1:8082` |
| 发送域名 | `$host` |

7. 点击 **提交**

### 步骤 7：配置 SSL（可选）

1. 点击站点 → **设置** → **SSL**
2. 选择 **Let's Encrypt** 或上传自己的证书
3. 点击 **申请** 或 **保存**
4. 开启 **强制HTTPS**

---

## 方式二：使用 Docker（如已安装）

### 步骤 1：安装 Docker

1. 打开宝塔面板 → **软件商店**
2. 搜索 **Docker管理器**
3. 点击 **安装**

### 步骤 2：上传文件

上传以下文件到 `/www/wwwroot/sakuraedl-admin/`：
- `Dockerfile`
- `docker-compose.yml`
- `main.go`
- `go.mod`
- `go.sum`
- `static/` 文件夹

### 步骤 3：构建并运行

在宝塔终端执行：
```bash
cd /www/wwwroot/sakuraedl-admin
docker-compose up -d --build
```

### 步骤 4：配置反向代理

同方式一的步骤 6

---

## 方式三：直接运行（测试用）

### 使用宝塔终端

```bash
cd /www/wwwroot/sakuraedl-admin
chmod +x sakuraedl-admin

# 后台运行
nohup ./sakuraedl-admin > app.log 2>&1 &

# 查看日志
tail -f app.log
```

---

## 防火墙配置

### 方式 A：仅内网访问（推荐）

不需要开放 8082 端口，通过 Nginx 反向代理访问

### 方式 B：直接访问

1. 打开宝塔面板 → **安全**
2. 添加端口规则：`8082`
3. 放行

---

## Nginx 配置优化

如需自定义 Nginx 配置，点击站点 → **配置文件**：

```nginx
location / {
    proxy_pass http://127.0.0.1:8082;
    proxy_http_version 1.1;
    proxy_set_header Host $host;
    proxy_set_header X-Real-IP $remote_addr;
    proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    proxy_set_header X-Forwarded-Proto $scheme;
    proxy_set_header Upgrade $http_upgrade;
    proxy_set_header Connection "upgrade";
    
    # 上传文件大小限制
    client_max_body_size 100M;
    
    # 超时设置
    proxy_connect_timeout 60s;
    proxy_send_timeout 60s;
    proxy_read_timeout 60s;
}
```

---

## 验证部署

### 检查服务状态

```bash
# 查看进程
ps aux | grep sakuraedl

# 查看端口
netstat -tlnp | grep 8082

# 测试 API
curl http://127.0.0.1:8082/api/loaders/list
```

### 访问管理后台

- 内网：`http://服务器IP:8082`
- 域名：`https://api.your-domain.com`

默认登录信息：
- 用户名：`admin`
- 密码：`admin123`

---

## 客户端配置

部署完成后，修改 SakuraEDL 客户端连接地址：

**文件：** `Qualcomm/Services/cloud_loader_service.cs`

```csharp
// 修改为你的服务器地址
private const string API_BASE_PROD = "https://api.your-domain.com/api";
```

然后重新编译客户端。

---

## 常见问题

### Q: 服务启动失败

```bash
# 查看 Supervisor 日志
tail -f /var/log/supervisor/sakuraedl-admin.log

# 或查看程序日志
tail -f /www/wwwroot/sakuraedl-admin/app.log
```

### Q: 502 Bad Gateway

1. 检查服务是否运行：`ps aux | grep sakuraedl`
2. 检查端口是否监听：`netstat -tlnp | grep 8082`
3. 重启服务：在 Supervisor 管理器中点击重启

### Q: 权限问题

```bash
chown -R www:www /www/wwwroot/sakuraedl-admin
chmod -R 755 /www/wwwroot/sakuraedl-admin
```

### Q: 数据库错误

确保 `data` 目录存在且有写入权限：
```bash
mkdir -p /www/wwwroot/sakuraedl-admin/data
chmod 777 /www/wwwroot/sakuraedl-admin/data
```

---

## 备份数据

在宝塔面板中设置定时任务：

1. 打开 **计划任务**
2. 添加任务：
   - 任务类型：Shell 脚本
   - 执行周期：每天
   - 脚本内容：

```bash
#!/bin/bash
DATE=$(date +%Y%m%d)
BACKUP_DIR=/www/backup/sakuraedl
mkdir -p $BACKUP_DIR
tar -czvf $BACKUP_DIR/sakuraedl-$DATE.tar.gz \
    /www/wwwroot/sakuraedl-admin/data \
    /www/wwwroot/sakuraedl-admin/uploads
# 删除 7 天前的备份
find $BACKUP_DIR -name "*.tar.gz" -mtime +7 -delete
```

---

## 更新部署

1. 停止服务（在 Supervisor 管理器中）
2. 上传新的 `sakuraedl-admin` 文件
3. 设置权限：`chmod +x sakuraedl-admin`
4. 启动服务

---

## 联系支持

- QQ 群：https://qm.qq.com/q/z3iVnkm22c
