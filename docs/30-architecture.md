# 技术方案：转盘抽奖（前后端分离）

| 项 | 内容 |
| --- | --- |
| 产物 | 技术方案（architecture） |
| 版本 | **v4**（v1 → v2：A1 裁决落地、A2–A6 确认、错误码登记同步；v2 → v3：Q1 存储口径对齐、REV-02 会话恢复契约、Q2 认证幂等补齐（D-15）、REV-01 判定登记；**v3 → v4：D-08 重试耗尽终态 `500` → `1001` 改判（用户裁决 CHG-16 契约冲突）**） |
| 冻结时间 | 2026-09-17T00:00:00+08:00（v1 首冻）/ 2026-09-17T12:00:00+08:00（v2 修订）/ 2026-09-17T20:00:00+08:00（v3 修订）/ **2026-09-17T22:00:00+08:00（v4 修订）** |
| 上游依赖 | `docs/00-brief.md` **v2**（含 CR-01 密码强度收紧裁决）；`docs/10-prd.md` **v2**；`docs/20-prototype.html` **v3**（锚点集合与触发描述未变）；`docs/development-spec.md`（**§3.3 会话口径，2026-09-17 修订**，与本文 §2.8 一致）；`docs/60-review.md` **v1**（§8 Q1–Q3 裁决与 REV-01 / REV-02 / REV-06 来源） |
| 写入者 | software-architect |
| 状态 | **v4 定稿**（v3 → v4：仅 D-08 重试耗尽终态 `500` → `1001` 定点改判）；A1–A7 全部闭环（A7 = 2026-09-17 用户 Q1 裁决，见文末「假设与裁决清单」）；REV-01「可在 20 锚点内完成」的判定与升级条件见 §2.6 补充约定 |

**v4 变更说明（2026-09-17）** —— 本次为**定点修订**（v3 → v4），**只由用户对 CHG-16 契约冲突（抽奖事务重试耗尽的终态错误码）的裁决驱动**，不重开方案论证：

| # | 修订点（驱动） | 落点 |
| --- | --- | --- |
| 1 | **D-08 重试耗尽终态改判为 `1001`（用户裁决：保持代码现状 `1001`，修订架构）**：`:544` 原写「死锁 1213 → 整个事务重试 1 次，仍失败 → **500** + CRITICAL 告警」，与 ① `docs/error-codes.md`「使用边界」（`1001` = 业务层主动降级提示；`500` = 全局 ExceptionFilter 兜底、**业务代码禁止手动使用**）② `:910` 对 `1001` 的指派（旧文未覆盖该场景，见本表 #2）③ 实现 `DrawService` 返回 `1001` 冲突（CHG-16 记录的四方冲突）。**改判理由**：原 `500` 描述的是**缺陷状态本身**——修复前瞬时错误未做分类、直接冒泡到全局 ExceptionFilter 兜底，用户实测看到的正是 500（`docs/52-qa-report.md` §1.6.6、`tests/e2e/logs/tc56b-*`）；修复后按设计落 `1001`「系统繁忙，请稍后重试」（HTTP 200）。**「整个事务重试 1 次」与「CRITICAL 告警」维持原文不变**（重试次数不在本次改判范围；代码侧对齐为 2 次尝试由 engineer 按本行落地） | D-08「异常与重试」正文（`:544`）及紧随的改判说明 |
| 2 | **API-07 错误语义同步显式化（`:910`）**：`1001` 的语义范围由「限流/依赖不可用」显式扩为**含 D-08 瞬时错误整事务重试耗尽的终态**（仍 `1001`、仍 HTTP 200，属归属显式化、非新决策；判据：`:910` 是 API-07 错误语义的汇总行，engineer / test-designer 直接据其实现与断言，旧文被 CHG-16 认定漏列「瞬时错误重试耗尽」） | API-07 错误语义（§5.2） |

**未变更声明（v3 → v4）**：除上述两处外，本文其余内容（§1–§8、附录 A/B、假设与裁决清单 A1–A7）均未变更：模块（MOD-01…MOD-11）、接口清单（API-01…API-08 仍为 **v1**）、数据设计（六表 + Redis 键）、其余决策（D-01…D-15，除 D-08 上述措辞外）、性能与成本预算（§8）不变。**无破坏性接口变更**：本次只细化「重试耗尽」终态的错误码归属（`1001` 与 `500` 原本均在 API-07 错误语义集合内），不改接口结构、字段、版本，不触发 API 版本升级。

**失效传播（`docs/artifacts.md` §5：本次 30（v3 → v4）变更 → 下游）**：

| 下游 | 本次需复核 / 重跑的内容 |
| --- | --- |
| `docs/40-changelog.md` | engineer：CHG-16「未裁决项 (1)」按本裁决闭环（终态 = `1001`）；正文对 `:544` 的逐字引用（「仍失败 → 500 + CRITICAL 告警」）同步为 `1001`；重试次数（未裁决项 (2)）按「架构侧不变、代码侧 2 次尝试对齐」落地并回写 |
| `docs/50-testcases.md` | test-designer：错误语义索引（「500 TC-45、TC-69」）复核——瞬时错误重试耗尽的终态按 `1001`（HTTP 200）断言；TC-45（系统异常）不涉重试耗尽、语义不变 |
| `docs/51-defects.md` | test-executor：BUG-05 属修复前缺陷记录（实测 500 与「设计为 `1001`」表述与本次裁决一致，无需改判）；其正文对 `:544` 的逐字引用（含 500）随上游 v4 复核；随后续修复复验推进闭环 |
| `docs/52-qa-report.md` | test-executor：按 §5 加失效标记（上游 30 升 v4）；BUG-05 复验（重试耗尽 → `1001` + CRITICAL 告警）纳入回归 |
| `docs/60-review.md` | code-reviewer：复核 CHG-16 实现与本行三要素一致（终态 `1001` / 重试 1 次 / CRITICAL 告警） |
| `tests/unit/**` + `tests/integration/**` | engineer：重试耗尽用例期望（终态 `1001`、整事务尝试次数与 `:544`「重试 1 次」= 2 次尝试对齐）与「高并发不出现 500」断言随实现更新 |
| `tests/e2e/**` | test-executor：TC-56b 复验——锁竞争（重试耗尽场景）下终态为 `1001`「系统繁忙，请稍后重试」（HTTP 200），而非 500 |

---

**v3 变更说明（2026-09-17）** —— 本次为**定点修订**（v2 → v3），**只由第 5 步代码审查 `docs/60-review.md` v1 §8 三项用户裁决驱动**，不重开方案论证：

| # | 修订点（驱动） | 落点 |
| --- | --- | --- |
| 1 | **Q1（用户裁决：保留内存方案，同步修订规范 / 架构口径）**：v2 的 D-11 / 附录 A 第 1 条仍写 localStorage 存 access token，与 `docs/development-spec.md` §3.3 新口径冲突 | D-11 正文与备选、附录 A 第 1 条、§2.5 骨架注释、MOD-08 职责；新增 §2.8 |
| 2 | **REV-02（严重）会话恢复触发契约**：只在受保护路由允许一次静默恢复、公开页不触发；闸门失败后允许再试；`/auth/refresh` 新增 `user` 登记为**兼容性新增** | 新增 §2.8；API-03 响应数据 / 版本与兼容 / 前端契约；MOD-08 |
| 3 | **Q2（用户裁决：按契约补齐）注册 / 登录 `Idempotency-Key`**：§5.4 原条款不足以直接实现（服务端实现未定义），本次补成可实现契约（不新增表） | 新增 **D-15**；§5.4 扩展；API-01 / API-02；§7 第 15 行 |
| 4 | **REV-01 判定（本次新增结论）**：修复**可在原型已冻结的 20 个状态锚点内完成**——次数未知不得当作 0；无需新增锚点、不改变任何锚点的触发语义（锚点 12 触发收紧为其原型本义） | §2.6 锚点 12 触发条件 + 补充约定；API-06 / API-07；附录 A 第 7 条 |
| 5 | **Q3（用户裁决：三项全纳入本期）登记**：经核对 S2（`resetAt` 引导）/ D-14（2400ms + `transitionend`）/ 展示格式（`yyyy-MM-dd HH:mm`）**均已是本文件既有契约**（§1.1、D-14、API-08），**不产生契约变更**；engineer 按 60-review 修复建议对齐实现即可 | 仅本表登记（无正文落点） |

**未变更声明（v2 → v3）**：§2.1–§2.5 分层与模块划分（MOD-01…MOD-11）、§3 其余决策（D-01…D-14 除 D-11 正文措辞外内容不变）、§4 数据设计（**六表结构与 Redis 键设计不变，无迁移**）、§5.1 通用约定、§6 其余 RSK、§7 除新增第 15 行外、§8 性能与成本预算均未变更。**无破坏性接口变更**：API-01…API-08 仍为 **v1**（API-03 新增 `user` 属 §5.1 第 2 条「新增可选响应字段」，向后兼容、不升版）。

**失效传播（`docs/artifacts.md` §5：30 变更 → 下游）**：

| 下游 | 本次需复核 / 重跑的内容 |
| --- | --- |
| `docs/40-changelog.md` | engineer 记录 Q1 / Q2 / Q3 与 REV-01 / REV-02 / REV-06 的修复项及测试 |
| `docs/50-testcases.md` / `docs/51-defects.md` / `docs/52-qa-report.md` | test-designer / test-executor 按 §2.6 补充约定（次数未知态）与 §2.8（新标签页恢复、公开页不触发）调整用例；**原型锚点集合不变（仍 20 个）** |
| `docs/60-review.md` | code-reviewer 复核 REV-01 / REV-02 / REV-06 闭环，并复核「REV-01 修复无新增锚点」的判定 |
| `tests/unit/**` + `tests/integration/**` | engineer：`stores/auth.spec.ts` 保留公开页断言并新增受保护路由恢复用例（REV-02 / REV-18）；`useDrawFlow.spec.ts` 新增「首载即失败」用例（REV-17）；后端补认证幂等（D-15）与 CHG-11 重试路径（REV-19）用例 |
| `tests/e2e/**` | test-executor：新增「新标签页直开受保护路由 → 静默恢复后放行」与「首载次数查询失败 → 抽奖入口可用、无 `page-draw--noquota`」两个场景 |

---

**v2 变更说明（2026-09-17，历史）** —— 本次为**定点修订**，逐项如下：

| # | 修订点 | 落点 |
| --- | --- | --- |
| 1 | **A1 裁决落地（CR-01）**：密码字符类别由「至少同时含字母与数字」收紧为「须**同时包含大写字母、小写字母与数字**」；长度 **8–20 位不变**；不引入「首次登录强制改密」 | 头部元信息、API-01 请求体校验（§5.2）、RSK-12（§6）、A1 行（文末）、附录 A 第 10 条 |
| 2 | **A2–A6 确认**：用户采用架构师默认假设（未改动这 5 项的任何技术内容，仅补状态） | 文末假设与裁决清单；连带同步 RSK-11 / RSK-13 与 §3 的相关表述 |
| 3 | **错误码登记状态同步**：§5.3 由「登记就绪表」改为「已登记」（7 个码于 2026-09-17 写入 `docs/error-codes.md`，`docs/error-codes.md` 为权威来源） | §5.3 标题与说明、阅读指引第 5 条 |

**未变更声明**：架构主体（§2.1–§2.4 分层与模块划分 **MOD-01…MOD-11**）、接口清单与各接口契约结构（**API-01…API-08**）、数据设计（§4 六表 + Redis 键设计）、并发与幂等决策（**D-01…D-14**）、性能与成本预算（§8）**均未变更**。本次不含任何破坏性接口变更：API-01 仍为 **v1**，密码规则收紧属服务端校验实现与前端 Schema 的同步，不改变接口结构、字段或错误码（校验失败仍走 `1002`，见 §5.1 版本与兼容策略）。

**失效标记解除说明**：上游三者均已升版（`00-brief` v1→v2、`10-prd` v1→v2、`20-prototype` v2→v3），按 `docs/artifacts.md` §5 失效传播矩阵，本产物原处于「需复核」状态；**v2 即为复核后的产物，该状态随之解除**，无遗留失效标记。**§2.6 定位契约（20 个原型锚点 → 路由 / 组件 / `data-testid` 映射）未受影响**：原型 v3 仅同步 5 处密码文案（索引表 invalid 行、组件映射表规则描述、`reg-password-hint`、错误消息、文案清单），锚点 ID、`?page=` / `?state=` 状态机制与交互边界均未变，§2.6 映射表逐行仍成立。

> 阅读指引（下游 engineer / code-reviewer / test-designer / test-executor）：
>
> 1. 本方案是本仓库**首次落地应用代码**的技术基准（PRD 风险 R6：当前无 `src/`）。第 2.5 节给出从零搭建的目录骨架。
> 2. 技术栈已由 `CLAUDE.md` 与 `docs/development-spec.md` 钉死，本文**不重新论证框架选型**；第 7 章的对比表只覆盖**方案层取舍**。
> 3. 上游契约（00 / 10 / 20）只读，本产物未修改任何上游文件；涉及「怎么写」的冲突以本文为准（判定基准：业务规则 → 10，交互细节 → 20，实现方式 → 30）。**唯一例外**：密码强度口径原属「业务规则」维度，但已由用户在评审门改判为**规范 8.1 口径**并以 `docs/00-brief.md` CR-01 固定（见文末 A1），下游勿再按旧 PRD 口径实现或上报冲突。
> 4. 第 2.6 节是原型 20 锚点 → 真实路由 / 组件 / `data-testid` 的**测试定位契约**，test-designer / test-executor 必读。
> 5. 第 5.3 节的 7 个错误码**已于 2026-09-17 登记进 `docs/error-codes.md`**（含 `15xx` 抽奖模块码段申请与 `1203`/`1204` 历史欠账补录）；**`docs/error-codes.md` 为权威来源**，本表与其不一致时以该文件为准，engineer 取号前先查阅它。
> 6. **§2.8 会话与恢复契约**（v3 新增）是「access token 仅存内存 + 受保护路由静默恢复」的唯一契约，前端 engineer / code-reviewer / test 角色必读；**§5.4 与 D-15** 是注册 / 登录幂等的服务端契约（v3 补齐）。

---

## 1. 需求摘要

### 1.1 功能范围（引用 FR 编号）

| 范围 | FR | 架构落点（模块） |
| --- | --- | --- |
| 注册 / 登录 / 登出 / 无感刷新 | FR-01、FR-02 | MOD-01、MOD-09 |
| 奖池与转盘展示（权重、库存不下发） | FR-03、FR-11 | MOD-02、MOD-10 |
| 剩余次数展示与每日重置 | FR-04、FR-07 | MOD-03、MOD-10 |
| 执行抽奖（后端判定、加权随机、事务扣减、幂等、防超发） | FR-05、FR-08 | MOD-03、MOD-05、MOD-06 |
| 动画与结果反馈（落点以后端为准） | FR-06 | MOD-10 |
| 我的中奖记录（分页、本人隔离） | FR-09 | MOD-04、MOD-11 |
| 异常与错误提示（401 / 业务拒绝 / 系统异常三类） | FR-10 | MOD-08 |
| 可测试性（可注入时钟 / 随机源 / 确定性配置） | FR-05-R9、FR-11-2、PRD 4.6 | MOD-07 |

**Should 三项**（默认纳入本期，由主对话可裁剪，不影响 AC 基线）：S1 记录页空/载/错三态（并入 MOD-11）；S2 次数用尽文案含重置时间引导（1501 文案 + `resetAt` 字段，并入 API-06）；S3 抽奖接口按用户限流 ≤ 10 次/分钟（并入 MOD-03 的 API-07）。

### 1.2 关键非功能需求（可验收口径）

| 维度 | 要求 | 来源 |
| --- | --- | --- |
| 一致性 | 并发下次数 0 超扣、库存 0 超发；同一 `Idempotency-Key`（含并发）只生效一次并重放首次结果；三方（次数/库存/记录）同事务回滚 | FR-08、AC-12、AC-13 |
| 性能 | 抽奖接口 P95 ≤ 500ms；记录列表 ≤ 300ms（单机测试基准）；路由懒加载、列表强制分页 | PRD G4、4.1 |
| 安全 | 抽奖 / 记录接口全部鉴权；权重与库存不出接口；登录失败统一文案防枚举；抽奖请求不携带任何影响结果的参数 | FR-03-1、FR-05-R1、4.2 |
| 可测试性 | 时钟可注入（跨日用例）；随机源可注入；确定性配置**仅测试环境可达**（部署验收项） | 4.6、FR-05-R9、PRD R5 |
| 兼容性 | 桌面 Chrome / Edge 最新两版；移动 375–428px 可用 | 4.4、Q12 |
| i18n 预留 | 文案集中管理（`utils/messages.ts`）；时间存 ISO 8601、展示 UTC+8；无货币字段 | 4.5 |
| 规范约束 | `/api/v1/[controller]` 小写、`ApiResult<T>`（code=0 成功）、分页 `pageIndex` 从 1 起、提交类接口带 `Idempotency-Key`、新错误码先登记 | 4.7 |

