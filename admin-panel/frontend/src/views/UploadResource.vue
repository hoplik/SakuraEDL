<template>
  <div>
    <div class="page-header">
      <div>
        <h1 class="page-title">上传资源</h1>
        <p class="page-subtitle">添加高通 / 联发科 / 展锐平台资源文件到云端</p>
      </div>
    </div>

    <!-- 平台选择 -->
    <div class="platform-selector">
      <div
        v-for="p in platforms"
        :key="p.key"
        :class="['platform-card', { active: platform === p.key }]"
        @click="switchPlatform(p.key)"
      >
        <span class="platform-icon">{{ p.icon }}</span>
        <span class="platform-name">{{ p.name }}</span>
        <span class="platform-desc">{{ p.desc }}</span>
      </div>
    </div>

    <!-- 高通上传表单 -->
    <div v-if="platform === 'qualcomm'" class="card">
      <div class="card-header">
        <span class="card-title"><el-icon><Upload /></el-icon> Qualcomm Loader 文件</span>
      </div>
      <div class="card-body">
        <el-form :model="qcForm" label-width="120px" label-position="left">
          <el-form-item label="Loader 文件" required>
            <div
              class="upload-area"
              :class="{ dragover: isDragover }"
              @drop.prevent="handleDrop($event, 'qc', 'loader')"
              @dragover.prevent="isDragover = true"
              @dragleave.prevent="isDragover = false"
              @click="$refs.qcLoaderInput.click()"
            >
              <div class="upload-icon"><el-icon><Upload /></el-icon></div>
              <div v-if="!qcForm.loaderFile">
                <h4>拖拽文件到此处，或点击上传</h4>
                <p>支持 .bin, .elf, .mbn, .melf 格式</p>
              </div>
              <div v-else class="file-selected">
                <h4>✓ {{ qcForm.loaderFile.name }}</h4>
                <p>文件大小: {{ formatSize(qcForm.loaderFile.size) }}</p>
              </div>
            </div>
            <input ref="qcLoaderInput" type="file" hidden @change="handleFileSelect($event, 'qc', 'loader')" accept=".bin,.elf,.mbn,.melf">
          </el-form-item>

          <el-row :gutter="20">
            <el-col :span="12">
              <el-form-item label="厂商">
                <el-select v-model="qcForm.vendor" filterable allow-create placeholder="选择或输入厂商" style="width: 100%;">
                  <el-option-group label="常用厂商">
                    <el-option label="Xiaomi (小米)" value="xiaomi" />
                    <el-option label="Redmi (红米)" value="redmi" />
                    <el-option label="POCO" value="poco" />
                    <el-option label="OPLUS (欧加)" value="oplus" />
                    <el-option label="OnePlus (一加)" value="oneplus" />
                    <el-option label="OPPO" value="oppo" />
                    <el-option label="Realme (真我)" value="realme" />
                    <el-option label="Vivo" value="vivo" />
                  </el-option-group>
                  <el-option-group label="其他厂商">
                    <el-option label="Samsung (三星)" value="samsung" />
                    <el-option label="Huawei (华为)" value="huawei" />
                    <el-option label="Honor (荣耀)" value="honor" />
                    <el-option label="Motorola" value="motorola" />
                    <el-option label="Google" value="google" />
                  </el-option-group>
                </el-select>
              </el-form-item>
            </el-col>
            <el-col :span="12">
              <el-form-item label="芯片型号">
                <el-select v-model="qcForm.chip" filterable allow-create placeholder="选择或输入芯片" style="width: 100%;">
                  <el-option-group label="高通 Snapdragon 8 系列">
                    <el-option label="SM8750 (Snapdragon 8 Elite)" value="SM8750" />
                    <el-option label="SM8650 (Snapdragon 8 Gen 3)" value="SM8650" />
                    <el-option label="SM8550 (Snapdragon 8 Gen 2)" value="SM8550" />
                    <el-option label="SM8475 (Snapdragon 8+ Gen 1)" value="SM8475" />
                    <el-option label="SM8450 (Snapdragon 8 Gen 1)" value="SM8450" />
                    <el-option label="SM8350 (Snapdragon 888)" value="SM8350" />
                    <el-option label="SM8250 (Snapdragon 865)" value="SM8250" />
                    <el-option label="SM8150 (Snapdragon 855)" value="SM8150" />
                  </el-option-group>
                  <el-option-group label="高通 Snapdragon 7/6 系列">
                    <el-option label="SM7550 (Snapdragon 7 Gen 3)" value="SM7550" />
                    <el-option label="SM7475 (Snapdragon 7+ Gen 2)" value="SM7475" />
                    <el-option label="SM7450 (Snapdragon 7 Gen 1)" value="SM7450" />
                    <el-option label="SM6450 (Snapdragon 6 Gen 1)" value="SM6450" />
                    <el-option label="SM6375 (Snapdragon 695)" value="SM6375" />
                  </el-option-group>
                </el-select>
              </el-form-item>
            </el-col>
          </el-row>

          <el-row :gutter="20">
            <el-col :span="12">
              <el-form-item label="HW ID">
                <el-input v-model="qcForm.hw_id" placeholder="如: 009600E1" />
              </el-form-item>
            </el-col>
            <el-col :span="12">
              <el-form-item label="OEM ID">
                <el-input v-model="qcForm.oem_id" placeholder="如: 0x0001" />
              </el-form-item>
            </el-col>
          </el-row>

          <el-form-item label="PK Hash">
            <el-input v-model="qcForm.pk_hash" placeholder="64 位十六进制字符串 (可选，用于精确匹配)" />
          </el-form-item>

          <el-row :gutter="20">
            <el-col :span="12">
              <el-form-item label="存储类型">
                <el-radio-group v-model="qcForm.storage_type">
                  <el-radio value="ufs">UFS</el-radio>
                  <el-radio value="emmc">eMMC</el-radio>
                </el-radio-group>
              </el-form-item>
            </el-col>
            <el-col :span="12">
              <el-form-item label="验证类型" required>
                <el-radio-group v-model="qcForm.auth_type">
                  <el-radio value="none">无验证</el-radio>
                  <el-radio value="miauth">小米验证</el-radio>
                  <el-radio value="demacia">一加验证</el-radio>
                  <el-radio value="vip">VIP 验证</el-radio>
                </el-radio-group>
              </el-form-item>
            </el-col>
          </el-row>

          <!-- VIP 验证文件 -->
          <div v-if="qcForm.auth_type === 'vip'" class="vip-files">
            <div class="vip-file-box">
              <h4>📄 Digest 文件 (必需)</h4>
              <div
                class="upload-area small"
                @drop.prevent="handleDrop($event, 'qc', 'digest')"
                @dragover.prevent
                @click="$refs.qcDigestInput.click()"
              >
                <p v-if="!qcForm.digestFile">点击或拖拽上传</p>
                <p v-else class="file-ok">✓ {{ qcForm.digestFile.name }}</p>
              </div>
              <input ref="qcDigestInput" type="file" hidden @change="handleFileSelect($event, 'qc', 'digest')">
            </div>
            <div class="vip-file-box">
              <h4>🔐 Sign 文件 (必需)</h4>
              <div
                class="upload-area small"
                @drop.prevent="handleDrop($event, 'qc', 'sign')"
                @dragover.prevent
                @click="$refs.qcSignInput.click()"
              >
                <p v-if="!qcForm.signFile">点击或拖拽上传</p>
                <p v-else class="file-ok">✓ {{ qcForm.signFile.name }}</p>
              </div>
              <input ref="qcSignInput" type="file" hidden @change="handleFileSelect($event, 'qc', 'sign')">
            </div>
          </div>

          <el-form-item label="备注" style="margin-top: 24px;">
            <el-input v-model="qcForm.notes" type="textarea" :rows="2" placeholder="可选备注信息" />
          </el-form-item>

          <el-form-item>
            <el-button type="primary" @click="uploadQualcomm" :loading="uploading" size="large">
              <el-icon><Upload /></el-icon> 上传 Qualcomm 资源
            </el-button>
            <el-button @click="resetQcForm" size="large">重置</el-button>
          </el-form-item>
        </el-form>
      </div>
    </div>

    <!-- MTK 上传表单 -->
    <div v-if="platform === 'mtk'" class="card">
      <div class="card-header">
        <span class="card-title"><el-icon><Upload /></el-icon> MediaTek 资源文件</span>
      </div>
      <div class="card-body">
        <el-form :model="mtkForm" label-width="120px" label-position="left">
          <el-form-item label="资源文件" required>
            <div
              class="upload-area"
              :class="{ dragover: isDragover }"
              @drop.prevent="handleDrop($event, 'mtk', 'file')"
              @dragover.prevent="isDragover = true"
              @dragleave.prevent="isDragover = false"
              @click="$refs.mtkFileInput.click()"
            >
              <div class="upload-icon"><el-icon><Upload /></el-icon></div>
              <div v-if="!mtkForm.file">
                <h4>拖拽文件到此处，或点击上传</h4>
                <p>支持 DA, Auth, Preloader 等资源文件</p>
              </div>
              <div v-else class="file-selected">
                <h4>✓ {{ mtkForm.file.name }}</h4>
                <p>文件大小: {{ formatSize(mtkForm.file.size) }}</p>
              </div>
            </div>
            <input ref="mtkFileInput" type="file" hidden @change="handleFileSelect($event, 'mtk', 'file')">
          </el-form-item>

          <el-row :gutter="20">
            <el-col :span="12">
              <el-form-item label="资源类型" required>
                <el-select v-model="mtkForm.resource_type" placeholder="选择资源类型" style="width: 100%;">
                  <el-option label="DA (Download Agent)" value="da" />
                  <el-option label="Auth 文件" value="auth" />
                  <el-option label="Preloader" value="preloader" />
                  <el-option label="其他" value="other" />
                </el-select>
              </el-form-item>
            </el-col>
            <el-col :span="12">
              <el-form-item label="DA 模式">
                <el-select v-model="mtkForm.da_mode" placeholder="选择 DA 模式" style="width: 100%;">
                  <el-option label="BROM" value="brom" />
                  <el-option label="Preloader" value="preloader" />
                  <el-option label="通用" value="generic" />
                </el-select>
              </el-form-item>
            </el-col>
          </el-row>

          <el-row :gutter="20">
            <el-col :span="12">
              <el-form-item label="HW Code">
                <el-input v-model="mtkForm.hw_code" placeholder="如: 0x6893" />
              </el-form-item>
            </el-col>
            <el-col :span="12">
              <el-form-item label="芯片名称">
                <el-select v-model="mtkForm.chip_name" filterable allow-create placeholder="选择或输入芯片" style="width: 100%;">
                  <el-option-group label="天玑旗舰">
                    <el-option label="MT6989 (天玑 9400)" value="MT6989" />
                    <el-option label="MT6985 (天玑 9300)" value="MT6985" />
                    <el-option label="MT6983 (天玑 9200)" value="MT6983" />
                    <el-option label="MT6895 (天玑 8100)" value="MT6895" />
                    <el-option label="MT6893 (天玑 1200)" value="MT6893" />
                  </el-option-group>
                  <el-option-group label="天玑中端">
                    <el-option label="MT6877 (天玑 900)" value="MT6877" />
                    <el-option label="MT6853 (天玑 720)" value="MT6853" />
                    <el-option label="MT6833 (天玑 700)" value="MT6833" />
                  </el-option-group>
                  <el-option-group label="Helio 系列">
                    <el-option label="MT6785 (Helio G95)" value="MT6785" />
                    <el-option label="MT6769 (Helio G80)" value="MT6769" />
                    <el-option label="MT6765 (Helio P35)" value="MT6765" />
                  </el-option-group>
                </el-select>
              </el-form-item>
            </el-col>
          </el-row>

          <el-form-item label="描述">
            <el-input v-model="mtkForm.description" type="textarea" :rows="2" placeholder="资源描述信息" />
          </el-form-item>

          <el-form-item>
            <el-button type="primary" @click="uploadMtk" :loading="uploading" size="large">
              <el-icon><Upload /></el-icon> 上传 MTK 资源
            </el-button>
            <el-button @click="resetMtkForm" size="large">重置</el-button>
          </el-form-item>
        </el-form>
      </div>
    </div>

    <!-- SPD 上传表单 -->
    <div v-if="platform === 'spd'" class="card">
      <div class="card-header">
        <span class="card-title"><el-icon><Upload /></el-icon> Spreadtrum/UNISOC 资源文件</span>
      </div>
      <div class="card-body">
        <el-form :model="spdForm" label-width="120px" label-position="left">
          <el-form-item label="资源文件" required>
            <div
              class="upload-area"
              :class="{ dragover: isDragover }"
              @drop.prevent="handleDrop($event, 'spd', 'file')"
              @dragover.prevent="isDragover = true"
              @dragleave.prevent="isDragover = false"
              @click="$refs.spdFileInput.click()"
            >
              <div class="upload-icon"><el-icon><Upload /></el-icon></div>
              <div v-if="!spdForm.file">
                <h4>拖拽文件到此处，或点击上传</h4>
                <p>支持 FDL1, FDL2, Preloader 等资源文件</p>
              </div>
              <div v-else class="file-selected">
                <h4>✓ {{ spdForm.file.name }}</h4>
                <p>文件大小: {{ formatSize(spdForm.file.size) }}</p>
              </div>
            </div>
            <input ref="spdFileInput" type="file" hidden @change="handleFileSelect($event, 'spd', 'file')">
          </el-form-item>

          <el-row :gutter="20">
            <el-col :span="12">
              <el-form-item label="资源类型" required>
                <el-select v-model="spdForm.resource_type" placeholder="选择资源类型" style="width: 100%;">
                  <el-option label="FDL1" value="fdl1" />
                  <el-option label="FDL2" value="fdl2" />
                  <el-option label="Preloader" value="preloader" />
                  <el-option label="其他" value="other" />
                </el-select>
              </el-form-item>
            </el-col>
            <el-col :span="12">
              <el-form-item label="Chip ID">
                <el-input v-model="spdForm.chip_id" placeholder="如: 0x9863A" />
              </el-form-item>
            </el-col>
          </el-row>

          <el-row :gutter="20">
            <el-col :span="12">
              <el-form-item label="芯片名称">
                <el-select v-model="spdForm.chip_name" filterable allow-create placeholder="选择或输入芯片" style="width: 100%;">
                  <el-option-group label="虎贲系列">
                    <el-option label="T820 (虎贲 T820)" value="T820" />
                    <el-option label="T770 (虎贲 T770)" value="T770" />
                    <el-option label="T760 (虎贲 T760)" value="T760" />
                    <el-option label="T740 (虎贲 T740)" value="T740" />
                    <el-option label="T700 (虎贲 T700)" value="T700" />
                    <el-option label="T618 (虎贲 T618)" value="T618" />
                    <el-option label="T616 (虎贲 T616)" value="T616" />
                    <el-option label="T612 (虎贲 T612)" value="T612" />
                    <el-option label="T606 (虎贲 T606)" value="T606" />
                  </el-option-group>
                  <el-option-group label="SC 系列">
                    <el-option label="SC9863A" value="SC9863A" />
                    <el-option label="SC9832E" value="SC9832E" />
                    <el-option label="SC7731E" value="SC7731E" />
                  </el-option-group>
                </el-select>
              </el-form-item>
            </el-col>
          </el-row>

          <el-form-item label="描述">
            <el-input v-model="spdForm.description" type="textarea" :rows="2" placeholder="资源描述信息" />
          </el-form-item>

          <el-form-item>
            <el-button type="primary" @click="uploadSpd" :loading="uploading" size="large">
              <el-icon><Upload /></el-icon> 上传 SPD 资源
            </el-button>
            <el-button @click="resetSpdForm" size="large">重置</el-button>
          </el-form-item>
        </el-form>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, reactive } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import api from '@/api'
