import { describe, expect, it } from 'vitest'

import { AUTH_ERROR_CODE } from '@/types/auth'
import { DRAW_ERROR_CODE } from '@/types/draw'
import {
  ApiError,
  NETWORK_ERROR_CODE,
  isApiError,
  resolveErrorCode,
  resolveErrorMessage
} from '@/utils/error'
import { messages } from '@/utils/messages'

describe('utils/error', () => {
  it('业务错误码原样透传后端文案', () => {
    const error = new ApiError(DRAW_ERROR_CODE.quotaExhausted, '今日抽奖次数已用完，明日 0 点重置')

    expect(isApiError(error)).toBe(true)
    expect(resolveErrorCode(error)).toBe(1501)
    expect(resolveErrorMessage(error)).toBe('今日抽奖次数已用完，明日 0 点重置')
  })

  // 错误码取自 docs/error-codes.md：1001 系统繁忙 / 1102 凭证错误 / 1203 会话失效 / 1204 会话过期 / 1502 无候选
  const cases: Array<[number, string]> = [
    [1001, '系统繁忙，请稍后重试'],
    [1102, '用户名或密码错误'],
    [1203, '登录状态已失效，请重新登录'],
    [1204, '登录状态已过期，请重新登录'],
    [1502, '奖品已抽完，请稍后再来'],
    [409, '请勿重复提交']
  ]

  it.each(cases)('业务码 %i 展示后端文案 %s', (code, message) => {
    expect(resolveErrorCode(new ApiError(code, message))).toBe(code)
    expect(resolveErrorMessage(new ApiError(code, message))).toBe(message)
  })

  it('错误码常量与后端登记号一致', () => {
    expect(DRAW_ERROR_CODE.systemBusy).toBe(1001)
    expect(DRAW_ERROR_CODE.quotaExhausted).toBe(1501)
    expect(DRAW_ERROR_CODE.noCandidate).toBe(1502)
    expect(DRAW_ERROR_CODE.idempotencyConflict).toBe(409)
    expect(AUTH_ERROR_CODE.userNameTaken).toBe(1101)
    expect(AUTH_ERROR_CODE.accountLocked).toBe(1103)
  })

  it('非 ApiError 一律按网络异常处理并兜底文案', () => {
    expect(resolveErrorCode(new Error('Network Error'))).toBe(NETWORK_ERROR_CODE)
    expect(resolveErrorMessage(new Error('Network Error'))).toBe(messages.common.systemBusy)
    expect(resolveErrorMessage('boom')).toBe(messages.common.systemBusy)
    expect(resolveErrorMessage(undefined)).toBe(messages.common.systemBusy)
    expect(isApiError(new Error('Network Error'))).toBe(false)
  })

  it('文案为空的 ApiError 兜底为系统繁忙', () => {
    expect(resolveErrorMessage(new ApiError(DRAW_ERROR_CODE.systemBusy, '   '))).toBe(
      messages.common.systemBusy
    )
  })
})
