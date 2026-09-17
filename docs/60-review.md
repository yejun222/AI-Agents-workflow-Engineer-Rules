# 代码审查报告（code review）：转盘抽奖

| 项 | 内容 |
| --- | --- |
| 产物 | 代码审查报告（`docs/60-review.md`） |
| 版本 | **v6**（重跑轮：触发源 = `40-changelog` 新增 **CHG-18**（REV-21 修复：重试耗尽 CRITICAL 告警计数口径「尝试次数 → 重试次数」+ 守护性断言）；按 `docs/artifacts.md` §5「`40-changelog 变更` → 60-review」「`src/ 变更` → 测试」矩阵回跑；**CHG-18 定向复审（非全量重审）** + 21 项 REV 状态回写） |
| 失效标记 | **上游 `docs/40-changelog.md` 变更（CHG-18）与 `src/` 同步变更（`DrawService.cs` / `DrawServiceTests.cs`，2026-09-17 18:44–18:46），本产物已基于现行磁盘状态（复核窗口 2026-09-17 18:08 起）重跑复核。** |
| ⚠️ 事故说明 | `docs/60-review.md` v2 原文（513 行 / 82579 字节，含其内嵌的 v1 全文）于 **2026-09-17 15:16 因本角色一次工具误操作（空内容 Write 覆盖）丢失**；该文件从未入 git、无备份、**不可恢复**。本 v3 的 §A–§D 为本轮独立撰写内容（不依赖原文）；**§E 为「v2 重建内容」，明确标注为依 `40-changelog` CHG-13 段 + `51-defects.md:25` + `52-qa-report.md:95` 重建，非原文转录**；无法确证的 v2 章节已在 §E 如实声明未重建。另：v3 首次落盘曾因路径拼写偏差写入仓外平行目录（`…\Engineer Rules\feature\…`，非本仓），随后在正确仓内路径完成重写；仓外副本按本角色只读纪律未清理（事实披露，供主对话处置）。 |
| 冻结时间 | 2026-09-17 |
| 上游依赖 | `docs/10-prd.md` v2、`docs/30-architecture.md` **v4**、`docs/40-changelog.md`（CHG-13 / CHG-14 / CHG-15 / CHG-16 / CHG-17 / **CHG-18**）、`docs/51-defects.md`、`docs/50-testcases.md`（test-designer 并行更新中，本报告未依赖其新版本内容）、`docs/52-qa-report.md`、`docs/error-codes.md`、`docs/development-spec.md`、`docs/00-brief.md`、`CLAUDE.md` |
| 写入者 | code-reviewer |
| 轮次说明 | v1 首轮 19 项；v2 闭环复核 14 项 + 5 项用户裁决不纳入本轮；v3 = CHG-14 回跑（19 项回写 + 新增 REV-20）；v4 = CHG-15 回跑（REV-20 闭环）；v5 = CHG-16 / CHG-17 + 架构 v4 触发回跑（新增 REV-21）；**v6 = CHG-18 定向复审（非全量重审）**：① 复核 REV-21 修复（文案 / 传参 / 与 D-08 及同日志 WRN 行自洽性）；② 实跑复核守护断言（含本角色自选负向验证 N1′/N2′/N3′）；③ 改动集边界核验（是否夹带未声明改动）；④ CHG-18 记录属实性逐项核对；⑤ 21 项 REV 逐条回写（REV-21 → `已闭环`；5 项用户裁决残留按本轮调度指令转写为 `已接受（用户决断）`）。 |
| 审查方式 | 只读代码审查 + 只读验证（本角色本机实跑）。**v6 轮一手证据见 §H.2**（本角色 2026-09-17 18:49 起实跑）：改动集 mtime 扫描（`src/` 复核窗口内仅 `DrawService.cs` 18:44:48 与 `DrawServiceTests.cs` 18:46:28）；`dotnet build`（slnx）**0 警告 / 0 错误**；单测 **96 / 0 / 0**；守护用例定点 **1 / 0 / 0**；格式门禁（Application / UnitTests 两个涉及项目）**exit 0**；**负向验证 N0′–N4′ 于临时镜像目录施行、本仓 `src/` 零触碰（两文件 sha1 前后一致）**；生产日志（GBK 编码）定点复核。v5 轮证据（历史）见 §G.2。**未复跑** E2E（归 test-executor，见 §H.6）。 |
| 重跑结论 | **现行 21 项：已闭环 16（v5 的 15 + REV-21）/ 已接受（用户决断）5（REV-10/11/12/16/18，建议级，用户裁决不纳入本轮，v6 按调度指令转写标注）/ 未闭环 0**；**致命 / 严重未闭环 0 项**。CHG-18 审查结论：**REV-21 修复真实闭环** —— 终态告警改为「重试 {RetryCount} 次（共 {Attempts} 次尝试）」并传 `attempt - 1`（当前预算 = 1 次重试），与 D-08（`30-architecture.md:567`）「整个事务重试 1 次」及同日志 WRN「第 1 次」逐字自洽；守护断言实跑 96/0/0，且本角色独立负向验证对**三类回归（传参回退 / 常量漂移 / 文案改写）全部真实捕获**（§H.3-b）；改动集边界经 mtime 扫描无未声明文件（§H.2 #1）；CHG-18 记录与磁盘逐字一致、「上游契约变更 = 无」属实（§H.3-d）。 |