import { Upload } from '@element-plus/icons-vue'

const router = useRouter()
const uploading = ref(false)
const isDragover = ref(false)
const platform = ref('qualcomm')

const platforms = [
  { key: 'qualcomm', name: 'Qualcomm', icon: '📱', desc: '高通 Firehose Loader' },
  { key: 'mtk', name: 'MediaTek', icon: '⚡', desc: '联发科 DA / Auth' },
  { key: 'spd', name: 'Spreadtrum', icon: '🔧', desc: '展锐 FDL1 / FDL2' }
]

// 高通表单
const qcForm = reactive({
  loaderFile: null,
  digestFile: null,
  signFile: null,
  vendor: '',
  chip: '',
  hw_id: '',
  pk_hash: '',
  oem_id: '',
  auth_type: 'none',
  storage_type: 'ufs',
  notes: ''
})

// MTK 表单
const mtkForm = reactive({
  file: null,
  resource_type: 'da',
  hw_code: '',
  chip_name: '',
  da_mode: 'brom',
  description: ''
})

// SPD 表单
const spdForm = reactive({
  file: null,
  resource_type: 'fdl1',
  chip_id: '',
  chip_name: '',
  description: ''
})

const switchPlatform = (p) => {
  platform.value = p
}

const handleFileSelect = (e, platform, type) => {
  const file = e.target.files[0]
  if (!file) return
  
  if (platform === 'qc') {
    if (type === 'loader') qcForm.loaderFile = file
    else if (type === 'digest') qcForm.digestFile = file
    else if (type === 'sign') qcForm.signFile = file
  } else if (platform === 'mtk') {
    mtkForm.file = file
  } else if (platform === 'spd') {
    spdForm.file = file
  }
}

