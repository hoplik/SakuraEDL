<template>
  <div class="api-page container">
    <div class="page-header">
      <h1>API <span class="gradient-text">文档</span></h1>
      <p class="page-desc">SakuraEDL 云端服务 API - 可在线测试</p>
    </div>
    
    <div class="api-info glass">
      <div class="info-item">
        <div class="info-icon">🌐</div>
        <div class="info-content">
          <strong>Base URL</strong>
          <code>https://api.sakuraedl.org/api</code>
        </div>
      </div>
      <div class="info-item">
        <div class="info-icon">📋</div>
        <div class="info-content">
          <strong>Content-Type</strong>
          <code>application/json</code>
        </div>
      </div>
      <div class="info-item status-item">
        <div class="info-icon">{{ apiStatus.icon }}</div>
        <div class="info-content">
          <strong>API 状态</strong>
          <span :class="['status-badge', apiStatus.class]">
            {{ apiStatus.text }}
            <span v-if="apiStatus.latency" class="latency">{{ apiStatus.latency }}ms</span>
          </span>
        </div>
      </div>
    </div>
    
    <section class="api-section">
      <h2>公开接口</h2>
      
      <!-- 获取 Loader 列表 -->
      <div class="api-card">
        <div class="api-header">
          <span class="method get">GET</span>
          <code class="endpoint">/loaders/list</code>
          <button class="try-btn" @click="tryGetLoaders" :disabled="loading.loaders">
            {{ loading.loaders ? '请求中...' : '尝试请求' }}
          </button>
        </div>
        <p class="api-desc">获取所有可用的 Loader 列表</p>
        
        <div class="response-box" v-if="responses.loaders">
          <div class="response-header">
            <span :class="['status', responses.loaders.success ? 'success' : 'error']">
              {{ responses.loaders.success ? '✓ 成功' : '✗ 失败' }}
            </span>
            <span class="time">{{ responses.loaders.time }}ms</span>
          </div>
          <pre><code>{{ formatJson(responses.loaders.data) }}</code></pre>
        </div>
      </div>
      
      <!-- 匹配 Loader -->
      <div class="api-card">
        <div class="api-header">
          <span class="method post">POST</span>
          <code class="endpoint">/loaders/match</code>
          <button class="try-btn" @click="showMatchModal = true">尝试请求</button>
        </div>
        <p class="api-desc">根据设备信息自动匹配最佳 Loader</p>
        <h4>请求参数</h4>
        <table>
          <thead>
            <tr><th>参数</th><th>类型</th><th>必填</th><th>说明</th></tr>
          </thead>
          <tbody>
            <tr><td>msm_id</td><td>string</td><td>是</td><td>MSM 芯片 ID (如: 009600E1)</td></tr>
            <tr><td>pk_hash</td><td>string</td><td>否</td><td>PK Hash</td></tr>
            <tr><td>oem_id</td><td>string</td><td>否</td><td>OEM ID</td></tr>
            <tr><td>storage_type</td><td>string</td><td>否</td><td>存储类型 (ufs/emmc)</td></tr>
          </tbody>
        </table>
        
        <div class="response-box" v-if="responses.match">
          <div class="response-header">
            <span :class="['status', responses.match.success ? 'success' : 'error']">
              {{ responses.match.success ? '✓ 成功' : '✗ 失败' }}
            </span>
            <span class="time">{{ responses.match.time }}ms</span>
          </div>
          <pre><code>{{ formatJson(responses.match.data) }}</code></pre>
        </div>
      </div>
      
      <!-- 下载 Loader -->
      <div class="api-card">
        <div class="api-header">
          <span class="method get">GET</span>
          <code class="endpoint">/loaders/{id}/download</code>
        </div>
        <p class="api-desc">下载指定 ID 的 Loader 文件</p>
        <p>返回二进制文件流，需要在客户端保存为 .elf 或 .melf 文件</p>
      </div>
      
      <!-- 上报设备日志 -->
      <div class="api-card">
        <div class="api-header">
          <span class="method post">POST</span>
          <code class="endpoint">/device-logs</code>
        </div>
        <p class="api-desc">上报设备连接日志（用于统计）</p>
        <h4>请求参数</h4>
        <table>
          <thead>
            <tr><th>参数</th><th>类型</th><th>说明</th></tr>
          </thead>
          <tbody>
            <tr><td>platform</td><td>string</td><td>平台 (qualcomm)</td></tr>
            <tr><td>sahara_version</td><td>int</td><td>Sahara 协议版本 (1/2/3)</td></tr>
            <tr><td>msm_id</td><td>string</td><td>MSM ID</td></tr>
            <tr><td>pk_hash</td><td>string</td><td>PK Hash</td></tr>
            <tr><td>chip_name</td><td>string</td><td>芯片名称</td></tr>
            <tr><td>vendor</td><td>string</td><td>厂商名称</td></tr>
            <tr><td>storage_type</td><td>string</td><td>存储类型 (ufs/emmc)</td></tr>
            <tr><td>match_result</td><td>string</td><td>结果 (success/failed/info_collected)</td></tr>
          </tbody>
        </table>
      </div>
    </section>
    
    <!-- 设备数据库 -->
    <section class="api-section">
      <h2>设备数据库</h2>
      
      <div class="api-card">
        <div class="api-header">
          <span class="method get">GET</span>
          <code class="endpoint">/local/chips</code>
          <button class="try-btn" @click="tryGetChips" :disabled="loading.chips">
            {{ loading.chips ? '请求中...' : '尝试请求' }}
          </button>
        </div>
        <p class="api-desc">获取支持的芯片列表</p>
        <h4>查询参数</h4>
        <table>
          <thead>
            <tr><th>参数</th><th>类型</th><th>说明</th></tr>
          </thead>
          <tbody>
            <tr><td>q</td><td>string</td><td>搜索关键词</td></tr>
            <tr><td>series</td><td>string</td><td>芯片系列 (Snapdragon 8/7/6/4)</td></tr>
          </tbody>
        </table>
        <div class="response-box" v-if="responses.chips">
          <div class="response-header">
            <span :class="['status', responses.chips.success ? 'success' : 'error']">
              {{ responses.chips.success ? '✓ 成功' : '✗ 失败' }}
            </span>
            <span class="time">{{ responses.chips.time }}ms</span>
          </div>
          <pre><code>{{ formatJson(responses.chips.data) }}</code></pre>
        </div>
      </div>
      
      <div class="api-card">
        <div class="api-header">
          <span class="method get">GET</span>
          <code class="endpoint">/local/chips/{msm_id}</code>
        </div>
        <p class="api-desc">获取指定芯片的详细信息</p>
      </div>
      
      <div class="api-card">
        <div class="api-header">
          <span class="method get">GET</span>
          <code class="endpoint">/local/vendors</code>
          <button class="try-btn" @click="tryGetVendors" :disabled="loading.vendors">
            {{ loading.vendors ? '请求中...' : '尝试请求' }}
          </button>
        </div>
        <p class="api-desc">获取支持的厂商列表</p>
        <div class="response-box" v-if="responses.vendors">
          <div class="response-header">
            <span :class="['status', responses.vendors.success ? 'success' : 'error']">
              {{ responses.vendors.success ? '✓ 成功' : '✗ 失败' }}
            </span>
            <span class="time">{{ responses.vendors.time }}ms</span>
          </div>
          <pre><code>{{ formatJson(responses.vendors.data) }}</code></pre>
        </div>
      </div>
    </section>
    
    <!-- 统计分析 -->
    <section class="api-section">
      <h2>统计分析</h2>
      
      <div class="api-card">
        <div class="api-header">
          <span class="method get">GET</span>
          <code class="endpoint">/local/stats/chips</code>
          <button class="try-btn" @click="tryGetStatsChips" :disabled="loading.statsChips">
            {{ loading.statsChips ? '请求中...' : '尝试请求' }}
          </button>
        </div>
        <p class="api-desc">获取芯片统计数据</p>
        <div class="response-box" v-if="responses.statsChips">
          <div class="response-header">
            <span :class="['status', responses.statsChips.success ? 'success' : 'error']">
              {{ responses.statsChips.success ? '✓ 成功' : '✗ 失败' }}
            </span>
            <span class="time">{{ responses.statsChips.time }}ms</span>
          </div>
          <pre><code>{{ formatJson(responses.statsChips.data) }}</code></pre>
        </div>
      </div>
      
      <div class="api-card">
        <div class="api-header">
          <span class="method get">GET</span>
          <code class="endpoint">/local/stats/hot</code>
          <button class="try-btn" @click="tryGetStatsHot" :disabled="loading.statsHot">
            {{ loading.statsHot ? '请求中...' : '尝试请求' }}
          </button>
        </div>
        <p class="api-desc">获取热门设备 TOP 10</p>
        <div class="response-box" v-if="responses.statsHot">
          <div class="response-header">
            <span :class="['status', responses.statsHot.success ? 'success' : 'error']">
              {{ responses.statsHot.success ? '✓ 成功' : '✗ 失败' }}
            </span>
            <span class="time">{{ responses.statsHot.time }}ms</span>
          </div>
          <pre><code>{{ formatJson(responses.statsHot.data) }}</code></pre>
        </div>
      </div>
      
      <div class="api-card">
        <div class="api-header">
          <span class="method get">GET</span>
          <code class="endpoint">/local/stats/trends</code>
          <button class="try-btn" @click="tryGetStatsTrends" :disabled="loading.statsTrends">
            {{ loading.statsTrends ? '请求中...' : '尝试请求' }}
          </button>
        </div>
        <p class="api-desc">获取最近 7 天趋势分析</p>
        <div class="response-box" v-if="responses.statsTrends">
          <div class="response-header">
            <span :class="['status', responses.statsTrends.success ? 'success' : 'error']">
              {{ responses.statsTrends.success ? '✓ 成功' : '✗ 失败' }}
            </span>
            <span class="time">{{ responses.statsTrends.time }}ms</span>
          </div>
          <pre><code>{{ formatJson(responses.statsTrends.data) }}</code></pre>
        </div>
      </div>
    </section>
    
    <!-- 社区功能 -->
    <section class="api-section">
      <h2>社区功能</h2>
      
      <div class="api-card">
        <div class="api-header">
          <span class="method get">GET</span>
          <code class="endpoint">/local/announcements</code>
          <button class="try-btn" @click="tryGetAnnouncements" :disabled="loading.announcements">
            {{ loading.announcements ? '请求中...' : '尝试请求' }}
          </button>
        </div>
        <p class="api-desc">获取公告列表</p>
        <div class="response-box" v-if="responses.announcements">
          <div class="response-header">
            <span :class="['status', responses.announcements.success ? 'success' : 'error']">
              {{ responses.announcements.success ? '✓ 成功' : '✗ 失败' }}
            </span>
            <span class="time">{{ responses.announcements.time }}ms</span>
          </div>
          <pre><code>{{ formatJson(responses.announcements.data) }}</code></pre>
        </div>
      </div>
      
      <div class="api-card">
        <div class="api-header">
          <span class="method get">GET</span>
          <code class="endpoint">/local/changelog</code>
          <button class="try-btn" @click="tryGetChangelog" :disabled="loading.changelog">
            {{ loading.changelog ? '请求中...' : '尝试请求' }}
          </button>
        </div>
        <p class="api-desc">获取更新日志</p>
        <div class="response-box" v-if="responses.changelog">
          <div class="response-header">
            <span :class="['status', responses.changelog.success ? 'success' : 'error']">
              {{ responses.changelog.success ? '✓ 成功' : '✗ 失败' }}
            </span>
            <span class="time">{{ responses.changelog.time }}ms</span>
          </div>
          <pre><code>{{ formatJson(responses.changelog.data) }}</code></pre>
        </div>
      </div>
      
      <div class="api-card">
        <div class="api-header">
          <span class="method post">POST</span>
          <code class="endpoint">/local/feedback</code>
        </div>
        <p class="api-desc">提交用户反馈</p>
        <h4>请求参数</h4>
        <table>
          <thead>
            <tr><th>参数</th><th>类型</th><th>必填</th><th>说明</th></tr>
          </thead>
          <tbody>
            <tr><td>type</td><td>string</td><td>是</td><td>类型 (bug/feature/question)</td></tr>
            <tr><td>content</td><td>string</td><td>是</td><td>反馈内容</td></tr>
            <tr><td>contact</td><td>string</td><td>否</td><td>联系方式</td></tr>
            <tr><td>device</td><td>string</td><td>否</td><td>设备信息</td></tr>
          </tbody>
        </table>
      </div>
    </section>
    
    <section class="api-section">
      <h2>认证类型</h2>
      <table>
        <thead>
          <tr><th>auth_type</th><th>说明</th><th>适用设备</th></tr>
        </thead>
        <tbody>
          <tr><td><code>none</code></td><td>无需认证</td><td>老款设备</td></tr>
          <tr><td><code>miauth</code></td><td>小米认证</td><td>小米/红米</td></tr>
          <tr><td><code>demacia</code></td><td>OnePlus 认证</td><td>一加 (旧版)</td></tr>
          <tr><td><code>vip</code></td><td>VIP 认证</td><td>OPPO/OnePlus (新版)</td></tr>
        </tbody>
      </table>
    </section>
    
    <section class="api-section">
      <h2>错误码</h2>
      <table>
        <thead>
          <tr><th>code</th><th>说明</th></tr>
        </thead>
        <tbody>
          <tr><td><code>0</code></td><td>成功</td></tr>
          <tr><td><code>400</code></td><td>请求参数错误</td></tr>
          <tr><td><code>404</code></td><td>资源不存在 / 未找到匹配</td></tr>
          <tr><td><code>500</code></td><td>服务器内部错误</td></tr>
        </tbody>
      </table>
    </section>
    
    <!-- 匹配请求弹窗 -->
    <div class="modal-overlay" v-if="showMatchModal" @click.self="showMatchModal = false">
      <div class="modal">
        <h3>测试 Loader 匹配</h3>
        <div class="form-group">
          <label>MSM ID <span class="required">*</span></label>
          <input v-model="matchParams.msm_id" placeholder="如: 009600E1" />
        </div>
        <div class="form-group">
          <label>PK Hash</label>
          <input v-model="matchParams.pk_hash" placeholder="可选" />
        </div>
        <div class="form-group">
          <label>OEM ID</label>
          <input v-model="matchParams.oem_id" placeholder="可选" />
        </div>
        <div class="form-group">
          <label>存储类型</label>
          <select v-model="matchParams.storage_type">
            <option value="ufs">UFS</option>
            <option value="emmc">eMMC</option>
          </select>
        </div>
        <div class="modal-actions">
          <button class="vp-button secondary" @click="showMatchModal = false">取消</button>
          <button class="vp-button primary" @click="tryMatchLoader" :disabled="loading.match">
            {{ loading.match ? '请求中...' : '发送请求' }}
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, reactive, computed, onMounted } from 'vue'
import api from '@/api'

