import { createRouter, createWebHistory, type RouteRecordRaw } from 'vue-router'

import { useAuthStore } from '@/stores/auth'

declare module 'vue-router' {
  interface RouteMeta {
    /** 公开页面：无需登录即可访问（注册 / 登录）。 */
    public?: boolean
  }
}

/** 路由表（docs/30-architecture.md §2.6）：页面组件全部懒加载。 */
const routes: RouteRecordRaw[] = [
  { path: '/', redirect: '/draw' },
  {
    path: '/register',
    name: 'register',
    component: () => import('@/views/auth/RegisterView.vue'),
    meta: { public: true }
  },
  {
    path: '/login',
    name: 'login',
    component: () => import('@/views/auth/LoginView.vue'),
    meta: { public: true }
  },
  {
    path: '/draw',
    name: 'draw',
    component: () => import('@/views/draw/DrawView.vue')
  },
  {
    path: '/records',
    name: 'records',
    component: () => import('@/views/records/WinningRecordsView.vue')
  },
  { path: '/:pathMatch(.*)*', redirect: '/' }
]

const router = createRouter({
  history: createWebHistory(),
  routes
})

/**
 * 全局守卫：
 * - 公开页（`meta.public`）：**一律不触发刷新**（§2.8 第 2 条），直接放行；
 * - 受保护页：内存态为空时发起一次静默恢复（与 401 无感刷新共用同一在途 Promise），
 *   恢复判定完成前不放行目标组件（不得先闪登录页再回跳）；失败 → `/login?redirect=<目标路径>`。
 * `?redirect=` 同时是登录页「请先登录」Alert 的唯一触发条件（§2.6）。
 */
router.beforeEach(async (to) => {
  if (to.meta.public === true) {
    return true
  }

  const authStore = useAuthStore()
  await authStore.ensureSession({ allowRefresh: true })

  if (authStore.isAuthenticated) {
    return true
  }

  return { path: '/login', query: { redirect: to.fullPath } }
})

export default router
