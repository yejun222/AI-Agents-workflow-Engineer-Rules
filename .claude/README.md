# .claude/settings.json — 权限配置说明

把 [CLAUDE.md](../CLAUDE.md)「四、必须确认的场景」固化为 Claude Code 的权限规则，与规范形成强制闭环。

**接入方式**：本目录随仓库提交，团队全员共享。个人差异（如本机路径）写在 `.claude/settings.local.json` 并加入 `.gitignore`，不提交。

## 规则与规范的对应关系

| 分组 | 规则 | 对应规范 |
| --- | --- | --- |
| **允许**（免确认） | 日常验证命令：build / lint / format / test（前端与后端）、`dotnet run` 本地启动、只读 git 命令（status/diff/log/show）、**规范自身强制要求的直调入口**：`node node_modules/@playwright/test/cli.js install chromium`（E2E 首次必做，不在 `npm ci` 范围内）、`node tests/e2e/lib/launch-api.mjs`（角色公共协议 §5 的唯一启动入口）、`node node_modules/vue-tsc\|vite/bin/*.js`（含 `&` / 空格路径下 `npm run *` 的替代入口） | CLAUDE.md「一、常用命令」与工作准则第 4 条（交付前必须验证）；直调入口覆盖 CLAUDE.md 命令表对含 `&` 路径的处置与 `docs/role-protocol.md` §5 —— ⚠️ **规范里点名要求的命令必须落在本表，否则无人值守时子代理会因问答态直接失败**（实测：§5 的两条命令长期不在 allow 内），新增规范要求时同步本表并由 `tools/check-config.py` T25 守护 |
| **确认**（弹出询问） | 新增/卸载依赖（npm、`dotnet add`）、**新建解决方案 / 项目脚手架（`dotnet new *` / `dotnet sln *` / `npm create *`，2026-09-18 由 allow 移入，见下）**、EF 迁移与数据库操作、新增 shadcn 组件、git 写操作（commit/push/merge/rebase 等）、Docker、删除文件（rm）、外网访问（WebFetch/WebSearch/curl/wget）、Windows 原生命令（powershell/cmd） | 「四、必须确认的场景」：新依赖、**架构选型 / 新增分层 / 修改目录结构（脚手架命令正是这一类，故必须走确认）**、数据库变更、破坏性操作；curl/wget 额外覆盖安全规范 8.4（防外传敏感信息） |
| **拒绝**（不可执行） | 修改 `src/components/ui/` 源码、修改 `lib/utils.ts`（shadcn-vue CLI 生成的 `cn()` 助手，与其组件一并只读）、读取 `.env` 全家族密钥文件（`.env` / `.env.local` / `.env.*.local`，含子目录递归）、force push、`rm -rf`、`npm publish` / `dotnet nuget push` | 3.2（shadcn 只读红线）、3.1（`lib/utils.ts` 只读）、8.4（敏感信息）、11.1（main 禁止 force push） |

未列入上表的命令按 Claude Code 默认模式处理（首次执行时询问）。⚠️ **但本表对 allow 组是「举例」而非「穷举」**（2026-09-18 实测：allow **22** 条 / ask **35** 条 / deny **18** 条）——`npm run dev*` / `dotnet restore*` 只写在 CLAUDE.md「一、常用命令」表里，未列本表。核对「哪些命令免确认」时**以 `settings.json` 的实际条目为准，本表不能反推**（T25 只绑规范点名要求的那几条）。

⚠️ **脚手架命令与「四、必须确认的场景」的冲突已于 2026-09-18 根治（护栏 T30 机检）**：`Bash(dotnet new *)` / `Bash(dotnet sln *)` / `Bash(npm create *)` 三条原在 **allow**（无条件免确认），而它们正对应「架构选型 / 新增分层 / 修改目录结构」——该场景要求**先征得同意**，两者直接冲突，且冲突长期无人发现（allow 组此前从未被任何断言覆盖）。现三条已移入 **ask**，与规范一致；T30 断言「**allow 不得含脚手架命令** 且 **ask 必须含这三条**」——**这是双向锁**：只断言其中一侧，放宽 allow 的改动仍可静默通过。

## agents/ 与 commands/（七角色流水线）

本目录还承载七角色 Subagent 流水线（说明见仓库 README「七角色 Subagent 研发流水线」章节）：

- `agents/*.md`：7 个角色子代理定义。frontmatter 的 `tools` 白名单已按职责最小化；上方 allow/ask/deny 规则对子代理的工具调用**同样生效**（如 engineer 跑 `dotnet ef` 仍会弹确认、任何角色读不了 `.env`）。
- `commands/*.md`：`/feature` 编排命令 + 6 个单步命令。frontmatter 的 `allowed-tools` 仅对本次调用免确认，不改变 settings.json 的权限规则。
- 一致性自检：`python3 tools/check-config.py`（Windows 无 python3 时用 `python` 或 `py`）。改动角色 / 命令 / 协议文档后重跑。

## 按实际仓库调整

1. **前端目录**：deny 中**已并存两种布局**的路径——`./src/components/ui/**` 与 `./src/frontend/src/components/ui/**`（含 `lib/utils.ts` 两条同理）。本仓前端在 `src/frontend/`，两条都保留：多出的一条只是指向不存在的路径、无害，**缺的一条才是漏防**。按实际布局调整时**必须两条一起改**——只改一条会让另一条继续指向不存在路径，表面「已配置」实则该布局下 shadcn 源码可被直接改写。
2. **包管理器**：团队使用 npm；若改用 pnpm/yarn，需在 ask 中补充 `Bash(pnpm add *)` 等规则。
3. **后端路径**：`dotnet run --project *`、迁移命令中的项目名以实际 `.sln` 为准（CLAUDE.md 已同步占位）。

## 已知局限（依赖人工兜底）

- **必须从仓库根目录启动 Claude Code**：从子目录启动会静默丢失项目级 `.claude/settings.json`（[官方 issue #74023](https://github.com/anthropics/claude-code/issues/74023)），团队请统一从仓库根启动。
- **前缀匹配**：权限规则按命令前缀匹配。`git push -f` 写在命令末尾（如 `git push origin main -f`）时 deny 规则拦不住——但所有 push 都已列入 ask，人工确认时能看到完整命令，这是最终防线。
- **Windows 原生命令与参数顺序绕过**：PowerShell（`Remove-Item -Recurse -Force`）、CMD（`del /s /q`）等原生命令已列入 ask 兜底，但 deny 无法识别其内部参数；`rm -r -f` 同理可绕过 `rm -rf` deny——所有 `rm *` 均会触发 ask 人工确认。
- **deny 只拦 AI 的工具调用**：`npx shadcn-vue add`（ask 放行后）由 CLI 进程写入 `ui/` 目录，不会触发 Edit/Write 拦截；靠规范 3.2 约束。
- **子代理命中 ask 规则的行为**：后台子代理自 v2.1.186 起把确认请求转发主会话等待批准（Enter 批准该次 / Esc 拒绝该次）；此前版本静默自动拒绝。有人值守时流水线是**暂停等确认**而非静默失败；但**非交互 / 无人值守模式（`-p` / dontAsk）直接按配置拒绝**，ask 类命令会失败。官方 sub-agents 文档页未更新此行为（issue #70143）。

## 全局开关说明

- `permissions.defaultMode: "default"`：默认权限模式，allow/ask/deny 之外的操作首次执行时询问。
- `permissions.disableBypassPermissionsMode: "disable"`：禁止绕过权限模式（AI 和人都无法跳过确认）。若团队觉得过严可删除此键——但不建议，它与规范「先确认再动手」原则一致。
