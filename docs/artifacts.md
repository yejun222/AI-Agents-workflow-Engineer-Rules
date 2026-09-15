# 产物登记表（artifacts registry）

**所有角色必须读本文件。** 产物交接的唯一通道是下表登记的路径与「读取者」列——角色文件里写的输入清单必须与下表一致（三向对齐：登记表 ↔ 角色声明 ↔ 编排命令）。产物内容靠文件传递，不靠返回摘要。

## 1. 产物 → 写入者 / 读取者

| 产物 | 路径 | 写入者 | 读取者 |
| --- | --- | --- | --- |
| 原始需求（brief） | `docs/00-brief.md` | 主对话 | product-manager / prototype-designer / test-designer / test-executor |
| PRD | `docs/10-prd.md` | product-manager | prototype-designer / software-architect / engineer / code-reviewer / test-designer / test-executor |
| 交互原型 | `docs/20-prototype.html` | prototype-designer | software-architect / engineer / test-designer / test-executor |
| 技术方案 | `docs/30-architecture.md` | software-architect | engineer / code-reviewer / test-designer / test-executor |
| 改动说明 | `docs/40-changelog.md` | engineer | engineer / code-reviewer / test-executor |
| 测试用例 | `docs/50-testcases.md` | test-designer | test-executor / engineer |
| 缺陷报告 | `docs/51-defects.md` | test-executor | engineer / test-executor / 主对话 |
| 质量评估 | `docs/52-qa-report.md` | test-executor | 主对话 / test-executor |
| 代码审查 | `docs/60-review.md` | code-reviewer | engineer / test-executor / 主对话 |

## 2. 写入范围速查（写入者只能写本行列出的文件）

| 角色 | 可写 | 只读（不可修改） |
| --- | --- | --- |
| 主对话 | `docs/artifacts.md`、`docs/00-brief.md`（含 CR 追加） | 各角色产物（只协调、不代写；最终结论由其交付总结签署） |
| product-manager | `docs/10-prd.md` | `docs/00-brief.md`、代码、原型、技术方案 |
| prototype-designer | `docs/20-prototype.html` | PRD、原始需求、技术方案、`src/` |
| software-architect | `docs/30-architecture.md` | `src/`、`docs/10-prd.md`、`docs/20-prototype.html` |
| engineer | `src/`、`tests/unit/**`、`tests/integration/**`、`docs/40-changelog.md` | `docs/10/20/30-*`、`docs/50-testcases.md`、`docs/51-defects.md`、`docs/60-review.md`；不擅自 git commit / push |
| code-reviewer | `docs/60-review.md` | 除 `docs/60-review.md` 外一切只读（`src/`、`tests/`、`docs/`） |
| test-designer | `docs/50-testcases.md` | `src/` 与实现代码（**禁读**）；`docs/00/10/20/30-*` 只读输入；不读 `docs/40-*` 与 `docs/60-*` |
| test-executor | `docs/50-testcases.md`（仅回归回写）、`docs/51-defects.md`、`docs/52-qa-report.md`、`tests/e2e/**` | `src/`（只读）；`docs/00-*` 至 `docs/40-*`（只读）；`docs/60-review.md`（只读，未闭环致命/严重项纳入 51） |

测试分区归属：`tests/unit/**` + `tests/integration/**` → engineer（设计并执行代码级测试，跑通后结果写回 `docs/40-changelog.md`）；`tests/e2e/**` → test-executor（按 50 用例执行，缺陷记入 51）。

## 3. ID 体系

| 前缀 | 含义 | 维护者 | 规则 |
| --- | --- | --- | --- |
| FR | 功能需求 | product-manager | 10-prd 内稳定编号 |
| AC | 验收标准 | product-manager | 10-prd 内稳定编号，关联 FR |
| MOD / API | 模块 / 接口 | software-architect | 30-architecture 内稳定编号，关联 FR |
| CHG | 改动项 | engineer | 40-changelog 追加编号，关联 FR / AC |
| TC | 测试用例 | test-designer | 50-testcases 内稳定编号，关联 AC |
| BUG | 缺陷 | test-executor | 51-defects 内编号，关联 TC / CHG（若源自代码审查，关联 REV） |
| CR | 需求变更 | 主对话 | 追加在 00-brief 末尾，不覆盖原文 |
| REV | 审查问题项 | code-reviewer | 60-review 内稳定编号（关联 CHG / MOD / API）；供 engineer 闭环回写、test-executor 判定「是否纳入 51」 |

## 4. 版本与冻结

- 契约文件（00/10/20/30/50/51/52/60）头部必须包含**元信息块**：产物、版本（v1 起，冻结后变更升 v2…）、冻结时间（ISO 8601）、上游依赖（引用文件名 + 版本）。
- **00-brief 归档即冻结**：需求变更不修改原文，以 `CR-xx` 追加在文件末尾（含变更说明、时间、影响范围），涉及下游契约时按第 5 节失效传播矩阵处理。

## 5. 失效传播矩阵（上游变更 → 必须重跑的下游）

| 上游变更 | 受影响下游 |
| --- | --- |
| 00-brief 变更 | 10-prd（先由 product-manager 更新 PRD，再按下一行扇出）、20-prototype、50-testcases、51-defects、52-qa-report（后四者为直接消费者） |
| 10-prd 变更 | 20-prototype、30-architecture、40-changelog、50-testcases、51-defects、52-qa-report、60-review |
| 20-prototype 变更 | 30-architecture、40-changelog、50-testcases、51-defects、52-qa-report |
| 30-architecture 变更 | 40-changelog、50-testcases、51-defects、52-qa-report、60-review、tests/unit/**（engineer 复核）、tests/e2e/**（test-executor 复核） |
| 40-changelog 变更 | 51-defects、52-qa-report、60-review |
| src/ 变更 | tests/unit/**、tests/integration/**、tests/e2e/** |
| 50-testcases 变更 | 51-defects、52-qa-report |
| 51-defects 变更 | 52-qa-report |
| 60-review 变更 | 51-defects、52-qa-report |

规则：受影响下游需重跑或人工复核，并在头部加**失效标记**（「上游 vX 变更，本产物基于 vY，待复核」）；不重跑不能进入下一阶段。
