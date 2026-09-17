import { describe, expect, it } from 'vitest'

import {
  CONFIRM_PASSWORD_MESSAGE,
  PASSWORD_MESSAGE,
  USER_NAME_MESSAGE,
  loginSchema,
  registerSchema
} from '@/types/auth'

/** 返回注册表单中密码字段的校验消息集合。 */
function passwordIssues(password: string): string[] {
  const result = registerSchema.safeParse({
    userName: 'user_1',
    password,
    confirmPassword: password
  })

  if (result.success) {
    return []
  }

  return result.error.issues
    .filter((issue) => issue.path[0] === 'password')
    .map((issue) => issue.message)
}

describe('types/auth 注册表单 Schema（CR-01）', () => {
  it('冻结文案与后端 RequestValidators 逐字一致', () => {
    expect(PASSWORD_MESSAGE).toBe('密码需 8-20 位且同时含大写字母、小写字母与数字')
    expect(USER_NAME_MESSAGE).toBe('用户名需 4-20 位，仅字母、数字、下划线')
    expect(CONFIRM_PASSWORD_MESSAGE).toBe('两次输入不一致')
  })

  it('合法输入通过校验', () => {
    const result = registerSchema.safeParse({
      userName: 'user_1',
      password: 'Passw0rd',
      confirmPassword: 'Passw0rd'
    })

    expect(result.success).toBe(true)
  })

  it('缺少大写字母 / 缺少小写字母 / 缺少数字 均被拒绝', () => {
    expect(passwordIssues('passw0rd')).toEqual([PASSWORD_MESSAGE])
    expect(passwordIssues('PASSW0RD')).toEqual([PASSWORD_MESSAGE])
    expect(passwordIssues('Password')).toEqual([PASSWORD_MESSAGE])
  })

  it('长度越界（<8 或 >20）被拒绝', () => {
    expect(passwordIssues('Passw0r')).toEqual([PASSWORD_MESSAGE])
    // 21 位：大写 P + 小写 + 数字齐备，仅长度越界
    expect(passwordIssues('Passw0rdPassw0rdPassw')).toEqual([PASSWORD_MESSAGE])
  })

  it('边界长度 8 位与 20 位均通过', () => {
    expect(passwordIssues('Passw0rd')).toEqual([])
    expect(passwordIssues('Passw0rdPassw0rdPass')).toEqual([])
  })

  it('两次输入不一致时报错在确认密码字段', () => {
    const result = registerSchema.safeParse({
      userName: 'user_1',
      password: 'Passw0rd',
      confirmPassword: 'Passw0rd2'
    })

    expect(result.success).toBe(false)
    if (!result.success) {
      expect(result.error.issues).toHaveLength(1)
      expect(result.error.issues[0]?.path).toEqual(['confirmPassword'])
      expect(result.error.issues[0]?.message).toBe(CONFIRM_PASSWORD_MESSAGE)
    }
  })

  it('用户名规则：4-20 位且仅字母、数字、下划线', () => {
    const withName = (userName: string): boolean =>
      registerSchema.safeParse({ userName, password: 'Passw0rd', confirmPassword: 'Passw0rd' }).success

    expect(withName('abc')).toBe(false) // 低于下界（3 位）
    expect(withName('abcd')).toBe(true)
    expect(withName('USER_1234')).toBe(true)
    expect(withName('user-name')).toBe(false)
    expect(withName('a'.repeat(21))).toBe(false)
  })

  it('登录 Schema 仅要求用户名与密码非空', () => {
    expect(loginSchema.safeParse({ userName: 'user_1', password: 'x' }).success).toBe(true)
    expect(loginSchema.safeParse({ userName: '', password: 'x' }).success).toBe(false)
    expect(loginSchema.safeParse({ userName: 'user_1', password: '' }).success).toBe(false)
  })
})