### 1.3 明确不做（Won't，不设计进来）

W1 后台奖品管理端与 RBAC；W2 真实发放 / 核销 / 支付 / 物流；W3 风控反作弊；W4 排行榜 / 运营报表 / 裂变；W5 多语言多地区（仅做文案集中与 ISO 8601 预留）；W6 面向用户的抽奖历史（含未中奖）查询。
说明：MOD-05 的 `DrawRequest` 表是**后端幂等与审计的内部流水**，不提供任何查询接口，不等于 W6。

---

## 2. 架构设计

### 2.1 总体形态与运行时拓扑

前后端分离（PRD 4.7）：

```
浏览器（Vue 3 SPA，路由懒加载）
   │  /api/v1/**（axios 封装实例，access token 走 Authorization 头，refresh 走 httpOnly Cookie）
   ▼
Nginx（静态资源 + /api 反向代理；UseForwardedHeaders 前置，见 §8 与附录 B）
   ▼
ASP.NET Core API（.NET 10，四层：Api → Application → Infrastructure → Domain）
   ├── MySQL 8.4（业务数据；重放/超发/超扣的唯一底线）
   └── Redis（幂等结果缓存、refresh token 会话、登录失败计数）
```

- 结果判定 100% 在后端：请求体不携带任何业务参数（FR-05-R1），HTTP 边界上不存在「前端指定结果」的入口。
- SPA 只负责：拉奖池 → 提交抽奖 → 按返回条目标识定位扇区播动画 → 展示 Dialog / 三态 Alert。

### 2.2 后端模块划分（MOD-01…MOD-07）

| 模块 | 名称 | 职责 | 关联 FR | 主要实现位置 |
| --- | --- | --- | --- | --- |
| MOD-01 | 认证与会话 | 注册（用户名唯一、BCrypt）、登录（统一失败文案、失败锁定）、登出、refresh 轮换与复用检测、JWT 签发 | FR-01、FR-02、FR-10-1 | Application `AuthService` / Api `AuthController` / Infrastructure `RedisTokenStore` |
| MOD-02 | 奖池查询 | 启用条目按 `DisplayOrder` 输出；**白名单投影 DTO**（不含权重 / 库存）；奖池不存在删除入口 | FR-03、FR-11 | Application `PrizePoolService` / Api `PrizesController` |
| MOD-03 | 抽奖执行 | 次数校验与扣减、候选集构造、加权随机判定、库存条件扣减、中奖记录写入、事务与锁序、限流 | FR-04、FR-05、FR-07、FR-08 | Application `DrawService`（事务宿主）/ Api `DrawController` |
| MOD-04 | 中奖记录 | 本人记录倒序分页（恒定当前用户，不接受 userId 参数） | FR-09 | Application `WinningRecordService` / Api `RecordsController` |
| MOD-05 | 幂等与去重（横切） | `Idempotency-Key` 语义：DB 唯一索引为底线 + Redis 结果重放缓存；请求哈希不一致 → 409 | FR-05-R2、FR-08-4 | Application `IIdempotencyStore`（抽象）/ Infrastructure `RedisIdempotencyStore` |
| MOD-06 | 审计日志（横切） | EF `SaveChangesInterceptor` 自动审计实体变更 + `IAuditService` 显式记录业务动作（抽奖、登录/登出/注册） | FR-05-R8、规范第十章 | Infrastructure `AuditInterceptor` / `AuditService` |
| MOD-07 | 可测试性支撑（横切） | 可注入时钟（`TimeProvider`）、可注入随机源（`IRandomSource`）、确定性结果配置（仅测试环境，启动强校验） | FR-05-R9、FR-11-2、4.6 | Application `Options` + Infrastructure `SystemRandomSource` |

### 2.3 前端模块划分（MOD-08…MOD-11）

| 模块 | 名称 | 职责 | 关联 FR | 主要实现位置 |
| --- | --- | --- | --- | --- |
| MOD-08 | 前端基础设施 | 路由与守卫（`public: true` / 默认需登录、回跳 `redirect`、**受保护路由的会话静默恢复**）、`utils/request`（401 无感刷新去重）、会话态**仅存内存**（§2.8）、错误边界（`app.config.errorHandler` + `onErrorCaptured`）、主题令牌（`@theme inline`）、文案集中（`utils/messages.ts`）、时间格式化（`Intl`，Asia/Shanghai） | FR-10、FR-02（含 FR-02-1 会话恢复）、4.5、规范 3.3–3.7 | `router/`、`utils/`、`stores/auth.ts`、`assets/main.css` |
| MOD-09 | 认证页面 | 注册 / 登录表单（Zod + vee-validate）、submit / invalid / loginfail / guard 四态 | FR-01、FR-02 | `views/auth/RegisterView.vue`、`views/auth/LoginView.vue` |
| MOD-10 | 抽奖页与转盘交互 | 奖池拉取三态（loading / error / empty）、转盘扇区渲染、quota 同步、drawing / noquota / drawfail 态、按后端条目标识计算落点并播动画、结果 Dialog | FR-03、FR-04、FR-05、FR-06、FR-07 | `views/draw/DrawView.vue`、`components/business/{DrawWheel,DrawResultDialog,PrizeLegend,QuotaBadge}.vue`、`composables/useDrawFlow.ts` |
| MOD-11 | 中奖记录页 | TanStack Table（`ColumnDef<WinningRecord>`）+ 服务端分页（页码 ±1 收敛在一处）、四态 | FR-09 | `views/records/WinningRecordsView.vue`、`composables/usePagination.ts` |

### 2.4 模块依赖关系

```
MOD-09 ──依赖──▶ MOD-01（API-01/02/03/04）──▶ MOD-06 审计
MOD-10 ──依赖──▶ MOD-02（API-05）、MOD-03（API-06/07）
MOD-11 ──依赖──▶ MOD-04（API-08）
MOD-03 ──依赖──▶ MOD-05 幂等、MOD-06 审计、MOD-07 时钟/随机源
MOD-01 ──依赖──▶ MOD-06 审计、MOD-07 时钟（签发时间）
MOD-02 / MOD-04 ──依赖──▶ （仅基础设施：DbContext、缓存）
MOD-08 ──会话恢复──▶ MOD-01（API-03；仅受保护路由触发，与 401 刷新共用同一在途 Promise，§2.8）
MOD-08 ──被所有前端模块依赖（路由 / 请求 / 错误边界 / 主题 / 文案 / 会话恢复）
```

约束：前端模块不互相依赖业务内部状态（奖池、记录均为页面内 `ref`，不进 Pinia——规范 3.3 判定标准）；后端禁止跨层调用（Api 不触 DbContext、Infrastructure 只实现 Application 定义的抽象）。

### 2.5 目录骨架（从零搭建，只定义不创建）

根目录：`src/backend`（后端解决方案）+ `src/frontend`（前端应用）+ `tests/{unit,integration,e2e}`（测试分区，归属见 `docs/artifacts.md` 第 2 节）。选择理由见 D-13。

```
src/backend/                                   # .NET 10 四层 + 解决方案 LuckyDraw
├── LuckyDraw.slnx
├── src/
│   ├── LuckyDraw.Api/                         # 接口层
│   │   ├── Controllers/{AuthController,PrizesController,DrawController,RecordsController}.cs
│   │   ├── Filters/{ResultFilter,ExceptionFilter}.cs
│   │   ├── Program.cs                         # DI 装配 / 中间件顺序 / 启动校验
│   │   ├── appsettings.json                   # 生产基线：不含任何测试开关
│   │   ├── appsettings.Development.json       # 开发/测试确定性配置（仅此文件出现）
│   │   └── public partial class Program { }   # 集成测试 WebApplicationFactory 入口
│   ├── LuckyDraw.Application/
│   │   ├── Services/{AuthService,PrizePoolService,DrawService,WinningRecordService}.cs
│   │   ├── Dtos/                              # 与前端 types/ 一一对应
│   │   ├── Interfaces/                        # IIdempotencyStore / IAuditService / IRandomSource / IPrizeRepository…
│   │   ├── Options/{DrawOptions,JwtOptions,PrizeOptions}.cs   # 含 IValidateOptions 启动校验
│   │   └── Common/{PageQuery,PageResult,ClockExtensions}.cs
│   ├── LuckyDraw.Infrastructure/
│   │   ├── Data/AppDbContext.cs               # OnModelCreating：索引 / HasMaxLength / 种子
│   │   ├── Repositories/                      # 仓储实现
│   │   ├── Cache/RedisIdempotencyStore.cs     # MOD-05
│   │   ├── Audit/{AuditInterceptor,AuditService}.cs           # MOD-06
│   │   ├── Random/SystemRandomSource.cs       # MOD-07（RandomNumberGenerator）
│   │   └── Migrations/                        # 迁移 + HasData 种子（FR-11）
│   └── LuckyDraw.Domain/
│       ├── Entities/{User,PrizeItem,WinningRecord,UserDrawQuota,DrawRequest,AuditLog}.cs
│       ├── Enums/{PrizeItemType}.cs
│       └── Exceptions/BusinessException.cs
└── tests/                                     # 见下方 tests/ 统一分区（或作为解决方案内测试项目）
src/frontend/                                  # Vue 3 + Vite（规范 3.1 标准目录）
├── index.html
├── package.json / vite.config.ts / tsconfig.json / eslint.config.js
└── src/
    ├── api/{auth,prize,draw,record}.ts        # 接口函数（路径不含 /api 前缀）
    ├── assets/main.css                        # Tailwind 4 入口 + @theme inline（§2.7）
    ├── components/
    │   ├── ui/                                # shadcn-vue 生成，只读
    │   └── business/{DrawWheel,DrawResultDialog,PrizeLegend,QuotaBadge,EmptyState,AppBar}.vue
    ├── composables/{useDrawFlow,usePagination}.ts
    ├── layouts/{AuthLayout,AppLayout}.vue
    ├── lib/utils.ts
    ├── router/index.ts                        # 路由 + 全局守卫
    ├── stores/auth.ts                         # 仅认证态（access token + 用户信息，**仅内存**；§2.8）
    ├── types/{api,auth,prize,draw,record}.ts  # 与后端 DTO 字段一致
    ├── utils/{request,idempotency,datetime,messages,error}.ts
    └── views/
        ├── auth/{RegisterView,LoginView}.vue
        ├── draw/DrawView.vue
        └── records/WinningRecordsView.vue
tests/
├── unit/        # engineer：后端 xUnit 项目 + 前端 Vitest（就近 `*.spec.ts` 亦可，二者均属 engineer 维护）
├── integration/ # engineer：Testcontainers(MySQL) + WebApplicationFactory 关键 API 链路
└── e2e/         # test-executor：Playwright（前后端同时可用）
```

- 版本约束沿用规范 2.1 / 2.2（Vue 3.5+、TS 5+ strict、Tailwind 4、TanStack 8.x、Zod **3.25.x 锁 3.x**、vee-validate 4.15.x、Pinia 4.x、Vue Router 4.x、Vite 8.x、Node 24 LTS；.NET 10、EF Core 10、Microting EF Core MySql 10.0.11、FluentValidation、Serilog、StackExchange.Redis、Asp.Versioning）。
- shadcn-vue 组件按需添加：`npx shadcn-vue@2.x add button input label card form alert dialog badge skeleton table pagination`。
- 前端测试目录说明：Vitest 用例就近放 `src/frontend/src/**/__tests__/` 或 `*.spec.ts`（属 `src/`，仍由 engineer 维护）；`tests/e2e/**` 归 test-executor。二者分区不重叠。

### 2.6 路由、原型锚点与测试定位契约（20 → 30 契约落地）

**P2 决断（正式确认）**：路由沿用原型的建议值，**不做改名**，因此不存在「与 20-prototype.html 标注不一致」的映射差异项；后续如改名，必须在本节追加映射表并标注差异。

| 路由 | 组件 | 守卫 | 说明 |
| --- | --- | --- | --- |
| `/` | — | — | `redirect: '/draw'`（未登录再由守卫带 `redirect` 跳登录） |
| `/register` | `views/auth/RegisterView.vue` | `meta.public = true` | 注册成功即登录 → `/draw` |
| `/login` | `views/auth/LoginView.vue` | `meta.public = true` | 有 `?redirect=` 则登录后回跳；无则 `/draw` |
| `/draw` | `views/draw/DrawView.vue` | 默认需登录 | 入口页 |
| `/records` | `views/records/WinningRecordsView.vue` | 默认需登录 | 回跳目标同理 |
| `/:pathMatch(.*)*` | — | — | 重定向 `/` |

守卫行为：未登录访问受保护页 → `next('/login?redirect=<目标路径>')`；`redirect` 参数同时是 `page-login--guard` 态的**唯一触发条件**（登录页存在 `redirect` 时展示「请先登录」Alert）。

**测试定位契约（test-designer / test-executor 按此写选择器；engineer 按此落 `data-testid`）**：`data-testid` 与原型锚点 ID **逐字一致**（原型 `id="page-draw--win"` → 实现 `data-testid="page-draw--win"`）。同一锚点在不同状态共用一个 testid 时，以「该状态下可见的区块」为准。

| # | 原型锚点（data-testid） | 路由 | 组件 | 触发条件（真实实现） | 状态归属 |
| --- | --- | --- | --- | --- | --- |
| 1 | `page-register--default` | `/register` | RegisterView（Card 表单常驻） | 进入页面 | 常驻 |
| 2 | `page-register--submit` | `/register` | 同上（提交按钮切 disabled + Loader2） | 提交中 `isSubmitting` | 条件渲染 |
| 3 | `page-register--invalid` | `/register` | 同上（FormMessage 三段） | Zod 本地校验失败 或 后端 `1101` / `1002` | 条件渲染 |
| 4 | `page-login--default` | `/login` | LoginView（Card 表单常驻） | 进入页面（无 `redirect`） | 常驻 |
| 5 | `page-login--submit` | `/login` | 同上 | 提交中 | 条件渲染 |
| 6 | `page-login--loginfail` | `/login` | Alert destructive | 后端 `1102` / `1103` | 条件渲染 |
| 7 | `page-login--guard` | `/login` | Alert default | 存在 `?redirect=` 参数（守卫跳转或凭证失效引导） | 条件渲染 |
| 8 | `page-draw--default` | `/draw` | DrawView（转盘 + 信息区） | 奖池就绪且 `remaining > 0` | 常驻基础层 |
| 9 | `page-draw--loading` | `/draw` | Skeleton（圆形 + 文本行） | 奖池请求进行中（首屏进入 / 重试后） | 整块替换 |
| 10 | `page-draw--error` | `/draw` | Alert destructive + 重试按钮 | 奖池接口失败 / 超时（重试为纯只读，无幂等键） | 整块替换 |
| 11 | `page-draw--empty` | `/draw` | `EmptyState` + 禁用按钮 | 奖池接口返回启用条目为空，**或**抽奖返回 `1502` | 整块替换 / 状态叠加 |
| 12 | `page-draw--noquota` | `/draw` | Alert default（含 destructive Badge） | **后端确认** `remaining === 0`（初始加载或抽奖后刷新；次数未知 / 首载失败**不得**触发，v3 收紧见补充约定） | 状态叠加 |
| 13 | `page-draw--drawing` | `/draw` | 按钮 disabled + 转子动画 + `role="status"` | 点击抽奖后至动画停稳（请求 + 动画期间） | 状态叠加 |
| 14 | `page-draw--win` | `/draw` | `Dialog`（获奖变体） | 抽奖返回 `isWin=true`，动画停稳后打开 | 弹层 |
| 15 | `page-draw--lose` | `/draw` | `Dialog`（未中奖变体） | 抽奖返回 `isWin=false`，动画停稳后打开 | 弹层 |
| 16 | `page-draw--drawfail` | `/draw` | Alert destructive + 重试（沿用同一幂等键） | 抽奖请求失败 / 系统异常 | 状态叠加 |
| 17 | `page-records--default` | `/records` | Table + Pagination | 查询成功且 `totalCount > 0` | 常驻基础层 |
| 18 | `page-records--loading` | `/records` | 表头保留 + 3 行 Skeleton | 列表请求进行中 | 整块替换（表体） |
| 19 | `page-records--error` | `/records` | 表头保留 + Alert destructive + 重试 | 列表请求失败 | 整块替换（表体） |
| 20 | `page-records--empty` | `/records` | 表内占位行 + `EmptyState` | 查询成功且 `totalCount === 0` | 整块替换（表体） |

