import axios from 'axios'

// ==================== 配置 ====================

const isDev = import.meta.env.DEV
const baseURL = isDev ? '/api' : 'https://api.sakuraedl.org/api'

// 简单的内存缓存
const cache = new Map()
const CACHE_TTL = 30 * 1000 // 30秒缓存

// ==================== Axios 实例 ====================

const apiClient = axios.create({
  baseURL,
  timeout: 15000,
  headers: {
    'Content-Type': 'application/json'
  }
})

// ==================== 请求拦截器 ====================

apiClient.interceptors.request.use(
  config => {
    config.params = { ...config.params, _t: Date.now() }
    config.metadata = { startTime: Date.now() }
    return config
  },
  error => Promise.reject(error)
)

// ==================== 响应拦截器 ====================

apiClient.interceptors.response.use(
  response => {
    const duration = Date.now() - response.config.metadata.startTime
    
    if (typeof response.data === 'object' && response.data !== null) {
      response.data._duration = duration
    } else {
      response.data = {
        code: -1,
        message: '响应格式错误',
        data: null,
        _error: true,
        _duration: duration
      }
    }
    
    return response
  },
  error => {
    const message = getErrorMessage(error)
    console.error(`[API] 请求失败: ${message}`)
    
    return Promise.resolve({
      data: {
        code: error.response?.status || -1,
        message,
        data: null,
        _error: true,
        _duration: Date.now() - (error.config?.metadata?.startTime || Date.now())
      }
    })
  }
)

// ==================== 错误消息处理 ====================

function getErrorMessage(error) {
  if (error.response) {
    const status = error.response.status
    const messages = {
      400: '请求参数错误',
      401: '未授权',
      403: '拒绝访问',
      404: '资源不存在',
      500: '服务器错误',
      502: '网关错误',
      503: '服务不可用'
    }
    return messages[status] || `服务器错误 (${status})`
  }
  if (error.code === 'ECONNABORTED') return '请求超时'
  if (!navigator.onLine) return '网络已断开'
  return error.message || '未知错误'
}

// ==================== 缓存工具 ====================

function getCached(key) {
  const item = cache.get(key)
  if (item && Date.now() - item.time < CACHE_TTL) {
    return item.data
  }
  cache.delete(key)
  return null
}

function setCache(key, data) {
  cache.set(key, { data, time: Date.now() })
}

// ==================== API 方法 ====================