const handleDrop = (e, platform, type) => {
  const file = e.dataTransfer.files[0]
  if (!file) return
  
  if (platform === 'qc') {
    if (type === 'loader') qcForm.loaderFile = file
    else if (type === 'digest') qcForm.digestFile = file
    else if (type === 'sign') qcForm.signFile = file
  } else if (platform === 'mtk') {
    mtkForm.file = file
  } else if (platform === 'spd') {
    spdForm.file = file
  }
  isDragover.value = false
}

// 上传高通
const uploadQualcomm = async () => {
  if (!qcForm.loaderFile) {
    ElMessage.warning('请选择 Loader 文件')
    return
  }
  if (qcForm.auth_type === 'vip') {
    if (!qcForm.digestFile || !qcForm.signFile) {
      ElMessage.warning('VIP 类型需要上传 Digest 和 Sign 文件')
      return
    }
  }

  uploading.value = true
  try {
    const formData = new FormData()
    formData.append('loader', qcForm.loaderFile)
    formData.append('vendor', qcForm.vendor)
    formData.append('chip', qcForm.chip)
    formData.append('hw_id', qcForm.hw_id)
    formData.append('pk_hash', qcForm.pk_hash)
    formData.append('oem_id', qcForm.oem_id)
    formData.append('auth_type', qcForm.auth_type)
    formData.append('storage_type', qcForm.storage_type)
    formData.append('notes', qcForm.notes)

    if (qcForm.auth_type === 'vip') {
      formData.append('digest', qcForm.digestFile)
      formData.append('sign', qcForm.signFile)
    }

    const res = await api.uploadLoader(formData)
    if (res.code === 0) {
      ElMessage.success('Qualcomm 资源上传成功')
      router.push('/loaders')
    } else {
      ElMessage.error(res.message || '上传失败')
    }
  } catch (e) {
    ElMessage.error('上传失败: ' + e.message)
  }
  uploading.value = false
}

