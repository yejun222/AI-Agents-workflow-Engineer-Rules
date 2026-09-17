# AI Development Rules — Claude Code 开发规范包（含七角色多代理流水线）

Vue 3 + .NET 10 全栈项目的 Claude Code 规范模板：精简版工作规范（AI 每次会话加载）+ 全量规范（人工评审参考）+ 权限规则 + 分发指南 + **七角色 Subagent 研发流水线**（产品经理 → 原型 → 架构 → 研发 → 代码审查 → 测试设计 → 测试执行）。

## 文件结构

| 路径 | 用途 |
| --- | --- |
| [CLAUDE.md](CLAUDE.md) | 精简版工作规范，放**项目根目录**，AI 每次会话自动加载（含全部子代理） |
| [.claude/settings.json](.claude/settings.json) | 项目级权限规则（allow/ask/deny），随仓库提交，**对子代理的工具调用同样生效**；说明见 [.claude/README.md](.claude/README.md) |
| [.claude/agents/](.claude/agents/) | 7 个角色子代理定义（product-manager / prototype-designer / software-architect / engineer / code-reviewer / test-designer / test-executor），主对话按 description 调度 |
| [.claude/commands/](.claude/commands/) | `/feature` 全流程编排命令 + 6 个单步命令（`/prd` `/proto` `/arch` `/impl` `/review` `/test`） |
| [docs/development-spec.md](docs/development-spec.md) | 全量规范（15 章 + 附录），供人工评审、新人阅读；角色按需 Grep 定点读取 |
| [docs/error-codes.md](docs/error-codes.md) | 业务错误码注册表，新增业务错误码必须在此登记 |
| [docs/artifacts.md](docs/artifacts.md) | 流水线产物登记表（写入者 / 读取者 / ID 体系 / 失效传播矩阵） |
| [docs/state-machine.md](docs/state-machine.md) | 缺陷闭环状态机——发布结论的唯一权威定义 |
| [docs/role-protocol.md](docs/role-protocol.md) | 角色公共协议（交接 / 信息不足 / 摘要格式 / 冲突上报） |
| [docs/config-checklist.md](docs/config-checklist.md) | 配置交叉核对清单（需要人判断的条目） |
| [tools/check-config.py](tools/check-config.py) | 配置一致性自检脚本（`python3 tools/check-config.py`） |
| [global/CLAUDE.md](global/CLAUDE.md) | 个人全局版工作规范，复制到 `~/.claude/CLAUDE.md` |
| [global/settings.json](global/settings.json) | 个人全局版权限规则，合并进 `~/.claude/settings.json` |
| [docs/distribution.md](docs/distribution.md) | 如何让**所有新建项目**都遵守本规范并带上流水线（模板 / 全局 / 同步 三层方案） |

## 快速上手

1. **单个项目**：把 `CLAUDE.md`、`.claude/`、`docs/`、`tools/` 复制到仓库根目录，并把 `.claude/settings.local.json` 加入项目的 `.gitignore`。
2. **所有项目**：按 [global/](global/) 里的两个文件配置一次本机。
3. **团队分发与演进**：见 [docs/distribution.md](docs/distribution.md)。
4. **使用注意**：必须从仓库根目录启动 Claude Code（子目录启动会丢失项目级设置）。

---

# 七角色 Subagent 研发流水线

在 VSCode 的 Claude Code 官方插件中，用 **Subagent（子代理）** + **Slash Commands（斜杠命令）** 搭建一条「产品经理 → 原型 → 架构 → 研发 → 代码审查 → 测试」的可编排研发流水线。

## 角色清单

| 角色文件 | 角色 | 主输入 | 主产出 |
| --- | --- | --- | --- |
| product-manager.md | 产品经理 | 原始需求（brief） | `docs/10-prd.md` |
| prototype-designer.md | 原型设计工程师 | PRD + 原始需求 | `docs/20-prototype.html` |
| software-architect.md | 软件架构师 | PRD + 原型 | `docs/30-architecture.md` |
| engineer.md | 研发工程师 | 技术方案 + PRD + 原型 | `src/` + `docs/40-changelog.md` |
| code-reviewer.md | 代码审查工程师 | PRD + 技术方案 + 改动 + 代码 | `docs/60-review.md` |
| test-designer.md | 测试设计工程师 | PRD + 原始需求 + 原型 + 技术方案 | `docs/50-testcases.md` |
| test-executor.md | 测试执行工程师 | 用例 + PRD + 原始需求 + 原型 + 技术方案 + 改动 + 审查 + 代码 | `docs/51/52-*` + `tests/e2e/**` |

> 测试职责拆为「设计（不看代码，需求驱动）→ 执行（独立判定缺陷）」，且**最终发布结论由主对话按 `docs/state-machine.md` 判定**，测试角色只给事实与建议——防止运动员自己给自己判分。

