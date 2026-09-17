import { defineStore } from 'pinia'
import { computed, ref } from 'vue'

import * as authApi from '@/api/auth'
import type { AuthResponse, LoginFormValues, RegisterFormValues, UserDto } from '@/types/auth'
import { setAccessToken } from '@/utils/auth-token'
import { resolveErrorCode } from '@/utils/error'
import { createIdempotencyKey } from '@/utils/idempotency'

/** 会话恢复闸门（§2.8 第 4 条）：`idle`（未发起 / 失败后可重试）→ `restoring`（在途）→ `ready`（终态）。 */
export type SessionGate = 'idle' | 'restoring' | 'ready'

/** 会话恢复入参：仅受保护路由可开启刷新（公开页一律不触发，§2.8 第 2 条）。 */
export interface EnsureSessionOptions {
  /** 是否允许在内存态为空时发起一次静默恢复（仅受保护路由传 `true`）。 */
  allowRefresh?: boolean
}

/** 幂等键作用域（D-15）：注册 / 登录各自独立，`(operation, key)` 唯一确定一次逻辑提交。 */
type AuthOperation = 'register' | 'login'

/** Idempotency-Conflict 的业务码（与 HTTP 409 对齐，规范 6.4）。 */
const IDEMPOTENCY_CONFLICT_CODE = 409

/**
 * 是否已获得「明确业务结果」（D-15 第 6 条）：成功、业务拒绝（`code >= 1000`）与 `409` 均属之；
 * 网络错误（`-1`）/ 超时 / `429` / `5xx` 属「未获得业务结果」，重试必须沿用同一幂等键。
 */
function hasDefinitiveResult(error: unknown): boolean {
  const code = resolveErrorCode(error)
  return code >= 1000 || code === IDEMPOTENCY_CONFLICT_CODE
}

/**
 * 认证态 Store：仅承载登录态与用户信息（奖池 / 记录等页面态留在组件内，规范 3.3）。
 *
 * **存储口径（§2.8 第 1 条）**：会话态 = access token + 用户信息，**仅存内存**；
 * 禁止写 localStorage / sessionStorage——既不得作为凭证缓存，也不得作为「是否已登录」的判据。
 * 页面刷新 / 新标签页打开后内存为空是正常状态，由受保护路由触发一次静默刷新恢复。
 */
export const useAuthStore = defineStore('auth', () => {
  const user = ref<UserDto | null>(null)
  const sessionGate = ref<SessionGate>('idle')
  /** 在途恢复 Promise：`restoring` 期间的并发进入复用同一 Promise（§2.8 第 4 条）。 */
  let restorePromise: Promise<boolean> | null = null
  /** 注册 / 登录的幂等键（D-15）：同一逻辑提交的重试沿用同键，获得明确业务结果后重建。 */
  const submitKeys: Record<AuthOperation, string | null> = { register: null, login: null }

  const isAuthenticated = computed(() => user.value !== null)

  /** 落登录态（登录 / 注册成功与静默恢复共用同一结构）。 */
  function applySession(response: AuthResponse): void {
    user.value = response.user
    setAccessToken(response.accessToken)
  }

  /** 清空登录态（登出 / 会话失效 / 恢复失败）。 */
  function clearSession(): void {
    user.value = null
    setAccessToken(null)
  }

  /** 取当前逻辑提交的幂等键；不存在则生成（`utils/idempotency.ts` 为唯一出口）。 */
  function resolveSubmitKey(operation: AuthOperation): string {
    submitKeys[operation] ??= createIdempotencyKey()
    return submitKeys[operation]
  }

  /** 收到明确业务结果后作废该操作的幂等键（成功路径与业务拒绝路径共用）。 */
  function discardSubmitKeyOnDefinitiveResult(operation: AuthOperation, error: unknown): void {
    if (hasDefinitiveResult(error)) {
      submitKeys[operation] = null
    }
  }

  /** 注册（成功即登录，FR-01）；幂等键语义见 D-15。 */
  async function register(values: RegisterFormValues): Promise<void> {
    const key = resolveSubmitKey('register')

    try {
      const response = await authApi.register(values, key)
      submitKeys.register = null
      applySession(response)
    } catch (error) {
      discardSubmitKeyOnDefinitiveResult('register', error)
      throw error
    }
  }

  /** 登录（FR-02）；幂等键语义见 D-15。 */
  async function login(values: LoginFormValues): Promise<void> {
    const key = resolveSubmitKey('login')

    try {
      const response = await authApi.login(values, key)
      submitKeys.login = null
      applySession(response)
    } catch (error) {
      discardSubmitKeyOnDefinitiveResult('login', error)
      throw error
    }
  }

  /** 登出：服务端注销失败（网络异常等）不阻断本地登出，也不向调用方抛出。 */
  async function logout(): Promise<void> {
    try {
      await authApi.logout()
    } catch {
      // 本地登录态必须清空；服务端 refresh 会话由 Cookie 过期兜底
    } finally {
      clearSession()
    }
  }

  /**
   * 会话检查 / 恢复（§2.8）：
   * - 公开页（默认，`allowRefresh` 未开启）：只读内存态，**绝不发起刷新**（现有测试锁定该语义）；
   * - 受保护路由（`allowRefresh: true`）：内存态为空时发起一次静默恢复，`restoring` 期间并发复用同一 Promise，
   *   成功后写入与「登录成功」结构完全一致的内存态；失败则回 `idle` 允许后续导航重试（禁止定时器自动重试）。
   */
  async function ensureSession(options: EnsureSessionOptions = {}): Promise<boolean> {
    if (user.value !== null) {
      return true
    }

    if (options.allowRefresh !== true) {
      return false
    }

    if (sessionGate.value === 'ready') {
      // 终态：本次 SPA 生命周期内不再发起恢复
      return false
    }

    if (sessionGate.value === 'restoring' && restorePromise !== null) {
      return await restorePromise
    }

    sessionGate.value = 'restoring'
    restorePromise = restoreSession()

    try {
      return await restorePromise
    } finally {
      restorePromise = null
    }
  }

  /** 静默恢复：复用 401 无感刷新的同一在途 Promise（一次性轮换，禁止两路并发刷新）。 */
  async function restoreSession(): Promise<boolean> {
    const refreshed = await authApi.refreshSession()

    // 失败（1203 / 1204 / 网络 / 5xx）或旧后端缺 `user` 字段：不写任何会话态，闸门回 idle 允许再次导航重试
    if (refreshed === null || refreshed.user === undefined) {
      clearSession()
      sessionGate.value = 'idle'
      return false
    }

    applySession({
      accessToken: refreshed.accessToken,
      expiresIn: refreshed.expiresIn,
      user: refreshed.user
    })
    sessionGate.value = 'ready'
    return true
  }

  return { user, isAuthenticated, register, login, logout, clearSession, ensureSession }
})