const loading = reactive({
  loaders: false,
  match: false,
  health: true,
  chips: false,
  vendors: false,
  statsChips: false,
  statsHot: false,
  statsTrends: false,
  announcements: false,
  changelog: false
})

const responses = reactive({
  loaders: null,
  match: null,
  chips: null,
  vendors: null,
  statsChips: null,
  statsHot: null,
  statsTrends: null,
  announcements: null,
  changelog: null
})

const healthStatus = ref(null)

const apiStatus = computed(() => {
  if (loading.health) {
    return { icon: '⏳', text: '检测中...', class: 'pending' }
  }
  if (!healthStatus.value) {
    return { icon: '❓', text: '未知', class: 'unknown' }
  }
  if (healthStatus.value.status === 'ok') {
    return { 
      icon: '✅', 
      text: '正常', 
      class: 'online',
      latency: healthStatus.value.latency
    }
  }
  return { icon: '❌', text: '离线', class: 'offline' }
})

const showMatchModal = ref(false)
const matchParams = reactive({
  msm_id: '',
  pk_hash: '',
  oem_id: '',
  storage_type: 'ufs'
})

const formatJson = (data) => {
  try {
    // 移除内部字段
    const cleaned = { ...data }
    delete cleaned._duration
    delete cleaned._error
    return JSON.stringify(cleaned, null, 2)
  } catch {
    return String(data)
  }
}

