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
| 主对话 | `docs/artifacts.md`、`docs/00-brief.md`（含 CR 追加）、`docs/error-codes.md`（**仅架构期登记**，见下表后注） | 各角色产物（只协调、不代写；最终结论由其交付总结签署） |
| product-manager | `docs/10-prd.md` | `docs/00-brief.md`、代码、原型、技术方案 |
| prototype-designer | `docs/20-prototype.html` | PRD、原始需求、技术方案、`src/` |
| software-architect | `docs/30-architecture.md` | `src/`、`docs/10-prd.md`、`docs/20-prototype.html` |
| engineer | `src/`、`tests/unit/**`、`tests/integration/**`、`docs/40-changelog.md`、`docs/error-codes.md`（**仅实现期新增登记**，见下表后注） | `docs/10/20/30-*`、`docs/50-testcases.md`、`docs/51-defects.md`、`docs/60-review.md`；不擅自 git commit / push |
| code-reviewer | `docs/60-review.md` | 除 `docs/60-review.md` 外一切只读（`src/`、`tests/`、`docs/`） |
| test-designer | `docs/50-testcases.md` | `src/` 与实现代码（**禁读**）；`docs/00/10/20/30-*` 只读输入；不读 `docs/40-*` 与 `docs/60-*` |
| test-executor | `docs/50-testcases.md`（仅回归回写）、`docs/51-defects.md`、`docs/52-qa-report.md`、`tests/e2e/**` | `src/`（只读）；`docs/00-*` 至 `docs/40-*`（只读）；`docs/60-review.md`（只读，未闭环致命/严重项纳入 51） |

测试分区归属：`tests/unit/**` + `tests/integration/**` → engineer（设计并执行代码级测试，跑通后结果写回 `docs/40-changelog.md`）；`tests/e2e/**` → test-executor（按 50 用例执行，缺陷记入 51）。

> **`docs/error-codes.md` 的写入权按阶段归属**（本表是唯一权威；该文件**不属 §1 的产物清单**，是规范配套登记表）：**架构期**——software-architect 取号、主对话登记；**实现期**——engineer 取号并登记（依据 `docs/10-prd.md` FR-10 第 2 条「由 engineer 按 `docs/error-codes.md` 取号登记」）。两阶段的动作都只是**追加一行**：写入前必须先查表取号，已登记号码与含义一律不得修改或复用（细则见 `docs/error-codes.md` 文末「登记人」与「登记流程」）。
> ⚠️ 该文件此前**在本表中一行都没出现过**——于是「规范要求主对话/engineer 去登记」与「写入者只能写本行列出的文件」同时成立，两份文件**各自都没错**，任何单文件校验都看不出来（同型案例：`.claude/settings.json` 的 allow 缺口）。护栏 T28 已把本注与 `error-codes.md` 的「登记人」节绑死。

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
| OBS | 观察项（未立 BUG 的事实 / 契约空白 / 未审未执行项 / **审查未复核项**） | test-executor（51 / 52）、code-reviewer（60） | **按产物命名空间限定**：`51:OBS-nn` / `52:OBS-nn` / `60:OBS-nn` 各自独立序列；跨产物引用必须带文件名前缀（见下） |

### 3.1 观察项（OBS）与「未审 / 未执行」项

- **OBS 编号按产物命名空间限定**：`51-defects.md` 与 `52-qa-report.md` 各自维护独立序列，**跨产物引用必须写文件名前缀**（`51:OBS-08` / `52:OBS-08`），不得裸写 `OBS-08`。本仓库实测：同一事实（Playwright `outputDir` 清空）在 51 是 OBS-08、在 52 是 OBS-09，而 52 的 OBS-08 是**另一件事**；51 的序列还缺 09 / 10 —— **编号跨文件撞车且无人发现**，裸编号交叉引用必然指错。
  **护栏扫描面**：产物编号仅以 `51-defects.md` / `52-qa-report.md` / `60-review.md` 三份文件为扫描面（编号的载体）；**规范文件里的编号提及属元讨论，不在此列**。判定：这三份文件的**非引用块正文**里出现的裸 `OBS-nn`，必须**能在本文件的定义表中查到**（`| OBS-nn |` 行），或在**本文件的编号勘误块中登记过**（`>` 引用块，如 51 / 52 已有的「编号勘误」节）——两条都不满足即为**跨命名空间裸引用**，必须补前缀或补登记。勘误块是**登记披露**通道、不是豁免：它要求把编号冲突写明并留痕，正是为了防「编号跨文件撞车且无人发现」重演。
