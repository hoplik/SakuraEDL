<template>
  <div>
    <div class="page-header">
      <div>
        <h1 class="page-title">设备日志</h1>
        <p class="page-subtitle">查看设备匹配和下载记录</p>
      </div>
      <div style="display: flex; gap: 12px;">
        <el-input
          v-model="search"
          placeholder="搜索 MSM ID / PK Hash..."
          style="width: 240px"
          @keyup.enter="loadLogs"
          clearable
        >
          <template #prefix><el-icon><Search /></el-icon></template>
        </el-input>
        <el-select v-model="resultFilter" placeholder="匹配结果" style="width: 130px" @change="loadLogs" clearable>
          <el-option label="全部结果" value="" />
          <el-option label="连接成功" value="success" />
          <el-option label="连接失败" value="failed" />
          <el-option label="信息收集" value="info_collected" />
          <el-option label="未找到" value="not_found" />
        </el-select>
        <el-button @click="loadLogs" :loading="loading" round>
          <el-icon><Refresh /></el-icon> 刷新
        </el-button>
        <el-button @click="exportLogs" round>
          <el-icon><Download /></el-icon> 导出
        </el-button>
      </div>
    </div>

    <!-- 统计卡片 -->
    <div style="display: grid; grid-template-columns: repeat(5, 1fr); gap: 16px; margin-bottom: 24px;">
      <div style="background: white; padding: 20px; border-radius: 12px; box-shadow: 0 2px 8px rgba(0,0,0,0.05);">
        <div style="display: flex; align-items: center; gap: 12px;">
          <div style="width: 40px; height: 40px; border-radius: 10px; background: rgba(102,126,234,0.1); display: flex; align-items: center; justify-content: center; color: #667eea;">
            <el-icon size="20"><Document /></el-icon>
          </div>
          <div>
            <div style="font-size: 12px; color: #64748b;">总请求数</div>
            <div style="font-size: 24px; font-weight: 700; color: #1e293b;">{{ total }}</div>
          </div>
        </div>
      </div>
      <div style="background: white; padding: 20px; border-radius: 12px; box-shadow: 0 2px 8px rgba(0,0,0,0.05);">
        <div style="display: flex; align-items: center; gap: 12px;">
          <div style="width: 40px; height: 40px; border-radius: 10px; background: rgba(16,185,129,0.1); display: flex; align-items: center; justify-content: center; color: #10b981;">
            <el-icon size="20"><CircleCheck /></el-icon>
          </div>
          <div>
            <div style="font-size: 12px; color: #64748b;">连接成功</div>
            <div style="font-size: 24px; font-weight: 700; color: #10b981;">{{ stats.success || 0 }}</div>
          </div>
        </div>
      </div>
      <div style="background: white; padding: 20px; border-radius: 12px; box-shadow: 0 2px 8px rgba(0,0,0,0.05);">
        <div style="display: flex; align-items: center; gap: 12px;">
          <div style="width: 40px; height: 40px; border-radius: 10px; background: rgba(59,130,246,0.1); display: flex; align-items: center; justify-content: center; color: #3b82f6;">
            <el-icon size="20"><InfoFilled /></el-icon>
          </div>
          <div>
            <div style="font-size: 12px; color: #64748b;">信息收集</div>
            <div style="font-size: 24px; font-weight: 700; color: #3b82f6;">{{ stats.info_collected || 0 }}</div>
          </div>
        </div>
      </div>
      <div style="background: white; padding: 20px; border-radius: 12px; box-shadow: 0 2px 8px rgba(0,0,0,0.05);">
        <div style="display: flex; align-items: center; gap: 12px;">
          <div style="width: 40px; height: 40px; border-radius: 10px; background: rgba(239,68,68,0.1); display: flex; align-items: center; justify-content: center; color: #ef4444;">
            <el-icon size="20"><CircleClose /></el-icon>
          </div>
          <div>
            <div style="font-size: 12px; color: #64748b;">连接失败</div>
            <div style="font-size: 24px; font-weight: 700; color: #ef4444;">{{ stats.failed || 0 }}</div>
          </div>
        </div>
      </div>
      <div style="background: white; padding: 20px; border-radius: 12px; box-shadow: 0 2px 8px rgba(0,0,0,0.05);">
        <div style="display: flex; align-items: center; gap: 12px;">
          <div style="width: 40px; height: 40px; border-radius: 10px; background: rgba(245,158,11,0.1); display: flex; align-items: center; justify-content: center; color: #f59e0b;">
            <el-icon size="20"><Clock /></el-icon>
          </div>
          <div>
            <div style="font-size: 12px; color: #64748b;">今日请求</div>
            <div style="font-size: 24px; font-weight: 700; color: #f59e0b;">{{ stats.today || 0 }}</div>
          </div>
        </div>
      </div>
    </div>

    <div class="card">
      <div class="card-body" style="padding: 0;">
        <el-table :data="logs" v-loading="loading" stripe style="width: 100%;">
          <el-table-column prop="id" label="ID" width="60" align="center">
            <template #default="{ row }">
              <span style="font-weight: 600; color: #64748b;">#{{ row.id }}</span>
            </template>
          </el-table-column>
          
          <el-table-column prop="sahara_version" label="协议" width="70" align="center">
            <template #default="{ row }">
              <el-tag 
                :type="getSaharaVersionType(row.sahara_version)" 
                size="small" 
                effect="plain"
              >
                V{{ row.sahara_version || '?' }}
              </el-tag>
            </template>
          </el-table-column>
          
          <el-table-column prop="chip_name" label="芯片" width="100">
            <template #default="{ row }">
              <span style="font-weight: 500; color: #1e293b;">{{ row.chip_name || '-' }}</span>
            </template>
          </el-table-column>
          
          <el-table-column prop="vendor" label="厂商" width="90">
            <template #default="{ row }">
              <el-tag v-if="row.vendor" size="small" :type="getVendorType(row.vendor)" effect="plain">
                {{ row.vendor }}
              </el-tag>
              <span v-else style="color: #94a3b8;">-</span>
            </template>
          </el-table-column>
          
          <el-table-column prop="msm_id" label="MSM ID" width="100">
            <template #default="{ row }">
              <span style="font-family: monospace; font-size: 12px; color: #667eea; font-weight: 500;">{{ row.msm_id || '-' }}</span>
            </template>
          </el-table-column>
          
          <el-table-column prop="oem_id" label="OEM" width="70" align="center">
            <template #default="{ row }">
              <span style="font-family: monospace; font-size: 11px; color: #64748b;">{{ row.oem_id || '-' }}</span>
            </template>
          </el-table-column>
          
          <el-table-column prop="pk_hash" label="PK Hash" min-width="200">
            <template #default="{ row }">
              <el-tooltip :content="row.pk_hash" placement="top" :show-after="500">
                <span
                  style="font-family: monospace; font-size: 11px; color: #64748b; cursor: pointer;"
                  @click="copyToClipboard(row.pk_hash)"
                >
                  {{ row.pk_hash?.substring(0, 32) }}...
                </span>
              </el-tooltip>
            </template>
          </el-table-column>
          
          <el-table-column prop="storage_type" label="存储" width="70" align="center">
            <template #default="{ row }">
              <el-tag size="small" type="info" effect="plain">{{ row.storage_type?.toUpperCase() || '-' }}</el-tag>
            </template>
          </el-table-column>
          
          <el-table-column prop="match_result" label="结果" width="90" align="center">
            <template #default="{ row }">
              <el-tag :type="getResultType(row.match_result)" size="small" effect="dark">
                {{ getResultLabel(row.match_result) }}
              </el-tag>
            </template>
          </el-table-column>
          
          <el-table-column prop="client_ip" label="IP" width="120">
            <template #default="{ row }">
              <span style="font-family: monospace; font-size: 11px; color: #64748b;">{{ row.client_ip || '-' }}</span>
            </template>
          </el-table-column>
          
          <el-table-column prop="created_at" label="时间" width="160">
            <template #default="{ row }">
              <span style="color: #64748b; font-size: 12px;">{{ formatDateTime(row.created_at) }}</span>
            </template>
          </el-table-column>
          
          <el-table-column label="" width="50" align="center">
            <template #default="{ row }">
              <el-tooltip content="查看详情" placement="top">
                <el-button link @click="showDetail(row)">
                  <el-icon><View /></el-icon>
                </el-button>
              </el-tooltip>
            </template>
          </el-table-column>
        </el-table>
        
        <div v-if="logs.length === 0 && !loading" style="text-align: center; padding: 60px; color: #64748b;">
          <el-icon style="font-size: 56px; margin-bottom: 16px; color: #cbd5e1;"><DocumentDelete /></el-icon>
          <p style="font-size: 15px;">暂无日志记录</p>
          <p style="font-size: 13px; color: #94a3b8; margin-top: 8px;">设备匹配请求将显示在这里</p>
        </div>
      </div>
      
      <!-- 详情弹窗 -->
      <el-dialog v-model="detailVisible" title="设备详情" width="600px">
        <div v-if="detailRow" style="font-size: 14px;">
          <div style="display: grid; grid-template-columns: 120px 1fr; gap: 12px 16px; line-height: 1.8;">
            <div style="color: #64748b; font-weight: 500;">Sahara 协议</div>
            <div>
              <el-tag :type="getSaharaVersionType(detailRow.sahara_version)" size="small">
                V{{ detailRow.sahara_version || '?' }}
              </el-tag>
              <span style="margin-left: 8px; color: #94a3b8; font-size: 12px;">
                {{ detailRow.sahara_version === 3 ? '(V3 扩展信息)' : detailRow.sahara_version === 2 ? '(V2 标准)' : detailRow.sahara_version === 1 ? '(V1 旧版)' : '' }}
              </span>
            </div>
            
            <div style="color: #64748b; font-weight: 500;">芯片名称</div>
            <div style="font-weight: 600; color: #1e293b;">{{ detailRow.chip_name || '-' }}</div>
            
            <div style="color: #64748b; font-weight: 500;">厂商</div>
            <div>
              <el-tag v-if="detailRow.vendor" :type="getVendorType(detailRow.vendor)" size="small">
                {{ detailRow.vendor }}
              </el-tag>
              <span v-else>-</span>
            </div>
            
            <div style="color: #64748b; font-weight: 500;">MSM ID</div>
            <div style="font-family: monospace; color: #667eea;">{{ detailRow.msm_id || '-' }}</div>
            
            <div style="color: #64748b; font-weight: 500;">OEM ID</div>
            <div style="font-family: monospace;">{{ detailRow.oem_id || '-' }}</div>
            
            <div style="color: #64748b; font-weight: 500;">Model ID</div>
            <div style="font-family: monospace;">{{ detailRow.model_id || '-' }}</div>
            
            <div style="color: #64748b; font-weight: 500;">HW ID</div>
            <div style="font-family: monospace; font-size: 12px; word-break: break-all;">{{ detailRow.hw_id || '-' }}</div>
            
            <div style="color: #64748b; font-weight: 500;">序列号</div>
            <div style="font-family: monospace;">{{ detailRow.serial_number || '-' }}</div>
            
            <div style="color: #64748b; font-weight: 500;">PK Hash</div>
            <div 
              style="font-family: monospace; font-size: 11px; word-break: break-all; cursor: pointer; color: #64748b;"
              @click="copyToClipboard(detailRow.pk_hash)"
            >
              {{ detailRow.pk_hash || '-' }}
            </div>
            
            <div style="color: #64748b; font-weight: 500;">存储类型</div>
            <div>
              <el-tag size="small" type="info">{{ detailRow.storage_type?.toUpperCase() || '-' }}</el-tag>
            </div>
            
            <div style="color: #64748b; font-weight: 500;">结果</div>
            <div>
              <el-tag :type="getResultType(detailRow.match_result)" size="small" effect="dark">
                {{ getResultLabel(detailRow.match_result) }}
              </el-tag>
            </div>
            
            <div style="color: #64748b; font-weight: 500;">客户端 IP</div>
            <div style="font-family: monospace; font-size: 12px;">{{ detailRow.client_ip || '-' }}</div>
            
            <div style="color: #64748b; font-weight: 500;">时间</div>
            <div>{{ formatDateTime(detailRow.created_at) }}</div>
          </div>
        </div>
        <template #footer>
          <el-button @click="detailVisible = false">关闭</el-button>
        </template>
      </el-dialog>
      
      <div class="card-body" style="border-top: 1px solid #f1f5f9; display: flex; justify-content: space-between; align-items: center;">
        <span style="font-size: 13px; color: #64748b;">共 {{ total }} 条记录</span>
        <el-pagination
          v-model:current-page="page"
          v-model:page-size="pageSize"
          :total="total"
          :page-sizes="[50, 100, 200]"
          layout="sizes, prev, pager, next, jumper"
          @size-change="loadLogs"
          @current-change="loadLogs"
        />
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, reactive, onMounted } from 'vue'
import { ElMessage } from 'element-plus'
import api from '@/api'
import {
  Search, Refresh, Download, Document, View,
  CircleCheck, CircleClose, Warning, Clock, DocumentDelete, InfoFilled
} from '@element-plus/icons-vue'

