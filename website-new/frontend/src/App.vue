<template>
  <div class="vp-app">
    <VPNav />
    <main class="vp-main">
      <router-view />
    </main>
    <VPFooter />
  </div>
</template>

<script setup>
import VPNav from '@/components/VPNav.vue'
import VPFooter from '@/components/VPFooter.vue'
</script>
