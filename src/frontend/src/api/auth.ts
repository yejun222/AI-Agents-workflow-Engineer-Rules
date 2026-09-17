import type { AuthResponse, LoginRequest, RefreshResponse, RegisterRequest } from '@/types/auth'
import { http, refreshAccessToken } from '@/utils/request'

/** 注册（API-01）：后端注册成功即签发凭证，前端视作已登录；幂等键见 D-15。 */
export function register(payload: RegisterRequest, idempotencyKey: string): Promise<AuthResponse> {
  return http.post<AuthResponse>('/auth/register', payload, {
    headers: { 'Idempotency-Key': idempotencyKey }
  })
}

/** 登录（API-02）；幂等键见 D-15。 */
export function login(payload: LoginRequest, idempotencyKey: string): Promise<AuthResponse> {
  return http.post<AuthResponse>('/auth/login', payload, {
    headers: { 'Idempotency-Key': idempotencyKey }
  })
}

/** 登出（API-04）：注销服务端 refresh 会话并清除 Cookie。 */
export function logout(): Promise<void> {
  return http.post<void>('/auth/logout')
}

/**
 * 刷新访问令牌（API-03）：页面刷新 / 新标签页打开后内存令牌丢失，用于受保护路由的静默恢复（§2.8）。
 * 复用 `utils/request` 的去重刷新链路（裸实例 + 在途 Promise 复用），避免并发刷新导致凭证复用踢下线。
 * 成功返回响应体（含兼容性新增的 `user`），失败返回 `null` 由调用方引导登录。
 */
export function refreshSession(): Promise<RefreshResponse | null> {
  return refreshAccessToken()
}