- **未审 / 未执行项必须与 OBS 同级管理**：凡「本轮未执行」「离线未审」「环境不具备故跳过」的验证项，一律登记为 OBS，且**必须含责任人 + 计划执行时点**；同一项跨轮次仍挂着时，在条目上追加轮次计数。实测出现过「依赖漏洞扫描离线未执行」跨 **4 个版本**仍未被处置 —— 只声明「如实未审」不足以让它被处理，必须有人和期限。
- **OBS 不是缺陷的降级收纳箱**：判为 OBS **必须写明「为何不立 BUG」，且理由必须写成可机械提取的归类标签**——与下条「阻断 / 非阻断」是同一机理：散文式理由读者与护栏都无法核对。**已登记归类**（自 2026-09-18 起）：`契约空白`（契约未定义 / 未要求该情形，如 `51:OBS-01`）/ `设计内`（架构已明确的既定语义、行为符合 FR，如 `51:OBS-02`）/ `可测性缺口`（契约使该分支或边界不可构造，如 `51:OBS-03`）/ `执行侧`（环境 / 证据 / 工具链 / 用例自身导致「测不了」，如 `51:OBS-05` / `06` / `07`）/ `工具行为`（工具链自身副作用，如 `51:OBS-08` 的 `outputDir` 每次运行前自动清空）。**归类可扩展，但必须先在本条登记后使用**，不得就地自造标签——不设边界的「有理由」会退化成「把不想处理的问题扫到地毯下」的通行证。⚠️ **既有条目（51 / 52 的 OBS-01～11）写于本规则之前，按 13.4 不回改**；自本次修订起新增的 OBS 必须带标签。
- **审查侧的「未复核项」同样必须登记**（`60:OBS-nn`，与测试侧同规则）：code-reviewer 每轮复审中**未能独立复核 / 未复跑 / 环境不具备 / 超出变更面**的项，一律逐条登记，含**缘由 + 责任人 + 计划复核时点**。只写「如实声明」不足以免除登记义务——本仓库实测：`docs/60-review.md` §H.6 与 §F.5 各 5 项未复核项**无编号、无责任人、无期限**，跨版本是否仍挂着**无从查证**；而测试侧的同类项（51 / 52 的未执行项）有台账有期限，待遇不对称。
- **两侧的 OBS 都必须逐条标注「阻断 / 非阻断」**（`docs/state-machine.md` D2 第 2 项；`52:OBS-nn` 由 test-executor 标注、`60:OBS-nn` 由 code-reviewer 标注）：标注是**发布门禁的输入**，必须是**可机械提取的结构化字段**，不得写成「不再构成任何阻断」这类散文式否定——**只登记不给权重的项等于永不处置**（本仓库实测有项跨 4 个版本仍挂着，见上条）。**漏标注者按「阻断」推定**：推定阻断才能逼登记方表态，沉默不得成为放行手段。**判别口径**（与 `docs/state-machine.md` D2 及两个角色文件逐字一致，由护栏绑定）：**环境 / 证据 / 工具链类**默认按**阻断**登记；仅在「该观察项不影响任何结论的成立」时才标 `非阻断`，并写明**为何不影响结论**。
- **「有理由的未复跑」是闭集，三类之外一律视为未复核**：① 变更**不触及该断言面**（须给出检索证据：检索命令 + 命中数，如「全量检索 `tests/integration` 对 `X` 零命中」）；② **环境客观不具备**（须登记 `60:OBS-nn` + 责任人 + 时点）；③ **已由其他角色在同一版本上复跑**（须给出证据路径 + 版本一致声明）。不符合任一类而跳过复跑 → 按 §5 失效传播矩阵补做，且该轮产物须带失效标记。

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
