<template>
  <header class="vp-nav" :class="{ scrolled: isScrolled }">
    <div class="nav-container">
      <!-- Logo -->
      <router-link to="/" class="nav-logo">
        <img src="/logo.png" alt="SakuraEDL" />
        <span>SakuraEDL</span>
      </router-link>
      
      <!-- 导航菜单 -->
      <nav class="nav-menu" :class="{ open: menuOpen }">
        <router-link to="/" class="nav-link" @click="menuOpen = false">首页</router-link>
        <router-link to="/guide/getting-started" class="nav-link" @click="menuOpen = false">快速开始</router-link>
        
        <div class="nav-dropdown">
          <span class="nav-link dropdown-trigger">
            使用教程
            <svg width="12" height="12" viewBox="0 0 24 24" fill="currentColor">
              <path d="M7 10l5 5 5-5z"/>
            </svg>
          </span>
          <div class="dropdown-menu">
            <router-link to="/guide/qualcomm" @click="menuOpen = false">高通 EDL</router-link>
            <router-link to="/guide/mtk" @click="menuOpen = false">MTK 联发科</router-link>
            <router-link to="/guide/spd" @click="menuOpen = false">展锐 Spreadtrum</router-link>
            <router-link to="/guide/fastboot" @click="menuOpen = false">Fastboot</router-link>
          </div>
        </div>
        
        <router-link to="/download" class="nav-link" @click="menuOpen = false">下载</router-link>
        
        <div class="nav-dropdown">
          <span class="nav-link dropdown-trigger">
            芯片数据库
            <svg width="12" height="12" viewBox="0 0 24 24" fill="currentColor">
              <path d="M7 10l5 5 5-5z"/>
            </svg>
          </span>
          <div class="dropdown-menu">
            <router-link to="/chips" @click="menuOpen = false">📱 高通 Qualcomm</router-link>
            <router-link to="/mtk" @click="menuOpen = false">⚡ MTK 联发科</router-link>
            <router-link to="/spd" @click="menuOpen = false">🔧 展锐 Spreadtrum</router-link>
          </div>
        </div>
        
        <router-link to="/api" class="nav-link" @click="menuOpen = false">API</router-link>
        <router-link to="/stats" class="nav-link" @click="menuOpen = false">统计</router-link>
        <a href="https://qm.qq.com/q/z3iVnkm22c" target="_blank" class="nav-link">QQ群</a>
      </nav>
      
      <!-- 右侧操作 -->
      <div class="nav-actions">
        <button class="theme-toggle" @click="toggleTheme" title="切换主题">
          <svg v-if="isDark" width="20" height="20" viewBox="0 0 24 24" fill="currentColor">
            <path d="M12 7c-2.76 0-5 2.24-5 5s2.24 5 5 5 5-2.24 5-5-2.24-5-5-5zM2 13h2c.55 0 1-.45 1-1s-.45-1-1-1H2c-.55 0-1 .45-1 1s.45 1 1 1zm18 0h2c.55 0 1-.45 1-1s-.45-1-1-1h-2c-.55 0-1 .45-1 1s.45 1 1 1zM11 2v2c0 .55.45 1 1 1s1-.45 1-1V2c0-.55-.45-1-1-1s-1 .45-1 1zm0 18v2c0 .55.45 1 1 1s1-.45 1-1v-2c0-.55-.45-1-1-1s-1 .45-1 1zM5.99 4.58c-.39-.39-1.03-.39-1.41 0-.39.39-.39 1.03 0 1.41l1.06 1.06c.39.39 1.03.39 1.41 0s.39-1.03 0-1.41L5.99 4.58zm12.37 12.37c-.39-.39-1.03-.39-1.41 0-.39.39-.39 1.03 0 1.41l1.06 1.06c.39.39 1.03.39 1.41 0 .39-.39.39-1.03 0-1.41l-1.06-1.06zm1.06-10.96c.39-.39.39-1.03 0-1.41-.39-.39-1.03-.39-1.41 0l-1.06 1.06c-.39.39-.39 1.03 0 1.41s1.03.39 1.41 0l1.06-1.06zM7.05 18.36c.39-.39.39-1.03 0-1.41-.39-.39-1.03-.39-1.41 0l-1.06 1.06c-.39.39-.39 1.03 0 1.41s1.03.39 1.41 0l1.06-1.06z"/>
          </svg>
          <svg v-else width="20" height="20" viewBox="0 0 24 24" fill="currentColor">
            <path d="M12 3c-4.97 0-9 4.03-9 9s4.03 9 9 9 9-4.03 9-9c0-.46-.04-.92-.1-1.36-.98 1.37-2.58 2.26-4.4 2.26-2.98 0-5.4-2.42-5.4-5.4 0-1.81.89-3.42 2.26-4.4-.44-.06-.9-.1-1.36-.1z"/>
          </svg>
        </button>
        
        <a href="https://github.com/xiriovo/SakuraEDL" target="_blank" class="github-link" title="GitHub">
          <svg width="20" height="20" viewBox="0 0 24 24" fill="currentColor">
            <path d="M12 0c-6.626 0-12 5.373-12 12 0 5.302 3.438 9.8 8.207 11.387.599.111.793-.261.793-.577v-2.234c-3.338.726-4.033-1.416-4.033-1.416-.546-1.387-1.333-1.756-1.333-1.756-1.089-.745.083-.729.083-.729 1.205.084 1.839 1.237 1.839 1.237 1.07 1.834 2.807 1.304 3.492.997.107-.775.418-1.305.762-1.604-2.665-.305-5.467-1.334-5.467-5.931 0-1.311.469-2.381 1.236-3.221-.124-.303-.535-1.524.117-3.176 0 0 1.008-.322 3.301 1.23.957-.266 1.983-.399 3.003-.404 1.02.005 2.047.138 3.006.404 2.291-1.552 3.297-1.23 3.297-1.23.653 1.653.242 2.874.118 3.176.77.84 1.235 1.911 1.235 3.221 0 4.609-2.807 5.624-5.479 5.921.43.372.823 1.102.823 2.222v3.293c0 .319.192.694.801.576 4.765-1.589 8.199-6.086 8.199-11.386 0-6.627-5.373-12-12-12z"/>
          </svg>
        </a>
        
        <button class="menu-toggle" @click="menuOpen = !menuOpen">
          <svg v-if="!menuOpen" width="24" height="24" viewBox="0 0 24 24" fill="currentColor">
            <path d="M3 18h18v-2H3v2zm0-5h18v-2H3v2zm0-7v2h18V6H3z"/>
          </svg>
          <svg v-else width="24" height="24" viewBox="0 0 24 24" fill="currentColor">
            <path d="M19 6.41L17.59 5 12 10.59 6.41 5 5 6.41 10.59 12 5 17.59 6.41 19 12 13.41 17.59 19 19 17.59 13.41 12z"/>
          </svg>
        </button>
      </div>
    </div>
  </header>
