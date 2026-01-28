# 安装说明

## 下载软件

从以下渠道下载最新版本：

- [GitHub Releases](https://github.com/xiriovo/SakuraEDL/releases)
- [蓝奏云](https://www.lanzoui.com/multiflash)

## 系统要求

| 项目 | 最低要求 | 推荐配置 |
|------|----------|----------|
| 操作系统 | Windows 10 64位 | Windows 11 64位 |
| 处理器 | Intel Core i3 | Intel Core i5+ |
| 内存 | 4GB | 8GB+ |
| 磁盘空间 | 200MB | 1GB+ |
| 运行库 | .NET Framework 4.8 | .NET Framework 4.8 |

## 安装步骤

### 1. 运行安装程序

1. 右键点击 `SakuraEDL-Setup-v3.0.exe`
2. 选择 **以管理员身份运行**
3. 按向导提示完成安装

### 2. 安装运行库

如果提示缺少 .NET Framework：

1. 下载 [.NET Framework 4.8](https://dotnet.microsoft.com/download/dotnet-framework/net48)
2. 安装后重启电脑

### 3. 安装 USB 驱动

根据你要刷的设备平台，安装对应驱动：

#### 高通驱动 (9008)

1. 下载 [Qualcomm USB Driver](https://androiddatahost.com/qualcomm-hs-usb-qdloader-9008-driver/)
2. 解压并运行安装程序
3. 重启电脑

#### MTK 驱动

1. 下载 [MTK USB Driver](https://androiddatahost.com/mtk-usb-all-drivers/)
2. 解压到任意目录
3. 设备管理器中手动安装

#### 展锐驱动

1. 下载 [Spreadtrum USB Driver](https://androiddatahost.com/spreadtrum-usb-drivers/)
2. 运行安装程序
3. 重启电脑

#### ADB/Fastboot 驱动

1. 下载 [Google USB Driver](https://developer.android.com/studio/run/win-usb)
2. 或使用 [Universal ADB Driver](https://adb.clockworkmod.com/)

## 便携版使用

如果下载的是便携版（Portable）：

1. 解压到任意目录
2. 直接运行 `SakuraEDL.exe`
3. 首次运行可能需要安装驱动

## 验证安装

1. 运行 SakuraEDL
2. 连接设备（进入对应刷机模式）
3. 软件应自动检测到设备

## 卸载

### 安装版

1. 打开 **设置** → **应用**
2. 找到 SakuraEDL
3. 点击 **卸载**

### 便携版

直接删除程序目录即可。

## 常见安装问题

### 提示缺少 DLL

安装 [Visual C++ Redistributable](https://aka.ms/vs/17/release/vc_redist.x64.exe)

### 杀毒软件报警

SakuraEDL 是开源软件，部分杀软可能误报。请添加到白名单。

### 驱动安装失败

1. 关闭驱动签名验证
2. 以管理员身份安装
3. 尝试使用 [Zadig](https://zadig.akeo.ie/) 安装通用驱动
