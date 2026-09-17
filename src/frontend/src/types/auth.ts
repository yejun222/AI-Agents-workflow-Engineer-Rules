import { z } from 'zod'

/**
 * 认证相关类型与表单校验规则。
 * 规则常量与后端 `RequestValidators` 逐字一致（CR-01：密码 8-20 位且须同时含大写字母、小写字母与数字）。
 */
export interface UserDto {
  id: number
  userName: string
}

export interface AuthResponse {
  accessToken: string
  expiresIn: number
  user: UserDto
}

export interface RefreshResponse {
  accessToken: string
  expiresIn: number
  /**
   * API-03 于 2026-09-17 **兼容性新增**（v1 不升版）：受保护路由静默恢复会话时用户信息的唯一来源。
   * 旧后端可能缺失该字段——缺失时前端按恢复失败处理（引导重新登录，不得白屏，§2.8）。
   */
  user?: UserDto
}

export interface LoginRequest {
  userName: string
  password: string
}

/** 认证业务错误码（后端 `ErrorCodes`，全部取自 docs/error-codes.md）。 */
export const AUTH_ERROR_CODE = {
  systemBusy: 1001,
  validationFailed: 1002,
  userNameTaken: 1101,
  invalidCredentials: 1102,
  accountLocked: 1103,
  sessionInvalid: 1203,
  sessionExpired: 1204
} as const

/** 用户名规则（后端 `UserNamePattern`）。 */
export const USER_NAME_PATTERN = '^[A-Za-z0-9_]{4,20}$'

/** 密码规则（后端 `PasswordPattern`，CR-01）。 */
export const PASSWORD_PATTERN = '^(?=.*[a-z])(?=.*[A-Z])(?=.*\\d).{8,20}$'

/** 用户名错误消息（后端 `UserNameMessage`，逐字一致）。 */
export const USER_NAME_MESSAGE = '用户名需 4-20 位，仅字母、数字、下划线'

/** 密码错误消息（后端 `PasswordMessage`，CR-01 冻结文案，逐字一致）。 */
export const PASSWORD_MESSAGE = '密码需 8-20 位且同时含大写字母、小写字母与数字'

/** 确认密码错误消息（后端 `ConfirmPasswordMessage`，逐字一致）。 */
export const CONFIRM_PASSWORD_MESSAGE = '两次输入不一致'

const userNameField = z
  .string()
  .min(1, '请输入用户名')
  .regex(new RegExp(USER_NAME_PATTERN), USER_NAME_MESSAGE)

const passwordField = z
  .string()
  .min(1, '请输入密码')
  .regex(new RegExp(PASSWORD_PATTERN), PASSWORD_MESSAGE)

/** 注册表单 Schema（用户名 / 密码 / 确认密码，与后端 RegisterRequest 一一对应）。 */
export const registerSchema = z
  .object({
    userName: userNameField,
    password: passwordField,
    confirmPassword: z.string().min(1, '请再次输入密码')
  })
  .refine((values) => values.password === values.confirmPassword, {
    message: CONFIRM_PASSWORD_MESSAGE,
    path: ['confirmPassword']
  })

/** 注册表单值 / 注册请求体。 */
export type RegisterFormValues = z.infer<typeof registerSchema>
export type RegisterRequest = RegisterFormValues

/** 登录表单 Schema（登录不做强度校验，仅要求非空，与后端 LoginRequest 一致）。 */
export const loginSchema = z.object({
  userName: z.string().min(1, '请输入用户名'),
  password: z.string().min(1, '请输入密码')
})

/** 登录表单值 / 登录请求体。 */
export type LoginFormValues = z.infer<typeof loginSchema>
export type LoginFormRequest = LoginFormValues
