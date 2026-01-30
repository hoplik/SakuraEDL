<template>
  <div class="mtk-page container">
    <div class="page-header">
      <div class="header-content">
        <div class="platform-badge mtk">MTK</div>
        <h1>联发科 <span class="gradient-text">芯片数据库</span></h1>
        <p class="page-desc">SakuraEDL 支持的 MediaTek 芯片列表 - 支持 Carbonara / ALLINONE-SIGNATURE 漏洞</p>
      </div>
    </div>
    
    <!-- 统计卡片 -->
    <div class="stats-row" v-if="stats">
      <div class="stat-card">
        <div class="stat-icon">📱</div>
        <div class="stat-info">
          <span class="stat-value">{{ stats.total }}</span>
          <span class="stat-label">芯片总数</span>
        </div>
      </div>
      <div class="stat-card highlight">
        <div class="stat-icon">🔓</div>
        <div class="stat-info">
          <span class="stat-value">{{ stats.exploitable }}</span>
          <span class="stat-label">可利用漏洞</span>
        </div>
      </div>
      <div class="stat-card">
        <div class="stat-icon">🔥</div>
        <div class="stat-info">
          <span class="stat-value">{{ stats.carbonara }}</span>
          <span class="stat-label">Carbonara</span>
        </div>
      </div>
      <div class="stat-card">
        <div class="stat-icon">⚡</div>
        <div class="stat-info">
          <span class="stat-value">{{ stats.allinone }}</span>
          <span class="stat-label">ALLINONE</span>
        </div>
      </div>
    </div>
    
    <!-- 搜索和筛选 -->
    <div class="filter-bar glass">
      <div class="search-box">
        <svg width="20" height="20" viewBox="0 0 24 24" fill="currentColor">
          <path d="M15.5 14h-.79l-.28-.27A6.471 6.471 0 0016 9.5 6.5 6.5 0 109.5 16c1.61 0 3.09-.59 4.23-1.57l.27.28v.79l5 4.99L20.49 19l-4.99-5zm-6 0C7.01 14 5 11.99 5 9.5S7.01 5 9.5 5 14 7.01 14 9.5 11.99 14 9.5 14z"/>
        </svg>
        <input 
          v-model="searchQuery" 
          type="text" 
          placeholder="搜索芯片名称或型号..."
          @input="handleSearch"
        />
      </div>
      <div class="filter-group">
        <select v-model="selectedSeries" @change="fetchChips">
          <option value="">全部系列</option>
          <option value="Helio P">Helio P</option>
          <option value="Helio G">Helio G</option>
          <option value="Helio A">Helio A</option>
          <option value="Helio X">Helio X</option>
          <option value="Dimensity">Dimensity</option>
          <option value="Dimensity 9000">Dimensity 9000</option>
          <option value="Legacy">Legacy</option>
          <option value="MT8xxx">MT8xxx</option>
          <option value="Entry">Entry</option>
        </select>
      </div>
      <div class="filter-group">
        <label class="checkbox-label">
          <input type="checkbox" v-model="showExploitable" @change="fetchChips" />
          <span>仅显示可利用</span>
        </label>
      </div>
    </div>
    
    <!-- 芯片列表 -->
    <div class="chips-grid" v-if="!loading && chips.length">
      <div 
        v-for="chip in chips" 
        :key="chip.hw_code" 
        class="chip-card"
        :class="{ exploitable: chip.has_exploit }"
      >
        <div class="chip-header">
          <span class="chip-series">{{ chip.series }}</span>
          <span v-if="chip.has_exploit" class="exploit-badge">{{ chip.exploit_type || '可利用' }}</span>
        </div>
        <h3 class="chip-name">{{ chip.name }}</h3>
        <p class="chip-desc">{{ chip.description }}</p>
        <div class="chip-info">
          <div class="info-row">
            <span class="info-label">HW Code</span>
            <code class="info-value">{{ chip.hw_code }}</code>
          </div>
          <div class="info-row">
            <span class="info-label">架构</span>
            <span class="info-value">{{ chip.is_64bit ? '64-bit' : '32-bit' }}</span>
          </div>
        </div>
        <div class="chip-brands" v-if="chip.brands?.length">
          <span class="brands-label">使用品牌:</span>
          <div class="brands-list">
            <span v-for="brand in chip.brands" :key="brand" class="brand-tag">{{ brand }}</span>
          </div>
        </div>
        <div class="chip-status">
          <span v-if="chip.has_exploit" class="status-badge exploitable">🔓 {{ chip.exploit_type || '可利用' }}</span>
          <span v-else-if="chip.brom_patched" class="status-badge patched">🔒 BROM 已修补</span>
          <span v-else class="status-badge normal">✓ 支持</span>
        </div>
      </div>
    </div>
    
    <div v-if="loading" class="loading">
      <div class="spinner"></div>
      <p>加载中...</p>
    </div>
    <div v-if="!loading && !chips.length" class="empty-state">
      <p>未找到匹配的芯片</p>
    </div>
  </div>
