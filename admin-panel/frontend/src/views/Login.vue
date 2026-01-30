<template>
  <div class="login-container">
    <div class="login-box">
      <h2 class="login-title"><span>SakuraEDL</span> Admin</h2>
      
      <el-form :model="form" @submit.prevent="handleLogin">
        <el-form-item>
          <el-input
            v-model="form.username"
            placeholder="管理员用户名"
            :prefix-icon="User"
            size="large"
          />
        </el-form-item>
        
        <el-form-item>
          <el-input
            v-model="form.password"
            type="password"
            placeholder="密码"
            :prefix-icon="Lock"
            size="large"
            show-password
            @keyup.enter="handleLogin"
          />
        </el-form-item>
        
        <el-form-item>
          <el-button
            type="primary"
            :loading="loading"
            @click="handleLogin"
            class="login-btn"
            size="large"
          >
            {{ loading ? '登录中...' : '登 录' }}
          </el-button>
        </el-form-item>
      </el-form>
      
      <p class="login-footer">SakuraEDL Loader 云端管理系统 v3.0</p>
    </div>
  </div>
</template>

<script setup>
import { reactive, ref } from 'vue'
import { User, Lock } from '@element-plus/icons-vue'
import { ElMessage } from 'element-plus'
import { useAuthStore } from '@/stores/auth'

const authStore = useAuthStore()
const loading = ref(false)

const form = reactive({
  username: '',
  password: ''
})

const handleLogin = async () => {
  if (!form.username || !form.password) {
    ElMessage.warning('请输入用户名和密码')
    return
  }
  
  loading.value = true
  const result = await authStore.login(form.username, form.password)
  loading.value = false
  
  if (result.success) {
    ElMessage.success('登录成功')
  } else {
    ElMessage.error(result.message)
  }
}
</script>

<style scoped lang="scss">
.login-btn {
  width: 100%;
  height: 48px;
  font-size: 16px;
  border-radius: 12px;
  background: linear-gradient(135deg, #667eea, #764ba2);
  border: none;
}

.login-footer {
  text-align: center;
  color: rgba(255, 255, 255, 0.4);
  font-size: 12px;
  margin-top: 24px;
}
</style>
