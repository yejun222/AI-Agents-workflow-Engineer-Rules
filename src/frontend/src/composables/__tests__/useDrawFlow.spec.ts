import { beforeEach, describe, expect, it, vi } from 'vitest'

import * as drawApi from '@/api/draw'
import * as prizeApi from '@/api/prize'
import { useDrawFlow } from '@/composables/useDrawFlow'
import type { DrawResponseDto } from '@/types/draw'
import type { PrizePoolItemDto } from '@/types/prize'
import { ApiError } from '@/utils/error'
import { messages } from '@/utils/messages'

vi.mock('@/api/draw', () => ({
  fetchQuota: vi.fn(),
  submitDraw: vi.fn()
}))

vi.mock('@/api/prize', () => ({
  fetchPrizePool: vi.fn()
}))

const PRIZE_ITEMS: PrizePoolItemDto[] = [
  { id: 1, name: '一等奖 · 机械键盘', shortName: '键盘', type: 1, displayOrder: 1 },
  { id: 5, name: '谢谢参与', shortName: '谢谢参与', type: 3, displayOrder: 5 }
]

const WIN_RESULT: DrawResponseDto = { itemId: 1, isWin: true, remainingAttempts: 2 }
const LOSE_RESULT: DrawResponseDto = { itemId: 5, isWin: false, remainingAttempts: 1 }

beforeEach(() => {
  vi.clearAllMocks()
  vi.mocked(prizeApi.fetchPrizePool).mockResolvedValue({ items: PRIZE_ITEMS })
  vi.mocked(drawApi.fetchQuota).mockResolvedValue({
    remainingAttempts: 3,
    dailyLimit: 3,
    resetAt: '2026-09-17T16:00:00+00:00'
  })
})