// 上传 MTK
const uploadMtk = async () => {
  if (!mtkForm.file) {
    ElMessage.warning('请选择资源文件')
    return
  }
  if (!mtkForm.resource_type) {
    ElMessage.warning('请选择资源类型')
    return
  }

  uploading.value = true
  try {
    const formData = new FormData()
    formData.append('file', mtkForm.file)
    formData.append('resource_type', mtkForm.resource_type)
    formData.append('hw_code', mtkForm.hw_code)
    formData.append('chip_name', mtkForm.chip_name)
    formData.append('da_mode', mtkForm.da_mode)
    formData.append('description', mtkForm.description)

    const res = await api.uploadMtkResource(formData)
    if (res.code === 0) {
      ElMessage.success('MTK 资源上传成功')
      router.push('/mtk')
    } else {
      ElMessage.error(res.message || '上传失败')
    }
  } catch (e) {
    ElMessage.error('上传失败: ' + e.message)
  }
  uploading.value = false
}

// 上传 SPD
const uploadSpd = async () => {
  if (!spdForm.file) {
    ElMessage.warning('请选择资源文件')
    return
  }
  if (!spdForm.resource_type) {
    ElMessage.warning('请选择资源类型')
    return
  }

  uploading.value = true
  try {
    const formData = new FormData()
    formData.append('file', spdForm.file)
    formData.append('resource_type', spdForm.resource_type)
    formData.append('chip_id', spdForm.chip_id)
    formData.append('chip_name', spdForm.chip_name)
    formData.append('description', spdForm.description)

    const res = await api.uploadSpdResource(formData)
    if (res.code === 0) {
      ElMessage.success('SPD 资源上传成功')
      router.push('/spd')
    } else {
      ElMessage.error(res.message || '上传失败')
    }
  } catch (e) {
    ElMessage.error('上传失败: ' + e.message)
  }
  uploading.value = false
}

