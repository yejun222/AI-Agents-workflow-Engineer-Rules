<script setup lang="ts">
import { Badge } from '@/components/ui/badge'
import { PRIZE_TYPE, type PrizePoolItemDto } from '@/types/prize'
import { messages } from '@/utils/messages'

defineProps<{
  items: PrizePoolItemDto[]
}>()

/** 条目类型文案（实物 / 虚拟 / 未中奖）。 */
function typeLabel(type: number): string {
  if (type === PRIZE_TYPE.physical) {
    return messages.prizeType.physical
  }

  if (type === PRIZE_TYPE.virtual) {
    return messages.prizeType.virtual
  }

  return messages.prizeType.noPrize
}
</script>

<template>
  <section class="space-y-3">
    <h2 class="text-sm font-medium">
      {{ messages.draw.legendTitle }}
    </h2>
    <ul class="space-y-2">
      <li
        v-for="item in items"
        :key="item.id"
        class="flex items-center gap-3 text-sm"
        :data-testid="`draw-legend-${item.displayOrder}`"
      >
        <span class="w-5 text-center text-xs text-muted-foreground">{{ item.displayOrder }}</span>
        <span class="min-w-0 flex-1 truncate">{{ item.name }}</span>
        <Badge variant="outline">
          {{ typeLabel(item.type) }}
        </Badge>
      </li>
    </ul>
  </section>
</template>
