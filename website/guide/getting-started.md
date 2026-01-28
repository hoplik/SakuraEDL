# 快速开始

## 下载安装

### 系统要求

- Windows 10/11 64位
- .NET Framework 4.8+
- USB 驱动（高通9008/MTK/展锐）

### 下载地址

- **官方下载**: [GitHub Releases](https://github.com/xiriovo/SakuraEDL/releases)
- **蓝奏云**: [点击下载](https://www.lanzoui.com/sakuraedl)

### 安装步骤

1. 下载 `SakuraEDL-Setup-v3.0.exe`
2. 双击运行安装程序
3. 按提示完成安装
4. 安装 USB 驱动（首次使用）

---

## 驱动安装

### 高通 9008 驱动

1. 下载 [Qualcomm HS-USB Driver](https://androiddatahost.com/qualcomm-hs-usb-qdloader-9008-driver/)
2. 以管理员身份运行安装
3. 重启电脑

### MTK 驱动

1. 下载 [MTK USB VCOM Driver](https://androiddatahost.com/mtk-usb-all-drivers/)
2. 安装 `MTK_USB_Driver_v1.0.8.zip`
3. 重启电脑

### 展锐驱动

1. 下载 [Spreadtrum USB Driver](https://androiddatahost.com/spreadtrum-usb-drivers/)
2. 安装驱动
3. 重启电脑

---

## 进入刷机模式

### 高通 EDL 模式 (9008)

**方法一：组合键**
1. 关机
2. 同时按住 **音量+** + **音量-**
3. 插入 USB 数据线
4. 设备管理器显示 `Qualcomm HS-USB QDLoader 9008`

**方法二：ADB 命令**
```bash
adb reboot edl
```

**方法三：Fastboot 命令**
```bash
fastboot oem edl
```

### MTK BROM 模式

1. 关机并取出电池（如可拆卸）
2. 按住 **音量+** 或 **音量-**
3. 插入 USB 数据线
4. 设备管理器显示 `MediaTek USB Port`

### 展锐下载模式

1. 关机
2. 按住 **音量-**
3. 插入 USB 数据线
4. 设备管理器显示 `SPRD U2S Diag`

### Fastboot 模式

1. 关机
2. 按住 **音量-** + **电源键**
3. 出现 Fastboot 界面后松开

---

## 基本使用

### 1. 选择平台

打开 SakuraEDL，根据你的设备选择对应平台：
- **Qualcomm** - 高通芯片设备
- **MediaTek** - 联发科芯片设备
- **Spreadtrum** - 展锐芯片设备
- **Fastboot** - Fastboot 模式

### 2. 连接设备

1. 让设备进入对应的刷机模式
2. 软件会自动检测到设备
3. 显示设备信息

### 3. 选择操作

根据需要选择：
- **刷写分区** - 刷入指定镜像
- **读取分区** - 备份分区数据
- **擦除分区** - 清除分区数据
- **解锁** - 解锁 Bootloader

### 4. 执行操作

1. 选择要刷写的文件
2. 点击开始
3. 等待完成

---

## 下一步

- [高通 EDL 详细教程](/guide/qualcomm)
- [MTK 详细教程](/guide/mtk)
- [展锐详细教程](/guide/spd)
- [Fastboot 详细教程](/guide/fastboot)