export default {
  // ==================== 远程 API (api.sakuraedl.org) ====================
  
  // 获取公开统计数据 (合并远程统计 + 从 Loaders 派生的认证类型)
  async getStats(forceRefresh = false) {
    const cacheKey = 'stats'
    if (!forceRefresh) {
      const cached = getCached(cacheKey)
      if (cached) return cached
    }
    
    // 并行获取远程统计和 Loader 列表
    const [statsRes, loadersRes] = await Promise.all([
      apiClient.get('/public/stats'),
      apiClient.get('/loaders/list')
    ])
    
    const data = statsRes.data
    
    if (data.code === 0) {
      // 从 Loaders 派生 auth_type_stats
      const loaderList = loadersRes.data?.data?.loaders || []
      const authStats = {}
      loaderList.forEach(l => {
        const authType = l.auth_type || 'none'
        authStats[authType] = (authStats[authType] || 0) + 1
      })
      
      // 合并到返回数据
      if (data.data) {
        data.data.auth_type_stats = authStats
        data.data.total_loaders = data.data.enabled_loaders || loaderList.length
      }
      
      setCache(cacheKey, data)
    }
    return data
  },
  
  // 获取 Loader 列表
  async getLoaders(forceRefresh = false) {
    const cacheKey = 'loaders'
    if (!forceRefresh) {
      const cached = getCached(cacheKey)
      if (cached) return cached
    }
    
    const { data } = await apiClient.get('/loaders/list')
    if (data.code === 0) {
      setCache(cacheKey, data)
    }
    return data
  },
  
  // 匹配 Loader
  async matchLoader(params) {
    const { data } = await apiClient.post('/loaders/match', params)
    return data
  },
  
  // 获取 Loader 下载 URL
  getLoaderDownloadUrl(id) {
    return `${baseURL}/loaders/${id}/download`
  },
  
  // 上报设备日志
  async reportDeviceLog(logData) {
    const { data } = await apiClient.post('/device-logs', logData)
    return data
  },
  
  // ==================== 扩展 API (服务端实现) ====================
  
  // 获取芯片列表
  async getChips(params = {}) {
    const cacheKey = `chips-${JSON.stringify(params)}`
    const cached = getCached(cacheKey)
    if (cached) return cached
    
    const { data } = await apiClient.get('/chips', { params })
    if (data.code === 0) {
      setCache(cacheKey, data)
    }
    return data
  },
  
  // 获取厂商列表
  async getVendors() {
    const cacheKey = 'vendors'
    const cached = getCached(cacheKey)
    if (cached) return cached
    
    const { data } = await apiClient.get('/vendors')
    if (data.code === 0) {
      setCache(cacheKey, data)
    }
    return data
  },
  
  // ==================== 统计数据 ====================
  
  // 芯片统计
  async getStatsChips() {
    const { data } = await apiClient.get('/stats/chips')
    return data
  },
  
  // 厂商统计
  async getStatsVendors() {
    const { data } = await apiClient.get('/stats/vendors')
    return data
  },
  
  // 热门设备
  async getStatsHot() {
    const { data } = await apiClient.get('/stats/hot')
    return data
  },
  
  // 趋势分析
  async getStatsTrends(days = 7) {
    const { data } = await apiClient.get('/stats/trends', { params: { days } })
    return data
  },
  
  // 总览统计
  async getStatsOverview() {
    const { data } = await apiClient.get('/stats/overview')
    return data
  },
  
  // ==================== 内容 API ====================
  
  // 获取公告
  async getAnnouncements() {
    const { data } = await apiClient.get('/announcements')
    return data
  },
  
  // 获取更新日志
  async getChangelog() {
    const { data } = await apiClient.get('/changelog')
    return data
  },
  
  // 提交反馈
  async submitFeedback(feedback) {
    const { data } = await apiClient.post('/feedback', feedback)
    return data
  },
  
  // ==================== 高通芯片数据库 API ====================
  
  // 高通芯片列表
  async getQualcommChips(params = {}) {
    const cacheKey = `qualcomm-chips-${JSON.stringify(params)}`
    const cached = getCached(cacheKey)
    if (cached) return cached
    
    const { data } = await apiClient.get('/qualcomm/chips', { params })
    if (data.code === 0) {
      setCache(cacheKey, data)
    }
    return data
  },
  
  // 高通统计
  async getQualcommStats() {
    const cacheKey = 'qualcomm-stats'
    const cached = getCached(cacheKey)
    if (cached) return cached
    
    const { data } = await apiClient.get('/qualcomm/stats')
    if (data.code === 0) {
      setCache(cacheKey, data)
    }
    return data
  },
  
  // 高通品牌列表
  async getQualcommVendors() {
    const cacheKey = 'qualcomm-vendors'
    const cached = getCached(cacheKey)
    if (cached) return cached
    
    const { data } = await apiClient.get('/qualcomm/vendors')
    if (data.code === 0) {
      setCache(cacheKey, data)
    }
    return data
  },
  
  // ==================== MTK 芯片数据库 API ====================
  
  // MTK 芯片列表
  async getMtkChips(params = {}) {
    const cacheKey = `mtk-chips-${JSON.stringify(params)}`
    const cached = getCached(cacheKey)
    if (cached) return cached
    
    const { data } = await apiClient.get('/mtk/chips', { params })
    if (data.code === 0) {
      setCache(cacheKey, data)
    }
    return data
  },
  
  // MTK 统计
  async getMtkStats() {
    const cacheKey = 'mtk-stats'
    const cached = getCached(cacheKey)
    if (cached) return cached
    
    const { data } = await apiClient.get('/mtk/stats')
    if (data.code === 0) {
      setCache(cacheKey, data)
    }
    return data
  },
  
  // ==================== SPD 芯片数据库 API ====================
  
  // SPD 芯片列表
  async getSpdChips(params = {}) {
    const cacheKey = `spd-chips-${JSON.stringify(params)}`
    const cached = getCached(cacheKey)
    if (cached) return cached
    
    const { data } = await apiClient.get('/spd/chips', { params })
    if (data.code === 0) {
      setCache(cacheKey, data)
    }
    return data
  },
  
  // SPD 设备列表
  async getSpdDevices(params = {}) {
    const cacheKey = `spd-devices-${JSON.stringify(params)}`
    const cached = getCached(cacheKey)
    if (cached) return cached
    
    const { data } = await apiClient.get('/spd/devices', { params })
    if (data.code === 0) {
      setCache(cacheKey, data)
    }
    return data
  },
  
  // SPD 统计
  async getSpdStats() {
    const cacheKey = 'spd-stats'
    const cached = getCached(cacheKey)
    if (cached) return cached
    
    const { data } = await apiClient.get('/spd/stats')
    if (data.code === 0) {
      setCache(cacheKey, data)
    }
    return data
  },
  
  // ==================== 健康检查 ====================
  
  async healthCheck() {
    const start = Date.now()
    try {
      const { data } = await apiClient.get('/health', { timeout: 5000 })
      return {
        status: data.code === 0 ? 'ok' : 'error',
        latency: Date.now() - start
      }
    } catch (e) {
      return { status: 'error', latency: Date.now() - start, message: e.message }
    }
  },
  
  // 清除缓存
  clearCache() {
    cache.clear()
  },
  
  // 获取 API 基础 URL
  getBaseURL() {
    return baseURL
  }
}
