import type { PageQuery, PageResult } from '@/types/api'
import type { WinningRecordDto } from '@/types/record'
import { http } from '@/utils/request'

/** 查询本人中奖记录（API-08）：分页、按中奖时间倒序；不接受 userId 参数（取当前登录用户）。 */
export function fetchWinningRecords(query: PageQuery): Promise<PageResult<WinningRecordDto>> {
  return http.get<PageResult<WinningRecordDto>>('/records', { params: query })
}
