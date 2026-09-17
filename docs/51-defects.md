# 缺陷报告：转盘抽奖（前后端分离）

| 项 | 内容 |
| --- | --- |
| 产物 | 缺陷报告（defects） |
| 版本 | v1 |
| 冻结时间 | 2026-09-17T18:31:00+08:00（R7 回写时修正：原值 `2026-09-17T18:30:00+08:00` 晚于实际落盘时刻 17:18:40，属「标称冻结时刻」越界；本次按实际落盘时刻向下取整到分钟取值，保证冻结时间 ≤ 落盘时间） |
| 上游依赖 | `docs/50-testcases.md` **v2**（98 条）/ `docs/10-prd.md` **v2** / `docs/30-architecture.md` **v3** / `docs/error-codes.md` / `docs/60-review.md` **v4**（CHG-15 后定向复审） |
| 失效标记 / 复核结论（2026-09-17） | 上游 `docs/60-review.md` 更新至 **v3**（CHG-14 后重审），本产物基于 v2 复核 → **已复核，结论：无需变更**。60 v3 §B 经独立磁盘核验确认 CHG-14 的 **BUG-01 / BUG-02 / BUG-03 三项修复正确**，与本文件 §1 的 `已闭环` 判定**一致（互相印证，非冲突）**；§A 的 5 项未闭环 REV（REV-10/11/12/16/18）均为建议级且用户已裁决不纳入本轮 → 按纳入规则**不进入本台账**；本轮新增 **REV-20（建议级）**属 code-reviewer 的**格式门禁发现、非测试发现缺陷** → **不登记为 BUG**，已记入 `docs/52-qa-report.md` §3 **OBS-10**。据 60 v3 §D「低于门槛的残余观察」第 3 条：BUG-01「409 未列入 `error-codes.md`」的附带口径问题**已正式关闭**（设计内），见 §2 BUG-01 详情补注。 |
| 失效标记（`50-testcases` v3 触发回写，2026-09-17） | 上游 `docs/50-testcases.md` 变更至 **v3**（仅 TC-76 第三条款口径订正，依用户裁决「解释 1：判 TC-76 用词过严，改用例」）→ 按失效传播矩阵回写本台账：**§0 R4 行**（TC-76 由「3/4 子项通过」→ **4/4 全绿**）、**待确认清单 QX-E-07**（待裁决 → **已裁决并闭环**）。**本台账无新增 / 无状态变更的 BUG**（该口径冲突自始未立 BUG，功能面事实与结论不变：非空确定性覆盖项在生产一律 fail-fast）。 |
| 失效标记（`60-review` **v3 → v4** 触发复核，2026-09-17） | 上游 `docs/60-review.md` 更新至 **v4**（REV-20 经 CHG-15 修复后的**定向复审**：格式门禁 45 → **0**、exit 2 → **exit 0**，v4 判 **已闭环**）→ 按失效传播矩阵**人工复核 + 更新失效标记**：v4 仅更新 **REV-20** 一项，其余 **19 项维持 v3 判定**，**v4 轮新增发现 0 项**、**0 致命 / 严重未闭环**；REV-20 系**代码格式门禁发现、自始未登记为本台账 BUG**（记于 `docs/52-qa-report.md` §3 OBS-10，本轮已标记 **已消除**；本角色同日独立复跑四个项目 `dotnet format whitespace --verify-no-changes` **全部 exit 0**，证据 `tests/e2e/logs/format-gate-rerun-20260917.log`）→ **本台账无新增 / 无状态变更的 BUG；v4 未改变影响 51 结论的任何判定。** |
| 证据引用勘误（2026-09-17） | 核验发现 1 处被引证据文件名在磁盘不存在，已修正：`tc30-regression-20260917-151508.log` → **`…-151255.log`**（§0 R2 / §1 BUG-03 / §0 回归小结 共 3 处；`tests/e2e/logs/` 位于 Playwright `outputDir` 之外、不受运行前自动清空影响 → 判定为笔误，非文件被清理）。 |
| 失效标记（上游 `30-architecture` v4 / `40-changelog` CHG-16+17 / `60-review` v5 变更，2026-09-17 R7 回写） | 上游已变更：`docs/30-architecture.md` **v3 → v4**（D-08 整事务重试耗尽终态 `500` → **`1001`（HTTP 200）**、API-07 语义显式化）、`docs/40-changelog.md` 新增 **CHG-16**（BUG-04 / BUG-05 修复）与 **CHG-17**（用户裁决：`MaxTransactionAttempts` 3 → 2、`CommandTimeout` 30 → 60）→ 按 `docs/artifacts.md` §5 矩阵回写本台账：**BUG-04 / BUG-05 经 R7 实跑复验 → 均置 `已闭环`**（逐条证据见 §2 各 BUG 的「回归验证」块）；`docs/60-review.md` **v4 → v5** 定向复审：新增 **REV-21（建议级、`未闭环`）**，系 **CRITICAL 告警文案把「尝试 2 次」写成「重试 2 次」**（与 D-08「重试 1 次」口径错位）——属**审查问题项、非测试发现的缺陷**，按纳入规则（仅**致命 / 严重**必纳）**不登记为本台账 BUG**；其状态与状态机行 0.5 的判定口径由本角色在 `docs/52-qa-report.md` §2「60 审查项状态」节单列（结论：REV-21 不在 51 台账内，**不计入行 0.5**；如实标注供主对话向用户上报）。**【本轮更新（2026-09-17）：`docs/60-review.md` 已至 v6 —— REV-21 经 CHG-18 修复后转 `已闭环`（v6 §A：21 项 = 已闭环 16 / 已接受（用户决断）5 / 未闭环 0；致命 / 严重未闭环 0 项）；本台账无对应 BUG 新增或状态变更（REV-21 自始不在本台账内）。】** |
| 失效标记（R7 回归回写，2026-09-17） | 本批**不改动任何上游契约**；回归执行 = **TC-56b / TC-56c / TC-48c → 3/3 PASS**（第 3 轮回归，命令 / 原始输出 / 关键实测值见 §2 各 BUG 的「回归验证」块）。**BUG-04（建议）、BUG-05（一般）状态 → `已闭环`；当前未闭环缺陷 0 条；无致命 / 严重缺陷。** |
| 失效标记（`40-changelog` **CHG-18** + `60-review` **v6** 触发同步回写，2026-09-17） | 上游两处变更：`docs/40-changelog.md` 新增 **CHG-18**（REV-21 修复：终态 CRITICAL 告警文案计数口径「尝试次数 → 重试次数」+ 守护性断言）、`docs/60-review.md` **v6**（CHG-18 定向复审：**REV-21 → `已闭环`**；现行 21 项 = 已闭环 16 / 已接受（用户决断）5 / **未闭环 0**；**致命 / 严重未闭环 0 项**）。→ 按 `docs/artifacts.md` §5 矩阵回写本台账：**本台账无新增 BUG、无 BUG 状态变更**（REV-21 属 `60-review` 审查问题项、按纳入规则未登记为 BUG，其状态同步见本元信息块上一行与 `docs/52-qa-report.md` §2「60 审查项状态」）。**BUG-04 / BUG-05 状态维持 `已闭环` 不变**（本轮无对应回归动作）；**未修改本文件任何既有 BUG 记录**（历史证据按原样保留）。 |
| 写入者 | test-executor |
| 状态 | **回归轮完成（2026-09-17）**：**已执行 98/98、未执行 0 条**——最后一批 **R6（破坏性批：TC-56b / TC-56c）2/2 PASS**（见 §0 R6 与 `docs/52-qa-report.md` §1.6.6）。首轮 85/98 → R1–R5 补做 11 条 → **R6 补做 2 条**，覆盖缺口全部清零（§4 清单为空）。回归轮结论：BUG-01 / BUG-02 / BUG-03 **`已闭环`**；**新增 BUG-04（建议，未闭环）** 与 **BUG-05（一般，未闭环；R6 执行 TC-56b 时暴露，见 §2）**；待确认项 **QX-E-07 已裁决（用户决断）并闭环**（50 v3 + 断言修订 + 复跑 3 passed）；**QX-E-05 已消解**（统计抽样已按不改 `src/` 的方式执行完毕，无需再授权）。首轮基线见本文件 v1 冻结内容。**R7（CHG-16 / CHG-17 修复后回归，2026-09-17 18:27–18:30）**：TC-56b / TC-56c / TC-48c **3/3 PASS**；**BUG-04、BUG-05 均 → `已闭环`**（验证方式逐条见 §2）；**未闭环缺陷 0 条**；**（R7 时点）唯一未闭环建议级项 = `docs/60-review.md` v5 的 **REV-21**（审查项，非本台账 BUG，见 52 §2「60 审查项状态」）；【2026-09-17 更新：REV-21 已经 **CHG-18** 修复 → `已闭环`（60 v6 §A），见本元信息块「CHG-18 / v6 同步回写」失效标记行】** |
| 失效标记 / 复核结论（R6 破坏性批回写，2026-09-17） | 本批**不改动任何上游契约**（`50` v3 未变、`30-architecture` 未变、`60-review` v4 未变），故无失效传播；本文件与 `docs/52-qa-report.md` 为同批同源回写。**R6 新增 BUG-05（一般，`已闭环`）**，其关联 TC 为 **TC-56b**（该用例本身判定 **PASS**——回滚一致性 5/5 通过；BUG-05 系用例执行过程**暴露**的产品问题，**非**用例判据失败）。**最高严重程度由「一般」（首轮 BUG-01）保持为「一般」**；当前未闭环：**BUG-04（建议）+ BUG-05（一般）**；**无未闭环致命 / 严重**。 |

