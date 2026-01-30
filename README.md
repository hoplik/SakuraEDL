<p align="center">
  <img src="assets/logo.png" alt="SakuraEDL Logo" width="128">
</p>

# SakuraEDL

**一款开源的多功能安卓刷机工具**

支持高通 EDL (9008)、联发科 (MTK)、展讯 (SPD/Unisoc) 和 Fastboot 模式

[![License: CC BY-NC-SA 4.0](https://img.shields.io/badge/License-CC%20BY--NC--SA%204.0-lightgrey.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-4.8-blue.svg)](https://dotnet.microsoft.com/)
[![GitHub Stars](https://img.shields.io/github/stars/xiriovo/SakuraEDL)](https://github.com/xiriovo/SakuraEDL/stargazers)
[![GitHub Forks](https://img.shields.io/github/forks/xiriovo/SakuraEDL)](https://github.com/xiriovo/SakuraEDL/network/members)
[![GitHub Release](https://img.shields.io/github/v/release/xiriovo/SakuraEDL)](https://github.com/xiriovo/SakuraEDL/releases)

[中文文档](README.md) | [English](README_EN.md) | [快速参考](docs/QUICK_REFERENCE.md)

---

## 🎯 项目亮点

| 🚀 **多平台支持** | ⚡ **双协议引擎** | 🛠️ **功能全面** | ☁️ **云端匹配** |
|:---:|:---:|:---:|:---:|
| 高通 + MTK + 展讯 | XFlash + XML 协议 | 刷机 + 救砖 + 解密 | 自动匹配 Loader |

## 📸 界面预览

<p align="center">
  <img src="assets/screenshot.png" alt="SakuraEDL 界面截图" width="800">
</p>

---

## ✨ 功能特性

### 🆕 v3.0 新增功能

#### 🔧 联发科 (MTK) 全面支持
- **BROM/Preloader 模式刷机**
  - 自动检测 BROM 和 Preloader 模式
  - DA (Download Agent) 智能加载
  - 支持分离式 DA1 + DA2 文件
- **双协议引擎**
  - XFlash 二进制协议 (参考 mtkclient)
  - XML V6 协议 (兼容新设备)
  - 自动协议选择和回退
- **CRC32 校验和支持**
  - 数据传输完整性验证
  - 与 mtkclient 兼容
- **漏洞利用**
  - Carbonara 漏洞 (DA1 级别)
  - AllinoneSignature 漏洞 (DA2 级别)
  - 自动检测和执行

#### 📱 展讯 (SPD/Unisoc) 支持
- **FDL 下载协议**
  - FDL1/FDL2 自动下载
  - HDLC 帧编码
  - 动态波特率切换
- **PAC 固件解析**
  - 自动解析 PAC 包
  - 提取 FDL 和分区镜像
- **签名绕过 (T760/T770)**
  - `custom_exec_no_verify` 机制
  - 支持刷写未签名 FDL
- **芯片数据库**
  - SC9863A, T606, T610, T618
  - T700, T760 ✓已验证, T770
  - 自动地址配置

#### ☁️ 云端 Loader 匹配 (高通)
- **自动匹配**
  - 根据芯片 ID 自动获取 Loader
  - 无需本地 PAK 资源包
- **API 集成**
  - 云端 Loader 数据库
  - 实时更新支持

### 📊 协议对比

| 功能 | XML 协议 | XFlash 协议 |
|------|:--------:|:-----------:|
| 分区表读取 | ✅ | ✅ |
| 分区读取 | ✅ | ✅ |
| 分区写入 | ✅ | ✅ |
| CRC32 校验 | ❌ | ✅ |
| 兼容性 | 新设备 | 全设备 |

### 核心功能

#### 📱 高通 EDL (9008) 模式
- Sahara V2/V3 协议支持
- Firehose 协议增强刷写
- GPT 分区表备份/恢复
- 自动存储类型检测 (eMMC/UFS/NAND)
- OFP/OZIP/OPS 固件解密
- 智能密钥爆破 (50+ 组密钥)
- 🆕 原生 Diag 协议 (IMEI/MEID/QCN 读写)
- 🆕 Loader 特性检测 (自动分析支持的功能)
- 🆕 Motorola 固件包支持 (SINGLE_N_LONELY 格式)

#### ⚡ Fastboot 增强
- 分区读写操作
- OEM 解锁/重锁
- 设备信息查询
- 自定义命令执行
- 🆕 华为/荣耀设备完整支持
  - 设备信息读取 (IMEI/MEID/型号/固件版本)
  - FRP 解锁 (oem frp-unlock)
  - Device ID 获取 (用于解锁码计算)
  - Bootloader 解锁/锁定
  - EDL 模式重启

#### 🔧 联发科 (MTK)
- BROM/Preloader 模式
- XFlash + XML 双协议
- DA 自动加载
- 漏洞利用 (Carbonara/AllinoneSignature)

#### 📱 展讯 (SPD/Unisoc)
- FDL1/FDL2 下载
- PAC 固件解析
- T760/T770 签名绕过
- 🆕 ISP eMMC 直接访问
- 🆕 Bootloader 解锁/锁定
- 🆕 A/B 槽位切换
- 🆕 DM-Verity 控制
- 🆕 Boot.img 解析和设备信息提取
- 🆕 固件加解密
- 🆕 原生 Diag 协议 (IMEI/NV 读写)

#### 📦 固件工具
- Payload.bin 提取
- Super 分区合并
- Sparse/Raw 镜像转换
- rawprogram XML 解析

---

## 📋 系统要求

### 最低配置
- **操作系统**: Windows 10 (64-bit) 或更高版本
- **运行时**: .NET Framework 4.8
- **内存**: 4GB RAM
- **存储**: 500MB 可用空间

### 驱动要求
| 平台 | 驱动 | 用途 |
|------|------|------|
| 高通 | Qualcomm HS-USB | 9008 模式 |
| 联发科 | MediaTek PreLoader | BROM 模式 |
| 展讯 | SPRD USB | 下载模式 |
| 通用 | ADB/Fastboot | 调试模式 |

---

## 🚀 快速开始

### 安装步骤

1. **下载程序**
   - 从 [Releases](https://github.com/xiriovo/SakuraEDL/releases) 下载最新版本
   - 解压到任意目录（建议英文路径）

2. **安装驱动**
   - 根据设备平台安装对应驱动

3. **运行程序**
   ```
   SakuraEDL.exe
   ```

### 使用示例

#### 🔧 联发科 (MTK) 刷机

1. 选择 DA 文件 (或使用内置 DA)
2. 设备关机，按住音量键连接 USB
3. 程序自动完成：
   - BROM 握手
   - DA 加载 (XFlash/XML 协议)
   - 分区表读取
4. 选择分区进行读取/写入/擦除

#### 📱 展讯 (SPD) 刷机

1. 选择芯片型号 (如 T760)
2. 加载 PAC 固件或手动选择 FDL 文件
3. 设备进入下载模式
4. 点击"读取分区表"
5. 选择分区进行刷写

#### 🔐 高通 EDL 模式

1. 设备进入 9008 模式
2. 选择 Programmer 文件 (.mbn/.elf)
3. 选择固件包或分区镜像
4. 点击"开始刷写"

---

## 🛠️ 技术栈

- **运行时**: .NET Framework 4.8
- **UI 框架**: AntdUI
- **MTK 协议**: 参考 [mtkclient](https://github.com/bkerler/mtkclient)
- **SPD 协议**: 参考 [spd_dump](https://github.com/ArtRichards/spd_dump)

### 项目结构

```
SakuraEDL/
├── MediaTek/                   # 联发科模块
│   ├── Protocol/
│   │   ├── brom_client.cs      # BROM 客户端
│   │   ├── xml_da_client.cs    # XML V6 协议
│   │   ├── xflash_client.cs    # XFlash 二进制协议
│   │   └── xflash_commands.cs  # XFlash 命令码
│   ├── Common/
│   │   ├── mtk_crc32.cs        # CRC32 校验
│   │   └── mtk_checksum.cs     # 数据打包
│   ├── Services/
│   │   └── mediatek_service.cs # MTK 服务
│   ├── Exploit/
│   │   ├── carbonara_exploit.cs
│   │   └── AllinoneSignatureExploit.cs
│   └── Database/
│       └── mtk_chip_database.cs
├── Spreadtrum/                 # 展讯模块
│   ├── Protocol/
│   │   ├── fdl_client.cs       # FDL 客户端
│   │   ├── hdlc_protocol.cs    # HDLC 编码
│   │   ├── bsl_commands.cs     # BSL 命令
│   │   └── diag_client.cs      # 🆕 Diag 诊断协议
│   ├── Common/
│   │   ├── boot_parser.cs      # 🆕 Boot.img 解析
│   │   ├── cpio_parser.cs      # 🆕 CPIO 解析
│   │   ├── lz4_decompressor.cs # 🆕 LZ4 解压
│   │   ├── sprd_cryptograph.cs # 🆕 固件加解密
│   │   └── sprd_advanced_features.cs # 🆕 高级功能
│   ├── ISP/                    # 🆕 ISP eMMC 直接访问
│   │   ├── emmc_device.cs      # eMMC 设备操作
│   │   ├── emmc_gpt.cs         # GPT 分区解析
│   │   └── emmc_partition_manager.cs # 分区管理器
│   ├── Services/
│   │   └── spreadtrum_service.cs
│   └── Database/
│       └── sprd_fdl_database.cs
├── Qualcomm/                   # 高通模块
│   ├── Protocol/
│   │   ├── sahara_protocol.cs  # Sahara 协议
│   │   ├── firehose_client.cs  # Firehose 协议
│   │   └── diag_client.cs      # 🆕 Diag 诊断协议
│   ├── Common/
│   │   ├── loader_feature_detector.cs # 🆕 Loader 特性检测
│   │   └── motorola_support.cs # 🆕 Motorola 固件支持
│   └── Services/
│       ├── qualcomm_service.cs
│       └── cloud_loader_integration.cs  # 云端匹配
├── Fastboot/                   # Fastboot 模块
│   ├── Protocol/
│   │   ├── fastboot_protocol.cs # Fastboot 协议
│   │   └── fastboot_client.cs   # 原生客户端
│   ├── Vendor/                  # 🆕 厂商支持
│   │   └── huawei_honor_support.cs # 华为/荣耀支持
│   └── Services/
│       └── fastboot_service.cs
├── Common/                     # 通用模块
└── docs/                       # 文档
```

---

## 📊 支持的芯片

### 联发科 (MTK)
| 芯片 | HW Code | 漏洞 | 状态 |
|------|---------|------|------|
| MT6765 | 0x0766 | Carbonara | ✅ |
| MT6768 | 0x0788 | Carbonara | ✅ |
| MT6781 | 0x0813 | AllinoneSignature | ✅ |
| MT6833 | 0x0816 | AllinoneSignature | ✅ |
| MT6853 | 0x0788 | Carbonara | ✅ |

### 展讯 (SPD/Unisoc)
| 芯片 | exec_addr | 状态 |
|------|-----------|------|
| SC9863A | 0x5500 | ✅ |
| T606/T610/T618 | 0x5500 | ✅ |
| T700 | 0x65012f48 | ✅ |
| T760 | 0x65012f48 | ✅ 已验证 |
| T770 | 0x65012f48 | ✅ |

### 高通 (Qualcomm)
- SDM 系列 (660, 710, 845, 855, 865, 888)
- SM 系列 (8150, 8250, 8350, 8450, 8550)
- 云端自动匹配 Loader

---

## ❓ 常见问题

### MTK 设备无法识别？
- 确认已安装 MediaTek PreLoader 驱动
- 尝试关机后按住音量-连接
- 检查设备是否支持 BROM 模式

### SPD 设备签名验证失败？
- 确认 `custom_exec_no_verify_XXXXXXXX.bin` 文件存在
- 检查 FDL 地址配置是否正确
- T760/T770 需要特定漏洞文件

### XFlash 协议失败？
- 程序会自动回退到 XML 协议
- 检查 DA 文件是否完整
- 查看日志排查错误

---

## 📄 许可证

本项目采用 **非商业许可证** - 详见 [LICENSE](LICENSE) 文件

- ✅ 允许个人学习和研究使用
- ✅ 允许修改和分发（需保持相同许可）
- ❌ 禁止任何形式的商业用途
- ❌ 禁止出售或用于盈利

---

## 📧 联系方式

### 社区交流
- **QQ 群**: [SakuraEDL](https://qm.qq.com/q/z3iVnkm22c)
- **Telegram**: [@xiriery](https://t.me/xiriery)
- **Discord**: [加入服务器](https://discord.gg/sakuraedl)

### 开发者
- **GitHub**: [@xiriovo](https://github.com/xiriovo)
- **邮箱**: 1708298587@qq.com

---

## 🙏 致谢

- [mtkclient](https://github.com/bkerler/mtkclient) - MTK 协议参考
- [spd_dump](https://github.com/ArtRichards/spd_dump) - SPD 协议参考
- [edl](https://github.com/bkerler/edl) - Qualcomm EDL 参考

---

<p align="center">
  Made with ❤️ by SakuraEDL Team<br>
  Copyright © 2025-2026 SakuraEDL. All rights reserved.
</p>
