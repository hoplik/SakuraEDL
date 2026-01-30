<p align="center">
  <img src="assets/logo.png" alt="SakuraEDL Logo" width="128">
</p>

# SakuraEDL

**오픈소스 멀티 플랫폼 안드로이드 플래싱 도구**

Qualcomm EDL (9008), MediaTek (MTK), Spreadtrum (SPD/Unisoc), Fastboot 모드 지원

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-4.8-blue.svg)](https://dotnet.microsoft.com/)
[![GitHub Stars](https://img.shields.io/github/stars/xiriovo/SakuraEDL)](https://github.com/xiriovo/SakuraEDL/stargazers)
[![GitHub Release](https://img.shields.io/github/v/release/xiriovo/SakuraEDL)](https://github.com/xiriovo/SakuraEDL/releases)

[中文](README.md) | [English](README_EN.md) | [日本語](README_JA.md) | [한국어](README_KO.md) | [Русский](README_RU.md) | [Español](README_ES.md)

---

## 🎯 하이라이트

| 🚀 **멀티 플랫폼** | ⚡ **듀얼 프로토콜** | 🛠️ **풀 기능** | ☁️ **클라우드 매칭** |
|:---:|:---:|:---:|:---:|
| Qualcomm + MTK + SPD | XFlash + XML 프로토콜 | 플래시 + 복구 + 복호화 | 자동 Loader 매칭 |

---

## ✨ 기능

### 🆕 v3.0 신규 기능

#### 🔧 MediaTek (MTK) 완전 지원
- **BROM/Preloader 모드 플래싱**
  - BROM 및 Preloader 모드 자동 감지
  - DA (Download Agent) 스마트 로딩
  - 분리형 DA1 + DA2 파일 지원
- **듀얼 프로토콜 엔진**
  - XFlash 바이너리 프로토콜 (mtkclient 참조)
  - XML V6 프로토콜 (신규 기기용)
  - 자동 프로토콜 선택 및 폴백
- **익스플로잇**
  - Carbonara 익스플로잇 (DA1 레벨)
  - AllinoneSignature 익스플로잇 (DA2 레벨)
  - 자동 감지 및 실행

#### 📱 Spreadtrum (SPD/Unisoc) 지원
- **FDL 다운로드 프로토콜**
  - 자동 FDL1/FDL2 다운로드
  - HDLC 프레임 인코딩
  - 동적 보드레이트 전환
- **PAC 펌웨어 파싱**
  - PAC 패키지 자동 파싱
  - FDL 및 파티션 이미지 추출
- **서명 우회 (T760/T770)**
  - `custom_exec_no_verify` 메커니즘
  - 미서명 FDL 플래시 지원

#### ☁️ 클라우드 Loader 매칭 (Qualcomm)
- **자동 매칭**
  - 칩 ID 기반 자동 Loader 획득
  - 로컬 PAK 리소스 팩 불필요
- **API 통합**
  - 클라우드 Loader 데이터베이스
  - 실시간 업데이트 지원

### 핵심 기능

#### 📱 Qualcomm EDL (9008) 모드
- Sahara V2/V3 프로토콜 지원
- Firehose 향상 플래싱
- GPT 파티션 백업/복원
- 자동 스토리지 타입 감지 (eMMC/UFS/NAND)
- OFP/OZIP/OPS 펌웨어 복호화
- 스마트 키 브루트포스 (50+ 키 세트)
- 🆕 네이티브 Diag 프로토콜 (IMEI/MEID/QCN 읽기/쓰기)

#### ⚡ Fastboot 향상
- 파티션 읽기/쓰기
- OEM 잠금해제/잠금
- 기기 정보 쿼리
- 커스텀 명령 실행
- 🆕 Huawei/Honor 기기 완전 지원

#### 🔧 MediaTek (MTK)
- BROM/Preloader 모드
- XFlash + XML 듀얼 프로토콜
- 자동 DA 로딩
- 익스플로잇 (Carbonara/AllinoneSignature)

#### 📱 Spreadtrum (SPD/Unisoc)
- FDL1/FDL2 다운로드
- PAC 펌웨어 파싱
- T760/T770 서명 우회
- 🆕 ISP eMMC 직접 액세스
- 🆕 부트로더 잠금해제/잠금

---

## 📋 시스템 요구사항

### 최소 사양
- **OS**: Windows 10 (64-bit) 이상
- **런타임**: .NET Framework 4.8
- **RAM**: 4GB
- **저장공간**: 500MB 여유 공간

### 드라이버 요구사항
| 플랫폼 | 드라이버 | 용도 |
|--------|----------|------|
| Qualcomm | Qualcomm HS-USB | 9008 모드 |
| MediaTek | MediaTek PreLoader | BROM 모드 |
| Spreadtrum | SPRD USB | 다운로드 모드 |
| 범용 | ADB/Fastboot | 디버그 모드 |

---

## 🚀 빠른 시작

### 설치

1. **다운로드**
   - [Releases](https://github.com/xiriovo/SakuraEDL/releases)에서 최신 버전 다운로드
   - 임의 디렉토리에 압축 해제 (영문 경로 권장)

2. **드라이버 설치**
   - 기기 플랫폼에 맞는 드라이버 설치

3. **실행**
   ```
   SakuraEDL.exe
   ```

---

## 📄 라이선스

본 프로젝트는 **비상업적 라이선스** 적용 - [LICENSE](LICENSE) 파일 참조

- ✅ 개인 학습 및 연구 사용 허용
- ✅ 수정 및 배포 허용 (동일 라이선스 필수)
- ❌ 상업적 사용 금지
- ❌ 판매 또는 영리 목적 금지

---

## 📧 연락처

### 커뮤니티
- **Telegram**: [@xiriery](https://t.me/xiriery)
- **Discord**: [서버 참가](https://discord.gg/sakuraedl)

### 개발자
- **GitHub**: [@xiriovo](https://github.com/xiriovo)
- **이메일**: 1708298587@qq.com

---

## 🙏 감사의 말

- [mtkclient](https://github.com/bkerler/mtkclient) - MTK 프로토콜 참조
- [spd_dump](https://github.com/ArtRichards/spd_dump) - SPD 프로토콜 참조
- [edl](https://github.com/bkerler/edl) - Qualcomm EDL 참조

---

<p align="center">
  Made with ❤️ by SakuraEDL Team<br>
  Copyright © 2025-2026 SakuraEDL. All rights reserved.
</p>
