<template>
  <div class="stats-page container">
    <div class="stats-header">
      <div class="header-left">
        <h1>实时统计</h1>
        <p class="page-desc">SakuraEDL 云端服务 - 多平台实时数据统计</p>
      </div>
      <div class="header-right">
        <div class="live-clock">
          <span class="live-dot"></span>
          <span class="clock-time">{{ currentTime }}</span>
        </div>
        <div class="refresh-info">
          <span>数据每 30 秒自动刷新</span>
          <button class="refresh-btn" @click="fetchStats" :disabled="loading">
            🔄 刷新
          </button>
        </div>
      </div>
    </div>
    
    <!-- 总体统计卡片 -->
    <div class="total-stats" v-if="stats">
      <div class="total-card">
        <div class="total-icon">📊</div>
        <div class="total-info">
          <span class="total-value">{{ animatedStats.total_resources || 0 }}</span>
          <span class="total-label">云端资源总数</span>
        </div>
      </div>
      <div class="total-card">
        <div class="total-icon">📱</div>
        <div class="total-info">
          <span class="total-value">{{ animatedStats.total_logs || 0 }}</span>
          <span class="total-label">设备连接总数</span>
        </div>
      </div>
      <div class="total-card highlight">
        <div class="total-icon">🔥</div>
        <div class="total-info">
          <span class="total-value">{{ animatedStats.today_logs || 0 }}</span>
          <span class="total-label">今日活跃</span>
        </div>
      </div>
    </div>

    <!-- 平台切换标签 -->
    <div class="platform-tabs">
      <button 
        v-for="platform in platforms" 
        :key="platform.key"
        :class="['platform-tab', { active: activePlatform === platform.key }]"
        @click="activePlatform = platform.key"
      >
        <span class="tab-icon">{{ platform.icon }}</span>
        <span class="tab-name">{{ platform.name }}</span>
        <span class="tab-badge" v-if="getPlatformStats(platform.key)?.today_logs">
          {{ getPlatformStats(platform.key).today_logs }}
        </span>
      </button>
    </div>

    <!-- 平台详细统计 -->
    <div class="platform-stats" v-if="stats && getPlatformStats(activePlatform)">
      <div class="stats-grid">
        <div class="stat-card" v-for="(item, index) in platformStatCards" :key="item.key" :style="{ animationDelay: index * 0.1 + 's' }">
          <div class="stat-icon" :class="item.icon">{{ item.emoji }}</div>
          <div class="stat-info">
            <span class="stat-value">{{ getPlatformStats(activePlatform)[item.key] || 0 }}</span>
            <span class="stat-label">{{ item.label }}</span>
          </div>
        </div>
      </div>

      <!-- 最近设备 -->
      <section class="recent-section" v-if="getPlatformStats(activePlatform)?.recent_devices?.length">
        <h2>
          <span class="section-icon">{{ getPlatformIcon(activePlatform) }}</span>
          最近 {{ getPlatformName(activePlatform) }} 设备
        </h2>
        <div class="device-table">
          <table>
            <thead>
              <tr>
                <th>芯片 ID</th>
                <th>芯片名称</th>
                <th>{{ getExtraColumn(activePlatform).label }}</th>
                <th>结果</th>
                <th>时间</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="(device, i) in getPlatformStats(activePlatform).recent_devices" :key="i">
                <td><code>{{ device.chip_id || '-' }}</code></td>
                <td>{{ device.chip_name || '-' }}</td>
                <td>{{ getExtraValue(activePlatform, device) || '-' }}</td>
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

      <div class="empty-devices" v-else>
        <p>暂无 {{ getPlatformName(activePlatform) }} 设备连接记录</p>
      </div>
    </div>
    
    <div v-if="loading" class="loading"></div>
    <div v-if="error" class="error-msg">{{ error }}</div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted, onUnmounted } from 'vue'
import api from '@/api'

const stats = ref(null)
const loading = ref(true)
const error = ref('')
const currentTime = ref('')
const animatedStats = ref({})
const activePlatform = ref('qualcomm')

let clockTimer = null
let refreshTimer = null

const platforms = [
  { key: 'qualcomm', name: 'Qualcomm 高通', icon: '📱' },
  { key: 'mtk', name: 'MTK 联发科', icon: '⚡' },
  { key: 'spd', name: 'SPD 展锐', icon: '🔧' }
]

const platformStatCards = [
  { key: 'resources', label: '云端资源', emoji: '📦', icon: 'resources' },
  { key: 'logs', label: '设备日志', emoji: '📊', icon: 'logs' },
  { key: 'today_logs', label: '今日活跃', emoji: '🔥', icon: 'today' }
]

// 获取平台统计数据
const getPlatformStats = (platform) => {
  return stats.value?.[platform]
}

const getPlatformName = (platform) => {
  return platforms.find(p => p.key === platform)?.name || platform
}

const getPlatformIcon = (platform) => {
  return platforms.find(p => p.key === platform)?.icon || '📱'
}

