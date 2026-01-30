<template>
  <div class="home-page">
    <!-- Hero Section -->
    <section class="hero">
      <div class="hero-content">
        <h1 class="hero-title">
          <span class="gradient">{{ t('home.hero.title') }}</span>
        </h1>
        <p class="hero-tagline">{{ t('home.hero.subtitle') }}</p>
        <p class="hero-desc">{{ t('home.hero.description') }}</p>
        
        <div class="hero-actions">
          <router-link to="/guide/getting-started" class="vp-button primary large">
            {{ t('home.hero.getStarted') }}
            <svg width="16" height="16" viewBox="0 0 24 24" fill="currentColor">
              <path d="M8.59 16.59L13.17 12 8.59 7.41 10 6l6 6-6 6-1.41-1.41z"/>
            </svg>
          </router-link>
          <router-link to="/download" class="vp-button secondary large">{{ t('home.hero.download') }}</router-link>
        </div>
        
        <!-- 实时统计 -->
        <div class="hero-stats" v-if="stats">
          <div class="stat-item live-time">
            <span class="stat-value clock">{{ currentTime }}</span>
            <span class="stat-label">实时时间</span>
          </div>
          <div class="stat-item">
            <span class="stat-value animated">{{ animatedStats.total_loaders }}</span>
            <span class="stat-label">云端 Loader</span>
          </div>
          <div class="stat-item">
            <span class="stat-value animated">{{ formatNumber(animatedStats.total_logs) }}</span>
            <span class="stat-label">设备连接</span>
          </div>
          <div class="stat-item">
            <span class="stat-value animated pulse">{{ animatedStats.logs_today }}</span>
            <span class="stat-label">今日活跃</span>
          </div>
        </div>
      </div>
    </section>
    
    <!-- Features Section -->
    <section class="features container">
      <div class="features-grid">
        <div class="feature-card">
          <div class="feature-icon qualcomm">📱</div>
          <h3>{{ t('home.features.qualcomm.title') }}</h3>
          <p>{{ t('home.features.qualcomm.desc') }}</p>
          <router-link to="/guide/qualcomm" class="feature-link">
            {{ currentLang === 'zh' ? '了解更多 →' : 'Learn more →' }}
          </router-link>
        </div>
        
        <div class="feature-card">
          <div class="feature-icon mtk">⚡</div>
          <h3>{{ t('home.features.mtk.title') }}</h3>
          <p>{{ t('home.features.mtk.desc') }}</p>
          <router-link to="/guide/mtk" class="feature-link">
            {{ currentLang === 'zh' ? '了解更多 →' : 'Learn more →' }}
          </router-link>
        </div>
        
        <div class="feature-card">
          <div class="feature-icon spd">🔧</div>
          <h3>{{ t('home.features.spd.title') }}</h3>
          <p>{{ t('home.features.spd.desc') }}</p>
          <router-link to="/guide/spd" class="feature-link">
            {{ currentLang === 'zh' ? '了解更多 →' : 'Learn more →' }}
          </router-link>
        </div>
        
        <div class="feature-card">
          <div class="feature-icon fastboot">🚀</div>
          <h3>{{ t('home.features.fastboot.title') }}</h3>
          <p>{{ t('home.features.fastboot.desc') }}</p>
          <router-link to="/guide/fastboot" class="feature-link">
            {{ currentLang === 'zh' ? '了解更多 →' : 'Learn more →' }}
          </router-link>
        </div>
        
        <div class="feature-card">
          <div class="feature-icon cloud">☁️</div>
          <h3>{{ t('home.features.cloud.title') }}</h3>
          <p>{{ t('home.features.cloud.desc') }}</p>
          <router-link to="/guide/cloud-loader" class="feature-link">
            {{ currentLang === 'zh' ? '了解更多 →' : 'Learn more →' }}
          </router-link>
        </div>
        
        <div class="feature-card">
          <div class="feature-icon free">💎</div>
          <h3>{{ t('home.features.free.title') }}</h3>
          <p>{{ t('home.features.free.desc') }}</p>
          <a href="https://github.com/xiriovo/SakuraEDL" target="_blank" class="feature-link">
            GitHub →
          </a>
        </div>
      </div>
    </section>
    
    <!-- Recent Devices -->
    <section class="recent-section container" v-if="recentDevices.length">
      <div class="section-header">
        <h2>最近连接设备</h2>
        <div class="refresh-indicator">
          <span class="refresh-dot" :class="{ active: isRefreshing }"></span>
          <span class="refresh-text">每 5 秒自动刷新</span>
        </div>
      </div>
      <div class="recent-table">
        <table>
          <thead>
            <tr>
              <th>芯片</th>
              <th>厂商</th>
              <th>MSM ID</th>
              <th>状态</th>
              <th>时间</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="device in recentDevices" :key="device.id" class="device-row">
              <td>{{ device.chip_name || '-' }}</td>
              <td>{{ device.vendor || '-' }}</td>
              <td><code>{{ device.msm_id || '-' }}</code></td>
              <td>
                <span :class="['tag', getStatusType(device.match_result)]">
                  {{ getStatusLabel(device.match_result) }}
                </span>
              </td>
              <td>{{ formatTime(device.created_at) }}</td>
            </tr>
          </tbody>
        </table>
      </div>
    </section>
  </div>
