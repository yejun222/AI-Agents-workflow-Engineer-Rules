import { mount } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'

import QuotaBadge from '@/components/business/QuotaBadge.vue'
import { messages } from '@/utils/messages'

/**
 * 组件渲染测试（模板层）—— 本仓库组件渲染测试基建的**第一个用例**，约定见
 * `docs/development-spec.md` 7.2。
 *
 * 存在的原因：此前本仓库无组件渲染测试基建，模板层缺陷（占位错、文案错、空名称）
 * 一律以「既定口径」豁免、全部推给 E2E；而 E2E 又长期未复跑 —— 两个缺口叠加 =
 * **整层无人验证**。基建已具备（`jsdom` + `@vue/test-utils` 均已在 devDependencies），
 * 故模板层改动**不得**再以「无基建」为由豁免。
 *
 * 断言口径：每条断言都对应一条**契约规则**，且都能区分「修复前 / 修复后」——
 * 例如 null 占位若被写成 `String(remaining)` 会渲染 `null`、写成 `remaining ?? 0`
 * 会渲染 `0`，两者都会被下方断言拦下。
 */
const mountBadge = (remaining: number | null, dailyLimit: number | null = null) =>
  mount(QuotaBadge, { props: { remaining, dailyLimit } })

describe('components/business/QuotaBadge 模板层渲染', () => {
  it('REV-01：remaining 为 null（未知）时渲染占位符，不得渲染为 0 也不得渲染为 null', () => {
    const wrapper = mountBadge(null)
    const text = wrapper.get('[data-testid="draw-quota"]').text()

    expect(text).toContain(messages.draw.quotaUnknown)
    // 双向排除：既不能退化成 0（把"未知"说成"用完了"），也不能把 JS 的 null 直接渲染出来
    expect(text).not.toContain('0')
    expect(text).not.toContain('null')
  })

  it('§2.6：仅当后端确认 remaining === 0 才用 destructive 语义', () => {
    // 用 find() 而非 get()：只有 find() 返回带 exists() 的包装器（get() 找不到会直接抛）
    expect(mountBadge(0).find('.bg-destructive').exists()).toBe(true)
    // 反例：未知不是 0，故不得使用 destructive（这正是 REV-01 要防的误判）
    expect(mountBadge(null).find('.bg-destructive').exists()).toBe(false)
    expect(mountBadge(3).find('.bg-destructive').exists()).toBe(false)
  })

  it('remaining 为已确认的正数时按原值渲染', () => {
    const text = mountBadge(3).get('[data-testid="draw-quota"]').text()

    expect(text).toContain('3')
    expect(text).toContain(messages.draw.quotaUnit)
  })

  it('dailyLimit 为 null（未知）时不渲染上限徽标 —— 不得渲染为「每日上限 0 次」', () => {
    const text = mountBadge(3, null).get('[data-testid="draw-quota"]').text()

    expect(text).not.toContain('每日上限')
  })

  it('dailyLimit 已知时渲染上限文案', () => {
    const text = mountBadge(3, 5).get('[data-testid="draw-quota"]').text()

    expect(text).toContain(messages.draw.dailyLimit(5))
  })
})
