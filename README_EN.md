<p align="center">
  <img src="assets/logo.png" alt="SakuraEDL Logo" width="128">
</p>

# SakuraEDL

**An Open-Source Multi-Platform Android Flashing Tool**

Supports Qualcomm EDL (9008), MediaTek (MTK), Spreadtrum (SPD/Unisoc), and Fastboot modes

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-4.8-blue.svg)](https://dotnet.microsoft.com/)
[![GitHub Stars](https://img.shields.io/github/stars/xiriovo/SakuraEDL)](https://github.com/xiriovo/SakuraEDL/stargazers)
[![GitHub Release](https://img.shields.io/github/v/release/xiriovo/SakuraEDL)](https://github.com/xiriovo/SakuraEDL/releases)

[中文](README.md) | [English](README_EN.md) | [日本語](README_JA.md) | [한국어](README_KO.md) | [Русский](README_RU.md) | [Español](README_ES.md)

---

## 🎯 Highlights

| 🚀 **Multi-Platform** | ⚡ **Dual Protocol** | 🛠️ **Full-Featured** | ☁️ **Cloud Matching** |
|:---:|:---:|:---:|:---:|
| Qualcomm + MTK + SPD | XFlash + XML Protocol | Flash + Unbrick + Decrypt | Auto Loader Matching |

---

## ✨ Features

### 🆕 v3.0 New Features

#### 🔧 MediaTek (MTK) Full Support
- **BROM/Preloader Mode Flashing**
  - Auto-detect BROM and Preloader modes
  - Smart DA (Download Agent) loading
  - Support for separate DA1 + DA2 files
- **Dual Protocol Engine**
  - XFlash binary protocol (based on mtkclient)
  - XML V6 protocol (for newer devices)
  - Auto protocol selection and fallback
- **Exploits**
  - Carbonara exploit (DA1 level)
  - AllinoneSignature exploit (DA2 level)
  - Auto detection and execution

#### 📱 Spreadtrum (SPD/Unisoc) Support
- **FDL Download Protocol**
  - Auto FDL1/FDL2 download
  - HDLC frame encoding
  - Dynamic baud rate switching
- **PAC Firmware Parsing**
  - Auto parse PAC packages
  - Extract FDL and partition images
- **Signature Bypass (T760/T770)**
  - `custom_exec_no_verify` mechanism
  - Flash unsigned FDL support

#### ☁️ Cloud Loader Matching (Qualcomm)
- **Auto Matching**
  - Get loader based on chip ID automatically
  - No local PAK resource pack needed
- **API Integration**
  - Cloud loader database
  - Real-time update support

### Core Features

#### 📱 Qualcomm EDL (9008) Mode
- Sahara V2/V3 protocol support
- Firehose enhanced flashing
- GPT partition backup/restore
- Auto storage type detection (eMMC/UFS/NAND)
- OFP/OZIP/OPS firmware decryption
- Smart key bruteforce (50+ key sets)
- 🆕 Native Diag protocol (IMEI/MEID/QCN read/write)
- 🆕 Loader feature detection

#### ⚡ Fastboot Enhanced
- Partition read/write operations
- OEM unlock/relock
- Device info query
- Custom command execution
- 🆕 Huawei/Honor device full support
  - Device info reading (IMEI/MEID/Model/Firmware)
  - FRP unlock (oem frp-unlock)
  - Device ID acquisition
  - Bootloader unlock/lock

#### 🔧 MediaTek (MTK)
- BROM/Preloader mode
- XFlash + XML dual protocol
- Auto DA loading
- Exploits (Carbonara/AllinoneSignature)

#### 📱 Spreadtrum (SPD/Unisoc)
- FDL1/FDL2 download
- PAC firmware parsing
- T760/T770 signature bypass
- 🆕 ISP eMMC direct access
- 🆕 Bootloader unlock/lock
- 🆕 A/B slot switching

---

## 📋 System Requirements

### Minimum
- **OS**: Windows 10 (64-bit) or higher
- **Runtime**: .NET Framework 4.8
- **RAM**: 4GB
- **Storage**: 500MB free space

### Driver Requirements
| Platform | Driver | Purpose |
|----------|--------|---------|
| Qualcomm | Qualcomm HS-USB | 9008 mode |
| MediaTek | MediaTek PreLoader | BROM mode |
| Spreadtrum | SPRD USB | Download mode |
| Universal | ADB/Fastboot | Debug mode |

---

## 🚀 Quick Start

### Installation

1. **Download**
   - Get the latest version from [Releases](https://github.com/xiriovo/SakuraEDL/releases)
   - Extract to any directory (English path recommended)

2. **Install Drivers**
   - Install drivers according to your device platform

3. **Run**
   ```
   SakuraEDL.exe
   ```

---

## 📄 License

This project uses **Non-Commercial License** - See [LICENSE](LICENSE) file

- ✅ Personal learning and research allowed
- ✅ Modification and distribution allowed (same license required)
- ❌ Commercial use prohibited
- ❌ Selling or profit-making prohibited

---

## 📧 Contact

### Community
- **QQ Group**: [SakuraEDL](https://qm.qq.com/q/z3iVnkm22c)
- **Telegram**: [@xiriery](https://t.me/xiriery)
- **Discord**: [Join Server](https://discord.gg/sakuraedl)

### Developer
- **GitHub**: [@xiriovo](https://github.com/xiriovo)
- **Email**: 1708298587@qq.com

---

## 🙏 Acknowledgments

- [mtkclient](https://github.com/bkerler/mtkclient) - MTK protocol reference
- [spd_dump](https://github.com/ArtRichards/spd_dump) - SPD protocol reference
- [edl](https://github.com/bkerler/edl) - Qualcomm EDL reference

---

<p align="center">
  Made with ❤️ by SakuraEDL Team<br>
  Copyright © 2025-2026 SakuraEDL. All rights reserved.
</p>
