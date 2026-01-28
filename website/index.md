---
layout: home

hero:
  name: "SakuraEDL"
  text: "多平台手机刷机工具"
  tagline: 支持高通 EDL / MTK / 展锐 / Fastboot | 永久免费
  actions:
    - theme: brand
      text: 快速开始
      link: /guide/getting-started
    - theme: alt
      text: 下载工具
      link: /download
    - theme: alt
      text: 加入QQ群
      link: https://qm.qq.com/q/z3iVnkm22c

features:
  - icon: 🚀
    title: 高通 EDL 模式
    details: 支持 Sahara + Firehose 协议，云端 Loader 自动匹配，支持小米/一加/OPPO 等品牌认证
  - icon: 📱
    title: MTK 联发科
    details: 支持 BROM + DA 模式，XFlash 二进制协议，兼容 MT6765-MT6893 全系芯片
  - icon: 💾
    title: 展锐 Spreadtrum
    details: 支持 BSL + FDL 协议，自动检测芯片型号，兼容 SC9863A/T760 等芯片
  - icon: ⚡
    title: Fastboot 模式
    details: 支持标准 Fastboot 协议，AB 分区自动识别，Payload 刷机支持
  - icon: ☁️
    title: 云端 Loader
    details: 自动匹配设备对应的 Loader，VIP/小米/一加认证自动执行
  - icon: 🔧
    title: 完全免费
    details: 永久免费使用，无需注册，无广告，开源透明
---

## 支持的芯片

### 高通 Qualcomm
| 系列 | 芯片 |
|------|------|
| 旗舰 | SM8750, SM8650, SM8550, SM8475, SM8450, SM8350, SM8250, SM8150 |
| 中端 | SM7550, SM7475, SM7450, SM6450, SM6375, SM6350 |
| 入门 | SM4450, SM4375 |

### 联发科 MediaTek
| 系列 | 芯片 |
|------|------|
| 天玑 | MT6893, MT6885, MT6875, MT6873, MT6853 |
| Helio | MT6765, MT6762, MT6761, MT6739 |

### 展锐 Spreadtrum
| 系列 | 芯片 |
|------|------|
| 虎贲 | T760, T740, T710, T618, T610 |
| SC | SC9863A, SC9832E |

---

## 快速链接

<div class="quick-links">
  <a href="/guide/qualcomm">📘 高通教程</a>
  <a href="/guide/mtk">📗 MTK教程</a>
  <a href="/guide/spd">📙 展锐教程</a>
  <a href="/guide/fastboot">📕 Fastboot教程</a>
</div>

<style>
.quick-links {
  display: flex;
  gap: 16px;
  flex-wrap: wrap;
  margin-top: 24px;
}
.quick-links a {
  padding: 12px 24px;
  background: var(--vp-c-bg-soft);
  border-radius: 8px;
  text-decoration: none;
  font-weight: 500;
  transition: all 0.3s;
}
.quick-links a:hover {
  background: var(--vp-c-brand-soft);
}
</style>