---

## 0. 回归轮进度（2026-09-17，进行中）

**本轮授权**：主对话转达用户裁决 —— BUG-01 **返工**（engineer 已修 CHG-14）；BUG-02、BUG-03 **一并修复**（CHG-14）；13 条覆盖缺口**授权补做**（可用环境变量另起实例改运行配置，**不得改 `src/`**）。

| 批次 | 内容 | 状态 |
| --- | --- | --- |
| R1 | 回归 TC-08 / TC-17（BUG-01）、TC-63（BUG-02）：`node tests/e2e/api/api-round2.mjs` | **已完成（2026-09-17 15:12）**：TC-08 **PASS**（http=409）、TC-17 **PASS**（http=409）、TC-63 **PASS**（winTime=`2026-09-17T07:12:32.462764+00:00`，UTC=true）；脚本汇总 **PASS=7 / FAIL=1**（唯一 FAIL 为 TC-53，已知不可达分支 OBS-03，非缺陷）。证据：`tests/e2e/logs/api-round2-regression-20260917-151229.log` |
| R2 | 回归 TC-30（BUG-03）：E2E 错误态文案实测 | **已完成（2026-09-17 15:12，以证据文件名时间戳为准）**：**PASS**，实测文案 `奖池加载失败 \| 系统繁忙，请稍后重试 \| 重试`（1.1s）；证据 `tests/e2e/logs/tc30-regression-20260917-151255.log`（**勘误**：原文误引 `…-151508.log`，该文件名在磁盘不存在）。断言已按 50 原文收紧为 `toContain('奖池加载失败')` |
| R3 | 全量 E2E 重跑（`src/` 变更 → 矩阵强制） | **已完成（2026-09-17 15:13–15:17）**：**44 passed / 6 failed**（4.1m）。6 个失败 = TC-02、TC-04、TC-07、TC-29、TC-42、TC-66；其中 TC-02 / TC-04 / TC-07 的实测响应为 **429 限流**（页面文案「系统繁忙，请稍后重试」，即注册 10 次/分钟（按 IP）窗口被前序用例占满），TC-29 / TC-42 / TC-66 为 `Test timeout of 30000ms exceeded`。**隔离复跑（`--grep`，2026-09-17 15:19）：6 passed（16.9s）**→ 判定为**用例隔离缺陷（同类 OBS-07），非产品缺陷、非 CHG-14 回归劣化**。证据：`tests/e2e/logs/full-run-regression-20260917-151311.log` + `tests/e2e/logs/isolate-rerun-20260917-151932.log` |
| R4 | 覆盖补做 13 条：TC-23、TC-25、TC-38b、TC-48c、TC-56b、TC-56c、TC-76、TC-77、TC-78、TC-79、TC-79b、TC-84、TC-85 | **已完成 7 / 13**（2026-09-17，TC-76 收官 15:52）：TC-23 / TC-25 / TC-38b / TC-48c / TC-76 / TC-77 / TC-85 已执行，脚本化于 `tests/e2e/coverage.spec.ts`（10978 → 32336 字节）。结果：TC-23 / TC-25 / TC-38b **通过**；TC-48c 通过但暴露 **BUG-04**；**TC-77 / TC-85 通过**；**TC-76 经用户裁决后 4/4 子项全绿**——①生产基线取值齐 D-06 配置键表（`Enabled=false` / `ForcedResults[]` 空 / `WeightOverrides{}` 空）、②生产配置正常启动（401 就绪探针）、③三类确定性注入全部 fail-fast、④第三条款按 **50 v3** 取值口径修订断言后复跑通过（**3 passed / 15.0s**）。原「不含该节」口径冲突 = **QX-E-07：已裁决（用户决断）并闭环**（判 TC-76 原用词过严 → 改用例；**未立 BUG**、无工程侧改动）。证据：`tests/e2e/logs/deploy-tc76-77-85-v3-20260917-155208-01.log`、`coverage-tc76-prod-start-20260917-155210-01.log`、`coverage-tc76-failfast-1/2/3-20260917-1552*.log`、`coverage-tc77-testing-20260917-155215-05.log`（首跑 15:43 同结论日志亦在目录中）；详见 `docs/52-qa-report.md` §1.6.3 / §1.6.4。**剩余 6 条**（TC-56b / TC-56c / TC-78 / TC-79 / TC-79b / TC-84）待后续实例（其中 TC-78 / TC-79 / TC-79b / TC-84 已由 **R5** 补做完毕，见下行） |
| R5 | 覆盖补做（统计 / 性能）4 条：TC-78、TC-79、TC-79b、TC-84 | **已完成（2026-09-17 16:42–16:48）；4/4 PASS**：TC-78（n=10000，最大偏差 **0.300pp** ≤ 2pp，频率合计 **100.000%**）、TC-79（n=2000，库存置 0 的 `prize-coupon` **0 次**中出，最大偏差 **1.150pp**）、TC-79b（n=2000，全局固定概率假设被拒 **20.000pp** vs 候选集实时归一 **1.150pp**；`IsEnabled` 停用维度同结论，最大偏差 **1.259pp**、earbuds 0 次）、TC-84（抽奖 P95 **116.97ms** ≤ 500ms、记录列表 P95 **14.65ms** ≤ 300ms、**0 超时 / 0 失败**）。方法：另起 Testing 实例 :5199（发布产物、`ContentRoot=发布目录`、宿主日志 `Hosting environment: Testing`）、库 `luckydraw_test`、Redis db=2、`Draw__DailyLimit=30000` / `RateLimit__DrawPerMinute=100000` **仅经环境变量注入**；库存以夹具钉高（`Stock=1000000 WHERE Type<>3`）保证候选集恒定，**运行后已复位 FR-03 默认值并 flush Redis db=2**；**未改 `src/` 一行、未改任何配置文件**。**本批 0 新缺陷**；执行中的环境侧问题（ContentRoot 致 `appsettings*.json` 不加载 → 空签名密钥 → 令牌类接口 500）属**执行方式缺陷（启动方式错误）**，纠正后全绿，另登记观察项 **OBS-11**（见 `docs/52-qa-report.md` §3）。脚本：`tests/e2e/api/stats-suite.mjs` / `tests/e2e/api/perf-suite.mjs`（共用 `tests/e2e/api/stats-lib.mjs`，无新增第三方依赖）。证据：`tests/e2e/logs/stats-run-20260917-164218.log`（12873 B）、`tests/e2e/logs/perf-tc84-20260917-164745.log`（6581 B）、实例日志 `tests/e2e/logs/stats-instance-20260917-164034.log`；详见 `docs/52-qa-report.md` §1.6.5 |
| **R6** | 破坏性批（依赖故障注入）2 条：**TC-56b**、**TC-56c** —— 覆盖缺口最终清零 | **已完成（2026-09-17 17:08–17:18）；2/2 PASS**，**新增缺陷 1 条：BUG-05（一般，`已闭环`）**。**TC-56b PASS**：以「另一连接对 `User` 行持 X 锁」注入失败，在飞期经 `sys.innodb_lock_waits` 抓到等待方 trx 状态 `LOCK WAIT`、`trx_rows_modified=4`、被阻塞语句字面 = `INSERT INTO WinningRecord`（即写事务第 ⑥ 步），抽奖在 **30.6s** 后失败；**回滚一致性 5/5 通过**（UsedCount 1→1、`prize-mug` Stock 4→4、WinningRecord 1→1、DrawRequest 1→1、AuditLog(Draw) 1→1），AUTO_INCREMENT 前进量 DrawRequest +1 / WinningRecord +1 独立佐证「行确实插入过又被回滚」。**TC-56c PASS**：停 `luckydraw-redis`（TCP 6379 → ECONNREFUSED）后重放同一幂等键 → 结果与首次全等、UsedCount 1→1、无新记录、库存逐行不变；新键抽奖 code=0 且次数正常 +1；`/draw/quota`、`/records` 可用且与库内一致；实例日志命中设计内降级 warning「读取幂等结果缓存失败，退化为数据库幂等路径」「写入幂等结果缓存失败（不影响业务结果）」→ 架构 §6.1「Redis 不是抽奖主链路的强依赖」实测成立；收尾已 `docker start` 并验证注册 + 抽奖恢复。方法与复位：**未改 `src/` 一行、未改任何配置文件**；奖池以 SQL 夹具收窄（仅 `prize-mug` 入候选）后**已复位 FR-03 默认值**（实测 ✔）、Redis db2 已 flush、`innodb_lock_wait_timeout` 全程未改（终态 50）、无残留锁持有连接 / 事务（终态 0 / 0）。脚本：`tests/e2e/api/tc56b-tx-rollback.mjs`（415 行）、`tests/e2e/api/tc56c-redis-degradation.mjs`（354 行）。证据：`tests/e2e/logs/tc56b-real-20260917-170833.log`、`tests/e2e/logs/tc56b-instance-excerpt-20260917-170943.log`、`tests/e2e/logs/tc56c-run2-20260917-171700.log`、`tests/e2e/logs/tc56-cleanup-20260917-171736.log`；详见 `docs/52-qa-report.md` §1.6.6 |
| **R7** | 修复循环 b 步回归（CHG-16 / CHG-17 修复后）：**TC-48c**、**TC-56b**、**TC-56c** | **已完成（2026-09-17 18:27–18:30）；3/3 PASS、0 新缺陷**。**TC-56b PASS**：同一锁竞争注入法（`HOLD_SECONDS=300` > 重试预算 ≈100s，确保观察到「重试耗尽」而非「重试成功」），终态 **`http=200` / `code=1001`「系统繁忙，请稍后重试」/ elapsed=101037ms**（修复前 500 / 30615ms）；回滚一致性 **5/5**（UsedCount 1→1、`prize-mug` Stock 4→4、WinningRecord 1→1、DrawRequest 1→1、AuditLog(Draw) 1→1）；AUTO_INCREMENT DrawRequest +2 / WinningRecord +2（两次尝试均插入过并回滚）；实例日志 `CommandTimeout='60'`、attempt1 阻塞 50,012ms → `[WRN] …重试整个事务（第 1 次）`、attempt2 阻塞 50,005ms → `[FTL] …重试 2 次后仍遇数据库瞬时错误…`（措辞问题即 60 v5 REV-21 所指）、`[WRN] 业务异常：Code=1001`。**TC-56c PASS**：停 Redis（ECONNREFUSED）后重放结果全等（`{"itemId":5,"isWin":false,"remainingAttempts":29999}`）、UsedCount 1→1；新键 `code=0`、UsedCount 1→2、`prize-mug` Stock 50→49；`/draw/quota` = 29998 与库内一致、`/records` `totalCount=1` 与库内一致（`[OK]`）；降级 warning 各 2 行；收尾恢复 PONG。**TC-48c PASS**：弹层「恭喜获得 三等奖 · 定制马克杯」（名称不再为空）、**奖池重拉次数 = 1**（修复前 0）、重拉后快照含目标条目、无白屏。**BUG-04、BUG-05 经实跑复验 → 均置 `已闭环`**（逐条「回归轮次 + 验证方式」见 §2）；**未闭环缺陷 0 条**。断言变更（只加强，未删弱）：新增 `terminalOk`（钉死 1001 / HTTP 200）+ `budgetOk`（≥90s 证明重试确实发生）。环境偏离：`:5180` 已停（未触碰）→ TC-48c 以脚本内 `page.route` 转发 `:5197` 重跑；TC-56b 执行实例 `:5198`；`:5199`（PID 36564）经签发令牌健康检查复验通过。证据：`tests/e2e/logs/tc56b-rerun-20260917-182721.log`、`tests/e2e/logs/tc56-rerun-instance-20260917-182525.log`、`tests/e2e/logs/tc56c-rerun-20260917-183000.log`、`tests/e2e/logs/tc48c-rerun-20260917-182926.log`、`tests/e2e/logs/tc48c-instance-20260917-182750.log`；详见 `docs/52-qa-report.md` §1.6.7 |