</template>

<script setup>
import { ref, onMounted, onUnmounted } from 'vue'
import api from '@/api'
import { useI18n } from '@/i18n'

const { t, currentLang } = useI18n()

const stats = ref(null)
const recentDevices = ref([])
const currentTime = ref('')
const isRefreshing = ref(false)
const lastRefreshTime = ref(null)
const animatedStats = ref({
  total_loaders: 0,
  total_logs: 0,
  logs_today: 0
})

let clockTimer = null
let animationTimer = null
let refreshTimer = null
const REFRESH_INTERVAL = 5000 // 5秒刷新

// 实时时钟
const updateClock = () => {
  const now = new Date()
  const hours = String(now.getHours()).padStart(2, '0')
  const minutes = String(now.getMinutes()).padStart(2, '0')
  const seconds = String(now.getSeconds()).padStart(2, '0')
  currentTime.value = `${hours}:${minutes}:${seconds}`
}

// 数字动画效果
const animateNumber = (target, key, duration = 1500) => {
  const start = 0
  const end = target
  const startTime = performance.now()
  
  const animate = (currentTime) => {
    const elapsed = currentTime - startTime
    const progress = Math.min(elapsed / duration, 1)
    
    // easeOutExpo 缓动效果
    const easeProgress = progress === 1 ? 1 : 1 - Math.pow(2, -10 * progress)
    animatedStats.value[key] = Math.floor(easeProgress * end)
    
    if (progress < 1) {
      requestAnimationFrame(animate)
    }
  }
  
  requestAnimationFrame(animate)
}

const formatNumber = (num) => {
  if (num >= 10000) return (num / 10000).toFixed(1) + 'w'
  if (num >= 1000) return (num / 1000).toFixed(1) + 'k'
  return num.toString()
}

const formatTime = (dateStr) => {
  if (!dateStr) return '-'
  const date = new Date(dateStr)
  const now = new Date()
  const diff = (now - date) / 1000
  
  if (diff < 60) return '刚刚'
  if (diff < 3600) return Math.floor(diff / 60) + '分钟前'
  if (diff < 86400) return Math.floor(diff / 3600) + '小时前'
  return Math.floor(diff / 86400) + '天前'
}

const getStatusType = (result) => {
  const types = { success: 'success', failed: 'danger', info_collected: 'info', not_found: 'warning' }
  return types[result] || 'info'
}

const getStatusLabel = (result) => {
  const labels = { success: '成功', failed: '失败', info_collected: '收集', not_found: '未匹配' }
  return labels[result] || result
}

