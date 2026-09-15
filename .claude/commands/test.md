---
description: 只跑测试：用例设计（不读代码）→ 用例执行与质量评估
argument-hint: <补充说明>
allowed-tools: Agent, Read, Write, Edit, Glob, Grep, Bash
---

分两步调度测试子代理：

1. 先调度 test-designer：输入 `docs/10-prd.md` + `docs/00-brief.md` + `docs/20-prototype.html` + `docs/30-architecture.md` → 产出 `docs/50-testcases.md`（**禁读 src/**）。
2. 再调度 test-executor：输入 `docs/50-testcases.md` + `docs/10-prd.md` + `docs/00-brief.md` + `docs/20-prototype.html` + `docs/30-architecture.md` + `docs/40-changelog.md` + `docs/60-review.md`（如有）+ 实现代码 → 产出 `docs/51-defects.md`、`docs/52-qa-report.md`（事实 + 建议结论）、`tests/e2e/**`。

每步完成后把摘要转达给我确认后再派下一步；任一步含「待确认清单 / 契约冲突上报」时暂停等我处理。