**60-review 纳入说明**：`docs/60-review.md` v2 重跑结论为「闭环 14 / 未闭环 5 / 新增严重及以上 0」，5 项未闭环（REV-10/11/12/16/18）**全部为建议级**且已由用户裁决不纳入本轮。按角色定义的纳入规则（未闭环的**致命 / 严重**项必须纳入 51），**本轮无 60 来源的缺陷条目**（未重开该 5 项）。**v3 更新（2026-09-17）**：§B 经独立磁盘核验确认 CHG-14 三项修复正确（与本台账 `已闭环` 判定互相印证）；新增 **REV-20**（建议级：Infrastructure 项目 `dotnet format whitespace --verify-no-changes` 为红）系**代码格式门禁发现、非测试发现的产品缺陷** → **不登记为 BUG、不进本台账**，已如实记入 `docs/52-qa-report.md` §3 **OBS-10**（测试侧独立复跑：总数 45、行范围 95–121），其是否阻断由主对话裁决。**v4 更新（2026-09-17）**：REV-20 经 CHG-15 修复后定向复审判 **已闭环**（格式门禁 45 → **0**、exit 2 → **exit 0**）；本角色同日独立复跑四个项目 `dotnet format whitespace --verify-no-changes` **全部 exit 0**（证据 `tests/e2e/logs/format-gate-rerun-20260917.log`）；v4 轮**新增发现 0 项**、其余 **19 项维持 v3 判定**、**0 致命 / 严重未闭环** → 本台账**无对应缺陷条目新增或变更**。

---

## 1. 缺陷清单

| BUG | 严重程度 | 状态 | 关联 TC | 关联 CHG / REV | 标题 | 证据 |
| --- | --- | --- | --- | --- | --- | --- |
| BUG-01 | **一般** | **`已闭环`** | TC-08、TC-17 | CHG-14 | 注册 / 登录「同键异体」返回 HTTP 200 + body `code=409`，契约要求 **HTTP 409** | 首轮：`tests/e2e/logs/api-round2-20260917-145013.log`；**回归（第 2 轮）：`tests/e2e/logs/api-round2-regression-20260917-151229.log`** |
| BUG-02 | 建议 | **`已闭环`** | TC-63 | CHG-14 | `/api/v1/records` 的 `winTime` 未携带时区标识，不符合「ISO 8601（UTC）」的完整口径 | 首轮：`api-round2-20260917-145013.log`；**回归（第 2 轮）：`api-round2-regression-20260917-151229.log`** |
| BUG-03 | 建议 | **`已闭环`** | TC-30 | CHG-14 | 奖池加载失败态标题为「加载失败」，原型冻结面为「奖池加载失败」 | 首轮：`full-run-20260917-144348.log`；**回归（第 2 轮）：`tests/e2e/logs/tc30-regression-20260917-151255.log`** |
| **BUG-04** | 建议 | **`已闭环`**（R7 复跑验证） | TC-48c | **无**（覆盖补做暴露，非 CHG 引入） | 抽奖结果条目不在当前奖池快照时：未按 50 原文「重拉奖池一次再定位」，结果弹层**奖品名称为空**（「恭喜获得」后无名称） | `tests/e2e/logs/coverage-20260917-152011.log` 中 `TC-48c :: …弹层文案="恭喜获得  已记入我的中奖记录 …"；抽奖后奖池重拉次数=0` |