const tryGetLoaders = async () => {
  loading.loaders = true
  const start = Date.now()
  try {
    const data = await api.getLoaders(true) // 强制刷新
    responses.loaders = {
      success: data.code === 0,
      data,
      time: data._duration || (Date.now() - start),
      count: data.data?.length || 0
    }
  } catch (e) {
    responses.loaders = {
      success: false,
      data: { code: -1, message: e.message },
      time: Date.now() - start
    }
  } finally {
    loading.loaders = false
  }
}

const tryMatchLoader = async () => {
  if (!matchParams.msm_id) {
    alert('请输入 MSM ID')
    return
  }
  
  loading.match = true
  showMatchModal.value = false
  const start = Date.now()
  
  try {
    const data = await api.matchLoader({
      msm_id: matchParams.msm_id,
      pk_hash: matchParams.pk_hash || undefined,
      oem_id: matchParams.oem_id || undefined,
      storage_type: matchParams.storage_type
    })
    responses.match = {
      success: data.code === 0,
      data,
      time: data._duration || (Date.now() - start)
    }
  } catch (e) {
    responses.match = {
      success: false,
      data: { code: -1, message: e.message },
      time: Date.now() - start
    }
  } finally {
    loading.match = false
  }
}

const checkHealth = async () => {
  loading.health = true
  healthStatus.value = await api.healthCheck()
  loading.health = false
}