补充约定（供用例设计直接引用）：
- 原型 `?state=` / `?page=` 是**原型内部的状态直达参数，不进入真实应用**；真实导航由路由与用户操作驱动。E2E 不得依赖 `?state=`。
- 中奖 Dialog 的标题元素另设 `data-testid="draw-win-title"` / `draw-lose-title`（对应原型 `draw-win-title` / `draw-lose-title` 的 `aria-labelledby`），剩余次数徽标 `data-testid="draw-quota"`，记录行容器 `records-row-{id}`。
- 真实 `disabled` 必须由实现提供（原型仅有样式禁用）；E2E 断言按钮 `toBeDisabled()`，不得断言 CSS。
- 详情弹层由 shadcn-vue Dialog（reka-ui）承担焦点陷阱 / Esc 关闭 / 焦点回收；`prefers-reduced-motion` 下降级由 Tailwind `motion-reduce:` 变体承担。
- **次数未知态（REV-01 修复口径，v3 新增）**：`remaining` 未知（首载失败）**不得当作 0** —— 不展示 `page-draw--noquota`、抽奖按钮保持可用（可见 `page-draw--default` 基础层）、`draw-quota` 徽标以占位「—」呈现（**不得渲染数字 0**；`dailyLimit` 未知时上限徽标隐藏）。恢复通道 = ①每次进入页面重拉（FR-07-2）②抽奖成功后**先**以抽奖响应（API-07）的 `remainingAttempts` 回写徽标、**再**按契约刷新 API-06（刷新失败不覆盖已确认值）；收到 `1501` 即权威确认 → 置 `remaining = 0` 并展示锚点 12。该修复**不新增锚点、不改变任何锚点的触发语义**（锚点 12 为按其原型本义「当日已消耗 3 次，尝试第 4 次」收紧触发，原型锚点集合与 `?state=` 机制不动）；若审查方确需「可见的次数查询失败提示 + 重试按钮」，属新增边界状态（触碰 `20-prototype` 冻结面），须由主对话决定是否触发 20 修订，本次不引入。
- 现有测试 `composables/__tests__/useDrawFlow.spec.ts`「次数查询失败不覆盖已有值」的断言语义保留；**新增**「首载即失败」用例（REV-17）：断言抽奖入口可用、不展示 `page-draw--noquota`、徽标为占位。

### 2.7 设计令牌与主题配置规格（Tailwind 4，可直接搬入 `src/frontend/src/assets/main.css`）

口径（用户已确认，原型注释 [2]）：以 shadcn-vue neutral 主题为基线，**仅三处刻意加深**以满足 WCAG——`--muted-foreground` 0.556→0.439、`--destructive` 0.577→0.46、`--ring` 0.708→0.556。取值以原型 `:root` 为唯一来源（同名同值镜像），engineer 直接把下表值搬进配置，**不得引入原型的手写 CSS**。

```css
@import "tailwindcss";
/* shadcn-vue 2.x init 生成的其余导入（tw-animate-css 等）保持 init 结果 */

:root {
  --radius: 0.625rem;
  --background: oklch(0.97 0 0);
  --foreground: oklch(0.145 0 0);
  --card: oklch(1 0 0);
  --card-foreground: oklch(0.145 0 0);
  --popover: oklch(1 0 0);
  --popover-foreground: oklch(0.145 0 0);
  --primary: oklch(0.205 0 0);
  --primary-foreground: oklch(0.985 0 0);
  --secondary: oklch(0.97 0 0);
  --secondary-foreground: oklch(0.205 0 0);
  --muted: oklch(0.97 0 0);
  --muted-foreground: oklch(0.439 0 0);          /* 刻意加深：0.556 -> 0.439 */
  --accent: oklch(0.97 0 0);
  --accent-foreground: oklch(0.205 0 0);
  --destructive: oklch(0.46 0.176 27.5);         /* 刻意加深：0.577 0.245 27.325 -> 0.46 0.176 27.5 */
  --destructive-foreground: oklch(0.985 0 0);
  --border: oklch(0.922 0 0);
  --input: oklch(0.922 0 0);
  --ring: oklch(0.556 0 0);                      /* 刻意加深：0.708 -> 0.556 */
  --chart-1: oklch(0.646 0.222 41.116);
  --chart-2: oklch(0.6 0.118 184.704);
  --chart-3: oklch(0.398 0.07 227.392);
  --chart-4: oklch(0.828 0.189 84.429);
  --chart-5: oklch(0.769 0.188 70.08);
}

@theme inline {
  --color-background: var(--background);
  --color-foreground: var(--foreground);
  --color-card: var(--card);
  --color-card-foreground: var(--card-foreground);
  --color-popover: var(--popover);
  --color-popover-foreground: var(--popover-foreground);
  --color-primary: var(--primary);
  --color-primary-foreground: var(--primary-foreground);
  --color-secondary: var(--secondary);
  --color-secondary-foreground: var(--secondary-foreground);
  --color-muted: var(--muted);
  --color-muted-foreground: var(--muted-foreground);
  --color-accent: var(--accent);
  --color-accent-foreground: var(--accent-foreground);
  --color-destructive: var(--destructive);
  --color-destructive-foreground: var(--destructive-foreground);
  --color-border: var(--border);
  --color-input: var(--input);
  --color-ring: var(--ring);
  --color-chart-1: var(--chart-1);
  --color-chart-2: var(--chart-2);
  --color-chart-3: var(--chart-3);
  --color-chart-4: var(--chart-4);
  --color-chart-5: var(--chart-5);

  /* 转盘扇区专用语义令牌（原型用 color-mix 表达"淡色扇区"；在 @theme 内表达，业务代码只写 fill-wheel-n） */
  --color-wheel-1: color-mix(in oklab, var(--chart-4) 26%, var(--card));
  --color-wheel-2: color-mix(in oklab, var(--chart-2) 24%, var(--card));
  --color-wheel-3: color-mix(in oklab, var(--chart-1) 22%, var(--card));
  --color-wheel-4: color-mix(in oklab, var(--chart-5) 20%, var(--card));
  --color-wheel-5: color-mix(in oklab, var(--muted-foreground) 16%, var(--card));
  --color-wheel-win: color-mix(in oklab, var(--chart-1) 48%, var(--card));      /* 中奖扇区高亮 */
  --color-wheel-lose: color-mix(in oklab, var(--muted-foreground) 32%, var(--card));

  --radius-sm: calc(var(--radius) - 4px);
  --radius-md: calc(var(--radius) - 2px);
  --radius-lg: var(--radius);
  --radius-xl: calc(var(--radius) + 4px);
}

@layer base {
  * { @apply border-border outline-ring/50; }
  body { @apply bg-background text-foreground; }
}
```

硬性约束（规范 3.6 + 原型注释 [2]）：
1. 业务代码**只用 Tailwind 原子类 + shadcn-vue 组件**；禁止搬运原型 `.btn/.input/.card/.badge/.alert/.skeleton/.table/.modal/.wheel-*` 等 class 名，禁止新增独立 CSS 文件、禁止硬编码色值（含 SVG `fill="#xxx"`）。
2. 转盘扇区取色只用 `fill-wheel-1..5` / `fill-wheel-win` / `fill-wheel-lose`（由 `@theme` 提供），中奖扇区高亮用 `stroke-foreground stroke-[3.5]` 等价原子类；指针用 `fill-foreground`。
3. 暗色模式本期不做（原型豁免项）；如后续启用，从 shadcn 主题块引入 `.dark` 并重跑对比度检查。
4. `color-mix()` 依赖 Chrome/Edge ≥ 111、Safari ≥ 16.2（PRD 4.4 目标浏览器均满足）；无需回退分支，但记录为兼容边界。

### 2.8 会话与恢复契约（access token 仅存内存；REV-02 落点，2026-09-17 新增）

> 本节是**前端会话**的唯一契约，与 `docs/development-spec.md` §3.3（2026-09-17 口径）一致；engineer / code-reviewer / test-designer / test-executor 以本节为准。

**1. 存储口径（Q1 裁决落地）**

- 会话态 = access token + 用户信息，**仅存内存**（Pinia `stores/auth.ts` 的 state + `utils/auth-token.ts` 内存单例）；**禁止**写 localStorage / sessionStorage——既不得作为凭证缓存，也**不得作为「是否已登录」的判据**。
- refresh token 走 httpOnly + Secure + SameSite=Strict Cookie（`Path=/api/v1/auth`），前端任何代码不得读取 / 持久化（D-11 不变）。
- 登出（含接口失败路径）同步清空内存态；页面刷新 / 新标签页打开后内存为空是**正常状态**，不等于未登录。

**2. 恢复触发（唯一入口）**

- 触发条件 = 进入**受保护路由**（无 `meta.public`）且内存态无会话 → 发起一次静默恢复；恢复完成（成功或失败判定）前不得放行目标组件（不得先闪登录页再回跳）。
- **公开页（`/login`、`/register`）进入时一律不触发刷新**。现有测试 `src/frontend/src/stores/__tests__/auth.spec.ts`「无缓存用户时不调用刷新接口」所锁定的**公开页语义必须保留**；本次只放开受保护路由这条路径，**不得顺带放开公开页行为**。
- 恢复失败后按既有守卫行为重定向 `next('/login?redirect=<目标路径>')`（`page-login--guard` 态与 `?redirect=` 回跳语义不变）。

**3. 恢复动作与降级**

- 动作 = 调 `POST /api/v1/auth/refresh`（API-03；裸 axios 实例 + `withCredentials`）。**必须与 401 无感刷新共用同一在途刷新 Promise / 同一刷新函数**：refresh 是一次性轮换，两条路径各发一次并发刷新会被服务端复用检测判为 `1203` 并注销该用户全部会话（规范 3.4 硬约束在恢复路径同样适用）。
- 成功 = 写入 `accessToken` / `expiresIn` / `user`（`user` 为 API-03 的**兼容性新增**字段，见 §5.2）→ 放行；用户信息**唯一来源**即该响应（前端不再缓存用户信息）。
- 失败 = `1203` / `1204`（HTTP 200，**必须自判 `response.data.code`**）或网络 / 超时 / `5xx`：不写任何会话态 → 跳登录；不得白屏、不得在该路径内自动重试。`user` 字段缺失（旧后端）按恢复失败处理。

**4. 一次性闸门（`bootstrapped` 三态模型）**

- 闸门状态 `idle | restoring | ready`：`restoring` 期间的并发受保护路由进入**复用同一 Promise**（去重，不并发发两次 refresh）；仅 `ready` 为终态（本次 SPA 生命周期内不再发起恢复）。
- **失败后闸门回到 `idle`**（不是一次性置位终态）：用户后续**再次导航**到受保护路由时允许再试（消除 REV-02 第 2 点「一次失败即整段 SPA 生命周期不再重试」）；**禁止**定时器 / 拦截器自动循环重试（避免失败风暴）。
- 已知边界（接受并登记，见 RSK-15）：网络错误下可能存在「服务端已轮换、响应丢失」，此时下一次携旧 Cookie 的恢复会被 D-11 严格复用检测判为 `1203`（全端登出）——后果与「不重试」等价（都需重新登录），不额外设计宽限窗口。

**5. 与既有链路的边界**

- 恢复成功写出的内存态与「登录成功」写出的内存态**结构完全一致**；后续 401 仍按规范 3.4 走拦截器无感刷新（行为不变）。
- 恢复请求为只读语义，不携带 `Idempotency-Key`。

---

## 3. 关键决策与备选方案

> 每条格式：**决策** → 备选与取舍（为什么选它、放弃了什么）。

### D-01 并发防超发（库存扣减）：条件 UPDATE + 影响行数判断

**决策**：库存扣减统一走单条条件更新，语义为「影响行数 = 1 才算成功」：

```sql
UPDATE PrizeItem SET Stock = Stock - 1, UpdateTime = @now
WHERE Id = @id AND Stock > 0           -- 库存不足时影响 0 行，天然不超发
```

实现用 EF Core `ExecuteUpdateAsync`（返回影响行数）；影响 0 行表示「本事务读到候选集后、提交前该奖品被并发抽空」→ 在同一事务内**重新读取候选集并重抽**，最多 3 轮；3 轮后仍失败且候选集为空 → 抛 `1502`（整事务回滚，次数不消耗）。

**备选与取舍**：

| 备选 | 优点 | 为什么不选 |
| --- | --- | --- |
| 乐观并发（`[Timestamp]`/rowversion）+ 冲突重试 | EF 原生、可捕获 `DbUpdateConcurrencyException` | 每轮冲突都要重载实体 + 重试，写放大；`ExecuteUpdateAsync` 单语句本身就是原子的，重试由业务决定更可控 |
| 悲观锁 `SELECT ... FOR UPDATE` 先锁行再扣 | 逻辑直观 | 锁持有时间 = 整个事务时长（含随机判定与记录写入），热点奖品（权重 66 的「谢谢参与」不扣库存，但权重 20 的券会热）串行化严重，P95 恶化 |
| Redis `DECR` 原子扣减 + 异步落库 | 吞吐最高 | 库存成为「Redis 权威 + DB 最终一致」，Redis 抖动/flush 即超发或丢扣；跨日/幂等回滚语义复杂；PRD Q9 要求严格防超发，不接受该风险 |

放弃的是「缓存层吞吐」与「框架级乐观并发封装」，换得单语句原子性与零重试成本。

### D-02 每日次数计数：按日聚合行 + 条件 UPDATE（不用 Redis 计数）

**决策**：新增 `UserDrawQuota(Id, UserId, DrawDate, UsedCount, ...)`，**唯一索引 `(UserId, DrawDate)`**，`DrawDate` 为服务端时钟算出的 UTC+8 自然日。扣次语句：

```sql
UPDATE UserDrawQuota SET UsedCount = UsedCount + 1, UpdateTime = @now
WHERE UserId = @uid AND DrawDate = @today AND UsedCount < @dailyLimit   -- 影响 0 行 = 次数不足
```

行不存在（当日首次抽奖）→ 尝试 `INSERT ... UsedCount = 1`；唯一键冲突（并发首抽）→ 回到条件 UPDATE（最多 3 轮）。「跨日重置」不需要任何定时任务：新的一天 = 新行 = 天然 3 次（FR-07-1）。剩余次数查询 = `@dailyLimit - COALESCE(UsedCount, 0)`。

**备选与取舍**：

| 备选 | 优点 | 为什么不选 |
| --- | --- | --- |
| 累计行（`UsedCount` + `LastDrawDate`）单行更新，CASE WHEN 判断日切 | 行数不增长 | 重置逻辑藏在 SQL CASE 里，可读性与可测性差；行锁热点集中在「所有用户」不成立但活跃用户单行反复更新；跨日用例需要构造历史状态，比「换一行」难 |
| Redis `INCR` 计数（TTL 到当日 24:00） | 快 | Redis 丢数据 = 用户白得次数（可接受）但也会出现「Redis 有、DB 无」导致 AC-10/AC-11 断言不可复现；计数是**限额判定**，必须与库存/记录同事务，Redis 无法进 DB 事务 |
| 计数与库存都不落 DB，纯内存 | 最快 | 多实例不一致、重启归零，直接违反 FR-08 |

放弃的是「行数恒定」，换得重置语义零代码（自然日即分区键）与同事务一致性。清理策略：`DrawDate` 早于 30 天的行可物理删除（限额状态表，允许物理删除；清理任务本期不做，登记为 v2 运维项）。

### D-03 幂等实现位置与存储：Application 层服务 + DB 唯一索引为底线 + Redis 结果缓存

**决策**：
1. **实现位置** = Application 层 `DrawService` 内（事务在业务层，规范 4.4）。不放在中间件/Filter：Filter 无法参与 DB 事务、无法与「次数/库存/记录」原子提交，且响应重放会绕过异常的 `ApiResult` 包装路径。
2. **正确性底线** = MySQL `DrawRequest` 表，唯一索引 `(UserId, IdempotencyKey)`（`IdempotencyKey` 可空，缺键 = 普通新请求，MySQL 唯一索引允许多个 NULL，符合 FR-08-4「缺少 key 视为新请求」）。抽奖事务的**第一条写入**就是插入 `DrawRequest`（见 D-08 锁序），插入成功 = 抢到该 key 的执行权。
3. **重复请求路径**：
   - Redis 命中（`draw:idempotency:{userId}:{key}`，值为首次结果 JSON，TTL 24h）→ 直接重放，不触库；
   - Redis 未命中 → 正常进入事务；若 `DrawRequest` 插入触发唯一键冲突（DB 1062）→ 回滚本事务 → 读已提交的首次 `DrawRequest` 行 → **重放首次结果**（`isWin` / `itemId` / `remainingAttempts` 原样返回，code=0）；
   - 并发同 key 同时提交：第二个事务在唯一索引上等待第一个提交 → 得到 1062 → 重放。等待超时（1205）→ 返回 `409`（「请勿重复提交」）；
   - 请求体哈希不一致（`RequestHash` 列）→ `409`。因 FR-05-R1 规定抽奖请求体为空，哈希不一致在正常调用中不可达（保留该分支是为规范 6.5 的语义完整性）。