const resetQcForm = () => {
  qcForm.loaderFile = null
  qcForm.digestFile = null
  qcForm.signFile = null
  qcForm.vendor = ''
  qcForm.chip = ''
  qcForm.hw_id = ''
  qcForm.pk_hash = ''
  qcForm.oem_id = ''
  qcForm.auth_type = 'none'
  qcForm.storage_type = 'ufs'
  qcForm.notes = ''
}

const resetMtkForm = () => {
  mtkForm.file = null
  mtkForm.resource_type = 'da'
  mtkForm.hw_code = ''
  mtkForm.chip_name = ''
  mtkForm.da_mode = 'brom'
  mtkForm.description = ''
}

const resetSpdForm = () => {
  spdForm.file = null
  spdForm.resource_type = 'fdl1'
  spdForm.chip_id = ''
  spdForm.chip_name = ''
  spdForm.description = ''
}

const formatSize = (bytes) => {
  if (bytes < 1024) return bytes + ' B'
  if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(1) + ' KB'
  return (bytes / 1024 / 1024).toFixed(2) + ' MB'
}
</script>

<style scoped>
.platform-selector {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 16px;
  margin-bottom: 24px;
}

.platform-card {
  display: flex;
  flex-direction: column;
  align-items: center;
  padding: 24px 16px;
  background: #fff;
  border: 2px solid #e2e8f0;
  border-radius: 16px;
  cursor: pointer;
  transition: all 0.3s;
}