// 新增 API 调用方法
const tryGetChips = async () => {
  loading.chips = true
  const start = Date.now()
  try {
    const data = await api.getChips()
    responses.chips = { success: data.code === 0, data, time: Date.now() - start }
  } catch (e) {
    responses.chips = { success: false, data: { code: -1, message: e.message }, time: Date.now() - start }
  } finally {
    loading.chips = false
  }
}

const tryGetVendors = async () => {
  loading.vendors = true
  const start = Date.now()
  try {
    const data = await api.getVendors()
    responses.vendors = { success: data.code === 0, data, time: Date.now() - start }
  } catch (e) {
    responses.vendors = { success: false, data: { code: -1, message: e.message }, time: Date.now() - start }
  } finally {
    loading.vendors = false
  }
}

const tryGetStatsChips = async () => {
  loading.statsChips = true
  const start = Date.now()
  try {
    const data = await api.getStatsChips()
    responses.statsChips = { success: data.code === 0, data, time: Date.now() - start }
  } catch (e) {
    responses.statsChips = { success: false, data: { code: -1, message: e.message }, time: Date.now() - start }
  } finally {
    loading.statsChips = false
  }
}

const tryGetStatsHot = async () => {
  loading.statsHot = true
  const start = Date.now()
  try {
    const data = await api.getStatsHot()
    responses.statsHot = { success: data.code === 0, data, time: Date.now() - start }
  } catch (e) {
    responses.statsHot = { success: false, data: { code: -1, message: e.message }, time: Date.now() - start }
  } finally {
    loading.statsHot = false
  }
}