| **BUG-05** | **一般** | **`已闭环`**（R7 复跑验证） | TC-56b（用例 **PASS**，缺陷系执行中暴露） | **无**（非 CHG 引入；`MySqlErrors` 分类缺失自首轮交付即存在） | 抽奖事务**后半段写入点**（⑥ 中奖记录 / ⑦ 审计 / ⑧ 回填）未做瞬时错误分类，`DrawService` 的整事务重试机制对其失效 → 锁竞争下用户得到 **HTTP 500 / `code=500`「系统内部错误」**（设计为 **`1001`「系统繁忙，请稍后重试」**），且白等 30s | `tests/e2e/logs/tc56b-instance-excerpt-20260917-170943.log`（`CommandTimeout='30'` → 30039.97ms → 500）+ `tests/e2e/logs/tc56b-real-20260917-170833.log` |

**最高严重程度：一般（BUG-05，`已闭环`）。无致命 / 严重级缺陷；未闭环缺陷 0 条。** 审查项层面未闭环 **0** 条（`docs/60-review.md` **v6**：现行 21 项 = 已闭环 16 / 已接受（用户决断）5 / **未闭环 0**；**REV-21** 经 CHG-18 修复 → `已闭环`，非本台账 BUG，见 `docs/52-qa-report.md` §2「60 审查项状态」）。

> **本轮新增缺陷（1 条，来自授权覆盖补做的 TC-48c，**非** CHG-14 引入）**：见 §1.1 的 BUG-04；该缺陷为本轮把首轮「未执行」的 TC-48c 脚本化后暴露，与 CHG-14 的三项修复无因果关系。

> **本批（R5：TC-78 / TC-79 / TC-79b / TC-84）新增缺陷：0 条（显式声明）**。四类新覆盖面（统计抽样两层、概率实时归一、接口分位值）实测均满足契约阈值且未放宽断言；执行过程中的环境侧问题经独立排查确认**属执行方式缺陷（启动方式错误：发布产物以仓库根目录为 ContentRoot 启动 → `appsettings*.json` 不加载 → 空签名密钥）**，纠正后全绿，**非产品缺陷、未立 BUG**；其中暴露的「空签名密钥三处口径不一致」按要求登记为 **52 §3 OBS-11（观察项；属 REV-10 范围、用户已裁决「不纳入本轮」→ 不构成修复要求、不作为阻断项）**。

> **本批（R6：TC-56b / TC-56c 破坏性批）新增缺陷：1 条（显式声明）** —— **BUG-05（一般，`已闭环`）**，详见 §1.1。**两条用例本身均判 PASS**：TC-56b 的回滚一致性判据 **5/5 全部通过**（用例要验的「三方一致」成立），BUG-05 是执行过程中**暴露**的独立产品问题（错误码分层与重试机制失效），**不是** TC-56b 的判据失败，也不改变其结论。**TC-56c 0 缺陷**：架构 §6.1「Redis 不是抽奖主链路的强依赖」的 4 项承诺（抽奖与扣减走 MySQL / 次数与记录查询不依赖 Redis / 幂等重放退化为 DB 路径且不重复扣次 / 重放结果一致）**逐项实测成立**，并取得设计内降级 warning 的正向证据。

> **回归轮小结（2026-09-17 第 2 轮）**：BUG-01 / BUG-02 / BUG-03 三条**全部实测转绿，状态置 `已闭环`**。
> | BUG | 关联 TC | 回归结论 | 回归轮次 + 验证方式 | 证据 |
> | --- | --- | --- | --- | --- |
> | BUG-01 | TC-08、TC-17 | **通过**：注册与登录「同键异体」均已返回 **HTTP 409**（`http=409 code=409`），未签发新凭证、未产生第二次副作用 | 第 2 轮（CHG-14 后）/ 接口级 `node tests/e2e/api/api-round2.mjs` 复跑 | `tests/e2e/logs/api-round2-regression-20260917-151229.log`（TC-08 / TC-17 行） |
> | BUG-02 | TC-63 | **通过**：`winTime = 2026-09-17T07:12:32.462764+00:00`（ISO 8601=true，**UTC=true**），与库中 UTC 值 `2026-09-17 07:12:32.462764` 一致 | 第 2 轮（CHG-14 后）/ 同脚本 | 同日志 TC-63 行 |
> | BUG-03 | TC-30 | **通过（文案已改）**：错误态实测文案 = **「奖池加载失败 \| 系统繁忙，请稍后重试 \| 重试」**，与原型冻结面逐字一致；重试后进入 `page-draw--default`，次数保持 3（只读） | 第 2 轮（CHG-14 后）/ E2E `--grep "TC-30"`；**断言已按 50 原文收紧**为 `toContain('奖池加载失败')`（原为 `toContain('加载失败')`，无法区分修复） | `tests/e2e/logs/tc30-regression-20260917-151255.log` |
>
> **回归未引入新缺陷**（该批脚本汇总 `PASS=7 / FAIL=1`，唯一 FAIL 为 TC-53 的已知不可达分支 OBS-03，首轮即如此，非回归劣化）。

---

## 2. 缺陷详情

### BUG-01　（一般，`已闭环`）注册 / 登录「同键异体」返回 HTTP 200 + body `code=409`

- **编号**：BUG-01
- **关联 TC**：TC-08（步骤 3）、TC-17（步骤 3）
- **关联 CHG / REV**：CHG（`docs/30-architecture.md` **D-15 / §5.4** 注册 / 登录幂等实现）
- **严重程度**：一般
- **状态**：**`已闭环`**（第 3 轮回归 · R7 复跑验证，验证方式见本 BUG 文末「回归验证」块）
- **复现步骤**：
  1. 注册用户 `A`（`POST /api/v1/auth/register`，请求头 `Idempotency-Key: K1`，请求体 `{userName:"A", password:"Abcd1234", confirmPassword:"Abcd1234"}`）→ 200 / `code=0`。
  2. 以**同一键 K1**、**不同请求体**再次调用注册接口（`userName` 改为 `Ax`）。
  3. 观察 HTTP 状态码与响应体。
  - 登录侧同构：`POST /api/v1/auth/login`，同一键 K2 先以正确密码提交、再以 `password: "Abcd1234z"` 提交。
- **实际结果**：
  - 注册：`HTTP 200`，响应体 `{"code":409,...}`；未创建新用户（`User` 表无 `Ax` 行），未产生第二次业务副作用 —— 冲突**被识别**了。
  - 登录：`HTTP 200`，响应体 `code=409`。
  - 即：**业务码正确（409）、HTTP 状态码错误（200）**。
- **期望结果**（依据 `docs/30-architecture.md` §5.1「错误码分工」行与 §5.4）：
  - 「`409` 幂等冲突」属 §5.1 中与 `400/401/403/429/500` 并列的 **HTTP 状态码**分工，应为 **HTTP 409**；
  - `docs/50-testcases.md` §0 统一口径表亦明确「幂等冲突 → HTTP 409（30 §5.4）」，并注明「期望结果一律按此写，不得写成『返回 400』」。
  - 附带口径问题：`code=409` **低于 1000**，与 §5.1「业务异常 `code ≥ 1000`」的成功 / 业务异常二分口径不一致（`409` 未登记在 `docs/error-codes.md` 的业务码表中）。**（2026-09-17 复核补注：`docs/60-review.md` v3 §D「低于门槛的残余观察」第 3 条已**正式关闭**本附带口径问题 —— `error-codes.md:4` 边界条款明示 `4xx/5xx` 段与 HTTP 状态码对齐，`409` 由 `30-architecture.md:598` 承接登记，属设计内，不再追办）**。
- **影响范围**：
  - 本项目前端按 `response.data.code` 判定，`utils/error.ts` 已统一映射 409 文案，**用户可见行为不受影响**（TC-71 已实测前端对 409 展示「请勿重复提交」）。
  - 影响面在**契约一致性**与**非浏览器客户端**：第三方 / 移动端按 HTTP 状态码判定时，会把一次被拒绝的重复提交当作成功；同时使 §5.1 的错误码分工表对 `409` 失效。
  - 影响接口：`POST /api/v1/auth/register`（API-01）、`POST /api/v1/auth/login`（API-02）。抽奖侧 `POST /api/v1/draw` 的 409 分支在契约下不可达，见 §3 观察项 OBS-03。
- **证据**：
  - `api-round2.mjs` 运行输出（TC-08）：
    `步骤1 code=0；步骤3（同键 K1 + 不同 userName）http=200 code=409 是否签发新凭证=no；用户表新建记录数 name=1 other=0`
  - `api-round2.mjs` 运行输出（TC-17）：
    `步骤1 code=0；步骤3（同键 K2 + 不同请求体）http=200 code=409`
  - 无 Playwright 截图（接口级用例，证据为脚本标准输出；脚本可重跑复现：`node tests/e2e/api/api-round2.mjs`）。

