@echo off
chcp 65001 >nul
echo ============================================
echo   MultiFlash TOOL 便携版打包脚本
echo ============================================
echo.

set VERSION=2.2.0
set OUTPUT_DIR=portable
set PACKAGE_NAME=MultiFlash_Portable_v%VERSION%

:: 清理旧的输出目录
if exist "%OUTPUT_DIR%\%PACKAGE_NAME%" rd /s /q "%OUTPUT_DIR%\%PACKAGE_NAME%"
if not exist "%OUTPUT_DIR%" mkdir "%OUTPUT_DIR%"
mkdir "%OUTPUT_DIR%\%PACKAGE_NAME%"

echo [复制] 正在复制文件...

:: 复制主程序
copy "bin\Release\MultiFlash.exe" "%OUTPUT_DIR%\%PACKAGE_NAME%\" >nul
copy "bin\Release\MultiFlash.exe.config" "%OUTPUT_DIR%\%PACKAGE_NAME%\" >nul

:: 复制 UI 库
copy "bin\Release\AntdUI.dll" "%OUTPUT_DIR%\%PACKAGE_NAME%\" >nul
copy "bin\Release\SunnyUI.dll" "%OUTPUT_DIR%\%PACKAGE_NAME%\" >nul
copy "bin\Release\SunnyUI.Common.dll" "%OUTPUT_DIR%\%PACKAGE_NAME%\" >nul
copy "bin\Release\HandyControl.dll" "%OUTPUT_DIR%\%PACKAGE_NAME%\" >nul

:: 复制资源包（如果存在）
if exist "bin\Release\edl_loaders.pak" copy "bin\Release\edl_loaders.pak" "%OUTPUT_DIR%\%PACKAGE_NAME%\" >nul
if exist "bin\Release\firehose.pak" copy "bin\Release\firehose.pak" "%OUTPUT_DIR%\%PACKAGE_NAME%\" >nul

:: 复制图标
copy "MultiFlash TOOL.ico" "%OUTPUT_DIR%\%PACKAGE_NAME%\" >nul

:: 创建说明文件
echo MultiFlash TOOL v%VERSION% 便携版 > "%OUTPUT_DIR%\%PACKAGE_NAME%\说明.txt"
echo. >> "%OUTPUT_DIR%\%PACKAGE_NAME%\说明.txt"
echo 使用说明: >> "%OUTPUT_DIR%\%PACKAGE_NAME%\说明.txt"
echo 1. 确保已安装 .NET Framework 4.8 >> "%OUTPUT_DIR%\%PACKAGE_NAME%\说明.txt"
echo 2. 直接运行 MultiFlash.exe >> "%OUTPUT_DIR%\%PACKAGE_NAME%\说明.txt"
echo. >> "%OUTPUT_DIR%\%PACKAGE_NAME%\说明.txt"
echo 系统要求: >> "%OUTPUT_DIR%\%PACKAGE_NAME%\说明.txt"
echo - Windows 7/8/10/11 >> "%OUTPUT_DIR%\%PACKAGE_NAME%\说明.txt"
echo - .NET Framework 4.8 >> "%OUTPUT_DIR%\%PACKAGE_NAME%\说明.txt"
echo. >> "%OUTPUT_DIR%\%PACKAGE_NAME%\说明.txt"
echo GitHub: https://github.com/xiriovo/edltool >> "%OUTPUT_DIR%\%PACKAGE_NAME%\说明.txt"

echo.
echo [信息] 文件已复制到: %OUTPUT_DIR%\%PACKAGE_NAME%\
echo.

:: 检查是否有 7z
where 7z >nul 2>&1
if %ERRORLEVEL% EQU 0 (
    echo [打包] 正在创建压缩包...
    cd "%OUTPUT_DIR%"
    7z a -t7z -mx=9 "%PACKAGE_NAME%.7z" "%PACKAGE_NAME%\*" >nul
    cd ..
    echo [完成] 压缩包: %OUTPUT_DIR%\%PACKAGE_NAME%.7z
) else (
    echo [提示] 未找到 7z，跳过压缩。
    echo        请手动压缩 %OUTPUT_DIR%\%PACKAGE_NAME% 文件夹
)

echo.
echo ============================================
echo   便携版打包完成！
echo ============================================
echo.
pause
