<script setup lang="ts">
import { RouterLink, useRouter } from 'vue-router'

import { Button } from '@/components/ui/button'
import { useAuthStore } from '@/stores/auth'
import { messages } from '@/utils/messages'

const authStore = useAuthStore()
const router = useRouter()

/** 登出：清空本地登录态后回到登录页（服务端 refresh 会话一并注销）。 */
async function handleLogout(): Promise<void> {
  await authStore.logout()
  await router.replace('/login')
}
</script>

<template>
  <header class="border-b border-border bg-card">
    <div class="mx-auto flex h-14 w-full max-w-5xl items-center justify-between gap-4 px-6">
      <RouterLink
        to="/draw"
        class="text-base font-semibold tracking-tight"
      >
        {{ messages.app.brand }}
      </RouterLink>

      <nav class="flex items-center gap-4 text-sm">
        <RouterLink
          to="/draw"
          class="text-muted-foreground hover:text-foreground"
        >
          {{ messages.draw.title }}
        </RouterLink>
        <RouterLink
          to="/records"
          class="text-muted-foreground hover:text-foreground"
        >
          {{ messages.records.title }}
        </RouterLink>
      </nav>

      <div class="flex items-center gap-3 text-sm">
        <span
          v-if="authStore.user"
          class="text-muted-foreground"
          data-testid="appbar-username"
        >
          {{ authStore.user.userName }}
        </span>
        <Button
          variant="ghost"
          size="sm"
          type="button"
          data-testid="appbar-logout"
          @click="handleLogout"
        >
          {{ messages.common.logout }}
        </Button>
      </div>
    </div>
  </header>
</template>
