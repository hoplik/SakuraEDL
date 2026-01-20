@echo off
chcp 65001 >nul
echo ============================================
echo   MultiFlash TOOL 安装程序构建脚本
echo ============================================
echo.

:: 检查 Inno Setup 是否安装
set ISCC_PATH=
if exist "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" (
    set "ISCC_PATH=C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
) else if exist "C:\Program Files\Inno Setup 6\ISCC.exe" (
    set "ISCC_PATH=C:\Program Files\Inno Setup 6\ISCC.exe"
) else if exist "%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe" (
    set "ISCC_PATH=%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe"
)

if "%ISCC_PATH%"=="" (
    echo [错误] 未找到 Inno Setup 6
    echo.
    echo 请从以下地址下载安装 Inno Setup:
    echo https://jrsoftware.org/isdl.php
    echo.
    pause
    exit /b 1
)

echo [信息] 找到 Inno Setup: %ISCC_PATH%
echo.

:: 创建输出目录
if not exist "installer" mkdir installer

:: 编译安装程序
echo [构建] 正在编译安装程序...
"%ISCC_PATH%" setup.iss

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [错误] 编译失败！
    pause
    exit /b 1
)

echo.
echo ============================================
echo   构建完成！
echo   输出文件: installer\MultiFlash_Setup_v2.2.0.exe
echo ============================================
echo.
pause
