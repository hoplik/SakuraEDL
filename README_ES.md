<p align="center">
  <img src="assets/logo.png" alt="SakuraEDL Logo" width="128">
</p>

# SakuraEDL

**Herramienta de código abierto multiplataforma para flashear Android**

Soporta Qualcomm EDL (9008), MediaTek (MTK), Spreadtrum (SPD/Unisoc) y modo Fastboot

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-4.8-blue.svg)](https://dotnet.microsoft.com/)
[![GitHub Stars](https://img.shields.io/github/stars/xiriovo/SakuraEDL)](https://github.com/xiriovo/SakuraEDL/stargazers)
[![GitHub Release](https://img.shields.io/github/v/release/xiriovo/SakuraEDL)](https://github.com/xiriovo/SakuraEDL/releases)

[中文](README.md) | [English](README_EN.md) | [日本語](README_JA.md) | [한국어](README_KO.md) | [Русский](README_RU.md) | [Español](README_ES.md)

---

## 🎯 Características Destacadas

| 🚀 **Multiplataforma** | ⚡ **Protocolo Dual** | 🛠️ **Funciones Completas** | ☁️ **Coincidencia en la Nube** |
|:---:|:---:|:---:|:---:|
| Qualcomm + MTK + SPD | Protocolo XFlash + XML | Flash + Recuperación + Descifrado | Coincidencia automática de Loader |

---

## ✨ Funciones

### 🆕 Novedades en v3.0

#### 🔧 Soporte Completo para MediaTek (MTK)
- **Flasheo en modo BROM/Preloader**
  - Detección automática de modos BROM y Preloader
  - Carga inteligente de DA (Download Agent)
  - Soporte para archivos DA1 + DA2 separados
- **Motor de Protocolo Dual**
  - Protocolo binario XFlash (basado en mtkclient)
  - Protocolo XML V6 (para dispositivos nuevos)
  - Selección automática de protocolo y respaldo
- **Exploits**
  - Exploit Carbonara (nivel DA1)
  - Exploit AllinoneSignature (nivel DA2)
  - Detección y ejecución automática

#### 📱 Soporte para Spreadtrum (SPD/Unisoc)
- **Protocolo de Descarga FDL**
  - Descarga automática FDL1/FDL2
  - Codificación de tramas HDLC
  - Cambio dinámico de velocidad de baudios
- **Análisis de Firmware PAC**
  - Análisis automático de paquetes PAC
  - Extracción de FDL e imágenes de partición
- **Bypass de Firma (T760/T770)**
  - Mecanismo `custom_exec_no_verify`
  - Soporte para flashear FDL sin firmar

#### ☁️ Coincidencia de Loader en la Nube (Qualcomm)
- **Coincidencia Automática**
  - Obtención automática de Loader según ID del chip
  - No requiere paquete PAK local
- **Integración API**
  - Base de datos de Loader en la nube
  - Soporte de actualización en tiempo real

### Funciones Principales

#### 📱 Modo Qualcomm EDL (9008)
- Soporte de protocolo Sahara V2/V3
- Flasheo mejorado con Firehose
- Respaldo/restauración de tabla de particiones GPT
- Detección automática de tipo de almacenamiento (eMMC/UFS/NAND)
- Descifrado de firmware OFP/OZIP/OPS
- Fuerza bruta inteligente de claves (50+ conjuntos)
- 🆕 Protocolo Diag nativo (lectura/escritura IMEI/MEID/QCN)

#### ⚡ Fastboot Mejorado
- Operaciones de lectura/escritura de particiones
- Desbloqueo/bloqueo OEM
- Consulta de información del dispositivo
- Ejecución de comandos personalizados
- 🆕 Soporte completo para dispositivos Huawei/Honor

#### 🔧 MediaTek (MTK)
- Modo BROM/Preloader
- Protocolo dual XFlash + XML
- Carga automática de DA
- Exploits (Carbonara/AllinoneSignature)

#### 📱 Spreadtrum (SPD/Unisoc)
- Descarga FDL1/FDL2
- Análisis de firmware PAC
- Bypass de firma T760/T770
- 🆕 Acceso directo ISP eMMC
- 🆕 Desbloqueo/bloqueo de bootloader

---

## 📋 Requisitos del Sistema

### Mínimos
- **SO**: Windows 10 (64-bit) o superior
- **Runtime**: .NET Framework 4.8
- **RAM**: 4GB
- **Almacenamiento**: 500MB de espacio libre

### Requisitos de Controladores
| Plataforma | Controlador | Uso |
|------------|-------------|-----|
| Qualcomm | Qualcomm HS-USB | Modo 9008 |
| MediaTek | MediaTek PreLoader | Modo BROM |
| Spreadtrum | SPRD USB | Modo descarga |
| Universal | ADB/Fastboot | Modo depuración |

---

## 🚀 Inicio Rápido

### Instalación

1. **Descargar**
   - Obtén la última versión de [Releases](https://github.com/xiriovo/SakuraEDL/releases)
   - Extrae en cualquier directorio (se recomienda ruta en inglés)

2. **Instalar Controladores**
   - Instala los controladores según la plataforma de tu dispositivo

3. **Ejecutar**
   ```
   SakuraEDL.exe
   ```

---

## 📄 Licencia

Este proyecto usa **Licencia No Comercial** - Ver archivo [LICENSE](LICENSE)

- ✅ Permitido uso personal de aprendizaje e investigación
- ✅ Permitida modificación y distribución (misma licencia requerida)
- ❌ Uso comercial prohibido
- ❌ Venta o fines de lucro prohibidos

---

## 📧 Contacto

### Comunidad
- **Telegram**: [@xiriery](https://t.me/xiriery)
- **Discord**: [Unirse al servidor](https://discord.gg/sakuraedl)

### Desarrollador
- **GitHub**: [@xiriovo](https://github.com/xiriovo)
- **Correo**: 1708298587@qq.com

---

## 🙏 Agradecimientos

- [mtkclient](https://github.com/bkerler/mtkclient) - Referencia del protocolo MTK
- [spd_dump](https://github.com/ArtRichards/spd_dump) - Referencia del protocolo SPD
- [edl](https://github.com/bkerler/edl) - Referencia Qualcomm EDL

---

<p align="center">
  Made with ❤️ by SakuraEDL Team<br>
  Copyright © 2025-2026 SakuraEDL. All rights reserved.
</p>
