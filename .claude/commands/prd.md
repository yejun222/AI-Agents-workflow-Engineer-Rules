---
description: 只跑产品经理：把需求写成 PRD
argument-hint: <需求描述>
allowed-tools: Agent, Read, Write, Edit, Glob, Grep, Bash
---

针对「$ARGUMENTS」调度 product-manager 子代理：

- 输入：`docs/00-brief.md`（若已归档）+ 本命令传入的需求描述
- 产出：`docs/10-prd.md`
- 若返回摘要含「待确认清单 / 契约冲突上报」，转达给我暂停确认。
