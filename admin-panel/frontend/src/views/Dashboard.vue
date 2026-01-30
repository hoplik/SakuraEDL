<template>
  <div>
    <div class="page-header">
      <div>
        <h1 class="page-title">仪表盘</h1>
        <p class="page-subtitle">Loader 云端管理系统概览</p>
      </div>
      <el-button @click="loadStats" :loading="loading" round>
        <el-icon><Refresh /></el-icon> 刷新数据
      </el-button>
    </div>

    <!-- 统计卡片 -->
    <div class="stats-grid">
      <div class="stat-card primary">
        <div class="stat-icon"><el-icon><Folder /></el-icon></div>
        <h3>Loader 总数</h3>
        <div class="value">{{ stats.total_loaders || 0 }}</div>
      </div>
      
      <div class="stat-card success">
        <div class="stat-icon"><el-icon><CircleCheck /></el-icon></div>
        <h3>已启用</h3>
        <div class="value">{{ stats.enabled_loaders || 0 }}</div>
        <div class="trend up">
          {{ enableRate }}% 启用率
        </div>
      </div>
      
      <div class="stat-card warning">
        <div class="stat-icon"><el-icon><Download /></el-icon></div>
        <h3>总下载次数</h3>
        <div class="value">{{ formatNumber(stats.total_downloads || 0) }}</div>
      </div>
      
      <div class="stat-card danger">
        <div class="stat-icon"><el-icon><Connection /></el-icon></div>
        <h3>总匹配次数</h3>
        <div class="value">{{ formatNumber(stats.total_matches || 0) }}</div>
      </div>
    </div>

    <!-- 快捷操作 -->
    <div class="quick-actions">
      <div class="quick-action-btn" @click="$router.push('/upload')">
        <div class="icon" style="background: rgba(102,126,234,0.1); color: #667eea;">
          <el-icon><Upload /></el-icon>
        </div>
        <div class="text">
          <h4>上传 Loader</h4>
          <p>添加新的 Loader 文件</p>
        </div>
      </div>
      
      <div class="quick-action-btn" @click="$router.push('/loaders')">
        <div class="icon" style="background: rgba(16,185,129,0.1); color: #10b981;">
          <el-icon><Setting /></el-icon>
        </div>
        <div class="text">
          <h4>管理 Loader</h4>
          <p>编辑或删除现有 Loader</p>
        </div>
      </div>
      
      <div class="quick-action-btn" @click="$router.push('/logs')">
        <div class="icon" style="background: rgba(245,158,11,0.1); color: #f59e0b;">
          <el-icon><Document /></el-icon>
        </div>
        <div class="text">
          <h4>查看日志</h4>
          <p>设备匹配记录</p>
        </div>
      </div>
    </div>

    <!-- 统计图表 -->
    <div style="display: grid; grid-template-columns: 1fr 1fr; gap: 24px;">
      <div class="card">
        <div class="card-header">
          <span class="card-title"><el-icon><PieChart /></el-icon> 验证类型分布</span>
        </div>
        <div class="card-body">
          <div style="display: flex; gap: 24px; flex-wrap: wrap;">
            <div
              v-for="(count, type) in stats.auth_type_stats"
              :key="type"
              style="text-align: center; flex: 1; min-width: 80px;"
            >
              <span class="auth-tag" :class="type">{{ getAuthTypeLabel(type) }}</span>
              <div style="font-size: 28px; font-weight: 700; margin-top: 12px; color: #1e293b;">{{ count }}</div>
              <div style="font-size: 12px; color: #64748b;">个 Loader</div>
            </div>
            <div v-if="!stats.auth_type_stats || Object.keys(stats.auth_type_stats).length === 0" style="color: #64748b;">
              暂无数据
            </div>
          </div>
        </div>
      </div>

      <div class="card">
        <div class="card-header">
          <span class="card-title"><el-icon><TrendCharts /></el-icon> 厂商分布</span>
        </div>
        <div class="card-body">
          <div style="display: flex; gap: 12px; flex-wrap: wrap;">
            <div
              v-for="(count, vendor) in stats.vendor_stats"
              :key="vendor"
              class="vendor-badge"
              :class="vendor.toLowerCase()"
            >
              {{ vendor }}: {{ count }}
            </div>
            <div v-if="!stats.vendor_stats || Object.keys(stats.vendor_stats).length === 0" style="color: #64748b; font-size: 13px;">
              暂无数据
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- 最近匹配 -->
    <div class="card" style="margin-top: 24px;">
      <div class="card-header">
        <span class="card-title"><el-icon><Clock /></el-icon> 最近匹配的设备</span>
        <el-button text @click="$router.push('/logs')">查看全部</el-button>
      </div>
      <div class="card-body" style="padding: 0;">
        <el-table :data="stats.recent_devices || []" stripe>
          <el-table-column prop="msm_id" label="MSM ID" width="120">
            <template #default="{ row }">
              <span style="font-family: monospace; font-size: 13px; color: #667eea;">{{ row.msm_id }}</span>
            </template>
          </el-table-column>
          <el-table-column prop="pk_hash" label="PK Hash">
            <template #default="{ row }">
              <el-tooltip :content="row.pk_hash" placement="top">
                <span style="font-family: monospace; font-size: 11px; color: #64748b;">
                  {{ row.pk_hash?.substring(0, 40) }}...
                </span>
              </el-tooltip>
            </template>
          </el-table-column>
          <el-table-column prop="storage_type" label="存储" width="80">
            <template #default="{ row }">
              <el-tag size="small" type="info">{{ row.storage_type?.toUpperCase() }}</el-tag>
            </template>
          </el-table-column>
          <el-table-column prop="match_result" label="结果" width="100">
            <template #default="{ row }">
              <el-tag :type="row.match_result === 'matched' ? 'success' : 'danger'" size="small" effect="dark">
                {{ row.match_result === 'matched' ? '成功' : '失败' }}
              </el-tag>
            </template>
          </el-table-column>
          <el-table-column prop="created_at" label="时间" width="180">
            <template #default="{ row }">
              <span style="color: #64748b; font-size: 12px;">{{ row.created_at }}</span>
            </template>
          </el-table-column>
        </el-table>
        
        <div v-if="!stats.recent_devices?.length" style="text-align: center; padding: 40px; color: #64748b;">
          <el-icon style="font-size: 48px; margin-bottom: 12px;"><DocumentDelete /></el-icon>
          <p>暂无匹配记录</p>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue'
import api from '@/api'
import {
  Refresh, Folder, CircleCheck, Download, Connection,
  Upload, Setting, Document, PieChart, TrendCharts,
  Clock, DocumentDelete
} from '@element-plus/icons-vue'

const loading = ref(false)
const stats = ref({})

const enableRate = computed(() => {
  const total = stats.value.total_loaders || 1
  const enabled = stats.value.enabled_loaders || 0
  return Math.round(enabled / total * 100)
})

const loadStats = async () => {
  loading.value = true
  try {
    const res = await api.getStats()
    if (res.code === 0) {
      stats.value = res.data
    }
  } catch (e) {
    console.error('加载统计失败', e)
  }
  loading.value = false
}

const formatNumber = (num) => {
  if (num >= 1000000) return (num / 1000000).toFixed(1) + 'M'
  if (num >= 1000) return (num / 1000).toFixed(1) + 'K'
  return num.toString()
}

const getAuthTypeLabel = (type) => {
  const labels = { none: '无验证', miauth: '小米', demacia: '一加', vip: 'VIP' }
  return labels[type] || type
}

onMounted(() => {
  loadStats()
})
</script>
