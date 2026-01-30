<template>
  <div class="guide-page">
    <VPSidebar :items="sidebarItems" />
    
    <main class="guide-content">
      <article class="content-body" v-html="content"></article>
      
      <div class="page-nav" v-if="prevPage || nextPage">
        <router-link v-if="prevPage" :to="prevPage.link" class="nav-prev">
          <span class="nav-label">上一页</span>
          <span class="nav-title">{{ prevPage.text }}</span>
        </router-link>
        <router-link v-if="nextPage" :to="nextPage.link" class="nav-next">
          <span class="nav-label">下一页</span>
          <span class="nav-title">{{ nextPage.text }}</span>
        </router-link>
      </div>
    </main>
  </div>
</template>

<script setup>
import { ref, computed, watch } from 'vue'
import { useRoute } from 'vue-router'
import VPSidebar from '@/components/VPSidebar.vue'

const route = useRoute()

const sidebarItems = [
  {
    text: '入门指南',
    items: [
      { text: '快速开始', link: '/guide/getting-started' },
      { text: '安装说明', link: '/guide/installation' },
      { text: '常见问题', link: '/guide/faq' }
    ]
  },
  {
    text: '平台教程',
    items: [
      { text: '高通 EDL 模式', link: '/guide/qualcomm' },
      { text: 'MTK 联发科', link: '/guide/mtk' },
      { text: '展锐 Spreadtrum', link: '/guide/spd' },
      { text: 'Fastboot 模式', link: '/guide/fastboot' }
    ]
  },
  {
    text: '进阶功能',
    items: [
      { text: '云端 Loader', link: '/guide/cloud-loader' },
      { text: '自动认证', link: '/guide/auto-auth' },
      { text: '分区操作', link: '/guide/partitions' }
    ]
  }
]

const allPages = sidebarItems.flatMap(g => g.items)

const currentIndex = computed(() => {
  return allPages.findIndex(p => p.link === route.path)
})

const prevPage = computed(() => {
  return currentIndex.value > 0 ? allPages[currentIndex.value - 1] : null
})

const nextPage = computed(() => {
  return currentIndex.value < allPages.length - 1 ? allPages[currentIndex.value + 1] : null
})

