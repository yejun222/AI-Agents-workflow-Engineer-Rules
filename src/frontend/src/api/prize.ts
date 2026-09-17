import type { PrizePoolResponse } from '@/types/prize'
import { http } from '@/utils/request'

/** 查询奖池（API-05）：返回启用中的条目（按扇区顺序升序），响应不含权重与库存（D-07）。 */
export function fetchPrizePool(): Promise<PrizePoolResponse> {
  return http.get<PrizePoolResponse>('/prizes')
}
