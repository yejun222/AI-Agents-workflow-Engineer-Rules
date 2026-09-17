import { describe, expect, it } from 'vitest'

import { formatUtc8DateTime, parseUtc } from '@/utils/datetime'

describe('utils/datetime', () => {
  it('.NET 的 UTC 时间串（无时区后缀、6 位毫秒）按 UTC 解析，不当作本地时间', () => {
    // WinTime 由后端 DateTime（Kind=Utc）序列化而来：2026-09-17T07:12:33.123456
    expect(parseUtc('2026-09-17T07:12:33.123456').toISOString()).toBe('2026-09-17T07:12:33.123Z')
  })

  it('带 Z 后缀与带时区偏移的时间串解析为同一时刻', () => {
    expect(parseUtc('2026-09-17T07:12:33Z').getTime()).toBe(
      parseUtc('2026-09-17T15:12:33+08:00').getTime()
    )
  })

  it('已携带时区标识（Z / +00:00）的时间串不得被二次拼接 Z（BUG-02 修复后的 winTime 形态）', () => {
    // BUG-02 修复后 winTime 由 DateTimeOffset 序列化而来：2026-09-17T06:51:42.397853+00:00
    expect(parseUtc('2026-09-17T06:51:42.397853+00:00').toISOString()).toBe(
      '2026-09-17T06:51:42.397Z'
    )
    expect(parseUtc('2026-09-17T06:51:42.397853Z').toISOString()).toBe(
      '2026-09-17T06:51:42.397Z'
    )
    // 二者解析为同一时刻：+00:00 与 Z 等价，且毫秒被截断到 3 位
    expect(formatUtc8DateTime('2026-09-17T06:51:42.397853+00:00', 'minute')).toBe(
      '2026-09-17 14:51'
    )
  })

  it('展示统一换算为 UTC+8', () => {
    expect(formatUtc8DateTime('2026-09-17T07:12:33.123456')).toBe('2026-09-17 15:12:33')
    expect(formatUtc8DateTime('2026-09-17T16:00:00Z')).toBe('2026-09-18 00:00:00')
  })

  it('UTC+8 跨日边界（UTC 16:00 = 次日 0 点）', () => {
    expect(formatUtc8DateTime('2026-09-16T16:00:00Z')).toBe('2026-09-17 00:00:00')
  })

  it('分钟精度（PRD 4.1-3：记录页展示格式 yyyy-MM-dd HH:mm）不携带秒', () => {
    expect(formatUtc8DateTime('2026-09-17T07:12:33.123456', 'minute')).toBe('2026-09-17 15:12')
    expect(formatUtc8DateTime('2026-09-16T16:00:00Z', 'minute')).toBe('2026-09-17 00:00')
  })

  it('分钟精度下非法输入同样原样返回', () => {
    expect(formatUtc8DateTime('not-a-date', 'minute')).toBe('not-a-date')
  })

  it('无法解析的输入原样返回且不抛错', () => {
    expect(formatUtc8DateTime('not-a-date')).toBe('not-a-date')
    expect(formatUtc8DateTime('')).toBe('')
  })
})