</template>

<script setup>
import { ref, onMounted, onUnmounted } from 'vue'

const isScrolled = ref(false)
const menuOpen = ref(false)
const isDark = ref(false)

const handleScroll = () => {
  isScrolled.value = window.scrollY > 0
}

const toggleTheme = () => {
  isDark.value = !isDark.value
  document.documentElement.classList.toggle('dark', isDark.value)
  localStorage.setItem('theme', isDark.value ? 'dark' : 'light')
}

onMounted(() => {
  window.addEventListener('scroll', handleScroll)
  isDark.value = localStorage.getItem('theme') === 'dark' || 
    (!localStorage.getItem('theme') && window.matchMedia('(prefers-color-scheme: dark)').matches)
  document.documentElement.classList.toggle('dark', isDark.value)
})

onUnmounted(() => {
  window.removeEventListener('scroll', handleScroll)
})
</script>

<style lang="scss" scoped>
.vp-nav {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  height: var(--vp-nav-height);
  background: var(--vp-c-bg);
  border-bottom: 1px solid transparent;
  z-index: 100;
  transition: all 0.3s;
  
  &.scrolled {
    border-color: var(--vp-c-divider);
    background: rgba(255, 255, 255, 0.9);
    backdrop-filter: blur(12px);
    
    .dark & {
      background: rgba(27, 27, 31, 0.9);
    }
  }
}

