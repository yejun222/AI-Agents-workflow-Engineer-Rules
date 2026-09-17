<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { RouterLink, useRouter } from 'vue-router'

import DrawResultDialog from '@/components/business/DrawResultDialog.vue'
import DrawWheel from '@/components/business/DrawWheel.vue'
import EmptyState from '@/components/business/EmptyState.vue'
import PrizeLegend from '@/components/business/PrizeLegend.vue'
import QuotaBadge from '@/components/business/QuotaBadge.vue'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { useDrawFlow } from '@/composables/useDrawFlow'
import AppLayout from '@/layouts/AppLayout.vue'
import { formatUtc8DateTime } from '@/utils/datetime'
import { messages } from '@/utils/messages'

const router = useRouter()

const {
  poolState,
  poolError,
  prizeItems,
  remaining,
  dailyLimit,
  resetAt,
  phase,
  drawError,
  result,
  resultIndex,
  hasQuota,
  loadPool,
  reloadPoolForResult,
  loadQuota,
  startDraw,
  clearResult
} = useDrawFlow()

/** 转盘停稳标记：停稳后才展示结果弹层（FR-06：落点与后端返回条目严格一致）。 */
const settled = ref<boolean>(false)
const dialogOpen = ref<boolean>(false)

const isDrawing = computed<boolean>(() => phase.value === 'drawing')
const isWin = computed<boolean>(() => result.value?.isWin === true)
/** 结果未到（抽奖中）时保持旋转，收到结果后再精确停稳。 */
const settleIndex = computed<number>(() => (isDrawing.value ? -1 : resultIndex.value))
const highlightIndex = computed<number>(() =>
  settled.value && result.value !== null ? resultIndex.value : -1
)
const prizeName = computed<string>(() => {
  const current = result.value
  if (current === null) {
    return ''
  }

  // 重拉奖池后仍不可定位（条目被停用 / 重拉失败）时的兜底名称：不得渲染空名称（BUG-04）
  return prizeItems.value.find((item) => item.id === current.itemId)?.name ?? messages.draw.prizeUnknown
})

/** 转盘中心与徽标共用：次数未知时以占位「—」呈现，禁止渲染成 0（REV-01）。 */
const remainingText = computed<string>(() =>
  remaining.value === null ? messages.draw.quotaUnknown : String(remaining.value)
)

/** Should-S2：次数用尽时的精确重置时间引导（resetAt 未知时降级为无时间的固定文案）。 */
const quotaExhaustedDetail = computed<string>(() =>
  resetAt.value === null
    ? `${messages.draw.quotaLabel}0 ${messages.draw.quotaUnit}`
    : messages.draw.quotaResetHint(formatUtc8DateTime(resetAt.value, 'minute'))
)

/** 进入页面：拉取奖池与剩余次数（奖池失败可重试；次数失败不阻断抽奖）。 */
onMounted(async (): Promise<void> => {
  await Promise.all([loadPool(), loadQuota()])
})

/** 点击抽奖 / 继续抽奖：新一次尝试；若上一次请求失败未决，沿用同一幂等键（D-03）。 */
async function handleStartDraw(): Promise<void> {
  if (isDrawing.value || !hasQuota.value) {
    return
  }

  dialogOpen.value = false
  settled.value = false
  await startDraw()

  if (result.value !== null && resultIndex.value < 0) {
    // 结果条目不在当前奖池快照（页面加载后奖池发生启用 / 停用变更的竞态）：按 FR-05-R10
    // 重拉奖池一次再定位；定位成功则交回正常停稳路径（转盘停稳后弹层），不会卡在动画上
    await reloadPoolForResult()
  }

  if (result.value !== null && resultIndex.value < 0) {
    // 重拉后仍不可定位：跳过旋转直接展示结果，保证结果反馈不被渲染边界阻塞、不白屏
    settled.value = true
    dialogOpen.value = true
  }
}

/** 转盘停稳后弹出结果层。 */
function handleSettled(): void {
  settled.value = true
  if (result.value !== null) {
    dialogOpen.value = true
  }
}

/** 继续抽奖：关闭弹层并作废本次幂等键。 */
function handleContinue(): void {
  dialogOpen.value = false
  settled.value = false
  clearResult()
}

function handleViewRecords(): void {
  void router.push('/records')
}
</script>

