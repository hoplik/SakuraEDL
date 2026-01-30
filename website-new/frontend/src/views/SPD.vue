<template>
  <div class="spd-page container">
    <div class="page-header">
      <div class="header-content">
        <div class="platform-badge spd">SPD</div>
        <h1>展锐 <span class="gradient-text">芯片数据库</span></h1>
        <p class="page-desc">SakuraEDL 支持的 Spreadtrum/UNISOC 芯片与设备列表</p>
      </div>
    </div>
    
    <!-- 标签页切换 -->
    <div class="tab-bar">
      <button 
        :class="['tab-btn', { active: activeTab === 'chips' }]" 
        @click="activeTab = 'chips'"
      >
        📱 芯片列表
      </button>
      <button 
        :class="['tab-btn', { active: activeTab === 'devices' }]" 
        @click="activeTab = 'devices'"
      >
        📦 设备支持
      </button>
    </div>
    
    <!-- 统计卡片 -->
    <div class="stats-row" v-if="stats">
      <div class="stat-card">
        <div class="stat-icon">📱</div>
        <div class="stat-info">
          <span class="stat-value">{{ stats.total_chips }}</span>
          <span class="stat-label">芯片型号</span>
        </div>
      </div>
      <div class="stat-card">
        <div class="stat-icon">📦</div>
        <div class="stat-info">
          <span class="stat-value">{{ stats.total_devices }}</span>
          <span class="stat-label">设备支持</span>
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
        <div class="stat-icon">🏭</div>
        <div class="stat-info">
          <span class="stat-value">{{ Object.keys(stats.by_brand || {}).length }}</span>
          <span class="stat-label">品牌覆盖</span>
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
          :placeholder="activeTab === 'chips' ? '搜索芯片型号...' : '搜索设备名称...'"
          @input="handleSearch"
        />
      </div>
      <div class="filter-group" v-if="activeTab === 'chips'">
        <select v-model="selectedSeries" @change="fetchChips">
          <option value="">全部系列</option>
          <option value="T3xx">Tiger T3xx</option>
          <option value="T4xx">Tiger T4xx</option>
          <option value="T6xx">Tiger T6xx</option>
          <option value="T7xx">Tiger T7xx</option>
          <option value="T8xx">Tiger T8xx</option>
          <option value="SC77xx">SC77xx</option>
          <option value="SC85xx">SC85xx</option>
          <option value="SC98xx">SC98xx</option>
          <option value="SC65xx">SC65xx</option>
          <option value="UMS">UMS</option>
          <option value="T1xx">T1xx</option>
        </select>
      </div>
      <div class="filter-group" v-if="activeTab === 'devices'">
        <select v-model="selectedBrand" @change="fetchDevices">
          <option value="">全部品牌</option>
          <option value="Samsung">Samsung</option>
          <option value="Realme">Realme</option>
          <option value="Infinix">Infinix</option>
          <option value="Nokia">Nokia</option>
          <option value="Blackview">Blackview</option>
          <option value="Lenovo">Lenovo</option>
          <option value="Vivo">Vivo</option>
        </select>
      </div>
    </div>
    
    <!-- 芯片列表 -->
    <div v-if="activeTab === 'chips'" class="chips-grid">
      <div 
        v-for="chip in chips" 
        :key="chip.chip_id" 
        class="chip-card"
        :class="{ exploitable: chip.has_exploit }"
      >
        <div class="chip-header">
          <span class="chip-series">{{ chip.series }}</span>
          <span v-if="chip.has_exploit" class="exploit-badge">🔓 {{ chip.exploit_id }}</span>
        </div>
        <h3 class="chip-name">{{ chip.name }}</h3>
        <p class="chip-desc">{{ chip.description }}</p>
        <div class="chip-info">
          <div class="info-row">
            <span class="info-label">Chip ID</span>
            <code class="info-value">{{ chip.chip_id }}</code>
          </div>
          <div class="info-row">
            <span class="info-label">存储</span>
            <span class="info-value storage-type">{{ chip.storage }}</span>
          </div>
        </div>
        <div class="chip-brands" v-if="chip.brands?.length">
          <span class="brands-label">使用品牌:</span>
          <div class="brands-list">
            <span v-for="brand in chip.brands" :key="brand" class="brand-tag">{{ brand }}</span>
          </div>
        </div>
        <div class="chip-status">
          <span v-if="chip.has_exploit" class="status-badge exploitable">🔓 可利用</span>
          <span v-else class="status-badge normal">✓ 支持</span>
        </div>
      </div>
    </div>
    
    <!-- 设备列表 -->
    <div v-if="activeTab === 'devices'" class="devices-table">
      <table>
        <thead>
          <tr>
            <th>品牌</th>
            <th>设备</th>
            <th>芯片</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="device in devices" :key="`${device.chip}-${device.device}`">
            <td>
              <span class="brand-badge">{{ device.brand }}</span>
            </td>
            <td>
              <strong>{{ device.device }}</strong>
            </td>
            <td>
              <code>{{ device.chip }}</code>
            </td>
          </tr>
        </tbody>
      </table>
      <div v-if="!devices.length" class="empty-table">
        暂无匹配设备
      </div>
    </div>
    
    <div v-if="loading" class="loading">
      <div class="spinner"></div>
      <p>加载中...</p>
    </div>
  </div>