</template>

<script setup>
import { ref, reactive, onMounted } from 'vue'
import api from '@/api'

const chips = ref([])
const loading = ref(true)
const searchQuery = ref('')
const selectedSeries = ref('')
const showExploitable = ref(false)
const stats = reactive({
  total: 0,
  exploitable: 0,
  carbonara: 0,
  allinone: 0
})

let searchTimeout = null

const fetchChips = async () => {
  loading.value = true
  try {
    const params = {}
    if (searchQuery.value) params.q = searchQuery.value
    if (selectedSeries.value) params.series = selectedSeries.value
    
    const res = await api.getMtkChips(params)
    if (res.code === 0) {
      let result = res.data.chips || []
      if (showExploitable.value) {
        result = result.filter(c => c.has_exploit)
      }
      chips.value = result
    }
  } catch (e) {
    console.error('获取 MTK 芯片列表失败', e)
  } finally {
    loading.value = false
  }
}

const fetchStats = async () => {
  try {
    const res = await api.getMtkStats()
    if (res.code === 0) {
      Object.assign(stats, res.data)
    }
  } catch (e) {
    console.error('获取统计失败', e)
  }
}

const handleSearch = () => {
  clearTimeout(searchTimeout)
  searchTimeout = setTimeout(fetchChips, 300)
}

onMounted(() => {
  fetchChips()
  fetchStats()
})
</script>

<style lang="scss" scoped>
.mtk-page {
  padding: 48px 24px 80px;
}

.page-header {
  text-align: center;
  margin-bottom: 40px;
  
  .header-content {
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: 16px;
  }
  
  h1 {
    font-size: 2.5rem;
    margin: 0;
  }
  
  .gradient-text {
    background: linear-gradient(135deg, #f59e0b 0%, #ef4444 100%);
    -webkit-background-clip: text;
    -webkit-text-fill-color: transparent;
    background-clip: text;
  }
  
  .page-desc {
    color: var(--vp-c-text-3);
    margin: 0;
    max-width: 600px;
  }
}

.platform-badge {
  display: inline-flex;
  align-items: center;
  padding: 8px 20px;
  border-radius: 20px;
  font-weight: 700;
  font-size: 14px;
  letter-spacing: 1px;
  
  &.mtk {
    background: linear-gradient(135deg, rgba(245, 158, 11, 0.15), rgba(239, 68, 68, 0.15));
    color: #f59e0b;
    border: 1px solid rgba(245, 158, 11, 0.3);
  }
}

.stats-row {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 16px;
  margin-bottom: 32px;
  
  @media (max-width: 768px) {
    grid-template-columns: repeat(2, 1fr);
  }
}

.stat-card {
  display: flex;
  align-items: center;
  gap: 16px;
  padding: 20px;
  background: var(--vp-c-bg);
  border: 1px solid var(--vp-c-divider);
  border-radius: 16px;
  transition: all 0.3s;
  
  &:hover {
    transform: translateY(-2px);
    box-shadow: var(--vp-shadow-2);
  }
  
  &.highlight {
    background: linear-gradient(135deg, rgba(245, 158, 11, 0.1), rgba(239, 68, 68, 0.05));
    border-color: rgba(245, 158, 11, 0.3);
  }
  
  .stat-icon {
    font-size: 28px;
  }
  
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

.filter-bar {
  display: flex;
  gap: 16px;
  padding: 16px 20px;
  margin-bottom: 24px;
  background: rgba(255, 255, 255, 0.7);
  backdrop-filter: blur(10px);
  border: 1px solid var(--vp-c-divider);
  border-radius: 16px;
  flex-wrap: wrap;
  align-items: center;
  
  .dark & {
    background: rgba(27, 27, 31, 0.7);
  }
}

.search-box {
  flex: 1;
  min-width: 200px;
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 10px 16px;
  background: var(--vp-c-bg);
  border: 1px solid var(--vp-c-divider);
  border-radius: 10px;
  
  svg { color: var(--vp-c-text-3); }
  
  input {
    flex: 1;
    border: none;
    background: none;
    font-size: 14px;
    color: var(--vp-c-text-1);
    outline: none;
    
    &::placeholder { color: var(--vp-c-text-3); }
  }
}

.filter-group {
  select {
    padding: 12px 16px;
    border: 1px solid var(--vp-c-divider);
    border-radius: 10px;
    background: var(--vp-c-bg);
    color: var(--vp-c-text-1);
    font-size: 14px;
    cursor: pointer;
    
    &:focus {
      outline: none;
      border-color: #f59e0b;
    }
  }
}

.checkbox-label {
  display: flex;
  align-items: center;
  gap: 8px;
  cursor: pointer;
  font-size: 14px;
  color: var(--vp-c-text-2);
  
  input {
    width: 18px;
    height: 18px;
    accent-color: #f59e0b;
  }
}

.chips-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(320px, 1fr));
  gap: 20px;
}