### BUG-02　（建议，`已闭环`）`/api/v1/records` 的 `winTime` 未携带时区标识

- **编号**：BUG-02
- **关联 TC**：TC-63
- **关联 CHG / REV**：CHG（API-08 响应数据契约）
- **严重程度**：建议
- **状态**：**`已闭环`**（第 3 轮回归 · R7 复跑验证，验证方式见本 BUG 文末「回归验证」块）
- **复现步骤**：
  1. 以任一有中奖记录的用户调用 `GET /api/v1/records?pageIndex=1&pageSize=10`。
  2. 观察 `data.items[0].winTime` 的字符串形态。
- **实际结果**：`winTime = "2026-09-17T06:37:31"` —— **无 `Z` / `+00:00` 时区标识**（同时分秒后无毫秒）。该值与库中 UTC 时间一致（`WinningRecord.CreateTime = 2026-09-17 06:37:31`，UTC）。
- **期望结果**（依据 `docs/30-architecture.md` §5.1「时间 `ISO 8601 字符串（UTC）`」与 API-08 行「`winTime` = ISO 8601（UTC）」）：应携带 UTC 时区标识（如 `2026-09-17T06:37:31Z` 或 `+00:00`），使「UTC」这一口径**自描述**，而非依赖接收方事先约定。
- **影响范围**：
  - **本项目前端不受影响**：`utils/datetime.ts` 的 `parseUtc()` 对无时区标识的字符串按 UTC 解析，`formatUtc8DateTime` 再手动 `+8h`，实测页面正确展示 `2026-09-17 14:37`（见 TC-63 运行日志）。
  - 影响面在**契约自描述性与互操作**：任何直接以 `new Date(winTime)` 解析的消费者（脚本、第三方客户端、后续新增前端模块）会按**本地时间**解释该值，在 UTC+8 环境下产生 **8 小时**偏差。
  - 影响接口：`GET /api/v1/records`（API-08）。同类结构风险：`GET /api/v1/draw/quota` 的 `resetAt` 已正确携带 `+00:00`（TC-38c 实测 `2026-09-17T16:00:00+00:00`），两处口径不一致。
- **证据**：
  - `api-round2.mjs` 运行输出（TC-63）：`记录字段=id,prizeName,winTime；时间值=2026-09-17T06:37:31（ISO 8601=true，UTC=false）；DB 原始=2026-09-17 06:37:31.783156`
  - E2E 侧 TC-63 运行日志：`接口 winTime=2026-09-17T06:37:31（无时区标识）；期望展示(UTC+8)=2026-09-17 14:37；若按本地时间误解析则为=2026-09-17 06:37；页面含期望值=true`（页面展示正确，故定级为建议）

### BUG-03　（建议，`已闭环`）奖池加载失败态标题为「加载失败」，原型冻结面为「奖池加载失败」

- **编号**：BUG-03
- **关联 TC**：TC-30
- **关联 CHG / REV**：CHG（前端奖池错误态文案）
- **严重程度**：建议
- **状态**：**`已闭环`**（第 3 轮回归 · R7 复跑验证，验证方式见本 BUG 文末「回归验证」块）
- **复现步骤**：
  1. 已登录进入抽奖页。
  2. 令 `GET /api/v1/prizes` 请求失败（E2E 中以 `route.abort('failed')` 注入）。
  3. 观察 `page-draw--error` 区域的文案。
- **实际结果**：错误态文案为 **「加载失败」** + 「系统繁忙，请稍后重试」+ 重试按钮（destructive Alert）。
- **期望结果**（依据 `docs/20-prototype.html` 冻结面与 `docs/50-testcases.md` TC-30）：抽奖页奖池错误态标题应为 **「奖池加载失败」**。原型中该文案出现在状态锚点说明中：「奖池加载失败，可重试（Alert destructive）」「奖池加载失败 / 系统繁忙，请稍后重试」「奖池加载失败」+「系统繁忙，请稍后重试」+ 重试按钮。
- **影响范围**：
  - 纯文案偏差，**功能不受影响**（错误态锚点、重试按钮、重试后恢复 `page-draw--default`、重试为只读请求均已实测通过）。
  - 现实根因可理解：前端把该文案集中为 `messages.ts` 的通用键 `loadFailedTitle: '加载失败'`（这同时满足了 TC-73b 的「文案集中管理」要求），代价是丢失了「奖池」这一限定词。
  - 影响范围：抽奖页奖池错误态（`page-draw--error`）。记录页错误态（`page-records--error`）的原型口径同样是「加载失败」，无偏差。
- **证据**：
  - `tests/e2e/qa.spec.ts` TC-30 运行日志：`TC-30 :: 错误态文案="加载失败 | 系统繁忙，请稍后重试 | 重试"`
  - 前端实现：`src/frontend/src/utils/messages.ts:17` `loadFailedTitle: '加载失败'`
  - 原型冻结面：`docs/20-prototype.html`（`奖池加载失败` 出现于抽奖页错误态说明）

### BUG-04　（建议，`已闭环`）结果条目不在奖池快照时未重拉奖池，弹层奖品名称为空

- **编号**：BUG-04
- **关联 TC**：TC-48c（本轮覆盖补做，首轮未执行）
- **关联 CHG / REV**：**无**（未由 CHG-14 引入；该路径代码在首轮交付中即存在，本轮首次脚本化后暴露）
- **严重程度**：建议
- **状态**：**`已闭环`**（第 3 轮回归 · R7 复跑验证，验证方式见本 BUG 文末「回归验证」块）
- **复现步骤**（`tests/e2e/coverage.spec.ts` 的 TC-48c 已脚本化，可重跑）：
  1. 直连测试库夹具：`UPDATE PrizeItem SET IsEnabled = 0 WHERE Code = 'prize-mug'`（页面加载时该条目停用）。
  2. 注册一次性用户并进入 `/draw`：前端奖池快照**不含**该条目（实测快照 id 集合不含 `id=3`）。
  3. 抽奖前夹具：启用该条目（`IsEnabled = 1, Stock = 5`）、其余实物库存置 0、`no-prize` 权重置 0 → 该条目成为**唯一可中出候选**。
  4. 点击抽奖，观察结果弹层文案与「抽奖后是否重拉奖池」。
- **实际结果**：
  - 后端返回 `{"itemId":3,"isWin":true,...}`（即快照外条目）；前端**跳过旋转直接展示结果弹层**（不卡动画、不白屏）—— 关键不变量满足。
  - **弹层文案 = 「恭喜获得  已记入我的中奖记录  今日剩余次数：2 次  查看我的中奖记录 继续抽奖」** —— **奖品名称缺失**（`prizeName` 取自快照，快照无该条目）。
  - **抽奖后奖池重拉次数 = 0**（前端未发起第二次 `GET /api/v1/prizes`）。
- **期望结果**（依据 `docs/50-testcases.md` TC-48c 原文，FR-05-R10）：条目不在当前奖池快照时**重拉奖池一次再定位**；仍不可定位时才跳过旋转直接展示结果 —— 且「保证结果反馈不被渲染边界阻塞、不白屏」。
- **影响范围**：
  - 触发条件为**快照竞态**（页面加载后奖池发生启用/停用变更，且该条目恰好成为抽奖结果），现实窗口小 → 定级**建议**。
  - 用户可见后果：极少数情况下结果弹层出现「恭喜获得」+ 空名称，缺少奖品名（未中奖路径不受影响）。
  - 影响面：抽奖页结果弹层（`page-draw--win` 的奖品名）；「跳过旋转直接展示结果」与「不白屏」两个不变量**均已满足**。
  - 备注：`DrawResponseDto` 仅含 `itemId / isWin / remainingAttempts`（无条目名称），故「重拉奖池」是前端取得名称的唯一通道 —— 实现该步骤即可同时消除本缺陷。
