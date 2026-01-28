#!/bin/bash
# MultiFlash 文档站构建脚本

echo "=========================================="
echo "  MultiFlash 文档站构建"
echo "=========================================="

# 安装依赖
echo ""
echo "[1/3] 安装依赖..."
npm install

# 构建
echo ""
echo "[2/3] 构建文档..."
npm run build

# 输出
echo ""
echo "[3/3] 构建完成!"
echo ""
echo "输出目录: .vitepress/dist"
echo ""
echo "部署方式:"
echo "  1. 将 .vitepress/dist 目录上传到服务器"
echo "  2. 配置 Nginx 指向该目录"
echo ""