4. **业务拒绝（1501 / 1502）不缓存、不落 `DrawRequest`**（事务回滚天然不落）：拒绝是状态确定性的，重试必然复现同样拒绝，无需重放，也避免了「昨日拒绝被 24h 缓存重放到今日」的跨日误伤。Redis 只缓存成功结果（含未中奖），TTL 24h。

**备选与取舍**：

| 备选 | 优点 | 为什么不选 |
| --- | --- | --- |
| 规范 6.5 字面实现：Redis `SET NX` 写 PENDING → 处理 → 覆盖为结果；重复请求轮询 PENDING 直至有结果或超时 409 | 与规范文字完全一致 | PENDING 中间态要引入轮询与超时判定，并发同 key 的「返回与首次一致」退化为「大概率一致，超时 409」；而 DB 唯一索引本身就能给出强一致的重放（InnoDB 让第二个插入等待第一个提交）——轮询是多余的复杂度 |
| 纯 Redis（无 DB 兜底） | 最简单 | 违反规范 6.5「数据库唯一索引兜底」；Redis 重启/flush/淘汰后同 key 重放变为重复扣次，直接违反 AC-12 |
| 纯 DB（去掉 Redis 缓存） | 单一数据源，最少组件 | 重复请求要走一次注定失败的插入 + 回滚，热点场景下白白占用唯一索引锁；结果缓存是廉价的正收益（1 次 GET） |
| 中间件 / ActionFilter 统一幂等 | 一处代码全接口复用 | 无法进事务；重放需要缓存完整 HTTP 响应（含头与状态），与全局 ResultFilter 的包装顺序耦合，脆弱 |

放弃的是「与规范 6.5 逐字一致」（本实现是 6.5 的**语义等价细化**，已在摘要中标注，供 code-reviewer 复核）与「纯 Redis 的极简」，换得并发同 key 的确定性重放。

### D-04 加权随机：整数权重 + 累积区间取样 + 加密级随机源

**决策**：
- 权重为整数（配置与表字段均为 int）；候选集权重求和 `Σw`；取 `r = RandomNumberGenerator.GetInt32(0, Σw)`（`[0, Σw)` 均匀整数）；按 `DisplayOrder` 顺序做累积区间扫描，`r < cumulative` 命中。整数运算无浮点误差，`P(i) = wᵢ/Σw` 精确成立（AC-16 的偏差只来自抽样波动）。
- 候选集 = 启用中条目 ∩（`type = NoPrize` 或 `Stock > 0`）（FR-05-R3），每次判定**实时**从 DB 读取（`AsNoTracking + Select` 投影出 `Id/Weight/Stock/Type/DisplayOrder`），不缓存权重——缓存会让「库存归零即时剔除」（AC-14）出现窗口期。
- 候选集为空 → 抛 `1502`（事务回滚，次数不消耗）。
- 随机源经 `IRandomSource` 注入（D-05），生产实现为 `RandomNumberGenerator`（加密级，不可预测），**不**用 `Random.Shared`：彩票类场景若随机数可预测，等于把 AC-16 的可验证性与 FR-05-R1 的「结果不可由客户端影响」一起丢掉。

**备选与取舍**：

| 备选 | 优点 | 为什么不选 |
| --- | --- | --- |
| `Random.Shared.Next` | 最快、零分配 | 可预测（种子可推断），且多线程共享实例在重负载下分布抖动；彩票语义不达标 |
| Alias Method（别名法） | O(1) 单次采样 | 候选集 ≤ 10 条（默认 5），O(n) 扫描成本 < 1µs，收益为零；别名表的构建/更新复杂度与「候选集每次实时重算」冲突 |
| 浮点权重（double）+ `[0,1)` 随机 | 权重可配置为小数 | 累积求和引入舍入误差，边界判定需要 epsilon 补偿，统计断言（AC-16 偏差 ≤2pp）更容易踩坑 |
| 预计算「概率表」缓存到 Redis | 减少每请求计算 | 库存变化无法实时反映（AC-14 要求剔立即生效），缓存失效复杂度远超收益 |

放弃的是「支持小数权重」与「O(1) 采样」，换得精确整数概率与实时候选集。

### D-05 时钟与随机源抽象：BCL `TimeProvider` + 自定义 `IRandomSource`

**决策**：
- 时钟：使用 .NET 内置 `TimeProvider`（BCL，随 DI 注册为 Singleton），业务代码只调 `TimeProvider.GetUtcNow()`；测试用 **override `GetUtcNow()` 的极小 Fake 子类**（测试程序集内 3 行代码）。不引入 `Microsoft.Extensions.TimeProvider.Testing` 新包（规范 2.4：不引入规范未提及的依赖）。
- 时区：UTC+8 判定用**固定偏移 `TimeSpan.FromHours(8)`**（中国无夏令时），不使用 `TimeZoneInfo.FindSystemTimeZoneById`（Windows/Linux tz 数据库差异是跨平台测试的经典坑）。自然日换算收敛在一处：`ClockExtensions.GetDrawDateUtc8(this TimeProvider)`。
- 随机源：Application 层定义 `IRandomSource { int NextInt(int exclusiveMax); }`；Infrastructure 实现 `SystemRandomSource`（`RandomNumberGenerator.GetInt32`）；测试用 `SeededRandomSource`（可复现，仅测试程序集）与 `ForcedRandomSource`（返回固定值，用于边界用例）。注册为 Singleton。

**备选与取舍**：

| 备选 | 优点 | 为什么不选 |
| --- | --- | --- |
| 自建 `IClock` 接口 + `SystemClock` | 不依赖 BCL 版本 | 与 .NET 10 生态重复造轮子（规范工作准则 2「复用优先」）；`TimeProvider` 已是一等公民 |
| 引入 `Microsoft.Extensions.TimeProvider.Testing`（FakeTimeProvider） | 开箱即用、支持时间推进 | 规范 2.4 未列该包；自写 Fake 只需 override 一个方法，成本更低 |
| 静态工具类（如 `DateTime.UtcNow` 直调） | 零注入 | 跨日用例（AC-11）无法构造，直接违背 PRD 4.6 |

放弃的是「FakeTimeProvider 的便利 API（如 Advance）」，换得零新增依赖；如需「推进时间」，测试直接改 Fake 的 `Now` 字段即可。

### D-06 确定性配置的防泄漏：启动强校验（fail fast）

**决策**：测试可控性配置集中在一个 `Draw` 配置节，且由 `IValidateOptions<DrawOptions>` 在**应用启动时**强校验：

| 配置键 | 用途 | 生产基线取值 |
| --- | --- | --- |
| `Draw:DailyLimit` | 每日次数（默认 3） | 3 |
| `Draw:Deterministic:Enabled` | 确定性开关 | **false** |
| `Draw:Deterministic:ForcedResults[]` | `{ userName, prizeItemCode }` 映射：指定用户名命中指定条目（用于 AC-08 / AC-09 / AC-13 构造中奖与未中奖链路） | **空数组** |
| `Prize:WeightOverrides{}` | `{ code: weight }` 权重覆盖（用于 AC-14 / AC-16 的统计场景） | **空对象** |

校验规则：若 `Deterministic:Enabled = true` 或任一覆盖项非空，而宿主环境**既不是 Development 也不是 Testing** → 抛 `InvalidOperationException`，**应用拒绝启动**，并写 CRITICAL 日志。同时 `appsettings.json`（生产基线）只含 `Draw:Deterministic:Enabled = false`，确定性配置只允许出现在 `appsettings.Development.json` 与环境变量注入中（PRD R5 = 部署验收项，见附录 B）。

运行时生效点：`Deterministic:Enabled` 为真时，`DrawService` 在加权随机前先查映射（用户名不区分大小写）；命中则直接以该条目为结果（仍走完整的次数扣减 / 库存条件扣减 / 记录 / 审计事务路径——**只替换随机判定这一步**，其余规则与生产一致）。映射中的 `prizeItemCode` 若查无此条目 → 记 warning 并回退正常加权随机（测试配置错误不产生 500）。

**备选与取舍**：

| 备选 | 优点 | 为什么不选 |
| --- | --- | --- |
| 只靠配置文件分层（生产配置文件不写该节） | 零代码 | 只靠约定，任何一次合并/覆盖失误都会静默泄漏；(PRD R5 要求"必须不可达"，约定不是不可达) |
| 编译期 `#if DEBUG` / 条件编译 | 编译产物层面物理隔离 | E2E 与集成测试跑的是 Release 产物，测不了；且业务分支被编译符号割裂，可读性差 |
| 独立测试构建镜像 | 最彻底 | 构建/部署成本翻倍，且无法覆盖「同一镜像 + 环境变量误配」这一最常见的泄漏路径 |

放弃的是「零代码约定」的简洁，换得「配置泄漏 = 应用起不来」的机械保证。

### D-07 奖池接口的 DTO 投影：Select 白名单（结构上不可能泄漏权重/库存）

**决策**：`PrizePoolItemDto = { id, name, shortName, type, displayOrder }`，查询 `Where(IsEnabled && !IsDeleted).OrderBy(DisplayOrder).Select(...)` 投影，`AsNoTracking()`。DTO 无 `Weight` / `Stock` 属性（不是「序列化时忽略」，而是**不存在**），AC-06 的「响应不含权重与库存字段」由结构保证，集成测试断言 JSON 键集合。

`Code`（种子稳定性标识）与 `IsDeleted` 同样不下发；`shortName` 供转盘扇区文字（原型扇区短名：一等奖 / 二等奖 / 三等奖 / 幸运奖 / 谢谢参与）与图例对应。

**备选与取舍**：Entity + `[JsonIgnore]` → 违反规范 4.4「禁止返回 Entity」，且未来新增敏感列时忘记标注即泄漏，弃；自定义 JsonConverter 白名单 → 过度设计，弃。放弃的是「少写一个 DTO」，换得不可逆的信息收口。

### D-08 抽奖事务边界、隔离级别与固定锁序

**决策**：
- **事务宿主** = Application 层 `DrawService`（规范 4.4：事务在业务层，控制器不碰）；隔离级别显式 **READ COMMITTED**。
  为什么不是 MySQL 默认 RR：重抽（D-01 第 3 步）与重试读必须在**失败后看到最新已提交的库存**，RR 的普通 SELECT 复用事务快照会读到旧值，导致「重抽仍选到刚被抽空的奖品」循环失败；条件 UPDATE 在两种隔离级下都是当前读、本身安全，但候选集回读需要 RC。
- **锁序（同类事务必须一致，防死锁）**：

```
① INSERT DrawRequest（唯一索引 (UserId, IdempotencyKey)，抢幂等权 + 占位）
② UPDATE UserDrawQuota（条件扣次；影响 0 行 → 抛 1501，回滚）
③ SELECT 候选集（AsNoTracking 投影，实时读）
④ 加权随机判定（内存）
⑤ 命中奖品额外：UPDATE PrizeItem（条件扣库存；影响 0 行 → 回 ③ 重抽，≤3 轮）
⑥ INSERT WinningRecord（仅中奖）
⑦ INSERT AuditLog（FR-05-R8，同一事务：与扣减同生共死）
⑧ UPDATE DrawRequest（回填结果：itemId / isWin / remaining）
⑨ COMMIT → ⑩ Redis SETEX 幂等结果缓存（best effort，失败仅告警不回滚）
```

  ①→②的顺序**不可颠倒**：若先锁 quota 再争 DrawRequest，两个同 key 并发请求会与正常请求形成锁环（A 持 quota 等 DrawRequest 唯一锁，B 持 DrawRequest 锁等 quota）。
- **异常与重试**：`DbUpdateException` 1062（幂等重复）→ 回滚 → 读首次行重放（D-03）；锁等待超时 1205 且发生在 ①→ 映射为 `409`；死锁 1213 → 整个事务重试 1 次，仍失败 → `1001`（HTTP 200）+ CRITICAL 告警。
- **终态错误码改判说明（v4，用户裁决）**：原稿终态写 `500`，描述的是**缺陷状态本身**——修复前瞬时错误未做分类、直接冒泡到全局 ExceptionFilter 兜底，用户实测看到的正是 500（`docs/52-qa-report.md` §1.6.6、`tests/e2e/logs/tc56b-*`）；且 `docs/error-codes.md` 明文禁止业务代码手动使用 500。CHG-16 修复后按设计落 `1001`「系统繁忙，请稍后重试」（HTTP 200），本行随用户裁决对齐代码现状；「整个事务重试 1 次」与「CRITICAL 告警」不变（详见文首「v4 变更说明」）。
- **审计写入方式说明**（规范 4.4 关于 `ExecuteUpdateAsync` 绕过拦截器的告诫）：stock / quota 用 `ExecuteUpdateAsync`，不走变更跟踪，故**不产生**实体级自动审计——这是刻意的：这两个计数器每次抽奖都变，实体级「变更前/后 JSON」无业务价值。抽奖的审计义务由 ⑦ 的显式 `IAuditService` 写入承担（用户、时间、结果条目、消耗次数，FR-05-R8），且**同事务**提交：审计失败 = 抽奖失败（比规范第十章「审计失败不阻断主流程」更严格；理由：抽奖是本系统唯一的资金等价动作，宁可拒绝也不留无痕操作。该加强点登记为假设 A2，**已于 2026-09-17 确认**（用户采用架构师默认假设））。
- **回滚语义**：1501 / 1502 / 任一系统异常 → 整事务回滚，次数、库存、记录、DrawRequest 四者零残留（FR-08-3）。

**备选与取舍**：

| 备选 | 优点 | 为什么不选 |
| --- | --- | --- |
| 保持 RR + 重读候选时用 `SELECT ... FOR UPDATE`（当前读） | 不改隔离级 | 每次重读都加锁，锁面扩大；RC 下普通回读即可，成本更低 |
| SESerializable | 最强隔离 | 冲突率暴涨、退化为串行，P95 直接失控 |
| 拆分「扣次事务」与「扣库存+写记录事务」 | 事务更短 | 中间失败 = 次数扣了奖品没记（FR-08-3 三方一致被破坏），弃 |
| 幂等表 INSERT 放在事务最后 | 减少无效插入 | 锁序变成 quota→prize→…→DrawRequest，与并发同 key 正常请求形成环，见上；且重复请求会先白扣一轮再回滚 |

放弃的是「先扣次后扣库存的直观顺序」与「更短事务」，换得确定性锁序与幂等权先占。

### D-09 奖池初始化：EF 迁移 `HasData` 种子

**决策**：5 条默认奖池在 `OnModelCreating` 用 `HasData` 声明（固定 `Id` 1–5 与 `Code`），随迁移落库；`dotnet ef database update` 重复执行不产生重复条目（迁移历史表保证只执行一次）→ FR-11-1 / AC-18。

| Id | Code | Name | ShortName | Type | Weight | Stock |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | `prize-keyboard` | 一等奖 · 机械键盘 | 一等奖 | 1 实物 | 1 | 3 |
| 2 | `prize-earbuds` | 二等奖 · 蓝牙耳机 | 二等奖 | 1 实物 | 3 | 10 |
| 3 | `prize-mug` | 三等奖 · 定制马克杯 | 三等奖 | 1 实物 | 10 | 50 |
| 4 | `prize-coupon` | 幸运奖 · 平台优惠券 | 幸运奖 | 2 虚拟 | 20 | 200 |
| 5 | `no-prize` | 谢谢参与 | 谢谢参与 | 3 未中奖 | 66 | 0（类型豁免，不占库存） |

`DisplayOrder` = 1–5（与原型扇区 ①–⑤ 一一对应）；`IsEnabled = true`；`IsDeleted = false`。

**备选与取舍**：启动时幂等 Seeder（`if not exists insert`）→ 应用启动即写库、多实例启动竞态、测试库难以隔离，弃；手工 SQL 脚本 → 脱离迁移体系（规范 5.4），弃。放弃的是「可在生产热修奖池」的便利（本期不需要，W1 不做管理端）。

### D-10 中奖记录冗余名称快照

**决策**：`WinningRecord` 同时存 `PrizeItemId`（可追溯）与 `PrizeName`（展示快照，写入时从奖品表带出）。列表查询零 join，走索引 `(UserId, CreateTime)`。

**备选与取舍**：仅 FK + 查询时 join → 奖品改名/下架会污染历史记录（记录不可改，FR-09-2 要求历史稳定）；纯名称无 FK → 丢失可追溯性。放弃的是「无冗余的单源」，换得历史记录不可变性与列表性能。

### D-11 刷新令牌存储：Redis 单会话族 + 哈希存储

