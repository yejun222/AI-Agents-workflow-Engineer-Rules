/** 我的中奖记录列表项（后端 `WinningRecordDto`）。 */
export interface WinningRecordDto {
  id: number
  prizeName: string
  /** 中奖时间（后端 UTC `DateTime`，序列化不带时区后缀，展示前须按 UTC 解析）。 */
  winTime: string
}
