<template>
  <footer class="vp-footer">
    <div class="footer-container">
      <p class="footer-message">{{ t('footer.license') }}</p>
      <p class="footer-copyright">{{ t('footer.copyright') }}</p>
    </div>
  </footer>
</template>

<script setup>
import { useI18n } from '@/i18n'
const { t } = useI18n()
</script>

<style lang="scss" scoped>
.vp-footer {
  border-top: 1px solid var(--vp-c-divider);
  padding: 24px;
  text-align: center;
}

.footer-message {
  font-size: 14px;
  color: var(--vp-c-text-2);
  margin: 0 0 8px;
}

.footer-copyright {
  font-size: 13px;
  color: var(--vp-c-text-3);
  margin: 0;
}
</style>