**决策**：
- refresh token 为 7 天有效期的不透明随机串，落 httpOnly + Secure + SameSite=Strict Cookie，`Path=/api/v1/auth`（仅刷新/登出接口携带）；access token 2h，**仅存内存**（不落 localStorage / sessionStorage；规范 3.3 于 2026-09-17 修订后的口径），用户信息同样仅存内存；页面刷新 / 新标签页由**受保护路由**触发一次静默恢复补全会话（契约见 §2.8）。
- 服务端状态：Redis `draw:auth:refresh:{userId}` = `{ jti, tokenSha256, expiresAt }`，TTL 7 天；**只存哈希**（禁止缓存敏感信息明文，规范第九章）。
- 轮换与复用检测：每次 refresh 生成新 token 覆盖该键；若请求 token 的哈希 ≠ 当前值（且键存在）→ 判定复用 → 删除键（该用户全部会话失效）+ 审计告警 + 返回 `1203`；键不存在（过期/登出）→ `1204`。
- 登出：删键 + 下发过期 Cookie + 审计。
- 已知取舍：Redis 丢失 = 全部用户需要重新登录（可接受）；access token 在其 2h 有效期内不随登出即时失效（无黑名单；如需即时踢线，v2 增加 Redis 黑名单，登记为已知优化）。

**备选与取舍**：DB `UserRefreshToken` 表（多设备、可审计、持久）→ 多一次写放大与清理任务，本期无多设备要求；纯无状态 refresh → 无法实现规范 4.3 要求的一次性轮换与复用检测。放弃的是「多设备会话」，换得最少的存储面。

**access token 存储口径（v3 修订，Q1 裁决）**：备选 ① localStorage（v2 及旧规范 3.3 口径）→ 免恢复请求，但一次 XSS 即可读走凭证；② sessionStorage → 标签页级存储，同样可被 XSS 读取，且把「是否已登录」的判据放到标签页存储会引入「新标签页即使 refresh Cookie 有效也被强制登出」类回归（REV-02 现场）；③ **仅存内存（采纳）** → 凭证生命周期限制在当前页面上下文，代价是刷新 / 新标签页需一次静默恢复（恢复面收敛在受保护路由，契约见 §2.8）。放弃的是「免恢复请求的便利」，换得 XSS 面收窄与明确的恢复契约。

### D-12 登录失败不得用 401 表达（错误码分工的总原则）

**决策**：`401` 的语义被规范 3.4 占用为「access token 失效 → 前端无感刷新」。因此**登录失败 / 账号锁定 / 注册冲突 / 参数校验失败一律返回 HTTP 200 + 业务 code**（1102 / 1103 / 1101 / 1002），只有「受保护接口缺/坏 access token」才用 401。

各状态与 HTTP 的分工（全接口一致，详见 §5.1）：`400` 模型绑定与 DataAnnotations；`401` 凭证缺失/失效；`403` 无权限（本期无 RBAC，保留语义）；`409` 幂等冲突（重复提交 / key 复用体不一致）；`429` 限流；`500` 系统内部错误；`≥1000` 业务异常一律 HTTP 200。

**备选与取舍**：401 + 前端按接口路径白名单豁免刷新 → 任何一次漏配都表现为「登录失败却跳登录页刷圈」的诡异现象，且在拦截器里维护路径名单是长期负债。放弃的是「HTTP 语义洁癖」，换得刷新拦截器行为可预测。

### D-13 目录骨架：`src/backend` + `src/frontend`，测试三分区

**决策**：见 §2.5。前后端都收在 `src/` 下（`docs/artifacts.md` 将 engineer 写入范围登记为 `src/`、`tests/unit/**`、`tests/integration/**`；`tests/e2e/**` 归 test-executor），同时满足 CLAUDE.md「前后端分目录」的建议。

**备选与取舍**：顶层 `frontend/` + `backend/`（更常见的 monorepo 命名）→ 与 artifacts 登记的 `src/` 字面不符，需主对话改登记，弃；单一解决方案内嵌 SPA（`wwwroot` 托管）→ 与「前后端分离」需求原文冲突，弃。

### D-14 转盘动画：CSS transition + 目标角计算（不引入动画库）

**决策**：
- 扇区顺序 = 奖池 `displayOrder` 升序；扇区 i（0 起）中心角 `θi = 72° × i + 36°`（5 扇区，12 点方向起顺时针；扇区数按奖池条目数动态计算 `360/N`）。
- 指针固定 12 点；转子目标角 `rotation = 360° × n + (360° - θi)`，`n = 3`（满足 FR-06「≥2 圈」）。
- 实现：`DrawWheel.vue` 用 `transform: rotate(...)` + `transition: transform 2400ms cubic-bezier(.16,.84,.24,1)`；`motion-reduce:` 下直接落终值（无过渡、无旋转圈）。Dialog 在 `transitionend` 后打开（2.6s 兜底定时器防事件丢失）。
- 落点唯一来源 = 后端返回 `itemId` 在当前奖池快照中的位置；`itemId` 不在快照 → 重拉奖池一次再定位；仍无 → 跳过旋转、直接弹 Dialog 并记 warning（保证结果反馈不被渲染边界阻塞）。
- 动画期间按钮保持禁用（`page-draw--drawing` 态），后端亦已逐次校验（前端禁用只是体验）。

**备选与取舍**：GSAP / anime.js → 规范 2.4 未列，需走依赖确认且为本项目唯一用例引入 30KB+，弃；Web Animations API → 零依赖但需要手写 reduced-motion 判定与 `finished` Promise 处理，与 Tailwind 的 `motion-reduce:` 体系割裂，弃；「先随机转、出结果后再纠正落点」→ 违反 FR-06 落点严格一致，弃。

### D-15 注册 / 登录的幂等：复用 `IIdempotencyStore`（Redis 结果重放 + NX 占位），不新增表

**决策**（v3 新增；2026-09-17 用户裁决 **Q2「按契约补齐」**，规范 6.5 通用要求落地）：

1. **作用域** = `(operation, Idempotency-Key)`，`operation ∈ {register, login}`；请求体哈希不一致 → `409`（同 D-03 语义）；键缺失 = 无幂等，正常处理（同 FR-08-4 口径，不报错）。
2. **实现** = 复用 MOD-05 的 `IIdempotencyStore` 抽象（Infrastructure 的 Redis 实现；如需按值类型泛化由 engineer 在 Application 层完成，不引入新依赖）；key = `draw:idempotency:auth:{operation}:{key}`（前缀沿用 A3），TTL **10 分钟**（覆盖双击 / 网络重试窗口；认证结果无金额语义，无需 24h）。
3. **并发同键** = `SET NX` 占位（PENDING）→ 第二个请求短轮询（≤ 2s）取结果 → 命中即重放；超时仍未落定 → `409`（「请勿重复提交」）。此处与 D-03 的差异是刻意的：抽奖有 `DrawRequest` 唯一索引给出强一致重放，认证没有等价的 DB 行（见备选表），故采用规范 6.5 的 NX + 短轮询形态。
4. **缓存内容不含任何凭证**（禁止缓存敏感信息明文，规范第九章）：只存 `{ userId, requestHash, completed }`；重放时**重新签发** access / refresh 凭证。同一逻辑提交只产生**一次业务副作用**：注册建用户一次；`Register` / `Login` 审计各一条；登录失败计数不重复累计；重放路径不重复审计（凭证是可再生的传输物，重新签发不算业务副作用）。
5. **仅缓存成功**：`1101` / `1102` / `1103` / `1002` 等业务拒绝不缓存（与 D-03-4 一致）。已知边界：未命中缓存的失败重试可能使登录失败计数 +1（更严格方向，登记为可接受）。
6. **前端**（配合 §5.4）：表单挂载生成一次 UUID；**收到明确业务结果（成功或业务拒绝）后重建**；仅在「未获得业务结果」的重试（网络错误 / 超时 / `429` / `5xx`）沿用同键。

**备选与取舍**：

| 备选 | 优点 | 为什么不选 |
| --- | --- | --- |
| 新增通用 `IdempotencyRequest` 表（DB 唯一索引兜底） | 与 D-03 同构；Redis 丢失后仍可重放 | 认证接口的重复执行**不破坏任何数据库不变量**（注册由 `User.UserName` 唯一索引兜底、登录不写业务表且单用户单会话族由 Redis 键覆盖兜底），DB 兜底的收益仅剩「Redis 丢后少一次 1101 / 少一次重登录」；代价是新增迁移 + 清理任务 + 双写 |
| 复用 `DrawRequest` 表 | 零新增结构 | 该表是抽奖流水（`PrizeItemId` / `IsWin` / `RemainingAttempts` 列语义专属，`UserId` NOT NULL），注册时尚无 `UserId`，写入会污染唯一索引 `(UserId, IdempotencyKey)` 的语义 |
| 只做前端防重复（不发请求头） | 零后端改动 | 用户已裁决「按契约补齐」；且前端单侧防不住「响应丢失后的重试」与直连接口场景（规范 6.5 的通用要求本身即含服务端语义） |

放弃的是「与抽奖同构的 DB 兜底」，换得零新增表与零迁移。

---

## 4. 数据设计

### 4.1 实体关系（6 表 + Redis）

```
User 1 ──── n WinningRecord n ──── 1 PrizeItem       （中奖记录：快照名称 + 可追溯 FK）
User 1 ──── n UserDrawQuota                          （每人每日一行，唯一 (UserId, DrawDate)）
User 1 ──── n DrawRequest                            （抽奖请求流水/幂等权，唯一 (UserId, IdempotencyKey)）
AuditLog                                             （独立只增表，不建 FK，避免审计被主表约束牵连）
Redis：幂等结果缓存 / refresh 会话 / 登录失败计数（§4.6）
```

### 4.2 表结构（MySQL 8.4 / utf8mb4 / InnoDB / 枚举存 int）

**User（MOD-01）**

| 列 | 类型 | 约束 | 说明 |
| --- | --- | --- | --- |
| Id | int | PK, 自增 | |
| UserName | varchar(20) | NOT NULL | 4–20 位 `[A-Za-z0-9_]`；**大小写不敏感唯一**（依赖 collation `utf8mb4_0900_ai_ci`，FR-01-1） |
| PasswordHash | varchar(100) | NOT NULL | BCrypt；禁止任何日志/响应输出（审计白名单排除该列） |
| IsDeleted | tinyint(1) | NOT NULL default 0 | |
| CreateTime / UpdateTime | datetime | NOT NULL | UTC，由拦截器统一赋值 |

索引：`UX_User_UserName` UNIQUE(UserName)。软删除与唯一键的取舍按规范 5.3 选项②：**删掉的用户名不再可用**（本期无删除入口）；所有唯一性校验查询必须 `IgnoreQueryFilters()`（规范 4.4）。

**PrizeItem（MOD-02）**

| 列 | 类型 | 约束 | 说明 |
| --- | --- | --- | --- |
| Id | int | PK | 种子固定 1–5 |
| Code | varchar(32) | NOT NULL | 种子/测试覆盖的稳定标识（不下发前端） |
| Name | varchar(50) | NOT NULL | 展示全名（如「三等奖 · 定制马克杯」） |
| ShortName | varchar(20) | NOT NULL | 转盘扇区短名（一等奖 / 二等奖 / 三等奖 / 幸运奖 / 谢谢参与） |
| Type | int | NOT NULL | 1=实物 / 2=虚拟 / 3=未中奖（NoPrize，豁免库存） |
| Weight | int | NOT NULL | 相对权重；**不下发前端** |
| Stock | int | NOT NULL | NoPrize 恒为 0 且不参与扣减；**不下发前端** |
| DisplayOrder | int | NOT NULL | 扇区顺序（1–5） |
| IsEnabled | tinyint(1) | NOT NULL default 1 | 上下架开关（FR-03-2，不做删除） |
| IsDeleted | tinyint(1) | NOT NULL default 0 | |
| CreateTime / UpdateTime | datetime | NOT NULL | UTC |

索引：`UX_PrizeItem_Code` UNIQUE(Code)；`IX_PrizeItem_Enabled_Order` (IsEnabled, DisplayOrder)（奖池查询：筛选 + 排序）。

**WinningRecord（MOD-04）**

| 列 | 类型 | 约束 | 说明 |
| --- | --- | --- | --- |
| Id | int | PK | 每用户每日 ≤3 条，int 足够 |
| UserId | int | NOT NULL, FK→User | 恒为当前登录用户，接口不接受 userId 参数 |
| PrizeItemId | int | NOT NULL, FK→PrizeItem | 可追溯 |
| PrizeName | varchar(50) | NOT NULL | **写入时快照**（D-10） |
| IsDeleted | tinyint(1) | NOT NULL default 0 | PRD 4.3：记录不做物理删除 |
| CreateTime / UpdateTime | datetime | NOT NULL | CreateTime 即「中奖时间」；倒序键 |

索引：`IX_WinningRecord_User_Time` (UserId, CreateTime)（覆盖列表查询：等值 + 倒序 + 分页；同时满足 FK(UserId) 的最左前缀要求，不另建单列索引）。记录只读：无 UPDATE / DELETE 路径（FR-09-2）。

**UserDrawQuota（MOD-03）**

| 列 | 类型 | 约束 | 说明 |
| --- | --- | --- | --- |
| Id | int | PK | |
| UserId | int | NOT NULL | |
| DrawDate | date | NOT NULL | **UTC+8 自然日**（服务端时钟换算） |
| UsedCount | int | NOT NULL | 已消耗次数（未中奖同样计入，FR-04） |
| CreateTime / UpdateTime | datetime | NOT NULL | UTC |

索引：`UX_UserDrawQuota_User_Date` UNIQUE(UserId, DrawDate)。
无 `IsDeleted`：限额状态行不是用户资产，允许物理删除（规范 5.2 的日志/会话类例外），旧行清理任务登记为 v2 运维项（本期不做，避免范围蔓延）。

**DrawRequest（MOD-05 幂等 + 流水）**

| 列 | 类型 | 约束 | 说明 |
| --- | --- | --- | --- |
| Id | bigint | PK | 高速增长表（用 bigint，规范 5.2） |
| UserId | int | NOT NULL | 幂等键作用域 = 用户 |
| IdempotencyKey | varchar(64) | NULL | 缺省 = 无幂等（FR-08-4）；MySQL 唯一索引允许多个 NULL |
| RequestHash | varchar(64) | NULL | 请求体规范化 SHA-256；不一致 → 409（R1 下正常不可达） |
| PrizeItemId | int | NULL | 结果条目（未中奖也写，指向「谢谢参与」） |
| IsWin | tinyint(1) | NOT NULL | |
| RemainingAttempts | int | NOT NULL | 首次响应里的剩余次数（重放原样返回） |
| CreateTime / UpdateTime | datetime | NOT NULL | UTC |

索引：`UX_DrawRequest_User_Key` UNIQUE(UserId, IdempotencyKey)。
无 IsDeleted / 无查询接口（≠ PRD W6）：流水表，物理删除允许，保留 30 天（清理任务 v2）。
「成功才落行」：1501 / 1502 / 系统异常事务回滚 → 无行（D-03-4）。

**AuditLog（MOD-06，按规范第十章字段集）**

| 列 | 类型 | 约束 | 说明 |
| --- | --- | --- | --- |
| Id | bigint | PK | 只增表 |
| OperatorId | int | NULL | 系统动作为 NULL |
| OperateTime | datetime | NOT NULL | UTC |
| IpAddress | varchar(45) | NULL | 取 `HttpContext`（反代后须为还原后的真实 IP） |
| UserAgent | varchar(200) | NULL | 截断存储 |
| Module | varchar(50) | NOT NULL | `auth` / `draw` |
| OperationType | varchar(50) | NOT NULL | `Register` / `Login` / `LoginFail` / `Logout` / `RefreshReuse` / `Draw` |
| TargetObject | varchar(64) | NULL | 如 DrawRequest.Id / UserId |
| BeforeJson / AfterJson | text | NULL | 抽奖的 AfterJson = `{ prizeItemId, isWin, remaining }`；**脱敏**：密码哈希、token、Cookie 一律排除 |

索引：`IX_AuditLog_Module_Time` (Module, OperateTime)。
无 UpdateTime / IsDeleted（只增不改；第十章专门约定优先于 5.2 通用要求）；日志写入失败不阻断主流程的通用策略**对抽奖例外**（见 D-08，登记为 A2，已于 2026-09-17 确认）。

### 4.3 逻辑删除使用面（规范 5.2 的落点）

| 表 | IsDeleted | 理由 |
| --- | --- | --- |
| User | 有 | 核心实体，规范红线；本期无删除入口 |
| PrizeItem | 有 | 用启用开关上下架、不做删除（FR-03-2）；保留 IsDeleted 以兼容未来迁移口径 |
| WinningRecord | 有 | PRD 4.3：记录不做物理删除 |
| UserDrawQuota | 无 | 限额状态行，允许物理删除（清理任务 v2） |
| DrawRequest | 无 | 幂等/审计流水，允许物理删除（保留 30 天） |
| AuditLog | 无 | 只增不改，按月归档（规范第十章） |

