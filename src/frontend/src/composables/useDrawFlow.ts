import { computed, ref, type ComputedRef, type Ref } from 'vue'

import { fetchQuota, submitDraw } from '@/api/draw'
import { fetchPrizePool } from '@/api/prize'
import { DRAW_ERROR_CODE, type DrawResponseDto } from '@/types/draw'
import type { PrizePoolItemDto } from '@/types/prize'
import { resolveErrorCode, resolveErrorMessage } from '@/utils/error'
import { createIdempotencyKey } from '@/utils/idempotency'

/** 奖池加载态：loading → ready / empty / error（§2.6 的 page-draw--loading / --empty / --error）。 */
export type PoolState = 'loading' | 'ready' | 'empty' | 'error'

/** 抽奖进行态：drawing 期间按钮禁用且按钮文案为「抽奖中…」。 */
export type DrawPhase = 'idle' | 'drawing'

/** 抽奖页流程状态与动作（页面态留在组合式函数内，不进 Pinia）。 */
export interface DrawFlow {
  poolState: Ref<PoolState>
  poolError: Ref<string>
  prizeItems: Ref<PrizePoolItemDto[]>
  /** 后端确认的剩余次数；`null` = **未知**（首载查询失败），严禁当作 0（§2.6 次数未知态）。 */
  remaining: Ref<number | null>
  /** 每日上限；`null` = 未知（上限徽标隐藏）。 */
  dailyLimit: Ref<number | null>
  /** 下次重置时刻（ISO 8601 UTC）；`null` = 未知（Should-S2 引导文案降级）。 */
  resetAt: Ref<string | null>
  phase: Ref<DrawPhase>
  drawError: Ref<string>
  result: Ref<DrawResponseDto | null>
  resultIndex: ComputedRef<number>
  /** 抽奖入口是否可用：未知（null）视为可用，仅「后端确认 0」才禁用（FR-04 / REV-01）。 */
  hasQuota: ComputedRef<boolean>
  loadPool: () => Promise<void>
  /** 结果条目不在当前奖池快照时的**定向重拉**（重拉奖池一次再定位，FR-05-R10 / TC-48c）。 */
  reloadPoolForResult: () => Promise<void>
  loadQuota: () => Promise<void>
  startDraw: () => Promise<void>
  clearResult: () => void
}

/**
 * 抽奖流程：奖池与剩余次数加载 → 点击抽奖（同一幂等键）→ 落点由后端 `itemId` 决定。
 * 幂等键只在「本次抽奖成功」后作废：失败重试沿用同一键，避免重复扣次（D-03）。
 */
export function useDrawFlow(): DrawFlow {
  const poolState = ref<PoolState>('loading')
  const poolError = ref<string>('')
  const prizeItems = ref<PrizePoolItemDto[]>([])
  const remaining = ref<number | null>(null)
  const dailyLimit = ref<number | null>(null)
  const resetAt = ref<string | null>(null)
  const phase = ref<DrawPhase>('idle')
  const drawError = ref<string>('')
  const result = ref<DrawResponseDto | null>(null)
  const pendingKey = ref<string | null>(null)

  const resultIndex = computed<number>(() => {
    const current = result.value
    if (current === null) {
      return -1
    }

    return prizeItems.value.findIndex((item) => item.id === current.itemId)
  })

  // 次数「未知」不等于「为 0」（REV-01）：仅后端确认 remaining === 0 才禁用入口、才展示 page-draw--noquota
  const hasQuota = computed<boolean>(() => remaining.value === null || remaining.value > 0)

  /** 加载奖池（纯只读，无幂等键）；失败可重试。 */
  async function loadPool(): Promise<void> {
    poolState.value = 'loading'
    poolError.value = ''

    try {
      const response = await fetchPrizePool()
      prizeItems.value = response.items
      poolState.value = response.items.length === 0 ? 'empty' : 'ready'
    } catch (error) {
      poolError.value = resolveErrorMessage(error)
      poolState.value = 'error'
    }
  }

  /**
   * 结果条目不在当前奖池快照时的定向重拉（页面加载后奖池发生启用 / 停用变更的竞态，FR-05-R10 / TC-48c）：
   * 重拉奖池一次，按最新快照重新定位结果条目（`resultIndex` 由 `prizeItems` 派生，替换即重新定位）。
   *
   * 与 `loadPool` 的关键区别：**不改动 `poolState`**。状态锚点一旦切换，`page-draw--default` 整块
   * （含转盘与结果弹层）会被渲染边界替换卸载，停稳事件与弹层一并丢失 —— 而重拉的目的是**保住结果反馈**。
   * 重拉失败或取回空快照（空快照无法定位任何条目）时保留原快照，由调用方按「仍不可定位」走兜底文案。
   */
  async function reloadPoolForResult(): Promise<void> {
    try {
      const response = await fetchPrizePool()
      if (response.items.length > 0) {
        prizeItems.value = response.items
      }
    } catch {
      // 静默降级：重拉是定位结果的补充通道，失败不得新增错误态、不得清空原有快照
    }
  }

  /**
   * 同步今日剩余次数（以服务端为准，FR-04 / FR-07-2）。
   * 失败**不覆盖已确认值**、也不把未知态写成 0：剩余次数保持未知（`null`），
   * 抽奖入口继续可用，恢复通道由后续导航重拉 / 抽奖响应回写 / `1501` 权威确认承担（§2.6）。
   */
  async function loadQuota(): Promise<void> {
    try {
      const quota = await fetchQuota()
      remaining.value = quota.remainingAttempts
      dailyLimit.value = quota.dailyLimit
      resetAt.value = quota.resetAt
    } catch {
      // 静默降级：不新增「次数查询失败」边界状态（不触碰原型的冻结锚点集合）
    }
  }

  /** 执行抽奖；失败按错误码分流（1501 次数用尽 / 1502 奖池抽完 / 其余提示可重试）。 */
  async function startDraw(): Promise<void> {
    if (phase.value === 'drawing') {
      return
    }

    phase.value = 'drawing'
    drawError.value = ''

    // 失败重试沿用同一幂等键；仅在成功（或确定性业务拒绝）后作废
    const key = pendingKey.value ?? createIdempotencyKey()
    pendingKey.value = key

    try {
      const response = await submitDraw(key)
      // 先以抽奖响应回写徽标（后端权威值，可修正首载失败导致的未知态），再异步刷新 API-06；
      // 刷新失败不覆盖此处已确认的值（loadQuota 失败不写状态）
      remaining.value = response.remainingAttempts
      result.value = response
      pendingKey.value = null
      void loadQuota()
    } catch (error) {
      const code = resolveErrorCode(error)
      if (code === DRAW_ERROR_CODE.quotaExhausted) {
        remaining.value = 0
        pendingKey.value = null
      } else if (code === DRAW_ERROR_CODE.noCandidate) {
        poolState.value = 'empty'
        pendingKey.value = null
      } else {
        drawError.value = resolveErrorMessage(error)
      }
    } finally {
      phase.value = 'idle'
    }
  }

  /** 关闭结果弹层（「继续抽奖」）；下一次抽奖会生成新的幂等键。 */
  function clearResult(): void {
    result.value = null
    pendingKey.value = null
  }

  return {
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
  }
}
