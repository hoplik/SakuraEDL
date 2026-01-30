<template>
  <div>
    <div class="page-header">
      <div>
        <h1 class="page-title">MTK 资源管理</h1>
        <p class="page-subtitle">管理 MediaTek DA 文件、Auth 文件等资源</p>
      </div>
      <div style="display: flex; gap: 12px;">
        <el-input
          v-model="search"
          placeholder="搜索芯片名、HW Code..."
          style="width: 220px"
          @keyup.enter="loadResources"
          clearable
        >
          <template #prefix><el-icon><Search /></el-icon></template>
        </el-input>
        <el-select v-model="typeFilter" placeholder="资源类型" style="width: 130px" @change="loadResources" clearable>
          <el-option label="全部类型" value="" />
          <el-option label="DA 文件" value="da" />
          <el-option label="Auth 文件" value="auth" />
          <el-option label="Preloader" value="preloader" />
          <el-option label="其他" value="other" />
        </el-select>
        <el-button type="primary" @click="showUploadDialog" round>
          <el-icon><Upload /></el-icon> 上传资源
        </el-button>
      </div>
    </div>

    <!-- 统计卡片 -->
    <div class="stats-row" v-if="stats">
      <div class="stat-card">
        <div class="stat-value">{{ stats.total_resources || 0 }}</div>
        <div class="stat-label">资源总数</div>
      </div>
      <div class="stat-card">
        <div class="stat-value">{{ stats.total_logs || 0 }}</div>
        <div class="stat-label">设备日志</div>
      </div>
      <div class="stat-card">
        <div class="stat-value">{{ stats.today_logs || 0 }}</div>
        <div class="stat-label">今日请求</div>
      </div>
      <div class="stat-card">
        <div class="stat-value">{{ stats.total_downloads || 0 }}</div>
        <div class="stat-label">总下载量</div>
      </div>
    </div>

    <el-tabs v-model="activeTab" @tab-change="handleTabChange">
      <el-tab-pane label="资源列表" name="resources">
        <div class="card">
          <div class="card-body" style="padding: 0;">
            <el-table :data="resources" v-loading="loading" stripe style="width: 100%;">
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
                      <el-tag size="small" type="warning">{{ getTypeLabel(row.resource_type) }}</el-tag>
                      <span style="color: #64748b; font-size: 12px;">{{ row.chip_name }}</span>
                      <span style="color: #94a3b8; font-size: 11px;">{{ formatSize(row.file_size) }}</span>
                    </div>
                  </div>
                </template>
              </el-table-column>
              
              <el-table-column prop="hw_code" label="HW Code" width="110">
                <template #default="{ row }">
                  <span style="font-family: monospace; font-size: 12px; color: #64748b;">{{ row.hw_code || '-' }}</span>
                </template>
              </el-table-column>
              
              <el-table-column prop="da_mode" label="DA 模式" width="100" align="center">
                <template #default="{ row }">
                  <el-tag v-if="row.da_mode" size="small" type="info">{{ row.da_mode }}</el-tag>
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
              
              <el-table-column prop="downloads" label="下载" width="80" align="center">
                <template #default="{ row }">
                  <span style="font-size: 13px; color: #64748b;">{{ row.downloads || 0 }}</span>
                </template>
              </el-table-column>
              
              <el-table-column label="操作" width="200" fixed="right" align="center">
                <template #default="{ row }">
                  <el-button size="small" @click="editResource(row)" text type="primary">
                    <el-icon><Edit /></el-icon> 编辑
                  </el-button>
                  <el-button
                    v-if="row.is_enabled"
                    size="small"
                    @click="toggleResource(row, false)"
                    text
                    type="warning"
                  >禁用</el-button>
                  <el-button
                    v-else
                    size="small"
                    @click="toggleResource(row, true)"
                    text
                    type="success"
                  >启用</el-button>
                  <el-button size="small" @click="deleteResource(row)" text type="danger">
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
              layout="sizes, prev, pager, next"
              @size-change="loadResources"
              @current-change="loadResources"
            />
          </div>
        </div>
      </el-tab-pane>

      <el-tab-pane label="设备日志" name="logs">
        <div class="card">
          <div class="card-body" style="padding: 0;">
            <el-table :data="logs" v-loading="logsLoading" stripe style="width: 100%;">
              <el-table-column prop="id" label="ID" width="70" align="center" />
              <el-table-column prop="hw_code" label="HW Code" width="100">
                <template #default="{ row }">
                  <code style="font-size: 12px;">{{ row.hw_code }}</code>
                </template>
              </el-table-column>
              <el-table-column prop="chip_name" label="芯片名称" width="120" />
              <el-table-column prop="secure_boot" label="安全启动" width="90" align="center">
                <template #default="{ row }">
                  <el-tag :type="row.secure_boot === 'true' || row.secure_boot === '1' ? 'danger' : 'success'" size="small">
                    {{ row.secure_boot === 'true' || row.secure_boot === '1' ? '开启' : '关闭' }}
                  </el-tag>
                </template>
              </el-table-column>
              <el-table-column prop="da_mode" label="DA 模式" width="90" />
              <el-table-column prop="match_result" label="匹配结果" width="100" align="center">
                <template #default="{ row }">
                  <el-tag :type="row.match_result === 'success' ? 'success' : 'info'" size="small">
                    {{ row.match_result }}
                  </el-tag>
                </template>
              </el-table-column>
              <el-table-column prop="client_ip" label="IP" width="120" />
              <el-table-column prop="created_at" label="时间" width="160" />
            </el-table>
          </div>
          <div class="card-body" style="border-top: 1px solid #f1f5f9; display: flex; justify-content: flex-end;">
            <el-pagination
              v-model:current-page="logsPage"
              v-model:page-size="logsPageSize"
              :total="logsTotal"
              :page-sizes="[20, 50, 100]"
              layout="sizes, prev, pager, next"
              @size-change="loadLogs"
              @current-change="loadLogs"
            />
          </div>
        </div>
      </el-tab-pane>
    </el-tabs>

    <!-- 上传对话框 -->
    <el-dialog v-model="uploadDialogVisible" title="上传 MTK 资源" width="550px">
      <el-form :model="uploadForm" label-width="100px">
        <el-form-item label="资源类型" required>
          <el-select v-model="uploadForm.resource_type" style="width: 100%;">
            <el-option label="DA 文件" value="da" />
            <el-option label="Auth 文件" value="auth" />
            <el-option label="Preloader" value="preloader" />
            <el-option label="其他" value="other" />
          </el-select>
        </el-form-item>
        <el-row :gutter="20">
          <el-col :span="12">
            <el-form-item label="HW Code">
              <el-input v-model="uploadForm.hw_code" placeholder="如 0x0717" />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="芯片名称">
              <el-input v-model="uploadForm.chip_name" placeholder="如 MT6765" />
            </el-form-item>
          </el-col>
        </el-row>
        <el-form-item label="DA 模式">
          <el-select v-model="uploadForm.da_mode" style="width: 100%;" clearable>
            <el-option label="XML" value="XML" />
            <el-option label="XFlash" value="XFlash" />
            <el-option label="Legacy" value="Legacy" />
          </el-select>
        </el-form-item>
        <el-form-item label="描述">
          <el-input v-model="uploadForm.description" type="textarea" :rows="2" />
        </el-form-item>
        <el-form-item label="选择文件" required>
          <el-upload
            ref="uploadRef"
            :auto-upload="false"
            :limit="1"
            :on-change="handleFileChange"
            drag
          >
            <el-icon class="el-icon--upload"><UploadFilled /></el-icon>
            <div class="el-upload__text">拖拽文件到这里或 <em>点击上传</em></div>
          </el-upload>
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="uploadDialogVisible = false">取消</el-button>
        <el-button type="primary" @click="submitUpload" :loading="uploadLoading">上传</el-button>
      </template>
    </el-dialog>

    <!-- 编辑对话框 -->
    <el-dialog v-model="editDialogVisible" title="编辑资源" width="500px">
      <el-form :model="editForm" label-width="100px">
        <el-form-item label="文件名">
          <el-input v-model="editForm.filename" disabled />
        </el-form-item>
        <el-row :gutter="20">
          <el-col :span="12">
            <el-form-item label="HW Code">
              <el-input v-model="editForm.hw_code" />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="芯片名称">
              <el-input v-model="editForm.chip_name" />
            </el-form-item>
          </el-col>
        </el-row>
        <el-form-item label="DA 模式">
          <el-select v-model="editForm.da_mode" style="width: 100%;" clearable>
            <el-option label="XML" value="XML" />
            <el-option label="XFlash" value="XFlash" />
            <el-option label="Legacy" value="Legacy" />
          </el-select>
        </el-form-item>
        <el-form-item label="描述">
          <el-input v-model="editForm.description" type="textarea" :rows="2" />
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
import { Search, Upload, Edit, Delete, UploadFilled } from '@element-plus/icons-vue'