- **证据**：
  - `tests/e2e/logs/coverage-20260917-152011.log`：`TC-48c :: 后端返回 itemId=3（快照外条目 id=3）；弹层文案="恭喜获得  已记入我的中奖记录  今日剩余次数：2 次  查看我的中奖记录 继续抽奖 Close"；抽奖后奖池重拉次数=0（50 原文期望「重拉奖池一次再定位」）`
  - 实现侧：`src/frontend/src/views/draw/DrawView.vue:85-89`（`resultIndex < 0` 时直接 `settled = true; dialogOpen = true`，无重拉）；`src/frontend/src/views/draw/DrawView.vue:55`（`prizeName` 仅从 `prizeItems` 快照取名，未命中返回空串）

- **回归验证（第 3 轮回归 · R7，2026-09-17 18:29）**：**复跑验证通过 → 状态置 `已闭环`**（CHG-16 前端修复后，按 TC-48c 原步骤回写复跑）。
  - 验证方式：`node node_modules/@playwright/test/cli.js test tc48c-regression.spec.ts`（本轮新增回归脚本；脚本内以 `page.route` 把 `/api/v1/**` 转发到修复后实例，环境偏离说明见 `docs/52-qa-report.md` §1.6.7）
  - 关键实测值：`1 passed (7.6s)`；抽奖弹层文案 = 「恭喜获得 **三等奖 · 定制马克杯** 已记入我的中奖记录 今日剩余次数：2 次 …」（奖品名称不再为空）；**抽奖后奖池重拉次数 = 1**（修复前实测 = 0）；重拉后快照含目标条目 = true；无白屏
  - 证据：`tests/e2e/logs/tc48c-rerun-20260917-182926.log`

### BUG-05　（一般，`已闭环`）抽奖事务后半段写入点的瞬时错误未分类：重试机制对其失效，用户得到 500「系统内部错误」而非 1001「系统繁忙」

- **编号**：BUG-05
- **关联 TC**：**TC-56b**（该用例 **PASS**；本缺陷系**执行过程中暴露**，非用例判据失败——用例判据「三方一致回滚」实测 5/5 通过）
- **关联 CHG / REV**：**无**（非 CHG-14/15 引入；`MySqlErrors` 的分类覆盖面自首轮交付即如此，本轮首次以真实锁竞争将其暴露）
- **严重程度**：**一般**
- **状态**：**`已闭环`**（第 3 轮回归 · R7 复跑验证，验证方式见本 BUG 文末「回归验证」块）
- **复现步骤**（已脚本化，可重跑）：
  1. `node tests/e2e/api/tc56b-tx-rollback.mjs`（脚本内 `TC56B_HOLD=300` 为默认；脚本会自建前置：注册用户 → 候选集收窄至 `prize-mug` → 探针抽奖建立 `UserDrawQuota` 行 → 在另一连接对 `User` 行 `START TRANSACTION; SELECT … FOR UPDATE` 持 X 锁 → 触发抽奖）。
  2. 观察抽奖请求的 HTTP 状态码、响应体与耗时。
- **实际结果**：
  - 客户端在第 **30615 ms** 收到 **`HTTP 500`**，响应体 `{"code":500,"message":"系统内部错误","data":null}`。
  - 实例日志同源证据（`tests/e2e/logs/tc56b-instance-excerpt-20260917-170943.log`）：
    ```
    [17:09:06 ERR] Failed executing DbCommand (30,016ms) [Parameters=[…], CommandType='Text', CommandTimeout='30']
    INSERT INTO `WinningRecord` (`CreateTime`, `PrizeItemId`, `PrizeName`, `UpdateTime`, `UserId`)
    VALUES (@p0, @p1, @p2, @p3, @p4); SELECT `Id`, `IsDeleted` FROM `WinningRecord` WHERE ROW_COUNT() = 1 AND `Id` = LAST_INSERT_ID();
    [17:09:06 ERR] An exception occurred in the database while saving changes for context type 'LuckyDraw.Infrastructure.Data.AppDbContext'.
    Microsoft.EntityFrameworkCore.DbUpdateException: An error occurred while saving the entity changes. See the inner exception for details.
     ---> MySqlConnector.MySqlException (0x80004005): The Command Timeout expired before the operation completed.
     ---> MySqlConnector.MySqlException (0x80004005): Query execution was interrupted
       at LuckyDraw.Infrastructure.Repositories.WinningRecordRepository.AddAsync(…) line 25
       at LuckyDraw.Application.Services.DrawService.ExecuteDrawAsync(…) line 185
       at LuckyDraw.Application.Services.DrawService.ExecuteDrawAsync(…) line 237
    [17:09:06 ERR] HTTP POST /api/v1/draw responded 500 in 30039.9728 ms
    ```
  - **无重试发生**（本角色独立复核：该窗口内 `重试` / `瞬时错误` / `retry` **命中 0 行**；4 条 ERR **时间戳全为 `17:09:06`**，系同一事件在 MySqlConnector / EF Core / 全局 ExceptionFilter 三层的重复记录，而非三次尝试）——即 `DrawService.DrawAsync` 的「最多重试 3 次整个事务」**一次都没有触发**。
- **期望结果**（依据）：
  - `docs/30-architecture.md:544`（D-08「异常与重试」）：「死锁 1213 → **整个事务重试 1 次**，仍失败 → 500 + CRITICAL 告警」；`TransientDataException.cs:4` 与 `DrawService.cs:109` 亦明文点名处理「死锁 1213 / **锁等待超时 1205**」。
  - `src/backend/src/LuckyDraw.Application/Common/ErrorCodes.cs:10` 与 `docs/error-codes.md`：**`1001`「系统繁忙，请稍后重试」**即「限流 / 依赖不可用的降级提示」，且 `error-codes.md` 明示 **500 为全局 ExceptionFilter 兜底、业务代码禁止手动使用**。
  - → 锁竞争类瞬时失败应走 `TransientDataException` 重试链，耗尽后返回 **`1001`**（HTTP 200），而非未分类的 **`500`**。
- **影响范围**：
  - **错误码分层被绕过**：客户端拿到的是「全局兜底」的 `500`，而非设计好的 `1001`；且**白等 30 秒**（`CommandTimeout=30`）才得到失败。
  - **重试机制名存实亡（结构性）**：`MySqlErrors.IsTransient`（`src/backend/src/LuckyDraw.Infrastructure/Data/MySqlErrors.cs:33`）只认 **1205 / 1213**，而抽奖事务六个写入点中**后三处完全没有分类**（实测 `catch` 计数：① `DrawRequestRepository` = 3、② `UserDrawQuotaRepository` = 2、③ `PrizeRepository` = 1、**⑥ `WinningRecordRepository` = 0、⑦ `AuditService` = 0**、⑧ `DrawRequestRepository.SaveResultAsync` 走 `ExecuteUpdateAsync` 且无 catch）→ 落在 ⑥⑦⑧ 的死锁 / 锁等待**一律直接冒泡成 500**，重试链完全跳过。
  - **「补 catch 也不够」（第二层根因）**：MySqlConnector 默认 `CommandTimeout=30`（日志实测 `CommandTimeout='30'`）**先于** MySQL 的 `innodb_lock_wait_timeout=50`（本机实测终态 50）触发**命令超时**，其 `MySqlException.Number` **不是 1205/1213** → 即便在 ⑥⑦⑧ 补上 `IsTransient` 判定，`IsTransient` 仍返回 false。**30 < 50 ⇒ 「锁等待超时 1205」这条分支在任何超过 30 秒的锁等待上都不可达**，恰好是它被写出来要处理的那个场景；修复须同时调整命令超时或把命令超时纳入瞬时判定。
  - **数据完整性不受影响**：事务仍整体回滚（TC-56b 实测 5/5 一致），不产生部分成功状态；前端对 `500` 与 `1001` 均落 `page-draw--drawfail` 且重试沿用同一幂等键（AC-17），用户可自行恢复。
  - 触发条件为**行锁竞争 / 长事务**（并发同奖品、同用户、外键父行竞争等）——正常低并发下不易触发，故**定级「一般」而非「严重」**：契约偏差明确且机制层面失效，但不损坏数据、用户可见面有限、可自行重试恢复。
