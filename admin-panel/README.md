# MultiFlash Admin Panel

后台管理面板，用于管理 Loader 文件和查看设备匹配日志。

## 功能特性

- 📊 **仪表盘**: 查看 Loader 统计、下载/匹配次数、最近匹配的设备
- 📁 **Loader 管理**: 列表查看、搜索、编辑、启用/禁用、删除
- 📤 **上传 Loader**: 支持上传自定义 Loader 文件
- 🔐 **VIP 验证**: VIP 类型需要额外上传 digest 和 sign 文件
- 📋 **设备日志**: 查看所有设备匹配记录

## 验证类型说明

| 类型 | 说明 | 需要的文件 |
|------|------|-----------|
| `none` | 无验证 | 仅 Loader 文件 |
| `miauth` | 小米验证 | 仅 Loader 文件 |
| `demacia` | 一加验证 | 仅 Loader 文件 |
| `vip` | VIP 验证 | Loader + Digest + Sign |

## 快速开始

### 1. 安装 Go 依赖

```bash
cd admin-panel/backend
go mod download
```

### 2. 启动后端服务

```bash
go run main.go
```

服务将在 `http://localhost:8081` 启动。

### 3. 访问管理面板

打开浏览器访问: http://localhost:8081

默认登录账号:
- 用户名: `admin`
- 密码: `multiflash2024`

## 环境变量配置

| 变量 | 默认值 | 说明 |
|------|--------|------|
| `ADMIN_USER` | admin | 管理员用户名 |
| `ADMIN_PASS` | multiflash2024 | 管理员密码 |
| `ADMIN_TOKEN` | multiflash-admin-2024 | API Token |

## API 接口

### 公开接口 (客户端使用)

| 方法 | 路径 | 说明 |
|------|------|------|
| POST | `/api/loaders/match` | 根据设备信息匹配 Loader |
| GET | `/api/loaders/{id}/download` | 下载 Loader 文件 |
| POST | `/api/device-logs` | 上报设备匹配日志 |

### 管理接口 (需要 Token)

| 方法 | 路径 | 说明 |
|------|------|------|
| POST | `/api/admin/login` | 登录获取 Token |
| GET | `/api/admin/loaders` | 获取 Loader 列表 |
| POST | `/api/admin/loaders/upload` | 上传新 Loader |
| GET | `/api/admin/loaders/{id}` | 获取单个 Loader 详情 |
| PUT | `/api/admin/loaders/{id}` | 更新 Loader 信息 |
| DELETE | `/api/admin/loaders/{id}` | 删除 Loader |
| POST | `/api/admin/loaders/{id}/enable` | 启用 Loader |
| POST | `/api/admin/loaders/{id}/disable` | 禁用 Loader |
| GET | `/api/admin/stats` | 获取统计数据 |
| GET | `/api/admin/logs` | 获取设备日志列表 |

### 请求示例

**匹配 Loader:**
```json
POST /api/loaders/match
{
    "msm_id": "009600E1",
    "pk_hash": "ABCD1234...",
    "oem_id": "0x0001",
    "storage_type": "ufs"
}
```

**上传 Loader (VIP 类型):**
```bash
curl -X POST http://localhost:8081/api/admin/loaders/upload \
  -H "X-Admin-Token: multiflash-admin-2024" \
  -F "loader=@prog_firehose.elf" \
  -F "digest=@loader.digest" \
  -F "sign=@loader.sign" \
  -F "vendor=Xiaomi" \
  -F "chip=SM8550" \
  -F "hw_id=009600E1" \
  -F "auth_type=vip" \
  -F "storage_type=ufs"
```

## 目录结构

```
admin-panel/
├── backend/
│   ├── main.go          # Go 后端主程序
│   ├── go.mod           # Go 模块配置
│   ├── multiflash.db    # SQLite 数据库 (运行后生成)
│   ├── uploads/         # 上传文件目录 (运行后生成)
│   │   ├── loaders/     # Loader 文件
│   │   ├── digest/      # Digest 文件
│   │   └── sign/        # Sign 文件
│   └── static/
│       └── index.html   # 前端管理界面
└── README.md
```

## 数据库结构

### loaders 表
- `id` - 主键
- `filename` - 文件名
- `vendor` - 厂商
- `chip` - 芯片型号
- `hw_id` - 硬件 ID (MSM ID)
- `pk_hash` - PK Hash (用于精确匹配)
- `oem_id` - OEM ID
- `auth_type` - 验证类型 (none/miauth/demacia/vip)
- `storage_type` - 存储类型 (ufs/emmc)
- `file_size` - 文件大小
- `file_md5` - 文件 MD5
- `file_path` - 文件路径
- `digest_path` - Digest 文件路径
- `sign_path` - Sign 文件路径
- `is_enabled` - 是否启用
- `downloads` - 下载次数
- `match_count` - 匹配次数
- `notes` - 备注
- `created_at` - 创建时间
- `updated_at` - 更新时间

### device_logs 表
- `id` - 主键
- `platform` - 平台
- `msm_id` - MSM ID
- `pk_hash` - PK Hash
- `oem_id` - OEM ID
- `storage_type` - 存储类型
- `match_result` - 匹配结果
- `loader_id` - 关联的 Loader ID
- `client_ip` - 客户端 IP
- `user_agent` - User Agent
- `created_at` - 创建时间

## 生产环境部署

1. 编译 Go 程序:
```bash
CGO_ENABLED=1 go build -o multiflash-admin main.go
```

2. 设置环境变量:
```bash
export ADMIN_USER=your_admin
export ADMIN_PASS=your_password
export ADMIN_TOKEN=your_secure_token
```

3. 使用 systemd 或 supervisor 管理服务

4. 配置 Nginx 反向代理 (推荐):
```nginx
server {
    listen 443 ssl;
    server_name api.example.com;
    
    location / {
        proxy_pass http://127.0.0.1:8081;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    }
}
```