const tryGetStatsTrends = async () => {
  loading.statsTrends = true
  const start = Date.now()
  try {
    const data = await api.getStatsTrends()
    responses.statsTrends = { success: data.code === 0, data, time: Date.now() - start }
  } catch (e) {
    responses.statsTrends = { success: false, data: { code: -1, message: e.message }, time: Date.now() - start }
  } finally {
    loading.statsTrends = false
  }
}

const tryGetAnnouncements = async () => {
  loading.announcements = true
  const start = Date.now()
  try {
    const data = await api.getAnnouncements()
    responses.announcements = { success: data.code === 0, data, time: Date.now() - start }
  } catch (e) {
    responses.announcements = { success: false, data: { code: -1, message: e.message }, time: Date.now() - start }
  } finally {
    loading.announcements = false
  }
}

const tryGetChangelog = async () => {
  loading.changelog = true
  const start = Date.now()
  try {
    const data = await api.getChangelog()
    responses.changelog = { success: data.code === 0, data, time: Date.now() - start }
  } catch (e) {
    responses.changelog = { success: false, data: { code: -1, message: e.message }, time: Date.now() - start }
  } finally {
    loading.changelog = false
  }
}

const copyCode = (text) => {
  navigator.clipboard.writeText(text)
}

onMounted(() => {
  checkHealth()
})
</script>

<style lang="scss" scoped>
.api-page {
  padding: 48px 24px 80px;
}