## 安装

目标目录树（层级必须保持；`tools/` 与 `README.md` 必须放**项目根**，不能放进 `.claude/`，否则 `tools/check-config.py` 的路径前置断言会失败）：

```
项目根/
├── README.md
├── tools/
│   └── check-config.py
├── docs/
│   ├── artifacts.md
│   ├── state-machine.md
│   ├── role-protocol.md
│   └── config-checklist.md
└── .claude/
    ├── agents/      # 7 个角色文件
    └── commands/    # /feature + 6 个单步命令
```

本仓库已按此布局内置。分发到新项目时：把 `README.md`、`tools/`、`docs/` 复制到项目根；`agents/`、`commands/` 复制到项目的 `.claude/` 下（**不要整目录改名放进 `.claude/`**），分发方案见 [docs/distribution.md](docs/distribution.md)。

1. 打开 VSCode 插件里的 Claude Code，进入项目目录。
2. 确认角色已加载：输入 `/list-agents`（别名 `/peers`）会列出可调用的子代理与会话——**需 v2.1.224+ 且当前会话启用跨会话消息**，旧版本会报 `Unknown command`；更普适的方式是输入 `@` 看补全列表里有没有 `(agent)` 项，或直接说「用 engineer 角色做 X」。
3. 输入 `/feature <功能描述>` 开始走全流程；或按需用单步命令 `/prd` `/proto` `/arch` `/impl` `/review` `/test`。

## 使用方式

- **全流程**：`/feature <功能描述>`——按 PRD → 原型 → 架构 → 研发 → 代码审查 → 测试 → 缺陷闭环 → 交付 推进。每步完成后暂停等你确认（评审门），说「跳过评审 / 直接跑完」可一口气跑到底。
- **单步**：`/prd` `/proto` `/arch` `/impl` `/review` `/test`——只跑一个角色，例如已有 PRD 只想画原型就 `/proto`。
- **缺陷闭环**：由 `docs/state-machine.md` 决定（安全网 → 结论枚举 → 修复循环 a/b/c → 人工决断），不写在 `/feature` 里重复定义。

## 交接链

**执行主链**（= `/feature` 步骤顺序，每段箭头后为评审门暂停点）：

```
主对话 ──归档原始需求(00-brief)──▶ 产品经理 ──PRD(10)──▶ 原型设计(20)──▶ 架构师 ──技术方案(30)──▶ 研发 ──代码+改动(40)──▶ 代码审查(60)──▶ 测试设计(50)──▶ 测试执行(51/52)──▶ 主对话·状态机判定──▶ 交付
```

**产物依赖**（节点输入以 `docs/artifacts.md`「读取者」列为准；箭头只表示主交接物，角色还要回读更上游文件）：

- 研发：30（技术基准）+ 回读 10（语义基准）+ 20（表现基准，前端任务必读）
- 代码审查：回读 10 + 30 + 40 + `src/`（只读；不消费原型）
- 测试设计：回读 10 + 00 + 20 + 30（**禁读 `src/` 与 60**——需求驱动，不看代码也不看代码审查）
- 测试执行：50 + 回读 00/10/20/30/40/60 + `src/`
- **60-review 的去向**：研发（按 REV 编号修复）｜ 测试执行（未闭环致命/严重项纳入 51；`已接受（用户决断）` 项除外）｜ 主对话（评审门决策）——**不流向测试设计**
- **三条接受路径不要混**：
  - `/review` 阶段的「接受残留」只针对 **60 审查问题（REV-xx）**，标注 `已接受（用户决断）`，不走状态机；
  - 缺陷闭环的「带残留接受」只针对 **51 测试缺陷（BUG-xx）**，走 `docs/state-machine.md` 行 5；
  - 覆盖缺口（`docs/50-testcases.md` 中未执行 / 未覆盖的用例）**既不是 REV 也不是 BUG**，走 `docs/state-machine.md` **行 3.5**（`F6` 转达「用户已接受覆盖缺口」→ `有条件发布`）。

**回边（缺陷闭环）**：

```
测试执行 ──51 缺陷──▶ 研发修复(a) ──▶ 测试执行回归(b) ──▶ 最多 2 轮 ──▶ 人工决断
                                                                      ├─ 继续修复（不计入自动循环上限）
                                                                      ├─ 带残留接受 → 测试执行落盘「有条件发布（含残留缺陷）」
                                                                      ├─ 接受覆盖缺口 → 测试执行落盘「有条件发布」（行 3.5）
                                                                      └─ 终止
⚠ 安全网：51 存在未闭环致命/严重缺陷时，无论 52 结论为何，一律先进修复循环（docs/state-machine.md 行 0）
```

