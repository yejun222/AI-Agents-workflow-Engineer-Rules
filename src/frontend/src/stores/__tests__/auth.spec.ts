import { createPinia, setActivePinia } from 'pinia'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import * as authApi from '@/api/auth'
import { useAuthStore } from '@/stores/auth'
import type { AuthResponse, RefreshResponse } from '@/types/auth'
import { getAccessToken, setAccessToken } from '@/utils/auth-token'
import { ApiError, NETWORK_ERROR_CODE } from '@/utils/error'

vi.mock('@/api/auth', () => ({
  login: vi.fn(),
  register: vi.fn(),
  logout: vi.fn(),
  refreshSession: vi.fn()
}))

const AUTH_RESPONSE: AuthResponse = {
  accessToken: 'access-token-1',
  expiresIn: 7200,
  user: { id: 7, userName: 'user_1' }
}

const REFRESH_RESPONSE: RefreshResponse = {
  accessToken: 'restored-token',
  expiresIn: 7200,
  user: { id: 7, userName: 'user_1' }
}

/** 幂等键格式断言用（规范 6.x：`Idempotency-Key` 为 UUID，D-15 同）。 */
const UUID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/

beforeEach(() => {
  setActivePinia(createPinia())
  sessionStorage.clear()
  localStorage.clear()
  setAccessToken(null)
  vi.clearAllMocks()
})

