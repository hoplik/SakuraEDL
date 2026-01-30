import axios from 'axios'
import { ElMessage } from 'element-plus'

const API_BASE = '/api'

// 创建 axios 实例
const http = axios.create({
  baseURL: API_BASE,
  timeout: 30000
})

// 请求拦截器
http.interceptors.request.use(
  config => {
    const token = localStorage.getItem('admin_token')
    if (token) {
      config.headers['X-Admin-Token'] = token
    }
    // 禁用缓存
    config.headers['Cache-Control'] = 'no-cache'
    config.headers['Pragma'] = 'no-cache'
    // 添加时间戳防止缓存
    config.params = {
      ...config.params,
      _t: Date.now()
    }
    return config
  },
  error => Promise.reject(error)
)

// 响应拦截器
http.interceptors.response.use(
  response => response.data,
  error => {
    if (error.response?.status === 401) {
      localStorage.removeItem('admin_token')
      window.location.href = '/login'
    }
    ElMessage.error(error.message || '请求失败')
    return Promise.reject(error)
  }
)

export default {
  // 登录
  login: (username, password) => http.post('/admin/login', { username, password }),

  // 统计
  getStats: () => http.get('/admin/stats'),

  // Loader 列表
  getLoaders: (params) => http.get('/admin/loaders', { params }),

  // Loader 详情
  getLoader: (id) => http.get(`/admin/loaders/${id}`),

  // 上传 Loader
  uploadLoader: (formData) => http.post('/admin/loaders/upload', formData, {
    headers: { 'Content-Type': 'multipart/form-data' },
    timeout: 120000
  }),

  // 更新 Loader
  updateLoader: (id, data) => http.put(`/admin/loaders/${id}`, data),

  // 删除 Loader
  deleteLoader: (id) => http.delete(`/admin/loaders/${id}`),

  // 启用 Loader
  enableLoader: (id) => http.post(`/admin/loaders/${id}/enable`),

  // 禁用 Loader
  disableLoader: (id) => http.post(`/admin/loaders/${id}/disable`),

  // 设备日志
  getLogs: (params) => http.get('/admin/logs', { params }),

  // 公开 API
  getPublicLoaders: (params) => http.get('/loaders/list', { params }),

  // ==================== MTK 资源管理 ====================
  
  // MTK 资源列表
  getMtkResources: (params) => http.get('/admin/mtk/resources', { params }),
  
  // 上传 MTK 资源
  uploadMtkResource: (formData) => http.post('/admin/mtk/resources/upload', formData, {
    headers: { 'Content-Type': 'multipart/form-data' },
    timeout: 120000
  }),
  
  // 更新 MTK 资源
  updateMtkResource: (id, data) => http.put(`/admin/mtk/resources/${id}`, data),
  
  // 删除 MTK 资源
  deleteMtkResource: (id) => http.delete(`/admin/mtk/resources/${id}`),
  
  // MTK 设备日志
  getMtkLogs: (params) => http.get('/admin/mtk/logs', { params }),
  
  // MTK 统计
  getMtkStats: () => http.get('/admin/mtk/stats'),

  // ==================== SPD 资源管理 ====================
  
  // SPD 资源列表
  getSpdResources: (params) => http.get('/admin/spd/resources', { params }),
  
  // 上传 SPD 资源
  uploadSpdResource: (formData) => http.post('/admin/spd/resources/upload', formData, {
    headers: { 'Content-Type': 'multipart/form-data' },
    timeout: 120000
  }),
  
  // 更新 SPD 资源
  updateSpdResource: (id, data) => http.put(`/admin/spd/resources/${id}`, data),
  
  // 删除 SPD 资源
  deleteSpdResource: (id) => http.delete(`/admin/spd/resources/${id}`),
  
  // SPD 设备日志
  getSpdLogs: (params) => http.get('/admin/spd/logs', { params }),
  
  // SPD 统计
  getSpdAdminStats: () => http.get('/admin/spd/stats')
}