// 获取统计数据
const fetchStats = async (isInitial = false) => {
  isRefreshing.value = true
  try {
    const res = await api.getStats(true) // 强制刷新
    if (res.code === 0) {
      stats.value = res.data
      
      // 检查是否有新设备
      const newDevices = res.data.recent_devices || []
      const oldIds = recentDevices.value.map(d => d.id)
      const hasNew = newDevices.some(d => !oldIds.includes(d.id))
      
      recentDevices.value = newDevices
      lastRefreshTime.value = new Date()
      
      // 初始加载时启动数字动画
      if (isInitial) {
        animateNumber(res.data.total_loaders || 0, 'total_loaders', 1200)
        animateNumber(res.data.total_logs || 0, 'total_logs', 1500)
        animateNumber(res.data.logs_today || 0, 'logs_today', 1000)
      } else {
        // 后续刷新时平滑更新数字
        animatedStats.value.total_loaders = res.data.total_loaders || 0
        animatedStats.value.total_logs = res.data.total_logs || 0
        animatedStats.value.logs_today = res.data.logs_today || 0
      }
    }
  } catch (e) {
    console.error('获取统计失败', e)
  } finally {
    isRefreshing.value = false
  }
}

onMounted(async () => {
  // 启动实时时钟
  updateClock()
  clockTimer = setInterval(updateClock, 1000)
  
  // 初始加载
  await fetchStats(true)
  
  // 每5秒刷新设备记录
  refreshTimer = setInterval(() => fetchStats(false), REFRESH_INTERVAL)
})

onUnmounted(() => {
  if (clockTimer) clearInterval(clockTimer)
  if (animationTimer) clearInterval(animationTimer)
  if (refreshTimer) clearInterval(refreshTimer)
})
</script>

<style lang="scss" scoped>
.hero {
  padding: 100px 24px 80px;
  text-align: center;
  position: relative;
  overflow: hidden;
  background: linear-gradient(180deg, var(--vp-c-bg) 0%, var(--vp-c-bg-soft) 100%);
  
  // 动态背景
  &::before {
    content: '';
    position: absolute;
    top: -50%;
    left: -50%;
    width: 200%;
    height: 200%;
    background: 
      radial-gradient(circle at 30% 20%, rgba(236, 72, 153, 0.12) 0%, transparent 40%),
      radial-gradient(circle at 70% 60%, rgba(139, 92, 246, 0.1) 0%, transparent 40%),
      radial-gradient(circle at 40% 80%, rgba(99, 102, 241, 0.08) 0%, transparent 35%);
    animation: heroFloat 15s ease-in-out infinite;
    pointer-events: none;
    z-index: 0;
  }
}

@keyframes heroFloat {
  0%, 100% { transform: translate(0, 0) scale(1); }
  33% { transform: translate(3%, -2%) scale(1.02); }
  66% { transform: translate(-2%, 3%) scale(0.98); }
}

.hero-content {
  max-width: 800px;
  margin: 0 auto;
  position: relative;
  z-index: 1;
}

