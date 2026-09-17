<script setup lang="ts">
import { onErrorCaptured, ref } from 'vue'
import { RouterView } from 'vue-router'

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { messages } from '@/utils/messages'

/** 错误边界降级标记：子树抛错时展示兜底 UI，避免白屏（规范 3.5）。 */
const crashed = ref(false)

onErrorCaptured((error: unknown): boolean => {
  console.error('[错误边界]', error)
  crashed.value = true
  return false
})
</script>

<template>
  <div
    v-if="crashed"
    class="flex min-h-screen items-center justify-center p-6"
    data-testid="app-error-boundary"
  >
    <Alert
      variant="destructive"
      class="max-w-md"
    >
      <AlertTitle>{{ messages.common.loadFailedTitle }}</AlertTitle>
      <AlertDescription>{{ messages.common.systemBusy }}</AlertDescription>
    </Alert>
  </div>

  <RouterView v-else />
</template>
