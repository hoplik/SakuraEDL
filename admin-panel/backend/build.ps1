# SakuraEDL Admin 构建脚本 (Windows PowerShell)

$VERSION = "3.0.0"
$OUTPUT_DIR = "./dist"

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "  SakuraEDL Admin 构建脚本" -ForegroundColor Cyan
Write-Host "  版本: $VERSION" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

# 创建输出目录
New-Item -ItemType Directory -Force -Path $OUTPUT_DIR | Out-Null
New-Item -ItemType Directory -Force -Path "$OUTPUT_DIR/uploads/loaders" | Out-Null
New-Item -ItemType Directory -Force -Path "$OUTPUT_DIR/uploads/digest" | Out-Null
New-Item -ItemType Directory -Force -Path "$OUTPUT_DIR/uploads/sign" | Out-Null
New-Item -ItemType Directory -Force -Path "$OUTPUT_DIR/data" | Out-Null

# 编译 Linux AMD64
Write-Host ""
Write-Host "[1/4] 编译 Linux AMD64..." -ForegroundColor Yellow
$env:GOOS = "linux"
$env:GOARCH = "amd64"
$env:CGO_ENABLED = "0"
go build -ldflags="-s -w" -o "$OUTPUT_DIR/sakuraedl-admin-linux-amd64" .
Write-Host "      完成: $OUTPUT_DIR/sakuraedl-admin-linux-amd64" -ForegroundColor Green

# 编译 Linux ARM64
Write-Host ""
Write-Host "[2/4] 编译 Linux ARM64..." -ForegroundColor Yellow
$env:GOOS = "linux"
$env:GOARCH = "arm64"
$env:CGO_ENABLED = "0"
go build -ldflags="-s -w" -o "$OUTPUT_DIR/sakuraedl-admin-linux-arm64" .
Write-Host "      完成: $OUTPUT_DIR/sakuraedl-admin-linux-arm64" -ForegroundColor Green

# 编译 Windows
Write-Host ""
Write-Host "[3/4] 编译 Windows AMD64..." -ForegroundColor Yellow
$env:GOOS = "windows"
$env:GOARCH = "amd64"
$env:CGO_ENABLED = "0"
go build -ldflags="-s -w" -o "$OUTPUT_DIR/sakuraedl-admin-windows-amd64.exe" .
Write-Host "      完成: $OUTPUT_DIR/sakuraedl-admin-windows-amd64.exe" -ForegroundColor Green

# 复制静态文件
Write-Host ""
Write-Host "[4/4] 复制静态文件..." -ForegroundColor Yellow
Copy-Item -Path "static" -Destination "$OUTPUT_DIR/" -Recurse -Force
Write-Host "      完成" -ForegroundColor Green

# 清理环境变量
Remove-Item Env:GOOS -ErrorAction SilentlyContinue
Remove-Item Env:GOARCH -ErrorAction SilentlyContinue
Remove-Item Env:CGO_ENABLED -ErrorAction SilentlyContinue

Write-Host ""
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "  构建完成!" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "输出文件:" -ForegroundColor White
Get-ChildItem -Path $OUTPUT_DIR -Filter "sakuraedl-admin*" | Format-Table Name, Length -AutoSize
