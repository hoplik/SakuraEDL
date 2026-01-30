<template>
  <div class="download-page container">
    <div class="page-header">
      <h1>下载 <span class="gradient-text">SakuraEDL</span></h1>
      <p class="page-desc">永久免费的多平台手机刷机工具</p>
    </div>
    
    <div class="download-main">
      <!-- 完整版 -->
      <div class="download-card primary">
        <div class="card-glow"></div>
        <div class="card-badge">
          <span class="badge-dot"></span>
          推荐
        </div>
        <div class="card-icon">🚀</div>
        <h2>SakuraEDL</h2>
        <p class="version">v3.0.0</p>
        <p class="desc">包含所有功能，支持高通/MTK/展锐/Fastboot</p>
        <ul class="features">
          <li>✅ 高通 EDL 9008 模式</li>
          <li>✅ MTK 联发科 BROM/Preloader</li>
          <li>✅ 展锐 Spreadtrum</li>
          <li>✅ Fastboot 线刷模式</li>
          <li>✅ 云端 Loader 自动匹配</li>
          <li>✅ 自动认证 (小米/一加/OPPO)</li>
          <li>✅ Payload.bin 提取</li>
          <li>✅ 分区读写擦除</li>
        </ul>
        <div class="download-buttons">
          <a href="https://github.com/xiriovo/SakuraEDL/releases" target="_blank" class="btn-download github">
            <svg width="20" height="20" viewBox="0 0 24 24" fill="currentColor">
              <path d="M12 0c-6.626 0-12 5.373-12 12 0 5.302 3.438 9.8 8.207 11.387.599.111.793-.261.793-.577v-2.234c-3.338.726-4.033-1.416-4.033-1.416-.546-1.387-1.333-1.756-1.333-1.756-1.089-.745.083-.729.083-.729 1.205.084 1.839 1.237 1.839 1.237 1.07 1.834 2.807 1.304 3.492.997.107-.775.418-1.305.762-1.604-2.665-.305-5.467-1.334-5.467-5.931 0-1.311.469-2.381 1.236-3.221-.124-.303-.535-1.524.117-3.176 0 0 1.008-.322 3.301 1.23.957-.266 1.983-.399 3.003-.404 1.02.005 2.047.138 3.006.404 2.291-1.552 3.297-1.23 3.297-1.23.653 1.653.242 2.874.118 3.176.77.84 1.235 1.911 1.235 3.221 0 4.609-2.807 5.624-5.479 5.921.43.372.823 1.102.823 2.222v3.293c0 .319.192.694.801.576 4.765-1.589 8.199-6.086 8.199-11.386 0-6.627-5.373-12-12-12z"/>
            </svg>
            GitHub 下载
          </a>
          <a href="https://wwbda.lanzn.com/SakuraEDL" target="_blank" class="btn-download lanzou">
            <svg width="20" height="20" viewBox="0 0 24 24" fill="currentColor">
              <path d="M19 9h-4V3H9v6H5l7 7 7-7zM5 18v2h14v-2H5z"/>
            </svg>
            蓝奏云下载
          </a>
        </div>
        <p class="size">约 15 MB | Windows 7/8/10/11 (64位)</p>
      </div>
    </div>
    
    <!-- 系统要求 -->
    <section class="requirements">
      <h2>系统要求</h2>
      <div class="req-grid">
        <div class="req-item">
          <strong>操作系统</strong>
          <span>Windows 7/8/10/11 (64位)</span>
        </div>
        <div class="req-item">
          <strong>.NET Framework</strong>
          <span>4.8 或更高版本</span>
        </div>
        <div class="req-item">
          <strong>驱动</strong>
          <span>高通 9008 驱动 / MTK USB 驱动</span>
        </div>
        <div class="req-item">
          <strong>权限</strong>
          <span>需要管理员权限运行</span>
        </div>
      </div>
    </section>
    
    <!-- 驱动下载 -->
    <section class="drivers">
      <h2>驱动下载</h2>
      <div class="driver-list">
        <a :href="drivers.qualcomm.url" class="driver-item" download>
          <div class="driver-info">
            <span class="driver-icon">📱</span>
            <div>
              <span class="driver-name">{{ drivers.qualcomm.name }}</span>
              <span class="driver-desc">{{ drivers.qualcomm.desc }}</span>
            </div>
          </div>
          <span class="driver-action">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="currentColor">
              <path d="M19 9h-4V3H9v6H5l7 7 7-7zM5 18v2h14v-2H5z"/>
            </svg>
            下载
          </span>
        </a>
        <a :href="drivers.mtk.url" class="driver-item" download>
          <div class="driver-info">
            <span class="driver-icon">⚡</span>
            <div>
              <span class="driver-name">{{ drivers.mtk.name }}</span>
              <span class="driver-desc">{{ drivers.mtk.desc }}</span>
            </div>
          </div>
          <span class="driver-action">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="currentColor">
              <path d="M19 9h-4V3H9v6H5l7 7 7-7zM5 18v2h14v-2H5z"/>
            </svg>
            下载
          </span>
        </a>
        <a :href="drivers.spd.url" class="driver-item" download>
          <div class="driver-info">
            <span class="driver-icon">🔧</span>
            <div>
              <span class="driver-name">{{ drivers.spd.name }}</span>
              <span class="driver-desc">{{ drivers.spd.desc }}</span>
            </div>
          </div>
          <span class="driver-action">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="currentColor">
              <path d="M19 9h-4V3H9v6H5l7 7 7-7zM5 18v2h14v-2H5z"/>
            </svg>
            下载
          </span>
        </a>
      </div>
    </section>
    
    <!-- 更新日志 -->
    <section class="changelog">
      <h2>更新日志</h2>
      <div class="log-item">
        <div class="log-header">
          <span class="log-version">v3.0.0</span>
          <span class="log-date">2026-01-28</span>
        </div>
        <ul>
          <li>新增云端 Loader 自动匹配功能</li>
          <li>新增 SAHARA v1/v2/v3 协议支持</li>
          <li>新增设备信息上报和统计</li>
          <li>优化小米/一加/OPPO 认证流程</li>
          <li>修复多处已知问题</li>
        </ul>
      </div>
    </section>
  </div>
