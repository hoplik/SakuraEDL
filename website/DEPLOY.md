# SakuraEDL 文档站部署指南

## 本地构建

### 前置要求

- Node.js 18+
- npm 或 pnpm

### 构建步骤

```bash
cd website

# 安装依赖
npm install

# 本地预览
npm run dev

# 构建生产版本
npm run build
```

构建产物在 `.vitepress/dist` 目录。

---

## 宝塔面板部署

### 方式一：Node.js 项目（推荐开发）

1. 上传 `website` 目录到服务器
2. 在宝塔 Node.js 管理器中添加项目
3. 启动命令：`npm run dev`
4. 端口：5173

### 方式二：纯静态部署（推荐生产）

1. 本地构建：`npm run build`
2. 上传 `.vitepress/dist` 目录到服务器
3. 在宝塔添加静态站点
4. 根目录指向 dist 文件夹

### Nginx 配置

```nginx
server {
    listen 80;
    server_name docs.sakuraedl.org;
    
    root /www/wwwroot/sakuraedl-docs;
    index index.html;
    
    location / {
        try_files $uri $uri/ /index.html;
    }
    
    # 缓存静态资源
    location ~* \.(js|css|png|jpg|jpeg|gif|ico|svg|woff|woff2)$ {
        expires 30d;
        add_header Cache-Control "public, immutable";
    }
}
```

---

## 完整部署架构

```
sakuraedl.org          → 主站/文档 (VitePress)
api.sakuraedl.org      → 后端 API (Go)
admin.sakuraedl.org    → 管理后台 (已内置在后端)
```

### 推荐配置

| 站点 | 类型 | 端口 |
|------|------|------|
| 主站/文档 | 静态 | 80/443 |
| 后端 API | Go 项目 | 8082 |

---

## GitHub Pages 部署（可选）

1. 在 GitHub 仓库设置中启用 Pages
2. 添加 GitHub Actions 工作流：

```yaml
# .github/workflows/deploy.yml
name: Deploy Docs

on:
  push:
    branches: [main]
    paths:
      - 'website/**'

jobs:
  deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      
      - name: Setup Node
        uses: actions/setup-node@v4
        with:
          node-version: 18
          
      - name: Build
        run: |
          cd website
          npm install
          npm run build
          
      - name: Deploy
        uses: peaceiris/actions-gh-pages@v3
        with:
          github_token: ${{ secrets.GITHUB_TOKEN }}
          publish_dir: website/.vitepress/dist
```

---

## 目录结构

```
website/
├── .vitepress/
│   ├── config.js      # VitePress 配置
│   └── dist/          # 构建输出
├── public/
│   └── logo.png       # 网站 Logo
├── guide/
│   ├── getting-started.md
│   ├── qualcomm.md
│   ├── mtk.md
│   ├── spd.md
│   └── fastboot.md
├── api/
│   └── index.md
├── index.md           # 首页
├── download.md        # 下载页
└── package.json
```

---

## 更新文档

1. 修改对应的 `.md` 文件
2. 本地预览：`npm run dev`
3. 构建：`npm run build`
4. 上传 dist 目录到服务器