> **本轮（v6）复核后的下游重跑清单（`docs/artifacts.md` §5，供主对话调度）**：
> 1. **`60-review 变更` → `51-defects` / `52-qa-report`**（test-executor）：REV-21 已转 `已闭环`，`52-qa-report.md`（§「60 审查项状态」行 `:511`、残留清单 `:519`）与 `51-defects.md` 中引用 REV-21 `未闭环` 的表述需同步；**不得重开**用户已裁决项（REV-10/11/12/16/18）与 CHG-17 交付残留（前端 15s vs 后端 100s，用户裁决本轮不动）。
> 2. **`40-changelog 变更（CHG-18）` → `51` / `52` / `60`**：`60`（本文件 v6）已复核；`51` / `52` 若引用该 CRITICAL 文案需随第 1 条同步。
> 3. **`src/ 变更（`DrawService.cs`，仅日志文案）→ `tests/e2e/**`**（test-executor）：E2E 断言不解析该文案（脚本检索确认无解析逻辑，§H.3-d）；**可选核验点**：TC-56b 下一次以新 DLL 重建 Api 复跑时，实例日志终态行应显示「重试 1 次（共 2 次尝试）」（非断言项，不构成回归判据）。
> 4. **v5 遗留**：`30-architecture v4 → 50-testcases`（test-designer 并行进行中，本角色未触碰该文件）；`tests/e2e/**` 的 TC-48c / TC-56b 复跑（test-executor 已于 18:18–18:30 产出复跑日志，详见其 `51` / `52` 回写）。

---

## §A REV 逐项闭环判定表（21 项；v6 定稿）

> 判定口径：`已闭环` = 已在磁盘代码中确认修复落地；`已接受（用户决断）` = 用户决断不修、作为已接受残留记录（v5 原标注为「未闭环（用户已裁决不纳入本轮）」，v6 按本轮调度指令转写，见下方转写说明）。
> **v5 轮判定方式（与本轮改动无交集的历史证据不重复展开，边界以 mtime 扫描证明「锚点文件未被本轮触碰」）**：见 §G.2 #7 改动集扫描——v5 实际改动集 = 后端 5 生产文件（`WinningRecordRepository.cs` / `AuditService.cs` / `DrawRequestRepository.cs` / `DrawService.cs` / `DependencyInjection.cs`，17:43–18:05）+ 前端 3 生产文件（`useDrawFlow.ts` / `messages.ts` / `DrawView.vue`，17:43–17:52）+ 4 个测试文件 + CHG-17 的 `CommandTimeoutWiringTests.cs`；其余锚点文件均未被触碰。
> **v6 轮判定方式（本轮为 CHG-18 定向复审，非全量重审）**：① REV-21 重读 + 实跑 + 负向验证复核并转 `已闭环`（见 §H.3-a/b）；② 其余 20 项的锚点文件经 mtime 边界扫描（§H.2 #1）确认**不在本轮改动集**（`src/` 复核窗口内仅 `DrawService.cs` / `DrawServiceTests.cs` 两个文件），判定维持 v5；③ 5 项用户裁决残留按调度指令转写为 `已接受（用户决断）`。
> **转写说明**：REV-10/11/12/16/18 在 v2–v5 标注为「未闭环（用户已裁决不纳入本轮）」；v6 按**本轮调度指令**（明确转达用户已裁决、要求标注「已接受（用户决断）」）转写为 `已接受（用户决断）`；**接受人未具名**（按「用户」记录）；依据 = `40-changelog.md:275` 原裁决 + CHG-13 §E.4。判定实体内容未变。
> REV-01 / REV-07 / REV-14 / REV-17 的锚点位于 v5 轮被改动的前端文件内（v5 已重读复核）；REV-19 / REV-21 的锚点位于 `DrawService.cs` / `DrawServiceTests.cs`（v6 已重读 + 实跑复核）。

| 编号 | 严重度 | v6 闭环判定 | 一句话依据（代码证据，含复核方式） |
| --- | --- | --- | --- |
| REV-01 | 严重 | **已闭环** | 维持 v5 判定（重读锚点：`useDrawFlow.ts:67`、`DrawView.vue:249/:258`、`useDrawFlow.spec.ts:80`）；前端文件不在 v6 复核窗口改动集（§H.2 #1） |
| REV-02 | 严重 | **已闭环** | 维持 v3 判定（三态闸门 `stores/auth.ts:11,43,121-147`、仅内存口径、与 401 共用同一在途 Promise）；锚点文件均不在本轮改动集 |
| REV-03 | 一般 | **已闭环** | 维持 v3 判定（`Program.cs:213-216` 分区分键、`:190-192` 中间件顺序）；`Program.cs` 不在本轮改动集 |
| REV-04 | 一般 | **已闭环** | 维持 v3 判定（`IdempotencyKeyValidator.cs:12`、`DrawController.cs:44`、`AuthController.cs:50,67`）；三文件均不在本轮改动集 |
| REV-05 | 一般 | **已闭环** | 维持 v3 判定（`BcryptPasswordHasher.cs:42` `VerifyOrDummy`、`Interfaces/Services.cs:21`、`AuthService.cs:148`）；均不在本轮改动集 |
| REV-06 | 一般 | **已闭环** | 维持 v3 判定（认证幂等块 `AuthService.cs:190-262`、前端键生命周期、`AuthIdempotencyTests.cs` 7 例；两处抛点 HTTP 409 与 D-15（`30-architecture.md:623`）一致）；`AuthService.cs` 最后改动为 15:03（CHG-14），不在本轮改动集 |
| REV-07 | 一般 | **已闭环** | 维持 v5 判定（`DrawView.vue:69`、`messages.ts:71` `quotaResetHint`）；前端文件不在 v6 复核窗口改动集 |
| REV-08 | 一般 | **已闭环** | 维持 v3 判定（`DrawWheel.vue:39` `SETTLE_DURATION_MS = 2400`）；`DrawWheel.vue` 不在本轮改动集 |
| REV-09 | 一般 | **已闭环** | 维持 v4 判定（后端 `src` 全仓 `devonly` 0 命中，v4 实跑）；v6 边界核验：`appsettings.*` / `LuckyDraw.Api.csproj` / `LuckyDrawApiFactory.cs` 均不在本轮改动集。注：`LuckyDrawApiFactory.cs:24` 的测试容器口令 `devonly` 为既有已记录残留（非本轮引入，非部署凭证），不重开 |
| REV-10 | 建议 | **已接受（用户决断）** | 已接受残留（`40-changelog:275` 用户裁决不修）；全零密钥回退仍在（`AuthService.cs:281-284`、`Program.cs` JWT 回退），两文件均不在本轮改动集（§H.2 #1） |
| REV-11 | 建议 | **已接受（用户决断）** | 已接受残留（同上）；`RequestValidators.cs` 不在本轮改动集 |
| REV-12 | 建议 | **已接受（用户决断）** | 已接受残留（同上）；`Program.cs` 不在本轮改动集 |
| REV-13 | 一般 | **已闭环** | 维持 v4 判定（前端 `src` 全仓 `fonts.googleapis` 0 命中，v4 实跑）；`main.css` 不在本轮改动集 |
| REV-14 | 一般 | **已闭环** | 维持 v5 判定（`messages.ts` 迁移键均在，含 `records.paginationLabel` `:114`；`DrawView.vue` 无用户可见中文字面量）；前端文件不在 v6 复核窗口改动集 |
| REV-15 | 建议 | **已闭环** | 维持 v3 判定（`WinningRecordsView.vue:48` 分钟精度、`datetime.spec.ts:40-43`）；该文件不在本轮改动集 |
| REV-16 | 建议 | **已接受（用户决断）** | 已接受残留（同上）；`WinningRecordsView.vue` 回退分支不在本轮改动集 |
| REV-17 | 建议 | **已闭环** | 维持 v5 判定（`useDrawFlow.spec.ts:80`「首载即失败」用例在原位）；前端文件不在 v6 复核窗口改动集 |
| REV-18 | 建议 | **已接受（用户决断）** | 已接受残留（同上）；`utils/request.ts` / 路由守卫不在本轮改动集 |
| REV-19 | 建议 | **已闭环** | 维持 v5 判定（`DrawService.cs:30` 常量 = 2 并注明「即重试 1 次」、`:121` `when (attempt >= MaxTransactionAttempts)`、`:134` 终态 `BusinessException(SystemBusy)`；`DrawServiceTests.cs` 期望 `Received(2)`）；v6 复核：常量与判定行**原位未动**（§H.3-c 行号交叉核验），单测实跑 96/0/0 |
| REV-20 | 建议 | **已闭环** | 维持 v4 闭环；v6 复跑格式门禁：本轮涉及的 Application / UnitTests 两项目均 `exit 0`（§H.2 #5），其余 4 项目未改动、v5 全绿维持 |
| **REV-21**（v5 新增） | 建议 | **已闭环（v6，CHG-18）** | v6 复核（§H.3-a/b）：终态告警已改「重试 {RetryCount} 次（共 {Attempts} 次尝试）」并传 `attempt - 1`（`DrawService.cs:128-133`；预算 2 次尝试 → 报「重试 1 次」），与 D-08（`30-architecture.md:567`）「整个事务重试 1 次」逐字对齐、与同日志 WRN「第 1 次」互补；守护用例 `DrawServiceTests.cs:297-318` 实跑通过（全量 96/0/0），本角色独立负向验证 N1′/N2′/N3′ 全部捕获（§H.2 #6） |

**统计（v6 定稿）**：21 项中已闭环 **16**（v5 的 15 + REV-21）/ `已接受（用户决断）` **5**（REV-10/11/12/16/18，均建议级）/ **未闭环 0 项**；**致命 / 严重未闭环 0 项**。v6 轮新增发现 **0 项**（另记录 1 项低于门槛的残余观察：既有注释 `:544` 陈旧锚点，见 §H.3-d）。

---

## §H v6 定向复审记录（CHG-18 / REV-21 闭环）

> 本节为**本轮（v6）**记录；范围 = CHG-18 全部改动面（`DrawService.cs` 日志文案 + `DrawServiceTests.cs` 守护用例）+「其余 20 项判定不受本轮变更影响」的边界核验。结论一律以 `src/` / `tests/` 磁盘代码与**本角色实跑**为准（不采信 CHG 自述；其数字均经复算）。

### H.1 复审范围与触发

- 触发：`docs/40-changelog.md` 新增 **CHG-18**（索引 `:35`、正文 `:780-864`）；对象 = `docs/60-review.md` v5 §A REV-21 行 + §G.5（建议级、`未闭环`），**用户裁决「派 engineer 修」**。
- 依据矩阵：`docs/artifacts.md` §5「`40-changelog 变更` → 60-review」+「`src/ 变更` → `tests/unit/**` / `tests/integration/**`」（engineer 已跑，本角色复跑单测侧）。
- 涉及模块 / 接口：MOD-03（抽奖服务，事务宿主）；**不涉及** API-01…API-08、DTO、迁移、错误码、HTTP 状态码、前端。
- 上游契约变更声称 = **无**（D-08 `docs/30-architecture.md:567` 维持「整个事务重试 1 次 / 终态 `1001`（HTTP 200）/ CRITICAL 告警」三要素）；本轮核验：架构文件 mtime 不在复核窗口内（§H.2 #1），**属实**。

### H.2 一手证据（命令 / 操作 → 实测观测值；全部本角色实跑）

| # | 验证项 | 命令或操作（工作目录） | 实测观测值 |
| --- | --- | --- | --- |
| 1 | 改动集边界（mtime 扫描） | `find src tests -newermt "2026-09-17 18:08" -type f …`（排除 bin/obj/node_modules/dist/.artifacts；仓库根） | **`src/` 内仅 2 个文件**：`DrawService.cs`（18:44:48）、`DrawServiceTests.cs`（18:46:28）——与 CHG-18 声明改动集完全吻合；其余命中均为 test-executor 的 `tests/e2e/**`（18:18–18:30 的 TC-48c / 56b / 56c 复跑产物与 publish-stats）。**关键否定证据**：`MySqlErrors.cs`（12:03）、`DependencyInjection.cs`（18:05）、`PrizeRepository.cs`（12:04）、迁移目录、`Program.cs` 等均未触碰。另：`docs/` 窗口内变更 = `40-changelog.md`（18:47，CHG-18 正文）+ `50/51/52`（test-executor 18:36–18:37）+ 本文件（18:15，v5 落盘）；**`30-architecture.md` 未变**。 |
| 2 | 构建 | `dotnet build "src/backend/LuckyDraw.slnx" --nologo`（仓库根） | **0 警告 / 0 错误**，2.22 s，exit 0 —— 与 CHG-18 #1 一致 |
| 3 | 单测（全量） | `dotnet test "tests/unit/LuckyDraw.UnitTests" --nologo`（仓库根） | **通过 96 / 失败 0 / 跳过 0**，259 ms，exit 0 —— 与 CHG-18 #2 一致（95 → 96，净增 1） |
| 4 | 守护用例定点 | 同上加 `--filter "FullyQualifiedName~CriticalLogReportsRetryCount"` | **通过 1 / 失败 0**，123 ms，exit 0 |
| 5 | 格式门禁（本轮涉及的两个项目） | `dotnet format whitespace --verify-no-changes`（分别于 `src/backend/src/LuckyDraw.Application` 与 `tests/unit/LuckyDraw.UnitTests` 目录执行，未加 `--nologo`） | **两项均 exit 0**、无输出；其余 4 项目锚点文件不在本轮改动集（v5 六项全绿维持） |
| 6 | 负向验证（本角色独立施行） | 临时镜像 `C:/tmp/reviewer-v6-neg-a`（`cp --parents` 复制 `src/backend/src` + `tests/unit/LuckyDraw.UnitTests` 源码，保持仓内相对 ProjectReference 结构，排除 bin/obj）→ 五次突变运行；**本仓全程只读** | **N0′ 基线 1/0 通过 → N1′（传参回退）1/0 失败 → N2′（常量漂移）1/0 失败 → N3′（文案改写）1/0 失败 → N4′ 还原复绿 1/0 通过**（明细见下表）；本仓被审两文件 sha1 前后逐字节一致（#10） |
| 7 | 生产日志复核（REV-21 现实性） | 直读 `tests/e2e/logs/tc56-rerun-instance-20260917-182525.log`（**GBK 编码**，以 UTF-8 检索会 0 命中；另以 `iconv -f GBK` 转码二次复核，逐字一致） | `:60` = `[18:28:17 WRN] 抽奖事务遇数据库瞬时错误，重试整个事务（第 1 次）：UserId=631`；`:155` = `[18:29:07 FTL] 抽奖事务重试 2 次后仍遇数据库瞬时错误（死锁 / 锁等待超时），已放弃本次抽奖：UserId=631` —— 修复前两行自相矛盾，REV-21 系运维实际读到的告警（非理论问题） |
| 8 | 旧文案残留检索 | `grep -rn "重试 2 次" src/backend src/frontend/src` | **0 命中**（旧错位文案已从 src 全仓清除） |
| 9 | `LogCritical` / `MaxTransactionAttempts` 使用面 | `grep -rn … --include=*.cs`（src） | `MaxTransactionAttempts` 仅 `DrawService.cs:30`（定义）与 `:121`（判定）两处；`LogCritical` 生产代码仅 `DrawService.cs:128` 一处（无其他告警点） |
| 10 | 本角色是否触碰本仓（自证） | 会话开始 / 负向验证结束后两次 `sha1sum` 两个被审文件 | 两次采样**逐字节一致**：`DrawService.cs` = `f878b3d98ed095c9709be751ff5d6646334f4b3c`、`DrawServiceTests.cs` = `9e56aa54c8132ce99bf4fff7ad62cd58c735d980`；本仓 `src/` 未被本角色修改 |

**负向验证明细（临时镜像 `C:/tmp/reviewer-v6-neg-a` 内施行；本仓 `src/` 全程只读）**：

| # | 人为制造的回归 | 实测结果 | 还原 |
| --- | --- | --- | --- |
| N0′ | （基线）镜像源码原样 | **通过 1 / 失败 0**（exit 0，150 ms）—— 镜像环境成立 | — |
| N1′ | CRITICAL 传参回退：`attempt - 1` → `attempt`（复原 REV-21 原始缺陷） | **失败 1 / 通过 0（exit 1）—— 当场捕获**：`Expected message "抽奖事务重试 2 次（共 2 次尝试）…" to contain "重试 1 次（共 2 次尝试）"`（收到串即 REV-21 缺陷输出原形） | 已还原（以 N4′ 复绿佐证） |
| N2′ | 常量漂移：`MaxTransactionAttempts` 2 → 3 | **失败 1 / 通过 0（exit 1）—— 当场捕获**：`Expected attempts to be 2 … but found 3`（断言 ① 触发并给出「必须同步复核 D-08」提示） | 同上 |
| N3′ | 文案改写：模板删去「（共 {Attempts} 次尝试）」 | **失败 1 / 通过 0（exit 1）—— 当场捕获**：`Expected message "抽奖事务重试 1 次后仍遇…" to contain "重试 1 次（共 2 次尝试）"` | 同上 |
| N4′ | 还原后复跑（基线复绿） | **通过 1 / 失败 0**（exit 0，126 ms） | — |

> 尾注：**本仓 `DrawService.cs` / `DrawServiceTests.cs` 的 sha1 在本角色整个会话前后一致**（会话开始与负向验证后两次采样相同，完整值见上表 #10）；镜像内 N1′–N3′ 三处突变均为本角色脚本所加、只作用于 `C:/tmp/reviewer-v6-neg-a` 副本。

### H.3 重点复核项结论

**a) REV-21 是否真正闭环 —— 结论：是（逐字 + 推导核验）。**

- 现行代码（本角色全文读）：`DrawService.cs:128-133` `_logger.LogCritical(exception, "抽奖事务重试 {RetryCount} 次（共 {Attempts} 次尝试）后仍遇数据库瞬时错误（死锁 / 锁等待超时），已放弃本次抽奖：UserId={UserId}", attempt - 1, attempt, userId)`；`:125-127` 为三行新增注释（标注口径推导「重试次数 = 尝试次数 - 1」与守护断言落点）。
- **`{RetryCount}` 取值推导复核**：循环 `:115` `for (var attempt = 1; ; attempt++)`；终态条件 `:121` `when (attempt >= MaxTransactionAttempts)`（常量 = 2，`:30`）→ 触发时 `attempt = 2`（已发生的尝试次数）；**重试次数 = 尝试次数 − 1 = `attempt - 1` = 1** ✓。边界外推：若预算 = 1（不重试），公式给 0（「重试 0 次（共 1 次尝试）」）仍正确，**无 off-by-one**。
- **与 D-08 自洽**：`30-architecture.md:567`「死锁 1213 → 整个事务重试 **1 次**，仍失败 → `1001`（HTTP 200）+ CRITICAL 告警」—— 现文案输出「重试 1 次（共 2 次尝试）」，逐字对齐。
- **与同日志 WRN 行自洽**：WRN（`:138-143`，**未动**，与 CHG-18 声明一致）在 `attempt = 1` 时打印「重试整个事务（第 1 次）」= 预告即将进行的第 1 次重试（该行语义本就正确，v5 REV-21 明文要求「勿动」）；FTL 复盘「已重试 1 次（共 2 次尝试）」。两行互补，不再矛盾（修复前后对照见 §H.2 #7）。
- **`attempt` / 重试次数再次混用风险**：生产代码中该口径仅此一处（#9：`LogCritical` 全仓唯一）；`attempt` 的其余用法（循环推进 `:115`、WRN 参数 `:141`）语义未变。

**b) 守护断言是否真能拦住回归 —— 结论：能；本角色独立负向验证全部捕获（§H.2 #6 明细）。**

- 断言结构（`DrawServiceTests.cs:297-318`）：① `attempts.Should().Be(2, "重试预算（MaxTransactionAttempts）变更时必须同步复核 D-08 并更新本断言")`（`:313`）—— 以 **`Transactions.BeginAsync` 实际调用数**（= 尝试次数）钉住预算；② 告警正文 `Should().Contain($"重试 {attempts - 1} 次（共 {attempts} 次尝试）")`（`:315-317`）—— 断言的是**关系**（重试次数 = 尝试次数 − 1，随实测 `attempts` 变化），**不是硬编码的「1」**；③ `SingleCriticalLogMessage`（`:440-452`）先 `HaveCount(1)`（防漏打 / 重复），再经 logger 自带 formatter 渲染正文（与落盘一致）。
- **回归类型覆盖（全部实跑捕获）**：N1′（传参回退）→ 断言 ② 失败，收到串即缺陷原形（`抽奖事务重试 2 次（共 2 次尝试）…` 对期望 `重试 1 次（共 2 次尝试）`）；N2′（常量漂移）→ 断言 ① 失败（`Expected attempts to be 2 … but found 3`，显式提示复核 D-08；此时断言 ② 的期望串随 `attempts` 自适应为「重试 2 次（共 3 次尝试）」，与实现（`attempt - 1`）本应一致、不构成冲突）；N3′（模板改写）→ 断言 ② 失败（收到串 `抽奖事务重试 1 次后仍遇…` 不满足含「重试 1 次（共 2 次尝试）」）。N0′/N4′ 前后复绿证明镜像基线与还原成立。**三类静默漂移均被真实捕获。**
- **如实标注残余（不单列 REV，偏严不偏松）**：若改动者同时改常量 **且** 同步改断言 ① 的 `Be(2)`，断言 ② 会自适应放行 —— 这是**刻意的设计要求**（预算变更必须显式落测试改动并复核 D-08），非缺口；守护目标本就是「防静默漂移」而非「防显式变更」。

**c) 是否夹带未声明改动 —— 结论：无（mtime 边界 + 行号位移交叉核验）。**

- mtime 扫描（§H.2 #1）：`src/` 复核窗口内仅 `DrawService.cs`（18:44:48）与 `DrawServiceTests.cs`（18:46:28）两文件，无任何未声明文件；CHG-18 声称的「仅日志文案、不改业务行为 / 错误码 / HTTP 状态码」与文件集一致。
- **无 VCS（`src/` 未入 git：`git ls-files src` = 0；`git diff` 对本仓 src 不适用）**，故另做**行号位移交叉核验**：以 18:28–18:29 生产日志（**修复前**二进制）的 stack trace 行号（`DrawService.cs:119 / :194 / :246`）对照现行文件 —— `:119`（`return await ExecuteDrawAsync`）**未位移**；`:194 → :198`（`WinningRecordRepository.AddAsync` 调用行）、`:246 → :250`（`throw;`）**均 +4**，恰等于「新增 3 行注释 + 新增 1 行传参」；再对照 v5 §G.3-c 记录的修复前锚点（`:30` 常量、`:115` 循环、`:121` catch 判定）**均原位**。**结论：`DrawService.cs` 的全部改动 = 3 行注释 + `LogCritical` 模板 / 传参（+1 行），除此外逐行可对齐，无夹带。**
- 测试侧：`DrawServiceTests.cs` 的改动 = 新增 1 个 `[Fact]`（`:290-318`）+ `Harness` 可选形参 / `Logger` 属性（`:463 / :467 / :535-536`）+ `SingleCriticalLogMessage`（`:435-452`）；其余用例用法未变（全量 96 = 95 + 1，#3 实跑佐证）。

**d) `docs/40-changelog.md` CHG-18 记录属实性 —— 结论：逐项属实、要素完整。**

- 改前 / 改后逐字对照（`:798-801`）：改后模板与 `DrawService.cs:130` **逐字一致**；改前模板形态（`{Attempt}` 单参 + 传 `attempt`）与生产日志 `:155` 的输出形态吻合；「改前 `:127` / 改后 `:130`」的行号差（+3）与「新增 3 行注释」的推导一致（§H.3-c 交叉核验）。
- `{RetryCount}` 推导（`:810-815`）：与 §H.3-a 独立复算一致。
- 守护断言落点（`:817-825`）：与磁盘逐项一致（用例名 / 紧邻既有 `…DegradesTo1001AfterRetryBudget` / `ReceivedCalls()` 机制 / `Harness` 可选形参 / 缺省 `NullLogger` 回落）。
- 「上游契约变更 = 无」（`:784`）：核验属实（§H.2 #1：架构文件未被触碰）。
- 影响面（`:846-850`）：如实声明「结构化日志字段名变化（`{Attempt}` → `{RetryCount}` + 新增 `{Attempts}`）」；本角色复核其「本仓无消费方」声称 —— `tests/e2e/**` 脚本（`tc56b-tx-rollback.mjs` 等）仅断言 HTTP / `code` / 耗时与库内三方一致，**不解析该文案**（对 `重试|RetryCount` 的命中均为注释 / 预算说明，无解析逻辑），属实；历史日志保留旧文案为既成事实。
- 回滚方式（`:852-855`）：与磁盘行号（`:125-133`）一致、可执行；「不建议单独回滚守护断言」的理由成立。
- **如实记录未改项（engineer 已声明）**：既有注释中的架构锚点 `:544`（`DrawService.cs:27 / :123 / :124`）系架构 v3 行号，v4 已移至 `:567`；CHG-18 仅在**新增**注释行按 `:567` 引用，未修正既有锚点。属陈旧行号引用（注释级、无行为影响），**低于 REV 门槛，记录为残余观察**（同 §D 体例），不单列新 REV、不阻断。

### H.4 对 21 项 REV 状态的影响

- REV-21：`未闭环` → **`已闭环`**（依据：§H.3-a 修复落地 + §H.3-b 守护实跑与负向验证 + §H.2 #2–#5）。
- REV-10 / REV-11 / REV-12 / REV-16 / REV-18：按**本轮调度指令**由 v5 的「未闭环（用户已裁决不纳入本轮）」转写为 **`已接受（用户决断）`**；接受人未具名（按「用户」记录）；依据 = `40-changelog.md:275` 原裁决 + CHG-13 §E.4。五者锚点文件均不在本轮改动集（§H.2 #1），判定实体内容未变。
- 其余 15 项：维持 v5 判定（锚点文件不在本轮改动集；§H.2 #1）。

### H.5 契约符合度与安全审查（CHG-18 增量）

- **契约符合度**：D-08（MOD-03，`30-architecture.md:567`）三要素在**日志口径**上亦对齐（重试 1 次 / 终态 `1001` / CRITICAL 告警）；终态错误码与 HTTP 状态码**未变**（`DrawService.cs:134`，两参 `BusinessException` → HTTP 200，`ErrorCodes.SystemBusy`）；无接口结构 / DTO / 迁移 / 前端变更（§H.2 #1 边界扫描）。
- **安全审查（OWASP 增量）**：日志参数仍仅 `attempt - 1` / `attempt`（int）与 `userId`（int）+ 异常对象，**无密码 / Token / 密钥 / 个人敏感信息新增**；无新增依赖 / 外链 / SQL 面；无 `v-html` / 注入面变化。依赖漏洞扫描仍为离线未执行（沿用 v2 未审声明）。

### H.6 本轮未完成 / 无法独立复核的项（如实声明）

1. **E2E 未复跑**（TC-56b 等）：归 test-executor；本轮变更仅日志文案，E2E 断言不涉及该文案（§H.3-d）。可选核验点：下次以新 DLL 重建 Api 后，实例日志终态行应为「重试 1 次（共 2 次尝试）」。
2. **集成测试未由本角色复跑**：CHG-18 声明 43/0/0；本轮变更不触及集成断言面（`tests/integration` 无 `LogCritical` / `CRITICAL` 断言，检索 0 命中），且单测已独立复跑；如需从严由主对话调度。
3. **格式门禁仅复跑涉及的两个项目**：Application / UnitTests exit 0；其余 4 项目锚点文件未变（v5 六项全绿维持）。
4. **无版本控制级 diff**（`src/` 未入 git）：改动集边界以 mtime 扫描 + 行号位移交叉核验（§H.3-c），非密码学证明。
5. **负向验证的强度边界**：已在源码镜像（`C:/tmp/reviewer-v6-neg-a`，`cp --parents` 复制两个源码树、按仓内相对 ProjectReference 还原结构）内顺利执行，五次运行退出码 0 / 1 / 1 / 1 / 0 明确，N0′ / N4′ 前后复绿排除环境噪声；镜像构建依赖本机 NuGet 缓存 / 源，非生产 CI 环境，其结论按「定向复核证据」量级使用（与仓内全量单测 96/0/0 互证）。

---

## §G v5 定向复审记录（CHG-16 / CHG-17 + 架构 v4）

> 本节为**v5 轮**记录；以下 §B–§F 为 v3 / v4 历史轮次记录（保持原文，v6 未改动其判定）。v5 轮为**定向复审**：范围 = CHG-16 / CHG-17 全部改动面 + 架构 v4 两处改判的契约符合度 + 「其余 REV 判定不受影响」的边界核验。结论一律以 `src/` / `tests/` 磁盘代码与**本角色实跑**为准，CHG 自述仅作线索。

### G.1 复审范围与触发

- 触发 1：`docs/40-changelog.md` 新增 **CHG-16**（索引 `:33`、正文 `:595-706`）与 **CHG-17**（索引 `:34`、正文 `:708-775`）——BUG-04 / BUG-05 缺陷闭环 + 用户裁决落地。
- 触发 2：`docs/30-architecture.md` **v3 → v4**（文首「v4 变更说明」`docs/30-architecture.md:12-31`）：① D-08「异常与重试」终态错误码 `500` → **`1001`（HTTP 200）**（现行正文 `:567-568`，即 v3 文中标注的 `:544`），「整个事务重试 1 次」「CRITICAL 告警」维持不变；② API-07 错误语义（现行 `:934`，即 v3 文中标注的 `:910`）`1001` 语义显式扩为「含 D-08 瞬时错误整事务重试耗尽的终态」；改判理由：原 `500` 描述的是**缺陷状态本身**（修复前未分类、冒泡到全局 ExceptionFilter 兜底），且 `docs/error-codes.md:32` 明文「`500` 为全局 ExceptionFilter 兜底，业务代码禁止手动使用」。
- 依据矩阵：`docs/artifacts.md` §5「`40-changelog 变更` → 60-review」+「`30-architecture 变更` → 60-review」；架构 v4 文首失效传播表 `docs/30-architecture.md:29` 明确要求本报告「复核 CHG-16 实现与本行三要素一致（终态 `1001` / 重试 1 次 / CRITICAL 告警）」。
- 涉及模块 / 接口：MOD-03（抽奖服务）、MOD-05（抽奖流水）、MOD-04（中奖记录）、MOD-06（审计）、MOD-10（抽奖页）、MOD-02/03（奖池）；API-07（错误语义，无接口结构变更）；不涉及迁移 / DTO / 接口版本变更。

### G.2 一手证据（命令 / 操作 → 实测观测值；全部本角色实跑）

| # | 验证项 | 命令或操作（工作目录） | 实测观测值 |
| --- | --- | --- | --- |
| 1 | 格式门禁（本地 6 项目，逐项目录执行、未加 `--nologo`） | `dotnet format whitespace --verify-no-changes`（Domain / Application / Infrastructure / Api / UnitTests / IntegrationTests 六个项目目录） | **六项均 `exit=0`**（含 CHG-17 修复过的 IntegrationTests，`TransientErrorClassificationTests.cs` 空白问题已不复现） |
| 2 | 单测 | `dotnet test "tests/unit/LuckyDraw.UnitTests" --nologo`（仓库根） | **通过 95 / 失败 0 / 跳过 0**，287 ms，exit 0 —— 与 CHG-16/17 声称一致（95 = CHG-14 基线 89 + 6 个 BUG-05 编排用例；CHG-17 未增删用例） |
| 3 | 集成测试 | 先 `dotnet build "tests/integration/LuckyDraw.IntegrationTests" -p:OutputPath="C:/tmp/reviewer-v5-int" -m:1 --nologo`，再 `dotnet test … --no-build -p:OutputPath="C:/tmp/reviewer-v5-int" --nologo`（仓库根） | 构建 **0 警告 / 0 错误**；测试 **通过 43 / 失败 0 / 跳过 0**，14 s（真实 MySQL 3307 + Redis）；与 CHG-17 声称 43 一致（CHG-16 基线 42 + `CommandTimeoutWiringTests` 1 例） |
| 4 | 守护用例定点复跑 | 同 #3 加 `--filter "FullyQualifiedName~CommandTimeout"`；`--filter "FullyQualifiedName~TransientErrorClassification"` | 分别 **通过 1 / 0 失败**（50 ms）与 **通过 3 / 0 失败**（4 s）—— 接线守护与三写入点分类用例均在真实环境实跑通过 |
| 5 | 前端测试 | `node node_modules/vitest/vitest.mjs run`（`src/frontend`） | **Test Files 7 passed (7) / Tests 58 passed (58)**，1.47 s —— 与 CHG-16/17 声称一致（CHG-14 基线 54 + 4 个 BUG-04 用例） |
| 6 | 前端类型 / 静态检查 | `node node_modules/vue-tsc/bin/vue-tsc.js --noEmit`；`node node_modules/eslint/bin/eslint.js . --max-warnings 0`（`src/frontend`） | 均 **exit 0**，无输出 |
| 7 | 改动集边界（mtime 扫描） | `find src tests -newermt "2026-09-17 15:55" …`（排除 bin/obj/node_modules/dist/.artifacts） | **与本轮声明改动集完全吻合、无未披露文件**：后端 5 生产文件（`WinningRecordRepository.cs` 17:43 / `AuditService.cs` 17:43 / `DrawRequestRepository.cs` 17:43 / `DrawService.cs` 18:03 / `DependencyInjection.cs` 18:05）；前端 3 生产文件（`useDrawFlow.ts` 17:43 / `messages.ts` 17:52 / `DrawView.vue` 17:52）；测试 4 文件（`IntegrationFixture.cs` 17:44 / `useDrawFlow.spec.ts` 17:48 / `messages.spec.ts` 17:52 / `DrawServiceTests.cs` 18:03 / `CommandTimeoutWiringTests.cs` 18:03 / `TransientErrorClassificationTests.cs` 18:06）；`tests/e2e/**` 为 test-executor 并行产物。**关键否定证据**：`MySqlErrors.cs`（12:03）、`TransientDataException.cs`（12:03）、`PrizeRepository.cs`（12:04）、`UserDrawQuotaRepository.cs`（12:04）均**早于本轮窗口、未被触碰** |
| 8 | `GetCommandTimeout()` 语义（官方文档） | 读 `~/.nuget/packages/microsoft.entityframeworkcore.relational/10.0.11/lib/net10.0/Microsoft.EntityFrameworkCore.Relational.xml` 的 `<returns>` 节点 | 「The timeout, in seconds, **or null if no timeout has been set**」——该 API 返回的是**被显式设置的值**，未接线时为 `null`（判定依据见 G.3-b） |

### G.3 重点复核项结论（engineer 自述均经独立核验）

**a) 三处新增瞬时错误分类的一致性与 `MySqlErrors.cs` 未改动 —— 结论：一致，未越界。**

- 三个新抛点（本角色全文读码）：`WinningRecordRepository.cs:27-38`（catch 过滤器 `:31`，Detach `:36`，`throw new TransientDataException("中奖记录写入遇数据库瞬时错误", exception)` `:37`）；`AuditService.cs:53-63`（catch `:57`，Detach `:61`，throw `:62`）；`DrawRequestRepository.cs:69-86`（catch `:81`，throw `:85`，**无 Detach 正确**——`ExecuteUpdateAsync` 不走变更跟踪）。三者均与既有模式**逐字同构**：`catch (Exception exception) when (MySqlErrors.IsTransient(exception))` → `throw new TransientDataException("…", exception)`，对照组 `PrizeRepository.cs:77-81`、`UserDrawQuotaRepository.cs:94-99`（后者另有 `DetachPendingQuotas()`，与 `:36` 的 Detach 语义等价：重试前清理残留 `Added` 实体）。
- **六个抛点齐全**（本角色逐处读到）：① `DrawRequestRepository.cs:47`（占位遇死锁）、② `UserDrawQuotaRepository.cs:98`（扣次）、⑤ `PrizeRepository.cs:80`（扣库存）、⑥ `:37`、⑦ `:62`、⑧ `:85`；与 `DrawService.cs:121/132` 的 `catch (TransientDataException)` 重试/终态两分支构成完整链路。
- **`MySqlErrors.cs` 未被改动（未越界）**：现文件（全文读）仅含 1062/1205/1213 三个常量与 `IsTransient = IsDeadlock || IsLockWaitTimeout`，**命令超时错误号未被并入瞬时判定**（即 CHG-16 声明的「方案 (a)」而非「方案 (b)」）；mtime **12:03:27** 早于本轮窗口（17:43–18:06）——无版本控制级 diff（`src/` 未入 git），以 mtime + 语义读码双重核验，结论：语义未被改动、无越界。参照组 `PrizeRepository.cs` / `UserDrawQuotaRepository.cs`（mtime 12:04）同证未被「顺手改写对齐」。

**b) `CommandTimeoutWiringTests` 是否真钉住「接线」—— 结论：是；两种回归均拦得住（判定依据如下，非采信自述）。**

- 断言结构（`CommandTimeoutWiringTests.cs:31-49`）：从测试宿主容器解析 `AppDbContext`（`:33-34`，宿主走真实 `AddInfrastructure` 装配）→ `dbContext.Database.GetCommandTimeout()` 读回**配置中实际生效的值**（`:37`，公开 API，非源码常量）→ `SHOW VARIABLES LIKE 'innodb_lock_wait_timeout'` 读回**服务端实际值**（`:52-64`，不假设 50）→ 断言 `NotBeNull`（`:40`）且 `BeGreaterThan(serverLockWait)`（`:45`）。
- **是否同义反复：否。** 依据 = 上表 #8 的 EF Core 10.0.11 官方语义：`GetCommandTimeout()` 返回**被设置的值、未设置时 `null`**——它不是把源码常量抄回来，而是读 DI/`DbContextOptions` 中已生效的配置。
- **回归 ①（删掉 `mySqlOptions.CommandTimeout(...)` 接线）**：`GetCommandTimeout()` 变为 `null` → 在 `:40` `NotBeNull` 处失败；**退一步**，即便某提供程序实现改为在未设置时返回连接器默认 30s，则 `30 > 50` 在 `:45` 失败——**两种语义分支下都会被拦下**（该「双分支都拦得住」的结构性论证不依赖 engineer 的负向验证自述）。
- **回归 ②（常量降到 ≤ 服务端值，如 50）**：接线在，读回值 50 → `50 > 50` 在 `:45` 失败，被拦下。
- 实跑佐证（本角色，#4）：当前实现下该用例在真实 MySQL 上 1/0/0 通过 → 当前接线值确实 > 服务端实际 `innodb_lock_wait_timeout`。
- **如实声明**：engineer 自述的两次负向验证（N1 常量→50 失败；N2 删接线在 `NotBeNull` 处失败）**本角色未独立复现**——复现需临时修改 `DependencyInjection.cs`，超出只读边界（调度指令明文禁止）。本结论基于 EF Core 官方语义 + 断言结构 + 实跑通过三者互洽的独立判定，与 N2 的具体失败位置（`NotBeNull`）一致。
- 低门槛残余（不单列 REV）：该断言校验的是「配置值 > 服务端值」不变量，**不**覆盖「命令超时是否最终被应用到每条 `DbCommand`」（提供程序行为）；另若有人改以连接串 `Default Command Timeout=60` 达标，会因 `GetCommandTimeout()==null` 而误报失败（偏严而非漏判，建议级以下，不回改）。

**c) `MaxTransactionAttempts = 2` 与 D-08 「重试 1 次」逐字相符；单测期望已同步、未删用例 —— 结论：相符。**

- `DrawService.cs:30` 常量 = **2**，注释「最大**尝试**次数 = 2（即重试 1 次）…对齐 D-08」；循环 `:115`（`for attempt = 1;;`）+ `:121` `catch (TransientDataException) when (attempt >= MaxTransactionAttempts)`：第 1 次失败走 `:132` 告警分支重试，第 2 次失败命中 `:121` → `LogCritical` + `throw BusinessException(SystemBusy)`（1001/HTTP 200）。**总共 2 次尝试 = 重试 1 次**，与 `30-architecture.md:567`「整个事务重试 1 次，仍失败 → `1001`（HTTP 200）+ CRITICAL 告警」三要素逐项相符（`>=` 写法使常量语义自动正确，未改循环结构）。
- 测试同步（逐行核对 + 实跑）：`DrawServiceTests.cs:261`、`:284`、`:330`、`:379` 均 `Transactions.Received(2).BeginAsync(...)`；`:272` 用例更名 `DrawAsync_WhenTransientErrorPersists_DegradesTo1001AfterRetryBudget`（原 `…AfterThreeAttempts`）；`MaxStockRetryRounds = 3`（`DrawService.cs:23`）为**另一常量**（库存重抽），未受波及、未误改。**未删用例**：单测总数 95（实跑 95/0/0），与 CHG-16 的 95 相同。
- 高并发负例维持：`ConcurrencyTests.cs:47-74`（断言 HTTP 200、无「系统内部错误」/`\"code\":500`、业务码 ∈ {0,1001,1501,409}），未因重试预算变化产生新缺口。

**d) BUG-04 前端修复红线 —— 结论：全部遵守（逐项实证）。**

- `components/ui/**` 未改：mtime 扫描（§G.2 #7）改动集内无任何 `components/ui/` 文件，前端改动仅声明的 3 个生产文件 + 2 个测试文件。
- 无 `any`：三个生产文件全文检索 `any` 仅命中 `tooManyRequests` 子串（`messages.ts:22`），无类型 `any` 使用；`vue-tsc --noEmit` exit 0（严格模式）实跑佐证。
- 请求走既有封装：`reloadPoolForResult()`（`useDrawFlow.ts:92-101`）调用既有 `fetchPrizePool`（`api/prize.ts:5-7`，内部 `http` = `utils/request` 实例），无新增 axios 直连。
- 仅 Tailwind：`DrawView.vue` 的两段判定（`:84-97`）不引入任何样式；模板无新增独立 CSS / 硬编码色值；全仓 `v-html` / `innerHTML` 检索 0 命中；兜底名经插值文本渲染（`DrawResultDialog.vue:30`）。
- 行为正确性复核：`handleStartDraw` 在结果不可定位时**仅重拉一次**（`:87-91`）→ 仍不可定位才跳过旋转直接开弹层（`:93-97`），兑现 TC-48c 期望；`reloadPoolForResult` 不改 `poolState`（避免渲染边界卸载弹层）、失败/空快照保留原快照（`:92-101`）；兜底键 `messages.draw.prizeUnknown = '未知奖品'`（`messages.ts:86`）由 `messages.spec.ts:13-18` 钉住；新增 3 例（`useDrawFlow.spec.ts:210-258`）覆盖重拉定位 / 失败保留 / 空快照保留，实跑 58/58 全过。

### G.4 REV-19 处置（v5 轮修订）

- **原文表述是否仍成立：不成立（部分）。** REV-19 的闭环事实要素（整事务重试 / 终态 1001 不透出 500 / 回滚不掩盖原始异常 / 高并发无 500）**全部仍然成立**（依据见 §A REV-19 行与 §G.3-c），但两处**表述**随 CHG-17 失效：① §E.2 重建行的「**3 次后 1001**」——重试预算已由 3 次尝试改为 **2 次尝试（重试 1 次）**，该句不再成立；② §A 行引用的 v3 旧锚点行号（`DrawServiceTests.cs:250,261,272,284,294`）——`:272` 处用例已被更名、`:284` 处期望值已由 `Received(3)` 改为 `Received(2)`，锚点需刷新。
- **v5 轮修订（`60` 为本角色写入域，已直接落盘）**：§A REV-19 行重写为「**已闭环（表述已随 CHG-17 修订）**」+ 新锚点（`DrawService.cs:30/:121/:125-130`；`DrawServiceTests.cs:261/272/284/330/379`；`ConcurrencyTests.cs:47-74`）与「2 次尝试 = 重试 1 次」口径；§E.2 的 REV-19 行同步修订并加注说明（该行为 v2 重建内容，原「3 次」系 CHG-11 时期的实现口径，CHG-17 后不再成立）。
- **判定维持**：REV-19 级别（建议）、闭环状态（已闭环）不变——契约侧 D-08 从来写的就是「重试 1 次」，是**实现侧偏离**（CHG-11 时期的 3 次尝试）被 CHG-17 改正为对齐契约，方向为「实现向契约收敛」，不构成 REV-19 的回退或新缺口。
- 附带口径澄清（供 downstream 引用）：`docs/52-qa-report.md:391` 记载的旧期望「重试最多 3 次（总耗时 3×超时）」已失效，应由 test-executor 在 52 复核时同步为「重试 1 次 / 共 2 次尝试 / 最坏 ≈ 100s」。

### G.5 v5 新增发现：REV-21（建议，`未闭环`（v5 时点）；**v6 已闭环**）CRITICAL 告警文案把「尝试次数」记成「重试次数」

- **位置**：`src/backend/src/LuckyDraw.Application/Services/DrawService.cs:125-129`（v5 时点行号）。
- **问题**：模板 `"抽奖事务重试 {Attempt} 次后仍遇数据库瞬时错误（死锁 / 锁等待超时），已放弃本次抽奖：UserId={UserId}"` 传入 `attempt`；`attempt` 是**尝试次数**（终态触发时 = 2），读数即「重试 2 次」，而实际只重试 1 次。
- **依据**：D-08（`docs/30-architecture.md:567`）「整个事务重试 **1 次**」；CHG-16 自述「输出异常、**尝试次数**、UserId」（`40-changelog.md:650`）——自述口径与模板措辞互证错位；CLAUDE.md 工作准则 7（关键逻辑注释/日志准确）。
- **修复建议**（任选其一，一行改动，v5 原文）：① 传 `attempt - 1` 并在模板注明「重试」；② 模板改为「尝试 {Attempt} 次后仍遇…」保持参数不变。
- **性质与边界**：仅影响运维日志可读性，**不影响错误语义 / 契约符合度**（CRITICAL 告警本身已兑现）；v5 定级**建议**、**不阻断**。现实性由生产日志佐证（v6 §H.2 #7）。
- **v6 闭环处置**：engineer 按修法 ① 落地（CHG-18），本角色 v6 复核通过 → **`已闭环`**（详见 §H.3-a / §H.3-b）。**注意**：本节为 v5 历史记录，其行号 / 状态均为当时时点。

### G.6 v5 轮未完成 / 无法独立复现的复核项（如实声明）

1. **engineer 的两次负向验证（N1 / N2）未由本角色复现**：复现需临时修改 `DependencyInjection.cs`，被只读边界与调度指令明文禁止；改以 EF Core 官方语义 + 断言结构做双分支拦截论证（§G.3-b）。
2. **`MySqlErrors.cs`「未被改动」无版本控制级证明**：`src/` 未入 git，以 mtime（12:03，早于本轮窗口）+ 全文语义读码双重旁证（§G.3-a）。
3. **E2E 未复跑**（TC-48c / TC-56b）：归 test-executor（`tests/e2e/**`），且需先以新 DLL 重建 Api（`LuckyDraw.Api/bin` 被运行中进程锁定，本轮集成测试同样以临时输出目录绕开）；列入下游重跑清单。
4. **REV-09 / REV-13 的检索锚点（`devonly` / `fonts.googleapis`）v5 轮未重跑命令**：以「锚点文件不在本轮改动集」（mtime 扫描）维持 v4 判定；如需从严复跑由主对话调度。
5. **服务端 `innodb_lock_wait_timeout` 实际值未直接读表**：由守护用例动态读取并断言（实跑通过）；本角色未单独直连查询，数值不单独记录。

### G.7 契约符合度与安全审查（CHG-16 / CHG-17 + 架构 v4 增量）

- **契约符合度**：① D-08（MOD-03，`30-architecture.md:567-568`）三要素逐项符合——重试 1 次（`DrawService.cs:30/121`）、终态 `1001`「系统繁忙，请稍后重试」（`:130`，两参 `BusinessException` → HTTP 200，与 `error-codes.md:32`「业务代码禁止手动使用 500」一致）、CRITICAL 告警（`:125`）；② API-07（`:934`）新语义「1001 含 D-08 瞬时错误重试耗尽终态」与前端 `page-draw--drawfail` 分流（`useDrawFlow.ts:140-150` 非 1501/1502 → `drawError` → drawfail）一致；③ BUG-04 修复与 FR-05-R10 / TC-48c 期望（重拉一次再定位、跳过旋转保反馈）一致；④ 命令超时 60s > 服务端锁等待超时（守护用例实跑通过），使 1205 分类分支可达——「方案 (a)」语义与 `MySqlErrors` 不变声明一致；⑤ 无接口结构 / DTO / 迁移变更（mtime 扫描证实）。
- **安全审查（OWASP 增量）**：本轮改动无新增依赖 / 外链；无 SQL 拼接（用例内 `SET SESSION` / `SHOW VARIABLES` 均为测试侧固定字面量，无用户输入）；日志仅含 UserId 与异常对象，**无密码 / Token / 密钥**（逐参核对 `:125-129`）；Detach 收尾不改变信息暴露面；前端无 `v-html`、无新增网络出口。依赖漏洞扫描仍为离线未执行（沿用 v2 未审声明）。
- **负面清单确认**：本轮改动文件集合**不含** `Program.cs` / `ExceptionFilter.cs` / `ErrorCodes.cs` / `AppDbContext.cs` / 迁移 / 实体 / `components/ui/**` / `utils/request.ts`；mtime 旁证完备（§G.2 #7）。
- **已决残留（不重开，引用 CHG-17 记录）**：前端 `request.ts` 15s < 后端最坏 100s 的超时预算不匹配，经**用户裁决本轮不动**（`40-changelog.md:758-760`）；生产若前置 nginx（默认 `proxy_read_timeout` 60s）需部署侧统一口径——列入交付总结，非本报告问题项。

---

## §B v3 轮审查结论（CHG-14；历史记录，原文保留，v6 未改动）

### BUG-01（一般）认证侧幂等冲突改为 HTTP 409 —— 结论：**正确**；契约符合；前端行为零差异；过程记录有一处不自洽（不影响修复本身）

**改动逐点核验（磁盘）**：

1. 两处抛点与抽奖侧逐字同写法：`AuthService.cs:222`（在途占位超时，`:221` 注释说明契约依据）与 `:260`（同键异体）均为 `throw new BusinessException(ErrorCodes.IdempotencyConflict, "请勿重复提交", ErrorCodes.IdempotencyConflict);`，对照 `DrawService.cs:264`、`DrawRequestRepository.cs:41`。
2. **`body.code` 仍为 409**（不得被改成 ≥1000）：`ErrorCodes.cs:37` `IdempotencyConflict = 409`，且 `BusinessException` 的 `Code` 与 `HttpStatusCode` 为两个独立属性（`BusinessException.cs:10,13`）。契约依据：`30-architecture.md:598`（`409` 幂等冲突为与「`≥1000` 业务异常一律 HTTP 200」**并列分立**的 HTTP 状态码）、`:623`（D-15-1）、`:843`（API-02 错误语义）、`:955`（§5.4）、`50-testcases.md:26`（统一口径表「幂等冲突 → HTTP 409」）。
3. **`ExceptionFilter` 通用映射未被改动**：`ExceptionFilter.cs:40` 仍为 `StatusCode = businessException.HttpStatusCode`；系统异常 `:53-60`（500 + 「系统内部错误」）不变；`BusinessException.cs:19` 三参构造（默认 200）不变。
4. **其他错误码的 HTTP 映射未被波及**：`AuthService.cs` 全文件 `throw` 清点（10 处）——仅 `:222/:260` 带第 3 参数；1101 / 1103 / 1102（`:108,141,164`）与 1203 / 1204 / SessionInvalid（`:268,295,300,311,315`）均 2 参 → HTTP 200，**D-12 的「登录失败不得用 401」语义与「≥1000 → 200」口径不变**；429 限流链路（`Program.cs:192` 中间件）未被 CHG-14 触及。
5. 测试侧核验：`AuthIdempotencyTests.cs:84`（注册冲突断 `HttpStatusCode.Conflict`）+ `:86`（body code=409）+ `:88`（不携带凭证）；`:112-133` 新增登录侧 409 用例（`:117`）；`:167-170` 并发同键改为「状态码—业务码配对」；单测 `AuthServiceTests.cs:184,196,206,218` 两处抛点补断 `HttpStatusCode == 409`；`IntegrationFixture.cs:191-195` 注释已更正为「业务异常默认 200，幂等冲突 409 例外」。
6. **前端零差异核验（重点增量）**：新形态（HTTP 409）经 `request.ts:106,111-120`（`mapHttpError` 取响应体 message → `ApiError(error.response.status, message)`）产出 `ApiError(409, '请勿重复提交')`；旧形态（HTTP 200 + body code=409）经 `unwrapResponse:153-155` 同样抛 `ApiError(409, '请勿重复提交')` —— **两条路径对消费方完全同构**。`stores/auth.ts:22-32` 键作废判据 `code >= 1000 || code === 409` 不受影响；`types/draw.ts:21`、`error.spec.ts:30`（409 → 文案映射）与实现一致。即本修复对浏览器前端**无任何行为变化**，收益落在非浏览器消费者（按 HTTP 状态码判定者不再把拒绝误当成功）。
7. 端到端佐证（test-executor 回归，非本角色执行）：`tests/e2e/logs/api-round2-regression-20260917-151229.log:2-3` TC-08 / TC-17「http=409 code=409」PASS；`51-defects.md:20`（R1 已完成）、`:33`（BUG-01 **已闭环**）。

**「原测试把缺陷固化成了期望」说法核验（诚实结论）**：

- **实质成立**：(a) 修复前集成套件全绿（CHG-13 验证记录 38 例）而实测行为为 HTTP 200（`51-defects.md:33` 证据 `tests/e2e/logs/api-round2-20260917-145013.log`；`52-qa-report.md:80` 复现记录）→ 原用例不可能断言 409，「未守护 HTTP 状态码」成立；(b) CHG-14 负向验证（撤掉第 3 参数 → 用例以 `Expected ... Conflict {value: 409} ... but found ... OK {value: 200}` 失败）表明改写后的用例具备真实捕获能力（`40-changelog.md:496`）。
- **细节无法独立裁定，且 engineer 自身两处表述互相矛盾**：`40-changelog.md:488` 既引用原断言为 `response.StatusCode.Should().Be(HttpStatusCode.OK)`，同一句又写「只断言 body、不断言 HTTP 状态码」；`AuthIdempotencyTests.cs:114` 新注释亦称「此前仅断言 body，未断言状态码而漏判」。两者不能同真。`tests/` 未纳入 git（`git status` 为未跟踪，磁盘无历史版本），**无法复核原断言原文**。两种情形（显式断言 200 / 完全未断言状态码）性质相同（测试未把状态码作为判据），不影响修复有效性与本项判定；建议 engineer 将 40 中该句统一为单一口径。
- 端到端补充：`51-defects.md:20-21` 记录 R1/R2 均已完成且 PASS（R3 全量 E2E 与 R4 覆盖补做由 test-executor 继续，不在本角色范围）。

### BUG-02（建议）`/records` 的 `winTime` 补 UTC 时区标识 —— 结论：**正确**；「爆炸半径」声称经独立核验**全部为真**

- **改动逐点核验**：`RecordDtos.cs:17` `WinTime` 为 `DateTimeOffset`（`:4-18` 全文：仅 `Id` / `PrizeName` / `WinTime` 三属性）；唯一构造点 `WinningRecordRepository.cs:52` → `WinTime = new DateTimeOffset(DateTime.SpecifyKind(x.CreateTime, DateTimeKind.Utc))`（`:46-53` 投影；v5 复核：该方法现位于 `:65`，内容未变）。
- **「未挂全局值转换器 / 未注册全局 JSON 转换器 / 未改 `JsonSerializerOptions`」独立验证**：后端全仓检索 `HasConversion|ValueConverter|ConfigureConventions|JsonSerializerOptions|AddJsonOptions|JsonConverter` → **仅 `AppDbContext.cs:82` 一处枚举 `HasConversion<int>`（既有，非日期字段）**；`Program.cs` 无任何序列化配置命中。
- **消费面验证**：全仓 `WinningRecordDto` 检索 → 仅 API-08 链路（`RecordsController.cs:28` / `WinningRecordService.cs:19` / `Repositories.cs:76` / `ApplicationServices.cs:53` / 仓储）+ 前端 `types/record.ts`；后端 `WinTime` 检索 → 仅 DTO 与仓储投影。`resetAt`（API-06，既有 `DateTimeOffset`）未动。
- **库表 / 迁移 / 实体未改动的结构核验**：迁移目录仅 `20260917035407_InitialCreate`（+ Designer + Snapshot），无新增迁移；迁移文件内 `DateTimeOffset` 零命中；实体 `CreateTime` 仍为 `DateTime`（`AuditableEntity.cs:13`）；`AppDbContext.cs:127-140` WinningRecord 映射无转换器。**诚实声明**：`src/` 未纳入 git，无法用 diff 证明「未改动」，以上为「改动痕迹检索 + 结构一致性」核验，与声称一致；mtime 核验（见附注）进一步支持改动集边界。
- **前端兼容核验**：`datetime.ts:11-13` `parseUtc` 先毫秒截断（`.(\d{3})\d+` → 3 位）再以 `/(?:z|[+-]\d{2}:?\d{2})$/i` 判时区，带 `+00:00` 原样解析（不会拼出 `...ZZ`）；`types/record.ts:6` 仍 `winTime: string`；`WinningRecordsView.vue:48` 分钟精度展示不变。新单测 `datetime.spec.ts:17-29` 覆盖 `+00:00` 形态；集成 `DrawApiTests.cs:129-135` 补断 `Z`/`+00:00` 与偏移为 0。
- 端到端佐证：回归日志 TC-63「时间值=2026-09-17T07:12:32.462764+00:00（ISO 8601=true，UTC=true）」PASS（`api-round2-regression-20260917-151229.log:6`）；`51-defects.md:34`（BUG-02 **已闭环**）。
- **低门槛残留（不计问题，不单列 REV）**：`types/record.ts:5` 前端注释「序列化不带时区后缀」在修复后已过时（行为兼容，仅注释陈旧，建议后续顺手订正）。

### BUG-03（建议）抽奖页错误态文案 —— 结论：**正确**；通用键一字未动，无「顺手统一文案」

- **改动逐点核验**：`messages.ts:17` `common.loadFailedTitle: '加载失败'` **原样未动**（v5 复核仍在 `:17`）；`:89-94` 新增 `prizes.loadFailedTitle: '奖池加载失败'`（注释明确「两页各自的冻结口径，不可合并」；v5 复核位于 `:95-99`）；`DrawView.vue:145` 标题改用新键（v5 复核位于 `:153`）；`WinningRecordsView.vue:160`、`App.vue:28` 仍用 `common.loadFailedTitle`（记录页与全局错误边界行为不变）。
- **原型口径核对**：`20-prototype.html:578`（抽奖页 `error` 状态定义表）=「奖池加载失败」；`:587`（记录页 `error` 状态定义表）=「加载失败」——确为两页各自的冻结文案；原型文末文案清单表与状态定义表冲突按「状态定义表为准」处理（CHG-14 自述，本轮未复核清单表原文）。
- **测试**：新增 `messages.spec.ts:6-11` 同时钉住两个键（防后人再次合并；v5 复核仍在 `:6-11`，并新增 BUG-04 用例于 `:13-18`）。
- 端到端佐证：`51-defects.md:21`（R2 已完成，PASS，实测文案「奖池加载失败 | 系统繁忙，请稍后重试 | 重试」；证据 `tc30-regression-20260917-151508.log`（**勘误**：`51-defects.md:28` 已注明该文件名在磁盘不存在，实为 `tc30-regression-20260917-151255.log`）；本角色亦直接读过后者 `:4`（同结论）；`51-defects.md:35`（BUG-03 **已闭环**）。

### 附注：改动集边界核验（mtime 独立旁证，v3 轮）

v3 轮对本仓库 `src/`、`tests/`（排除 node_modules / bin / obj / .artifacts）检索「今日 14:40 后被修改的文件」，结果为：`AuthIdempotencyTests.cs` / `IntegrationFixture.cs` / `AuthServiceTests.cs` / `RecordDtos.cs` / `WinningRecordRepository.cs` / `datetime.spec.ts` / `messages.spec.ts` / `messages.ts` / `DrawView.vue` / `DrawApiTests.cs` / `AuthService.cs` + 5 个 e2e 日志 + `qa.spec.ts`（test-executor 回归文件）——**与 CHG-14 声明的改动集完全吻合，无任何未披露文件被改动**（`Program.cs` / `ExceptionFilter.cs` / `ErrorCodes.cs` / `AppDbContext.cs` / 迁移 / 实体均无新 mtime）。mtime 非版本控制级证据，仅作旁证。（v5 轮扫描见 §G.2 #7；v6 轮扫描见 §H.2 #1。）

---

## §C 核实（v3 轮）：「测试把缺陷固化成了期望」的说法是否属实 + 是否还有其他固化

1. **实质属实**（细节有限度）：见 §B BUG-01 末段——修复前全绿 + 实测 200 → 原用例未守护 HTTP 状态码；负向验证表明改写后具备捕获能力。**原断言的具体文本无法独立复核**（`tests/` 未跟踪、无版本历史），且 `40-changelog.md:488` 与 `AuthIdempotencyTests.cs:114` 两处表述自相矛盾（一处引用原断言 `Should().Be(HttpStatusCode.OK)`、一处称「未断言状态码」），建议统一口径。
2. **是否还有其他测试固化错误期望：未发现**。本轮扫描 `tests/integration/**` 全部 21 处 `HttpStatusCode.OK` 断言——除已改写者外，其余均落在契约要求 HTTP 200 的路径（成功、≥1000 业务码、1002、1102/1103、1204、1501、并发配额），无错误期望。
3. **唯一可疑配对的裁决**：`ConcurrencyTests.cs:57` 硬断言 HTTP 200、同时业务码允许列表含 409（`:65`）。经可达性分析：该用例 12 个请求使用**互异**幂等键，409 分支（`DrawService.cs:258-265` 经唯一索引未落定 / 哈希不一致）不可达；若未来可达会以 HTTP 断言失败显式暴露而非静默放过，**不构成固化**（作为容忍项保留，无行动项）。

---

## §D v3 轮新增发现（REV-20；已于 v4 闭环；原文保留）

### REV-20（建议，`已闭环`（v4，v5 / v6 复验保持））Infrastructure 项目格式门禁为红：45 处空白违规全在 AppDbContext.cs 种子块

- **位置**：`src/backend/src/LuckyDraw.Infrastructure/Data/AppDbContext.cs:92-122`（`HasData` 种子数据块，对象初始化器多属性同行，如 `:95-97`、`:101-103`）。
- **证据（v3 本角色本机实跑，只读验证）**：于 `src/backend/src/LuckyDraw.Infrastructure/` 执行 `dotnet format whitespace --verify-no-changes` → **exit 2**、输出 **45 条 `WHITESPACE`**、45 条均指向 `AppDbContext.cs`（与 CHG-14「已知遗留」声称的数字 45 与位置**逐项一致**，`40-changelog.md:551`）；同日于 `src/backend/src/LuckyDraw.Application/` 同命令 → **exit 0**（0 条）——CHG-14 的生产改动项目本身门禁通过。
- **依据**：CLAUDE.md 工作准则 4（交付前用格式检查验证，不静默跳过）与「一、常用命令」按项目目录执行格式检查的专门告诫；当前状态使 Infrastructure 项目该门禁无法通过。
- **性质**：**非 CHG-14 引入**（种子块源自 CHG-03；CHG-14 已如实披露并选择不擅自修改，处理克制、符合最小改动原则）。定级「建议」：无行为影响，但属交付自查清单中「格式检查」项的现实失败点。
- **修复建议**：由主对话裁决后，engineer 在 `src/backend/src/LuckyDraw.Infrastructure/` 执行 `dotnet format whitespace`（纯空白改动、无行为变化），并复跑 `--verify-no-changes` 至 exit 0。
- **闭环记录（v4）**：主对话裁决后由 engineer 落地为 **CHG-15**（`40-changelog.md:32` 索引 / `:557` 正文；改动唯一文件 `Data/AppDbContext.cs`，sha1 `34a7807…` → `a2f19b8…`）。v4 定向复审独立验证通过：格式门禁 exit 0、去空白逐字节 `cmp` 一致、单测 89/0/0、集成 39/0/0。**v5 复验**：6 项目格式门禁实跑全 exit 0（§G.2 #1），保持闭环。**v6 复验**：本轮涉及的两项目（Application / UnitTests）exit 0（§H.2 #5），AppDbContext 所在 Infrastructure 项目文件未变（§H.2 #1），保持闭环。

### 低于门槛的残余观察（不计入新发现数、不单列 REV；v3 轮记录）

1. **过程记录不自洽**：`40-changelog.md:488` 与 `AuthIdempotencyTests.cs:114` 的表述矛盾（见 §C-1），素材无版本历史无法裁定原文，建议 engineer 统一（不影响修复判定）。
2. **类注释未同步 409 例外**：`BusinessException.cs:4`（「code ≥ 1000，HTTP 200」）与 `ExceptionFilter.cs:10`（「业务异常 → HTTP 200 + 业务码（D-12）」）未反映 `409` 例外（既有措辞、非 v3 轮引入；`IntegrationFixture.cs:191-195` 同类注释已在 CHG-14 更正，可对齐）。
3. **409 未列入 `error-codes.md`**：属**设计内**——`error-codes.md:4` 边界条款明示「`4xx/5xx` 段与 HTTP 状态码对齐」，`409` 由 `30-architecture.md:598` 承接登记。**正式关闭** `51-defects.md` BUG-01 的「附带口径问题」，不追办。
4. **TC-53 回归 [FAIL] 属已知契约可测性缺口**（OBS-03：抽奖请求体恒为 `{}`，409 分支不可构造；`api-round2-regression-20260917-151229.log:4`），非 CHG-14 回归，归 test-executor / 主对话既有口径，本报告不重开。

### 契约符合度与安全审查（CHG-14 增量；v3 轮记录）

- **契约符合度**：API-01 / API-02（MOD-01）幂等冲突由「HTTP 200 + code 409」修正为「**HTTP 409 + code 409**」，与 `30-architecture.md:598/623/843/955`、`50-testcases.md:26` 一致 → 关闭 BUG-01 偏离；错误码分工全表经 §B BUG-01 第 4 点核验未被波及。API-08（MOD-04）`winTime` 补 `+00:00` 自描述时区，与 §5.1 时间口径及 API-08 行一致 → 关闭 BUG-02 偏离。抽奖页错误态（MOD-10）标题与原型状态定义表 `20-prototype.html:578` 逐字一致 → 关闭 BUG-03 偏离。v2 对其余接口的契约结论口径（「总体符合、无致命项」）沿用，原文细节见 §E 重建声明。
- **安全审查（OWASP 增量）**：CHG-14 未引入新风险面 —— 无新增依赖 / 外链；无 SQL / 反序列化面改动；409 冲突响应体不含凭证（`AuthIdempotencyTests.cs:88,132` 断言 `Data == null`）；`winTime` 序列化形态变化（`+00:00`）不改变信息暴露面；无敏感信息新增入库入仓。依赖漏洞扫描仍为离线未执行（沿用 v2 未审声明）。
- **负面清单确认**：本轮改动文件集合（生产：`AuthService.cs` 两处 throw + `RecordDtos.cs` + `WinningRecordRepository.cs` + `messages.ts` + `DrawView.vue`）**不含** `Program.cs` / `ExceptionFilter.cs` / `ErrorCodes.cs` / `AppDbContext.cs` / 迁移 / 实体；mtime 旁证与磁盘抽查一致（§B 附注）。

---

## §E v2 重建内容（**原文因事故丢失，非转录；依 CHG-13 / 51 / 52 重建**）

> **重建声明（必读）**：`docs/60-review.md` v2 原文（513 行，含其内嵌的 v1 全文）于 2026-09-17 15:16 因本角色工具误操作（空内容 Write）被覆盖丢失，不可恢复。本节**不是原文**，而是依据以下可核来源**重建**：
> - `docs/40-changelog.md` **CHG-13 段（`:272-472`）**——全部 19 项 REV 的逐项落地记录（改动文件 / 方案简述 / 回滚建议 / 测试结果 / 关联契约）；
> - `docs/40-changelog.md:275`（明确不在本轮范围的 5 项）；
> - `docs/51-defects.md:25`、`docs/52-qa-report.md:95`（v2 结论口径引用）。
> **未重建部分（如实声明）**：v2 的 §3 契约符合度逐接口表 / §4 安全审查表 / §5 CHG-11 复核 / §6 测试审查 / §7 未审声明 / §8 冲突上报 / §9 结论**的全文均未重建**（来源仅能确证其结论口径，不能确证逐行原文）；v1 原文同样丢失。

### E.1 v2 结论口径（重建，来源：`51-defects.md:25`、`52-qa-report.md:95`）

- v2 重跑结论 = 「**闭环 14 / 未闭环 5 / 本轮新增（严重及以上）0**」；5 项未闭环 = **REV-10 / REV-11 / REV-12 / REV-16 / REV-18**，全部为**建议级**且已由用户裁决不纳入本轮（`40-changelog:275`），按角色纳入规则不进入 51（`51-defects.md:25`）。
- v2 确认「0 条致命 / 严重未闭环」（`52-qa-report.md:95`「按来源：docs/60-review.md 转入 0」）。
- v2 头部所述上游依赖为 `30-architecture.md` **v3**（§2.6 次数未知态、§2.8 会话与恢复契约、D-14、D-15）与 `development-spec.md` §3.3（access token 仅存内存）等（重建自 `40-changelog:274` 的契约基准描述，与 v2 头部口径一致；逐字原文不可确证）。

### E.2 CHG-13 逐项落地记录（重建自 `40-changelog:272-459`）

| REV | v2 判定 | 改动文件（重建自 CHG-13） | 关键方案（要点） | 关联 |
| --- | --- | --- | --- | --- |
| REV-13 | 已闭环 | `frontend/src/assets/main.css` | 删 Google Fonts 外链；`--font-sans` 改系统字体栈（零依赖方案） | 架构 §8、附录 B |
| REV-14 | 已闭环 | `messages.ts` + LoginView / RegisterView / AuthLayout / QuotaBadge / WinningRecordsView | 6 处（含 `aria-label="分页"`）迁入 `messages.*`；`register.subtitle` 由冻结常量组合 | 附录 A 第 4/10 条 |
| REV-15 | 已闭环 | `datetime.ts` / `WinningRecordsView.vue` / `datetime.spec.ts` | `TimePrecision`（默认 second）；记录页传 `'minute'` | PRD 4.1-3 |
| REV-08 | 已闭环 | `DrawWheel.vue` + `eslint.config.js`（配套 globals） | 2400ms + `cubic-bezier(.16,.84,.24,1)`；`transitionend` 主通道 + 2600ms 兜底；`settlePending` 窗口；motion-reduce 直接落终值 | D-14、FR-06 |
| REV-01 | 已闭环 | `useDrawFlow.ts` / `DrawView.vue` / `QuotaBadge.vue` / `DrawResultDialog.vue` / `messages.ts` | `remaining`/`dailyLimit` 改 `number \| null`（未知 ≠ 0）；锚点 12 收紧为 `remaining === 0`；三条恢复通道；未新增锚点 | §2.6 v3、FR-04、FR-10-3 |
| REV-17 | 已闭环 | `useDrawFlow.spec.ts` | 新增「首载即失败」「1501 收紧为 0」「抽奖后刷新采用其值」；原用例更名收窄语义；9 → 12 例 | REV-01、工作准则 5 |
| REV-02 | 已闭环 | `stores/auth.ts` / `router/index.ts` / `utils/request.ts` / `api/auth.ts` / `types/auth.ts` / `auth.spec.ts` / `AuthDtos.cs` / `AuthService.cs` | §2.8 四条：内存唯一判据、仅受保护路由触发、复用同一在途刷新 Promise、三态闸门（ready 终态 / 失败回 idle）；API-03 响应兼容性新增 `user` | §2.8、API-03、MOD-08 |
| REV-06 | 已闭环 | `api/auth.ts` / `stores/auth.ts` / `Services.cs` / `AuthService.cs` / `ApplicationServices.cs` / `RedisAuthIdempotencyStore.cs` / `DependencyInjection.cs` / `AuthController.cs` + 单测 8 例 / 集成 6 例 | D-15 六条：作用域 `(operation,key)`、`SET NX` 占位 + ≤2s 短轮询 409、仅缓存成功、重放只重签凭证；指纹用 HMAC-SHA256（密码不裸哈希入缓存）；Redis 不可用降级无幂等 | D-15、Q2 裁决 |
| REV-03 | 已闭环 | `Program.cs` | `ResolveDrawPartitionKey`：`draw:user:{userId}` / 未认证回落 IP | Should-S3、规范 8.6 |
| REV-04 | 已闭环 | `IdempotencyKeyValidator.cs` / `DrawController.cs` / `AuthController.cs` + 单测 12 例 / 集成 3 例 | 长度先于形态；>64 或非 UUID → 1002；缺失 = 无幂等不变 | API-07、error-codes 1002 |
| REV-05 | 已闭环 | `Services.cs` / `BcryptPasswordHasher.cs` / `AuthService.cs` + 单测 4 例 | `VerifyOrDummy`：空哈希对静态假哈希同工作因子校验；两失败路径耗时抹平 | FR-02、AC-04 |
| REV-07 | 已闭环 | `useDrawFlow.ts` / `DrawView.vue` / `messages.ts` / `datetime.ts` | `resetAt` 全链路透出，锚点 12 描述行渲染「重置时间：yyyy-MM-dd HH:mm（UTC+8）」；未知时降级 | Should-S2、API-06 |
| REV-09 | 已闭环 | `appsettings.Development.json` / `LuckyDraw.Api.csproj`（UserSecretsId）/ `LuckyDrawApiFactory.cs` | 连接串口令与签名密钥移出仓库（user-secrets / 环境变量）；测试侧环境变量可覆盖 | 附录 B B6、规范 8.4 |
| REV-19 | 已闭环 | `DrawServiceTests.cs`（3 例）/ `ConcurrencyTests.cs`（1 例） | 整事务重试轮次 / **重试 1 次（共 2 次尝试）后 1001**（v5 修订：原重建文「3 次后 1001」为 CHG-11 时期实现口径，经 CHG-17 用户裁决对齐 D-08 后不再成立）/ 回滚不掩盖；高并发不出现 500 | CHG-11、§5.2、**CHG-17（v5）** |

> 重建备注：`LuckyDrawApiFactory.cs` 本地测试容器仍保留默认口令 `devonly`（非部署凭证）——CHG-13「遗留说明」（`40-changelog:448`）与 v2 §C 残留观察一致，本 v3/v4/v5/v6 未重开。
> v5 备注：本表为 v2 重建内容；REV-19 行已按 §G.4 修订，其余行未动。

### E.3 CHG-13 统一验证记录（重建自 `40-changelog:461-471`）

| 命令 | 结果（v2 时段实跑数字） |
| --- | --- |
| `dotnet build LuckyDraw.slnx` | 0 Warning / 0 Error |
| `dotnet test tests/unit/LuckyDraw.UnitTests` | 通过 89 / 失败 0 |
| `dotnet test tests/integration/LuckyDraw.IntegrationTests` | 通过 38 / 失败 0（实跑，MySQL 3307 + Redis 6379） |
| `vue-tsc --noEmit` / `vite build` / `eslint --max-warnings 0` | 退出码 0 / built in 737ms / 退出码 0 |
| `vitest run` | 6 文件 / 52 用例通过 |

### E.4 不在本轮范围（用户裁决，重建自 `40-changelog:275`）

REV-10、REV-11、REV-12、REV-16、REV-18 五项建议级不修，未做任何顺手改动——与 §A 判定一致，进最终交付的「已接受残留清单」。

---

## §F v4 定向复审记录（CHG-15 / REV-20 闭环；历史记录，原文保留）

> 本节为 v4 轮记录（非全量重审）：范围仅 CHG-15 相关面（`AppDbContext.cs` 种子块格式化），外加「其余 REV 判定不受本轮变更影响」的边界核验。以下均为 v4 轮本角色一手实跑 / 只读比对，**不采信 changelog 与调度简报的自述结论**（简报中的数字均经复算，未见偏差）。

### F.1 复审范围与触发

- 触发：`docs/40-changelog.md` 新增 **CHG-15**（索引 `:32`、正文 `:557`，两处行号经 `grep -n` 实测确认与简报一致）：`Data/AppDbContext.cs` 种子块纯空白格式化（REV-20 闭环，来源 v3 §D；用户裁决修复）。
- 依据矩阵：`docs/artifacts.md` §5「`40-changelog 变更` → 60-review」+「`src/ 变更` → 测试重跑」。
- 涉及模块 / 接口：MOD-02（奖池种子，`HasData`）；**不涉及** API、DTO、迁移、契约。

### F.2 一手证据（v4 轮：命令 / 操作 → 实测观测值）

| # | 验证项 | 命令或操作（工作目录） | 实测观测值 |
| --- | --- | --- | --- |
| 1 | 格式门禁（复审对象） | `dotnet format whitespace --verify-no-changes`（`src/backend/src/LuckyDraw.Infrastructure`） | **exit=0**，无输出（v3 记录为 exit 2 / 45 处 `WHITESPACE`，已转绿；按告诫未加 `--nologo`） |
| 2 | 格式门禁（其余项目，排除不完整修复） | 同命令（`LuckyDraw.Domain` / `LuckyDraw.Application` / `LuckyDraw.Api`） | 三项均 **exit=0** —— 后端 4 个项目格式门禁全绿 |
| 3 | 种子取值未变 | 定点阅读 `Data/AppDbContext.cs:92-167` | 5 条种子全在且取值逐项核对：`prize-keyboard`(W=1,S=3) / `prize-earbuds`(W=3,S=10) / `prize-mug`(W=10,S=50) / `prize-coupon`(W=20,S=200) / `no-prize`(W=66,S=0)；与 `30-architecture.md:565-569` 冻结表逐项一致 |
| 4 | **纯空白证明（主证据）** | `tr -d '[:space:]' < 改前快照 > A`；同式处理现文件得 B；`cmp A B` | **逐字节一致**：A、B 各 **6953 字节**，sha1 均为 `e1616333a8fee03b8b65a15a1fe35160fe508caa` |
| 5 | 纯空白证明（补强 1：token 流） | 两文件 `tr -s '[:space:]' '\n'` 后 `cmp`（可发现任何 token 合并 / 拆分） | 两侧均 **604 token**，**全同** |
| 6 | 纯空白证明（补强 2：字面量 / 注释） | `grep -o '"[^"]*"'` 与 `grep -o '//.*'` 后 `diff`（覆盖去空白比对对字面量 / 注释内部空白的盲区） | 字符串字面量 **33/33 全同**（如 `"一等奖 · 机械键盘"` 内部空格保留）；注释 **20/20 全同**；两文件均无转义引号 |
| 7 | 改前快照真实性（旁证互证） | 与 v3 于修复前（15:24 冻结）记录的锚点比对 | 快照（191 行 / 9255 字节 / sha1 `34a7807…`，mtime 15:52）与 v3 三处改前锚点逐一吻合：`:82` `HasConversion<int>`、`:92-122`「多属性同行」种子块、`:127-140` `ConfigureWinningRecord`；现文件对应位置为 `:82` 原位、种子块 `:92-167`、方法 `:172-185`（**精确平移 +45 行、跨度行数一致**） |
| 8 | 文件指纹 | `wc -l -c` / `sha1sum` | 现文件 **236 行 / 10200 字节 / sha1 `a2f19b8bf5eabb2c3d0b644b2f44be6662d3fb2c`** —— 与 CHG-15 声称逐项一致 |
| 9 | 单测 | `dotnet test tests/unit/LuckyDraw.UnitTests --nologo`（仓库根） | **通过 89 / 失败 0 / 跳过 0**（282 ms），exit 0 |
| 10 | 集成测试 | 先 `dotnet build tests/integration/… -p:OutputPath="C:/tmp/reviewer-itest" -m:1`（0 警告 / 0 错误），再 `dotnet test … --no-build -p:OutputPath="C:/tmp/reviewer-itest"`（仓库根） | **通过 39 / 失败 0 / 跳过 0**（10 s，真实 MySQL 3307 + Redis 6379），exit 0；构建直接用临时输出目录，**未遇**文件占用，**未杀任何进程** |
| 11 | 改动集边界 | `find src tests -newermt "2026-09-17 15:24"`（v3 冻结时刻；排除 bin/obj/node_modules/deploy） | `src/backend` 源文件唯一变更 = `AppDbContext.cs`（mtime 15:52:14）；前端仅 `dist/` 构建产物变动（`src/frontend/src` 未动）；`tests/` 仅 test-executor 的 e2e 文件与日志；迁移目录仍仅 `20260917035407_InitialCreate`（mtime 11:54 未动） |
| 12 | 既有闭环项定点复验（抽查） | 后端 `src` 全仓 `grep -ri devonly`；前端 `src` 全仓 `grep -ri fonts.googleapis` | 均为 **0 命中**（REV-09 / REV-13 锚点复验一致） |

### F.3 证据强度声明（如实标注，v4 轮）

- **主证据（#4）强度 = 强旁证，非版本控制级**：`src/` 未纳入 git（`git ls-files src` = 0，`git fsck` 无悬空对象可恢复），磁盘上不存在版本控制级 diff。改前快照由 engineer 留存于系统临时目录（`%TEMP%\rev20_AppDbContext.before.cs`），**其未被篡改无法用密码学手段证明**。本轮把「自证」升级为「旁证」的方式：该快照与 v3 在修复前 15:24 冻结时记录的**三处独立锚点**（行号 + 行内容 + 排版特征）全部吻合，且统计值（191 行 / 9255 字节 / sha1 前缀 `34a7807`）与 CHG-15 声称一致 —— v3 冻结点早于快照存活时间，两者互相独立地指向同一改前状态。
- **盲区补强（#5 / #6）**：去空白比对对「字面量 / 注释内部空白增删」不敏感（`tr` 会一并剥除），已用 token 流比对 + 33 条字符串字面量与 20 条注释的精确 `diff` 覆盖，均全同 → 「纯空白」结论无已知盲区残留。
- **行为级佐证**：单测 89/0/0、集成 39/0/0 与「零行为改变」预期一致；集成夹具走 `MigrateAsync` 落库路径，种子结构被测试链路实际消费。

### F.4 对其他 REV 判定的影响核验与结论（v4 轮）

- **边界核验**：自 v3 冻结（15:24）以来，`src/backend` 源文件仅 `AppDbContext.cs` 变更（§F.2 #11）；v3 对其余 19 项定点锚点所在文件本轮未被触碰（CHG-14 的 3 个后端文件 mtime 为 15:03–15:07，早于 v3 且 v3 已复核）→ **其余 19 项判定维持 v3 结论**；未闭环 5 项（REV-10/11/12/16/18）按用户裁决原样保留，**不作本轮阻断项**。
- **本轮新增发现**：**0 项**。
- **结论**：REV-20 **已闭环**；现行 20 项 = 已闭环 15 / 未闭环 5（均建议级、均用户已裁决不纳入本轮）；致命 / 严重未闭环 **0**。

### F.5 未能验证的部分（如实声明，v4 轮）

1. **「修复前 45 处 WHITESPACE / exit 2」的红状态无法在修复后的磁盘上二次复现**——一次性现象，锚定于 v3 §D（修复前记录）+ CHG-15 基线复现记录 + 现门禁 exit 0；三者互洽，但本轮不具备重放该历史状态的条件。
2. **快照完整性无密码学证明**（无 git / 无签名），仅三重锚点旁证（§F.3）。
3. **E2E 套件未复跑**——超出本角色范围（归 test-executor），且 CHG-15 为纯空白变更不改变运行时行为；如需从严按矩阵执行由主对话调度。
4. **前端验证未复跑**（`vue-tsc` / `vitest` / `eslint`）——CHG-15 未触及 `src/frontend`，v3 口径沿用。
5. **未直接查库核对种子行**——集成夹具会按用例改写权重 / 库存（`IntegrationFixture.cs:70-76`），库内当前值不构成种子原值证据；种子正确性以源码 + 冻结契约表 + 迁移落库路径为准。

---

> **本报告完整性声明**：§A（21 行判定表：原 19 项 + REV-20 + REV-21，v6 定稿状态）、§H（v6 轮定向复审记录：CHG-18 / REV-21 复核、负向验证、边界与属实性核验）、§G（v5 轮记录）、§B–§F（v3 / v4 历史轮次记录，保持原文）均已落盘。v2/v1 原文因事故丢失的记载见头部「事故说明」。
> **v6 轮未完成复核项（如实声明，详见 §H.6）**：E2E 未复跑（归 test-executor）；集成未由本角色复跑（变更不触及该断言面，检索 0 命中 + 单测已独立复跑）；其余 4 项目格式门禁以 v5 结论 + 未改动边界维持；无 VCS 级 diff（以 mtime + 行号位移交叉核验）；负向验证的镜像环境边界（§H.6 #5）。
> **v6 出口判定**：**致命 / 严重未闭环 0 项** → 不触发修复循环；5 项 `已接受（用户决断）`（建议级）作为已接受残留进入交付总结。
