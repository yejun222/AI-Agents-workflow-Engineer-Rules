/** 奖品条目类型（后端 `PrizeItemType` 枚举 int 值）。 */
export const PRIZE_TYPE = {
  physical: 1,
  virtual: 2,
  noPrize: 3
} as const

export type PrizeType = (typeof PRIZE_TYPE)[keyof typeof PRIZE_TYPE]

/** 奖池条目对外投影（后端 `PrizePoolItemDto`，D-07 白名单：结构上不含权重与库存）。 */
export interface PrizePoolItemDto {
  id: number
  name: string
  shortName: string
  type: number
  displayOrder: number
}

/** 奖池查询响应（后端 `PrizePoolResponse`）。 */
export interface PrizePoolResponse {
  items: PrizePoolItemDto[]
}
