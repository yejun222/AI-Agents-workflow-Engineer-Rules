<script setup lang="ts">
import { computed } from 'vue'

import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle
} from '@/components/ui/dialog'
import { messages } from '@/utils/messages'

const props = defineProps<{
  open: boolean
  isWin: boolean
  prizeName: string
  /** 剩余次数；`null` = 未知（§2.6 REV-01：未知不得当作 0，以占位「—」呈现）。 */
  remaining: number | null
}>()

const emit = defineEmits<{
  (event: 'update:open', value: boolean): void
  (event: 'continue'): void
  (event: 'viewRecords'): void
}>()

const title = computed<string>(() =>
  props.isWin ? `${messages.draw.winTitle} ${props.prizeName}` : messages.draw.loseTitle
)

const description = computed<string>(() =>
  props.isWin ? messages.draw.winDesc : messages.draw.loseDesc
)

/** 剩余次数展示值：未知（查询失败且未获抽奖响应回写）时用占位符，不显示 0。 */
const remainingText = computed<string>(() =>
  props.remaining === null ? messages.draw.quotaUnknown : String(props.remaining)
)
</script>

<template>
  <Dialog
    :open="open"
    @update:open="(value: boolean) => emit('update:open', value)"
  >
    <DialogContent :data-testid="isWin ? 'page-draw--win' : 'page-draw--lose'">
      <DialogHeader>
        <DialogTitle :data-testid="isWin ? 'draw-win-title' : 'draw-lose-title'">
          {{ title }}
        </DialogTitle>
        <DialogDescription>{{ description }}</DialogDescription>
      </DialogHeader>

      <p class="text-sm text-muted-foreground">
        {{ messages.draw.quotaLabel }}{{ remainingText }} {{ messages.draw.quotaUnit }}
      </p>

      <DialogFooter>
        <Button
          variant="outline"
          type="button"
          @click="emit('viewRecords')"
        >
          {{ messages.draw.viewRecords }}
        </Button>
        <Button
          type="button"
          @click="emit('continue')"
        >
          {{ messages.draw.continueDraw }}
        </Button>
      </DialogFooter>
    </DialogContent>
  </Dialog>
</template>