.nav-container {
  max-width: 1400px;
  height: 100%;
  margin: 0 auto;
  padding: 0 24px;
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.nav-logo {
  display: flex;
  align-items: center;
  gap: 10px;
  font-size: 18px;
  font-weight: 700;
  color: var(--vp-c-text-1);
  transition: all 0.3s;
  
  img {
    width: 32px;
    height: 32px;
    transition: transform 0.3s;
  }
  
  span {
    background: linear-gradient(135deg, #ec4899, #8b5cf6);
    -webkit-background-clip: text;
    -webkit-text-fill-color: transparent;
    background-clip: text;
  }
  
  &:hover {
    img {
      transform: rotate(-10deg) scale(1.1);
    }
  }
}

.nav-menu {
  display: flex;
  align-items: center;
  gap: 4px;
  
  @media (max-width: 768px) {
    display: none;
    position: fixed;
    top: var(--vp-nav-height);
    left: 0;
    right: 0;
    bottom: 0;
    background: var(--vp-c-bg);
    flex-direction: column;
    padding: 24px;
    gap: 8px;
    
    &.open {
      display: flex;
    }
  }
}

.nav-link {
  padding: 8px 14px;
  color: var(--vp-c-text-2);
  font-size: 14px;
  font-weight: 500;
  border-radius: 8px;
  transition: all 0.3s;
  position: relative;
  
  &::after {
    content: '';
    position: absolute;
    bottom: 4px;
    left: 50%;
    width: 0;
    height: 2px;
    background: linear-gradient(90deg, #ec4899, #8b5cf6);
    transition: all 0.3s;
    transform: translateX(-50%);
    border-radius: 1px;
  }
  
  &:hover {
    color: #ec4899;
    
    &::after {
      width: calc(100% - 28px);
    }
  }
  
  &.router-link-active {
    color: #ec4899;
    background: rgba(236, 72, 153, 0.08);
    
    &::after {
      width: calc(100% - 28px);
    }
  }
  
  @media (max-width: 768px) {
    width: 100%;
    padding: 12px 16px;
    
    &::after {
      display: none;
    }
  }
}

.nav-dropdown {
  position: relative;
  
  .dropdown-trigger {
    display: flex;
    align-items: center;
    gap: 4px;
    cursor: pointer;
  }
  
  .dropdown-menu {
    display: none;
    position: absolute;
    top: 100%;
    left: 0;
    min-width: 160px;
    background: var(--vp-c-bg);
    border: 1px solid var(--vp-c-divider);
    border-radius: 12px;
    padding: 8px;
    box-shadow: var(--vp-shadow-3);
    
    a {
      display: block;
      padding: 8px 12px;
      color: var(--vp-c-text-2);
      border-radius: 6px;
      
      &:hover {
        background: var(--vp-c-bg-soft);
        color: var(--vp-c-brand);
      }
    }
  }
  
  &:hover .dropdown-menu {
    display: block;
  }
  
  @media (max-width: 768px) {
    width: 100%;
    
    .dropdown-menu {
      position: static;
      border: none;
      box-shadow: none;
      padding-left: 16px;
    }
  }
}

.nav-actions {
  display: flex;
  align-items: center;
  gap: 8px;
}

.theme-toggle, .github-link {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 36px;
  height: 36px;
  border-radius: 8px;
  background: none;
  border: none;
  color: var(--vp-c-text-2);
  cursor: pointer;
  transition: all 0.2s;
  
  &:hover {
    background: var(--vp-c-bg-soft);
    color: var(--vp-c-brand);
  }
}

.menu-toggle {
  display: none;
  align-items: center;
  justify-content: center;
  width: 36px;
  height: 36px;
  border-radius: 8px;
  background: none;
  border: none;
  color: var(--vp-c-text-2);
  cursor: pointer;
  
  @media (max-width: 768px) {
    display: flex;
  }
}
</style>