.page-header {
  margin-bottom: 32px;
  
  h1 {
    font-size: 2.2rem;
    margin-bottom: 8px;
  }
  
  .gradient-text {
    background: linear-gradient(135deg, #ec4899 0%, #8b5cf6 100%);
    -webkit-background-clip: text;
    -webkit-text-fill-color: transparent;
    background-clip: text;
  }
  
  .page-desc {
    color: var(--vp-c-text-3);
    margin: 0;
  }
}

.api-info {
  display: flex;
  gap: 24px;
  margin-bottom: 48px;
  padding: 24px 28px;
  background: rgba(255, 255, 255, 0.7);
  backdrop-filter: blur(10px);
  border: 1px solid var(--vp-c-divider);
  border-radius: 16px;
  
  .dark & {
    background: rgba(27, 27, 31, 0.7);
  }
  
  @media (max-width: 768px) {
    flex-direction: column;
    gap: 16px;
  }
}

.info-item {
  display: flex;
  align-items: center;
  gap: 12px;
  
  .info-icon {
    font-size: 24px;
  }
  
  .info-content {
    strong {
      display: block;
      font-size: 12px;
      color: var(--vp-c-text-3);
      margin-bottom: 2px;
      text-transform: uppercase;
      letter-spacing: 0.5px;
    }
    
    code {
      font-size: 14px;
      background: var(--vp-c-bg-mute);
      padding: 4px 10px;
      border-radius: 6px;
    }
  }
  
  &.status-item {
    margin-left: auto;
    
    @media (max-width: 768px) {
      margin-left: 0;
    }
  }
}

.status-badge {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  padding: 4px 12px;
  border-radius: 20px;
  font-size: 13px;
  font-weight: 600;
  
  &.online {
    background: rgba(16, 185, 129, 0.15);
    color: #10b981;
  }
  
  &.offline {
    background: rgba(239, 68, 68, 0.15);
    color: #ef4444;
  }
  
  &.pending {
    background: rgba(245, 158, 11, 0.15);
    color: #f59e0b;
  }
  
  .latency {
    font-size: 11px;
    opacity: 0.8;
  }
}

.api-section {
  margin-bottom: 48px;
  
  h2 {
    margin-bottom: 24px;
    display: flex;
    align-items: center;
    gap: 10px;
    
    &::before {
      content: '';
      width: 4px;
      height: 24px;
      background: linear-gradient(180deg, #ec4899, #8b5cf6);
      border-radius: 2px;
    }
  }
}

.api-card {
  background: var(--vp-c-bg);
  border: 1px solid var(--vp-c-divider);
  border-radius: 16px;
  padding: 24px 28px;
  margin-bottom: 20px;
  transition: all 0.3s;
  
  &:hover {
    border-color: rgba(236, 72, 153, 0.3);
    box-shadow: var(--vp-shadow-2);
  }
  
  h4 {
    font-size: 13px;
    color: var(--vp-c-text-3);
    margin: 20px 0 12px;
    text-transform: uppercase;
    letter-spacing: 0.5px;
  }
}

.api-header {
  display: flex;
  align-items: center;
  gap: 14px;
  margin-bottom: 14px;
  flex-wrap: wrap;
}

.method {
  padding: 5px 12px;
  border-radius: 6px;
  font-size: 11px;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.5px;
  
  &.get { 
    background: linear-gradient(135deg, rgba(16, 185, 129, 0.15), rgba(16, 185, 129, 0.05));
    color: #10b981;
    border: 1px solid rgba(16, 185, 129, 0.2);
  }
  &.post { 
    background: linear-gradient(135deg, rgba(59, 130, 246, 0.15), rgba(59, 130, 246, 0.05));
    color: #3b82f6;
    border: 1px solid rgba(59, 130, 246, 0.2);
  }
  &.put {
    background: linear-gradient(135deg, rgba(245, 158, 11, 0.15), rgba(245, 158, 11, 0.05));
    color: #f59e0b;
    border: 1px solid rgba(245, 158, 11, 0.2);
  }
  &.delete {
    background: linear-gradient(135deg, rgba(239, 68, 68, 0.15), rgba(239, 68, 68, 0.05));
    color: #ef4444;
    border: 1px solid rgba(239, 68, 68, 0.2);
  }
}

.endpoint {
  font-size: 15px;
  font-weight: 600;
  font-family: var(--vp-font-family-mono);
  color: var(--vp-c-text-1);
}

.try-btn {
  margin-left: auto;
  padding: 8px 18px;
  background: linear-gradient(135deg, #ec4899, #8b5cf6);
  color: white;
  border: none;
  border-radius: 8px;
  font-size: 13px;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.3s;
  
  &:hover:not(:disabled) {
    transform: translateY(-1px);
    box-shadow: 0 4px 15px rgba(236, 72, 153, 0.4);
  }
  
  &:disabled {
    opacity: 0.6;
    cursor: not-allowed;
    transform: none;
  }
}

.api-desc {
  color: var(--vp-c-text-2);
  margin: 0;
  line-height: 1.6;
}

.response-box {
  margin-top: 20px;
  background: var(--vp-c-bg-soft);
  border-radius: 12px;
  overflow: hidden;
  border: 1px solid var(--vp-c-divider);
  animation: fadeIn 0.3s ease;
}

@keyframes fadeIn {
  from { opacity: 0; transform: translateY(-10px); }
  to { opacity: 1; transform: translateY(0); }
}

.response-header {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 12px 16px;
  background: var(--vp-c-bg-mute);
  font-size: 13px;
  border-bottom: 1px solid var(--vp-c-divider);
}

.status {
  display: flex;
  align-items: center;
  gap: 6px;
  font-weight: 600;
  
  &.success { color: #10b981; }
  &.error { color: #ef4444; }
}

.time {
  color: var(--vp-c-text-3);
  font-family: var(--vp-font-family-mono);
  font-size: 12px;
  padding: 2px 8px;
  background: var(--vp-c-bg);
  border-radius: 4px;
}

.response-box pre {
  margin: 0;
  padding: 16px 20px;
  max-height: 350px;
  overflow: auto;
  font-size: 13px;
  line-height: 1.6;
  
  code {
    background: none;
    padding: 0;
    color: var(--vp-c-text-2);
  }
}

// 弹窗
.modal-overlay {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: rgba(0, 0, 0, 0.6);
  backdrop-filter: blur(4px);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
  animation: fadeIn 0.2s ease;
}

.modal {
  background: var(--vp-c-bg);
  border-radius: 20px;
  padding: 28px;
  width: 90%;
  max-width: 420px;
  box-shadow: var(--vp-shadow-3);
  animation: scaleIn 0.3s ease;
  
  h3 {
    margin: 0 0 24px;
    font-size: 1.3rem;
    display: flex;
    align-items: center;
    gap: 10px;
    
    &::before {
      content: '🔍';
    }
  }
}

@keyframes scaleIn {
  from { opacity: 0; transform: scale(0.9); }
  to { opacity: 1; transform: scale(1); }
}

.form-group {
  margin-bottom: 18px;
  
  label {
    display: block;
    font-size: 14px;
    font-weight: 600;
    margin-bottom: 8px;
    color: var(--vp-c-text-1);
    
    .required {
      color: #ef4444;
      margin-left: 2px;
    }
  }
  
  input, select {
    width: 100%;
    padding: 12px 14px;
    border: 1px solid var(--vp-c-divider);
    border-radius: 10px;
    background: var(--vp-c-bg-soft);
    color: var(--vp-c-text-1);
    font-size: 14px;
    transition: all 0.2s;
    
    &:focus {
      outline: none;
      border-color: #ec4899;
      box-shadow: 0 0 0 3px rgba(236, 72, 153, 0.1);
    }
    
    &::placeholder {
      color: var(--vp-c-text-3);
    }
  }
}

.modal-actions {
  display: flex;
  justify-content: flex-end;
  gap: 12px;
  margin-top: 28px;
  
  .vp-button {
    padding: 10px 24px;
    border-radius: 10px;
    font-weight: 600;
    
    &.primary {
      background: linear-gradient(135deg, #ec4899, #8b5cf6);
      
      &:hover:not(:disabled) {
        box-shadow: 0 4px 15px rgba(236, 72, 153, 0.4);
      }
    }
  }
}
</style>
