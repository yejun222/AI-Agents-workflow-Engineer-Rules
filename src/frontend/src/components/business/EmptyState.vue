<script setup lang="ts">
import { Button } from '@/components/ui/button'

defineProps<{
  title: string
  description?: string
  actionLabel?: string
}>()

const emit = defineEmits<{
  (event: 'action'): void
}>()
</script>

<template>
  <div class="flex flex-col items-center gap-4 py-8 text-center">
    <!-- 内联 SVG 几何插画（原型空态同款；装饰性图形不进可访问性树） -->
    <svg
      viewBox="0 0 120 80"
      class="size-24"
      aria-hidden="true"
    >
      <rect
        x="14"
        y="18"
        width="92"
        height="48"
        rx="8"
        class="fill-muted stroke-border"
        stroke-width="1"
      />
      <circle
        cx="60"
        cy="42"
        r="14"
        class="fill-card stroke-border"
        stroke-width="1"
      />
      <path
        d="M60 30 L60 42 L69 46"
        class="stroke-muted-foreground"
        stroke-width="2"
        fill="none"
        stroke-linecap="round"
      />
    </svg>

    <div class="space-y-1">
      <p class="text-base font-medium">
        {{ title }}
      </p>
      <p
        v-if="description"
        class="text-sm text-muted-foreground"
      >
        {{ description }}
      </p>
    </div>

    <Button
      v-if="actionLabel"
      type="button"
      @click="emit('action')"
    >
      {{ actionLabel }}
    </Button>
  </div>
</template>
