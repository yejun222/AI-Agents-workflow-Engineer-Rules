/**
 * 访问令牌的内存持有者：token **绝不落 localStorage / sessionStorage**（规范 3.3 安全要求）。
 * 单独成模块是为了打断 `request -> store -> api -> request` 的循环依赖。
 */
let accessToken: string | null = null

/** 读取当前访问令牌。 */
export function getAccessToken(): string | null {
  return accessToken
}

/** 写入（或清空）访问令牌。 */
export function setAccessToken(token: string | null): void {
  accessToken = token
}

/** 会话失效回调（刷新失败时由 request 层触发，用于清理前端态并跳转登录）。 */
type UnauthorizedHandler = () => void

let unauthorizedHandler: UnauthorizedHandler | null = null

/** 注册会话失效回调（应用启动时注册一次）。 */
export function setUnauthorizedHandler(handler: UnauthorizedHandler | null): void {
  unauthorizedHandler = handler
}

/** 触发会话失效处理。 */
export function notifyUnauthorized(): void {
  unauthorizedHandler?.()
}
