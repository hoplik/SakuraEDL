import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import api from '@/api'
import router from '@/router'

export const useAuthStore = defineStore('auth', () => {
  const token = ref('')
  const username = ref('')

  const isLoggedIn = computed(() => !!token.value)

  function checkAuth() {
    const savedToken = localStorage.getItem('admin_token')
    const savedUsername = localStorage.getItem('admin_username')
    if (savedToken) {
      token.value = savedToken
      username.value = savedUsername || 'admin'
    }
  }

  async function login(user, pass) {
    try {
      const res = await api.login(user, pass)
      if (res.code === 0) {
        token.value = res.data.token
        username.value = res.data.username
        localStorage.setItem('admin_token', res.data.token)
        localStorage.setItem('admin_username', res.data.username)
        router.push('/dashboard')
        return { success: true }
      } else {
        return { success: false, message: res.message || '登录失败' }
      }
    } catch (error) {
      return { success: false, message: '网络错误' }
    }
  }

  function logout() {
    token.value = ''
    username.value = ''
    localStorage.removeItem('admin_token')
    localStorage.removeItem('admin_username')
    router.push('/login')
  }

  return {
    token,
    username,
    isLoggedIn,
    checkAuth,
    login,
    logout
  }
})