### 4.4 审计日志落地

- **自动**（`SaveChangesInterceptor`）：追踪 User / PrizeItem 的实体修改；抽奖路径的 `ExecuteUpdateAsync` 刻意不产生实体级审计（D-08 已述理由）。
- **显式**（`IAuditService`，随业务事务/请求提交）：注册、登录成功、登录失败（第 5 次触发锁定时）、登出、refresh 复用检测、**每次抽奖**（FR-05-R8）。
- 记录内容禁令：密码、token、Cookie 绝不出现在 BeforeJson / AfterJson / 日志（规范红线）；`PasswordHash` 列入拦截器排除名单。

### 4.5 种子与迁移（FR-11）

- 单一 `InitialCreate` 迁移：建 6 表 + `HasData` 5 条奖池（D-09 表）。
- 部署顺序：迁移先于应用启动（规范 5.4）；`dotnet ef migrations script` 产出 SQL 供 DBA 审核。
- 测试环境的权重/库存覆盖：**由测试夹具直接写测试库**（集成测试）构造 AC-13 / AC-14 / AC-16 场景；E2E 的确定性命中链路走 `Draw:Deterministic` 配置（D-06）。PRD FR-11-2 的「配置覆盖库存」不做运行时配置面（理由：库存是并发正确性状态，运行时覆盖会绕过条件更新语义），登记为假设 A5（已于 2026-09-17 确认）。

### 4.6 缓存设计（Redis，key 前缀 `draw`）

| Key | 值 | TTL | 用途 / 依据 |
| --- | --- | --- | --- |
| `draw:idempotency:{userId}:{key}` | 首次结果 JSON `{itemId,isWin,remainingAttempts}` | 24h | 重复请求快速重放（D-03，规范 6.5） |
| `draw:auth:refresh:{userId}` | `{jti, tokenSha256, expiresAt}` | 7d | refresh 轮换与复用检测（D-11） |
| `draw:auth:loginfail:{userNameLower}` | 失败计数（int，INCR） | 15m | 连续 5 次锁定 15 分钟（规范 4.3；按**提交的用户名**计数，不区分账号是否存在 → 防枚举） |

规则：全部 key 必带 TTL（禁止永久 key）；不缓存敏感信息明文（只存令牌哈希）；奖池与库存**不缓存**（AC-14 要求剔除即时生效）；策略建议 `maxmemory-policy volatile-lru`（所有 key 均有 TTL）。多实例部署共享同一 Redis（无本地缓存参与业务判定）。

### 4.7 时间与时区口径

| 环节 | 口径 |
| --- | --- |
| DB 存储 | UTC `datetime`，由 `SaveChanges` 拦截器统一赋 `CreateTime` / `UpdateTime` |
| 接口输出 | ISO 8601（UTC，`Z`）；`resetAt` 为次日 00:00（UTC+8）对应的 UTC 时刻 |
| 「今日」判定 | 服务端时钟（`TimeProvider`）→ 固定 UTC+8 偏移 → `DateOnly`（D-05）；**不信任客户端时间**（FR-05-R7） |
| 前端展示 | `Intl.DateTimeFormat('zh-CN', { timeZone: 'Asia/Shanghai' })` → `yyyy-MM-dd HH:mm`（免新增日期库，满足 FR-09-3 与 PRD 4.5-2） |

---

## 5. 接口设计

### 5.1 通用约定（所有接口适用）

| 项 | 约定 |
| --- | --- |
| URL | `/api/v1/[controller]` 小写（`AddRouting(o => o.LowercaseUrls = true)`；控制器 `[ApiVersion("1.0")]`） |
| 响应包装 | 控制器直接返回业务对象，全局 `ResultFilter` 包装为 `ApiResult<T>`；**禁止**手动构造、禁止控制器 try-catch（规范 4.2） |
| 成功 | `code = 0`，`data` 为业务体 |
| 业务异常 | `code ≥ 1000`，**HTTP 200**；前端按 `code` 判定，不按 HTTP 状态码（规范 6.4 / 3.4） |
| 错误码分工 | `400` 模型绑定/DataAnnotations；`401` 仅「access token 缺失/失效」（触发无感刷新）；`403` 无权限；`409` 幂等冲突；`429` 限流（响应体 `code=1001`）；`500` 系统内部错误；登录失败等**不得用 401**（D-12） |
| 认证 | `Authorization: Bearer {accessToken}`；公开接口显式 `[AllowAnonymous]`；受保护接口默认需要认证 |
| 分页 | 请求 `pageIndex`（**从 1 起**）/ `pageSize`（默认 10、上限 100，超出按 100）；响应 `PageResult<T> = { items, totalCount, pageIndex, pageSize }`（规范 6.2/6.3）。⚠️ TanStack Table 的 `pageIndex` 从 0 起：±1 转换必须收敛在 `composables/usePagination.ts` 一处 |
| 时间 | ISO 8601 字符串（UTC） |
| 提交 | `application/json`；提交类接口带 `Idempotency-Key`（UUID v4）请求头（规范 6.5）—— 抽奖见 D-03，注册 / 登录见 D-15 |
| 序列化 | camelCase（后端默认）；前端 `types/` 与 DTO 字段逐一对齐 |
| 限流 | 登录（按 IP+用户名）与注册（按 IP）：固定窗口 10 次/分钟；抽奖（按已认证 userId，Should-S3）：10 次/分钟；拒绝 → HTTP 429 + `code 1001`；反代下必须先 `UseForwardedHeaders`（规范 8.6） |

**版本与兼容策略（全部接口通用规则）**：
1. 当前版本 **v1**；URL 显式带版本（`/api/v1/...`）。
2. **向后兼容的定义**（v1 内允许、无需升版）：新增可选响应字段；新增错误码（前端必须对未知 `code` 走通用错误展示，不得白屏）；新增可选请求参数。
3. **破坏性变更**（必须升 `v2` 并保留 v1 ≥ 一个大版本周期、标注废弃）：删除/重命名响应字段、改变字段类型或语义、删除参数、改变错误码含义、改变认证方式。
4. **迁移路径**：v2 落地时，前端 `api/` 层按模块整体切 v2（路径常量集中定义），保留 v1 契约到「前端 v2 上线 + 一个发布周期」后移除；后端用 `[ApiVersion("2.0")]` 分方法映射，v1 方法标注 `[Obsolete]` 并在响应头提示。
5. 每个接口的专项兼容策略见其条目「版本与兼容」栏。

---

### 5.2 接口清单（API-01…API-08）

#### API-01 注册　`POST /api/v1/auth/register`

| 项 | 内容 |
| --- | --- |
| 关联 | MOD-01 / MOD-06 / MOD-07；FR-01、AC-01、AC-02 |
| 认证 | `[AllowAnonymous]`；限流：按 IP 10 次/分钟 |
| 请求头 | `Content-Type: application/json`、`Idempotency-Key: {uuid}`（D-15：同键重放返回首次成功结果，不重复建用户 / 审计；键缺失 = 无幂等，正常处理） |
| 请求体 | `{ userName: string, password: string, confirmPassword: string }`（DTO 校验：用户名 4–20 位 `[A-Za-z0-9_]`；**密码 8–20 位且须同时包含大写字母、小写字母与数字**（CR-01 口径，规范 8.1）；两次一致——后端 FluentValidation 与前端 `types/auth.ts` 的 Zod Schema **规则文本逐字一致**，缺任一类字符即校验失败 → `1002`） |
| 响应数据 | `{ accessToken: string, expiresIn: number, user: { id: number, userName: string } }`；refresh token 走 `Set-Cookie`（httpOnly + Secure + SameSite=Strict，`Path=/api/v1/auth`） |
| 成功语义 | 注册即登录（FR-01-4）；唯一性判定含大小写变体（collation 已保证），校验查询 `IgnoreQueryFilters()` |
| 错误语义 | `1101` 用户名已被占用（HTTP 200）；`1002` 校验失败（HTTP 200）；`400` 绑定失败；`429` 限流；`409` 幂等键复用且体不一致 |
| 版本与兼容 | v1。响应字段只增不改；若需新增用户字段（v2 候选），必须在 v1 尾部追加可选字段并同步前端 `types/auth.ts` |
| 审计 | `Register`（含 IP/UA；AfterJson 仅 userName/id，绝不含密码） |

#### API-02 登录　`POST /api/v1/auth/login`

| 项 | 内容 |
| --- | --- |
| 关联 | MOD-01 / MOD-06；FR-02、AC-03、AC-04 |
| 认证 | `[AllowAnonymous]`；限流：按 IP + 用户名 10 次/分钟 |
| 请求头 | `Content-Type: application/json`、`Idempotency-Key: {uuid}`（D-15：同键重放返回首次成功结果并重新签发凭证，不重复累计失败计数 / 审计；键缺失 = 无幂等，正常处理） |
| 请求体 | `{ userName: string, password: string }` |
| 响应数据 | 同 API-01（token + user） |
| 成功语义 | 签发 access（2h）+ refresh（7d，Redis 记录，D-11）；前端有 `redirect` 参数则回跳，否则 `/draw` |
| 错误语义 | `1102` 用户名或密码错误（**统一文案，不区分原因**，HTTP 200）；`1103` 连续 5 次失败锁定 15 分钟（HTTP 200，按提交用户名计数 → 对不存在的用户名同样生效，防枚举）；`1002` 校验失败；`429` 限流；`409` 幂等冲突（同键并发等待超时或体不一致） |
| 版本与兼容 | v1；错误码文案属响应契约，**只允许追加错误码，禁止改义**（改义须升 v2） |
| 审计 | 成功 `Login`；触发锁定 `LoginFail` |

#### API-03 刷新　`POST /api/v1/auth/refresh`

| 项 | 内容 |
| --- | --- |
| 关联 | MOD-01；FR-02-1（规范 3.4）、FR-10-1、AC-05 |
| 认证 | `[AllowAnonymous]` + **Cookie 内的 refresh token**（裸 axios 调用，不带拦截器；前端 `withCredentials`） |
| 请求体 | 空 |
| 响应数据 | `{ accessToken, expiresIn, user: { id, userName } }` + 轮换后的 `Set-Cookie`；`user` 为 2026-09-17 **兼容性新增**（见「版本与兼容」）；响应体**不含** refresh token 明文（仅 Cookie） |
| 成功语义 | 一次性轮换（旧 token 立即失效）；复用检测命中 → 该用户全部会话失效 + 审计 `RefreshReuse`；同时返回 `user`，供前端在内存态为空时补全会话（§2.8 静默恢复） |
| 错误语义 | `1203` refresh 无效/复用（HTTP 200，前端**必须自判 `response.data.code`**——业务异常是 200，拦截器错误分支进不去）；`1204` refresh 过期（HTTP 200）；两者触发前端「引导重新登录 + 回跳」 |
| 版本与兼容 | **v1 不升版**；新增响应字段 `user` 属 §5.1 第 2 条「新增可选响应字段」（向后兼容）；**前端恢复路径可依赖的字段集** = `accessToken` / `expiresIn` / `user.id` / `user.userName`；该字段缺失（旧后端）时前端按恢复失败处理 → 引导登录（不得白屏）。`1203/1204` 为规范 3.4 已引用的既定码（已于 2026-09-17 随 §5.3 一并登记进 `docs/error-codes.md`） |
| 前端契约 | 并发刷新去重（在途 Promise 复用）、每请求只重放一次（`_retry` 标记）、刷新失败清理本地态（规范 3.4 四坑清单）；**受保护路由的会话恢复（§2.8）必须复用同一在途刷新 Promise，禁止两条刷新路径各自发请求** |

#### API-04 登出　`POST /api/v1/auth/logout`

| 项 | 内容 |
| --- | --- |
| 关联 | MOD-01 / MOD-06；FR-02、S「登出后回到登录页」 |
| 认证 | 需登录（access token）；携带 refresh Cookie |
| 响应数据 | `null` |
| 成功语义 | 删除 Redis refresh 键 + 下发过期 Cookie；前端清空 access token 与用户态 → `/login` |
| 错误语义 | `401`（access 已失效也允许登出成功？**决策**：登出接口对无效 access 仍返回成功（幂等），避免「已登出但前端卡死」；实现为 `[AllowAnonymous]` + 仅按 Cookie 清理） |
| 版本与兼容 | v1 |
| 审计 | `Logout` |

> API-04 说明：登出不使用 401 拒绝，是为了满足 FR-02 的「登出后回到登录页」——前端在 `finally` 中一律清态跳转，不依赖错误分支。

#### API-05 奖池查询　`GET /api/v1/prizes`

| 项 | 内容 |
| --- | --- |
| 关联 | MOD-02；FR-03、FR-11、AC-06 |
| 认证 | 需登录（未登录 → 401） |
| 请求参数 | 无（不接受任何筛选参数） |
| 响应数据 | `{ items: [{ id, name, shortName, type, displayOrder }] }`，按 `displayOrder` 升序；**结构上不含 weight / stock / code / isEnabled**（D-07） |
| 业务语义 | 只含 `IsEnabled && !IsDeleted`；库存为 0 的条目**仍返回**（扇区形态不变，FR-03-1/2）；无分页（奖池规模有界，默认 5 条，上限按业务约定 ≤ 20） |
| 错误语义 | `401`；`500` |
| 版本与兼容 | v1；新增字段为兼容变更（前端必须容忍未知字段）；**删除 weight/stock 的"不存在"是硬契约**——任何未来版本若返回权重/库存，属破坏性变更（信息暴露），须显式评审 |
| 前端契约 | 失败 → `page-draw--error`；返回空数组 → `page-draw--empty`（§2.6 #11） |

#### API-06 剩余次数查询　`GET /api/v1/draw/quota`

| 项 | 内容 |
| --- | --- |
| 关联 | MOD-03 / MOD-07；FR-04、FR-07、AC-07、AC-11 |
| 认证 | 需登录 |
| 请求参数 | 无 |
| 响应数据 | `{ remainingAttempts: number, dailyLimit: number, resetAt: string }`（`resetAt` = 次日 00:00 UTC+8 的 ISO 8601 UTC 时刻，供 Should-S2 的「明日 0 点重置」引导） |
| 业务语义 | `remainingAttempts = dailyLimit − 当日 UsedCount`（无行 = 0 消耗）；以服务端时钟与 UTC+8 判定（FR-05-R7）；每次进入抽奖页与每次抽奖后调用（FR-07-2） |
| 错误语义 | `401`；`500` |
| 版本与兼容 | v1；字段只增不改 |
| 前端契约 | **次数未知不得视为 0**（REV-01 口径）：首载失败时抽奖按钮保持可用、不展示 `page-draw--noquota`、`draw-quota` 以占位「—」呈现；恢复通道 = 进入页面重拉 / 抽奖响应回写（§2.6 补充约定） |

#### API-07 执行抽奖　`POST /api/v1/draw`

| 项 | 内容 |
| --- | --- |
| 关联 | MOD-03 / MOD-05 / MOD-06 / MOD-07；FR-04、FR-05、FR-07、FR-08、AC-08~AC-17 |
| 认证 | 需登录；限流 10 次/分钟/用户（Should-S3，拒绝 → 429 + `1001`） |
| 请求头 | `Idempotency-Key: {uuid}`（**缺失 = 无幂等，正常消耗**，FR-08-4；格式非法 → `1002`） |
| 请求体 | **空对象 `{}`**：不存在任何影响结果的业务参数（FR-05-R1）；若有额外字段一律忽略（防未来误用）——结果只能由后端判定 |
| 响应数据 | `{ itemId: number, isWin: boolean, remainingAttempts: number }`（FR-05-R10：结果条目标识 + 最新剩余次数） |
| 成功语义 | 完整事务流程见 D-08；返回的 `itemId` 是前端落点的唯一依据（FR-06） |
| 错误语义 | `1501` 次数已用完（HTTP 200，前端 → `page-draw--noquota`；**1501 是「次数未知」时的权威确认** —— 前端收到后置 `remaining = 0` 并展示锚点 12，兜底修正首载失败）；`1502` 候选集为空（HTTP 200，前端 → `page-draw--empty`）；`1001` 限流/依赖不可用（HTTP 429/200，**含 D-08 瞬时错误整事务重试耗尽的终态**，前端 → `page-draw--drawfail` 文案「系统繁忙，请稍后重试」）；`409` 幂等冲突（同 key 并发等待超时或体不一致）；`401`；`500`（前端 → `page-draw--drawfail`，重试沿用同一幂等键，AC-17） |
| 重复请求语义 | 同 key 重复（含并发）：重放首次结果，`code=0`，**不再消耗次数**（AC-12）；重放来源见 D-03 |
| 版本与兼容 | v1。结果字段集固定；若 v2 需要「奖品说明/图片」等附加信息，以**新增可选字段**方式在 v1 追加；改变 `itemId` 语义（如改为 code 字符串）= 破坏性变更 → 升 v2 |
| 审计 | 每次成功抽奖 1 条 `Draw`（用户、时间、结果条目、消耗次数，FR-05-R8） |

