import { describe, expect, it } from 'vitest'

import { messages } from '@/utils/messages'

describe('utils/messages 冻结文案', () => {
  it('BUG-03：抽奖页奖池错误态标题为「奖池加载失败」，与记录页「加载失败」是两个独立口径', () => {
    // 原型状态定义表：抽奖页 error 态 =「奖池加载失败」；记录页 error 态 =「加载失败」。
    // 两页文案本就不同，不得合并为同一个键，故在此把两者同时钉住。
    expect(messages.prizes.loadFailedTitle).toBe('奖池加载失败')
    expect(messages.common.loadFailedTitle).toBe('加载失败')
  })

  it('BUG-04：结果条目重拉后仍不可定位时的兜底奖品名非空（「恭喜获得」后不得为空）', () => {
    // 兜底文案只在「重拉奖池一次后仍不可定位」时使用；此键被删 / 被清空都会让弹层重新出现
    // BUG-04 的空名称现象，故在此钉住其为非空冻结文案。
    expect(messages.draw.prizeUnknown.trim()).not.toBe('')
    expect(messages.draw.prizeUnknown).toBe('未知奖品')
  })
})