// 页面内容 (简化版，实际可从 API 获取)
const contents = {
  'getting-started': `
    <h1>快速开始</h1>
    <p>欢迎使用 SakuraEDL！本指南将帮助你快速上手。</p>
    
    <h2>安装软件</h2>
    <ol>
      <li>下载最新版 SakuraEDL</li>
      <li>解压到任意目录</li>
      <li>以管理员身份运行 <code>SakuraEDL.exe</code></li>
    </ol>
    
    <h2>安装驱动</h2>
    <p>根据你的设备平台，安装对应的驱动程序：</p>
    <ul>
      <li><strong>高通设备</strong>：安装 Qualcomm 9008 驱动</li>
      <li><strong>MTK 设备</strong>：安装 MediaTek USB 驱动</li>
      <li><strong>展锐设备</strong>：安装 Spreadtrum 驱动</li>
    </ul>
    
    <h2>连接设备</h2>
    <ol>
      <li>将设备进入对应的刷机模式（EDL/BROM/研发模式）</li>
      <li>使用 USB 数据线连接电脑</li>
      <li>软件会自动识别设备并显示端口</li>
    </ol>
    
    <h2>下一步</h2>
    <p>根据你的设备平台，查看对应的详细教程。</p>
  `,
  'installation': `
    <h1>安装说明</h1>
    
    <h2>系统要求</h2>
    <ul>
      <li>Windows 7/8/10/11 (64位)</li>
      <li>.NET Framework 4.8</li>
      <li>管理员权限</li>
    </ul>
    
    <h2>安装步骤</h2>
    <ol>
      <li>从官网或 GitHub 下载最新版本</li>
      <li>解压 ZIP 压缩包到任意目录（建议英文路径）</li>
      <li>右键点击 <code>SakuraEDL.exe</code>，选择"以管理员身份运行"</li>
    </ol>
    
    <h2>驱动安装</h2>
    <p>首次使用需要安装设备驱动，软件会自动提示安装。</p>
  `,
  'faq': `
    <h1>常见问题</h1>
    
    <h2>设备无法识别</h2>
    <p>请检查以下几点：</p>
    <ul>
      <li>驱动是否正确安装</li>
      <li>USB 数据线是否支持数据传输</li>
      <li>设备是否正确进入刷机模式</li>
    </ul>
    
    <h2>Loader 签名验证失败</h2>
    <p>这通常意味着 Loader 与设备不匹配。请尝试使用云端自动匹配功能。</p>
    
    <h2>刷机失败</h2>
    <p>请检查镜像文件是否完整，以及是否选择了正确的分区。</p>
  `,
  'qualcomm': `
    <h1>高通 EDL 模式</h1>
    <p>高通 EDL (Emergency Download) 模式是高通芯片的底层刷机模式。</p>
    
    <h2>进入 EDL 模式</h2>
    <p>不同设备进入方式略有不同：</p>
    <ul>
      <li><strong>小米</strong>：关机后按住音量-和电源键</li>
      <li><strong>一加</strong>：使用工程线或 ADB 命令</li>
      <li><strong>OPPO</strong>：使用深度测试或 ADB 命令</li>
    </ul>
    
    <h2>自动认证</h2>
    <p>新款设备需要认证才能刷机：</p>
    <ul>
      <li><strong>小米设备</strong>：自动执行小米 Auth 认证</li>
      <li><strong>一加设备</strong>：勾选 OnePlus 验证选项</li>
      <li><strong>VIP 设备</strong>：需要 VIP 资源包</li>
    </ul>
    
    <h2>操作流程</h2>
    <ol>
      <li>选择正确的 COM 端口</li>
      <li>选择 Loader 文件（或使用云端自动匹配）</li>
      <li>点击"连接"建立通信</li>
      <li>选择要操作的分区进行读写</li>
    </ol>
  `,
  'mtk': `
    <h1>MTK 联发科</h1>
    <p>支持 BROM 模式和 Preloader 模式。</p>
    
    <h2>进入 BROM 模式</h2>
    <ol>
      <li>完全关机</li>
      <li>按住音量-</li>
      <li>插入 USB 数据线</li>
    </ol>
    
    <h2>支持的芯片</h2>
    <p>MT6765, MT6768, MT6785, MT6833, MT6853, MT6873, MT6877, MT6885, MT6893 等</p>
  `,
  'spd': `
    <h1>展锐 Spreadtrum</h1>
    <p>支持研发模式和下载模式。</p>
    
    <h2>进入下载模式</h2>
    <ol>
      <li>完全关机</li>
      <li>按住音量-</li>
      <li>插入 USB 数据线</li>
    </ol>
  `,
  'fastboot': `
    <h1>Fastboot 模式</h1>
    <p>通用的 Android 刷机模式，支持线刷包和 Payload 提取。</p>
    
    <h2>进入 Fastboot</h2>
    <p>关机后按住音量-和电源键，或使用 ADB：</p>
    <pre><code>adb reboot bootloader</code></pre>
    
    <h2>线刷包支持</h2>
    <p>支持自动解析线刷包中的 flash_all.bat，一键刷入所有分区。</p>
  `,
  'cloud-loader': `
    <h1>云端 Loader</h1>
    <p>自动匹配设备对应的 Loader，无需手动选择。</p>
    
    <h2>工作原理</h2>
    <ol>
      <li>读取设备的 MSM ID 和 PK Hash</li>
      <li>向云端服务器查询匹配的 Loader</li>
      <li>自动下载并使用</li>
    </ol>
    
    <h2>支持的设备</h2>
    <p>云端已收录主流品牌的常见机型 Loader。</p>
  `,
  'auto-auth': `
    <h1>自动认证</h1>
    <p>自动执行品牌厂商的验证流程。</p>
    
    <h2>小米设备</h2>
    <p>自动执行 MiAuth 认证，无需手动操作。</p>
    
    <h2>一加/OPPO 设备</h2>
    <p>勾选对应的验证选项，软件会自动处理。</p>
  `,
  'partitions': `
    <h1>分区操作</h1>
    <p>支持读取、写入、擦除分区操作。</p>
    
    <h2>读取分区</h2>
    <p>将设备分区数据备份到本地文件。</p>
    
    <h2>写入分区</h2>
    <p>将本地镜像文件刷入设备分区。</p>
    
    <h2>擦除分区</h2>
    <p>清空指定分区的数据。</p>
  `
}

const content = computed(() => {
  const page = route.params.page || 'getting-started'
  return contents[page] || '<h1>页面不存在</h1><p>请从侧边栏选择页面。</p>'
})
</script>

<style lang="scss" scoped>
.guide-page {
  display: flex;
}

.guide-content {
  flex: 1;
  margin-left: var(--vp-sidebar-width);
  padding: 48px 48px 80px;
  max-width: calc(100% - var(--vp-sidebar-width));
  
  @media (max-width: 960px) {
    margin-left: 0;
    max-width: 100%;
    padding: 32px 24px 60px;
  }
}

.content-body {
  max-width: var(--vp-content-width);
  
  :deep(h1) {
    font-size: 2rem;
    margin-bottom: 16px;
  }
  
  :deep(h2) {
    border-top: 1px solid var(--vp-c-divider);
    padding-top: 24px;
    margin-top: 32px;
  }
  
  :deep(ul), :deep(ol) {
    padding-left: 24px;
    margin: 16px 0;
    
    li {
      margin: 8px 0;
      color: var(--vp-c-text-2);
    }
  }
  
  :deep(pre) {
    margin: 16px 0;
  }
}

.page-nav {
  display: flex;
  justify-content: space-between;
  margin-top: 48px;
  padding-top: 24px;
  border-top: 1px solid var(--vp-c-divider);
  gap: 16px;
  
  a {
    flex: 1;
    max-width: 48%;
    padding: 16px 20px;
    border: 1px solid var(--vp-c-divider);
    border-radius: 8px;
    transition: all 0.2s;
    
    &:hover {
      border-color: var(--vp-c-brand);
    }
  }
  
  .nav-prev {
    text-align: left;
  }
  
  .nav-next {
    text-align: right;
    margin-left: auto;
  }
  
  .nav-label {
    display: block;
    font-size: 12px;
    color: var(--vp-c-text-3);
    margin-bottom: 4px;
  }
  
  .nav-title {
    font-weight: 500;
    color: var(--vp-c-brand);
  }
}
</style>