#### API-08 我的中奖记录　`GET /api/v1/records`

| 项 | 内容 |
| --- | --- |
| 关联 | MOD-04；FR-09、AC-15 |
| 认证 | 需登录；**不接受 userId 参数**（越权隔离，FR-09-1） |
| 请求参数 | `pageIndex`（从 1）、`pageSize`（默认 10、上限 100）；**排序固定为 CreateTime 倒序**（不开放 `sortField` / `sortOrder`：页面需求固定，开放排序会让 `(UserId, CreateTime)` 索引失效——`PageQuery` 基类保留字段但接口层白名单为空、传入即忽略并记 debug 日志） |
| 响应数据 | `PageResult<{ id, prizeName, winTime }>`；`winTime` = ISO 8601（UTC），前端按 UTC+8 展示 `yyyy-MM-dd HH:mm` |
| 业务语义 | 恒定当前用户；不含未中奖记录（FR-09）；每页默认 10（Q7） |
| 错误语义 | `401`；`1002`（pageSize/pageIndex 非法时按规范取整，不报错：pageSize > 100 → 按 100，pageIndex < 1 → 按 1）；`500` |
| 版本与兼容 | v1；分页结构 `PageResult<T>` 全局统一，字段只增不改；新增列（如「奖品类型」）为兼容变更 |
| 前端契约 | TanStack Table 服务端分页；页码 ±1 转换只在 `usePagination.ts`；四态见 §2.6 #17–20 |

### 5.3 错误码登记表（**已于 2026-09-17 登记进 `docs/error-codes.md`**）

> 本表 7 个错误码已于 **2026-09-17** 由主对话全部登记进 **`docs/error-codes.md`**（含 `15xx` 抽奖模块码段的申请，并补录历史欠账 `1203` / `1204`，来源标注「规范 3.4 引用」）。**`docs/error-codes.md` 为权威来源**：新增业务错误码必须先查阅它再取号，两者如有不一致**以其为准**，engineer 不得仅凭本表取号。语义遵循 `ApiResult` 约定（`code=0` 成功、`≥1000` 业务异常且 HTTP 200）。

| 码 | 模块 | 含义（用户可见文案） | 触发场景 | HTTP | 关联 |
| --- | --- | --- | --- | --- | --- |
| 1101 | 11xx 用户 | 用户名已被占用 | 注册时用户名重复（含大小写变体，FR-01-1） | 200 | FR-01、AC-02 |
| 1102 | 11xx 用户 | 用户名或密码错误 | 登录失败（统一文案防枚举，FR-02） | 200 | FR-02、AC-04 |
| 1103 | 11xx 用户 | 账号已锁定，请 15 分钟后再试 | 连续 5 次登录失败（规范 4.3；对不存在用户名同样计数） | 200 | FR-02 |
| 1203 | 12xx 权限 | 登录状态已失效，请重新登录 | refresh 无效 / 被复用（规范 3.4 已引用该码，本表仅登记语义） | 200 | FR-10-1、AC-05 |
| 1204 | 12xx 权限 | 登录状态已过期，请重新登录 | refresh 过期（规范 3.4 已引用） | 200 | FR-10-1、AC-05 |
| 1501 | 15xx 抽奖（新模块） | 今日抽奖次数已用完，明日 0 点重置 | 当日 UsedCount ≥ 3 后再次抽奖（含绕过前端直调） | 200 | FR-04、FR-07、AC-10 |
| 1502 | 15xx 抽奖（新模块） | 奖品已抽完，请稍后再来 | 候选集为空（默认奖池含常驻「谢谢参与」，正常不触发） | 200 | FR-05-R3 |

复用既有码（不新增）：`1001` 限流/依赖不可用的降级提示（error-codes.md 使用边界原文）；`1002` FluentValidation 业务校验失败；`409` 幂等冲突（规范 6.4/6.5）；`400/401/403/500` 与 HTTP 对齐。
注：`1203/1204` 原先仅出现在规范 3.4 正文、未登记在 error-codes.md；已于 2026-09-17 随本次登记**一并补录**（来源标注「规范 3.4 引用」）。

### 5.4 幂等跨接口约定（前端行为契约 + 注册 / 登录的服务端语义）

| 场景 | 约定 |
| --- | --- |
| 生成时机 | 每次**新的**抽奖尝试生成 `crypto.randomUUID()`（`utils/idempotency.ts` 唯一出口）；成功后清空；新尝试重新生成 |
| 重试沿用 | 抽奖失败（网络/超时/5xx/`1001`）后的重试**沿用同一 key**（FR-08-4、AC-17）；`page-draw--drawfail` 的重试按钮不换 key |
| 成功后重试 | 已成功（Dialog 已展示）不提供重试入口 |
| 注册 / 登录（v3 补齐） | 表单挂载时生成一次 `crypto.randomUUID()`（同一出口）；**收到明确业务结果**（成功，或 `1002` / `1101` / `1102` / `1103` 业务拒绝）后重建；仅在「未获得业务结果」的重试（网络错误 / 超时 / `429` / `5xx`）沿用同键 |
| 服务端 | 抽奖见 D-03；注册 / 登录见 D-15；`409` 语义 = 「请勿重复提交」，前端在 `utils/error.ts` 统一映射 |

> **注册 / 登录幂等的服务端语义（v3 补齐，Q2 裁决）**：作用域 `(operation, key)`，`operation ∈ {register, login}`；请求体哈希不一致 → `409`；键缺失 = 无幂等，正常处理（不报错）。实现复用 `IIdempotencyStore`（Redis；key `draw:idempotency:auth:{operation}:{key}`，TTL 10 分钟，`SET NX` 占位 + ≤ 2s 短轮询），**不新增表、不复用 `DrawRequest`**；缓存**不含任何凭证**，重放时重新签发 access / refresh；同一逻辑提交仅一次业务副作用（建用户 / 审计 / 失败计数不重复）。完整决策见 **D-15**。

---

## 6. 风险清单

| 编号 | 类型 | 风险 | 缓解措施 | 验证方式 |
| --- | --- | --- | --- | --- |
| RSK-01 | 依赖 | EF Core 10 + MySQL provider 适配风险（Pomelo 无 EF Core 10 版；规范 2.2 明确 Microting 分支，且警告「编译通过不能作为选型依据」） | 立项第一步先做「生成迁移 + 真连库查询」实测（规范 2.2 硬要求）；若 Microting 10.0.11 不达标 → 按规范回迁条件评估（禁止跨大版本混用） | 迁移落库 + 一次真实查询；失败即上报，不静默换包 |
| RSK-02 | 技术 | 并发超发 / 超扣次数（FR-08、AC-12、AC-13） | D-01 条件 UPDATE + 影响行数；D-02 唯一索引 + 条件扣次；D-08 固定锁序 + RC + 死锁重试；三方同事务回滚 | 集成测试：多任务并发同一奖品 / 同一用户；断言库存 ≥ 0、UsedCount ≤ 3、记录数一致 |
| RSK-03 | 技术 | 幂等缓存（Redis）与 DB 底线不一致 | Redis 仅作重放加速，正确性全部由 `DrawRequest` 唯一索引兜底（D-03）；缓存写失败仅告警 | 集成测试：写入后 flush Redis，同 key 重试仍返回首次结果且不重复扣次 |
| RSK-04 | 部署 | 测试确定性配置泄漏生产（PRD R5） | D-06 启动强校验：非 Development/Testing 且存在确定性配置 → 应用拒绝启动；生产基线 `appsettings.json` 不含该节 | 附录 B 部署验收项：以生产配置启动一次，确认启动失败/配置为空；CI 增加配置文件扫描（grep `Deterministic`） |
| RSK-05 | 技术 | 跨日重置依赖真实时间，测试难构造（PRD R3） | `TimeProvider` 注入 + 固定 UTC+8 偏移（D-05）；日切 = 换行，无定时任务 | 单测/集成：Fake 时钟推进到次日 00:00（AC-11） |
| RSK-06 | 技术 | 前端落点与后端结果不一致（PRD R4） | D-14：落点由返回 `itemId` 反算角度，动画纯表现；`transitionend` 后才弹 Dialog | E2E：中奖链路断言 Dialog 奖品名与扇区高亮编号一致（AC-08/09） |
| RSK-07 | 技术/工期 | 从零搭建（PRD R6）：本仓库无任何应用代码，前后端基础设施工程量高于常规迭代 | 范围锁死 MVP（Won't 清单）；复用规范既定约定（JWT、request 封装、错误码、分页）；目录骨架见 §2.5；建议实现顺序：后端骨架 + 认证 → 奖池/抽奖 → 记录 → 前端基础设施 → 页面 | code-reviewer 按 §2.5/§5 对照；集成测试覆盖 4 个控制器 |
| RSK-08 | 环境 | 集成测试依赖本地 Docker（Testcontainers）；环境缺失会卡住交付 | 规范 7.1 两条路线：方案一 Testcontainers（需 Docker）；方案二 `WebApplicationFactory<Program>` + 独立库名/独立 Redis db（须走环境变量覆盖，禁用 `ConfigureAppConfiguration`） | 跑完核对测试库真实存在且被写过（规范 7.1 硬要求） |
| RSK-09 | 环境 | E2E 需前后端同时可用，且「库存-1 / 权重覆盖」无 API 面可断言 | E2E 夹具直连**测试库**做数据准备与断言（仅限测试环境）；确定性命中链路走 `Draw:Deterministic`（D-06）；库存扣减类断言在集成测试完成（engineer 侧） | test-executor 按 §2.6 `data-testid` 编写；`tests/e2e` 独立 docker-compose.test 约定 |
| RSK-10 | 依赖 | shadcn-vue CLI 组件缺失或生成差异 | 固定 `npx shadcn-vue@2.x add`（禁止 @latest）；`pagination` 若 CLI 无稳定产出 → 用 Button + 条件渲染自建（业务组件，不属 ui/）；空态用 `components/business/EmptyState.vue`（原型映射表已定） | 添加后立即跑 `npm run build`；`components/ui/` 只读红线由 code-reviewer 核对 |
| RSK-11 | 技术 | 抽奖审计同事务导致「审计失败 = 抽奖失败」（比规范第十章更严格） | 有意为之（D-08）；同库同事务下审计独立失败概率极低 | 已确认（A2，2026-09-17 用户采用架构师默认假设）；若后续要求改异步，改为独立写入 + 告警 |
| RSK-12 | 契约 | 密码强度口径冲突：PRD FR-01-2（8–20 位含字母与数字）异于规范 8.1（含大小写字母 + 数字） | **已关闭**：2026-09-17 用户在第 3 步 `/arch` 评审门裁决**按规范 8.1 口径收紧**，并以 `docs/00-brief.md` **CR-01** 固定边界（字符类别收紧为「大写 + 小写 + 数字」；长度 8–20 位不变；不引入首次登录强制改密）；已落地为 API-01 的 DTO / FluentValidation 与前端 Zod Schema（§5.2、附录 A 第 10 条），上游 `10-prd` v2 / `20-prototype` v3 同步完成 | 复核项：code-reviewer 核对前后端规则文本逐字一致、与原型 v3 文案一致（FR-01-2 / AC-01 / AC-02） |
| RSK-13 | 安全 | 登出后 access token 在 2h 有效期内仍可用（无黑名单） | 已知取舍（D-11）；refresh 已即时失效，最长残留 = access 剩余寿命 | 已确认（A6，2026-09-17 用户采用架构师默认假设）；后续如需即时踢线加 Redis 黑名单 |
| RSK-14 | 体验 | 记录页「翻页至末页之后」的空态判定（原型 #20 触发条件含该场景） | 前端在请求返回 `totalCount` 后，若 `pageIndex > 总页数` 则回退一页重查；避免「空态 + 有记录」矛盾 | E2E 边界用例由 test-designer 覆盖 |
| RSK-15 | 技术 | 会话恢复与 refresh 一次性轮换的交互（v3 新增）：①并发刷新（恢复路径与 401 路径各发一次）会触发 D-11 严格复用检测 → `1203` 全端登出；②网络错误下「服务端已轮换、响应丢失」→ 下次携旧 Cookie 同样触发 `1203` | §2.8：恢复与 401 无感刷新**共用同一在途刷新 Promise**；失败不自动重试（仅用户再次导航可再试一次）；②的后果与「不重试」等价（都需重新登录），接受并不设计宽限窗口 | 单测：并发进入受保护路由 → 仅一次 refresh；E2E：新标签页直开受保护路由可恢复放行（REV-02 回归）；code-reviewer 复核两条刷新路径已合并 |

### 6.1 依赖降级矩阵（Redis 不可用时的行为，避免单点故障演化为全站不可用）

| 链路 | Redis 不可用时的行为 | 是否可用 |
| --- | --- | --- |
| 抽奖判定与扣减 | 完全走 MySQL（幂等退化为 DB 唯一索引路径，符合规范 6.5 兜底） | 可用 |
| 剩余次数 / 记录查询 | 不依赖 Redis | 可用 |
| 登录 / 注册 | 失败计数不可用 → 锁定功能降级（fail-open + warning 日志），登录本身不阻断 | 可用 |
| 无感刷新 | refresh 会话状态缺失 → 刷新失败（1203/1204）→ 引导重新登录 | 降级（会话重置） |
| 幂等重放 | 退化为 DB 路径，重复请求仍不重复扣次 | 可用 |

结论：**Redis 不是抽奖主链路的强依赖**，这是 D-02/D-03 把正确性放在 MySQL 的直接收益。

---

## 7. 技术选型对比表（方案层面）

> 技术栈（Vue 3 / .NET 10 / EF Core / MySQL / Redis / shadcn-vue…）为既定约束，不在本表内；本表只对**实现方案**做取舍。每行结论对应第 3 章的决策编号。

| # | 决策点 | 候选方案 | 优点 | 缺点 | 适用场景 | 结论（本文采纳） |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | 库存并发扣减 | ① 条件 UPDATE + 影响行数　② 乐观并发 rowversion + 重试　③ 悲观锁 FOR UPDATE　④ Redis 原子扣减 + 异步落库 | ① 单语句原子、无重试成本、DB 为唯一权威 | ① 需处理「影响 0 行」的重抽分支 | ① 严格防超发 + 低延迟（本项目） | **①**（D-01） |
| 2 | 每日次数存储 | ① 按日聚合行（UserId, DrawDate）　② 累计行 + 日戳 CASE　③ Redis 计数 | ① 重置零代码、与库存同事务 | ① 行数随用户×天数增长（可清理） | ① 需可测试、可断言的限额 | **①**（D-02） |
| 3 | 幂等实现 | ① Application 服务 + DB 唯一索引 + Redis 结果缓存　② 规范字面（Redis SET NX PENDING + 轮询）　③ 纯 Redis　④ 中间件统一幂等 | ① 并发同 key 强一致重放、实现最简 | ① 与规范 6.5 文字有细化偏离（需评审） | ① 事务型提交接口（抽奖） | **①**（D-03） |
| 4 | 随机数与权重 | ① 整数权重 + 累积区间 + `RandomNumberGenerator`　② `Random.Shared`　③ Alias Method　④ 浮点权重 | ① 概率精确、不可预测、O(n) 成本可忽略 | ① 每次请求需实时读候选集（有意为之） | ① 可审计的抽奖类判定 | **①**（D-04） |
| 5 | 时钟与随机源抽象 | ① BCL `TimeProvider` + 自写 Fake　② 自建 `IClock`　③ 引入 FakeTimeProvider 包 | ① 零新增依赖、BCL 标准 | ① Fake 需自写（3 行） | ① 需构造跨日/确定性用例 | **①**（D-05） |
| 6 | 确定性配置防泄漏 | ① 启动强校验 fail fast　② 配置文件分层约定　③ 编译期 `#if`　④ 独立测试构建 | ① 泄漏即启动失败，机械保证 | ① 生产误配会「起不来」（这正是目的） | ① PRD R5 部署验收项 | **①**（D-06） |
| 7 | 奖池下发 | ① Select 白名单 DTO　② Entity + `[JsonIgnore]`　③ 自定义转换器 | ① 字段不存在即不可泄漏 | ① 多一个 DTO 类 | ① 含敏感内部字段的查询接口 | **①**（D-07） |
| 8 | 事务隔离与锁序 | ① RC + 固定锁序 + 重抽≤3 轮　② 默认 RR + FOR UPDATE 重读　③ Serializable | ① 重抽可读到最新库存、死锁可控 | ① 需显式 `BeginTransactionAsync(ReadCommitted)` 与顺序纪律 | ① 多行竞争写（次数/库存/记录） | **①**（D-08） |
| 9 | 奖池初始化 | ① EF 迁移 `HasData`　② 启动 Seeder　③ 手工 SQL | ① 幂等、可版本化、无启动副作用 | ① 改种子需新迁移（本期无管理端，可接受） | ① 固定初始化数据（FR-11） | **①**（D-09） |
| 10 | 记录名称 | ① 冗余快照 `PrizeName`　② 仅 FK join | ① 历史记录稳定、列表免 join | ① 储存冗余（可接受） | ① 不可变的历史记录 | **①**（D-10） |
| 11 | refresh 会话 | ① Redis 单会话族 + 哈希　② DB 表（多设备）　③ 无状态 refresh | ① 满足一次性轮换 + 复用检测、存储面最小 | ① Redis 丢失 = 全端重登；② 单设备 | ① 本期单设备 MVP | **①**（D-11） |
| 12 | 登录失败语义 | ① HTTP 200 + 1102/1103　② HTTP 401 | ① 不误触发无感刷新拦截器 | ① 不符合纯 REST 语义直觉 | ① 有 401 刷新机制的前端 | **①**（D-12） |
| 13 | 转盘动画 | ① CSS transition + 目标角　② GSAP/anime.js　③ Web Animations API | ① 零依赖、与 `motion-reduce:` 体系一致 | ① 需自算角度与事件兜底 | ① 单一转盘动效 | **①**（D-14） |
| 14 | 目录与测试分区 | ① `src/backend` + `src/frontend` + `tests/{unit,integration,e2e}`　② 顶层 `frontend/`+`backend/`　③ SPA 内嵌 `wwwroot` | ① 与 artifacts 登记的写入范围一致 | ① 前端出现 `src/frontend/src` 嵌套 | ① 前后端分离 + 多角色流水线 | **①**（D-13） |
| 15 | 注册 / 登录幂等（v3） | ① 复用 `IIdempotencyStore`（Redis，键空间隔离）　② 新增 DB 通用幂等表　③ 复用 `DrawRequest` 表　④ 仅前端防重复 | ① 零新增表 / 迁移，键空间与抽奖隔离 | ① Redis 丢失后退化为「注册 1101 / 登录重登」，无数据不一致 | ① 无 DB 不变量诉求的认证接口 | **①**（D-15） |

