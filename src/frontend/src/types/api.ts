/** 后端统一响应包装（`ApiResult<T>`，`code=0` 为成功；业务异常 HTTP 200 + `code>=1000`）。 */
export interface ApiResult<T> {
  code: number
  message: string
  data: T | null
}

/** 通用分页响应（后端 `PageResult<T>`）。 */
export interface PageResult<T> {
  items: T[]
  totalCount: number
  pageIndex: number
  pageSize: number
}

/** 分页查询参数（`pageIndex` 从 1 起）。 */
export interface PageQuery {
  pageIndex: number
  pageSize: number
}
