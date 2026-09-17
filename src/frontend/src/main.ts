import { createPinia } from 'pinia'
import { createApp } from 'vue'

import App from '@/App.vue'
import router from '@/router'
import { useAuthStore } from '@/stores/auth'
import { setUnauthorizedHandler } from '@/utils/auth-token'

import '@/assets/main.css'

const app = createApp(App)
const pinia = createPinia()

app.use(pinia)
app.use(router)

// 全局错误边界：未捕获异常必须落日志并降级，禁止白屏（规范 3.5）
app.config.errorHandler = (error: unknown, _instance: unknown, info: string): void => {
  console.error('[全局异常]', info, error)
}

// 刷新令牌彻底失效时由 request 层回调：清理登录态并带 redirect 回登录页
const authStore = useAuthStore(pinia)
setUnauthorizedHandler((): void => {
  const currentPath = router.currentRoute.value.fullPath
  authStore.clearSession()
  void router.replace({ path: '/login', query: { redirect: currentPath } })
})

void router.isReady().then((): void => {
  app.mount('#app')
})
