import axios, {
  AxiosError,
  type AxiosInstance,
  type AxiosRequestConfig,
  type AxiosResponse,
  type InternalAxiosRequestConfig
} from 'axios'

import type { ApiResult } from '@/types/api'
import type { RefreshResponse } from '@/types/auth'
import { getAccessToken, notifyUnauthorized, setAccessToken } from '@/utils/auth-token'
import { ApiError, NETWORK_ERROR_CODE } from '@/utils/error'
import { messages } from '@/utils/messages'

/** 接口基地址：默认同源（开发态由 vite proxy 转发到后端，生产由网关代理）。 */
export const API_BASE_URL = `${import.meta.env.VITE_API_BASE_URL ?? ''}/api/v1`

/** 带内部重放标记的请求配置：每条请求只重放一次，避免「401 → 刷新 → 仍 401 → 再刷新」死循环。 */
interface RetryableRequestConfig extends InternalAxiosRequestConfig {
  _retry?: boolean
}

/** 这些路径的 401 不触发无感刷新（本就没有有效会话或刷新自身失败）。 */
const NO_REFRESH_PATHS = ['/auth/login', '/auth/register', '/auth/refresh', '/auth/logout']

const request: AxiosInstance = axios.create({
  baseURL: API_BASE_URL,
  timeout: 15000,
  // refresh token 在 httpOnly + SameSite=Strict Cookie（Path=/api/v1/auth），必须携带凭证
  withCredentials: true
})

/** 裸实例：刷新请求专用（复用主实例会让刷新失败的 401 再次进入同一拦截器，形成递归）。 */
const rawRequest: AxiosInstance = axios.create({
  baseURL: API_BASE_URL,
  timeout: 15000,
  withCredentials: true
})

/**
 * 在途刷新 Promise：refresh token 一次性轮换，并发刷新会被服务端判定「凭证复用」并注销全部会话。
 * 401 无感刷新与受保护路由的静默恢复（§2.8）**必须共用同一个 Promise**，禁止两条路径各自发刷新请求。
 */
let refreshPromise: Promise<RefreshResponse | null> | null = null

/**
 * 刷新访问令牌（API-03）：并发调用复用同一在途 Promise；失败返回 `null` 由调用方处理会话失效。
 * 刷新接口失败按 HTTP 200 + `code` 返回（规范 6.4），因此必须自行判断 `code`。
 * 成功时已写入内存访问令牌，返回体（含 `user`，兼容性新增）供会话恢复补全内存态。
 */
export function refreshAccessToken(): Promise<RefreshResponse | null> {
  if (refreshPromise === null) {
    refreshPromise = sendRefreshRequest()
  }

  return refreshPromise
}

async function sendRefreshRequest(): Promise<RefreshResponse | null> {
  try {
    const response = await rawRequest.post<ApiResult<RefreshResponse>>('/auth/refresh')
    if (response.data.code !== 0 || response.data.data === null) {
      return null
    }

    setAccessToken(response.data.data.accessToken)
    return response.data.data
  } catch {
    return null
  } finally {
    refreshPromise = null
  }
}

request.interceptors.request.use((config: InternalAxiosRequestConfig) => {
  const token = getAccessToken()
  if (token !== null && token.length > 0) {
    config.headers.set('Authorization', `Bearer ${token}`)
  }

  return config
})

request.interceptors.response.use(
  (response: AxiosResponse) => response,
  async (error: unknown): Promise<AxiosResponse> => {
    if (!axios.isAxiosError(error)) {
      throw new ApiError(NETWORK_ERROR_CODE, messages.common.systemBusy)
    }

    const config = error.config as RetryableRequestConfig | undefined
    const isUnauthorized = error.response?.status === 401

    if (isUnauthorized && config !== undefined && config._retry !== true && canRefresh(config.url)) {
      config._retry = true
      const refreshed = await refreshAccessToken()
      if (refreshed !== null) {
        // 重放必须走带请求拦截器的封装实例（直接 axios 会读不到刚更新的访问令牌，重放立刻再 401）
        return await request.request(config)
      }

      notifyUnauthorized()
      throw new ApiError(401, messages.common.sessionExpired)
    }

    throw new ApiError(error.response?.status ?? NETWORK_ERROR_CODE, mapHttpError(error))
  }
)

/** 非 2xx 响应：优先采用响应体内的 `ApiResult`（如限流 429 带 code=1001），否则按状态码给文案。 */
function mapHttpError(error: AxiosError): string {
  const payload = error.response?.data
  if (isApiResult(payload) && payload.message.trim().length > 0) {
    return payload.message
  }

  const status = error.response?.status
  if (status === 409) {
    return messages.common.idempotencyConflict
  }

  if (status === 429) {
    return messages.common.tooManyRequests
  }

  if (status === 401 || status === 403) {
    return messages.common.sessionExpired
  }

  return messages.common.systemBusy
}

function canRefresh(url: string | undefined): boolean {
  if (url === undefined) {
    return true
  }

  return !NO_REFRESH_PATHS.some((path) => url.includes(path))
}

/** 响应体是否满足 `ApiResult` 结构（`code` 为数字）。 */
function isApiResult(value: unknown): value is ApiResult<unknown> {
  return (
    typeof value === 'object' && value !== null && 'code' in value && typeof value.code === 'number'
  )
}

/** 解包 `ApiResult<T>`：`code=0` 返回 `data`，`code>=1000` 抛 `ApiError`（业务异常 HTTP 200）。 */
async function unwrapResponse<T>(pending: Promise<AxiosResponse<ApiResult<T>>>): Promise<T> {
  const response = await pending
  const payload = response.data

  if (payload.code !== 0) {
    const message = payload.message.trim().length > 0 ? payload.message : messages.common.systemBusy
    throw new ApiError(payload.code, message)
  }

  if (payload.data === null) {
    // 允许 data 为空的接口（如登出）返回空值
    return undefined as T
  }

  return payload.data
}

/** 统一请求入口：业务代码只用 `http.get` / `http.post`，禁止组件内直连 axios。 */
export const http = {
  get<T>(url: string, config?: AxiosRequestConfig): Promise<T> {
    return unwrapResponse<T>(request.get<ApiResult<T>>(url, config))
  },
  post<T>(url: string, data?: unknown, config?: AxiosRequestConfig): Promise<T> {
    return unwrapResponse<T>(request.post<ApiResult<T>>(url, data, config))
  }
}

export default request