const getExtraColumn = (platform) => {
  const columns = {
    qualcomm: { label: '存储类型', key: 'storage_type' },
    mtk: { label: 'DA 模式', key: 'da_mode' },
    spd: { label: '安全启动', key: 'secure_boot' }
  }
  return columns[platform] || { label: '-', key: '' }
}

const getExtraValue = (platform, device) => {
  const key = getExtraColumn(platform).key
  if (!key) return '-'
  const value = device[key]
  if (platform === 'qualcomm') return value?.toUpperCase()
  if (platform === 'spd') return value === 'true' || value === '1' ? '开启' : '关闭'
  return value
}

// 实时时钟
const updateClock = () => {
  const now = new Date()
  const year = now.getFullYear()
  const month = String(now.getMonth() + 1).padStart(2, '0')
  const day = String(now.getDate()).padStart(2, '0')
  const hours = String(now.getHours()).padStart(2, '0')
  const minutes = String(now.getMinutes()).padStart(2, '0')
  const seconds = String(now.getSeconds()).padStart(2, '0')
  currentTime.value = `${year}-${month}-${day} ${hours}:${minutes}:${seconds}`
}

// 数字动画
const animateNumber = (target, key, duration = 1000) => {
  const start = animatedStats.value[key] || 0
  const end = target
  if (start === end) return
  
  const startTime = performance.now()
  
  const animate = (currentTime) => {
    const elapsed = currentTime - startTime
    const progress = Math.min(elapsed / duration, 1)
    const easeProgress = progress === 1 ? 1 : 1 - Math.pow(2, -10 * progress)
    animatedStats.value[key] = Math.floor(start + (end - start) * easeProgress)
    
    if (progress < 1) {
      requestAnimationFrame(animate)
    }
  }
  
  requestAnimationFrame(animate)
}

const getStatusType = (result) => {
  const types = { success: 'success', matched: 'success', failed: 'danger', info_collected: 'info', not_found: 'warning' }
  return types[result] || 'info'
}

const getStatusLabel = (result) => {
  const labels = { success: '成功', matched: '匹配', failed: '失败', info_collected: '收集', not_found: '未匹配' }
  return labels[result] || result || '-'
}

const formatTime = (dateStr) => {
  if (!dateStr) return '-'
  try {
    return new Date(dateStr).toLocaleString('zh-CN')
  } catch {
    return dateStr
  }
}

const fetchStats = async () => {
  loading.value = true
  try {
    const res = await api.getStatsOverview()
    if (res.code === 0) {
      stats.value = res.data
      // 动画更新总数
      animateNumber(res.data.total_resources || 0, 'total_resources', 800)
      animateNumber(res.data.total_logs || 0, 'total_logs', 800)
      animateNumber(res.data.today_logs || 0, 'today_logs', 800)
    } else {
      error.value = res.message || '获取统计失败'
    }
  } catch (e) {
    error.value = '网络请求失败'
  } finally {
    loading.value = false
  }
}

onMounted(() => {
  updateClock()
  clockTimer = setInterval(updateClock, 1000)
  fetchStats()
  refreshTimer = setInterval(fetchStats, 30000)
})

onUnmounted(() => {
  if (clockTimer) clearInterval(clockTimer)
  if (refreshTimer) clearInterval(refreshTimer)
})
</script>

<style lang="scss" scoped>
.stats-page {
  padding: 48px 24px 80px;
}

.stats-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  margin-bottom: 32px;
  flex-wrap: wrap;
  gap: 24px;
  
  h1 {
    font-size: 2rem;
    margin: 0 0 8px;
  }
  
  .page-desc {
    color: var(--vp-c-text-3);
    margin: 0;
  }
}

.header-right {
  display: flex;
  align-items: center;
  gap: 24px;
  
  .live-clock {
    display: flex;
    align-items: center;
    gap: 8px;
    font-family: var(--vp-font-family-mono);
    font-size: 14px;
    color: var(--vp-c-text-2);
    
    .live-dot {
      width: 8px;
      height: 8px;
      background: #10b981;
      border-radius: 50%;
      animation: pulse 2s infinite;
    }
  }
  
  .refresh-info {
    display: flex;
    align-items: center;
    gap: 12px;
    font-size: 13px;
    color: var(--vp-c-text-3);
    
    .refresh-btn {
      padding: 6px 12px;
      border: 1px solid var(--vp-c-divider);
      border-radius: 6px;
      background: var(--vp-c-bg);
      cursor: pointer;
      font-size: 12px;
      
      &:hover { background: var(--vp-c-bg-mute); }
      &:disabled { opacity: 0.5; cursor: not-allowed; }
    }
  }
}

@keyframes pulse {
  0%, 100% { opacity: 1; }
  50% { opacity: 0.5; }
}

.total-stats {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 20px;
  margin-bottom: 32px;
  
  @media (max-width: 768px) {
    grid-template-columns: 1fr;
  }
}