const loading = ref(false)
const logs = ref([])
const total = ref(0)
const page = ref(1)
const pageSize = ref(50)
const search = ref('')
const resultFilter = ref('')
const stats = reactive({ matched: 0, not_found: 0, failed: 0, today: 0 })

const loadLogs = async () => {
  loading.value = true
  try {
    const res = await api.getLogs({
      page: page.value,
      page_size: pageSize.value,
      keyword: search.value,
      result: resultFilter.value
    })
    if (res.code === 0) {
      logs.value = res.data.list || []
      total.value = res.data.total || 0
      if (res.data.stats) {
        Object.assign(stats, res.data.stats)
      }
    }
  } catch (e) {
    console.error('加载日志失败', e)
  }
  loading.value = false
}

const getResultType = (result) => {
  if (result === 'success') return 'success'
  if (result === 'info_collected') return 'primary'
  if (result === 'not_found') return 'warning'
  if (result === 'failed') return 'danger'
  return 'info'
}

const getResultLabel = (result) => {
  const labels = {
    success: '连接成功',
    failed: '连接失败',
    info_collected: '信息收集',
    not_found: '未找到',
    matched: '匹配成功'
  }
  return labels[result] || result
}

const getSaharaVersionType = (version) => {
  if (version === 3) return 'success'  // V3 最新
  if (version === 2) return 'primary'  // V2 常见
  if (version === 1) return 'warning'  // V1 旧版
  return 'info'
}