<template>
  <AppLayout>
    <!-- 加载态：整块替换（#9） -->
    <div
      v-if="poolState === 'loading'"
      class="space-y-4"
      data-testid="page-draw--loading"
    >
      <div
        class="flex flex-col items-center gap-6 rounded-xl border border-border bg-card p-6 md:flex-row md:items-center"
      >
        <Skeleton class="size-56 shrink-0 rounded-full" />
        <div class="w-full space-y-3">
          <Skeleton class="h-4 w-40" />
          <Skeleton class="h-4 w-24" />
          <Skeleton class="h-10 w-full" />
        </div>
      </div>
      <p
        class="text-sm text-muted-foreground"
        role="status"
      >
        {{ messages.draw.loading }}
      </p>
    </div>

    <!-- 失败态：整块替换 + 重试（#10，纯只读无幂等键） -->
    <div
      v-else-if="poolState === 'error'"
      class="space-y-4"
      data-testid="page-draw--error"
    >
      <Alert variant="destructive">
        <AlertTitle>{{ messages.prizes.loadFailedTitle }}</AlertTitle>
        <AlertDescription>{{ poolError }}</AlertDescription>
      </Alert>
      <Button
        variant="outline"
        type="button"
        data-testid="draw-pool-retry"
        @click="loadPool"
      >
        {{ messages.common.retry }}
      </Button>
    </div>

    <!-- 空态：奖池无启用条目或抽奖返回 1502（#11） -->
    <div
      v-else-if="poolState === 'empty'"
      class="rounded-xl border border-border bg-card p-6"
      data-testid="page-draw--empty"
    >
      <EmptyState
        :title="messages.draw.poolEmptyTitle"
        :description="messages.draw.poolEmptyDesc"
      />
      <div class="mt-4 flex justify-center">
        <Button
          type="button"
          disabled
        >
          {{ messages.draw.start }}
        </Button>
      </div>
    </div>

    <!-- 基础层：奖池就绪（#8，常驻；noquota / drawing / drawfail 为其状态叠加） -->
    <div
      v-else
      class="space-y-6"
      data-testid="page-draw--default"
    >
      <!-- 抽奖失败（#16）：重试沿用同一幂等键，不会重复扣次 -->
      <Alert
        v-if="drawError.length > 0"
        variant="destructive"
        data-testid="page-draw--drawfail"
      >
        <AlertTitle>{{ messages.draw.drawFailedTitle }}</AlertTitle>
        <AlertDescription>{{ drawError }}</AlertDescription>
        <div class="mt-3">
          <Button
            variant="outline"
            size="sm"
            type="button"
            data-testid="draw-retry"
            :disabled="isDrawing"
            @click="handleStartDraw"
          >
            {{ messages.draw.retryDraw }}
          </Button>
        </div>
      </Alert>

      <div class="grid gap-6 rounded-xl border border-border bg-card p-6 md:grid-cols-[minmax(0,1fr)_320px]">
        <div class="flex flex-col items-center gap-4">
          <div
            class="size-64 sm:size-72"
            :data-testid="isDrawing ? 'page-draw--drawing' : undefined"
          >
            <DrawWheel
              :items="prizeItems"
              :spinning="isDrawing"
              :settle-index="settleIndex"
              :highlight-index="highlightIndex"
              :muted="!hasQuota"
              :center-text="`${messages.draw.quotaLabel}${remainingText} ${messages.draw.quotaUnit}`"
              @settled="handleSettled"
            />
          </div>
          <p
            v-if="isDrawing"
            class="text-sm text-muted-foreground"
            role="status"
          >
            {{ messages.draw.drawing }}
          </p>
        </div>

        <div class="space-y-5">
          <QuotaBadge
            :remaining="remaining"
            :daily-limit="dailyLimit"
          />

          <Button
            type="button"
            class="w-full"
            data-testid="draw-start"
            :disabled="isDrawing || !hasQuota"
            @click="handleStartDraw"
          >
            {{ isDrawing ? messages.draw.drawing : messages.draw.start }}
          </Button>

          <!-- 次数用尽（#12）：触发条件严格为「后端确认 remaining === 0」；
               次数未知（首载失败）不得命中本锚点（REV-01，§2.6 v3 收紧） -->
          <Alert
            v-if="remaining === 0"
            data-testid="page-draw--noquota"
          >
            <AlertTitle>{{ messages.draw.quotaExhausted }}</AlertTitle>
            <AlertDescription>{{ quotaExhaustedDetail }}</AlertDescription>
          </Alert>

          <PrizeLegend :items="prizeItems" />

          <RouterLink
            to="/records"
            class="inline-block text-sm text-muted-foreground hover:text-foreground"
          >
            {{ messages.draw.viewRecords }}
          </RouterLink>
        </div>
      </div>

      <DrawResultDialog
        :open="dialogOpen"
        :is-win="isWin"
        :prize-name="prizeName"
        :remaining="remaining"
        @update:open="(value: boolean) => (dialogOpen = value)"
        @continue="handleContinue"
        @view-records="handleViewRecords"
      />
    </div>
  </AppLayout>
</template>
