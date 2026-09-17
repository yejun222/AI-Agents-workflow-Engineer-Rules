/** 时间展示工具：后端时间统一为 UTC，展示一律换算到 UTC+8（PRD 4.1）。 */
const UTC8_OFFSET_MS = 8 * 60 * 60 * 1000

/**
 * 解析后端返回的时间串（容错 `.NET` 的 6 位毫秒与缺失时区后缀两种情形）：
 * - `.NET` `DateTime`（Kind=Utc）序列化为 `2026-09-17T07:12:33.123456`，**无 `Z` 后缀**，
 *   直接交给 `new Date()` 会被当作本地时间，必须补 `Z`；
 * - 毫秒超过 3 位不符合 ECMAScript 日期串格式，统一截断到 3 位避免各引擎解析差异。
 */
export function parseUtc(value: string): Date {
  const normalized = value.replace(/\.(\d{3})\d+/, '.$1')
  const hasTimeZone = /(?:z|[+-]\d{2}:?\d{2})$/i.test(normalized)
  return new Date(hasTimeZone ? normalized : `${normalized}Z`)
}

function pad(value: number): string {
  return String(value).padStart(2, '0')
}

/** 展示精度：`minute` = `yyyy-MM-dd HH:mm`（PRD 4.1-3 的记录页口径），`second` = 带秒。 */
export type TimePrecision = 'minute' | 'second'

/** 格式化为 UTC+8 的 `YYYY-MM-DD HH:mm[:ss]`（默认带秒）；无法解析时原样返回。 */
export function formatUtc8DateTime(value: string, precision: TimePrecision = 'second'): string {
  const date = parseUtc(value)
  if (Number.isNaN(date.getTime())) {
    return value
  }

  const shifted = new Date(date.getTime() + UTC8_OFFSET_MS)
  const datePart = `${shifted.getUTCFullYear()}-${pad(shifted.getUTCMonth() + 1)}-${pad(shifted.getUTCDate())}`
  const minutePart = `${pad(shifted.getUTCHours())}:${pad(shifted.getUTCMinutes())}`
  const timePart =
    precision === 'minute' ? minutePart : `${minutePart}:${pad(shifted.getUTCSeconds())}`

  return `${datePart} ${timePart}`
}
