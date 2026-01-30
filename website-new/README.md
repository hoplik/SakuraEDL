# SakuraEDL 官网 (Vue + Go)

使用 Vue 3 + Go 重写的 SakuraEDL 官网，模仿 VitePress 风格，对接 api.sakuraedl.org 统计数据。

## 项目结构

```
website-new/
├── frontend/           # Vue 3 前端
│   ├── src/
│   │   ├── api/        # API 调用
│   │   ├── components/ # 通用组件
│   │   ├── router/     # 路由配置
│   │   ├── styles/     # 样式文件
│   │   └── views/      # 页面组件
│   └── package.json
└── backend/            # Go 后端
    ├── main.go         # 服务入口
    ├── static/         # 前端编译产物
    └── go.mod
```

## 功能特性

- 🎨 VitePress 风格 UI
- 📊 实时统计数据展示
- 📱 响应式设计
- 🌙 暗色模式支持
- 🔄 API 代理转发

## 开发

### 前端开发

```bash
cd frontend
npm install
npm run dev
```

### 后端开发

```bash
cd backend
go run main.go
```

## 构建部署

### 1. 构建前端

```bash
cd frontend
npm run build
```

产物输出到 `backend/static/`

### 2. 编译后端

```bash
cd backend

# Linux
GOOS=linux GOARCH=amd64 CGO_ENABLED=0 go build -o sakuraedl-website .

# Windows
set GOOS=linux
set GOARCH=amd64
set CGO_ENABLED=0
go build -o sakuraedl-website .
```

### 3. 部署

上传 `sakuraedl-website` 和 `static/` 目录到服务器：

```bash
# 设置环境变量
export PORT=8080
export API_BASE_URL=https://api.sakuraedl.org/api

# 运行
./sakuraedl-website
```

### Nginx 配置

```nginx
server {
    listen 80;
    server_name sakuraedl.org;
    
    location / {
        proxy_pass http://127.0.0.1:8080;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
    }
}
```

## 页面说明

| 路由 | 说明 |
|------|------|
| `/` | 首页，展示功能介绍和实时统计 |
| `/guide/*` | 使用教程 |
| `/download` | 下载页面 |
| `/api` | API 文档 |
| `/stats` | 详细统计数据 |

## API 代理

后端会将 `/api/*` 请求代理到 `api.sakuraedl.org`，解决跨域问题。