.hero-title {
  font-size: 4.5rem;
  font-weight: 800;
  letter-spacing: -3px;
  margin: 0;
  animation: fadeInUp 0.8s ease forwards;
  
  @media (max-width: 768px) {
    font-size: 2.8rem;
    letter-spacing: -1px;
  }
  
  .gradient {
    background: linear-gradient(135deg, #ec4899 0%, #8b5cf6 40%, #6366f1 70%, #ec4899 100%);
    background-size: 200% auto;
    -webkit-background-clip: text;
    -webkit-text-fill-color: transparent;
    background-clip: text;
    animation: gradientFlow 4s ease infinite;
  }
}

@keyframes gradientFlow {
  0%, 100% { background-position: 0% center; }
  50% { background-position: 100% center; }
}

@keyframes fadeInUp {
  from {
    opacity: 0;
    transform: translateY(30px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}

.hero-tagline {
  font-size: 1.6rem;
  color: var(--vp-c-text-1);
  margin: 20px 0 12px;
  font-weight: 600;
  animation: fadeInUp 0.8s ease 0.1s forwards;
  opacity: 0;
}

.hero-desc {
  font-size: 1.15rem;
  color: var(--vp-c-text-2);
  margin: 0 0 36px;
  animation: fadeInUp 0.8s ease 0.2s forwards;
  opacity: 0;
}

.hero-actions {
  display: flex;
  justify-content: center;
  gap: 16px;
  flex-wrap: wrap;
  animation: fadeInUp 0.8s ease 0.3s forwards;
  opacity: 0;
  
  .vp-button.primary {
    background: linear-gradient(135deg, #ec4899 0%, #8b5cf6 100%);
    border: none;
    position: relative;
    overflow: hidden;
    
    &::before {
      content: '';
      position: absolute;
      top: 0;
      left: -100%;
      width: 100%;
      height: 100%;
      background: linear-gradient(90deg, transparent, rgba(255,255,255,0.2), transparent);
      transition: left 0.5s;
    }
    
    &:hover {
      transform: translateY(-2px);
      box-shadow: 0 8px 25px rgba(236, 72, 153, 0.4);
      
      &::before {
        left: 100%;
      }
    }
  }
  
  .vp-button.secondary {
    &:hover {
      transform: translateY(-2px);
      border-color: #ec4899;
      color: #ec4899;
    }
  }
}

.hero-stats {
  display: flex;
  justify-content: center;
  gap: 40px;
  margin-top: 60px;
  padding: 32px 40px;
  background: rgba(255, 255, 255, 0.5);
  backdrop-filter: blur(10px);
  border-radius: 20px;
  border: 1px solid rgba(236, 72, 153, 0.1);
  animation: fadeInUp 0.8s ease 0.5s forwards;
  opacity: 0;
  
  .dark & {
    background: rgba(27, 27, 31, 0.5);
  }
  
  @media (max-width: 640px) {
    gap: 20px;
    padding: 24px 20px;
    flex-wrap: wrap;
  }
}

.stat-item {
  text-align: center;
  position: relative;
  padding: 0 20px;
  
  &:not(:last-child)::after {
    content: '';
    position: absolute;
    right: 0;
    top: 50%;
    transform: translateY(-50%);
    width: 1px;
    height: 40px;
    background: linear-gradient(180deg, transparent, var(--vp-c-divider), transparent);
  }
  
  @media (max-width: 640px) {
    padding: 0 10px;
    
    &:not(:last-child)::after {
      display: none;
    }
  }
}

.stat-value {
  display: block;
  font-size: 2rem;
  font-weight: 700;
  color: var(--vp-c-brand);
  
  @media (max-width: 640px) {
    font-size: 1.5rem;
  }
  
  &.clock {
    font-family: 'JetBrains Mono', 'Fira Code', monospace;
    background: linear-gradient(90deg, #10b981, #3b82f6);
    -webkit-background-clip: text;
    -webkit-text-fill-color: transparent;
    background-clip: text;
    letter-spacing: 2px;
  }
  
  &.animated {
    transition: all 0.1s ease-out;
  }
  
  &.pulse {
    animation: pulse 2s infinite;
  }
}

@keyframes pulse {
  0%, 100% { opacity: 1; }
  50% { opacity: 0.7; }
}

.stat-label {
  font-size: 14px;
  color: var(--vp-c-text-3);
}

.stat-item.live-time {
  position: relative;
  
  &::before {
    content: '';
    position: absolute;
    top: -8px;
    left: 50%;
    transform: translateX(-50%);
    width: 8px;
    height: 8px;
    background: #10b981;
    border-radius: 50%;
    animation: blink 1s infinite;
  }
}

@keyframes blink {
  0%, 100% { opacity: 1; }
  50% { opacity: 0.3; }
}

.features {
  padding: 60px 24px;
}

.features-grid {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 24px;
  
  @media (max-width: 960px) {
    grid-template-columns: repeat(2, 1fr);
  }
  
  @media (max-width: 640px) {
    grid-template-columns: 1fr;
  }
}

.feature-card {
  background: var(--vp-c-bg);
  border: 1px solid var(--vp-c-divider);
  border-radius: 16px;
  padding: 28px;
  transition: all 0.4s cubic-bezier(0.4, 0, 0.2, 1);
  position: relative;
  overflow: hidden;
  
  // 入场动画
  opacity: 0;
  transform: translateY(30px);
  animation: cardFadeIn 0.6s ease forwards;
  
  @for $i from 1 through 6 {
    &:nth-child(#{$i}) {
      animation-delay: #{$i * 0.1}s;
    }
  }
  
  // 悬停渐变背景
  &::before {
    content: '';
    position: absolute;
    top: 0;
    left: 0;
    right: 0;
    bottom: 0;
    background: linear-gradient(135deg, rgba(236, 72, 153, 0.05) 0%, rgba(139, 92, 246, 0.05) 100%);
    opacity: 0;
    transition: opacity 0.3s;
    z-index: 0;
  }
  
  &:hover {
    border-color: rgba(236, 72, 153, 0.5);
    box-shadow: var(--vp-shadow-3), 0 0 30px rgba(236, 72, 153, 0.1);
    transform: translateY(-8px);
    
    &::before {
      opacity: 1;
    }
    
    .feature-icon {
      transform: scale(1.1) rotate(5deg);
    }
  }
  
  h3 {
    font-size: 18px;
    margin: 16px 0 10px;
    position: relative;
    z-index: 1;
  }
  
  p {
    font-size: 14px;
    color: var(--vp-c-text-2);
    margin: 0 0 16px;
    line-height: 1.7;
    position: relative;
    z-index: 1;
  }
}

@keyframes cardFadeIn {
  to {
    opacity: 1;
    transform: translateY(0);
  }
}

.feature-icon {
  width: 56px;
  height: 56px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 28px;
  border-radius: 14px;
  position: relative;
  z-index: 1;
  transition: all 0.3s ease;
  
  &.qualcomm { 
    background: linear-gradient(135deg, rgba(239, 68, 68, 0.15), rgba(239, 68, 68, 0.05)); 
    box-shadow: 0 4px 15px rgba(239, 68, 68, 0.1);
  }
  &.mtk { 
    background: linear-gradient(135deg, rgba(245, 158, 11, 0.15), rgba(245, 158, 11, 0.05)); 
    box-shadow: 0 4px 15px rgba(245, 158, 11, 0.1);
  }
  &.spd { 
    background: linear-gradient(135deg, rgba(16, 185, 129, 0.15), rgba(16, 185, 129, 0.05)); 
    box-shadow: 0 4px 15px rgba(16, 185, 129, 0.1);
  }
  &.fastboot { 
    background: linear-gradient(135deg, rgba(59, 130, 246, 0.15), rgba(59, 130, 246, 0.05)); 
    box-shadow: 0 4px 15px rgba(59, 130, 246, 0.1);
  }
  &.cloud { 
    background: linear-gradient(135deg, rgba(139, 92, 246, 0.15), rgba(139, 92, 246, 0.05)); 
    box-shadow: 0 4px 15px rgba(139, 92, 246, 0.1);
  }
  &.free { 
    background: linear-gradient(135deg, rgba(236, 72, 153, 0.15), rgba(236, 72, 153, 0.05)); 
    box-shadow: 0 4px 15px rgba(236, 72, 153, 0.1);
  }
}

.feature-link {
  font-size: 14px;
  font-weight: 600;
  color: #ec4899;
  position: relative;
  z-index: 1;
  display: inline-flex;
  align-items: center;
  gap: 4px;
  transition: all 0.3s;
  
  &:hover {
    color: #8b5cf6;
    gap: 8px;
  }
}

.recent-section {
  padding: 40px 24px 80px;
}

.section-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 24px;
  flex-wrap: wrap;
  gap: 16px;
  
  h2 {
    margin: 0;
  }
}

.refresh-indicator {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 6px 14px;
  background: var(--vp-c-bg-soft);
  border-radius: 20px;
  font-size: 13px;
  color: var(--vp-c-text-3);
}

.refresh-dot {
  width: 8px;
  height: 8px;
  background: #10b981;
  border-radius: 50%;
  animation: pulse 2s ease-in-out infinite;
  
  &.active {
    animation: spin 0.8s linear infinite;
    background: linear-gradient(135deg, #ec4899, #8b5cf6);
  }
}

@keyframes pulse {
  0%, 100% { opacity: 1; transform: scale(1); }
  50% { opacity: 0.5; transform: scale(0.8); }
}

@keyframes spin {
  to { transform: rotate(360deg); }
}

.device-row {
  transition: all 0.3s;
  
  &:hover {
    background: var(--vp-c-bg-soft);
  }
}

.recent-table {
  overflow-x: auto;
  border: 1px solid var(--vp-c-divider);
  border-radius: 16px;
  background: var(--vp-c-bg);
  
  table {
    margin: 0;
    
    th, td {
      white-space: nowrap;
    }
    
    code {
      font-size: 12px;
    }
  }
}
</style>
