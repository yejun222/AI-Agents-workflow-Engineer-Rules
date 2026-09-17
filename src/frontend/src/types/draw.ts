/** 剩余次数查询响应（后端 `DrawQuotaDto`）。 */
export interface DrawQuotaDto {
  remainingAttempts: number
  dailyLimit: number
  /** 下次重置时刻（UTC ISO 8601 带偏移）。 */
  resetAt: string
}

/** 执行抽奖响应（后端 `DrawResponseDto`；落点以 `itemId` 为唯一依据）。 */
export interface DrawResponseDto {
  itemId: number
  isWin: boolean
  remainingAttempts: number
}

/** 抽奖业务错误码（后端 `ErrorCodes`，全部取自 docs/error-codes.md）。 */
export const DRAW_ERROR_CODE = {
  systemBusy: 1001,
  quotaExhausted: 1501,
  noCandidate: 1502,
  idempotencyConflict: 409
} as const
