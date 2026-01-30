<template>
  <div class="not-found">
    <h1>404</h1>
    <p>页面不存在</p>
    <router-link to="/" class="vp-button primary">返回首页</router-link>
  </div>
</template>

<style lang="scss" scoped>
.not-found {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  min-height: 60vh;
  text-align: center;
  
  h1 {
    font-size: 6rem;
    color: var(--vp-c-brand);
    margin: 0;
  }
  
  p {
    font-size: 1.25rem;
    color: var(--vp-c-text-2);
    margin: 16px 0 32px;
  }
}
</style>