- **证据**：
  - 客户端侧：`tests/e2e/logs/tc56b-real-20260917-170833.log`（`抽奖响应 → http=500 code=500 msg=系统内部错误 elapsed=30615ms`；`原始响应体 = {"code":500,"message":"系统内部错误","data":null}`）
  - 服务端侧：`tests/e2e/logs/tc56b-instance-excerpt-20260917-170943.log`（源 `tests/e2e/logs/stats-instance-20260917-164034.log` 行 34618–34725，GBK 解码后含上述三段 ERR 与 `HTTP POST /api/v1/draw responded 500 in 30039.9728 ms`）
  - 在飞期独立旁证（同一次请求）：`sys.innodb_lock_waits` = `43751 | 43749 | luckydraw_test.User | PRIMARY | … | INSERT INTO WinningRecord (…`，等待方 `trx_state=LOCK WAIT`、`trx_rows_modified=4` → 阻塞发生在 **⑥ 中奖记录 INSERT**（② 扣次 / ③ 扣库存已写入之后），非「第一条语句就失败」。
  - 代码侧（只读核对，未改动）：`MySqlErrors.cs:33`、`WinningRecordRepository.cs:25`、`AuditService.cs`、`DrawRequestRepository.SaveResultAsync`、`DrawService.cs:117-130 / 185 / 237`。

- **回归验证（第 3 轮回归 · R7，2026-09-17 18:27–18:30）**：**复跑验证通过 → 状态置 `已闭环`**（CHG-16 + CHG-17 修复后，按 TC-56b 原步骤回写复跑）。
  - 验证方式：`STATS_BASE=http://127.0.0.1:5198/api/v1 node tests/e2e/api/tc56b-tx-rollback.mjs`（同一锁竞争注入法：另一连接对 `User` 行持 X 锁；锁持有时长 `HOLD_SECONDS=300` > 重试预算 ≈100s，确保观察到的终态是「重试耗尽」而非「重试成功」）
  - 关键实测值：终态 **`http=200` / `code=1001` / msg「系统繁忙，请稍后重试」/ elapsed=101037ms**（修复前：`http=500` / `code=500` / 30615ms）；回滚一致性 **5/5**（UsedCount 1→1、`prize-mug` Stock 4→4、WinningRecord 1→1、DrawRequest 1→1、AuditLog(Draw) 1→1）；AUTO_INCREMENT DrawRequest +2 / WinningRecord +2（两次尝试的插入均被回滚的独立佐证）；实例日志：`CommandTimeout='60'`、attempt1 阻塞 50,012ms → `[WRN] 抽奖事务遇数据库瞬时错误，重试整个事务（第 1 次）`、attempt2 阻塞 50,005ms → `[FTL] 抽奖事务重试 2 次后仍遇数据库瞬时错误（死锁 / 锁等待超时），已放弃本次抽奖`、`[WRN] 业务异常：Code=1001 Path=/api/v1/draw` —— 两次尝试合计 ≈100s，与 CHG-17「MaxTransactionAttempts=2」口径吻合（也独立佐证运行的是修复后的二进制）
  - 断言变更说明（只加强、未削弱；改前/改后逐条见 `docs/52-qa-report.md` §1.6.7）：原脚本仅断言「非 200 / 非 0」即算命中缺陷，新脚本增加 `terminalOk`（终态必须恰为 HTTP 200 / code=1001）+ `budgetOk`（耗时 ≥90s，证明重试确实发生过）
  - 证据：`tests/e2e/logs/tc56b-rerun-20260917-182721.log`、`tests/e2e/logs/tc56-rerun-instance-20260917-182525.log`（FTL 行见该日志第 155 行）

---

## 3. 观察项（非缺陷，供主对话参考）

| 编号 | 内容 | 判定依据 | 关联 TC |
| --- | --- | --- | --- |
| OBS-01 | **401 响应体为空**（`Content-Length: 0`，未经 `ApiResult` 包装，ASP.NET Core 默认行为） | 契约未规定 401 的响应体形态 → **契约空白**，按 `QX-E-01` 记入 52 观察项，不立 BUG（主对话已核实） | TC-14、TC-25b、TC-32 |
| OBS-02 | **登出后 access token 在 2h 有效期内仍可访问受保护接口** | `docs/30-architecture.md` API-04 明确为 `[AllowAnonymous]` + 仅按 Cookie 清理 refresh 键的**既定设计**（无 access token 黑名单）；前端在 `finally` 清态跳转，用户可见行为符合 FR-02 | TC-18 |
| OBS-03 | **抽奖同键异体的 409 分支在契约内不可达** | D-03 规定抽奖请求体恒为 `{}`，其规范化哈希基于空对象常量；额外字段不参与哈希 → 「请求体不同」不可构造，服务端按幂等**正常重放**（实测 HTTP 200 / `code=0`，仅扣 1 次、流水行 1 条）。核心不变量（不产生第二次副作用）满足 | TC-53 |
| OBS-04 | **§2.8-4 的一次性闸门为「每页面实例」语义** | 两个独立页面实例各自持内存态，实测各自发起 1 次 refresh（合计 2 次），同实例内为 1 次。跨实例去重不在契约要求内 | TC-22 |
| OBS-05 | **应用日志未落盘为文件** | Serilog 仅 `WriteTo: Console`，无法离线检索日志内容；TC-81 的「日志不含明文密码 / token」改为以 `AuditLog` 表 + `DrawRequest.RequestHash` 为可检索面验证（均无命中），并复以 `User.PasswordHash` 为 BCrypt（TC-82）交叉印证 | TC-81 |
| OBS-06 | **服务端未提供时钟注入通道** | 跨日重置与时间口径只能以「按 `DrawDate` 自然日分桶」的夹具等价验证（TC-57/58/59），23:59:59 / 00:00:00 的**逐秒边界**无法执行 | TC-57、TC-58、TC-59 |
| OBS-08 | **Playwright 的 `outputDir` 每次运行前会被自动清空**（Playwright 默认行为，非人工删除） | `tests/e2e/.artifacts/` 在每次 `node node_modules/@playwright/test/cli.js test` 启动时被清空，故**上一轮（14:35–14:38）4 个失败截图 / trace 目录已被后续复跑覆盖，磁盘上不再存在**。可持久化的证据改为落在 `outputDir` 之外的 `tests/e2e/logs/*.log`（重定向全量标准输出）。当前可核验证据：`tests/e2e/logs/full-run-20260917-144348.log`（47 passed / 3 failed）+ `tests/e2e/.artifacts/` 下本轮 3 个失败目录 | 全体 E2E |
| OBS-07 | **顺序相关不稳定（用例侧问题，非产品缺陷）** | 全量串行运行时 TC-16 / TC-37 / TC-69 / TC-70 失败（**3 项均为 `Test timeout of 30000ms exceeded`，非断言失败**），按 `--grep` 隔离复跑全部通过（主对话复核：3 passed）。共同签名：三条用例均以 `safeRegister()` 开头，即**先注册新用户**；全量串行时所有用例共用同一出口 IP，注册 / 登录限流窗口（10 次/分钟，`appsettings.json`）被前序用例占满，`safeRegister` 的 429 退避重试（每次约 11–12 s）耗尽 30 s 用例超时 → 超时失败；隔离复跑窗口空闲 → 秒级通过（实测 2.8 s / 0.9 s / 4.3 s）。**根因是用例共享 IP 限流窗口与 MySQL / Redis 状态，属用例隔离缺陷**，非产品缺陷。复跑证据见 `52-qa-report.md` §1.5 / OBS-07 | TC-16、TC-37、TC-69、TC-70 |
| OBS-11 | **空签名密钥的三处口径不一致 → 「应用可启动但令牌类接口全部 500」**（R5 执行侧发现，**属 REV-10 范围**）：同一「签名密钥为空」场景存在三种处理 —— ①校验侧 `src/backend/src/LuckyDraw.Api/Program.cs:76-78` 回退为 32 个 `'0'`；②签发侧 `src/backend/src/LuckyDraw.Infrastructure/Tokens/JwtTokenService.cs:40-42` **无回退**（空串直接进 `SymmetricSecurityKey`）；③`src/backend/src/LuckyDraw.Application/Services/AuthService.cs:283-285` 幂等指纹回退为 32 个 `'0'`（实现注释已自述属 REV-10 范围）。**实测后果**（本轮因错误启动方式真实复现）：应用**照常启动、不 fail-fast**；任何签发令牌的接口（注册 / 登录）返回 `HTTP 500` + 仅「系统内部错误」；且**失败时账号已不可收回** —— 客户端看到 500，但 `User` 行与 `AuditLog` 行已入库、Redis 刷新键已写入（磁盘证据：`luckydraw_test.User` 中 Id=387–390 = `st_fqjabbik` / `probe001` / `probe002` / `probe003`，创建于该故障窗口 08:21–08:25 UTC）。即便签发成功，校验侧（回退值）与签发侧（空串）口径也**永不可能互相匹配**。**处置声明：本条仅为观察记录**（触发条件为本轮启动方式错误、已纠正），**属 REV-10 范围，用户已裁决「不纳入本轮」→ 不构成新的修复要求、不作为阻断项、不登记为 BUG** | —（执行环境发现，非用例发现；根因排查与纠正记录见 `52-qa-report.md` §1.6.5「执行方式说明」） |

