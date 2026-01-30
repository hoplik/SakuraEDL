<template>
  <div>
    <div class="page-header">
      <div>
        <h1 class="page-title">Loader 管理</h1>
        <p class="page-subtitle">管理所有已上传的 Loader 文件</p>
      </div>
      <div style="display: flex; gap: 12px;">
        <el-input
          v-model="search"
          placeholder="搜索文件名、芯片..."
          style="width: 220px"
          @keyup.enter="loadLoaders"
          clearable
        >
          <template #prefix><el-icon><Search /></el-icon></template>
        </el-input>
        <el-select v-model="authFilter" placeholder="验证类型" style="width: 130px" @change="loadLoaders" clearable>
          <el-option label="全部类型" value="" />
          <el-option label="无验证" value="none" />
          <el-option label="小米验证" value="miauth" />
          <el-option label="一加验证" value="demacia" />
          <el-option label="VIP 验证" value="vip" />
        </el-select>
        <el-button type="primary" @click="$router.push('/upload')" round>
          <el-icon><Plus /></el-icon> 上传新 Loader
        </el-button>
      </div>
    </div>

    <div class="card">
      <div class="card-body" style="padding: 0;">
        <el-table :data="loaders" v-loading="loading" stripe style="width: 100%;">
          <el-table-column prop="id" label="ID" width="70" align="center">
            <template #default="{ row }">
              <span style="font-weight: 600; color: #64748b;">#{{ row.id }}</span>
            </template>
          </el-table-column>
          
          <el-table-column prop="filename" label="文件信息" min-width="280">
            <template #default="{ row }">
              <div style="display: flex; flex-direction: column; gap: 4px;">
                <span style="font-weight: 600; color: #1e293b;">{{ row.filename }}</span>
                <div style="display: flex; gap: 8px; align-items: center;">
                  <span class="vendor-badge" :class="getVendorClass(row.vendor)">{{ row.vendor || '未知' }}</span>
                  <span style="color: #64748b; font-size: 12px;">{{ row.chip }}</span>
                  <span style="color: #94a3b8; font-size: 11px;">{{ formatSize(row.file_size) }}</span>
                </div>
              </div>
            </template>
          </el-table-column>
          
          <el-table-column prop="hw_id" label="HW ID" width="110">
            <template #default="{ row }">
              <span style="font-family: monospace; font-size: 12px; color: #64748b;">{{ row.hw_id || '-' }}</span>
            </template>
          </el-table-column>
          
          <el-table-column prop="auth_type" label="验证" width="100" align="center">
            <template #default="{ row }">
              <span class="auth-tag" :class="row.auth_type">{{ getAuthTypeLabel(row.auth_type) }}</span>
            </template>
          </el-table-column>
          
          <el-table-column prop="storage_type" label="存储" width="80" align="center">
            <template #default="{ row }">
              <el-tag size="small" type="info" effect="plain">{{ row.storage_type?.toUpperCase() }}</el-tag>
            </template>
          </el-table-column>
          
          <el-table-column label="VIP" width="80" align="center">
            <template #default="{ row }">
              <div v-if="row.auth_type === 'vip'" style="display: flex; gap: 4px; justify-content: center;">
                <el-tooltip content="Digest 文件" placement="top">
                  <el-tag size="small" :type="row.has_digest ? 'success' : 'danger'" effect="dark">D</el-tag>
                </el-tooltip>
                <el-tooltip content="Sign 文件" placement="top">
                  <el-tag size="small" :type="row.has_sign ? 'success' : 'danger'" effect="dark">S</el-tag>
                </el-tooltip>
              </div>
              <span v-else style="color: #cbd5e1;">-</span>
            </template>
          </el-table-column>
          
          <el-table-column prop="is_enabled" label="状态" width="90" align="center">
            <template #default="{ row }">
              <span class="status-badge" :class="row.is_enabled ? 'enabled' : 'disabled'">
                <span class="dot"></span>
                {{ row.is_enabled ? '启用' : '禁用' }}
              </span>
            </template>
          </el-table-column>
          
          <el-table-column label="统计" width="100" align="center">
            <template #default="{ row }">
              <div style="font-size: 12px; color: #64748b;">
                <el-tooltip content="下载次数" placement="top">
                  <span><el-icon><Download /></el-icon> {{ row.downloads || 0 }}</span>
                </el-tooltip>
                <span style="margin: 0 6px; color: #e2e8f0;">|</span>
                <el-tooltip content="匹配次数" placement="top">
                  <span><el-icon><Connection /></el-icon> {{ row.match_count || 0 }}</span>
                </el-tooltip>
              </div>
            </template>
          </el-table-column>
          
          <el-table-column label="操作" width="200" fixed="right" align="center">
            <template #default="{ row }">
              <el-button size="small" @click="editLoader(row)" text type="primary">
                <el-icon><Edit /></el-icon> 编辑
              </el-button>
              <el-button
                v-if="row.is_enabled"
                size="small"
                @click="toggleLoader(row, false)"
                text
                type="warning"
              >
                <el-icon><VideoPause /></el-icon> 禁用
              </el-button>
              <el-button
                v-else
                size="small"
                @click="toggleLoader(row, true)"
                text
                type="success"
              >
                <el-icon><VideoPlay /></el-icon> 启用
              </el-button>
              <el-button size="small" @click="deleteLoader(row)" text type="danger">
                <el-icon><Delete /></el-icon>
              </el-button>
            </template>
          </el-table-column>
        </el-table>
      </div>
      
      <div class="card-body" style="border-top: 1px solid #f1f5f9; display: flex; justify-content: space-between; align-items: center;">
        <span style="font-size: 13px; color: #64748b;">共 {{ total }} 条记录</span>
        <el-pagination
          v-model:current-page="page"
          v-model:page-size="pageSize"
          :total="total"
          :page-sizes="[20, 50, 100]"
          layout="sizes, prev, pager, next, jumper"
          @size-change="loadLoaders"
          @current-change="loadLoaders"
        />
      </div>
    </div>

    <!-- 编辑对话框 -->
    <el-dialog v-model="editDialogVisible" title="编辑 Loader" width="600px">
      <el-form :model="editForm" label-width="100px">
        <el-form-item label="文件名">
          <el-input v-model="editForm.filename" disabled />
        </el-form-item>
        <el-row :gutter="20">
          <el-col :span="12">
            <el-form-item label="厂商">
              <el-input v-model="editForm.vendor" />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="芯片">
              <el-input v-model="editForm.chip" />
            </el-form-item>
          </el-col>
        </el-row>
        <el-row :gutter="20">
          <el-col :span="12">
            <el-form-item label="HW ID">
              <el-input v-model="editForm.hw_id" />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="OEM ID">
              <el-input v-model="editForm.oem_id" />
            </el-form-item>
          </el-col>
        </el-row>
        <el-form-item label="PK Hash">
          <el-input v-model="editForm.pk_hash" />
        </el-form-item>
        <el-row :gutter="20">
          <el-col :span="12">
            <el-form-item label="验证类型">
              <el-select v-model="editForm.auth_type" style="width: 100%;">
                <el-option label="无验证" value="none" />
                <el-option label="小米验证" value="miauth" />
                <el-option label="一加验证" value="demacia" />
                <el-option label="VIP 验证" value="vip" />
              </el-select>
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="存储类型">
              <el-select v-model="editForm.storage_type" style="width: 100%;">
                <el-option label="UFS" value="ufs" />
                <el-option label="eMMC" value="emmc" />
              </el-select>
            </el-form-item>
          </el-col>
        </el-row>
        <el-form-item label="备注">
          <el-input v-model="editForm.notes" type="textarea" :rows="2" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="editDialogVisible = false">取消</el-button>
        <el-button type="primary" @click="saveEdit" :loading="editLoading">保存</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup>