**审查回边（/review，独立于缺陷闭环）**：

```
研发 ──40 改动──▶ 代码审查(60) ──致命/严重──▶ 研发修复 ──▶ 重跑 60 ──最多 1 轮──▶ 人工决断
                                                                        ├─ 继续修复（不计入自动循环上限）
                                                                        ├─ 接受残留 → REV-xx 标「已接受（用户决断）」，不阻断后续、不纳入 51
                                                                        └─ 终止
```

## 机制说明

- **Subagent**：每个角色一个 Markdown 文件，frontmatter 声明 `name` / `description` / `tools` / `model` / `maxTurns` / `color`；正文是该角色的系统提示。子代理在独立上下文运行，**看不到主对话历史**，只通过文件交接产物（见 `docs/artifacts.md`）。
- **Slash Commands**：`commands/` 下的 `feature.md` 是编排命令，用主对话的 `Agent` 工具依次派发子代理。官方支持在 frontmatter 声明 `allowed-tools`（免逐次授权），本配置已声明（含派发工具 `Agent`）。
- **产物交接**：一切产物走 `docs/artifacts.md` 登记的路径；角色间不互相调用，不递归委派（角色 `tools` 白名单不含 `Agent` / `Task`）。
- **评审门**：默认每步完成后暂停给你确认；「待确认清单」「契约冲突上报」「部分完成」三种情况强制暂停。
- **缺陷闭环状态机**：`docs/state-machine.md` 是结论的**唯一权威定义**（可发布 / 有条件发布 / 有条件发布（含残留缺陷） / 不可发布（待确认接受） / 不可发布），`/feature` 与测试角色都只引用它。
- **变更控制**：`00-brief` 归档即冻结，需求变更以 `CR-xx` 追加；上游契约变更按 `docs/artifacts.md` 失效传播矩阵重跑下游并加失效标记。
- **技术规范接入**：CLAUDE.md（精简红线）由平台自动加载进每个子代理；各角色提示词按职责声明了 `docs/development-spec.md` 相关章节与 `docs/error-codes.md` 的定点读取（引用式，规范单一来源，不复制内容）。
- **已知优化（未启用）**：test-designer 输入不含 40-changelog，理论上第 4 步起可与 engineer 并行（关键路径 7 段 → 6 段）；为保持编排简单未启用，启用前见 `docs/config-checklist.md` E 节。

## 平台事实与版本要求（按官方文档核对）

> **核验日期：2026-09-15**——逐条对照官方文档（code.claude.com / docs.claude.com）与官方 changelog 核对。平台文档更新频繁，版本断言以核验日期为准；过期前重新核验，不要拿着旧结论改配置。