const loading = ref(false)
const resources = ref([])
const total = ref(0)
const page = ref(1)
const pageSize = ref(20)
const search = ref('')
const typeFilter = ref('')
const activeTab = ref('resources')
const stats = ref({})

// 日志
const logsLoading = ref(false)
const logs = ref([])
const logsTotal = ref(0)
const logsPage = ref(1)
const logsPageSize = ref(20)

// 上传
const uploadDialogVisible = ref(false)
const uploadLoading = ref(false)
const uploadRef = ref()
const uploadFile = ref(null)
const uploadForm = reactive({
  resource_type: 'da',
  hw_code: '',
  chip_name: '',
  da_mode: '',
  description: ''
})

// 编辑
const editDialogVisible = ref(false)
const editLoading = ref(false)
const editForm = reactive({})

const loadResources = async () => {
  loading.value = true
  try {
    const res = await api.getMtkResources({
      page: page.value,
      page_size: pageSize.value,
      keyword: search.value,
      type: typeFilter.value
    })
    if (res.code === 0) {
      resources.value = res.data.resources || []
      total.value = res.data.total || 0
    }
  } catch (e) {
    console.error('加载列表失败', e)
  }
  loading.value = false
}

const loadStats = async () => {
  try {
    const res = await api.getMtkStats()
    if (res.code === 0) {
      stats.value = res.data
    }
  } catch (e) {
    console.error('加载统计失败', e)
  }
}