.total-card {
  display: flex;
  align-items: center;
  gap: 16px;
  padding: 24px;
  background: var(--vp-c-bg);
  border: 1px solid var(--vp-c-divider);
  border-radius: 16px;
  transition: all 0.3s;
  
  &:hover {
    transform: translateY(-2px);
    box-shadow: var(--vp-shadow-2);
  }
  
  &.highlight {
    background: linear-gradient(135deg, rgba(236, 72, 153, 0.1), rgba(139, 92, 246, 0.05));
    border-color: rgba(236, 72, 153, 0.3);
  }
  
  .total-icon { font-size: 36px; }
  
  .total-info {
    display: flex;
    flex-direction: column;
  }
  
  .total-value {
    font-size: 2rem;
    font-weight: 700;
    background: linear-gradient(135deg, #ec4899, #8b5cf6);
    -webkit-background-clip: text;
    -webkit-text-fill-color: transparent;
  }
  
  .total-label {
    font-size: 14px;
    color: var(--vp-c-text-3);
  }
}

.platform-tabs {
  display: flex;
  gap: 12px;
  margin-bottom: 24px;
  flex-wrap: wrap;
}

.platform-tab {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 12px 20px;
  background: var(--vp-c-bg);
  border: 1px solid var(--vp-c-divider);
  border-radius: 12px;
  cursor: pointer;
  transition: all 0.3s;
  
  &:hover {
    border-color: rgba(236, 72, 153, 0.3);
  }
  
  &.active {
    background: linear-gradient(135deg, rgba(236, 72, 153, 0.1), rgba(139, 92, 246, 0.1));
    border-color: #ec4899;
  }
  
  .tab-icon { font-size: 18px; }
  .tab-name { font-weight: 500; color: var(--vp-c-text-1); }
  
  .tab-badge {
    padding: 2px 8px;
    background: #ec4899;
    color: #fff;
    border-radius: 10px;
    font-size: 12px;
    font-weight: 600;
  }
}

.platform-stats {
  animation: fadeIn 0.3s ease;
}

@keyframes fadeIn {
  from { opacity: 0; transform: translateY(10px); }
  to { opacity: 1; transform: translateY(0); }
}

.stats-grid {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 16px;
  margin-bottom: 32px;
  
  @media (max-width: 768px) {
    grid-template-columns: 1fr;
  }
}

.stat-card {
  display: flex;
  align-items: center;
  gap: 16px;
  padding: 20px;
  background: var(--vp-c-bg);
  border: 1px solid var(--vp-c-divider);
  border-radius: 12px;
  animation: slideUp 0.5s ease forwards;
  opacity: 0;
  
  .stat-icon { font-size: 28px; }
  
  .stat-info {
    display: flex;
    flex-direction: column;
  }
  
  .stat-value {
    font-size: 1.5rem;
    font-weight: 700;
    color: var(--vp-c-text-1);
  }
  
  .stat-label {
    font-size: 13px;
    color: var(--vp-c-text-3);
  }
}

@keyframes slideUp {
  from { opacity: 0; transform: translateY(20px); }
  to { opacity: 1; transform: translateY(0); }
}

.recent-section {
  background: var(--vp-c-bg);
  border: 1px solid var(--vp-c-divider);
  border-radius: 16px;
  padding: 24px;
  
  h2 {
    display: flex;
    align-items: center;
    gap: 8px;
    font-size: 1.1rem;
    margin: 0 0 16px;
    
    .section-icon { font-size: 20px; }
  }
}

.device-table {
  overflow-x: auto;
  
  table {
    width: 100%;
    border-collapse: collapse;
    
    th, td {
      padding: 12px 16px;
      text-align: left;
      border-bottom: 1px solid var(--vp-c-divider);
    }
    
    th {
      font-weight: 600;
      font-size: 13px;
      color: var(--vp-c-text-2);
      background: var(--vp-c-bg-mute);
    }
    
    td {
      font-size: 14px;
      color: var(--vp-c-text-1);
      
      code {
        font-family: var(--vp-font-family-mono);
        font-size: 12px;
        background: var(--vp-c-bg-mute);
        padding: 2px 6px;
        border-radius: 4px;
      }
    }
    
    tr:last-child td { border-bottom: none; }
  }
}

.tag {
  display: inline-block;
  padding: 4px 10px;
  border-radius: 12px;
  font-size: 12px;
  font-weight: 500;
  
  &.success {
    background: rgba(16, 185, 129, 0.1);
    color: #10b981;
  }
  
  &.warning {
    background: rgba(245, 158, 11, 0.1);
    color: #f59e0b;
  }
  
  &.danger {
    background: rgba(239, 68, 68, 0.1);
    color: #ef4444;
  }
  
  &.info {
    background: rgba(59, 130, 246, 0.1);
    color: #3b82f6;
  }
}

.empty-devices {
  text-align: center;
  padding: 40px 20px;
  color: var(--vp-c-text-3);
  background: var(--vp-c-bg);
  border: 1px solid var(--vp-c-divider);
  border-radius: 16px;
}

.loading {
  text-align: center;
  padding: 40px;
  color: var(--vp-c-text-3);
}

.error-msg {
  text-align: center;
  padding: 20px;
  color: #ef4444;
  background: rgba(239, 68, 68, 0.1);
  border-radius: 8px;
}
</style>
