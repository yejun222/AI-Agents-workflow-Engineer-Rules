import type { DrawQuotaDto, DrawResponseDto } from '@/types/draw'
import { http } from '@/utils/request'

/** 查询今日剩余次数（API-06）：以服务端为准（UTC+8 自然日），不信任客户端时间。 */
export function fetchQuota(): Promise<DrawQuotaDto> {
  return http.get<DrawQuotaDto>('/draw/quota')
}

/**
 * 执行抽奖（API-07）：请求体恒为空对象，影响结果的参数全部由服务端决定；
 * `idempotencyKey` 由调用方生成并在失败重试时沿用同一值（D-03）。
 */
export function submitDraw(idempotencyKey: string): Promise<DrawResponseDto> {
  return http.post<DrawResponseDto>(
    '/draw',
    {},
    { headers: { 'Idempotency-Key': idempotencyKey } }
  )
}
