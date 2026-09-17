import { describe, expect, it } from 'vitest'

import { createIdempotencyKey } from '@/utils/idempotency'

/** UUID v4 形态（版本位 4、变体位 8/9/a/b）。 */
const UUID_V4 = /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/

describe('utils/idempotency', () => {
  it('生成 UUID v4 形态的幂等键', () => {
    expect(createIdempotencyKey()).toMatch(UUID_V4)
  })

  it('连续生成的键互不相同（D-03：每次新抽奖独立成键）', () => {
    const keys = Array.from({ length: 100 }, () => createIdempotencyKey())
    expect(new Set(keys).size).toBe(keys.length)
  })
})
