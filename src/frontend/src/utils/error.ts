import { messages } from '@/utils/messages'

/** 网络异常 / 无法解析响应时的占位错误码（后端业务码一律 `>=1000`，HTTP 码为 0-599）。 */
export const NETWORK_ERROR_CODE = -1

/** 业务 / 网络错误统一类型：`code` 取自后端 `ApiResult.code`。 */
export class ApiError extends Error {
  readonly code: number

  constructor(code: number, message: string) {
    super(message)
    this.name = 'ApiError'
    this.code = code
  }
}

/** 类型守卫：判断是否为统一的 ApiError。 */
export function isApiError(error: unknown): error is ApiError {
  return error instanceof ApiError
}

/** 取错误码（非 ApiError 一律视作网络异常）。 */
export function resolveErrorCode(error: unknown): number {
  return isApiError(error) ? error.code : NETWORK_ERROR_CODE
}

/** 取用户可读文案：业务文案优先，其余一律兜底「系统繁忙，请稍后重试」。 */
export function resolveErrorMessage(error: unknown): string {
  if (isApiError(error) && error.message.trim().length > 0) {
    return error.message
  }

  return messages.common.systemBusy
}
