<template>
  <div class="admin-layout">
    <!-- 侧边栏 -->
    <aside class="sidebar">
      <div class="sidebar-logo">
        <div class="logo-icon">⚡</div>
        <span>SakuraEDL</span>
      </div>
      
      <ul class="sidebar-menu">
        <li
          v-for="item in menuItems"
          :key="item.path"
          :class="{ active: currentPath === item.path }"
          @click="navigate(item.path)"
        >
          <el-icon><component :is="item.icon" /></el-icon>
          <span>{{ item.name }}</span>
        </li>
        
        <div class="divider"></div>
        
        <li class="logout" @click="handleLogout">
          <el-icon><SwitchButton /></el-icon>
          <span>退出登录</span>
        </li>
      </ul>
    </aside>

    <!-- 主内容区 -->
    <main class="main-content">
      <router-view />
    </main>
  </div>
</template>

<script setup>
import { computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import {
  DataAnalysis,
  Folder,
  Upload,
  Document,
  Setting,
  SwitchButton,
  Cpu,
  Iphone
} from '@element-plus/icons-vue'

const route = useRoute()
const router = useRouter()
const authStore = useAuthStore()

const currentPath = computed(() => route.path)

const menuItems = [
  { path: '/dashboard', name: '仪表盘', icon: DataAnalysis },
  { path: '/upload', name: '上传资源', icon: Upload },
  { path: '/loaders', name: 'Qualcomm 资源', icon: Folder },
  { path: '/mtk', name: 'MTK 资源', icon: Cpu },
  { path: '/spd', name: 'SPD 资源', icon: Iphone },
  { path: '/logs', name: '设备日志', icon: Document },
  { path: '/settings', name: '系统设置', icon: Setting }
]

const navigate = (path) => {
  router.push(path)
}

const handleLogout = () => {
  authStore.logout()
}
</script>
