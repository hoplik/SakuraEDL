<template>
  <aside class="vp-sidebar">
    <div class="sidebar-content">
      <div v-for="group in items" :key="group.text" class="sidebar-group">
        <p class="group-title">{{ group.text }}</p>
        <ul class="group-items">
          <li v-for="item in group.items" :key="item.link">
            <router-link :to="item.link" class="sidebar-link" :class="{ active: isActive(item.link) }">
              {{ item.text }}
            </router-link>
          </li>
        </ul>
      </div>
    </div>
  </aside>
</template>

<script setup>
import { useRoute } from 'vue-router'

defineProps({
  items: {
    type: Array,
    required: true
  }
})

const route = useRoute()

const isActive = (link) => {
  return route.path === link || route.path.startsWith(link + '/')
}
</script>

<style lang="scss" scoped>
.vp-sidebar {
  position: fixed;
  top: var(--vp-nav-height);
  left: 0;
  bottom: 0;
  width: var(--vp-sidebar-width);
  background: var(--vp-c-bg);
  border-right: 1px solid var(--vp-c-divider);
  overflow-y: auto;
  padding: 24px;
  
  @media (max-width: 960px) {
    display: none;
  }
}

.sidebar-group {
  margin-bottom: 24px;
}

.group-title {
  font-size: 13px;
  font-weight: 700;
  color: var(--vp-c-text-1);
  text-transform: uppercase;
  letter-spacing: 0.4px;
  margin: 0 0 8px;
  padding: 0 8px;
}

.group-items {
  list-style: none;
  margin: 0;
  padding: 0;
}

.sidebar-link {
  display: block;
  padding: 6px 12px;
  font-size: 14px;
  color: var(--vp-c-text-2);
  border-radius: 6px;
  transition: all 0.2s;
  
  &:hover {
    color: var(--vp-c-brand);
    background: var(--vp-c-brand-dimm);
  }
  
  &.active {
    color: var(--vp-c-brand);
    font-weight: 500;
    background: var(--vp-c-brand-dimm);
  }
}
</style>