describe('composables/useDrawFlow', () => {
  it('奖池加载成功进入 ready 并填充条目', async () => {
    const flow = useDrawFlow()

    await flow.loadPool()

    expect(flow.poolState.value).toBe('ready')
    expect(flow.prizeItems.value).toHaveLength(2)
  })

  it('奖池为空进入 empty 态（对应 page-draw--empty）', async () => {
    vi.mocked(prizeApi.fetchPrizePool).mockResolvedValue({ items: [] })
    const flow = useDrawFlow()

    await flow.loadPool()

    expect(flow.poolState.value).toBe('empty')
  })

  it('奖池加载失败进入 error 态并给出可读文案（对应 page-draw--error）', async () => {
    vi.mocked(prizeApi.fetchPrizePool).mockRejectedValue(
      new ApiError(-1, messages.common.systemBusy)
    )
    const flow = useDrawFlow()

    await flow.loadPool()

    expect(flow.poolState.value).toBe('error')
    expect(flow.poolError.value).toBe(messages.common.systemBusy)
  })

  it('剩余次数刷新失败不覆盖已确认值，也不抛错', async () => {
    const flow = useDrawFlow()
    await flow.loadQuota()
    expect(flow.remaining.value).toBe(3)
    expect(flow.dailyLimit.value).toBe(3)

    vi.mocked(drawApi.fetchQuota).mockRejectedValue(new ApiError(-1, 'x'))
    await expect(flow.loadQuota()).resolves.toBeUndefined()
    expect(flow.remaining.value).toBe(3)
  })

  it('首载即失败：剩余次数保持未知（null，不得当作 0），抽奖入口仍可用（REV-01 / REV-17）', async () => {
    vi.mocked(drawApi.fetchQuota).mockRejectedValue(new ApiError(-1, messages.common.systemBusy))
    vi.mocked(drawApi.submitDraw).mockResolvedValueOnce(WIN_RESULT)
    const flow = useDrawFlow()

    await expect(flow.loadQuota()).resolves.toBeUndefined()

    // 未知 ≠ 0：既不写 0，也不进入「已用完」（page-draw--noquota 的触发条件是 remaining === 0）
    expect(flow.remaining.value).toBeNull()
    expect(flow.dailyLimit.value).toBeNull()
    expect(flow.hasQuota.value).toBe(true)

    // 抽奖入口可达：未确认次数时依然发起请求，且以抽奖响应回写剩余次数
    await flow.startDraw()
    expect(vi.mocked(drawApi.submitDraw)).toHaveBeenCalledTimes(1)
    expect(flow.remaining.value).toBe(2)
  })

  it('收到 1501：以权威错误码将未知次数收紧为 0（页面对应 page-draw--noquota）', async () => {
    vi.mocked(drawApi.fetchQuota).mockRejectedValue(new ApiError(-1, messages.common.systemBusy))
    vi.mocked(drawApi.submitDraw).mockRejectedValueOnce(new ApiError(1501, '今日抽奖次数已用完'))
    const flow = useDrawFlow()

    await flow.loadQuota()
    await flow.startDraw()

    expect(flow.remaining.value).toBe(0)
    expect(flow.hasQuota.value).toBe(false)
  })

  it('抽奖命中奖品：结果与剩余次数以抽奖响应为准（后续 API-06 刷新失败不回退），阶段回到 idle', async () => {
    vi.mocked(drawApi.submitDraw).mockResolvedValueOnce(WIN_RESULT)
    // 抽奖成功后会异步刷新 API-06；此处令其失败，锁定「刷新失败不覆盖已确认值」的分支
    vi.mocked(drawApi.fetchQuota).mockRejectedValue(new ApiError(-1, messages.common.systemBusy))
    const flow = useDrawFlow()
    await flow.loadPool()

    await flow.startDraw()

    expect(flow.phase.value).toBe('idle')
    expect(flow.result.value).toEqual(WIN_RESULT)
    expect(flow.resultIndex.value).toBe(0)
    expect(flow.remaining.value).toBe(2)
    expect(flow.drawError.value).toBe('')
  })

  it('抽奖成功后按契约刷新 API-06，并以刷新结果更新剩余次数（FR-07-2）', async () => {
    vi.mocked(drawApi.submitDraw).mockResolvedValueOnce(WIN_RESULT)
    const flow = useDrawFlow()
    await flow.loadQuota()
    expect(flow.remaining.value).toBe(3)

    vi.mocked(drawApi.fetchQuota).mockResolvedValue({
      remainingAttempts: 1,
      dailyLimit: 3,
      resetAt: '2026-09-17T16:00:00+00:00'
    })
    await flow.startDraw()

    // 抽奖响应先回写（2），随后 API-06 刷新成功采用其值（1）
    await vi.waitFor(() => {
      expect(flow.remaining.value).toBe(1)
    })
  })

  it('抽奖返回 1501（次数用尽）：剩余次数归零且不作失败提示', async () => {
    vi.mocked(drawApi.submitDraw).mockRejectedValueOnce(
      new ApiError(1501, '今日抽奖次数已用完，明日 0 点重置')
    )
    const flow = useDrawFlow()
    await flow.loadQuota()

    await flow.startDraw()

    expect(flow.remaining.value).toBe(0)
    expect(flow.hasQuota.value).toBe(false)
    expect(flow.drawError.value).toBe('')
    expect(flow.result.value).toBeNull()
  })

  it('抽奖返回 1502（候选集为空）：进入 empty 态（对应 page-draw--empty）', async () => {
    vi.mocked(drawApi.submitDraw).mockRejectedValueOnce(
      new ApiError(1502, '奖品已抽完，请稍后再来')
    )
    const flow = useDrawFlow()
    await flow.loadPool()

    await flow.startDraw()

    expect(flow.poolState.value).toBe('empty')
  })

  it('抽奖失败给出可重试提示，重试沿用同一幂等键，成功后作废重开新键', async () => {
    const flow = useDrawFlow()
    await flow.loadPool()

    vi.mocked(drawApi.submitDraw).mockRejectedValueOnce(
      new ApiError(-1, messages.common.systemBusy)
    )
    await flow.startDraw()
    expect(flow.drawError.value).toBe(messages.common.systemBusy)
    const firstKey = vi.mocked(drawApi.submitDraw).mock.calls[0]?.[0]

    // 重试：沿用同一幂等键（D-03：换键会重复扣次）
    vi.mocked(drawApi.submitDraw).mockResolvedValueOnce(WIN_RESULT)
    await flow.startDraw()
    const retryKey = vi.mocked(drawApi.submitDraw).mock.calls[1]?.[0]
    expect(retryKey).toBe(firstKey)
    expect(flow.drawError.value).toBe('')

    // 成功后键作废：下一次抽奖是新的幂等键
    vi.mocked(drawApi.submitDraw).mockResolvedValueOnce(LOSE_RESULT)
    await flow.startDraw()
    const nextKey = vi.mocked(drawApi.submitDraw).mock.calls[2]?.[0]
    expect(nextKey).not.toBe(retryKey)
  })

  it('clearResult 关闭结果并作废幂等键（继续抽奖走全新尝试）', async () => {
    vi.mocked(drawApi.submitDraw).mockResolvedValueOnce(WIN_RESULT)
    const flow = useDrawFlow()
    await flow.loadPool()
    await flow.startDraw()
    expect(flow.result.value).not.toBeNull()

    flow.clearResult()

    expect(flow.result.value).toBeNull()
    expect(flow.resultIndex.value).toBe(-1)
  })

  it('结果条目不在快照时重拉奖池一次并重新定位（TC-48c / FR-05-R10）', async () => {
    // 页面加载后奖池发生启用变更：快照不含条目 3，抽奖结果恰为条目 3
    const mug: PrizePoolItemDto = { id: 3, name: '三等奖 · 定制马克杯', shortName: '马克杯', type: 1, displayOrder: 3 }
    vi.mocked(drawApi.submitDraw).mockResolvedValueOnce({ itemId: 3, isWin: true, remainingAttempts: 2 })
    const flow = useDrawFlow()
    await flow.loadPool()
    expect(flow.prizeItems.value.map((item) => item.id)).not.toContain(3)

    await flow.startDraw()
    expect(flow.resultIndex.value).toBe(-1)

    // 重拉：返回包含条目 3 的最新快照 → 结果条目重新可定位
    vi.mocked(prizeApi.fetchPrizePool).mockResolvedValueOnce({ items: [...PRIZE_ITEMS, mug] })
    await flow.reloadPoolForResult()

    expect(vi.mocked(prizeApi.fetchPrizePool)).toHaveBeenCalledTimes(2)
    expect(flow.resultIndex.value).toBe(2)
    expect(flow.prizeItems.value[2]?.name).toBe('三等奖 · 定制马克杯')
  })

  it('重拉奖池不切换页面状态锚点（弹层与转盘不被渲染边界替换），失败时保留原快照', async () => {
    vi.mocked(drawApi.submitDraw).mockResolvedValueOnce({ itemId: 3, isWin: true, remainingAttempts: 2 })
    const flow = useDrawFlow()
    await flow.loadPool()
    await flow.startDraw()

    vi.mocked(prizeApi.fetchPrizePool).mockRejectedValueOnce(new ApiError(-1, messages.common.systemBusy))
    await expect(flow.reloadPoolForResult()).resolves.toBeUndefined()

    // 失败：不进 error / loading 态、不清空快照（否则结果反馈会被渲染边界阻塞）
    expect(flow.poolState.value).toBe('ready')
    expect(flow.poolError.value).toBe('')
    expect(flow.prizeItems.value).toHaveLength(2)
    expect(flow.resultIndex.value).toBe(-1)
  })

  it('重拉奖池返回空快照时保留原快照（空快照无法定位，交回兜底文案）', async () => {
    vi.mocked(drawApi.submitDraw).mockResolvedValueOnce({ itemId: 3, isWin: true, remainingAttempts: 2 })
    const flow = useDrawFlow()
    await flow.loadPool()
    await flow.startDraw()

    vi.mocked(prizeApi.fetchPrizePool).mockResolvedValueOnce({ items: [] })
    await flow.reloadPoolForResult()

    expect(flow.poolState.value).toBe('ready')
    expect(flow.prizeItems.value).toHaveLength(2)
    expect(flow.resultIndex.value).toBe(-1)
  })
})
