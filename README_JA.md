<p align="center">
  <img src="assets/logo.png" alt="SakuraEDL Logo" width="128">
</p>

# SakuraEDL

**オープンソースのマルチプラットフォームAndroidフラッシュツール**

Qualcomm EDL (9008)、MediaTek (MTK)、Spreadtrum (SPD/Unisoc)、Fastbootモードをサポート

[![License: CC BY-NC-SA 4.0](https://img.shields.io/badge/License-CC%20BY--NC--SA%204.0-lightgrey.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-4.8-blue.svg)](https://dotnet.microsoft.com/)
[![GitHub Stars](https://img.shields.io/github/stars/xiriovo/SakuraEDL)](https://github.com/xiriovo/SakuraEDL/stargazers)
[![GitHub Release](https://img.shields.io/github/v/release/xiriovo/SakuraEDL)](https://github.com/xiriovo/SakuraEDL/releases)

[中文](README.md) | [English](README_EN.md) | [日本語](README_JA.md) | [한국어](README_KO.md) | [Русский](README_RU.md) | [Español](README_ES.md)

---

## 🎯 ハイライト

| 🚀 **マルチプラットフォーム** | ⚡ **デュアルプロトコル** | 🛠️ **フル機能** | ☁️ **クラウドマッチング** |
|:---:|:---:|:---:|:---:|
| Qualcomm + MTK + SPD | XFlash + XMLプロトコル | フラッシュ + 復旧 + 復号 | 自動Loader取得 |

---

## ✨ 機能

### 🆕 v3.0 新機能

#### 🔧 MediaTek (MTK) フルサポート
- **BROM/Preloaderモードフラッシュ**
  - BROMとPreloaderモードの自動検出
  - DA (Download Agent) スマートローディング
  - 分離型DA1 + DA2ファイルサポート
- **デュアルプロトコルエンジン**
  - XFlashバイナリプロトコル（mtkclient参考）
  - XML V6プロトコル（新しいデバイス向け）
  - 自動プロトコル選択とフォールバック
- **エクスプロイト**
  - Carbonaraエクスプロイト（DA1レベル）
  - AllinoneSignatureエクスプロイト（DA2レベル）
  - 自動検出と実行

#### 📱 Spreadtrum (SPD/Unisoc) サポート
- **FDLダウンロードプロトコル**
  - 自動FDL1/FDL2ダウンロード
  - HDLCフレームエンコーディング
  - 動的ボーレート切り替え
- **PACファームウェア解析**
  - PACパッケージ自動解析
  - FDLとパーティションイメージ抽出
- **署名バイパス (T760/T770)**
  - `custom_exec_no_verify`メカニズム
  - 未署名FDLフラッシュサポート

#### ☁️ クラウドLoaderマッチング (Qualcomm)
- **自動マッチング**
  - チップIDに基づいて自動的にLoader取得
  - ローカルPAKリソースパック不要
- **API統合**
  - クラウドLoaderデータベース
  - リアルタイム更新サポート

### コア機能

#### 📱 Qualcomm EDL (9008) モード
- Sahara V2/V3プロトコルサポート
- Firehose拡張フラッシュ
- GPTパーティションバックアップ/リストア
- 自動ストレージタイプ検出（eMMC/UFS/NAND）
- OFP/OZIP/OPSファームウェア復号
- スマートキーブルートフォース（50+キーセット）
- 🆕 ネイティブDiagプロトコル（IMEI/MEID/QCN読み書き）

#### ⚡ Fastboot拡張
- パーティション読み書き
- OEMアンロック/リロック
- デバイス情報クエリ
- カスタムコマンド実行
- 🆕 Huawei/Honorデバイスフルサポート

#### 🔧 MediaTek (MTK)
- BROM/Preloaderモード
- XFlash + XMLデュアルプロトコル
- 自動DAローディング
- エクスプロイト（Carbonara/AllinoneSignature）

#### 📱 Spreadtrum (SPD/Unisoc)
- FDL1/FDL2ダウンロード
- PACファームウェア解析
- T760/T770署名バイパス
- 🆕 ISP eMMC直接アクセス
- 🆕 ブートローダーアンロック/ロック

---

## 📋 システム要件

### 最小要件
- **OS**: Windows 10 (64-bit) 以降
- **ランタイム**: .NET Framework 4.8
- **RAM**: 4GB
- **ストレージ**: 500MB空き容量

### ドライバ要件
| プラットフォーム | ドライバ | 用途 |
|------------------|----------|------|
| Qualcomm | Qualcomm HS-USB | 9008モード |
| MediaTek | MediaTek PreLoader | BROMモード |
| Spreadtrum | SPRD USB | ダウンロードモード |
| ユニバーサル | ADB/Fastboot | デバッグモード |

---

## 🚀 クイックスタート

### インストール

1. **ダウンロード**
   - [Releases](https://github.com/xiriovo/SakuraEDL/releases)から最新版をダウンロード
   - 任意のディレクトリに解凍（英語パス推奨）

2. **ドライバインストール**
   - デバイスプラットフォームに応じたドライバをインストール

3. **実行**
   ```
   SakuraEDL.exe
   ```

---

## 📄 ライセンス

本プロジェクトは**非商用ライセンス**を採用 - [LICENSE](LICENSE)ファイルを参照

- ✅ 個人的な学習と研究使用可
- ✅ 変更と配布可（同じライセンス必須）
- ❌ 商用利用禁止
- ❌ 販売または営利目的禁止

---

## 📧 お問い合わせ

### コミュニティ
- **Telegram**: [@xiriery](https://t.me/xiriery)
- **Discord**: [サーバー参加](https://discord.gg/sakuraedl)

### 開発者
- **GitHub**: [@xiriovo](https://github.com/xiriovo)
- **メール**: 1708298587@qq.com

---

## 🙏 謝辞

- [mtkclient](https://github.com/bkerler/mtkclient) - MTKプロトコル参考
- [spd_dump](https://github.com/ArtRichards/spd_dump) - SPDプロトコル参考
- [edl](https://github.com/bkerler/edl) - Qualcomm EDL参考

---

<p align="center">
  Made with ❤️ by SakuraEDL Team<br>
  Copyright © 2025-2026 SakuraEDL. All rights reserved.
</p>
