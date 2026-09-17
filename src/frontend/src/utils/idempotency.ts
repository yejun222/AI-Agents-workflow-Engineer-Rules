/**
 * 幂等键工具（D-03）：抽奖请求头 `Idempotency-Key` 由前端生成 UUID；
 * **失败重试必须沿用同一键**（换键会产生第二次扣次），成功后重新生成。
 */
export function createIdempotencyKey(): string {
  if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') {
    return crypto.randomUUID()
  }

  // 非安全上下文（如局域网 http 访问）下 randomUUID 不可用，退化为随机字节拼接 UUID v4
  const bytes = new Uint8Array(16)
  crypto.getRandomValues(bytes)
  const hex = Array.from(bytes, (byte) => byte.toString(16).padStart(2, '0')).join('')

  return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-4${hex.slice(13, 16)}-8${hex.slice(17, 20)}-${hex.slice(20, 32)}`
}