.platform-card:hover {
  border-color: #a855f7;
  transform: translateY(-2px);
}

.platform-card.active {
  border-color: #8b5cf6;
  background: linear-gradient(135deg, rgba(139, 92, 246, 0.05), rgba(168, 85, 247, 0.1));
  box-shadow: 0 4px 20px rgba(139, 92, 246, 0.15);
}

.platform-icon {
  font-size: 36px;
  margin-bottom: 8px;
}

.platform-name {
  font-size: 16px;
  font-weight: 600;
  color: #1e293b;
}

.platform-desc {
  font-size: 12px;
  color: #64748b;
  margin-top: 4px;
}

.upload-area {
  border: 2px dashed #e2e8f0;
  border-radius: 12px;
  padding: 40px 20px;
  text-align: center;
  cursor: pointer;
  transition: all 0.3s;
  background: #fafbfc;
}

.upload-area:hover,
.upload-area.dragover {
  border-color: #8b5cf6;
  background: rgba(139, 92, 246, 0.05);
}

.upload-area.small {
  padding: 20px;
}

.upload-area h4 {
  margin: 8px 0 4px;
  font-size: 14px;
  color: #1e293b;
}

.upload-area p {
  margin: 0;
  font-size: 12px;
  color: #64748b;
}

.upload-icon {
  font-size: 32px;
  color: #a855f7;
}

.file-selected h4 {
  color: #10b981;
}

.file-ok {
  color: #10b981 !important;
  font-weight: 500;
}

.vip-files {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 20px;
  margin-top: 24px;
}

.vip-file-box {
  border: 1px solid #e2e8f0;
  border-radius: 12px;
  padding: 16px;
  background: #fafbfc;
}

.vip-file-box h4 {
  font-size: 14px;
  color: #1e293b;
  margin: 0 0 12px;
}

@media (max-width: 768px) {
  .platform-selector {
    grid-template-columns: 1fr;
  }
  
  .vip-files {
    grid-template-columns: 1fr;
  }
}
</style>