</template>

<script setup>
import { reactive } from 'vue'

// 驱动下载配置
const drivers = reactive({
  qualcomm: {
    name: '高通 9008 驱动',
    desc: 'Qualcomm HS-USB QDLoader 9008',
    url: '/downloads/qualcomm/qc_driver.exe'
  },
  mtk: {
    name: 'MTK USB 驱动',
    desc: 'MediaTek PreLoader USB VCOM',
    url: '/downloads/mediatek/mtk_driver.exe'
  },
  spd: {
    name: '展锐 USB 驱动',
    desc: 'Spreadtrum Download Port',
    url: '/downloads/spreadtrum/spd_driver_v2.exe'
  }
})
</script>

<style lang="scss" scoped>
.download-page {
  padding: 48px 24px 80px;
}

.page-header {
  text-align: center;
  margin-bottom: 48px;
  
  h1 {
    font-size: 2.5rem;
    margin-bottom: 12px;
  }
  
  .gradient-text {
    background: linear-gradient(135deg, #ec4899 0%, #8b5cf6 50%, #6366f1 100%);
    -webkit-background-clip: text;
    -webkit-text-fill-color: transparent;
    background-clip: text;
  }
  
  .page-desc {
    color: var(--vp-c-text-3);
    font-size: 1.1rem;
    margin: 0;
  }
}

.download-main {
  max-width: 540px;
  margin: 0 auto 60px;
}

.download-card {
  position: relative;
  background: var(--vp-c-bg);
  border: 1px solid var(--vp-c-divider);
  border-radius: 24px;
  padding: 40px 36px;
  text-align: center;
  overflow: hidden;
  animation: cardFloat 0.6s ease forwards;
  
  &.primary {
    border-color: rgba(236, 72, 153, 0.3);
    
    &:hover {
      border-color: rgba(236, 72, 153, 0.6);
      transform: translateY(-4px);
      
      .card-glow {
        opacity: 1;
      }
    }
  }
  
  h2 {
    margin: 16px 0 8px;
    font-size: 1.8rem;
  }
  
  .version {
    background: linear-gradient(135deg, #ec4899, #8b5cf6);
    -webkit-background-clip: text;
    -webkit-text-fill-color: transparent;
    font-weight: 700;
    font-size: 1.2rem;
    margin: 0 0 16px;
  }
  
  .desc {
    color: var(--vp-c-text-2);
    margin: 0 0 24px;
  }
}

@keyframes cardFloat {
  from {
    opacity: 0;
    transform: translateY(30px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}

.card-glow {
  position: absolute;
  top: -50%;
  left: -50%;
  width: 200%;
  height: 200%;
  background: radial-gradient(circle, rgba(236, 72, 153, 0.1) 0%, transparent 50%);
  opacity: 0;
  transition: opacity 0.5s;
  pointer-events: none;
  z-index: 0;
}

.card-icon {
  font-size: 48px;
  margin-bottom: 8px;
  animation: float 3s ease-in-out infinite;
}

@keyframes float {
  0%, 100% { transform: translateY(0); }
  50% { transform: translateY(-8px); }
}

.card-badge {
  position: absolute;
  top: -1px;
  left: 50%;
  transform: translateX(-50%);
  background: linear-gradient(135deg, #ec4899, #8b5cf6);
  color: white;
  padding: 6px 20px;
  border-radius: 0 0 12px 12px;
  font-size: 13px;
  font-weight: 600;
  display: flex;
  align-items: center;
  gap: 6px;
  
  .badge-dot {
    width: 6px;
    height: 6px;
    background: white;
    border-radius: 50%;
    animation: pulse 1.5s infinite;
  }
}

@keyframes pulse {
  0%, 100% { opacity: 1; }
  50% { opacity: 0.5; }
}

.download-buttons {
  display: flex;
  gap: 12px;
  justify-content: center;
  flex-wrap: wrap;
  margin-bottom: 8px;
}

.btn-download {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  padding: 14px 28px;
  border-radius: 12px;
  font-weight: 600;
  font-size: 15px;
  transition: all 0.3s;
  text-decoration: none;
  
  &.github {
    background: linear-gradient(135deg, #ec4899, #8b5cf6);
    color: white;
    
    &:hover {
      transform: translateY(-2px);
      box-shadow: 0 8px 25px rgba(236, 72, 153, 0.4);
    }
  }
  
  &.lanzou {
    background: var(--vp-c-bg-soft);
    color: var(--vp-c-text-1);
    border: 1px solid var(--vp-c-divider);
    
    &:hover {
      border-color: #ec4899;
      color: #ec4899;
      transform: translateY(-2px);
    }
  }
}

.features {
  list-style: none;
  padding: 0;
  margin: 0 0 24px;
  text-align: left;
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 8px;
  
  @media (max-width: 480px) {
    grid-template-columns: 1fr;
  }
  
  li {
    padding: 6px 0;
    color: var(--vp-c-text-2);
    font-size: 14px;
  }
}

.size {
  margin: 16px 0 0;
  font-size: 13px;
  color: var(--vp-c-text-3);
}

.requirements, .drivers, .changelog {
  max-width: 800px;
  margin: 0 auto 48px;
  
  h2 {
    margin-bottom: 20px;
  }
}

.req-grid {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 16px;
  
  @media (max-width: 640px) {
    grid-template-columns: 1fr;
  }
}

.req-item {
  background: var(--vp-c-bg-soft);
  padding: 16px 20px;
  border-radius: 8px;
  
  strong {
    display: block;
    margin-bottom: 4px;
    color: var(--vp-c-text-1);
  }
  
  span {
    color: var(--vp-c-text-2);
    font-size: 14px;
  }
}

.driver-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.driver-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 16px 20px;
  background: var(--vp-c-bg);
  border: 1px solid var(--vp-c-divider);
  border-radius: 12px;
  transition: all 0.3s;
  text-decoration: none;
  
  &:hover {
    border-color: #ec4899;
    background: rgba(236, 72, 153, 0.05);
    transform: translateX(4px);
    
    .driver-action {
      color: #ec4899;
    }
  }
  
  .driver-info {
    display: flex;
    align-items: center;
    gap: 12px;
  }
  
  .driver-icon {
    font-size: 24px;
  }
  
  .driver-name {
    display: block;
    font-weight: 600;
    color: var(--vp-c-text-1);
    margin-bottom: 2px;
  }
  
  .driver-desc {
    display: block;
    font-size: 13px;
    color: var(--vp-c-text-3);
  }
  
  .driver-action {
    display: flex;
    align-items: center;
    gap: 6px;
    padding: 8px 16px;
    background: var(--vp-c-bg-soft);
    border-radius: 8px;
    font-size: 14px;
    font-weight: 500;
    color: var(--vp-c-text-2);
    transition: all 0.3s;
  }
}


.log-item {
  background: var(--vp-c-bg);
  border: 1px solid var(--vp-c-divider);
  border-radius: 12px;
  padding: 20px 24px;
}

.log-header {
  display: flex;
  align-items: center;
  gap: 12px;
  margin-bottom: 12px;
}

.log-version {
  font-weight: 600;
  color: var(--vp-c-brand);
}

.log-date {
  font-size: 13px;
  color: var(--vp-c-text-3);
}

.log-item ul {
  margin: 0;
  padding-left: 20px;
  
  li {
    color: var(--vp-c-text-2);
    font-size: 14px;
    margin: 6px 0;
  }
}
</style>