---

## 8. 性能与成本预算（可验收口径）

> 对齐 PRD G4 与 `docs/development-spec.md` 第十四章；「硬性」= 规范/PRD 已有卡点，「本功能目标」= 本方案内部预算（低于硬性上限，留出回归余量）。

### 8.1 前端

| 指标 | 硬性 | 本功能目标 | 校验方式 |
| --- | --- | --- | --- |
| 主 chunk（gzip） | ≤ 500KB（规范 14） | 首屏公共 chunk ≤ 250KB | `vite-bundle-visualizer` + CI 卡点 |
| 路由 chunk | 全部 views 懒加载（强制） | DrawView / RecordsView 各自 ≤ 60KB gzip | 构建产物报告 |
| 转盘图形（内联 SVG + 动画 CSS） | — | ≤ 10KB，且不引入图片 / 字体 / 图标库外链 | 构建产物检查 |
| LCP | ≤ 2.5s（规范 14） | 抽奖页桌面 ≤ 1.8s（本地 Lighthouse 中位数） | Lighthouse |
| 点击「抽奖」→ 按钮禁用反馈 | — | ≤ 100ms（同步置态，不等网络） | E2E/手测 |
| 列表一次渲染 DOM 行数 | 分页强制 | ≤ 10 行/页 | 代码核对 |

### 8.2 后端接口

| 接口 | 硬性（PRD/规范） | 内部预算分解 | 目标 P95 |
| --- | --- | --- | --- |
| `POST /api/v1/draw` | P95 ≤ 500ms（G4） | 鉴权+限流 ≤ 15ms + Redis GET ≤ 5ms + DB 事务（≤5 写 / ≤3 读）≤ 100ms + 序列化/框架 ≤ 30ms | **≤ 200ms** |
| `GET /api/v1/records` | ≤ 300ms（PRD 4.1） | 1 次 count + 1 次分页查询，索引 `(UserId, CreateTime)` 覆盖 | **≤ 150ms** |
| `GET /api/v1/prizes` | — | 单表查询 ≤ 20 行 | **≤ 80ms** |
| `GET /api/v1/draw/quota` | — | 单行等值查询 | **≤ 50ms** |
| `GET /api/v1/auth/*` | — | BCrypt 哈希为 CPU 主导项（登录/注册 ≤ 300ms 可接受） | ≤ 350ms |

资源用量预算（单次抽奖）：MySQL 写行数 **≤ 5**（DrawRequest、Quota、Stock、Record、AuditLog），读查询 ≤ 3；Redis 命令 **≤ 2**（GET + SETEX；纯重放路径 = 1 次 GET）；无任何外部服务调用。
并发基准（集成测试，单机）：50 并发同奖品抽奖 → 库存 ≥ 0 且 `中奖记录数 = 实扣库存数`；同用户并发抽奖 → `UsedCount ≤ 3`。

### 8.3 数据与基础设施成本上限

| 项 | 预算 | 说明 |
| --- | --- | --- |
| DrawRequest 行数 | ≤ 3 行 × DAU / 日，保留 30 天（清理任务 v2） | 10k DAU 假设下 ≤ 90 万行 |
| WinningRecord 行数 | ≤ 3 行 × DAU / 日 | 与抽奖同量级 |
| Redis 内存 | ≤ 512MB（全部 key 带 TTL，`volatile-lru`） | 幂等结果 24h + refresh 7d |
| 容器配额 | MySQL 1C/1G、Redis 0.5C/512M、API 1C/512M | Docker Compose |
| CI 时长 | 单次流水线 ≤ 10 分钟（build + lint + 单测 + 集成测试） | 超时视为性能回归 |
| 统计验证成本 | 10000 次抽样（内存级判定函数）≤ 2s | AC-16 的统计断言基准 |
| 外部付费依赖 / Token 成本 | **0**：无 LLM 调用、无第三方计费 API | 「Token 成本上限」不适用，登记以免下游误判 |

---

## 附录 A：前端实现要点清单（engineer 落地自检）

1. `stores/auth.ts` 的会话态（access token + 用户信息）**仅存内存**（Pinia state；禁止 localStorage / sessionStorage，任何浏览器持久化一律禁止；规范 3.3 v3 口径）；登出同步清空；refresh 走 httpOnly Cookie，**禁止**在前端任何位置持久化；页面刷新 / 新标签页进入**受保护路由**时按 §2.8 静默恢复（公开页不触发）。
2. `utils/request.ts`：baseURL 携带 `/api`（`api/` 内路径写 `/v1/...`）；成败判定看 `code` 而非 HTTP 状态码；401 无感刷新四件套（并发去重、只重放一次、裸 axios 发刷新、刷新函数自判 `response.data.code`）逐条落实（规范 3.4）。
3. 错误边界：`app.config.errorHandler` + 路由级 `onErrorCaptured` 降级 UI（规范 3.7）；抽奖/记录错误**只用页面内 Alert**，不引入 Toast（避免双报，原型映射表已定）。
4. 文案全部经 `utils/messages.ts`（键值集中，为 i18n 预留，PRD 4.5-1）；组件内禁止硬编码用户可见中文。
5. 幂等键：`utils/idempotency.ts` 为唯一出口；失败重试沿用、成功后重建（§5.4）。
6. 分页：`usePagination.ts` 收敛 TanStack `pageIndex`(0 起) ↔ 接口 `pageIndex`(1 起) 的 ±1（规范 6.2 警告）。
7. 抽奖页状态机（对齐 §2.6）：`poolLoading → ready | poolError | poolEmpty`；`ready → drawing（请求+动画）→ dialog(win|lose) → ready(刷新 quota)`；任一步失败 → `drawFail`（重试沿用同 key）；`quota = 0 → noquota`（按钮真实 disabled）；**`quota` 未知（`remaining` 初值 `null`，首载失败）→ 不进入 `noquota`、按钮保持可用、徽标占位「—」**，由抽奖响应 / 页面重进兜底修正（REV-01，见 §2.6 补充约定）。
8. 可访问性：Dialog 焦点陷阱由 shadcn-vue 承担；次数变化 `aria-live="polite"`；装饰性 SVG `aria-hidden="true" focusable="false"`；动效统一 `motion-reduce:` 降级。
9. 所有 views 路由懒加载；无 `v-html`；无新增独立 CSS；`components/ui/` 只读。
10. **密码规则单一事实来源（CR-01 落点）**：注册密码规则为「**8–20 位，须同时包含大写字母、小写字母与数字**」。前端 `types/auth.ts` 的 Zod Schema 与后端 DTO 的 DataAnnotations / FluentValidation **规则文本逐字一致**（任一字符类别缺失即失败）；提示文案键集中放 `utils/messages.ts`，与原型 v3 `reg-password-hint`「8-20 位，须同时包含大写字母、小写字母与数字」一致。禁止：只在前端校验（后端必须独立校验）、用宽松的「含字母与数字」正则（未收紧到大小写）、依赖后端返回才提示而前端的本地校验缺失。

## 附录 B：部署与配置验收项（含 PRD R5 卡点）

| # | 验收项 | 判据 |
| --- | --- | --- |
| B1 | 确定性配置不可达（R5） | 以生产配置启动：`Draw:Deterministic:Enabled=false` 且覆盖项为空；若注入任意确定性配置 → 应用启动失败（D-06）。CI grep 扫描 `appsettings*.json` |
| B2 | 迁移先于应用启动 | 部署脚本顺序：`dotnet ef database update`（或 SQL 脚本）→ 启动 API（规范 5.4） |
| B3 | 反代 IP 还原 | `UseForwardedHeaders` 排在 `UseRateLimiter` **之前**；清空可信代理用 `KnownNetworks.Clear()` / `KnownProxies.Clear()`（不是集合初始化器）（规范 8.6） |
| B4 | CORS 白名单 | `Cors:AllowedOrigins` 显式域名，禁止 `AllowAnyOrigin`；安全响应头（nosniff / DENY / CSP） |
| B5 | Cookie 属性 | 生产：refresh Cookie `httpOnly + Secure + SameSite=Strict + Path=/api/v1/auth`；本地开发允许 `Secure=false`（环境配置切换） |
| B6 | 密钥不入库入仓 | `Jwt:SigningKey`、DB/Redis 连接串走 `dotnet user-secrets`（开发）/ 环境变量（生产）（规范 8.4） |
| B7 | 环境变量清单 | `ConnectionStrings__Default`、`Redis__Configuration`、`Jwt__Issuer/Audience/SigningKey`、`Cors__AllowedOrigins`、`Draw__DailyLimit` |
| B8 | 审计与日志红线 | 日志不含密码 / token / 密钥；抽奖审计条目含用户、时间、结果、消耗次数（FR-05-R8） |

## 假设与裁决清单（A1–A7，**全部已闭环**，2026-09-17）

> v1 冻结时本节为「待确认清单」。**2026-09-17 用户在第 3 步 `/arch` 评审门逐项裁决**：**A1 改判**——由 v1 的「PRD 口径」改为**规范 8.1 口径收紧**，已发 `docs/00-brief.md` **CR-01** 固定裁决边界，并同步上游 `10-prd` v2 / `20-prototype` v3；**A2–A6 一律采用架构师默认假设**（用户原话「其他冲突采用架构师默认假设」），不产生 CR，技术内容未变，仅补状态。
>
> 因此本节现为**已闭环的假设台账**：A2–A6 的「若假设不成立的影响」列保留为**回退预案**（仅在后续版本触发时参考）；A1 的落地项已执行完毕。**v3 追加 A7**（`docs/60-review.md` §8 Q1 的用户裁决）；同轮 Q2 / Q3 的裁决登记见文首「v3 变更说明」（Q2 落为 D-15 / §5.4，Q3 经核对不产生契约变更）。

| 编号 | 缺失信息 | 状态（2026-09-17） | 结论：采用的假设 / 裁决结果 | 若假设不成立的影响（回退预案） |
| --- | --- | --- | --- | --- |
| A1 | 密码强度口径：PRD FR-01-2「8–20 位含字母与数字」与规范 8.1「含大小写字母 + 数字」不一致 | **已裁决**（用户裁决：按规范 8.1 口径，涉及其他成果一并修改） | **按规范 8.1 口径收紧**：密码须**同时包含大写字母、小写字母与数字**；长度保持 **8–20 位不变**；**不引入**「首次登录强制改密」。裁决边界见 `docs/00-brief.md` **CR-01** | 落地项已执行：前端 `types/auth.ts` 的 Zod Schema、后端 DTO / FluentValidation（§5.2 API-01）、文案清单（原型 v3）、AC-01 / AC-02（PRD v2）；回退至 PRD v1 口径需再发 CR 并重跑 20 / 30 / 50 |
| A2 | 抽奖审计写入失败是否允许不阻断主流程（规范第十章「不阻断」vs 本方案同事务） | **已确认**（2026-09-17，用户采用架构师默认假设） | 抽奖审计**同事务**：宁可拒绝也不留无痕操作（D-08） | 若要求不阻断：抽奖审计改独立写入 + 告警，接受极小的「抽奖成功但无审计」窗口 |
| A3 | 缓存 key 项目前缀 | **已确认**（2026-09-17，用户采用架构师默认假设） | 统一前缀 `draw`（如 `draw:idempotency:{userId}:{key}`） | 若项目前缀有既有约定，需全量替换 key 前缀与文档 |
| A4 | 幂等实现细化：省去规范 6.5 的 Redis PENDING 与轮询，以 DB 唯一索引给出强一致重放 | **已确认**（2026-09-17，用户采用架构师默认假设） | 采用本方案（D-03），语义与 6.5 等价 | 若必须逐字对齐规范：增加 PENDING 状态与轮询超时 409 路径，并发同 key 的「结果一致」退化为「大概率一致」 |
| A5 | FR-11-2「测试环境通过配置覆盖库存」 | **已确认**（2026-09-17，用户采用架构师默认假设） | 库存不由运行时配置覆盖（会绕过并发正确性语义）；集成测试夹具直接写测试库，权重覆盖走测试配置 | 若必须配置化：新增测试环境专用启动钩子（同时需强化 R5 防护面） |
| A6 | 登出后 access token 在 2h 内仍有效；登出接口对无效 access 返回成功 | **已确认**（2026-09-17，用户采用架构师默认假设） | 接受（无黑名单；登出以 refresh 失效为准） | 若要求即时失效：引入 Redis access 黑名单（每请求一次判断，P95 预算 +5ms） |
| A7 | access token 存储口径：v2 及旧规范 3.3 为「localStorage 持久化」，实现（CHG-09）为「仅内存 + refresh 恢复」，两者冲突（`docs/60-review.md` §8 Q1） | **已裁决**（**用户，2026-09-17**；来源 `docs/60-review.md` §8 Q1） | **保留内存方案**并同步修订规范 / 架构口径：access token 与用户信息**仅存内存**，页面刷新 / 新标签页由**受保护路由**触发一次静默恢复（`docs/development-spec.md` §3.3 已于同日改齐；落地契约见 §2.8），并**先消除 REV-02 的新标签页回归**。理由：localStorage 中一次 XSS 即可读走凭证，内存方案把凭证生命周期限制在当前页面上下文，代价是刷新后必须有一次静默恢复 | 若回退 localStorage：删除 §2.8 恢复路径、恢复「新标签页免恢复」，但重新引入 XSS 可读写面；需与规范 3.3 同步改回口径，并重跑 30 / 40 / 60 与前端测试 |

---

写入范围声明：本文件为 software-architect 唯一产物 `docs/30-architecture.md`（**v4**）。上游 `docs/00-brief.md` v2、`docs/10-prd.md` v2、`docs/20-prototype.html` v3、`docs/development-spec.md`（§3.3 新口径）、`docs/error-codes.md`、`docs/60-review.md` v1 均只读，未修改；未创建或修改任何 `src/`、`tests/` 内容；未改动任何上游契约文件；未执行 git commit。
上游变更时按 `docs/artifacts.md` 第 5 节失效传播矩阵重出本产物并升版本。**本次 30（v2 → v3）的下游影响**（40-changelog、50-testcases、51-defects、52-qa-report、60-review、tests/unit/**、tests/integration/**、tests/e2e/**）已在文首「v3 变更说明」的失效传播表中逐项列明，下游接口人按该表复核。**本次 30（v3 → v4）的下游影响**（范围同 v3：40-changelog、50-testcases、51-defects、52-qa-report、60-review、tests/unit/**、tests/integration/**、tests/e2e/**）已在文首「v4 变更说明」的失效传播表中逐项列明。
