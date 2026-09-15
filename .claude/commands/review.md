---
description: 只跑代码审查：对实现做只读审查并产出审查报告
argument-hint: <补充说明>
allowed-tools: Agent, Read, Write, Edit, Glob, Grep, Bash
---

调度 code-reviewer 子代理：

- 输入：`docs/10-prd.md` + `docs/30-architecture.md` + `docs/40-changelog.md` + `src/` + `tests/`
- 产出：`docs/60-review.md`（只读审查，不改代码）
- 若返回摘要含「待确认清单 / 契约冲突上报」，转达给我暂停确认。
