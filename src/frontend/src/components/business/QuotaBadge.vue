<script setup lang="ts">
import { computed } from 'vue'

import { Badge } from '@/components/ui/badge'
import { messages } from '@/utils/messages'

const props = defineProps<{
  /** 后端确认的剩余次数；`null` = 未知（首载失败），必须占位呈现，禁止渲染为 0（REV-01）。 */
  remaining: number | null
  /** 每日上限；`null` = 未知（上限徽标隐藏）。 */
  dailyLimit: number | null
}>()

/** 仅「后端确认 remaining === 0」才换 destructive 语义（§2.6 page-draw--noquota）。 */
const isExhausted = computed<boolean>(() => props.remaining === 0)

const remainingText = computed<string>(() =>
  props.remaining === null ? messages.draw.quotaUnknown : String(props.remaining)
)

const dailyLimitText = computed<string>(() =>
  props.dailyLimit === null ? '' : messages.draw.dailyLimit(props.dailyLimit)
)
</script>

<template>
  <div
    class="flex items-center gap-2 text-sm"
    data-testid="draw-quota"
  >
    <span class="text-muted-foreground">{{ messages.draw.quotaLabel }}</span>
    <Badge :variant="isExhausted ? 'destructive' : 'default'">
      {{ remainingText }} {{ messages.draw.quotaUnit }}
    </Badge>
    <span
      v-if="dailyLimitText.length > 0"
      class="text-xs text-muted-foreground"
    >
      {{ dailyLimitText }}
    </span>
  </div>
</template>
