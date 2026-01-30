# SakuraEDL Admin - 宝塔面板部署指南 (MySQL 版)

## 前置要求

- 宝塔面板 7.0+
- 服务器系统：CentOS 7+ / Ubuntu 18.04+
- MySQL 5.7+ 或 MySQL 8.0

---

## 步骤 1：安装 MySQL

1. 打开宝塔面板 → **软件商店**
2. 搜索 **MySQL** 并安装（推荐 MySQL 8.0）
3. 安装完成后点击 **设置** → 记住 root 密码

---

## 步骤 2：创建数据库

### 方式 A：通过宝塔面板（推荐）

1. 打开宝塔面板 → **数据库**
2. 点击 **添加数据库**
3. 填写：

| 字段 | 值 |
|------|-----|
| 数据库名 | `sakuraedl` |
| 用户名 | `sakuraedl` |
| 密码 | `071123gan` |
| 访问权限 | 本地服务器 |

4. 点击 **提交**

### 方式 B：通过命令行

```bash
mysql -u root -p

# 执行以下 SQL
CREATE DATABASE sakuraedl CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE USER 'sakuraedl'@'localhost' IDENTIFIED BY 'your_password_here';
GRANT ALL PRIVILEGES ON sakuraedl.* TO 'sakuraedl'@'localhost';
FLUSH PRIVILEGES;
EXIT;
```

---

## 步骤 3：上传程序文件

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
│   └── index.html           # 前端页面
└── uploads/                 # 自动创建
    ├── loaders/
    ├── digest/
    └── sign/
```

---

## 步骤 4：设置执行权限

```bash
chmod +x /www/wwwroot/sakuraedl-admin/sakuraedl-admin
```

---

## 步骤 5：配置 Supervisor 守护进程

### 安装 Supervisor

1. 打开宝塔面板 → **软件商店**
2. 搜索 **Supervisor管理器**
3. 点击 **安装**

### 添加守护进程

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

4. **重要：添加环境变量**

在 Supervisor 配置中添加环境变量（编辑 `/etc/supervisor/conf.d/sakuraedl-admin.conf`）：

```ini
[program:sakuraedl-admin]
command=/www/wwwroot/sakuraedl-admin/sakuraedl-admin
directory=/www/wwwroot/sakuraedl-admin
user=root
autostart=true
autorestart=true
startsecs=3
startretries=3
redirect_stderr=true
stdout_logfile=/www/wwwlogs/sakuraedl-admin.log
environment=DB_HOST="127.0.0.1",DB_PORT="3306",DB_USER="sakuraedl",DB_PASS="071123gan",DB_NAME="sakuraedl",ADMIN_TOKEN="sakuraedl-admin-2024",ADMIN_USER="admin",ADMIN_PASS="sakuraedl2024"
```

**⚠️ 请将 `your_db_password` 替换为步骤 2 中设置的数据库密码！**

5. 重启 Supervisor：
```bash
supervisorctl reread
supervisorctl update
supervisorctl restart sakuraedl-admin
```

---

## 步骤 6：配置反向代理

1. 打开宝塔面板 → **网站** → **添加站点**
2. 填写域名（如 `api.sakuraedl.org`）
3. 选择 **纯静态**
4. 点击 **提交**

5. 点击站点 → **设置** → **反向代理**
6. 点击 **添加反向代理**：

| 字段 | 值 |
|------|-----|
| 代理名称 | `sakuraedl` |
| 目标URL | `http://127.0.0.1:8082` |
| 发送域名 | `$host` |

7. 点击 **提交**

### 配置 Nginx（重要）

点击站点 → **设置** → **配置文件**，在 `server {}` 块内添加：

```nginx
# 文件上传大小限制
client_max_body_size 100M;
```

---

## 步骤 7：配置 SSL（可选）

1. 点击站点 → **设置** → **SSL**
2. 选择 **Let's Encrypt**
3. 点击 **申请**
4. 开启 **强制HTTPS**

---

## 验证部署

### 检查服务状态

```bash
# 查看进程
ps aux | grep sakuraedl

# 查看端口
netstat -tlnp | grep 8082

# 查看日志
tail -f /www/wwwlogs/sakuraedl-admin.log

# 测试 API
curl http://127.0.0.1:8082/api/loaders/list
```

### 检查数据库连接

```bash
mysql -u sakuraedl -p -e "SHOW TABLES FROM sakuraedl;"
```

应该显示：
```
+---------------------+
| Tables_in_sakuraedl |
+---------------------+
| device_logs         |
| loaders             |
+---------------------+
```

### 访问管理后台

- 地址：`https://api.sakuraedl.org`
- 用户名：`admin`
- 密码：`sakuraedl2024`（或你设置的密码）

---

## 环境变量说明

| 变量 | 默认值 | 说明 |
|------|--------|------|
| `DB_HOST` | `127.0.0.1` | MySQL 主机 |
| `DB_PORT` | `3306` | MySQL 端口 |
| `DB_USER` | `sakuraedl` | MySQL 用户名 |
| `DB_PASS` | `sakuraedl2024` | MySQL 密码 |
| `DB_NAME` | `sakuraedl` | 数据库名 |
| `ADMIN_USER` | `admin` | 管理后台用户名 |
| `ADMIN_PASS` | `sakuraedl2024` | 管理后台密码 |
| `ADMIN_TOKEN` | `sakuraedl-admin-2024` | API Token |

---

## 常见问题

### Q: 数据库连接失败

```
数据库连接测试失败: dial tcp 127.0.0.1:3306: connect: connection refused
```

**解决：**
1. 检查 MySQL 是否运行：`systemctl status mysql`
2. 检查用户名密码是否正确
3. 检查用户是否有权限连接

### Q: 表不存在

程序启动时会自动创建表。如果表不存在，检查日志中是否有错误。

### Q: 502 Bad Gateway

1. 检查服务是否运行：`ps aux | grep sakuraedl`
2. 检查日志：`tail -f /www/wwwlogs/sakuraedl-admin.log`
3. 重启服务：`supervisorctl restart sakuraedl-admin`

### Q: 上传文件失败

确保 Nginx 配置中有：
```nginx
client_max_body_size 100M;
```

---

## 备份数据

### MySQL 数据备份

在宝塔面板 → **计划任务** 中添加：

```bash
#!/bin/bash
DATE=$(date +%Y%m%d_%H%M%S)
BACKUP_DIR=/www/backup/sakuraedl
mkdir -p $BACKUP_DIR

# 备份数据库
mysqldump -u sakuraedl -p'your_password' sakuraedl > $BACKUP_DIR/db_$DATE.sql

# 备份上传文件
tar -czvf $BACKUP_DIR/uploads_$DATE.tar.gz /www/wwwroot/sakuraedl-admin/uploads

# 删除 7 天前的备份
find $BACKUP_DIR -name "*.sql" -mtime +7 -delete
find $BACKUP_DIR -name "*.tar.gz" -mtime +7 -delete
```

---

## 联系支持

- QQ 群：https://qm.qq.com/q/z3iVnkm22c