describe('stores/auth', () => {
  it('登录成功后写入内存令牌与用户信息，凭证与用户信息均不落 Web Storage（§2.8 第 1 条）', async () => {
    vi.mocked(authApi.login).mockResolvedValue(AUTH_RESPONSE)
    const store = useAuthStore()

    await store.login({ userName: 'user_1', password: 'Passw0rd' })

    expect(store.isAuthenticated).toBe(true)
    expect(store.user?.userName).toBe('user_1')
    expect(getAccessToken()).toBe('access-token-1')
    expect(sessionStorage.length).toBe(0)
    expect(localStorage.length).toBe(0)
  })

  it('注册成功即登录（FR-01）', async () => {
    vi.mocked(authApi.register).mockResolvedValue(AUTH_RESPONSE)
    const store = useAuthStore()

    await store.register({ userName: 'user_1', password: 'Passw0rd', confirmPassword: 'Passw0rd' })

    expect(store.isAuthenticated).toBe(true)
    expect(getAccessToken()).toBe('access-token-1')
  })

  it('登出接口失败时仍清空本地登录态且不向调用方抛出', async () => {
    vi.mocked(authApi.login).mockResolvedValue(AUTH_RESPONSE)
    vi.mocked(authApi.logout).mockRejectedValue(new Error('Network Error'))
    const store = useAuthStore()
    await store.login({ userName: 'user_1', password: 'Passw0rd' })

    await expect(store.logout()).resolves.toBeUndefined()

    expect(store.isAuthenticated).toBe(false)
    expect(store.user).toBeNull()
    expect(getAccessToken()).toBeNull()
  })

  it('公开页（不带 allowRefresh）不调用刷新接口', async () => {
    const store = useAuthStore()

    await expect(store.ensureSession()).resolves.toBe(false)
    expect(vi.mocked(authApi.refreshSession)).not.toHaveBeenCalled()
  })

  it('受保护路由凭 refresh Cookie 静默恢复，写入的内存态与登录成功结构一致（§2.8 第 5 条）', async () => {
    vi.mocked(authApi.refreshSession).mockResolvedValue(REFRESH_RESPONSE)
    const store = useAuthStore()

    await expect(store.ensureSession({ allowRefresh: true })).resolves.toBe(true)

    expect(store.isAuthenticated).toBe(true)
    expect(store.user).toEqual({ id: 7, userName: 'user_1' })
    expect(getAccessToken()).toBe('restored-token')
  })

  it('恢复失败（1203 / 1204 或网络异常）不写会话态，且回 idle 允许下次导航再试（§2.8 第 4 条）', async () => {
    vi.mocked(authApi.refreshSession).mockResolvedValueOnce(null)
    const store = useAuthStore()

    await expect(store.ensureSession({ allowRefresh: true })).resolves.toBe(false)
    expect(store.isAuthenticated).toBe(false)
    expect(getAccessToken()).toBeNull()

    // 再次导航到受保护路由：允许重试（失败不是终态）
    vi.mocked(authApi.refreshSession).mockResolvedValueOnce(REFRESH_RESPONSE)
    await expect(store.ensureSession({ allowRefresh: true })).resolves.toBe(true)
    expect(vi.mocked(authApi.refreshSession)).toHaveBeenCalledTimes(2)
    expect(store.isAuthenticated).toBe(true)
  })

  it('恢复响应缺 user 字段（旧后端）按失败处理，引导登录（API-03 兼容性新增）', async () => {
    vi.mocked(authApi.refreshSession).mockResolvedValue({ accessToken: 'restored-token', expiresIn: 7200 })
    const store = useAuthStore()

    await expect(store.ensureSession({ allowRefresh: true })).resolves.toBe(false)
    expect(store.isAuthenticated).toBe(false)
    expect(store.user).toBeNull()
  })

  it('并发进入受保护路由只发起一次刷新（在途 Promise 复用，避免 1203 复用检测）', async () => {
    vi.mocked(authApi.refreshSession).mockResolvedValue(REFRESH_RESPONSE)
    const store = useAuthStore()

    const [first, second] = await Promise.all([
      store.ensureSession({ allowRefresh: true }),
      store.ensureSession({ allowRefresh: true })
    ])

    expect(first).toBe(true)
    expect(second).toBe(true)
    expect(vi.mocked(authApi.refreshSession)).toHaveBeenCalledTimes(1)
  })

  it('恢复成功后的 ready 为终态：本次 SPA 生命周期内不再发起恢复', async () => {
    vi.mocked(authApi.refreshSession).mockResolvedValue(REFRESH_RESPONSE)
    const store = useAuthStore()
    await store.ensureSession({ allowRefresh: true })

    store.clearSession()

    await expect(store.ensureSession({ allowRefresh: true })).resolves.toBe(false)
    expect(vi.mocked(authApi.refreshSession)).toHaveBeenCalledTimes(1)
  })

  it('已登录时 ensureSession 直接返回 true 且不触发刷新', async () => {
    vi.mocked(authApi.login).mockResolvedValue(AUTH_RESPONSE)
    const store = useAuthStore()
    await store.login({ userName: 'user_1', password: 'Passw0rd' })

    await expect(store.ensureSession()).resolves.toBe(true)
    expect(vi.mocked(authApi.refreshSession)).not.toHaveBeenCalled()
  })

  it('注册 / 登录携带 UUID 形式的 Idempotency-Key（D-15）', async () => {
    vi.mocked(authApi.login).mockResolvedValue(AUTH_RESPONSE)
    vi.mocked(authApi.register).mockResolvedValue(AUTH_RESPONSE)
    const store = useAuthStore()

    await store.login({ userName: 'user_1', password: 'Passw0rd' })
    await store.register({ userName: 'user_1', password: 'Passw0rd', confirmPassword: 'Passw0rd' })

    expect(vi.mocked(authApi.login).mock.calls[0]?.[1]).toMatch(UUID_PATTERN)
    expect(vi.mocked(authApi.register).mock.calls[0]?.[1]).toMatch(UUID_PATTERN)
  })

  it('未获得业务结果（网络异常）时重试沿用同一幂等键，成功后才重建（D-15 第 6 条）', async () => {
    vi.mocked(authApi.login).mockRejectedValueOnce(new ApiError(NETWORK_ERROR_CODE, '网络异常'))
    vi.mocked(authApi.login).mockResolvedValueOnce(AUTH_RESPONSE)
    const store = useAuthStore()

    await expect(store.login({ userName: 'user_1', password: 'Passw0rd' })).rejects.toBeInstanceOf(ApiError)
    await store.login({ userName: 'user_1', password: 'Passw0rd' })

    const firstKey = vi.mocked(authApi.login).mock.calls[0]?.[1]
    const secondKey = vi.mocked(authApi.login).mock.calls[1]?.[1]
    expect(secondKey).toBe(firstKey)

    // 上一轮已成功（明确业务结果）→ 下一次逻辑提交重建键
    vi.mocked(authApi.login).mockResolvedValueOnce(AUTH_RESPONSE)
    await store.login({ userName: 'user_1', password: 'Passw0rd' })
    expect(vi.mocked(authApi.login).mock.calls[2]?.[1]).not.toBe(firstKey)
  })

  it('业务拒绝（1002）属明确业务结果：重试用新键，避免撞上服务端占位冲突（D-15 第 6 条）', async () => {
    vi.mocked(authApi.login).mockRejectedValueOnce(new ApiError(1002, '用户名或密码错误'))
    vi.mocked(authApi.login).mockResolvedValueOnce(AUTH_RESPONSE)
    const store = useAuthStore()

    await expect(store.login({ userName: 'user_1', password: 'Passw0rd' })).rejects.toBeInstanceOf(ApiError)
    await store.login({ userName: 'user_1', password: 'Passw0rd' })

    expect(vi.mocked(authApi.login).mock.calls[1]?.[1]).not.toBe(vi.mocked(authApi.login).mock.calls[0]?.[1])
  })
})