import { ref, reactive, onMounted } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import api from '@/api'
import {
  Search, Plus, Download, Connection, Edit,
  VideoPause, VideoPlay, Delete
} from '@element-plus/icons-vue'

const loading = ref(false)
const loaders = ref([])
const total = ref(0)
const page = ref(1)
const pageSize = ref(20)
const search = ref('')
const authFilter = ref('')

const editDialogVisible = ref(false)
const editLoading = ref(false)
const editForm = reactive({})

const loadLoaders = async () => {
  loading.value = true
  try {
    const res = await api.getLoaders({
      page: page.value,
      page_size: pageSize.value,
      keyword: search.value,
      auth_type: authFilter.value
    })
    if (res.code === 0) {
      loaders.value = res.data.list || []
      total.value = res.data.total || 0
    }
  } catch (e) {
    console.error('加载列表失败', e)
  }
  loading.value = false
}

const editLoader = (row) => {
  Object.assign(editForm, row)
  editDialogVisible.value = true
}

const saveEdit = async () => {
  editLoading.value = true
  try {
    const res = await api.updateLoader(editForm.id, editForm)
    if (res.code === 0) {
      ElMessage.success('保存成功')
      editDialogVisible.value = false
      loadLoaders()
    } else {
      ElMessage.error(res.message || '保存失败')
    }
  } catch (e) {
    ElMessage.error('保存失败')
  }
  editLoading.value = false
}

const toggleLoader = async (row, enable) => {
  try {
    const res = enable ? await api.enableLoader(row.id) : await api.disableLoader(row.id)
    if (res.code === 0) {
      ElMessage.success(enable ? '已启用' : '已禁用')
      loadLoaders()
    }
  } catch (e) {
    ElMessage.error('操作失败')
  }
}

const deleteLoader = async (row) => {
  try {
    await ElMessageBox.confirm(`确定要删除 "${row.filename}" 吗？此操作不可恢复。`, '确认删除', { type: 'warning' })
    const res = await api.deleteLoader(row.id)
    if (res.code === 0) {
      ElMessage.success('删除成功')
      loadLoaders()
    } else {
      ElMessage.error(res.message || '删除失败')
    }
  } catch (e) {
    if (e !== 'cancel') ElMessage.error('删除失败')
  }
}

const formatSize = (bytes) => {
  if (bytes < 1024) return bytes + ' B'
  if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(1) + ' KB'
  return (bytes / 1024 / 1024).toFixed(2) + ' MB'
}

const getAuthTypeLabel = (type) => {
  const labels = { none: '无验证', miauth: '小米', demacia: '一加', vip: 'VIP' }
  return labels[type] || type
}

const getVendorClass = (vendor) => {
  if (!vendor) return ''
  const v = vendor.toLowerCase()
  if (v.includes('xiaomi') || v.includes('redmi') || v.includes('poco')) return 'xiaomi'
  if (v.includes('oplus') || v.includes('oneplus') || v.includes('oppo') || v.includes('realme')) return 'oplus'
  if (v.includes('samsung')) return 'samsung'
  if (v.includes('huawei') || v.includes('honor')) return 'huawei'
  return ''
}

onMounted(() => {
  loadLoaders()
})
</script>
