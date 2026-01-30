import { createRouter, createWebHistory } from 'vue-router'

const routes = [
  {
    path: '/',
    name: 'Home',
    component: () => import('@/views/Home.vue')
  },
  {
    path: '/guide/:page?',
    name: 'Guide',
    component: () => import('@/views/Guide.vue')
  },
  {
    path: '/download',
    name: 'Download',
    component: () => import('@/views/Download.vue')
  },
  {
    path: '/api/:page?',
    name: 'Api',
    component: () => import('@/views/ApiDocs.vue')
  },
  {
    path: '/stats',
    name: 'Stats',
    component: () => import('@/views/Stats.vue')
  },
  {
    path: '/chips',
    name: 'Chips',
    component: () => import('@/views/Chips.vue')
  },
  {
    path: '/mtk',
    name: 'MTK',
    component: () => import('@/views/MTK.vue')
  },
  {
    path: '/spd',
    name: 'SPD',
    component: () => import('@/views/SPD.vue')
  },
  {
    path: '/:pathMatch(.*)*',
    name: 'NotFound',
    component: () => import('@/views/NotFound.vue')
  }
]

const router = createRouter({
  history: createWebHistory(),
  routes,
  scrollBehavior(to, from, savedPosition) {
    if (savedPosition) return savedPosition
    if (to.hash) return { el: to.hash, behavior: 'smooth' }
    return { top: 0 }
  }
})

export default router