.chip-card {
  background: var(--vp-c-bg);
  border: 1px solid var(--vp-c-divider);
  border-radius: 16px;
  padding: 24px;
  transition: all 0.3s;
  
  &:hover {
    transform: translateY(-4px);
    box-shadow: var(--vp-shadow-3);
    border-color: rgba(245, 158, 11, 0.3);
  }
  
  &.exploitable {
    border-left: 3px solid #f59e0b;
  }
}

.chip-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 12px;
}

.chip-series {
  font-size: 12px;
  font-weight: 600;
  padding: 4px 10px;
  background: linear-gradient(135deg, rgba(245, 158, 11, 0.1), rgba(239, 68, 68, 0.1));
  color: #f59e0b;
  border-radius: 6px;
}

.exploit-badge {
  font-size: 11px;
  font-weight: 600;
  padding: 3px 8px;
  background: rgba(239, 68, 68, 0.1);
  color: #ef4444;
  border-radius: 4px;
}

.chip-name {
  font-size: 1.2rem;
  margin: 0 0 8px;
  color: var(--vp-c-text-1);
}

.chip-desc {
  font-size: 14px;
  color: var(--vp-c-text-2);
  margin: 0 0 16px;
}

.chip-info {
  display: flex;
  flex-direction: column;
  gap: 8px;
  margin-bottom: 16px;
}

.info-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.info-label {
  font-size: 13px;
  color: var(--vp-c-text-3);
}

.info-value {
  font-size: 13px;
  color: var(--vp-c-text-2);
  
  &.da-mode {
    padding: 2px 8px;
    border-radius: 4px;
    font-size: 12px;
    
    &.xml { background: rgba(59, 130, 246, 0.1); color: #3b82f6; }
    &.xflash { background: rgba(139, 92, 246, 0.1); color: #8b5cf6; }
    &.legacy { background: rgba(107, 114, 128, 0.1); color: #6b7280; }
  }
}

.chip-brands {
  margin-bottom: 16px;
  
  .brands-label {
    font-size: 12px;
    color: var(--vp-c-text-3);
    display: block;
    margin-bottom: 8px;
  }
  
  .brands-list {
    display: flex;
    flex-wrap: wrap;
    gap: 6px;
  }
  
  .brand-tag {
    display: inline-block;
    padding: 3px 10px;
    background: linear-gradient(135deg, rgba(245, 158, 11, 0.08), rgba(239, 68, 68, 0.08));
    color: var(--vp-c-text-2);
    border-radius: 12px;
    font-size: 12px;
    font-weight: 500;
  }
}

.chip-status {
  text-align: center;
}

.status-badge {
  display: inline-block;
  padding: 6px 16px;
  border-radius: 20px;
  font-size: 13px;
  font-weight: 600;
  
  &.exploitable {
    background: rgba(245, 158, 11, 0.1);
    color: #f59e0b;
  }
  
  &.patched {
    background: rgba(239, 68, 68, 0.1);
    color: #ef4444;
  }
  
  &.normal {
    background: rgba(16, 185, 129, 0.1);
    color: #10b981;
  }
}

.loading {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 60px 20px;
  color: var(--vp-c-text-3);
  
  .spinner {
    width: 40px;
    height: 40px;
    border: 3px solid var(--vp-c-divider);
    border-top-color: #f59e0b;
    border-radius: 50%;
    animation: spin 1s linear infinite;
  }
}

@keyframes spin {
  to { transform: rotate(360deg); }
}

.empty-state {
  text-align: center;
  padding: 60px 20px;
  color: var(--vp-c-text-3);
}
</style>
