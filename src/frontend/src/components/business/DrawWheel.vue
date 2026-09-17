<script setup lang="ts">
import { computed, onBeforeUnmount, ref, watch } from 'vue'

import { PRIZE_TYPE, type PrizePoolItemDto } from '@/types/prize'
import { messages } from '@/utils/messages'

const props = withDefaults(
  defineProps<{
    items: PrizePoolItemDto[]
    /** 是否处于「抽奖中」（请求 + 动画期间）。 */
    spinning: boolean
    /** 后端返回条目的扇区下标；-1 表示结果未到（仅保持旋转）。 */
    settleIndex: number
    /** 停稳后需要高亮的扇区下标；-1 表示不高亮。 */
    highlightIndex?: number
    /** 次数用尽时置灰降饱和。 */
    muted?: boolean
    /** 中心圆盘第二行文案（如「剩余 3 次」）。 */
    centerText?: string
  }>(),
  { highlightIndex: -1, muted: false, centerText: '' }
)

const emit = defineEmits<{
  (event: 'settled'): void
}>()

/** 扇区底色令牌：必须是字面类名，Tailwind 才能扫描生成（禁止运行时拼串）。 */
const SECTOR_FILLS = ['fill-wheel-1', 'fill-wheel-2', 'fill-wheel-3', 'fill-wheel-4', 'fill-wheel-5'] as const

const CENTER = 100
const RADIUS = 88
const LABEL_RADIUS = 60
const INDEX_RADIUS = 36
/**
 * 停稳动画时长（ms，D-14：2400ms cubic-bezier(.16,.84,.24,1)）：由转子内联过渡样式消费（`rotorStyle`），
 * 兜底定时器略长于动画，仅在 `transitionend` 丢失（标签页节流 / 过渡未触发）时兜底。
 */
const SETTLE_DURATION_MS = 2400
const SETTLE_FALLBACK_MS = 2600
/** D-14 缓动曲线（与时长一起构成唯一事实来源，避免模板里再复制一遍数值）。 */
const SETTLE_EASING = 'cubic-bezier(.16,.84,.24,1)'

const rotor = ref<SVGGElement | null>(null)
const rotation = ref(0)
let settleTimer: number | null = null
/** 是否处于「等待停稳信号」窗口：窗口外的 transitionend（起转过渡）一律忽略。 */
let settlePending = false

/**
 * 转子样式：时长与缓动取自 D-14 常量（唯一事实来源）。
 * `motion-reduce:transition-none` 只置 `transition-property: none`，内联的时长/缓动不会让它复活，
 * 因此减动效下仍为「直接落终值」（§2.6 #15）。
 */
const rotorStyle = computed<Record<string, string>>(() => ({
  transform: `rotate(${rotation.value}deg)`,
  transformOrigin: '100px 100px',
  transitionDuration: `${SETTLE_DURATION_MS}ms`,
  transitionTimingFunction: SETTLE_EASING
}))

const sectorAngle = computed<number>(() => (props.items.length === 0 ? 360 : 360 / props.items.length))

interface SectorShape {
  index: number
  number: string
  path: string
  shortName: string
  labelX: number
  labelY: number
  labelRotation: number
  indexX: number
  indexY: number
  fillClass: string
}

function polar(angle: number, radius: number): { x: number; y: number } {
  const radians = (angle * Math.PI) / 180
  return {
    x: Number((CENTER + radius * Math.sin(radians)).toFixed(2)),
    y: Number((CENTER - radius * Math.cos(radians)).toFixed(2))
  }
}

const sectors = computed<SectorShape[]>(() => {
  const angle = sectorAngle.value

  return props.items.map((item, index) => {
    const mid = index * angle + angle / 2
    const start = polar(index * angle, RADIUS)
    const end = polar((index + 1) * angle, RADIUS)
    const label = polar(mid, LABEL_RADIUS)
    const indexPoint = polar(mid, INDEX_RADIUS)
    const highlighted = props.highlightIndex === index
    const isNoPrize = item.type === PRIZE_TYPE.noPrize

    let fillClass: string = SECTOR_FILLS[index % SECTOR_FILLS.length] ?? SECTOR_FILLS[0]
    if (highlighted) {
      // 中奖扇区高亮 fill-wheel-win，未中奖落点 fill-wheel-lose（§2.7 令牌）
      fillClass = isNoPrize ? 'fill-wheel-lose' : 'fill-wheel-win'
    }

    return {
      index,
      number: String(index + 1),
      path: [
        `M ${CENTER} ${CENTER}`,
        `L ${start.x} ${start.y}`,
        `A ${RADIUS} ${RADIUS} 0 ${angle > 180 ? 1 : 0} 1 ${end.x} ${end.y}`,
        'Z'
      ].join(' '),
      shortName: item.shortName,
      labelX: label.x,
      labelY: label.y,
      // 文字沿径向排布
      labelRotation: Number((mid - 90).toFixed(2)),
      indexX: indexPoint.x,
      indexY: indexPoint.y,
      fillClass
    }
  })
})