const getVendorType = (vendor) => {
  const types = {
    'Xiaomi': 'warning',
    'OnePlus': 'danger',
    'OPPO': 'success',
    'Realme': '',
    'Samsung': 'primary'
  }
  return types[vendor] || 'info'
}

// 详情弹窗
const detailVisible = ref(false)
const detailRow = ref(null)

const showDetail = (row) => {
  detailRow.value = row
  detailVisible.value = true
}

const formatDateTime = (dateStr) => {
  if (!dateStr) return '-'
  try {
    const date = new Date(dateStr)
    return date.toLocaleString('zh-CN', {
      year: 'numeric', month: '2-digit', day: '2-digit',
      hour: '2-digit', minute: '2-digit', second: '2-digit'
    })
  } catch (e) {
    return dateStr
  }
}

const copyToClipboard = async (text) => {
  try {
    await navigator.clipboard.writeText(text)
    ElMessage.success('已复制到剪贴板')
  } catch (e) {
    ElMessage.error('复制失败')
  }
}

const exportLogs = () => {
  if (logs.value.length === 0) {
    ElMessage.warning('暂无数据可导出')
    return
  }
  const headers = ['ID', '平台', 'MSM ID', 'PK Hash', '存储类型', '结果', 'IP', '时间']
  const rows = logs.value.map(log => [
    log.id, log.platform || '', log.msm_id || '', log.pk_hash || '',
    log.storage_type || '', log.match_result || '', log.client_ip || '', log.created_at || ''
  ])
  const csvContent = [headers, ...rows].map(r => r.map(c => `"${c}"`).join(',')).join('\n')
  const blob = new Blob(['\uFEFF' + csvContent], { type: 'text/csv;charset=utf-8;' })
  const link = document.createElement('a')
  link.href = URL.createObjectURL(blob)
  link.download = `device_logs_${new Date().toISOString().slice(0, 10)}.csv`
  link.click()
  ElMessage.success('导出成功')
}

onMounted(() => {
  loadLogs()
})
</script>
