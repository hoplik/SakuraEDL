<template>
  <div>
    <div class="page-header">
      <div>
        <h1 class="page-title">系统设置</h1>
        <p class="page-subtitle">配置管理后台相关选项</p>
      </div>
    </div>

    <div style="display: grid; grid-template-columns: 1fr 1fr; gap: 24px;">
      <div class="card">
        <div class="card-header">
          <span class="card-title"><el-icon><InfoFilled /></el-icon> 系统信息</span>
        </div>
        <div class="card-body">
          <div style="display: flex; flex-direction: column; gap: 16px;">
            <div style="display: flex; justify-content: space-between; padding: 12px; background: #f8fafc; border-radius: 8px;">
              <span style="color: #64748b;">版本</span>
              <span style="font-weight: 600; color: #1e293b;">SakuraEDL Admin v3.0</span>
            </div>
            <div style="display: flex; justify-content: space-between; padding: 12px; background: #f8fafc; border-radius: 8px;">
              <span style="color: #64748b;">后端地址</span>
              <span style="font-family: monospace; color: #667eea;">{{ apiBase }}</span>
            </div>
            <div style="display: flex; justify-content: space-between; padding: 12px; background: #f8fafc; border-radius: 8px;">
              <span style="color: #64748b;">存储类型</span>
              <span style="font-weight: 600; color: #1e293b;">MySQL</span>
            </div>
            <div style="display: flex; justify-content: space-between; padding: 12px; background: #f8fafc; border-radius: 8px;">
              <span style="color: #64748b;">当前用户</span>
              <span style="font-weight: 600; color: #1e293b;">{{ username }}</span>
            </div>
          </div>
        </div>
      </div>

      <div class="card">
        <div class="card-header">
          <span class="card-title"><el-icon><Link /></el-icon> API 端点</span>
        </div>
        <div class="card-body">
          <div style="display: flex; flex-direction: column; gap: 12px; font-size: 13px;">
            <div style="display: flex; gap: 12px; align-items: center;">
              <el-tag size="small" type="success">GET</el-tag>
              <code style="flex: 1; color: #64748b;">/api/loaders/list</code>
              <span style="color: #94a3b8;">获取 Loader 列表</span>
            </div>
            <div style="display: flex; gap: 12px; align-items: center;">
              <el-tag size="small" type="success">GET</el-tag>
              <code style="flex: 1; color: #64748b;">/api/loaders/{id}/download</code>
              <span style="color: #94a3b8;">下载 Loader</span>
            </div>
            <div style="display: flex; gap: 12px; align-items: center;">
              <el-tag size="small" type="success">GET</el-tag>
              <code style="flex: 1; color: #64748b;">/api/loaders/{id}/digest</code>
              <span style="color: #94a3b8;">下载 Digest</span>
            </div>
            <div style="display: flex; gap: 12px; align-items: center;">
              <el-tag size="small" type="success">GET</el-tag>
              <code style="flex: 1; color: #64748b;">/api/loaders/{id}/sign</code>
              <span style="color: #94a3b8;">下载 Sign</span>
            </div>
            <div style="display: flex; gap: 12px; align-items: center;">
              <el-tag size="small" type="warning">POST</el-tag>
              <code style="flex: 1; color: #64748b;">/api/loaders/match</code>
              <span style="color: #94a3b8;">匹配 Loader</span>
            </div>
          </div>
        </div>
      </div>
    </div>

    <div class="card" style="margin-top: 24px;">
      <div class="card-header">
        <span class="card-title"><el-icon><Warning /></el-icon> 危险操作</span>
      </div>
      <div class="card-body">
        <div style="display: flex; align-items: center; justify-content: space-between; padding: 16px; background: #fef2f2; border-radius: 8px; border: 1px solid #fecaca;">
          <div>
            <h4 style="color: #dc2626; margin-bottom: 4px;">清空所有日志</h4>
            <p style="color: #f87171; font-size: 13px;">此操作将删除所有设备匹配日志，无法恢复</p>
          </div>
          <el-button type="danger" plain disabled>
            <el-icon><Delete /></el-icon> 清空日志
          </el-button>
        </div>
      </div>
    </div>

    <div class="card" style="margin-top: 24px;">
      <div class="card-header">
        <span class="card-title"><el-icon><QuestionFilled /></el-icon> 帮助与支持</span>
      </div>
      <div class="card-body">
        <div style="display: grid; grid-template-columns: repeat(3, 1fr); gap: 16px;">
          <a href="https://github.com/xiriovo/SakuraEDL" target="_blank" style="text-decoration: none;">
            <div style="padding: 20px; background: #f8fafc; border-radius: 12px; text-align: center; cursor: pointer; transition: all 0.3s;" class="help-item">
              <el-icon style="font-size: 32px; color: #667eea; margin-bottom: 12px;"><Link /></el-icon>
              <h4 style="color: #1e293b; margin-bottom: 4px;">GitHub</h4>
              <p style="color: #64748b; font-size: 12px;">查看源代码</p>
            </div>
          </a>
          <a href="https://sakuraedl.org" target="_blank" style="text-decoration: none;">
            <div style="padding: 20px; background: #f8fafc; border-radius: 12px; text-align: center; cursor: pointer; transition: all 0.3s;" class="help-item">
              <el-icon style="font-size: 32px; color: #10b981; margin-bottom: 12px;"><Document /></el-icon>
              <h4 style="color: #1e293b; margin-bottom: 4px;">文档</h4>
              <p style="color: #64748b; font-size: 12px;">使用指南</p>
            </div>
          </a>
          <a href="https://qm.qq.com/q/z3iVnkm22c" target="_blank" style="text-decoration: none;">
            <div style="padding: 20px; background: #f8fafc; border-radius: 12px; text-align: center; cursor: pointer; transition: all 0.3s;" class="help-item">
              <el-icon style="font-size: 32px; color: #f59e0b; margin-bottom: 12px;"><ChatDotRound /></el-icon>
              <h4 style="color: #1e293b; margin-bottom: 4px;">QQ 群</h4>
              <p style="color: #64748b; font-size: 12px;">加入交流群</p>
            </div>
          </a>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { computed } from 'vue'
import { useAuthStore } from '@/stores/auth'
import {
  InfoFilled, Link, Warning, Delete,
  QuestionFilled, Document, ChatDotRound
} from '@element-plus/icons-vue'

const authStore = useAuthStore()
const username = computed(() => authStore.username || 'admin')
const apiBase = computed(() => window.location.origin + '/api')
</script>

<style scoped>
.help-item:hover {
  background: #f1f5f9 !important;
  transform: translateY(-2px);
}
</style>