const loadLogs = async () => {
  logsLoading.value = true
  try {
    const res = await api.getMtkLogs({
      page: logsPage.value,
      page_size: logsPageSize.value,
      keyword: search.value
    })
    if (res.code === 0) {
      logs.value = res.data.logs || []
      logsTotal.value = res.data.total || 0
    }
  } catch (e) {
    console.error('加载日志失败', e)
  }
  logsLoading.value = false
}

const handleTabChange = (tab) => {
  if (tab === 'logs' && logs.value.length === 0) {
    loadLogs()
  }
}

const showUploadDialog = () => {
  Object.assign(uploadForm, { resource_type: 'da', hw_code: '', chip_name: '', da_mode: '', description: '' })
  uploadFile.value = null
  uploadDialogVisible.value = true
}

const handleFileChange = (file) => {
  uploadFile.value = file.raw
}

const submitUpload = async () => {
  if (!uploadFile.value) {
    ElMessage.warning('请选择文件')
    return
  }
  
  uploadLoading.value = true
  try {
    const formData = new FormData()
    formData.append('file', uploadFile.value)
    formData.append('resource_type', uploadForm.resource_type)
    formData.append('hw_code', uploadForm.hw_code)
    formData.append('chip_name', uploadForm.chip_name)
    formData.append('da_mode', uploadForm.da_mode)
    formData.append('description', uploadForm.description)
    
    const res = await api.uploadMtkResource(formData)
    if (res.code === 0) {
      ElMessage.success('上传成功')
      uploadDialogVisible.value = false
      loadResources()
      loadStats()
    } else {
      ElMessage.error(res.message || '上传失败')
    }
  } catch (e) {
    ElMessage.error('上传失败')
  }
  uploadLoading.value = false
}

const editResource = (row) => {
  Object.assign(editForm, row)
  editDialogVisible.value = true
}

const saveEdit = async () => {
  editLoading.value = true
  try {
    const res = await api.updateMtkResource(editForm.id, editForm)
    if (res.code === 0) {
      ElMessage.success('保存成功')
      editDialogVisible.value = false
      loadResources()
    } else {
      ElMessage.error(res.message || '保存失败')
    }
  } catch (e) {
    ElMessage.error('保存失败')
  }
  editLoading.value = false
}

const toggleResource = async (row, enable) => {
  try {
    const res = await api.updateMtkResource(row.id, { is_enabled: enable })
    if (res.code === 0) {
      ElMessage.success(enable ? '已启用' : '已禁用')
      loadResources()
    }
  } catch (e) {
    ElMessage.error('操作失败')
  }
}

const deleteResource = async (row) => {
  try {
    await ElMessageBox.confirm(`确定要删除 "${row.filename}" 吗？`, '确认删除', { type: 'warning' })
    const res = await api.deleteMtkResource(row.id)
    if (res.code === 0) {
      ElMessage.success('删除成功')
      loadResources()
      loadStats()
    } else {
      ElMessage.error(res.message || '删除失败')
    }
  } catch (e) {
    if (e !== 'cancel') ElMessage.error('删除失败')
  }
}

const formatSize = (bytes) => {
  if (!bytes) return '-'
  if (bytes < 1024) return bytes + ' B'
  if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(1) + ' KB'
  return (bytes / 1024 / 1024).toFixed(2) + ' MB'
}

const getTypeLabel = (type) => {
  const labels = { da: 'DA', auth: 'Auth', preloader: 'Preloader', other: '其他' }
  return labels[type] || type
}

onMounted(() => {
  loadResources()
  loadStats()
})
</script>

<style scoped>
.stats-row {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 16px;
  margin-bottom: 24px;
}

.stat-card {
  background: #fff;
  border-radius: 12px;
  padding: 20px;
  text-align: center;
  border: 1px solid #e2e8f0;
}

.stat-value {
  font-size: 28px;
  font-weight: 700;
  color: #f59e0b;
}

.stat-label {
  font-size: 13px;
  color: #64748b;
  margin-top: 4px;
}
</style>