</template>

<script setup>
import { ref, reactive, onMounted, watch } from 'vue'
import api from '@/api'

const activeTab = ref('chips')
const chips = ref([])
const devices = ref([])
const loading = ref(true)
const searchQuery = ref('')
const selectedSeries = ref('')
const selectedBrand = ref('')
const stats = reactive({
  total_chips: 0,
  total_devices: 0,
  exploitable: 0,
  by_series: {},
  by_brand: {}
})

let searchTimeout = null

const fetchChips = async () => {
  loading.value = true
  try {
    const params = {}
    if (searchQuery.value) params.q = searchQuery.value
    if (selectedSeries.value) params.series = selectedSeries.value
    
    const res = await api.getSpdChips(params)
    if (res.code === 0) {
      chips.value = res.data.chips || []
    }
  } catch (e) {
    console.error('获取 SPD 芯片列表失败', e)
  } finally {
    loading.value = false
  }
}

const fetchDevices = async () => {
  loading.value = true
  try {
    const params = {}
    if (searchQuery.value) params.q = searchQuery.value
    if (selectedBrand.value) params.brand = selectedBrand.value
    
    const res = await api.getSpdDevices(params)
    if (res.code === 0) {
      devices.value = res.data.devices || []
    }
  } catch (e) {
    console.error('获取 SPD 设备列表失败', e)
  } finally {
    loading.value = false
  }
}

const fetchStats = async () => {
  try {
    const res = await api.getSpdStats()
    if (res.code === 0) {
      Object.assign(stats, res.data)
    }
  } catch (e) {
    console.error('获取统计失败', e)
  }
}

const handleSearch = () => {
  clearTimeout(searchTimeout)
  searchTimeout = setTimeout(() => {
    if (activeTab.value === 'chips') {
      fetchChips()
    } else {
      fetchDevices()
    }
  }, 300)
}

watch(activeTab, (newTab) => {
  searchQuery.value = ''
  if (newTab === 'chips') {
    fetchChips()
  } else {
    fetchDevices()
  }
})

onMounted(() => {
  fetchChips()
  fetchDevices()
  fetchStats()
})
</script>

<style lang="scss" scoped>
.spd-page {
  padding: 48px 24px 80px;
}