- **`/agents`**：v2.1.198 起**只打印提醒**（让你编辑 `agents/` 目录），不再有交互界面；不要用它确认角色加载。注意与 `claude agents` CLI 命令（后台 agent 会话管理 TUI）区分，那是另一个东西。
- **`/list-agents`**：列出子代理、团队队友与会话，别名 `/peers`。**需 v2.1.224+（macOS / Linux）或 v2.1.234+（Windows）且当前会话启用跨会话消息**；旧版本报 `Unknown command`。以本机实际行为为准，不识别时改用 `@` 补全验证角色加载。
- **`/doctor`**：其安装体检会报告**同一目录下重名**的 agent 文件并建议处理（v2.1.205 前会打开诊断屏、列出重复项并指明生效项）；跨嵌套目录的同名定义按「最接近工作目录者生效」（v2.1.178+），`/doctor` 不负责跨目录查重。
- **`/tasks`**：列出当前会话的后台工作（含已完成的子代理）；**子代理行上会显示其运行的模型**（v2.1.242+，设了 effort 也会显示）。用它核对实际模型，不要依赖 frontmatter 的预期。
- **子代理嵌套**：官方现行表述——子代理默认可以再生成子代理，**最多主对话下 3 层**（v2.1.219 起，此前为 1 层）；`CLAUDE_CODE_MAX_SUBAGENT_SPAWN_DEPTH` 可调（设 1 关闭嵌套，硬上限 5）。历史版本分层各来源说法不一，不逐一列。本配置在角色 `tools` 中不声明 `Agent`，从机制上禁止递归委派。
- **`maxTurns` 是硬性轮次上限**：达到后子代理停止，输出标记为 `partial`（partial 标记需 v2.1.246+），主对话可 resume 它继续。所以「完成定义」仍然必须写清楚，否则可能被轮次上限截断。
- **`allowed-tools`（命令）语义**：授权**仅对本次调用生效**，下一条消息即失效；它**不限制**哪些工具可用——未列出的工具仍可调用，只是仍受你的权限规则约束；要对整个会话预批准，应在 `settings.json` 的 `permissions.allow` 中加规则。
- **工具改名**：v2.1.63 起 `Task` 工具改名为 `Agent`，旧的 `Task(...)` 引用仍作为别名可用（官方确认范围为 settings 与 agent 定义）。本配置统一使用 `Agent`。
- **`settings.json` 默认角色**：顶层键 `"agent": "engineer"`（JSON 形态 `{ "agent": "engineer" }`）；命令行 `--agent` 可临时覆盖。
- **`memory` 字段**：官方原文 "Read, Write, and Edit tools are **automatically enabled**"（子代理管理记忆文件所需）。注意：与显式 `tools` 白名单冲突时的优先级官方未明确，且存在白名单覆盖自动启用的 bug 报告（issue #57507），启用前建议实测。关闭方式：`autoMemoryEnabled: false`（settings.json 的 JSON 布尔键）或 `CLAUDE_CODE_DISABLE_AUTO_MEMORY=1`（环境变量），两种形态不同、别混用。
- **plugin 形式的 agent**：支持 `memory` 等字段，仅 `hooks` / `mcpServers` / `permissionMode` 会被静默忽略；**缺 `name` 或 frontmatter 解析失败时仍会按文件名加载**（本地 `agents/` 目录则相反：缺 name 会被静默跳过）。本配置未用被忽略字段，可直接打包。
- **@ 提及语法**：输入 `@` 后跟角色名，从补全列表选带 `(agent)` 后缀的条目（与文件条目区分；插件角色显示 scoped name，如 `my-plugin:code-reviewer`）。**手打 `@agent-<name>` 形式（本地，如 `@agent-engineer`）时补全菜单只显示文件条目——官方文档记载的预期行为，提交后仍按角色派发**；插件手打为 `@agent-<plugin>:<name>`。裸手打 `@engineer` 会被当作文件引用，不要这样写。
- **加载优先级**：managed > `--agents` > 项目 `.claude/agents/` > 用户 `~/.claude/agents/` > plugin；嵌套目录同名时**最接近工作目录者生效**（v2.1.178+）。
- **后台子代理工具集缩小**：后台运行的子代理只保留内置工具子集（Read/Grep/Glob/Bash/Edit/Write/WebFetch 等）。本配置角色工具均在此集合内。
- **子代理与权限确认**：后台子代理命中 ask 规则时，v2.1.186 起**把确认请求转发主会话等待批准**（Enter 批准该次调用 / Esc 只拒绝该次调用，子代理继续其他工作）；此前版本为静默自动拒绝。官方 sub-agents 文档页仍写 auto-deny（滞后，见 issue #70143）。前台子代理继承权限模式正常弹确认。**非交互模式（`-p` / dontAsk / 无人值守）不等待、直接按配置拒绝**——无人值守跑流水线时 ask 类命令会直接失败。
- **description 预算**：官方——自定义子代理 description 合计超 15,000 tokens 时**启动警告**（不阻止加载），细节应写进各 agent 正文按需加载。本项目远低于该值。

## 注意事项

- **产物交接靠文件，不靠摘要**：摘要会被上下文压缩，角色间只认 `docs/` 下的文件（路径见 `docs/artifacts.md`）。
- **memory 的依赖**：角色文件未启用 `memory`，不需要依赖落盘行为；若后续自行启用，注意 memory 会自动获得 Read/Write/Edit、跨会话持久化，且受 `autoMemoryEnabled` 控制。
- **插件打包**：把 `.claude/agents/` 打包进插件后，plugin agent 的 scoped name 为 `<plugin>:<角色名>`；安全提示——别让 `description` 包含可注入内容。
- **平台假设**：路径按 POSIX 书写（Windows 下用 Git Bash / WSL）；回滚依赖 git（提交由你手动执行）；版本要求：Claude Code ≥ v2.1.246（partial 标记）且建议 v2.1.242+（/tasks 显示模型）。

## 前置条件

- 主会话持有子代理派发工具（`Agent`）：`/feature` 及单步命令在 frontmatter 已声明 `allowed-tools`（含 `Agent`），可免去逐次授权；但若 `settings.json` 中该工具被显式 **deny**，该声明无法覆盖，编排仍会中断。首次使用请先跑一个最小流程确认可用。
- 项目目录有 `docs/`（产物落盘）、`src/`（研发写入）、`tests/`（测试分区）目录；没有时让对应角色按 `docs/artifacts.md` 约定创建。
- 用 `tools/check-config.py` 做配置一致性自检：`python3 tools/check-config.py`（Windows 无 python3 时用 `python` 或 `py`），应全部通过；改动任何角色 / 命令 / 文档后重跑。