function prefersReducedMotion(): boolean {
  return (
    typeof window !== 'undefined' &&
    typeof window.matchMedia === 'function' &&
    window.matchMedia('(prefers-reduced-motion: reduce)').matches
  )
}

function clearSettleTimer(): void {
  if (settleTimer !== null) {
    window.clearTimeout(settleTimer)
    settleTimer = null
  }
}

/** 停稳终态：仅在等待窗口内生效（幂等，transitionend 与兜底定时器先到者胜）。 */
function markSettled(): void {
  if (!settlePending) {
    return
  }

  settlePending = false
  clearSettleTimer()
  emit('settled')
}

/** 停稳信号主通道：转子的 transform 过渡结束（D-14 以 transitionend 为准，避免与计时器双时间轴漂移）。 */
function handleTransitionEnd(event: TransitionEvent): void {
  if (event.propertyName !== 'transform' || event.target !== rotor.value) {
    return
  }

  markSettled()
}

// 起转：先转若干圈等待后端结果，结果到达后再精确停稳
watch(
  () => props.spinning,
  (spinning) => {
    if (!spinning) {
      return
    }

    clearSettleTimer()
    rotation.value += 360 * 3
  }
)

// 停稳：补足 2 圈后停在该扇区中心，指针（12 点）指向结果扇区
watch(
  () => props.settleIndex,
  (index) => {
    if (index < 0) {
      return
    }

    clearSettleTimer()
    settlePending = false

    const angle = sectorAngle.value
    const mid = index * angle + angle / 2
    const targetOffset = ((360 - mid) % 360 + 360) % 360
    const currentOffset = ((rotation.value % 360) + 360) % 360
    rotation.value += 360 * 2 + ((targetOffset - currentOffset + 360) % 360)

    settlePending = true

    if (prefersReducedMotion()) {
      // 降级：无过渡（motion-reduce:transition-none）即无 transitionend，直接落终值并立即通知停稳
      settleTimer = window.setTimeout(() => {
        settleTimer = null
        markSettled()
      }, 0)
      return
    }

    // 主信号为 transitionend；兜底定时器只在事件丢失时生效（时长略长于 SETTLE_DURATION_MS）
    settleTimer = window.setTimeout(() => {
      settleTimer = null
      markSettled()
    }, SETTLE_FALLBACK_MS)
  }
)

onBeforeUnmount(() => {
  clearSettleTimer()
  settlePending = false
})
</script>

<template>
  <svg
    viewBox="0 0 200 200"
    role="img"
    :aria-label="messages.draw.wheelLabel"
    class="size-full"
    :class="{ 'opacity-60 saturate-0': muted }"
  >
    <g
      ref="rotor"
      class="transition-transform motion-reduce:transition-none"
      :style="rotorStyle"
      @transitionend="handleTransitionEnd"
    >
      <path
        v-for="sector in sectors"
        :key="sector.index"
        :d="sector.path"
        :class="[sector.fillClass, 'stroke-background']"
        stroke-width="1"
      />
      <text
        v-for="sector in sectors"
        :key="`label-${sector.index}`"
        :x="sector.labelX"
        :y="sector.labelY"
        :transform="`rotate(${sector.labelRotation} ${sector.labelX} ${sector.labelY})`"
        text-anchor="middle"
        dominant-baseline="middle"
        class="fill-foreground text-[9px] font-medium"
      >
        {{ sector.shortName }}
      </text>
      <text
        v-for="sector in sectors"
        :key="`number-${sector.index}`"
        :x="sector.indexX"
        :y="sector.indexY"
        text-anchor="middle"
        dominant-baseline="middle"
        class="fill-muted-foreground text-[10px]"
      >
        {{ sector.number }}
      </text>
    </g>

    <!-- 中心圆盘不随转盘旋转 -->
    <circle
      :cx="CENTER"
      :cy="CENTER"
      :r="30"
      class="fill-card stroke-border"
      stroke-width="1"
    />
    <text
      :x="CENTER"
      :y="CENTER - 5"
      text-anchor="middle"
      class="fill-foreground text-[10px] font-semibold"
    >
      {{ messages.app.brand }}
    </text>
    <text
      v-if="centerText.length > 0"
      :x="CENTER"
      :y="CENTER + 9"
      text-anchor="middle"
      class="fill-muted-foreground text-[9px]"
    >
      {{ centerText }}
    </text>

    <!-- 指针 -->
    <path
      d="M 100 2 L 93 20 L 107 20 Z"
      class="fill-foreground"
    />
  </svg>
</template>