---

## 4. 未执行用例及原因

> **2026-09-17 收敛（回归轮，最终态）**：首轮 13 条未执行项**已全部补做完毕，本清单为空**。收敛路径：TC-23 / TC-25 / TC-38b / TC-48c / TC-76 / TC-77 / TC-85（R4，见 `docs/52-qa-report.md` §1.6.3；TC-76 第 4 子项的口径冲突 QX-E-07 经用户裁决后复跑通过，见 §1.6.4）→ TC-78 / TC-79 / TC-79b / TC-84（R5，**4/4 PASS**，见 §1.6.5）→ **TC-56b / TC-56c（R6，破坏性批，2026-09-17 17:08–17:18，2/2 PASS，见 §1.6.6）**。**当前未执行：0 条（98/98 已执行）**。

| TC | 层级 | 状态 |
| --- | --- | --- |
| ~~TC-56b~~ | INT | **已执行（R6，2026-09-17 17:08–17:09，PASS）**：以「另一连接对 `User` 行持 X 锁」注入事务中途失败，失败点经在飞期 `sys.innodb_lock_waits` 证实为第 ⑥ 步 `INSERT INTO WinningRecord`（等待方 `trx_rows_modified=4`，即写入之后才阻塞）；**回滚一致性 5/5 通过**；执行中暴露 **BUG-05**（见 §2）。证据 `tests/e2e/logs/tc56b-real-20260917-170833.log` |
| ~~TC-56c~~ | INT | **已执行（R6，2026-09-17 17:17–17:18，PASS）**：停 `luckydraw-redis` 后重放同一幂等键结果全等、不重复扣次，新键抽奖主链路可用，次数 / 记录查询与库内一致，并取得设计内降级 warning 正向证据；收尾已恢复 Redis 并验证注册 + 抽奖正常。证据 `tests/e2e/logs/tc56c-run2-20260917-171700.log` |

---

## 5. 已执行但结论有偏差的用例（非缺陷）

| TC | 执行结论 | 偏差说明 |
| --- | --- | --- |
| TC-53 | **未达断言口径**（HTTP 200 / `code=0` 重放） | 见 OBS-03：409 分支在 D-03 契约下不可达；核心不变量（仅扣 1 次、无第二次副作用、流水行 1 条）实测满足。**属契约可测性缺口，不计缺陷**，已登记待确认清单 |
| TC-56 | 通过（**缩放执行**） | 契约口径为 50 并发；受每日 3 次 / 用户限制，缩放为「库存 3 / 6 用户并发」。核心不变量（库存 ≥ 0、中奖记录数 = 实际扣减库存数）实测满足 |
| TC-57 / TC-58 / TC-59 | 通过（**夹具等价**） | 见 OBS-06：以「昨日配额行 `UsedCount = 3`」的夹具等价验证跨日重置；逐秒边界未执行 |
| TC-13 | **通过（2026-09-17 复跑已补全）** | 前 5 次 `1102`、第 6 次 `1103`（HTTP 200）、锁定期内正确密码亦被拒 —— 均实测通过。**原被登录限流阻塞的分支已在复跑中验证**：不存在用户名连续错密同样得到 `1102 ×5 → 1103`，即**锁定计数按用户名而非账号存在性**，不构成用户名枚举信道（判据另见 `docs/error-codes.md:35`）。证据：`tests/e2e/logs/api-round2-20260917-145013.log`（第 5 行 `[PASS] TC-13b`） |
| TC-22 | 通过（**口径说明**） | 见 OBS-04：实测跨页面实例合计 2 次 refresh，符合「每实例一次性闸门」的契约语义 |
| TC-81 | 通过（**可检索面受限**） | 见 OBS-05：应用日志未落盘，改以 `AuditLog` / `DrawRequest` / `User.PasswordHash` 交叉验证 |
| TC-60 | **用例缺陷（已修正，复跑通过）** | 定位符作用域错误：`page-records--default` 挂在 `<TableBody>`（仅表体，`WinningRecordsView.vue:199-202`），表头 `<TableHeader>` 是其上方兄弟节点（`:120-127`），原断言取表体文本查列头文案，**永远不可能通过**。已改为断言 `table thead` 含「奖品名称」「中奖时间」（保留 50 的原意）。**修正后复跑：1 passed**，实测表头文本 `奖品名称 \| 中奖时间`、仅本人 2 条、时间倒序（14:41 → 14:40）。产品侧 AC-15 已满足。**非产品缺陷**，不计入 BUG |

---

## 6. 定级异议登记

（无。engineer 若对 BUG-01 / BUG-02 / BUG-03 的定级有异议，可在本表追加「定级异议」并交由主对话裁决。）

---

## 待确认清单（test-executor）

| 编号 | 缺失信息 | 采用的默认假设 | 若假设不成立的影响 |
| --- | --- | --- | --- |
| QX-E-01 | 401 响应体形态（`null` / 空体，未经 `ApiResult` 包装）是否属契约要求 | 判定为**契约空白**，记入 52 观察项，不立 BUG（主对话已核实） | 若产品要求 401 也返回 `ApiResult` 结构，则为一般缺陷，需 engineer 补全局处理 |
| QX-E-02 | 抽奖接口「同键异体」是否应返回 409（`docs/50-testcases.md` TC-53 的期望结果），抑或 D-03「请求体恒为 `{}`」使其不可达 | 按 D-03 判定为**不可达分支**，不计缺陷（OBS-03） | 若产品要求 draw 也校验请求体哈希，则 TC-53 转为缺陷，需 engineer 在 DTO 层保留请求体指纹 |
| QX-E-03 | 订单 / 战斗类…（无）—— 本行留空占位，不涉及 | — | — |
| QX-E-04 | TC-76 / TC-77 的**部署类验收**是否授权 test-executor 变更运行配置（`dotnet user-secrets`）并重启服务 | **已按不触碰既有环境的方式执行完毕（2026-09-17）**：另起实例 :5191 / :5192，配置**仅经环境变量注入**，未改 `src/` 与任何 `appsettings*.json` / `launchSettings.json`，未触碰主对话的 :5180 / :5173 与开发库 → **无需再授权** | 仅当要求「以 `dotnet user-secrets` 方式重做」时需另行授权 |
| QX-E-05 | TC-78 / TC-79 统计抽样的**执行方式**（是否授权临时提高 `Draw:DailyLimit` 以取得 1 万 / 1 千次样本） | **已执行（2026-09-17 16:42–16:48，R5）→ 无需再授权**：另起 Testing 实例 :5199，`Draw__DailyLimit=30000` / `RateLimit__DrawPerMinute=100000` **仅经环境变量注入**，未改 `src/`、未改任何 `appsettings*.json` / `launchSettings.json`，未触碰主对话实例（:5180 / :5198）与开发库 `luckydraw_dev`；库存以数据夹具钉高并在运行后复位 FR-03 默认值 | 无（已执行完毕）；结果见 `docs/52-qa-report.md` §1.6.5 |
| QX-E-07（**已裁决并闭环**） | 生产构建产物是否允许存在「空的确定性配置节」：D-06 散文（`docs/30-architecture.md:502`「只含 `Enabled=false`」）与 TC-76 原文（`docs/50-testcases.md` **v2** `:264`「不含该节」；**v3** 该行移至 `:268`）/ RSK-04（`30-architecture.md:966`「不含该节」）口径互斥；实测该节存在（`Enabled:false` + `ForcedResults:[]`） | **已裁决（用户决断，2026-09-17）：采用解释 1 —— 判 TC-76 原用词过严，改用例**：test-designer 出 50 v3（第三条款改为断言取值，齐 D-06 配置键表 `:495-500`）→ 用例断言同步修订并复跑 **3 passed（TC-76 4/4 全绿）**；**未立 BUG**、无工程侧改动（功能面：非空覆盖项在生产一律 fail-fast，无泄漏路径） | 无（已闭环）；`30-architecture.md` 散文 `:502` / RSK-04 `:966` 的宽泛措辞是否修订属架构文档自身处置范围，不阻塞本台账结论 |
