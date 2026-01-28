#!/bin/bash
# SakuraEDL Admin 构建脚本

set -e

VERSION="3.0.0"
BUILD_TIME=$(date +%Y%m%d_%H%M%S)
OUTPUT_DIR="./dist"

echo "=========================================="
echo "  SakuraEDL Admin 构建脚本"
echo "  版本: $VERSION"
echo "=========================================="

# 创建输出目录
mkdir -p $OUTPUT_DIR

# 编译 Linux AMD64
echo ""
echo "[1/4] 编译 Linux AMD64..."
GOOS=linux GOARCH=amd64 CGO_ENABLED=0 go build -ldflags="-s -w" -o $OUTPUT_DIR/sakuraedl-admin-linux-amd64 .
echo "      完成: $OUTPUT_DIR/sakuraedl-admin-linux-amd64"

# 编译 Linux ARM64
echo ""
echo "[2/4] 编译 Linux ARM64..."
GOOS=linux GOARCH=arm64 CGO_ENABLED=0 go build -ldflags="-s -w" -o $OUTPUT_DIR/sakuraedl-admin-linux-arm64 .
echo "      完成: $OUTPUT_DIR/sakuraedl-admin-linux-arm64"

# 编译 Windows
echo ""
echo "[3/4] 编译 Windows AMD64..."
GOOS=windows GOARCH=amd64 CGO_ENABLED=0 go build -ldflags="-s -w" -o $OUTPUT_DIR/sakuraedl-admin-windows-amd64.exe .
echo "      完成: $OUTPUT_DIR/sakuraedl-admin-windows-amd64.exe"

# 复制静态文件
echo ""
echo "[4/4] 复制静态文件..."
cp -r static $OUTPUT_DIR/
mkdir -p $OUTPUT_DIR/uploads/{loaders,digest,sign}
mkdir -p $OUTPUT_DIR/data

# 创建部署包
echo ""
echo "创建部署包..."
cd $OUTPUT_DIR

# Linux 部署包
tar -czvf sakuraedl-admin-linux-amd64-$VERSION.tar.gz \
    sakuraedl-admin-linux-amd64 static uploads data

tar -czvf sakuraedl-admin-linux-arm64-$VERSION.tar.gz \
    sakuraedl-admin-linux-arm64 static uploads data

# Windows 部署包
zip -r sakuraedl-admin-windows-$VERSION.zip \
    sakuraedl-admin-windows-amd64.exe static uploads data

cd ..

echo ""
echo "=========================================="
echo "  构建完成!"
echo "=========================================="
echo ""
echo "输出文件:"
ls -la $OUTPUT_DIR/*.tar.gz $OUTPUT_DIR/*.zip 2>/dev/null || true
echo ""