.page-header {
  text-align: center;
  margin-bottom: 32px;
  
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
    background: linear-gradient(135deg, #10b981 0%, #06b6d4 100%);
    -webkit-background-clip: text;
    -webkit-text-fill-color: transparent;
    background-clip: text;
  }
  
  .page-desc {
    color: var(--vp-c-text-3);
    margin: 0;
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
  
  &.spd {
    background: linear-gradient(135deg, rgba(16, 185, 129, 0.15), rgba(6, 182, 212, 0.15));
    color: #10b981;
    border: 1px solid rgba(16, 185, 129, 0.3);
  }
}

.tab-bar {
  display: flex;
  gap: 8px;
  margin-bottom: 24px;
  padding: 4px;
  background: var(--vp-c-bg-soft);
  border-radius: 12px;
  width: fit-content;
}

.tab-btn {
  padding: 10px 24px;
  border: none;
  border-radius: 8px;
  font-size: 14px;
  font-weight: 500;
  cursor: pointer;
  background: transparent;
  color: var(--vp-c-text-2);
  transition: all 0.3s;
  
  &:hover {
    color: var(--vp-c-text-1);
  }
  
  &.active {
    background: linear-gradient(135deg, #10b981 0%, #06b6d4 100%);
    color: white;
    box-shadow: 0 4px 12px rgba(16, 185, 129, 0.3);
  }
}

.stats-row {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 16px;
  margin-bottom: 24px;
  
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
    background: linear-gradient(135deg, rgba(16, 185, 129, 0.1), rgba(6, 182, 212, 0.05));
    border-color: rgba(16, 185, 129, 0.3);
  }
  
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

.filter-group select {
  padding: 12px 16px;
  border: 1px solid var(--vp-c-divider);
  border-radius: 10px;
  background: var(--vp-c-bg);
  color: var(--vp-c-text-1);
  font-size: 14px;
  cursor: pointer;
  
  &:focus {
    outline: none;
    border-color: #10b981;
  }
}

.chips-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(300px, 1fr));
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
    border-color: rgba(16, 185, 129, 0.3);
  }
  
  &.exploitable {
    border-left: 3px solid #10b981;
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
  background: linear-gradient(135deg, rgba(16, 185, 129, 0.1), rgba(6, 182, 212, 0.1));
  color: #10b981;
  border-radius: 6px;
}

.exploit-badge {
  font-size: 11px;
  font-weight: 600;
  padding: 3px 8px;
  background: rgba(16, 185, 129, 0.1);
  color: #10b981;
  border-radius: 4px;
}

.chip-name {
  font-size: 1.1rem;
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
  
  &.storage-type {
    padding: 2px 8px;
    border-radius: 4px;
    background: rgba(6, 182, 212, 0.1);
    color: #06b6d4;
    font-size: 12px;
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
    background: linear-gradient(135deg, rgba(16, 185, 129, 0.08), rgba(6, 182, 212, 0.08));
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
    background: rgba(16, 185, 129, 0.1);
    color: #10b981;
  }
  
  &.normal {
    background: rgba(59, 130, 246, 0.1);
    color: #3b82f6;
  }
}

.devices-table {
  background: var(--vp-c-bg);
  border: 1px solid var(--vp-c-divider);
  border-radius: 16px;
  overflow: hidden;
  
  table {
    width: 100%;
    border-collapse: collapse;
    margin: 0;
    
    th, td {
      padding: 14px 20px;
      text-align: left;
      border-bottom: 1px solid var(--vp-c-divider);
    }
    
    th {
      background: var(--vp-c-bg-soft);
      font-weight: 600;
      color: var(--vp-c-text-2);
    }
    
    tr:last-child td {
      border-bottom: none;
    }
    
    tr:hover td {
      background: var(--vp-c-bg-soft);
    }
  }
  
  .brand-badge {
    display: inline-block;
    padding: 4px 12px;
    border-radius: 6px;
    font-size: 12px;
    font-weight: 600;
    background: linear-gradient(135deg, rgba(16, 185, 129, 0.1), rgba(6, 182, 212, 0.1));
    color: #10b981;
  }
  
  code {
    font-size: 13px;
    padding: 2px 8px;
    background: var(--vp-c-bg-soft);
    border-radius: 4px;
  }
}

.empty-table {
  padding: 40px;
  text-align: center;
  color: var(--vp-c-text-3);
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
    border-top-color: #10b981;
    border-radius: 50%;
    animation: spin 1s linear infinite;
  }
}

@keyframes spin {
  to { transform: rotate(360deg); }
}
</style>
