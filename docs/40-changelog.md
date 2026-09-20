# 40 变更记录（Changelog）

## 元信息

| 项 | 内容 |
| --- | --- |
| 产物 | `docs/40-changelog.md`（engineer 交付的改动台账，含回滚建议） |
| 版本 | **v3**（v2 → v3：本批新增 `CHG-20`「集成夹具 Redis 覆盖值修复（`51:OBS-12`）」—— 按 `docs/artifacts.md` §4 属**正文契约内容变更** → 升版，依据与时段见「本轮变更登记（2026-09-20）」行；v1 → v2：本批新增 `CHG-19`「引用修复改动集（两角色同源 8 文件 18/18）＋ 同批护栏与规则文本加固」—— 按 `docs/artifacts.md` §4 属**正文契约内容变更** → 升版，依据与时段见下方「本轮变更登记」行。v1 冻结取值 `2026-09-17` 早于 `CHG-14`…`CHG-18` 的落盘且头部未同步，本轮按 §4「末次实际落盘时刻」口径一并订正） |
| 冻结时间 | **2026-09-20T11:22:00+08:00**（**实测**：本批（v3，`CHG-20`）末次落盘时刻取 `date` 实际输出 `2026-09-20T11:22:41+08:00` 向下取整到分钟；**证据强度 = 旁证** —— 本批尚未提交，无提交级落盘证据（`docs/artifacts.md` §4「落盘证据只认 git 提交时刻」），提交 hash **不推定**）。**上一版取值**：`2026-09-18T17:54:00+08:00`（**实测**：`date` 输出 `2026-09-18 17:54:33 +0800`，向下取整到分钟 = 本批（v2）末次落盘时刻；**证据强度 = 旁证** —— 本批尚未提交，**无提交级落盘证据**（`docs/artifacts.md` §4「落盘证据只认 git 提交时刻」），提交 hash **不推定**。v1 取值 `2026-09-17`（日粒度）见「版本」行说明） |
| 上游依赖 | `docs/30-architecture.md` **v4**、`docs/10-prd.md` v2、`docs/20-prototype.html` v3（**2026-09-18 勘误**：原值 30 **v2** 与本文正文不符——CHG-13 以 30 **v3** 为契约基准，CHG-16 / CHG-17 / CHG-18 均对齐 30 **v4** 的 D-08；按 `docs/artifacts.md` §5，30 变更时本产物是受影响下游，但本文正文**已随每次变更逐条回写**（CHG-16…18 即其落地），故**无需补失效标记**，只需订正本行） |
| 写入范围 | `src/**`、`tests/unit/**`、`tests/integration/**`、本文件 |
| 说明 | 改动逐条 ID 稳定（CHG-01…），关联 FR / AC；「测试结果」栏只在**实际执行**后填写，未执行的一律标注原因 |
| 本轮变更登记（2026-09-18） | **改了哪一类**：新增 `CHG-19`（引用修复改动集补登 ＋ 同批护栏与规则文本加固）—— 按 `docs/artifacts.md` §4 属**正文契约内容变更**；**何时**：落盘日 `2026-09-18`（具体时刻见本文件「冻结时间」行的实测读数）；**依据哪条规则**：`docs/artifacts.md` §4「冻结后仍有落盘时的处置」（正文变更 → 升版；头部元信息块同步）+ `docs/role-protocol.md` §7.4（时间戳取 `date` 实测输出、禁标称钟点）；**处置**：**升版 v1 → v2**、冻结时间同步取本批末次落盘。**范围声明（本批覆盖提交面）**：`git diff --numstat -- src tests` 实测 8 文件 / 18 增 18 删（`src/**` + `tests/unit/**` 归 engineer；`tests/e2e/**` 归 test-executor —— 同轮两角色同源；逐处清单见 CHG-19 §1）＋ 护栏与规则文本三件（`tools/check-config.py`、`docs/role-protocol.md`、`.gitignore`，由主对话落盘；见 CHG-19 §1b）；工作区其余脏文件（`docs/*`、`.claude/*` 等属其他角色）**不在本 CHG 清单内**，提交时以 `git show --stat` 逐文件核对 |
| 本轮变更登记（2026-09-20） | **改了哪一类**：新增 `CHG-20`（集成夹具 Redis 覆盖值修复，关联 `51:OBS-12`）—— 按 `docs/artifacts.md` §4 属**正文契约内容变更**（非台账类回写）；**何时**：落盘日 `2026-09-20`（实测读数见「冻结时间」行）；**依据哪条规则**：`docs/artifacts.md` §4「冻结后仍有落盘时的处置」+ `docs/role-protocol.md` §7.4（时间戳取 `date` 实测输出、禁标称钟点）；**处置**：**升版 v2 → v3**、冻结时间同步取本批末次落盘。**范围声明**：`git diff --stat -- tests/integration` 实测 2 文件 / 74 增 7 删；证据目录（绝对路径，留存不删）`D:\AI test\AI-Agents-workflow-Engineer-Rules\tests\integration\_tmp-obs12-fix-20260920-01`。 |

## 变更索引

| CHG | 层次 / 主题 | 关联 | 状态 |
| --- | --- | --- | --- |
| CHG-01 | 后端解决方案骨架与分层项目 | FR-11、MOD 划分（§2.5） | 已落地 |
| CHG-02 | Domain：实体 / 枚举 / 业务异常 | FR-01、FR-05、FR-08、FR-09 | 已落地 |
| CHG-03 | Infrastructure：DbContext / 迁移 / 种子数据 | FR-11、AC-18 | 已落地（真实库验证） |
| CHG-04 | Infrastructure：仓储并发控制（D-01 / D-02 / D-03） | FR-05、FR-08、AC-12、AC-13、AC-14 | 已落地 |
| CHG-05 | Infrastructure：缓存 / 审计 / 令牌 / 随机源 / 密码哈希 | FR-05-R8、D-04、D-11 | 已落地 |
| CHG-06 | Application：服务 / 校验器 / Options 强校验 | FR-01…FR-09、CR-01、D-06 | 已落地 |
| CHG-07 | Api：控制器 / 全局过滤器 / Program 装配 / 配置 | FR-02、FR-10、API-01…API-08 | 已落地 |
| CHG-08 | 后端测试（`tests/unit/**`、`tests/integration/**`） | AC-11、AC-12、AC-13、AC-16 | 已落地（64 + 28 全绿） |
| CHG-09 | 前端工程（`src/frontend/**`） | FR-01…FR-10、§2.6 testid | 已落地 |
| CHG-10 | 前端验证（build / lint / 单测） | 附录 A 检查清单 | 已落地（类型 0 错 / lint 0 warning / 单测 41 通过） |
| CHG-11 | 测试暴露缺陷的修复（响应包装 / 并发死锁） | D-01、D-02、D-12、FR-10 | 已修复并回归 |
| CHG-12 | 前端测试守卫：`passWithNoTests` 置 `false`（防 CI 假绿） | 附录 A 检查清单 | 已落地（6/41 通过 + 守卫 exit 1 验证） |
| CHG-13 | 60-review v1 用户裁决的 14 项 REV 修复 | REV-01…REV-09、REV-13…REV-15、REV-17、REV-19 | 已落地（14/14，逐项见本节） |
| CHG-14 | 缺陷闭环：BUG-01（认证幂等冲突 HTTP 409）、BUG-02（`winTime` 补时区标识）、BUG-03（抽奖页奖池错误态文案） | BUG-01/02/03、TC-08、TC-17、TC-63、TC-30 | 已落地（3/3，实跑验证见本节末；附 1 项非本轮引入的格式遗留） |
| CHG-15 | Infrastructure：`AppDbContext.cs` 种子块纯空白格式化（REV-20 闭环） | REV-20、CHG-03（种子来源） | 已落地（45 处 WHITESPACE → 0，格式门禁 exit 0；纯空白、零行为改变，实跑验证见本节） |
| CHG-16 | 缺陷闭环：BUG-04（结果条目不在奖池快照时重拉一次 + 兜底文案）、BUG-05（抽奖事务 ⑥⑦⑧ 瞬时错误分类 + 命令超时 60s + 重试耗尽 CRITICAL 告警） | BUG-04、BUG-05、TC-48c、TC-56b、FR-05-R10、FR-08-3、D-08、`docs/artifacts.md` §5 | 已落地（2/2，实跑验证见本节）；其上报的 **2 项契约冲突已于 CHG-17 经用户裁决闭合**（终态错误码保持 `1001`；重试次数改代码对齐） |
| CHG-17 | 用户裁决落地：重试预算 3 → 2 次尝试对齐架构 D-08（最坏等待 150s → **100s**）+ `CommandTimeout` 接线守护性断言（含 2 项负向验证） | BUG-05、D-08（架构 `:544`）、CHG-16、`docs/artifacts.md` §5 | 已落地（实跑 + 负向验证见本节；附 1 项交付残留：前端 15s 与后端最坏 100s 不匹配，裁决本轮不动） |
| CHG-18 | REV-21 闭环：重试耗尽 CRITICAL 告警的计数口径修正（**尝试次数 → 重试次数**，对齐 D-08「重试 1 次」）+ 同源根因治理的守护性断言（含 2 项负向验证） | REV-21、D-08（架构 `:567`）、CHG-16、CHG-17、`docs/artifacts.md` §5 | 已落地（实跑 + 负向验证见本节；**上游契约变更 = 无**，仅日志文案，不改错误码 / HTTP 状态码） |
| CHG-19 | 引用修复改动集（裸行号锚点 → 稳定 ID；含 `TC-39b` 幽灵编号判定）＋ 同批护栏 T30 扩面与临时目录前缀闭集双向锁、规则文本同步加固 | `60:OBS-32`、`60:OBS-05`、`docs/development-spec.md` 13.4、`D-08`、`TC-39`、`docs/artifacts.md` §5、`docs/role-protocol.md` §6 / §7.6 | 已落地（构建 0/0 + 单测 96/0/0 + 护栏 369 项全 PASS 实跑；**集成未通过：环境不具备**，见本节） |
| CHG-20 | 集成夹具 Redis 覆盖值修复（连接与清理目标同源派生 + 守护断言） | `51:OBS-12`（阻断/执行侧）、`FR-02`、`D-03` | 已落地（判别轮 43/43 / 对照轮 43/43 / 单测 96/0/0 / 护栏 373 全 PASS） |

---

## CHG-01 后端解决方案骨架与分层项目

- **改动文件**
  - `src/backend/Directory.Build.props`
  - `src/backend/LuckyDraw.slnx`
  - `src/backend/src/LuckyDraw.Domain/LuckyDraw.Domain.csproj`
  - `src/backend/src/LuckyDraw.Application/LuckyDraw.Application.csproj`
  - `src/backend/src/LuckyDraw.Infrastructure/LuckyDraw.Infrastructure.csproj`
  - `src/backend/src/LuckyDraw.Api/LuckyDraw.Api.csproj`
  - `src/backend/src/LuckyDraw.Api/Properties/launchSettings.json`
- **方案简述**：按 `docs/30-architecture.md` §2.5 建立四层结构 `Api → Application → Infrastructure → Domain`，统一 `net10.0` / `Nullable` / `ImplicitUsings`；依赖方向单向：Api → Application、Api → Infrastructure、Infrastructure → Application → Domain。数据访问取 `Microting.EntityFrameworkCore.MySql` 10.0.11（Pomelo 无 EF Core 10 版本，RSK-01）。Api 引 `Asp.Versioning.Mvc` / `JwtBearer` / `OpenApi` / `Serilog.AspNetCore` / `Microsoft.EntityFrameworkCore.Design`（仅设计期工具，供 `dotnet ef`），Infrastructure 引 `BCrypt.Net-Next` / `StackExchange.Redis` / `System.IdentityModel.Tokens.Jwt`。Api 项目末尾 `public partial class Program` 开放集成测试入口（规范 7.1 方案二）。
- **影响面**：新增工程；无既有代码依赖，属首次落地。开发态监听 `http://localhost:5180`。
- **回滚建议**：删除 `src/backend/**` 整个目录即可回退（本仓库此前无任何后端产物）。
- **测试结果**：`dotnet build LuckyDraw.slnx`（于 `src/backend/`）→ **生成成功，0 个警告，0 个错误**。
- **关联**：FR-11、§2.5 骨架、RSK-01。

## CHG-02 Domain：实体 / 枚举 / 业务异常

- **改动文件**
  - `src/backend/src/LuckyDraw.Domain/Entities/AuditableEntity.cs`、`User.cs`、`PrizeItem.cs`、`WinningRecord.cs`、`UserDrawQuota.cs`、`DrawRequest.cs`、`AuditLog.cs`
  - `src/backend/src/LuckyDraw.Domain/Enums/PrizeItemType.cs`
  - `src/backend/src/LuckyDraw.Domain/Exceptions/BusinessException.cs`
- **方案简述**：实体对应技术方案 §4 表 DDL：业务表主键 int 自增（`AuditLog` / `DrawRequest` 为 bigint，高速增长表）；`User` / `PrizeItem` / `WinningRecord` 逻辑删除字段 `IsDeleted`；`UserDrawQuota` 以 `(UserId, DrawDate)` 承载每日配额（D-02，不引入 Redis 计数）；`DrawRequest` 以 `(UserId, IdempotencyKey)` 承载幂等（D-03）；`WinningRecord` 冗余 `PrizeName` 快照，保证奖品改名后历史可追溯。`BusinessException` 携带 `Code`（≥1000）与 `HttpStatusCode`（默认 200，特殊场景如幂等并发冲突用 409），由 Api 层全局过滤器统一转换（D-12）。
- **影响面**：仅新增领域模型，无外部依赖（Domain 不引任何第三方包）。
- **回滚建议**：删除上述文件；`db` 层无独立迁移（与 CHG-03 同一迁移文件），如需彻底回退按 CHG-03 的回滚指引处理。
- **测试结果**：随 CHG-01 构建通过（编译级验证）。行为级验证见 CHG-08。
- **关联**：FR-01、FR-05、FR-08、FR-09、FR-11。

## CHG-03 Infrastructure：DbContext / 迁移 / 种子数据

- **改动文件**
  - `src/backend/src/LuckyDraw.Infrastructure/Data/AppDbContext.cs`
  - `src/backend/src/LuckyDraw.Infrastructure/Data/MySqlErrors.cs`
  - `src/backend/src/LuckyDraw.Infrastructure/Data/TransactionManager.cs`
  - `src/backend/src/LuckyDraw.Infrastructure/Data/Migrations/20260917035407_InitialCreate.cs`（及其 `Designer` / `AppDbContextModelSnapshot`）
  - `src/backend/src/LuckyDraw.Api/appsettings.json`、`appsettings.Development.json`、`appsettings.Testing.json`
- **方案简述**
  - 6 张业务表全部 `utf8mb4_0900_ai_ci`（`User.UserName` 显式排序规则，保证 `Admin` / `admin` 唯一冲突，FR-01-1）；字符串字段全部 `HasMaxLength()`；索引：`UX_User_UserName`、`UX_PrizeItem_Code`、`IX_PrizeItem_Enabled_Order`、`IX_WinningRecord_User_Time`、`UX_UserDrawQuota_User_Date`、`UX_DrawRequest_User_Key`、`IX_AuditLog_Module_Time`。
  - 查询过滤器 `!IsDeleted` 作用于 `User` / `PrizeItem` / `WinningRecord`；唯一性校验路径显式 `IgnoreQueryFilters()`（软删除不释放唯一键，规范陷阱一）。
  - 种子数据 `HasData`：5 条默认奖池（键盘 w1/s3、耳机 w3/s10、马克杯 w10/s50、优惠券 w20/s200、谢谢参与 w66/不限），`SeedTime` 固定 2026-01-01Z，保证迁移幂等、重复执行不产生重复条目（FR-11、AC-18）。
  - 事务：`ITransactionManager` 以 READ COMMITTED 开启，未提交即释放自动回滚；`MySqlErrors` 统一识别 1062（重复键）/ 1205（锁等待超时）/ 1213（死锁），供仓储翻译为业务错误码。
  - 配置：生产 `appsettings.json` 连接串/签名密钥留空且确定性开关关闭；开发态 `localhost:3307`（宿主 3306 被本机既有容器占用，故开发容器映射 3307）+ Redis `localhost:6379`；`appsettings.Testing.json` 放宽限流（集成测试需要）并关闭确定性。
- **影响面**：新增数据库结构（6 张业务表 + 历史表）与默认奖池数据；`Api` 启动依赖上述配置节。开发态端口 3307 需与本机容器一致。
- **回滚建议**：`dotnet ef database update 0 --project src/LuckyDraw.Infrastructure --startup-project src/LuckyDraw.Api` 回退全部表；随后删除 `Data/Migrations/**` 与 `Data/AppDbContext.cs`。仅回退种子数据：`DELETE FROM PrizeItem WHERE Id BETWEEN 1 AND 5`（生产禁用）。
- **测试结果**（真实库，非编译级）：
  - `dotnet ef migrations add InitialCreate --project src/LuckyDraw.Infrastructure --startup-project src/LuckyDraw.Api --output-dir Data/Migrations` → 生成 `20260917035407_InitialCreate`，`Build succeeded`。
  - `dotnet ef database update` → `Applying migration '20260917035407_InitialCreate'. Done.`
  - 容器：`luckydraw-mysql`（mysql:8.4 → MySQL 8.4.11，宿主 3307）、`luckydraw-redis`（redis:alpine，宿主 6379）均 `Up`。
  - 真实查询校验：`SHOW TABLES` 返回 `AuditLog / DrawRequest / PrizeItem / User / UserDrawQuota / WinningRecord (+__EFMigrationsHistory)`；`information_schema.STATISTICS` 确认 4 个唯一索引与 5 个普通索引如设计；`SELECT` 种子返回 5 条，`HEX(Name)` 校验中文按 utf8mb4 正确落库（如 `prize-keyboard` = `E4B880E7AD89E5A596...`「一等奖 · 机械键盘」）。
- **关联**：FR-11、AC-18、D-02、D-03、规范 5 章。

## CHG-04 Infrastructure：仓储并发控制（D-01 / D-02 / D-03）

- **改动文件**
  - `src/backend/src/LuckyDraw.Infrastructure/Repositories/UserRepository.cs`
  - `src/backend/src/LuckyDraw.Infrastructure/Repositories/PrizeRepository.cs`
  - `src/backend/src/LuckyDraw.Infrastructure/Repositories/UserDrawQuotaRepository.cs`
  - `src/backend/src/LuckyDraw.Infrastructure/Repositories/DrawRequestRepository.cs`
  - `src/backend/src/LuckyDraw.Infrastructure/Repositories/WinningRecordRepository.cs`
- **方案简述**
  - **D-01 库存**：`TryDecrementStockAsync` 用条件式 `UPDATE PrizeItem SET Stock = Stock - 1 WHERE Id = @id AND Stock > 0`，**以受影响行数为唯一正确性判据**（≤0 行视为被抢占）；候选集查询取 `IsEnabled && (Type = NoPrize || Stock > 0)`，扣减失败回到候选集重抽（`DrawService` 侧上限 3 轮）。
  - **D-02 每日配额**：`UserDrawQuotaRepository` 先条件式 `UPDATE ... SET UsedCount = UsedCount + 1 WHERE UserId=@u AND DrawDate=@d AND UsedCount < @limit`；0 行再尝试 `INSERT`；唯一索引冲突（1062）则脱离跟踪后重试条件更新，≤3 轮，全部失败抛 1103/配额业务码。
  - **D-03 幂等**：`DrawRequestRepository.TryAddAsync` 先插入 `(UserId, IdempotencyKey)` 占位，重复键（1062）返回 `false` 交由服务层重放首次结果；锁等待超时（1205）翻译为「请勿重复提交」（409）。
  - 只读路径一律 `AsNoTracking()`，列表一律 `Select()` 投影 DTO（权重、库存不出现在任何 DTO）；唯一性校验使用 `IgnoreQueryFilters()`。
- **影响面**：抽奖核心并发语义；仓储为 Infrastructure 内部实现，接口定义在 Application（不跨层）。
- **回滚建议**：删除 `Repositories/**` 与 `Interface/Repositories` 实现即可回退到无持久化状态（需同时回退 CHG-06 的 `DrawService`）。数据库结构不受影响。
- **测试结果**：随 CHG-01 构建通过；`dotnet test` 行为级验证见 CHG-08（本条目提交时尚未创建测试工程）。
- **关联**：FR-05、FR-08、AC-12、AC-13、AC-14、D-01、D-02、D-03。

## CHG-05 Infrastructure：缓存 / 审计 / 令牌 / 随机源 / 密码哈希

- **改动文件**
  - `src/backend/src/LuckyDraw.Infrastructure/Cache/RedisConnectionProvider.cs`、`RedisIdempotencyStore.cs`、`RedisRefreshTokenStore.cs`、`RedisLoginAttemptStore.cs`
  - `src/backend/src/LuckyDraw.Infrastructure/Audit/AuditInterceptor.cs`、`AuditService.cs`
  - `src/backend/src/LuckyDraw.Infrastructure/Security/BcryptPasswordHasher.cs`
  - `src/backend/src/LuckyDraw.Infrastructure/Tokens/JwtTokenService.cs`
  - `src/backend/src/LuckyDraw.Infrastructure/Random/SystemRandomSource.cs`
  - `src/backend/src/LuckyDraw.Infrastructure/DependencyInjection.cs`
- **方案简述**
  - Redis 仅承担**可降级**职责：幂等结果缓存（`draw:idempotency:{userId}:{key}`，24h，权威判据仍是数据库唯一索引）、refresh 会话（`draw:auth:refresh:{userId}`，**只存 SHA-256 哈希**，D-11）、登录失败计数（`draw:auth:loginfail:{userNameLower}`，5 次锁定 15 分钟，故障开放）。连接为惰性建立，`AbortOnConnectFail=false`，Redis 不可用时全部降级为数据库路径（§6.1 降级矩阵）。
  - 审计：`AuditInterceptor` 维护 `CreateTime/UpdateTime`，并对 `User` / `PrizeItem` 变更写 `AuditLog`（字段白名单序列化，**不含 PasswordHash**）；`AuditService` 从 `HttpContext` 补 IP / UserAgent（UA 截断 200），操作人取 `sub` 声明（`ClaimNames.UserId`）。
  - 令牌（D-11）：access 用 `JsonWebTokenHandler` + `SecurityTokenDescriptor`，声明字典显式使用 `sub` / `name`（关闭出站映射，与 Api 侧 `MapInboundClaims=false` 对齐）；refresh 为 `{userId}.{jti}.{base64secret}` 不透明串，服务端只存哈希，轮换即覆盖（单会话族）。
  - 密码 BCrypt（工作因子 11），BCrypt 4.x 的哈希前缀为 legacy `$2a$`，用 `EnhancedEntropy` 关闭的默认实现并在失败时捕获 `SaltParseException` 返回 false。
  - 随机源（D-04 / D-05）：`SystemRandomSource` 用 `RandomNumberGenerator.GetInt32`（加密级），接口 `IRandomSource` 便于测试注入；时钟统一用 BCL `TimeProvider`（`TryAddTimeProvider`），不自定义 `IClock`。
- **影响面**：认证 / 抽奖链路的旁路能力；Redis 不可用不影响业务正确性（仅失去热点缓存与失败计数）。
- **回滚建议**：删除 `Cache/**`、`Audit/**`、`Tokens/**`、`Random/**`、`Security/**` 及 `DependencyInjection.cs` 中对应注册即回退；数据库无变更。
- **测试结果**：随 CHG-01 构建通过；Redis 连通性未做自动化验证（见 CHG-08）。
- **关联**：FR-02、FR-05-R8、FR-10、D-04、D-05、D-11、规范 8.6 / 10 章。

## CHG-06 Application：服务 / 校验器 / Options 强校验

- **改动文件**
  - `src/backend/src/LuckyDraw.Application/Common/PageQuery.cs`、`PageResult.cs`、`ClockExtensions.cs`、`ErrorCodes.cs`、`ClaimNames.cs`
  - `src/backend/src/LuckyDraw.Application/Dtos/AuthDtos.cs`、`PrizeDtos.cs`、`DrawDtos.cs`、`RecordDtos.cs`
  - `src/backend/src/LuckyDraw.Application/Options/DrawOptions.cs`、`PrizeOptions.cs`、`JwtOptions.cs`、`RateLimitOptions.cs`、`OptionsValidators.cs`
  - `src/backend/src/LuckyDraw.Application/Interfaces/Repositories.cs`、`Services.cs`、`ApplicationServices.cs`
  - `src/backend/src/LuckyDraw.Application/Validators/RequestValidators.cs`
  - `src/backend/src/LuckyDraw.Application/Services/AuthService.cs`、`DrawService.cs`、`PrizePoolService.cs`、`WinningRecordService.cs`
  - `src/backend/src/LuckyDraw.Application/DependencyInjection.cs`
- **方案简述**
  - **CR-01 密码规则落地**：`RegisterRequestValidator.PasswordPattern = ^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{8,20}$`，提示文案 `密码需 8-20 位且同时含大写字母、小写字母与数字`（与原型 v3 `reg-password-hint` 逐字一致）；用户名 `^[A-Za-z0-9_]{4,20}$`；确认密码不一致 → `两次输入不一致`。**无首次登录强制改密**。上述常量将作为前端 `types/auth.ts` Zod Schema 的逐字对照源。
  - **抽奖链路（DrawService）**：固定锁顺序 —— ①INSERT `DrawRequest` 占位 → ②条件扣减当日配额 → ③读候选集 → ④加权抽取 → ⑤条件扣减库存 → ⑥写中奖记录 → ⑦写审计 → ⑧回填 `DrawRequest` 结果 → ⑨提交后（事务外）写 Redis 幂等缓存；任一步失败整体回滚（D-08：宁可拒绝，不留无审计的操作）。重放路径先查 Redis 缓存，未命中回落数据库唯一索引，结果不一致时返回「请勿重复提交」（409）。
  - **D-04 加权随机**：整数权重 + 累积区间采样（`cum += w`，取 `r < cum`），随机源注入；`Prize:WeightOverrides` 可在测试环境覆盖权重（FR-11-2）；确定性强制结果（`Draw:Deterministic:Enabled` + `ForcedResults[{userName, prizeItemCode}]`）仅测试环境可用，未命中时告警并回退随机。
  - **D-06 启动强校验**：`DrawOptionsValidator` / `PrizeOptionsValidator` / `JwtOptionsValidator` 经 `IValidateOptions<T>` + `ValidateOnStart()` 生效，在 Development / Testing 之外检测到确定性配置或开发签名密钥时直接**拒绝启动**。
  - **D-07 奖池 DTO 白名单**：`PrizePoolItemDto` 仅 `id/name/shortName/type/displayOrder`，权重与库存仅存在于内部 `DrawCandidateDto`，不出网关。
  - **时间口径（D-05）**：`ClockExtensions.GetDrawDateUtc8()` / `GetNextResetAtUtc()` 基于 `TimeProvider`，固定 +8 偏移，跨日重置不信任客户端时间。
  - 错误码仅使用 `docs/error-codes.md` 已登记号码（1001 / 1002 / 1101 / 1102 / 1103 / 1203 / 1204 / 1501 / 1502）与已登记的幂等冲突码，未新增号码。
- **影响面**：全部业务语义集中于此；Api 层仅做协议转换。
- **回滚建议**：删除 `Services/**`、`Validators/**`、`Dtos/**`、`Options/**`、`DependencyInjection.cs`，并同步回退 CHG-04 仓储；`Api` 层随之不可编译，属整体回退。
- **测试结果**：随 CHG-01 构建通过；编译期修复 2 处（校验器命名空间引用、`TryParseRefreshToken` 可空元组解包）。行为级验证见 CHG-08。
- **关联**：FR-01…FR-09、FR-11、AC-01…AC-18、CR-01、D-04…D-08。

## CHG-07 Api：控制器 / 全局过滤器 / Program 装配 / 配置

- **改动文件**
  - `src/backend/src/LuckyDraw.Api/Controllers/AuthController.cs`、`PrizesController.cs`、`DrawController.cs`、`RecordsController.cs`
  - `src/backend/src/LuckyDraw.Api/Filters/ResultFilter.cs`、`ExceptionFilter.cs`、`ValidationFilter.cs`
  - `src/backend/src/LuckyDraw.Api/Common/ApiResult.cs`、`ClaimPrincipalExtensions.cs`、`RateLimitPolicies.cs`
  - `src/backend/src/LuckyDraw.Api/Program.cs`
- **方案简述**
  - 路由 `api/v{version:apiVersion}/[controller]` + 全局小写 URL（`AddRouting(LowercaseUrls)`），`Asp.Versioning` 采用 `UrlSegmentApiVersionReader`，默认 v1；覆盖 API-01（注册）、API-02（登录）、API-03（刷新）、API-04（登出）、API-05（奖池）、API-06（剩余次数）、API-07（抽奖，`Idempotency-Key` 请求头 + 限流）、API-08（记录分页）。
  - 响应统一 `ApiResult<T>`：控制器**只返回业务对象**，由 `ResultFilter` 包装（`code=0`）；业务异常由 `ExceptionFilter` 转 HTTP 200 + `code≥1000`（D-12：登录失败绝不用 401）；FluentValidation 失败由 `ValidationFilter` 转 `code=1002`；模型绑定失败 → 400 ApiResult。控制器内无 try-catch、无 `ApiResult` 手构、无 `DbContext`。
  - 认证：JwtBearer `MapInboundClaims=false`，`ValidateIssuer/Audience/Lifetime/IssuerSigningKey` 全开，`ClockSkew=30s`；refresh 令牌走 httpOnly + Secure（非 Development/Testing）+ SameSite=Strict Cookie，`Path=/api/v1/auth`；`RefreshToken` 字段在 DTO 上标 `[JsonIgnore]`，仅通过 Cookie 表达。
  - 限流（规范 8.6）：注册 / 登录按 IP、抽奖按用户，固定窗口 10 次/分钟（Testing 放宽至 1000 以支持并发测试），拒绝响应为 429 + `code=1001`「系统繁忙，请稍后重试」（rate limit 为基础设施拒绝，保留 429 语义，与业务异常不同）。
  - 中间件顺序：`UseForwardedHeaders`（先清空 `KnownIPNetworks` / `KnownProxies`，须在限流前）→ 安全响应头（`X-Content-Type-Options` / `X-Frame-Options` / CSP）→ `UseSerilogRequestLogging` → `UseRouting` → `UseCors`（白名单 + `AllowCredentials`，未配置来源则不添加策略）→ `UseAuthentication` → `UseAuthorization` → `UseRateLimiter` → `MapControllers`。
  - 日志：Serilog 结构化输出，`Microsoft.EntityFrameworkCore.Database.Command` 降至 Warning，避免 SQL 参数（含用户输入）进日志。
- **影响面**：对外契约层；前端与 E2E 全部依赖此处路由 / 响应结构 / Cookie 名。
- **回滚建议**：删除 `Controllers/**`、`Filters/**`、`Common/**`、`Program.cs` 与 `appsettings*.json`；后端即回到不可运行状态（整体回退起点）。
- **测试结果**：`dotnet build LuckyDraw.slnx` 通过（0 警告 0 错误，含 `ASPDEPR005` 修复：`KnownNetworks` → `KnownIPNetworks`）；`dotnet ef migrations add / database update` 借助此启动项目成功执行。HTTP 端到端验证见 CHG-08。
- **关联**：FR-02、FR-03、FR-04、FR-05、FR-09、FR-10、API-01…API-08、D-12。

## CHG-08 后端测试（`tests/unit/**`、`tests/integration/**`）

- **改动文件**
  - `tests/unit/LuckyDraw.UnitTests/LuckyDraw.UnitTests.csproj`（xUnit + FluentAssertions 7.* + NSubstitute 5.*）
  - `tests/unit/LuckyDraw.UnitTests/TestDoubles/FixedTimeProvider.cs`、`SequenceRandomSource.cs`
  - `tests/unit/LuckyDraw.UnitTests/Common/WeightedSamplerTests.cs`、`ClockExtensionsTests.cs`、`PageQueryTests.cs`
  - `tests/unit/LuckyDraw.UnitTests/Validators/RequestValidatorTests.cs`
  - `tests/unit/LuckyDraw.UnitTests/Services/DrawServiceTests.cs`
  - `tests/integration/LuckyDraw.IntegrationTests/LuckyDrawApiFactory.cs`、`IntegrationFixture.cs`、`AssemblyInfo.cs`
  - `tests/integration/LuckyDraw.IntegrationTests/AuthApiTests.cs`、`DrawApiTests.cs`、`ConcurrencyTests.cs`
  - `src/backend/src/LuckyDraw.Application/Common/WeightedSampler.cs`（新增）、`Services/DrawService.cs`（改用 `WeightedSampler`）
  - `src/backend/src/LuckyDraw.Application/LuckyDraw.Application.csproj`（`InternalsVisibleTo` 单元测试程序集）
- **方案简述**
  - 单元：把 D-04 的累积区间取样抽到 `WeightedSampler`（internal，便于对区间边界做白盒断言），覆盖命中值 0/3/4/13/14/33/34/99 的边界、零权重永不命中、权重全零回退、10 万次取样分布偏差 < 1.5%；D-05 用自实现 `FixedTimeProvider`（不引第三方时钟包）覆盖 UTC+8 跨日与重置时刻；CR-01 逐条覆盖「缺大写 / 缺小写 / 缺数字 / 长度越界」并**锁定冻结文案与正则常量**；`DrawService` 用 NSubstitute 覆盖 1501、1502、幂等重放、409 冲突、确定性命中、库存重抽 ≤3 轮、权重覆盖、无幂等键分支。
  - 集成（规范 7.1 方案二）：`WebApplicationFactory<Program>` + **环境变量**注入连接串（未使用 `ConfigureAppConfiguration`），独立库 `luckydraw_test` + Redis db=1；每例前清表并复位奖池权重 / 库存；测试程序集禁用并行。
- **影响面**：新增测试工程与测试专用 DB；生产代码仅新增 internal 取样器与可见性声明，无行为变更。
- **回滚建议**：删除 `tests/unit/**`、`tests/integration/**` 及 `WeightedSampler.cs`（把 `DrawService.WeightedPick` 还原为内联实现），并移除 `LuckyDraw.Application.csproj` 中的 `InternalsVisibleTo`。
- **测试结果**（实际执行）
  - `dotnet test tests/unit/LuckyDraw.UnitTests/LuckyDraw.UnitTests.csproj` → **已通过：64 通过 / 0 失败**（含 2 处测试自身期望值订正：UTC+8 次日零点对应的 UTC 重置时刻）。
  - `dotnet test tests/integration/LuckyDraw.IntegrationTests/LuckyDraw.IntegrationTests.csproj` → **已通过：28 通过 / 0 失败**（7s，连真实 MySQL 8.4.11 + Redis）。
  - 覆盖：AC-01/02/03/04/05/06/07/08/10/12/13/15，D-12（登录失败 HTTP 200 + 1102）、D-07（响应不含 weight/stock）、D-01（10 并发抢库存 3 → 恰好 3 中、库存 0、无负数）、D-02（10 并发同用户 → 恰好 3 成功 / 7 个 1501、`UsedCount=3`）、D-03（同键并发重放一致且只扣 1 次；清 Redis 后走唯一索引路径结果一致）。
- **关联**：AC-11…AC-16、规范 7.1。

## CHG-11 测试暴露缺陷的修复（响应包装 / 并发死锁）

- **背景**：集成测试首轮跑出 2 类缺陷（**均为真实缺陷，不是测试写错**），修复后回归全绿。
- **改动文件**
  - `src/backend/src/LuckyDraw.Api/Common/ApiResult.cs`（新增 `IApiResult` 标记接口）
  - `src/backend/src/LuckyDraw.Api/Filters/ResultFilter.cs`（改判 `IApiResult`）
  - `src/backend/src/LuckyDraw.Domain/Exceptions/TransientDataException.cs`（新增）
  - `src/backend/src/LuckyDraw.Infrastructure/Data/MySqlErrors.cs`（新增 `IsTransient`）
  - `src/backend/src/LuckyDraw.Infrastructure/Repositories/UserDrawQuotaRepository.cs`、`PrizeRepository.cs`、`DrawRequestRepository.cs`
  - `src/backend/src/LuckyDraw.Application/Services/DrawService.cs`（整车重试 + 尽力回滚）
- **缺陷 1：校验失败响应被二次包装（破坏 API 契约）**
  - 现象：`POST /api/v1/auth/register`（弱密码）返回 `{"code":0,"data":{"code":1002,"message":"密码需 …"},"data":…}` —— 外层 code=0，前端按 `code===0` 判定会把业务失败当成功。
  - 根因：`ResultFilter` 用 `is ApiResult` 判断「已包装」，而 `ApiResult<T>` 继承自 `ApiResult<object?>`（派生的非泛型 `ApiResult`），泛型实例判不出来 → 再包一层。
  - 修复：引入 `IApiResult` 标记接口（`ApiResult<T>` 实现），`ResultFilter` 改判接口。
- **缺陷 2：同用户并发抽奖触发 MySQL 死锁 1213 → HTTP 500**
  - 现象：10 个并发抽奖请求中 4 个返回 500（日志：`Deadlock found when trying to get lock`，栈顶为配额条件 UPDATE）。
  - 根因：同用户并发下「流水占位 → 配额条件更新 → 配额行缺失则插入」产生交叉加锁；InnoDB 对死锁牺牲者**回滚整个事务**，此前仅按错误号局部处理（1062/1205），死锁直接冒泡成 500。
  - 修复：仓储把 1213/1205 翻译为领域级 `TransientDataException`（业务层不依赖数据库实现），`DrawService` **重试整个事务 ≤3 次**（重试幂等安全：流水占位随事务回滚后重新抢占同一幂等键），耗尽后返回 1001「系统繁忙，请稍后重试」（FR-10-3）而非 500；事务回滚改为「尽力回滚」，避免死锁后的二次异常掩盖原始异常。
- **影响面**：抽奖链路并发健壮性、认证错误响应契约；对正常路径无行为变化（无条件扣减语义与锁顺序未变）。
- **回滚建议**：还原上述 5 个生产文件到本条目之前版本（`git checkout`/编辑器撤销即可）；`IApiResult` 与 `TransientDataException` 为新增类型，删除即可，无数据变更。
- **测试结果**：修复后 `tests/integration` 全量重跑 **28 通过 / 0 失败**（含 `ConcurrentDraws_BySameUser_NeverExceedDailyLimit`、`ConcurrentDraws_OnScarcePrize_NeverOversellStock`、`ConcurrentDraws_WithSameIdempotencyKey_ConsumeQuotaOnce`）；`tests/unit` 64 通过 / 0 失败。
- **关联**：D-01、D-02、D-03、D-12、FR-08、FR-10、规范 6.3 / 6.4。

## CHG-09 前端工程（`src/frontend/**`）

- **改动文件**
  - 工程配置：`package.json`、`vite.config.ts`（含 vitest 段与 `/api` 开发代理）、`tsconfig.json`、`index.html`、`eslint.config.js`、`components.json`、`.gitignore`
  - 主题：`src/assets/main.css`（Tailwind 4 入口 + `:root` 令牌 + `@theme inline`，逐值搬自 §2.7）
  - 类型与契约：`src/types/{api,auth,prize,draw,record}.ts`（字段与后端 DTO 逐一对齐；Zod Schema 与错误码常量在此声明）
  - 请求层：`src/utils/{request,auth-token,error,idempotency,datetime,messages}.ts`；接口定义 `src/api/{auth,prize,draw,record}.ts`
  - 状态与路由：`src/stores/auth.ts`（仅认证态）、`src/router/index.ts`（懒加载 + 全局守卫）、`src/main.ts`、`src/App.vue`（错误边界）
  - 组合式：`src/composables/{useDrawFlow,usePagination}.ts`
  - 布局与组件：`src/layouts/{AuthLayout,AppLayout}.vue`、`src/components/business/{AppBar,QuotaBadge,PrizeLegend,EmptyState,DrawWheel,DrawResultDialog}.vue`
  - 页面：`src/views/auth/{RegisterView,LoginView}.vue`、`src/views/draw/DrawView.vue`、`src/views/records/WinningRecordsView.vue`
  - **只读生成物（禁止手改，权限层 deny）**：`src/components/ui/**`、`src/lib/utils.ts` —— 由 `npx shadcn-vue@2.x add button input label card form alert dialog badge skeleton table pagination` 生成
- **方案简述**
  - 技术栈按架构 §2.5 版本约束：Vue 3.5 + TS strict（无 `any`、`<script setup lang="ts">`）+ Tailwind 4（`@theme` 令牌，无独立 CSS、无色值硬编码）+ shadcn-vue 2.x + Pinia 4 + Vue Router 4 + Zod 3.25 + vee-validate 4.15 + TanStack Table 8 + axios。
  - §2.6 定位契约：20 个状态锚点**全部**落 `data-testid`（与锚点 ID 逐字一致），另加 `draw-win-title` / `draw-lose-title` / `draw-quota` / `records-row-{id}`；同态复用同一 testid 时挂在「该状态可见区块」上（如 `page-register--invalid` 挂 form、`page-records--loading/error/empty` 挂 tbody 替换块）；原型 `?state=` / `?page=` 未进入真实应用；`disabled` 由实现真实提供。
  - 认证：访问令牌**只存内存**（`utils/auth-token.ts`），绝不落 localStorage/sessionStorage；页面刷新后由受保护路由首次进入时静默 `POST /auth/refresh` 恢复（用户名等非敏感信息缓存于 sessionStorage）。无感刷新按规范 3.3 四条硬约束实现：在途 Promise 去重（防 refresh token 一次性轮换被并发复用踢下线）、`_retry` 标记单次重放、刷新走**裸 axios 实例**、重放走**带拦截器的封装实例**，且刷新失败按 `code` 自行判定（业务异常 HTTP 200 不进错误分支）。
  - 幂等：`Idempotency-Key` 由前端 `crypto.randomUUID()` 生成，**失败重试沿用同一键**，仅在成功/确定性业务拒绝后作废（D-03）。
  - 状态归属：奖池、记录等页面态留在 `composables` / 组件内，Pinia 仅承载认证态（规范 3.3）。
  - 文案集中 `utils/messages.ts`；CR-01 密码规则常量与错误文案与后端 `RequestValidators` **逐字一致**（`types/auth.ts` 导出并被单测锁定）。
  - 其余：请求统一走 `utils/request`（组件内禁止直连 axios）；`app.config.errorHandler` + `onErrorCaptured` 降级 UI（禁止白屏）；`prefers-reduced-motion` 由 Tailwind `motion-reduce:` 承担；转盘扇区配色仅用 `@theme` 的 `fill-wheel-*` 令牌。
- **影响面**：新增前端工程；不改变任何后端契约与 DTO 字段；未引入规范外依赖（新增依赖均为规范 2.2 已列项 + shadcn-vue 组件运行期依赖 `reka-ui` / `class-variance-authority` / `clsx` / `tailwind-merge` / `@lucide/vue` / `@vueuse/core` / `tw-animate-css`）。
- **回滚建议**：整目录删除 `src/frontend/**` 即可（后端与测试不受影响）；如需保留工程骨架、仅回滚本轮实现，删除 `src/{api,components,composables,layouts,router,stores,types,utils,views}` 与 `src/{App.vue,main.ts}` 并还原 `src/assets/main.css`。
- **测试结果**：见 CHG-10（`vue-tsc` exit 0、`vite build` exit 0、`vitest run` 41 通过、`eslint --max-warnings 0` exit 0）。
- **关联**：FR-01…FR-10、§2.5 / §2.6 / §2.7、D-03 / D-07 / D-11 / D-12、AC-02、AC-04、AC-05、AC-06、AC-07、AC-08、AC-09、AC-10。

## CHG-10 前端验证（build / lint / 单测）

- **改动文件**：`eslint.config.js`（补 `.vue` 浏览器 globals）、`src/composables/useDrawFlow.ts`（删空自赋值）、若干 `.vue` / `.ts` 的**纯格式化**改动（`eslint --fix`，未触及 `src/components/ui/**`）、新增 `src/**/__tests__/*.spec.ts`（6 个文件）。
- **方案简述**
  - 验证方式：因环境限制（见下）改用直接调用二进制，命令与结果原文：
    | 命令 | 结果 |
    | --- | --- |
    | `node node_modules/vue-tsc/bin/vue-tsc.js --noEmit` | **exit 0**（0 类型错误） |
    | `node node_modules/vite/bin/vite.js build` | **exit 0**，2695 modules，CSS 38.46 kB；`LoginView` / `RegisterView` / `DrawView` / `WinningRecordsView` 各自独立 chunk（**路由懒加载生效**） |
    | `node node_modules/eslint/bin/eslint.js . --max-warnings 0` | 首轮 **exit 1（6 error + 120 warning）** → 修复后 **exit 0（0 error / 0 warning）** |
    | `node node_modules/vitest/vitest.mjs run` | **6 个文件 / 41 用例全部通过** |
  - **环境约束（如实记录，非本项目代码问题）**：仓库绝对路径含 `&`（`AI-Agents workflow & Engineer Rules&feature`），npm 在 Windows 经 `cmd.exe` 展开脚本时把 `&` 当命令分隔符，`npm run build|lint|test:unit` 必然报 `'Engineer' 不是内部或外部命令` / `Cannot find module 'D:\AI test\vue-tsc\bin\vue-tsc.js'`。**未**写入 `.npmrc` 覆盖 `script-shell`（硬编码 Git Bash 路径会破坏 Linux/CI）；`package.json` 中保留标准脚本名，CI/常规路径下 `npm run *` 可直接使用。
  - lint 修复分两类：① **配置缺口** `.vue` 文件不在 typescript-eslint 的 TS 作用域内，`js.configs.recommended` 的 `no-undef` 把 `window` / `console` 误报为未定义 → 在 `files: ['**/*.vue']` 块注册浏览器 globals（选择「补 globals」而非「关规则」，保留规则价值）；② **真实缺陷** `useDrawFlow.loadQuota` catch 中的 `remaining.value = remaining.value`（`no-self-assign`）为空自赋值死代码 → 删除该行只留注释，行为不变（失败时 `remaining` 保持进入前的值，按钮可用性由抽奖返回值兜底修正）。其余 120 个 warning 为格式类（`vue/max-attributes-per-line`、`vue/singleline-html-element-content-newline`），由 `eslint --fix` 自动修复。
  - 前端测试目录口径：用例落在 `src/frontend/src/**/__tests__/*.spec.ts`，与架构 §2.5 第 186 行「Vitest 用例就近放 `src/frontend/src/**/__tests__/` 或 `*.spec.ts`」一致（顶层 `tests/{unit,integration}` 为后端 xUnit 项目，二者分区不重叠）；`vitest.config` 的 include 即 `src/**/__tests__/**/*.spec.ts`，因此**不存在与骨架的偏差**。
  - 覆盖清单：`utils/error.ts`（错误码分流矩阵，含 1001 / 1102 / 1203 / 1204 / 1501 / 1502 / 409 与网络异常兜底）、`utils/datetime.ts`（.NET 无 `Z` 后缀按 UTC 解析、6 位毫秒截断、UTC+8 跨日边界、非法输入原样返回）、`utils/idempotency.ts`（UUID v4 形态、100 键互异）、`types/auth.ts`（CR-01：缺大写 / 缺小写 / 缺数字 / 长度越界 / 8 与 20 边界 / 确认密码不一致 / 冻结文案逐字）、`stores/auth.ts`（登录与注册落态、登出接口失败仍清空且不抛、`ensureSession` 恢复与失败路径、**令牌不落存储**）、`composables/useDrawFlow.ts`（ready / empty / error、次数查询失败不阻断、win / lose / 1501 / 1502、幂等键沿用与成功后作废、`clearResult`）。
- **影响面**：仅前端工程内部；不涉及接口契约、不改动 `src/components/ui/**`。
- **回滚建议**：`eslint.config.js` 的 globals 块可直接删除（会退回 5 个 `no-undef` 误报）；`useDrawFlow` 的删除行为死代码，无需回滚；测试文件删除 `src/**/__tests__/**` 即可。
- **测试结果**：即上表三条 exit 0 + vitest 41/41。**未跑项**：组件渲染测试（未写，理由：本轮判分锚点由 E2E 覆盖，`tests/e2e/**` 归 test-executor）；真实浏览器 E2E（同上）；`npm run *` 原样脚本（环境限制，见上）。
- **关联**：附录 A 检查清单、规范 3.3（无感刷新）/ 3.5（错误边界）、FR-01…FR-10。

## CHG-12 前端测试守卫：`passWithNoTests` 置 `false`

- **改动文件**：`src/frontend/vite.config.ts`（`test.passWithNoTests` 由 `true` 改为 `false`；`environment: 'jsdom'` 与 `include: ['src/**/__tests__/**/*.spec.ts']` 保持不变）。
- **方案简述**：CHG-10 首轮 `vitest run` 报「No test files found」却 **exit 0**（假绿），根因是配置里显式写了 `passWithNoTests: true`，而非 vitest 默认行为。41 个用例到位后该配置只剩风险：**将来谁误删或挪错测试目录，CI 会绿着放过**（与 CLAUDE.md 专门警告的 `dotnet format` 在 `.slnx` 上打印 help 并静默退出码 0 属同一类坑）。改为 `false` 后，无匹配用例时 vitest 以非 0 退出，守卫真实生效；为便于后人理解，配置项上方加一行注释说明缘由。
- **影响面**：仅前端测试配置，不影响运行时代码、构建产物与接口契约；正常用例集下行为完全一致（仍 6 文件 / 41 用例）。副作用是**期望的**：测试目录为空/失配时 CI 转为红灯。
- **回滚建议**：把 `src/frontend/vite.config.ts` 的 `passWithNoTests` 改回 `true` 即可（单行改动，无其他连带），随即恢复「无用例也通过」的旧行为。
- **测试结果**（实际执行，直接调用二进制；npm script 因仓库路径含 `&` 不可用，见 CHG-10）
  | 命令 | 结果 |
  | --- | --- |
  | `node node_modules/vitest/vitest.mjs run` | **6 文件 / 41 用例通过，exit 0** |
  | `node node_modules/vitest/vitest.mjs run src/__nonexistent__` | 输出 `No test files found, exiting with code 1`，**exit 1（观察到的实际退出码）** —— 守卫生效；未删除/移动任何真实测试文件 |
- **关联**：附录 A 检查清单、CHG-10（同源问题的收口）、CLAUDE.md 对「静默退出码 0 = CI 假绿」的同类警告。

---

## CHG-13 60-review v1 用户裁决的 14 项 REV 修复

- **背景**：`docs/60-review.md` v1 的 14 项（严重 2 / 一般 11 / 从建议级单独纳入 1）经用户裁决后由本轮修复。契约基准为 `docs/30-architecture.md` **v3**（§2.6 次数未知态补充约定、**§2.8 会话与恢复契约**、D-14、D-15、API-03 响应新增 `user`）与 `docs/development-spec.md` §3.3（access token 仅存内存）。
- **明确不在本轮范围**（用户裁决）：REV-10、REV-11、REV-12、REV-16、REV-18 五项建议级不修，未做任何顺手改动。
- **统一验证结果**：见本节末尾「本轮验证记录」（实际命令与结果数字，收尾时统一回填）。

### REV-13　移除 Google Fonts 外链（性能预算禁外链 + CSP 基线）

- **状态**：已落地。
- **改动文件**：`src/frontend/src/assets/main.css`（删除文件头部的 `@import url('https://fonts.googleapis.com/css2?...Geist...')`；`--font-sans` 由 `'Geist Variable', sans-serif` 改为系统字体栈 `'Geist', 'Segoe UI', system-ui, sans-serif`）。
- **方案简述**：采用审查建议的零依赖方案 ①：外链字体是本仓库唯一的外部运行时依赖（渲染阻塞 `@import`），与架构 §8 构建产物「不引入图片 / 字体 / 图标库外链」及附录 B `default-src 'self'` 的 CSP 基线两侧冲突，且家族名（`Geist` vs `'Geist Variable'`）本就不匹配、实际静默回退；删除后既保留语义令牌结构，又消除渲染阻塞点。未安装任何字体包（引入依赖需先确认）。
- **影响面**：仅前端首屏资源加载与字体回退表现；不影响接口契约、构建产物结构（CSS 体积略减，外链请求消失）。字体回退到系统栈属预期行为。
- **回滚建议**：在 `main.css` 头部恢复该 `@import`，并把 `--font-sans` 改回 `'Geist Variable', sans-serif`（单文件两处改动）。
- **测试结果**：见本节末尾「本轮验证记录」（构建 / lint / 单测）。
- **关联**：REV-13、架构 §8 性能预算、附录 B（统一安全响应头）、CLAUDE.md 前端红线。

### REV-14　组件内硬编码用户可见中文迁入 `utils/messages.ts`

- **状态**：已落地。
- **改动文件**：`src/frontend/src/utils/messages.ts`、`src/frontend/src/views/auth/LoginView.vue`、`src/frontend/src/views/auth/RegisterView.vue`、`src/frontend/src/layouts/AuthLayout.vue`、`src/frontend/src/components/business/QuotaBadge.vue`、`src/frontend/src/views/records/WinningRecordsView.vue`。
- **方案简述**：6 处（含 `aria-label="分页"`）字面量全部迁入 `messages.*`：
  - 新增 `login.subtitle`、`authLayout.tagline`；
  - 新增 `register.subtitle`，**由 `types/auth` 的冻结常量 `USER_NAME_MESSAGE` / `PASSWORD_MESSAGE` 组合而成**（规则文案唯一来源，消除 RegisterView 原「密码 8-20 位且含大小写字母与数字」与 `passwordHint` 的第二份副本，落实附录 A 第 10 条）；
  - 新增 `records.totalCount(count)` / `records.pageIndicator(page, total)` / `records.paginationLabel`、`draw.dailyLimit(limit)`（含参文案用函数形态，仍集中在唯一出口）。
  - 卡片副标题可见文案随组合略有扩写（"用户名需 4-20 位，仅字母、数字、下划线；密码需 8-20 位且同时含大写字母、小写字母与数字"），仍为规则准确表述；该位置不是 §2.6 的 20 个锚点，未触碰冻结文案。
- **影响面**：仅前端文案出口；组件内不再出现用户可见中文字面量（i18n 预留恢复有效）。
- **回滚建议**：删除新增键并把 6 处模板改回字面量（各文件独立，可逐处回退）。
- **测试结果**：见本节末尾「本轮验证记录」。
- **关联**：REV-14、架构附录 A 第 4 / 10 条、MOD-08 职责、PRD 4.5-1。

### REV-15　记录页时间展示改为 `yyyy-MM-dd HH:mm`

- **状态**：已落地。
- **改动文件**：`src/frontend/src/utils/datetime.ts`、`src/frontend/src/views/records/WinningRecordsView.vue`、`src/frontend/src/utils/__tests__/datetime.spec.ts`。
- **方案简述**：`formatUtc8DateTime` 增加精度参数（`TimePrecision = 'minute' | 'second'`，默认 `second` 保持既有调用语义），记录页列单元格改传 `'minute'`，输出 `yyyy-MM-dd HH:mm`（PRD 4.1-3 / API-08）。单测补两组分钟精度用例（含 UTC+8 跨日与非法输入原样返回），原带秒断言保留（两者均为受控行为）。
- **影响面**：仅记录页时间列可见格式（去秒）；UTC+8 换算与解析容错逻辑不变。
- **回滚建议**：`WinningRecordsView.vue` 的 `formatUtc8DateTime(row.original.winTime, 'minute')` 去掉第二参数即可回到带秒格式；精度参数可保留（默认行为未变）。
- **测试结果**：见本节末尾「本轮验证记录」。
- **关联**：REV-15、PRD 4.1-3、FR-09-3、API-08。

### REV-08　转盘停稳动画按 D-14 补齐（2400ms + `transitionend` + 兜底）

- **状态**：已落地。
- **改动文件**：`src/frontend/src/components/business/DrawWheel.vue`；配套 `src/frontend/eslint.config.js`（`.vue` 文件块补 `SVGGElement` / `TransitionEvent` 两个浏览器 globals，服务于转子 `ref<SVGGElement | null>` 与 `transitionend` 处理函数的规则检查，属 REV-08 的配套改动）。
- **方案简述**：停稳时长 1000ms → **2400ms**，缓动改为 `cubic-bezier(.16,.84,.24,1)`（磁盘实现：时长与缓动为 `SETTLE_DURATION_MS`（L39，2400）与 `SETTLE_EASING`（L42）两个常量，经 `rotorStyle` 计算属性以内联过渡样式下发；模板转子为 `class="transition-transform motion-reduce:transition-none"` + `:style="rotorStyle"`，**未使用** Tailwind 任意值类。改用内联样式的原因是：最初写成 `duration-[2400ms] ease-[...]` 字面量后，两个 TS 常量在模板中无人引用，被 `vue-tsc` 判为 `TS6133`「已声明但从未读取」，且时长 / 缓动在两处各写一份存在漂移风险；收拢到 `rotorStyle` 后常量成为唯一事实来源，`transform` / `transformOrigin` 一并内联（`motion-reduce:transition-none` 只置 `transition-property: none`，内联的时长 / 缓动不会使其复活，§2.6 #15 的减动效语义不变））；停稳信号由「与动画等长的 `setTimeout`」改为 **`transitionend` 主通道 + 2600ms 兜底定时器**（`settlePending` 窗口保证起转过渡的事件被忽略、两个信号先到者胜且幂等）；`prefers-reduced-motion` 分支保持「无过渡、无旋转圈、直接落终值」（`motion-reduce:transition-none` 下不会产生 `transitionend`，以 0ms 定时器立即通知停稳）。旋转圈数不变（起转 3 圈 + 停稳补 2 圈，满足 FR-06 ≥2 圈）。
- **影响面**：仅抽奖页动画时序；`settled` 事件与视觉停稳的一致性由同一时间轴（CSS 过渡）驱动，消除「弹层早于 / 晚于转盘停稳」的漂移。
- **回滚建议**：还原 `SETTLE_DURATION_MS` 为 1000、模板类名改回 `duration-[1000ms] ease-out`，并把 `transitionend`/兜底逻辑改回单一 `setTimeout`（单文件改动）。
- **测试结果**：见本节末尾「本轮验证记录」（组件级动画时序由 E2E 覆盖，本轮不加渲染测试，同 CHG-10 口径）。
- **关联**：REV-08、D-14、FR-06、§2.6 #14 / #15。

### REV-01　首屏次数查询失败不再禁用抽奖入口、不再误报「次数已用完」

- **状态**：已落地。
- **改动文件**：`src/frontend/src/composables/useDrawFlow.ts`、`src/frontend/src/views/draw/DrawView.vue`、`src/frontend/src/components/business/QuotaBadge.vue`、`src/frontend/src/components/business/DrawResultDialog.vue`（`remaining` prop 放宽为 `number | null` 并以「—」占位渲染，是未知次数态弹层侧的呈现出口）、`src/frontend/src/utils/messages.ts`。
- **方案简述**（严格按 §2.6 v3「次数未知态」补充约定）：
  - `remaining` / `dailyLimit` 改为 `number | null`，初值 `null`（**未知 ≠ 0**）；新增透出 `resetAt`。
  - 「未知」时：抽奖按钮保持可用（`hasQuota = remaining === null || remaining > 0`）、**不**命中 `page-draw--noquota`（触发条件收紧为 `remaining === 0`，即后端确认）、`draw-quota` 徽标以占位「—」呈现（不渲染 0）、每日上限徽标在上限未知时隐藏；转盘中心文案同样用占位符（原实现会渲染 `null`）。
  - 恢复通道：①进入页面重拉；②抽奖成功后**先**以 API-07 响应的 `remainingAttempts` 回写徽标、**再**异步刷新 API-06（`loadQuota` 失败不覆盖已确认值）；③收到 `1501` 权威确认 → `remaining = 0` → 展示锚点 12。
  - **未新增任何锚点 / 边界状态**（契约明确不引入「可见的次数查询失败提示 + 重试按钮」，避免触碰 `20-prototype` 冻结面）。
- **影响面**：抽奖页首屏失败路径的可达性与文案正确性；锚点 8 / 12 / 16 的触发语义按 v3 收紧（其余锚点不变）。
- **回滚建议**：`useDrawFlow` 的 `remaining` / `dailyLimit` 改回 `number` 初值 0、`hasQuota` 改回 `remaining > 0`，DrawView 的 `v-if` 改回 `!hasQuota`、徽标改回直接渲染 `remaining`（三文件，互相独立）。
- **测试结果**：见本节末尾「本轮验证记录」（含 REV-17 新增用例）。
- **关联**：REV-01、FR-04、FR-10-3、AC-07、AC-10、§2.6（v3 补充约定）、API-06 前端契约。

### REV-17　补「首载即失败」用例，消除被固化的错误行为

- **状态**：已落地。
- **改动文件**：`src/frontend/src/composables/__tests__/useDrawFlow.spec.ts`。
- **方案简述**：
  - 原用例「剩余次数查询失败不阻断主流程」改名为「**刷新**失败不覆盖已确认值」（保留其断言语义，但不再充当「不阻断」的证据）；
  - **新增**「首载即失败」用例：断言 `remaining === null`（不得为 0）、`hasQuota === true`、抽奖请求照常发出、并以抽奖响应回写剩余次数；
  - **新增**「收到 1501 将未知次数收紧为 0」用例；
  - **新增**「抽奖成功后刷新 API-06 并采用其值」用例；
  - 调整「抽奖命中奖品」用例：令抽奖后的 API-06 刷新**失败**，锁定「刷新失败不覆盖抽奖响应已确认值」分支（否则该断言会被后续刷新结果掩盖，正是 REV-17 指出的同一类构造缺陷）。
- **影响面**：仅测试；用例数 9 → 12。
- **回滚建议**：删除新增用例并还原两处标题 / mock 构造（测试文件单文件回退，不影响运行时代码）。
- **测试结果**：见本节末尾「本轮验证记录」。
- **关联**：REV-17、REV-01、FR-10-3、CLAUDE.md 工作准则 5。

### REV-02　会话恢复改为「仅受保护路由触发 + 内存优先」（§2.8）

- **状态**：已落地（store 三态闸门 + 无存储化 + 前端刷新链路返回体改造 + API-03 后端补 `user` + 路由守卫接入 + `auth.spec.ts` 按新模型重写）。
- **改动文件**：
  - `src/frontend/src/stores/auth.ts`（删除 `sessionStorage` 用户缓存与 `readCachedUser`；引入 `sessionGate: 'idle' | 'restoring' | 'ready'` 与在途 `restorePromise`；`ensureSession({ allowRefresh })`：默认（公开页语义）只读内存态、**绝不刷新**，受保护路由传 `allowRefresh: true` 时发起一次静默恢复，`restoring` 期间复用同一 Promise，失败回 `idle`；`ready` 为终态）
  - `src/frontend/src/router/index.ts`（守卫仅对受保护路由调 `ensureSession({ allowRefresh: true })`；`meta.public` 分支直接放行、**不触碰刷新链路**）
  - `src/frontend/src/utils/request.ts`（在途刷新 Promise 返回 `RefreshResponse | null` 而非仅 token 字符串；401 重放判据同步调整 —— 刷新链路本体未变）
  - `src/frontend/src/api/auth.ts`（`refreshSession()` 返回 `RefreshResponse | null`，复用 `refreshAccessToken` 的同一在途 Promise）
  - `src/frontend/src/types/auth.ts`（`RefreshResponse` 增可选 `user`）
  - `src/frontend/src/stores/__tests__/auth.spec.ts`（重写为内存态模型：公开页断言原样保留，新增受保护路由恢复 / 缺 `user` 判失败 / 并发去重 / `ready` 终态 / 失败回 `idle` 可重试用例）
  - `src/backend/src/LuckyDraw.Application/Dtos/AuthDtos.cs`、`Services/AuthService.cs`（API-03 响应新增 `user`，**兼容性新增、v1 不升版**）
- **方案简述**：按 §2.8 四条契约落地：①会话态仅存内存（access token + user），**禁止** localStorage / sessionStorage 作为凭证或「是否已登录」判据 → 删除 sessionStorage 用户缓存，用户信息唯一来源为「登录 / 注册响应」或「refresh 响应的 `user`」；②恢复触发唯一入口 = 受保护路由（公开页不触发，保留既有测试语义，`auth.spec.ts` 的公开页断言未改）；③恢复动作复用 401 无感刷新的同一在途 Promise 与裸实例（一次性轮换，禁止两路并发刷新触发 `1203` 全端登出）；④三态闸门，`ready` 为终态、失败回 `idle` 允许再次导航重试（无定时器 / 拦截器自动重试）。
- **影响面**：新标签页 / 页面刷新后的登录态保持；`API-03` 响应体新增字段（向后兼容）；后端 `RefreshResponse` 结构变化需前端 `types/auth.ts` 同步（已同步）。
- **回滚建议**：还原 `stores/auth.ts` 为「sessionStorage 用户缓存 + `bootstrapped` 布尔」旧实现，`utils/request.ts` / `api/auth.ts` 的返回体改回 `string | null`，并撤回 `RefreshResponse.User`（前后端各一处）；守卫改回 `await authStore.ensureSession()`；`auth.spec.ts` 还原旧用例。
- **测试结果**：`src/stores/__tests__/auth.spec.ts`（12 例，含新增的恢复 / 去重 / 终态用例）与 `src/composables/__tests__/useDrawFlow.spec.ts`（含 REV-17 新增用例）合计 **25 passed / 0 failed**（`node node_modules/vitest/vitest.mjs run src/stores/__tests__/auth.spec.ts src/composables/__tests__/useDrawFlow.spec.ts`，Duration 1.30s）；全量前端四件套见本节末尾「本轮验证记录」。
- **关联**：REV-02、FR-02、FR-10-1、AC-05、`docs/development-spec.md` §3.3、§2.8、API-03、MOD-08。

### REV-06　注册 / 登录携带 `Idempotency-Key`（D-15）

- **状态**：已落地（前端调用链 + 键生命周期 + 后端 D-15 服务端语义 + 单测 / 集成用例）。
- **改动文件**：
  - 前端：`src/frontend/src/api/auth.ts`（`register` / `login` 增加 `Idempotency-Key` 请求头参数）、`src/frontend/src/stores/auth.ts`（键生命周期：`resolveSubmitKey(operation)` 首次生成 / 重试复用，`hasDefinitiveResult` 判定明确业务结果后作废）、`src/frontend/src/stores/__tests__/auth.spec.ts`（3 例）
  - 后端 Application：`Interfaces/Services.cs`（新增 `AuthIdempotencyEntry(int UserId, string RequestHash, bool Completed)` 与 `IAuthIdempotencyStore`：`TryAcquireAsync` / `TryGetAsync` / `CompleteAsync` / `ReleaseAsync`）、`Services/AuthService.cs`（注册 / 登录改为 `ExecuteIdempotentlyAsync` 编排 + `RegisterCoreAsync` / `LoginCoreAsync` 业务主体拆分 + `ComputeRequestFingerprint`）、`Interfaces/ApplicationServices.cs`（`IAuthService.RegisterAsync` / `LoginAsync` 增 `string? idempotencyKey` 参数）
  - 后端 Infrastructure：`Cache/RedisAuthIdempotencyStore.cs`（新增：key `draw:idempotency:auth:{operation}:{key}`，TTL 10 分钟，`SET NX` 占位 + `StringGet` 取结果 + `KeyDelete` 释放，Redis 不可用一律按「无幂等」放行）、`DependencyInjection.cs`（注册 `IAuthIdempotencyStore`）
  - 后端 Api：`Controllers/AuthController.cs`（注册 / 登录读取 `Idempotency-Key` 并经 `IdempotencyKeyValidator` 归一化）
  - 测试：`tests/unit/LuckyDraw.UnitTests/Services/AuthServiceTests.cs`（8 例 D-15：键缺失不触碰存储、成功写 `{userId,hash,completed}` 且不含凭证、业务拒绝释放占位不缓存、同键重放不重查密码 / 不重建用户 / 不重复审计、同键不同体 409、在途超时 409、注册侧重放）、`tests/integration/LuckyDraw.IntegrationTests/AuthIdempotencyTests.cs`（6 例：同键注册只建一用户 + 只一条 Register 审计、同键不同体 409、同键登录只一条 Login 审计、并发同键只建一用户且无 500、非法键 1002、缺键正常成功）
- **方案简述**：按 D-15 六条逐条落地。①作用域 `(operation, key)`，`operation ∈ {register, login}` 落在 Redis key 上，注册 / 登录互不串扰；②复用 MOD-05 抽象族与同一 Redis 缓存（同前缀 `draw:idempotency:`，仅追加 `auth:` 命名空间），**不新增表、不新增依赖**；③并发同键 = `SET NX` 占位（值含请求体指纹）→ 未抢到者每 100ms 轮询、≤2s 超时即 `409`「请勿重复提交」，占位被释放（前一次业务失败）时再次尝试接管执行；④缓存内容只有 `{ userId, requestHash, completed }`，重放时按 `userId` 重新签发 access / refresh（凭证可再生，不算业务副作用），因此不重复建用户 / 不重复审计 / 不重复累计失败计数；⑤仅缓存成功——`1101` / `1102` / `1103` / `1002` 走 `ReleaseAsync` 释放占位；⑥前端键语义见上。
  - **请求体指纹用 HMAC-SHA256 而非裸哈希**（对 D-15-1「请求体哈希」的实现选择）：登录请求体含密码，裸 SHA-256 落进 Redis 会变成可离线爆破的密码校验子；HMAC 密钥来自 `Jwt:SigningKey`（服务端配置，不入缓存），缓存内容因此不构成凭证泄露面。指纹同时用于「同键不同体立即 409」，避免同键换密码反而绕过密码校验拿到他人凭证。
  - **实现形态说明**：D-15-2 允许「按值类型泛化 `IIdempotencyStore`」。抽奖侧的 `IIdempotencyStore`（`(userId, key, DrawResponseDto)`）缺少认证所需的 `NX 占位 / 释放 / 短轮询` 原语（抽奖的强一致由 `DrawRequest` 唯一索引承担），强行泛化会改动已验证的 D-03 抽奖链路；故按 D-15-2 的允许范围在 Application 层新增**同族并列抽象** `IAuthIdempotencyStore`，共用同一 Redis 连接与 key 前缀，未新增表、未引入依赖。
- **影响面**：注册 / 登录新增可选的 Redis 依赖（不可用时降级为无幂等，认证链路不受阻）；重复提交 / 响应丢失重试不再产生第二次副作用；`IAuthService` 两个方法签名新增参数（仅 `AuthController` 一处调用点）。
- **回滚建议**：前端撤回 `api/auth.ts` 请求头参数与 `stores/auth.ts` 的 `submitKeys` / `resolveSubmitKey` / `discardSubmitKeyOnDefinitiveResult`；后端把 `AuthService` 的 `RegisterAsync` / `LoginAsync` 还原为无幂等版本（删除 `ExecuteIdempotentlyAsync` / `ReplayAsync` 等私有方法）、`AuthController` 去掉 header 参数、`IAuthService` 还原签名、删除 `IAuthIdempotencyStore` 与 `RedisAuthIdempotencyStore` 及 DI 注册。键为可选行为，回滚无破坏性（回滚即回到「重复提交可产生第二次副作用」状态）。
- **测试结果**：单测 86 通过 / 0 失败（含本项 8 例）；`auth.spec.ts` 12 例通过；集成用例见「本轮验证记录」。
- **关联**：REV-06、D-15、架构 §5.4、API-01 / API-02 请求头栏、规范 6.5、Q2 裁决。

### REV-03　抽奖限流改为按已认证用户分区

- **状态**：已落地。
- **改动文件**：`src/backend/src/LuckyDraw.Api/Program.cs`（新增 `ResolveDrawPartitionKey`，`RateLimitPolicies.Draw` 改用它；`ResolvePartitionKey` 保留给注册 / 登录）。
- **方案简述**：`RateLimitPolicies.Draw` 的 partitionKey 由 `draw:{RemoteIpAddress}` 改为「已认证用户」：取 `ClaimNames.UserId`（`sub`）拼 `draw:user:{userId}`；未认证（无 sub）回落 `draw:ip:{ip}` 保留「限流不可绕过」语义；注册 / 登录维持按 IP（叠加服务层按用户名的失败锁定）。限流中间件排在认证中间件之后（Program.cs 中间件顺序注释），策略求值时 claims 已就绪。
- **影响面**：同一出口 IP（NAT / 办公网 / 反代）后的多用户不再互相挤占 10 次/分额度；未认证请求行为不变。
- **回滚建议**：分区键改回 `$"draw:{ResolvePartitionKey(context)}"` 并删除 `ResolveDrawPartitionKey`（单文件两处）。
- **测试结果**：`dotnet build LuckyDraw.slnx` 0 warning / 0 error；行为验证依赖集成测试（单用户上限 1000 次/分，本机无法在测试中触发 429，未新增用例——限流维度在架构中由 Should-S3 覆盖）。
- **关联**：REV-03、API-07 认证栏、架构 §5.1 限流行、Should-S3、规范 8.6。

### REV-04　`Idempotency-Key` 格式校验 → 1002

- **状态**：已落地（含单测 + 集成负例）。
- **改动文件**：
  - `src/backend/src/LuckyDraw.Application/Common/IdempotencyKeyValidator.cs`（新增：空白 → null（无幂等）；长度 > 64 或非 UUID 形态 → `BusinessException(1002, "Idempotency-Key 格式非法（应为 UUID）")`；`[GeneratedRegex]` 校验 8-4-4-4-12 十六进制）
  - `src/backend/src/LuckyDraw.Api/Controllers/DrawController.cs`（入口先 `IdempotencyKeyValidator.Validate` 再进服务层）
  - `src/backend/src/LuckyDraw.Api/Controllers/AuthController.cs`（注册 / 登录同口径，见 REV-06）
  - `tests/unit/LuckyDraw.UnitTests/Validators/IdempotencyKeyValidatorTests.cs`（新增：缺失 → null、大小写 UUID 通过并归一化、5 类非法形态 → 1002、超长 → 1002）
  - `tests/integration/LuckyDraw.IntegrationTests/DrawApiTests.cs`（新增 3 例：非 UUID → 200 + 1002 且无流水、200 字符超长键 → 200 + 1002（不再落 1406 → 500）、合法 UUID 照常成功）
- **方案简述**：把「格式非法 → 1002」的分支放到业务层之前（校验器在 Application，控制器只做一次调用），长度判据先于形态判据，保证任何超长值都不会到达 varchar(64) 列；`Trim` 后校验，避免首尾空白被当作非法。未改动 `DrawService`（保持「缺失键 = 无幂等」语义）。
- **影响面**：抽奖接口对非法键由 500 变为 200 + 1002；合法 UUID 键与缺失键行为完全不变（集成测试对照例覆盖）。
- **回滚建议**：`DrawController` / `AuthController` 去掉 `IdempotencyKeyValidator.Validate` 调用（改回直传原始 header 值）并删除校验器与其用例。
- **测试结果**：单测（4 个方法 / 12 个用例）随 `dotnet test tests/unit/LuckyDraw.UnitTests` 全绿（见「本轮验证记录」）；集成 3 例见同表。
- **关联**：REV-04、API-07 请求头栏、架构 §5.1 提交行、`docs/error-codes.md` 1002 使用边界。

### REV-05　消除登录失败路径的账号枚举时序侧信道

- **状态**：已落地（含 4 个单测）。
- **改动文件**：
  - `src/backend/src/LuckyDraw.Application/Interfaces/Services.cs`（`IPasswordHasher` 新增 `VerifyOrDummy(string password, string? passwordHash)`，`Verify` 语义不变）
  - `src/backend/src/LuckyDraw.Infrastructure/Security/BcryptPasswordHasher.cs`（实现 `VerifyOrDummy`：`passwordHash` 为空时对**静态假哈希**执行一次同工作因子（11）校验后返回 false；假哈希的明文由 `RandomNumberGenerator` 生成后即丢弃，无对应账号，不存在「猜中假哈希口令而误判成功」的路径）
  - `src/backend/src/LuckyDraw.Application/Services/AuthService.cs`（`var passwordMatched = user is not null && _passwordHasher.Verify(...)` → `_passwordHasher.VerifyOrDummy(request.Password, user?.PasswordHash)`；`if (!passwordMatched)` → `if (user is null || !passwordMatched)` 以消除可空抑制）
  - `tests/unit/LuckyDraw.UnitTests/Services/AuthServiceTests.cs`（新增 4 例：用户不存在仍恰好调用一次等价校验且失败面与「密码错误」逐字一致、密码错误走真实哈希、锁定态不进密码校验、成功路径签发凭证）
- **方案简述**：把「用户存在性」与「密码正确性」解耦——校验入口不再因 `user is null` 短路，两种失败路径都付出一次 BCrypt 校验成本，耗时差被抹平；对外错误码（1102）与文案（「用户名或密码错误」）不变，AC-04 语义不变。假哈希放在 Infrastructure 实现内（Application 不引用 BCrypt.Net，避免跨层引入加密库），工作因子与生产哈希一致，避免「假哈希成本低于真哈希」反而造成新的时序差。
- **影响面**：登录失败路径多一次 BCrypt 校验（约与成功路径同量级，仍在 ≤350ms 预算内）；`IPasswordHasher` 接口新增成员（仅一个实现、一处调用点，无其它实现类需要同步）。
- **回滚建议**：`AuthService.cs` 改回 `user is not null && _passwordHasher.Verify(...)`（一行），并撤回 `IPasswordHasher.VerifyOrDummy` 与其实现、删除新增单测（回滚即回到账号枚举时序侧信道状态，不推荐）。
- **测试结果**：`dotnet build LuckyDraw.slnx` 成功（0 warning / 0 error）；`dotnet test tests/unit/LuckyDraw.UnitTests/LuckyDraw.UnitTests.csproj` → **通过 68 / 失败 0 / 跳过 0**（含本次新增 4 例，170ms）。
- **关联**：REV-05、FR-02、AC-04、`docs/error-codes.md` 1102 边界。

### REV-07　`resetAt` 落地 Should-S2（次数用尽文案含精确重置时间）

- **状态**：已落地。
- **改动文件**：`src/frontend/src/composables/useDrawFlow.ts`（透出 `resetAt`）、`src/frontend/src/views/draw/DrawView.vue`（`page-draw--noquota` 的 AlertDescription 展示「重置时间：yyyy-MM-dd HH:mm（UTC+8）」）、`src/frontend/src/utils/messages.ts`（`draw.quotaResetHint`）、`src/frontend/src/utils/datetime.ts`（分钟精度复用）。
- **方案简述**：`resetAt` 由 API-06 全链路透出到视图，按 UTC+8 渲染为 `yyyy-MM-dd HH:mm`；`resetAt` 未知（查询失败）时降级为原固定文案「今日剩余次数：0 次」，不展示错误的时间。锚点 12 的标题文案保持冻结口径不变（新增信息落在描述行）。
- **影响面**：抽奖页次数用尽态的描述文案；锚点 12 触发条件不变。
- **回滚建议**：`loadQuota` 去掉 `resetAt` 赋值、`DrawView` 的 AlertDescription 改回固定文案（两文件）。
- **测试结果**：见本节末尾「本轮验证记录」。
- **关联**：REV-07、Should-S2、API-06、PRD §6、FR-04。

### REV-09　开发态连接串口令与签名密钥移出仓库

- **状态**：已落地。
- **改动文件**：
  - `src/backend/src/LuckyDraw.Api/appsettings.Development.json`（删除 `ConnectionStrings:Default` 整段（原含 `Password=devonly`）与 `Jwt:SigningKey`（原 `dev-only-placeholder-signing-key-override-in-deployment`）；保留非敏感项：Redis 地址、CORS 白名单、抽奖 / 奖品配置、Serilog 级别）
  - `src/backend/src/LuckyDraw.Api/LuckyDraw.Api.csproj`（新增 `<UserSecretsId>luckydraw-api-9f4c1e27-5b83-4d61-a70e-6c2f18d3b945</UserSecretsId>`，使 `dotnet user-secrets` 可用；`WebApplicationBuilder` 在 Development 下自动加载 user-secrets，其优先级高于 appsettings.*.json）
  - `tests/integration/LuckyDraw.IntegrationTests/LuckyDrawApiFactory.cs`（连接串 / Redis 地址改为「环境变量可覆盖 + 本地容器默认值」：`LUCKDRAW_TEST_CONNECTION_STRING` / `LUCKDRAW_TEST_REDIS`）
- **方案简述**：按附录 B B6：`ConnectionStrings:Default` 与 `Jwt:SigningKey` 走 `dotnet user-secrets`（开发）/ 环境变量（生产），仓库内只留空值（`appsettings.json` 的 `""`）与非敏感项。**测试侧注入路径未变**：`LuckyDrawApiFactory.ConfigureWebHost` 通过环境变量 `ConnectionStrings__Default` / `Redis__Configuration` 注入（**不是** `ConfigureAppConfiguration`，避免绕过 D-06 启动校验），因此集成测试不读取 `appsettings.Development.json`；本项实测 38 个集成用例全绿，证明该注入路径成立。
- **本地启动步骤（一次即可）**：`cd src/backend/src/LuckyDraw.Api && dotnet user-secrets set "ConnectionStrings:Default" "Server=localhost;Port=3307;Database=luckydraw_dev;User Id=root;Password=<本机容器口令>;CharSet=utf8mb4;SslMode=None;AllowPublicKeyRetrieval=True"` 与 `dotnet user-secrets set "Jwt:SigningKey" "<≥32 位随机串>"`（或分别设环境变量 `ConnectionStrings__Default` / `Jwt__SigningKey`）。未设置时应用按既有行为立即失败并给出「未配置数据库连接串 ConnectionStrings:Default。」提示。
- **影响面（如实登记）**：①开发态不再能零配置启动，须先执行上述一次配置；②`Jwt:SigningKey` 移除后，Development 下若未配置，`Program.cs` 的既有回退（全零 32 位密钥）会被实际走到——该回退属 REV-10 范围，本轮**未改动**，按用户裁决仅记录不修；建议本地按上文显式配置以避开。
- **回滚建议**：恢复 `appsettings.Development.json` 中的口令段与开发签名密钥、撤回 csproj 的 `UserSecretsId`（不推荐；回滚即回到 REV-09 缺陷状态）。
- **测试结果**：`dotnet test tests/integration/LuckyDraw.IntegrationTests --nologo` → 通过 38 / 失败 0（实跑，容器在跑），见「本轮验证记录」。
- **关联**：REV-09、架构附录 B B6 / B5、规范 8.4、CLAUDE.md 安全红线。

> 遗留说明：`tests/integration/LuckyDraw.IntegrationTests/LuckyDrawApiFactory.cs` 仍保留本地开发容器默认口令 `devonly`（127.0.0.1:3307 的一次性测试容器，非部署凭证），目的是让集成测试开箱可跑；如需彻底外置，CI 侧注入 `LUCKDRAW_TEST_CONNECTION_STRING` 即可覆盖。

### REV-19　补 CHG-11 重试路径回归（单测 + 集成负例）

- **状态**：已落地。
- **改动文件**：
  - `tests/unit/LuckyDraw.UnitTests/Services/DrawServiceTests.cs`（新增 3 例：①瞬时错误后成功 → 整事务重试；②瞬时错误持续 → 3 次后降级 `1001`；③回滚失败不掩盖原始异常）
  - `tests/integration/LuckyDraw.IntegrationTests/ConcurrencyTests.cs`（新增负例：同用户 12 并发抽奖不出现 500 / 「系统内部错误」，业务码仅允许 0 / 1001 / 1501 / 409，且成功次数仍受每日上限约束）
- **方案简述**：用替身仓储在首个事务内抛 `TransientDataException`（`IDrawRequestRepository.TryAddAsync` 首调抛、次调成功）构造两条路径，断言 `TransactionManager.BeginAsync` 恰好调用 2 / 3 次（= 整个事务被重开，而非局部重试）、失败事务已 `RollbackAsync`、成功那次只写一条审计、耗尽后 `BusinessException(1001, "系统繁忙，请稍后重试")` 且无记录 / 审计副作用；回滚失败用一个必然回滚的场景（扣次影响 0 行 → 1501）+ `RollbackAsync` 抛异常构造，断言上抛的仍是 1501 而非回滚异常。集成侧不构造真实死锁（不可稳定复现），改为断言「高并发下不得出现 500」，覆盖 CHG-11 修复的目标语义。
- **回滚建议**：删除两组新增用例（测试文件独立回退，不影响产品代码）。
- **测试结果**：单测 89 通过 / 0 失败（含本项 3 例）；集成 38 通过 / 0 失败（含本项负例），见「本轮验证记录」。
- **关联**：REV-19、CHG-11、架构 §5.2 API-07 错误语义、规范 7.1。

### 本轮验证记录（实际命令与结果数字）

| 命令 | 结果 | 备注 |
| --- | --- | --- |
| `dotnet build LuckyDraw.slnx`（于 `src/backend/`） | 已成功生成，**0 Warning / 0 Error** | — |
| `dotnet test tests/unit/LuckyDraw.UnitTests --nologo` | **通过 89 / 失败 0 / 跳过 0 / 总计 89**，261 ms | 含 REV-19 重试 3 例、D-15 幂等 12 例、键校验 12 例 |
| `dotnet test tests/integration/LuckyDraw.IntegrationTests --nologo` | **通过 38 / 失败 0 / 跳过 0 / 总计 38**，9 s | **实跑（非跳过）**：MySQL 3307 + Redis 6379 容器均在运行 |
| `node node_modules/vue-tsc/bin/vue-tsc.js --noEmit` | 退出码 0，无输出 | 于 `src/frontend/` |
| `node node_modules/vite/bin/vite.js build` | `✓ built in 737ms` | 于 `src/frontend/` |
| `node node_modules/eslint/bin/eslint.js . --max-warnings 0` | 退出码 0，无输出 | 于 `src/frontend/` |
| `node node_modules/vitest/vitest.mjs run` | **Test Files 6 passed (6) / Tests 52 passed (52)**，1.47 s | 于 `src/frontend/` |

---

## CHG-14 缺陷闭环：BUG-01 / BUG-02 / BUG-03（同一轮修复）

- **状态**：**已落地（3/3）**，实跑验证见本节末「本轮验证记录」（后端 build / 单测 / 集成测试、前端 vue-tsc / vitest / eslint 全部实跑，数字为真实输出）。
- **背景**：`docs/51-defects.md` v1 的三条未闭环缺陷由用户裁决**同轮修复**（原任务书仅授权 BUG-01，BUG-02 / BUG-03 经用户批准后扩权纳入，故合并于同一 CHG 编号，不另开条目）。
- **上游契约变更**：**无**。三条均为实现未跟上既有契约，`docs/30-architecture.md` / `docs/50-testcases.md` / `docs/20-prototype.html` / `docs/error-codes.md` 全部未改。

### BUG-01　（一般，`未闭环`）认证侧幂等冲突返回 HTTP 409

- **关联 TC**：TC-08（步骤 3）、TC-17（步骤 3）。
- **缺陷现象**：注册 / 登录在「同键异体」时返回 **HTTP 200 + body `code=409`**，契约要求 **HTTP 409**（`docs/30-architecture.md` §5.1、§5.4、D-15）。
- **根因**（已复核确认）：`BusinessException(int code, string message, int httpStatusCode = 200)` 的第 3 参数默认为 200，`ExceptionFilter.cs:40` 直接以 `businessException.HttpStatusCode` 作 HTTP 状态码。**抽奖侧传了第 3 参数**（`DrawService.cs:264`、`DrawRequestRepository.cs:41` → HTTP 409），**认证侧两处未传**（`AuthService.cs:221`、`:258`）→ 同一业务码在两条链路上映射出不同 HTTP 状态码。属实现遗漏，契约（§5.1 / §5.4 / D-15）**无需修改**。
- **改动文件（生产）**：`src/backend/src/LuckyDraw.Application/Services/AuthService.cs` —— 原第 221 行（「在途占位超时」）与第 258 行（「同键异构体」）两处 `throw`，各补第 3 参数 `ErrorCodes.IdempotencyConflict`，与抽奖侧逐字同写法；**改动后**因各增一行注释，两处 `throw` 位移至第 **222** / **260** 行。**仅此一个生产文件、两行实质改动**，未触碰 `body.code` 取值、未动 `ExceptionFilter` 通用映射、未动 `ErrorCodes` 常量。
- **改动文件（测试）**：
  - **漏检原因（如实记录）**：集成用例 `AuthIdempotencyTests.Register_WithSameKeyDifferentBody_ReportsConflict` 原先把**错误行为写成了断言** —— `response.StatusCode.Should().Be(HttpStatusCode.OK)`，其文档注释甚至写着「HTTP 200 + 业务码，D-12」。即缺陷不是「没测」，而是**测试把缺陷固化了**（只断言 body、不断言 HTTP 状态码，而状态码恰是 BUG-01 的判据）。
  - `tests/integration/LuckyDraw.IntegrationTests/AuthIdempotencyTests.cs`：① 上述用例改判 `HttpStatusCode.Conflict` 并补「冲突响应不得携带凭证」；② 并发同键注册用例的循环断言由「一律 200」改为 **HTTP 状态码与业务码配对**（0 ↔ 200、409 ↔ 409），否则修复后该用例会变成不稳定用例；③ **新增** `Login_WithSameKeyDifferentBody_Returns409`（TC-17 步骤 3 的登录侧集成覆盖，此前缺失）。
  - `tests/integration/LuckyDraw.IntegrationTests/IntegrationFixture.cs`：`ShouldBeCode` 的文档注释改为「业务异常默认 200，幂等冲突 409 例外」——原注释「业务错误一律 HTTP 200」正是本缺陷的认知来源（仅注释，无行为变化）。
  - `tests/unit/LuckyDraw.UnitTests/Services/AuthServiceTests.cs`：两条幂等冲突单测（`LoginAsync_OnSameKeyDifferentBody_ThrowsConflict`、`LoginAsync_WhenPlaceholderNeverSettles_ThrowsConflictAfterTimeout`）在原有 `Code == 409` 之外补断 `HttpStatusCode == 409`，把两处抛点都钉住。
- **回滚建议**：把上述两处 `throw` 的第 3 参数删掉即回到改动前状态（生产单文件两行，无连带、无数据变更）；测试侧回退需同时恢复被改写的断言，否则测试会立刻转红（这正是期望的守卫）。
- **测试结果**（实跑，见本节末「本轮验证记录」）：
  - `dotnet test tests/unit/LuckyDraw.UnitTests` → **通过 89 / 失败 0 / 跳过 0**（含改后补断 `HttpStatusCode == 409` 的两条幂等冲突用例）。
  - `dotnet test tests/integration/LuckyDraw.IntegrationTests` → **通过 39 / 失败 0 / 跳过 0**（真实 MySQL 8.4.11 + Redis；含改写后的注册 409 用例、并发同键的「状态码—业务码配对」断言、**新增**的登录侧 409 用例）。
  - **负向验证（实测，证明测试确实能抓到本缺陷）**：临时撤掉 `AuthService.cs` 两处 `throw` 的第 3 参数后，`dotnet test ... --filter "FullyQualifiedName~DifferentBody"` → **失败 2 / 通过 0**，两条用例的报错均为 `Expected response.StatusCode to be HttpStatusCode.Conflict {value: 409} ... but found HttpStatusCode.OK {value: 200}.`，与 BUG-01 现象（HTTP 200 + body 409）逐字对应；随后**已恢复**修复，并重跑全量（上表数字为恢复后的实跑结果）。
- **关联**：BUG-01、TC-08、TC-17、D-15、架构 §5.1 / §5.4。

### BUG-02　（建议，`未闭环`）`/api/v1/records` 的 `winTime` 补 UTC 时区标识

- **关联 TC**：TC-63。
- **缺陷现象**：`winTime = "2026-09-17T06:51:42.397853"` —— 无 `Z` / 无 `+00:00`，而契约要求 ISO 8601（UTC）自描述（架构 §5.1「时间 `ISO 8601 字符串（UTC）`」、API-08 行）。
- **根因**：`WinningRecordDto.WinTime` 为 `DateTime`，值来自 MySQL `DATETIME` 列 `WinningRecord.CreateTime`，连接器读回 `Kind = Unspecified`，`System.Text.Json` 对 `Unspecified` **不输出任何时区标识**。对照 API-06 的 `resetAt` 是 `DateTimeOffset` → 输出 `+00:00`，两处口径本就不一致。
- **修复方式与爆炸半径（按要求补充说明）**：
  - **改的是哪一个属性**：**仅** `WinningRecordDto.WinTime` 一个属性（`src/backend/src/LuckyDraw.Application/Dtos/RecordDtos.cs`），类型由 `DateTime` 改为 `DateTimeOffset`，并在**唯一**构造点 `WinningRecordRepository.QueryAsync` 的投影里显式标注 UTC：`WinTime = new DateTimeOffset(DateTime.SpecifyKind(x.CreateTime, DateTimeKind.Utc))`。
  - **为什么不是全局**：**未**在 `OnModelCreating` 挂任何 `DateTime` 值转换器，**未**注册全局 JSON 转换器，**未**改 `JsonSerializerOptions`。全局方案会把所有接口的所有时间字段一并改动（含 `AuditLog`、`DrawRequest` 等），爆炸半径远超 BUG-02，且属未经确认的大范围接口行为变更，故不采用。
  - **是否影响其他接口的时间字段**：**否**。`WinningRecordDto` 只被 `GET /api/v1/records`（API-08）使用（全仓唯一构造点即上述仓储投影）。`resetAt`（API-06）本就是 `DateTimeOffset`，未改动；其余接口无对外时间字段。库表结构、迁移、实体、`AppDbContext` 映射均未改动。
  - **选 `DateTimeOffset` 而非 `DateTime` + `Kind=Utc` 的理由**：与既有 `resetAt` 的表达方式一致，使两个对外的 UTC 时间字段线上格式统一（`+00:00`），直接消除缺陷报告指出的「两处口径不一致」。
- **前端连带确认**：`src/frontend/src/utils/datetime.ts` 的 `parseUtc()` 先做毫秒截断、再用 `/(?:z|[+-]\d{2}:?\d{2})$/i` 判断是否已有时区标识，**已带标识时原样 `new Date()`，不会拼出 `...ZZ`**（读码确认 + 新增单测实测）。
- **改动文件（生产）**：`src/backend/src/LuckyDraw.Application/Dtos/RecordDtos.cs`、`src/backend/src/LuckyDraw.Infrastructure/Repositories/WinningRecordRepository.cs`（各 1 处 + 注释）。
- **改动文件（测试）**：`tests/integration/LuckyDraw.IntegrationTests/DrawApiTests.cs`（`Draw_OnGuaranteedWin_WritesRecordVisibleToOwnerOnly` 补断 winTime 携带 `Z` / `+00:00` 且偏移为 0）、`src/frontend/src/utils/__tests__/datetime.spec.ts`（新增 1 例：`+00:00` 与 6 位毫秒形态不得被二次拼 `Z`，UTC+8 展示正确）。
- **回滚建议**：`WinTime` 改回 `DateTime` 并把投影还原为 `WinTime = x.CreateTime`（两个文件各一行）；前端无需改动（`parseUtc` 对本修复前后两种形态都兼容）。
- **测试结果**（实跑）：
  - 集成用例 `Draw_OnGuaranteedWin_WritesRecordVisibleToOwnerOnly`（已补断 winTime 时区标识）**通过** → 证明真实 MySQL 落库值经 API-08 返回时带 `+00:00` 且偏移为 0，且投影内的 `DateTime.SpecifyKind` 客户端求值路径可用（未触发 EF 翻译错误、未产生告警）。
  - 前端 `node node_modules/vitest/vitest.mjs run src/utils/__tests__/datetime.spec.ts` 随全量 **54 通过** → `+00:00` 与 6 位毫秒形态**不会被二次拼接 `Z`**，UTC+8 展示仍为 `2026-09-17 14:51`。
  - **未做（如实说明）**：E2E 侧 TC-63 属 test-executor 范围，本轮未执行（`tests/e2e/**` 不在本角色写入范围）。
- **关联**：BUG-02、TC-63、架构 §5.1 时间口径 / API-08。

### BUG-03　（建议，`未闭环`）抽奖页奖池错误态标题改为「奖池加载失败」

- **关联 TC**：TC-30。
- **缺陷现象**：抽奖页 `page-draw--error` 的 Alert 标题为「加载失败」，原型状态定义表冻结为「奖池加载失败」。
- **关键约束（避免改错地方）**：`messages.common.loadFailedTitle: '加载失败'` 是**通用键**，记录页也在用；而记录页的原型口径**就是**「加载失败」（原型状态定义表记录页 `error` 行）。**两页文案本就不同**，因此**不能**改通用键。
- **修复方式**：新增专用键 `messages.prizes.loadFailedTitle: '奖池加载失败'`（`src/frontend/src/utils/messages.ts`），仅抽奖页错误态（`DrawView.vue`）改用它；`common.loadFailedTitle` **保持 `'加载失败'` 不变**，记录页（`WinningRecordsView.vue`）与全局错误边界（`App.vue`）行为完全不变。
- **原型自身不一致的处理**：原型文末的文案清单表把两页并列成同一句，与状态定义表冲突 —— 以**状态定义表**为准（其逐行给出 `page-draw--error` / `page-records--error` 各自的冻结文案）。**未**据此修改记录页，**未**改动原型文件。
- **改动文件（生产）**：`src/frontend/src/utils/messages.ts`（新增 `prizes` 分组，1 个键）、`src/frontend/src/views/draw/DrawView.vue`（第 145 行标题引用该键）。
- **改动文件（测试）**：`src/frontend/src/utils/__tests__/messages.spec.ts`（**新增文件**，1 例：同时钉住 `prizes.loadFailedTitle === '奖池加载失败'` 与 `common.loadFailedTitle === '加载失败'`，防止后人再次「合并为一个键」）。既有 `useDrawFlow.spec.ts` 的奖池失败用例断言的是 `poolError`（描述行）而非标题，**未受影响、无需改动**。
- **回滚建议**：`DrawView.vue` 标题改回 `messages.common.loadFailedTitle`，并删除 `messages.prizes` 分组与新增的 `messages.spec.ts`（回滚即退回「加载失败」的偏差文案）。
- **测试结果**（实跑）：
  - 新增 `src/utils/__tests__/messages.spec.ts` **通过**（同时钉住 `prizes.loadFailedTitle` 与 `common.loadFailedTitle` 两个不同取值）。
  - 既有 `useDrawFlow.spec.ts` 的「奖池加载失败进入 error 态」用例**通过且无需改动**（其断言对象是 `poolError` 描述行，不是标题）。
  - 全量前端校验：`vue-tsc --noEmit` **退出码 0**；`vitest run` **7 文件 / 54 用例通过**；`eslint . --max-warnings 0` **退出码 0**。
  - **未做（如实说明）**：`DrawView.vue` 的标题文案属模板层，本轮沿用 CHG-10 既定口径不加组件渲染测试；其 DOM 断言由 E2E 的 TC-30 覆盖（`tests/e2e/**` 归 test-executor）。
- **关联**：BUG-03、TC-30、原型状态定义表（抽奖页 `error` / 记录页 `error`）、FR-03 / FR-10-3。

### 本轮验证记录（实际命令与结果，均在 CHG-14 三条改动落地后执行）

| 命令 | 结果 | 备注 |
| --- | --- | --- |
| `dotnet build LuckyDraw.slnx`（于 `src/backend/`） | 已成功生成，**0 Warning / 0 Error** | 首次执行曾因 `:5180` 开发服务器占用 DLL 失败（`MSB3027`），停进程后重跑通过 |
| `dotnet test tests/unit/LuckyDraw.UnitTests --nologo` | **通过 89 / 失败 0 / 跳过 0 / 总计 89**，276 ms | 两处幂等冲突抛点均已补断 `HttpStatusCode == 409` |
| `dotnet test tests/integration/LuckyDraw.IntegrationTests --nologo` | **通过 39 / 失败 0 / 跳过 0 / 总计 39**，9 s | **实跑（非跳过）**：MySQL 3307 + Redis 6379 容器 `Up`；较 CHG-13 的 38 例净增 1 例（登录侧 409） |
| `dotnet test ... --filter "FullyQualifiedName~DifferentBody"`（**负向验证**：临时撤掉修复） | **失败 2 / 通过 0** | 报错 `Expected ... Conflict {value: 409} ... but found ... OK {value: 200}`；验证后已恢复修复并重跑全量 |
| `dotnet format whitespace --verify-no-changes`（**于 6 个项目目录分别执行**） | Domain / Application / Api / UnitTests / IntegrationTests → **exit 0**；**Infrastructure → exit 2**（45 处，**全部**在 `Data/AppDbContext.cs`） | 详见下方「已知遗留」 |
| `node node_modules/vue-tsc/bin/vue-tsc.js --noEmit` | **退出码 0**，无输出 | 于 `src/frontend/` |
| `node node_modules/vitest/vitest.mjs run` | **Test Files 7 passed (7) / Tests 54 passed (54)**，1.52 s | 较 CHG-13 的 6 文件 52 例：新增 `messages.spec.ts`（1 例）与 `datetime.spec.ts`（1 例） |
| `node node_modules/eslint/bin/eslint.js . --max-warnings 0` | **退出码 0**，无输出 | 于 `src/frontend/` |

### 已知遗留（**非本轮引入，未擅自修改**）

- **`src/backend/src/LuckyDraw.Infrastructure/Data/AppDbContext.cs` 存在 45 处既有空格格式违规**（`dotnet format whitespace` 报 `WHITESPACE`），集中在第 92–122 行 `HasData` 种子数据块（对象初始化器把多个属性写在同一行）。该文件**本轮未被改动**（本轮 Infrastructure 侧只改了 `Repositories/WinningRecordRepository.cs`，格式检查为 exit 0），且报错行落在 CHG-03 引入的种子数据代码上；此前未被发现的原因正是 CLAUDE.md 明确警告的坑：`dotnet format` 在 `.slnx` 工作区根目录执行会**打印 help 并以退出码 0 结束**，所以历次验证记录里都没有按项目目录跑过格式检查（见 CHG-13「本轮验证记录」亦无该行）。
- **未修改**该文件（属本轮三项缺陷之外的既有格式债，改动它是未经授权的无关改动）。**修复方式**：于 `src/backend/src/LuckyDraw.Infrastructure/` 执行 `dotnet format whitespace`（纯空白改动，无行为变化），建议由主对话另行裁决。

---

## CHG-15 Infrastructure：`AppDbContext.cs` 种子块纯空白格式化（REV-20 闭环）

- **状态**：**已落地**。格式门禁由红转绿；下表全部为实跑输出（命令 + 工作目录 + 实测结果）。
- **来源与裁决**：`docs/60-review.md` **v3** §D **REV-20**（建议级，未闭环）——Infrastructure 项目 `dotnet format whitespace --verify-no-changes` 为 exit 2、45 处 `WHITESPACE`。经**用户裁决「派 engineer 修（推荐）」**后执行。该问题**非本轮引入**（种子块源自 CHG-03），CHG-14 已如实披露并选择不擅自修改（见上节「已知遗留」）。
- **改动文件（唯一）**：`src/backend/src/LuckyDraw.Infrastructure/Data/AppDbContext.cs`。
- **改动内容与行范围（实测）**：`ConfigurePrizeItem` 内 `HasData` 种子块（改前第 **92–122** 行；`--verify-no-changes` 实测报错位置为第 **95–121** 行，15 个行号 × 3 列 = 45 处）。5 个 `new PrizeItem { … }` 对象初始化器由「多个属性挤在同一行」改为「每行一个属性」（改后块占第 92–167 行）。文件 191 行 → 236 行，字节 9255 → 10200（sha1：`34a7807…` → `a2f19b8…`）。改动**全部由格式化工具生成**，无任何手改（`src/backend` 与仓库根均无 `.editorconfig`，即采用 `dotnet format` 默认换行选项）。
- **方案简述（纯空白、零行为改变，已用字节级比对证明）**：以 `tr -d '[:space:]'` 剥离改前快照与改后文件的**全部空白字符**后 `cmp` 逐字节比对 → **完全一致（各 6953 字节）**。即除空白字符外无任何字节变化：标识符、字面量（含中文奖品名）、token 顺序、注释全部原样；`HasData` 种子取值（5 条奖池、`SeedTime`）逐项未动；**不新增迁移、不改模型快照、不触碰数据库**。
- **验证（全部实跑；命令 / 工作目录 / 实测结果）**：

  | # | 命令 | 工作目录 | 实测结果 |
  | --- | --- | --- | --- |
  | 1 | `dotnet format whitespace --verify-no-changes`（**改前**基线复现） | `src/backend/src/LuckyDraw.Infrastructure` | **exit 2**；45 处 `WHITESPACE`，全部位于 `Data/AppDbContext.cs`，实测行 95–121（与 60 v3 §D、52 OBS-10 的数字一致） |
  | 2 | `dotnet format whitespace`（修复；**未加** `--verify-no-changes`） | 同上 | exit 0，无输出 |
  | 3 | `dotnet format whitespace --verify-no-changes`（**改后**） | 同上 | **exit 0**，无输出 |
  | 4 | `dotnet build` | 同上 | **0 警告 / 0 错误**，exit 0（Domain / Application / Infrastructure 一并构建） |
  | 5 | `dotnet test tests/unit/LuckyDraw.UnitTests --nologo` | 仓库根 | **通过 89 / 失败 0 / 跳过 0**（276 ms），exit 0 —— 与 CHG-14 记录数字一致 |
  | 6 | `dotnet build tests/integration/LuckyDraw.IntegrationTests … -p:OutputPath=<临时目录> -m:1`，再 `dotnet test … --no-build -p:OutputPath=<临时目录>` | 仓库根 | **通过 39 / 失败 0 / 跳过 0**（10 s，真实 MySQL 8.4.11 + Redis），exit 0 —— 与 CHG-14 记录数字一致（临时输出目录的原因见下条） |
  | 7 | `dotnet build --output <临时目录>`（Api 编译级确认） | `src/backend/src/LuckyDraw.Api` | **0 警告 / 0 错误**，exit 0 |

  工作目录口径：按 CLAUDE.md 告诫，格式检查**进到项目目录执行**（`.slnx` 工作区根目录执行会打印 help 并以 exit 0 静默结束）；`whitespace --verify-no-changes` 未使用 `--nologo`（会被当成文件路径）。
- **`LuckyDraw.Api/bin` 文件锁（如实记录，未杀进程）**：验证期间 `:5180` 上运行中的开发服务器（PID 4308，映像路径 `src/backend/src/LuckyDraw.Api/bin/Debug/net10.0/LuckyDraw.Api.exe`）锁定了 Api 输出目录。向**常规输出目录**构建 Api / 集成测试项目时失败，错误原文（关键片段逐字）：`error MSB3027: 无法将“…\LuckyDraw.Infrastructure.dll”复制到“bin\Debug\net10.0\LuckyDraw.Infrastructure.dll”。超出了重试计数 10。失败。文件被“LuckyDraw.Api (4308)”锁定。` 与 `error MSB3021: 无法将文件“…\LuckyDraw.Infrastructure.dll”复制到“bin\Debug\net10.0\LuckyDraw.Infrastructure.dll”。The process cannot access the file '…' because it is being used by another process.`（另有 10 条 `MSB3026` 重试警告）。按任务约束**未终止该进程**；改用临时输出路径（`--output` / `-p:OutputPath=`，写入系统临时目录）完成 Api 编译确认与集成测试构建 / 运行——**该路径不触及 `LuckyDraw.Api/bin`，与 `:5180` 进程互不干扰**。连带事实：`LuckyDraw.Api/bin/Debug/net10.0/` 内的产物仍为改动前版本（本次为纯空白变更，行为等价）；如需刷新为本次构建，应在停服后于 Api 目录执行一次 `dotnet build`（由主对话择机调度）。
- **改动范围核对（`git diff` 无法佐证，如实说明）**：`src/` 在本仓库为**未跟踪（untracked）**状态（`git ls-files` 对该文件输出为空），`git diff --stat` 只显示 6 个**对话开始前即存在**的 tracked 修改（`.claude/settings.json`、`.gitignore`、`CLAUDE.md`、`docs/development-spec.md`、`docs/error-codes.md`、`docs/state-machine.md`），**均非本次产生**。替代佐证：① 剥离空白后的 `cmp` 逐字节一致（见上）；② 按修改时间扫描 `src/backend` 全目录（排除 bin/obj），本轮仅 `Data/AppDbContext.cs` 一个源文件被改动；③ 未触碰任何 `appsettings*.json` / `launchSettings.json`（扫描结果中无）。
- **回滚建议**：用改动前快照（或等价的行布局）覆盖 `Data/AppDbContext.cs` 即可回退；**无数据库 / 契约 / 行为连带**（无新增迁移、无 DTO 变更）。回滚后 Infrastructure 项目格式门禁会重新转红（45 处）——这是期望的守卫，不是缺陷。
- **失效传播判定（按 `docs/artifacts.md` §5 矩阵；结论供主对话调度）**：
  - **`40-changelog 变更` → `51-defects` / `52-qa-report` / `60-review`（矩阵要求重跑或人工复核）**：
    - `60-review`：**需回写**——REV-20 状态应由「未闭环」改为「已闭环」（涉及 §A 判定表、§D、统计行与头部依赖），由 code-reviewer 独立复跑 `--verify-no-changes` 后回写（预期 exit 0）。
    - `52-qa-report`：**需回写**——其 §3 `OBS-10` 记录的「格式门禁为红」已成历史，应更新为「已消除 / 门禁转绿」（实测：45 处 → 0 处、exit 2 → exit 0）。
    - `51-defects`：**无需变更**——其正文已明确 REV-20「不登记为 BUG、不进本台账」（格式门禁发现、非测试发现的缺陷）；本次闭环不新增 BUG、不关闭任何已登记 BUG，建议复核时顺带确认该口径未变。
  - **`src/ 变更` → `tests/unit/**` / `tests/integration/**` / `tests/e2e/**`（矩阵要求重跑）**：
    - `tests/unit/**`：**已在本轮实跑**（89 / 0 / 0，与改前一致，见上表 #5），无需再跑。
    - `tests/integration/**`：**已在本轮实跑**（39 / 0 / 0，与改前一致，见上表 #6），无需再跑。
    - `tests/e2e/**`：**本角色判定无需重跑**——本次为纯空白变更（剥离空白后字节一致），运行时行为、DOM 结构、API 契约与数据零变化，E2E 断言对象均不受影响；且 `tests/e2e/**` 归 test-executor（不在本角色范围），如需从严按矩阵执行由主对话调度。E2E 所依赖的 `:5180` 进程运行的是改动前二进制（行为等价），重跑不受阻。
- **关联**：REV-20、CHG-03（种子块来源）、CHG-14「已知遗留」、`docs/artifacts.md` §5 失效传播矩阵、CLAUDE.md「一、常用命令」`dotnet format` 告诫、交付自查清单「格式检查」项。

---

## CHG-16 缺陷闭环：BUG-04（结果条目不在快照时重拉奖池 + 兜底文案）/ BUG-05（抽奖事务后半段瞬时错误分类 + 命令超时口径 + CRITICAL 告警）

- **状态**：**已落地（2/2）**，下表全部为实跑输出（实际命令 + 工作目录 + 实测结果）。
- **来源与裁决**：`docs/51-defects.md` §2 两条未闭环缺陷（BUG-04 建议级 / BUG-05 一般级）经**用户裁决「两条都修」**后由本角色执行（第 7 步缺陷闭环 a 阶段）。BUG-05 的修复方向由 `docs/52-qa-report.md` §1.6.6 与 QX-E-09 明示：**仅补 ⑥⑦⑧ 的 catch 不足以修复**，须同时处置「`CommandTimeout=30` 早于 `innodb_lock_wait_timeout=50`」的第二层根因 → 本轮三层一起做（分类 + 命令超时 + 告警）。
- **上游契约变更**：**无**。两条均为实现未跟上既有契约；`docs/10-prd.md` / `docs/20-prototype.html` / `docs/30-architecture.md` / `docs/error-codes.md` / `docs/50-testcases.md` 逐字未改。**终态错误码与重试次数两处契约冲突只上报不裁决**（逐字引用见本节末「未裁决项」）。
- **未触发「必须确认的场景」**：无新第三方依赖、无数据库表结构变更、无破坏性接口变更、**未改任何后端 DTO / 接口数据结构**（BUG-04 复用既有 API-05，未新增字段）。

### BUG-04　（建议，`未闭环`）结果条目不在当前奖池快照时重拉奖池一次再定位

- **关联 TC**：TC-48c（E2E；`tests/e2e/**` 归 test-executor，本轮未执行）。
- **缺陷现象（原文）**：`tests/e2e/logs/coverage-20260917-152011.log` —— 弹层文案 `恭喜获得  已记入我的中奖记录 …`（名称空），**抽奖后奖池重拉次数 = 0**。
- **根因**：`DrawView.vue` 的 `handleStartDraw` 在抽奖成功后直接用**页面加载时**的 `prizeItems` 快照定位结果（`prizeName` 由 `prizeItems.find(...)` 求值），**从未按 FR-05-R10 / TC-48c 重拉奖池**。TC-48c 的场景（条目在页面加载时停用、抽奖前才启用）必然使快照不含该条目 → 名称为空、转盘亦定位不到扇区（`resultIndex = -1`）。
- **修复（前端 3 处）**：
  1. `useDrawFlow.ts` 新增 `reloadPoolForResult(): Promise<void>` —— 调既有 `api/prize` 的 `fetchPrizePool()` 重拉一次；**刻意不改 `poolState`**：状态锚点一旦离开 `ready`，`page-draw--default` 整块（含转盘与结果弹层）会被渲染边界卸载，停稳事件与结果反馈一并丢失——而重拉的目的正是**保住结果反馈**。重拉失败或取回空快照时静默保留原快照。
  2. `DrawView.vue` `handleStartDraw`：抽奖返回后若 `resultIndex < 0` → `await reloadPoolForResult()`（`resultIndex` 由 `prizeItems` 派生，替换快照即重新定位，无需额外代码）；**仍** `< 0` → `settled = true; dialogOpen = true` 跳过旋转直接展示结果，兑现 TC-48c 期望原文「跳过旋转直接展示结果，保证结果反馈不被渲染边界阻塞、不白屏」。
  3. **兜底文案**：`prizeName` 在重拉后仍不可定位时返回新增键 `messages.draw.prizeUnknown`（`'未知奖品'`），不再渲染空名称（即本缺陷的可见现象）。注：该分支此前**不存在**兜底，空名称是唯一出口。
- **约束遵守（读码核对）**：未改 `src/components/ui/**`（shadcn-vue 只读）与 `lib/utils.ts`；`<script setup lang="ts">` + 显式类型、无 `any`；请求走既有 `api/prize`（内部 `utils/request`），未新增 axios 直连；样式仅 Tailwind 原子类，未新增 CSS / 硬编码色值。
- **改动文件（生产）**：
  - `src/frontend/src/composables/useDrawFlow.ts`（`DrawFlow` 接口 `:35`、实现 `:84-101`、导出 `:175`）
  - `src/frontend/src/views/draw/DrawView.vue`（`handleStartDraw` 的两段判定 `:86-96`；`prizeName` 兜底取值 `:56`）
  - `src/frontend/src/utils/messages.ts`（新增 `draw.prizeUnknown`）
- **改动文件（测试）**：`src/frontend/src/composables/__tests__/useDrawFlow.spec.ts`（+3 例：重拉后重新定位 / 重拉失败保留原快照且不进 error 态 / 空快照保留原快照）、`src/frontend/src/utils/__tests__/messages.spec.ts`（+1 例：钉住兜底键非空）。
- **覆盖边界（如实声明）**：`prizeName` 兜底位于 SFC 模板层，本仓库无组件渲染测试基建（`@vue/test-utils` 在依赖中但无既有组件用例、无 jsdom 约定），沿用 CHG-14 对 BUG-03 的既定口径 —— 以 `messages.spec.ts` 钉住冻结键，DOM 断言交由 E2E 的 TC-48c（`tests/e2e/**` 不在本角色写入范围，本轮未执行）。
- **回滚建议**：删除 `reloadPoolForResult` 与 `DrawView.vue` 的两段判定、`prizeName` 兜底还原为 `?? ''`、删除 `messages.draw.prizeUnknown` 及其用例（前端 3 文件 + 2 测试文件，无后端 / 数据连带）。
- **测试结果**：见下表 #4 / #5（前端 7 文件 58 例全绿、`vue-tsc` / `eslint` 均 exit 0）。
- **关联**：BUG-04、TC-48c、FR-05-R10、FR-06。

### BUG-05　（一般，`未闭环`）抽奖事务后半段写入点未做瞬时错误分类

- **关联 TC**：TC-56b（该用例本身 **PASS** —— 回滚一致性 5/5 通过；BUG-05 系用例执行过程**暴露**的独立产品问题，非判据失败）。
- **缺陷现象（52 §1.6.6 实测）**：锁竞争下抽奖返回 **HTTP 500「系统内部错误」**（设计为 **1001「系统繁忙，请稍后重试」**），并白等 **30.6s**；重试链**一次都未触发**。
- **根因（两层，均已处置）**：
  1. **分类缺失**：⑥ `WinningRecordRepository.AddAsync` / ⑦ `AuditService` / ⑧ `DrawRequestRepository.SaveResultAsync` 未做 `MySqlErrors.IsTransient` 判定（原仅 ①⑤ 有）→ 这三点的 1205/1213 抛原生 `MySqlException`，**越过** `DrawService` 的 `catch (TransientDataException)` → 整事务重试失效 → 冒泡到全局 ExceptionFilter → 500。
  2. **命令超时抢先（隐藏根因）**：`CommandTimeout` 从未配置 = MySqlConnector 默认 **30s**，早于服务端 `innodb_lock_wait_timeout`（默认 **50s**，本项目未改）→ 任何 > 30s 的锁等待先被命令超时中断，而命令超时的 `MySqlException.Number` 既非 1205 也非 1213，`IsTransient` 无法识别 → **即使补了 ⑥⑦⑧ 的 catch，分类分支仍不可达**（实测日志原文 `CommandTimeout='30'` → `30039.97ms` → 500）。
- **2.1 瞬时错误分类（逐字复用既有模式）**：三个写入点各补 `catch (Exception exception) when (MySqlErrors.IsTransient(exception))` → `throw new TransientDataException(...)`，与 `PrizeRepository.cs:77-80` / `UserDrawQuotaRepository.cs:94-98` 同写法：

  | 写入点 | 文件（改动后行号） | 抛出消息 | 失败后收尾 |
  | --- | --- | --- | --- |
  | ⑥ 中奖记录 | `src/backend/src/LuckyDraw.Infrastructure/Repositories/WinningRecordRepository.cs:27-38` | 中奖记录写入遇数据库瞬时错误 | `Entry(record).State = EntityState.Detached` |
  | ⑦ 审计 | `src/backend/src/LuckyDraw.Infrastructure/Audit/AuditService.cs:53-63` | 审计写入遇数据库瞬时错误 | `Entry(auditLog).State = EntityState.Detached` |
  | ⑧ 回填流水 | `src/backend/src/LuckyDraw.Infrastructure/Repositories/DrawRequestRepository.cs:69-86` | 抽奖流水结果回填遇数据库瞬时错误 | 无需（`ExecuteUpdateAsync` 不走变更跟踪，无待脱离实体） |

- **幂等安全性自证（已复核，结论：安全）**：`ExecuteDrawAsync` 的失败路径先整体回滚 —— `DrawService.cs:238` 的 **catch-all** → `:240` `TryRollbackAsync(scope, …)`（`:249` 定义，尽力回滚，吞掉二次异常）→ ① 的 `DrawRequest` 占位随事务消失、② 的次数扣减与 ⑤ 的库存扣减一并回滚 → 重试重新抢占同一幂等键。`DrawService.cs:109-110` 的论证（「判据是整个事务已回滚，而非失败点位置」）对 ⑥⑦⑧ **同样成立**，故无需停下上报。
  - **额外必要的两处收尾**（否则重试会重复写入）：⑥⑦ 用 `SaveChangesAsync`，失败后实体仍以 `Added` 状态留在变更跟踪器中 → 必须显式脱离，否则重试的下一次 `SaveChanges` 会把残留实体**再插一遍**（重复中奖记录 / 重复审计）；集成用例以「行数**恰好 1**」断言这一点（见下表 #3）。⑧ 用 `ExecuteUpdateAsync`（绕过变更跟踪）→ 无残留实体。
- **2.2 命令超时口径：选方案 (a)** —— `DependencyInjection.cs:25` 新增常量 `CommandTimeoutSeconds = 60`，`:48` 经 `mySqlOptions.CommandTimeout(CommandTimeoutSeconds)` 生效（**未改** `MySqlErrors.IsTransient` 的 1205/1213 语义，按约束）。理由：
  1. (a) 让代码**本来就写了分类分支**的「锁等待超时 1205」重新可达 —— 这是最小且语义正确的处置；60 > 50 保证 MySQL 的 1205 先于客户端命令超时触发。
  2. 命令超时保留「客户端主动中断、**事务状态未知**」的保守语义（不纳入瞬时判定、不重试），与 `TryRollbackAsync` 吞掉二次异常的实现自洽。
  3. (b) 把命令超时并进瞬时判定，等于给「事务状态未知」开重试口子，且对 > 30s 的锁等待仍拿不到 1205（只是把不可达点后移），属把根因藏起来。
- **最长用户等待时间（量化，本 CHG 明确给出）**：
  - **尝试次数口径**：`DrawService.MaxTransactionAttempts = 3`（`DrawService.cs:26`，语义为「最大**尝试**次数」= 3 次尝试 / 2 次重试）。
  - 单次尝试最坏 = 一次锁等待吃满 `innodb_lock_wait_timeout = 50s` 后由 **MySQL 抛 1205**（不再是 30s 命令超时）→ **服务端最坏 ≈ 3 × 50s = 150s**（每次尝试的事务开销为毫秒级，可忽略；⑩ Redis 幂等缓存是 best-effort 且不入临界路径）。
  - 若按 `docs/30-architecture.md:544`「整个事务重试 **1 次**」的口径（= 2 次尝试）则 **≈ 2 × 50s = 100s** → **这处契约冲突直接决定该数值是 100s 还是 150s**（见本节末「未裁决项 (2)」）。
  - **修复前后对比**：修复前 ≈ **30.6s**（52 §1.6.6 实测，1 次尝试、未分类、终态 500）→ 修复后 **≤ 150s**（终态 1001 + CRITICAL）。耗时上界变长**不是新增等待**，而是「本来就没有重试，只是被 30s 命令超时伪装成快速失败」；修复后长尾只在**真实 50s 级锁竞争**下出现。
  - **前端口径（如实补充，未改）**：浏览器用户经 `utils/request` 的 `timeout: 15000` 约束，**15s 即在前端失败**（服务端事务可能仍在跑），此时按 D-03 沿用同一幂等键重试是幂等安全的（重放 / 占位超时分支已由 AC-12 用例覆盖）。
  - **网关口径（未改，待部署侧对齐）**：若生产前置 nginx，其 `proxy_read_timeout` **默认 60s** 会在网关把 100s / 150s 的长尾切断为 504 —— 需部署清单同步该值，或由主对话裁决是否下调重试预算（本轮不动，不擅自改配置）。
- **2.3 CRITICAL 告警**：`DrawService.cs:117-126` 的重试耗尽分支由 `LogWarning` 提升为 **`LogCritical`**（输出异常、尝试次数、UserId），兑现 `docs/30-architecture.md:544`「仍失败 → 500 + **CRITICAL 告警**」中的「CRITICAL 告警」部分（**终态错误码那部分保持现状未改**，见「未裁决项 (1)」）。复用既有 Serilog 管道，未新增依赖 / 通道；日志仅含 UserId 与异常，**无密码 / Token / 密钥**。
- **改动文件（生产）**：`src/backend/src/LuckyDraw.Infrastructure/Repositories/WinningRecordRepository.cs`、`src/backend/src/LuckyDraw.Infrastructure/Audit/AuditService.cs`、`src/backend/src/LuckyDraw.Infrastructure/Repositories/DrawRequestRepository.cs`、`src/backend/src/LuckyDraw.Infrastructure/DependencyInjection.cs`、`src/backend/src/LuckyDraw.Application/Services/DrawService.cs`（5 个文件；`MySqlErrors.cs` **未动**）。
- **改动文件（测试）**：见下「测试与覆盖边界」。
- **回滚建议**：撤销上述 3 处 catch 与 Detach、删除 `CommandTimeoutSeconds` 及其 DSL 调用、`LogCritical` 还原为 `LogWarning`，并回退新增测试文件（后端 5 文件 + 2 测试文件，**无数据库 / 迁移 / DTO 连带**）。回滚后 TC-56b 场景会重新返回 500、白等 30s —— 属期望的守卫。
- **关联**：BUG-05、TC-56b、D-08（`docs/30-architecture.md:544`）、FR-08-3、`docs/error-codes.md:32`、`docs/artifacts.md` §5、CHG-11（重试机制首次落地）。

### 测试与覆盖边界（Task 3：证明 ⑥⑦⑧ 的瞬时错误确实进入重试链）

分层覆盖，**未伪造真实锁竞争**（构造方式与覆盖边界逐条声明）：

| 层 | 文件 | 构造方式 | 断言要点 |
| --- | --- | --- | --- |
| 仓储层（集成，真实 MySQL 行锁） | `tests/integration/LuckyDraw.IntegrationTests/TransientErrorClassificationTests.cs`（**新增**，+3 例） | ⑥ 另一连接持 `User` 行 X 锁阻塞 `WinningRecord` 的**外键父行校验**（正是 TC-56b 的证据形态）；⑦ `AuditLog` 无外键 → 以「探针行 + 范围 gap lock」阻塞插入；⑧ 直接锁 `DrawRequest` 目标行 | 仓储确实把 `MySqlException.Number == 1205` 翻译为 `TransientDataException`（= 具备进入重试链的资格）；锁释放后重放「回滚 + 重试」→ **⑥⑦ 行数恰好 1**（证明 Detach 生效、不重复插入）、⑧ 回填值正确（`PrizeItemId=4` / `IsWin=true` / `RemainingAttempts=2`） |
| 编排层（单测，替身注入） | `tests/unit/LuckyDraw.UnitTests/Services/DrawServiceTests.cs`（+2 个 `[Theory]` × 3 写入点 = 6 例） | 在 ⑥ / ⑦ / ⑧ 抛 `TransientDataException` | ①「整事务重试」：`BeginAsync` 2 次、`RollbackAsync` 1 次、`CommitAsync` 1 次、结果按重试后的新抽中结果落地；② 持续失败 → 3 次尝试后 `BusinessException`（`ErrorCodes.SystemBusy`，文案「系统繁忙，请稍后重试」），**不冒泡 500** |
| 测试夹具 | `tests/integration/LuckyDraw.IntegrationTests/IntegrationFixture.cs` | 新增 `CreateScope()` | 支持「同一 DbContext = 同一物理连接」跨多步复用（锁会话与业务操作需同连接） |

- **锁等待加速方式（未污染环境）**：用例内以**会话级** `SET SESSION innodb_lock_wait_timeout = 1` 把等待压到 1s；释放时 `SET SESSION innodb_lock_wait_timeout = DEFAULT` 复位（实测恢复 50）→ 关闭连接、释放作用域。**未改服务端全局值**、未残留持锁连接。
- **未覆盖（明确列出，不以「跳过」掩盖）**：① 跨 50s 的真实锁等待 + 完整 HTTP 链路（生产 `innodb_lock_wait_timeout = 50s`，用例内不可接受，故以 1s 会话级加速验证同一判定路径）；② E2E 的 TC-48c / TC-56b 属 test-executor（`tests/e2e/**` 不在本角色写入范围，本轮未执行）。

### 本轮验证记录（实际命令 / 工作目录 / 实际结果）

| # | 实际命令 | 工作目录 | 实际结果 |
| --- | --- | --- | --- |
| 1 | `dotnet test "tests/unit/LuckyDraw.UnitTests" --nologo` | 仓库根 | **通过 95 / 失败 0 / 跳过 0 / 总计 95**，259 ms，exit 0 —— CHG-14 基线 89，净增 **6**（2 个 `[Theory]` × 3 写入点） |
| 2 | `dotnet build "tests/integration/LuckyDraw.IntegrationTests" -p:OutputPath=<临时目录> -m:1 --nologo` | 仓库根 | **已成功生成，0 个警告 / 0 个错误**（连带构建 Api / Infrastructure / Application / Domain 全链；临时输出目录的原因见下方「文件锁」） |
| 3 | `dotnet test "tests/integration/LuckyDraw.IntegrationTests" --no-build -p:OutputPath=<临时目录> --nologo` | 仓库根 | **通过 42 / 失败 0 / 跳过 0 / 总计 42**，14 s，exit 0（真实 MySQL 8.4.11 + Redis）—— CHG-14/15 基线 39，净增 **3**（`TransientErrorClassificationTests`）；**实跑非跳过** |
| 4 | `node node_modules/vitest/vitest.mjs run` | `src/frontend` | **Test Files 7 passed (7) / Tests 58 passed (58)**，1.40 s —— CHG-14 基线 54，净增 **4**（3 例重拉 + 1 例兜底键） |
| 5 | `node node_modules/vue-tsc/bin/vue-tsc.js --noEmit`；`node node_modules/eslint/bin/eslint.js . --max-warnings 0` | `src/frontend` | 均 **exit 0**，无输出 |
| 6 | `dotnet format whitespace --verify-no-changes`（**未加** `--nologo`） | `.../LuckyDraw.Infrastructure`；`.../LuckyDraw.Application` | 两个被改动项目均 **exit 0**（CHG-15 已使 Infrastructure 转绿） |

- **`LuckyDraw.Api/bin` 文件锁（如实记录，未杀进程）**：`:5180` 上运行中的开发服务器（**PID 4308**）仍锁定 Api 输出目录；向常规输出目录构建 Api / 集成测试会报 `MSB3027` / `MSB3021`（原文同 CHG-15 节），故本轮后端集成构建与运行**全部使用 `-p:OutputPath=<临时目录>`**（`C:\Users\…\Temp\luckydraw-int-chg16`），**未终止该进程**。连带事实：`LuckyDraw.Api/bin` 内产物仍为**改动前**版本；本次修改落在 Infrastructure（3 文件）与 Application（1 文件）→ **若要复跑 E2E（TC-48c 取前端源码无碍；TC-56b 需新 DLL），须先在停服状态下重新构建 Api**，由主对话择机调度（`tests/e2e/**` 不在本角色范围）。
- **测试失败→修复→重跑（如实记录）**：单测首次运行时 2 例失败 —— 新增用例对 ⑦ 审计调用次数的期望写反（失败点在 ⑥ 时首次尝试走不到审计 → 1 次；其余两种情况两次尝试都走到 → 2 次）。修正断言后复跑方得 #1 的全绿数字；**#1 的数字为修正后的最终实跑结果**。

### 失效传播判定（按 `docs/artifacts.md` §5 矩阵）

- **`src/ 变更` → `tests/unit/**` / `tests/integration/**`**：**已在本轮实跑**（#1 / #3），无需再跑。
- **`src/ 变更` → `tests/e2e/**`**：**需重跑**（TC-48c 为本缺陷的原始暴露用例；TC-56b 为其关联用例），由 test-executor 执行；前置条件是**以新 DLL 重启 Api**（见上方文件锁备注）。
- **`40-changelog 变更` → `51-defects` / `52-qa-report` / `60-review`**：**需回写 / 复核**（矩阵要求）——`51-defects` 的 BUG-04 / BUG-05 状态、`52-qa-report` 的 §1.6.6 与 QX-E-09、`60-review` 的对应条目均待各角色独立复核后回写；**本角色无这三份文件的写入权**，仅在修复侧闭环（本轮不新增 BUG、不关闭任何 BUG）。

### 未裁决项（契约冲突，本角色只上报、不自行裁决 —— **两项均已于 CHG-17 经用户裁决闭合**）

**(1) 重试耗尽的终态错误码（四方冲突，逐字引用）**
- `docs/30-architecture.md:544`：「死锁 1213 → 整个事务重试 1 次，仍失败 → **500** + CRITICAL 告警」
- `docs/error-codes.md:32`：「`1001`（系统繁忙）：业务层主动降级提示（限流、熔断、依赖服务不可用等可重试场景）；**`500`（系统内部错误）为全局 ExceptionFilter 兜底，业务代码禁止手动使用**」
- `docs/30-architecture.md:910`（API-07 错误语义）：`1001` **限流/依赖不可用**（HTTP 429/200…）；`500`（前端 → `page-draw--drawfail`…）
- 代码现状 `DrawService.cs:125`：`throw new BusinessException(ErrorCodes.SystemBusy, "系统繁忙，请稍后重试")` → **1001 / HTTP 200**
- 判定冲突：`:544` 要 500，与 error-codes 的「业务代码禁止手动使用 500」直接矛盾；`:910` 把 1001 分配给「限流/依赖不可用」，未覆盖「瞬时错误重试耗尽」；代码返回 1001。**本轮按约束保持现状未改**。
- 供裁决的实测事实：修复前用户看到的就是 **500**（异常未被分类 → 直接冒泡到全局兜底），即「当前默认命中 :544 的 500」恰恰是**缺陷状态**；修复后才落到 1001。建议由架构侧明确 :544 的终态归属，或明确 1001 覆盖「瞬时错误重试耗尽」。

**(2) 整事务重试次数（1 次 vs 3 次）**
- `docs/30-architecture.md:544`：「整个事务重试 **1 次**」→ 最坏 **2 × 50s = 100s**
- 代码 `DrawService.cs:26`：`MaxTransactionAttempts = 3`（3 次尝试）→ 最坏 **3 × 50s = 150s**；CHG-11 自述「重试整个事务 ≤3 次」
- `docs/52-qa-report.md:391` 记载的简报期望为「重试最多 3 次（总耗时 3×超时）」——**该期望本身与 :544 冲突**。
- 口径澄清（避免继续误引）：`docs/30-architecture.md` 的 **D-02 是「每日次数计数：按日聚合行 + 条件 UPDATE」**，**不承载重试预算**；重试预算只出现在 :544（D-08「异常与重试」）。
- 本轮未改代码的 3 次、也未改架构；请主对话裁决后一次性对齐（改 `MaxTransactionAttempts` 或改 :544）。

---

## CHG-17 用户裁决落地：重试预算 3 → 2（对齐架构 D-08）+ `CommandTimeout` 接线守护性断言

- **状态**：**已落地**；下表全部为实跑输出（实际命令 + 工作目录 + 实测结果），另含 **2 项负向验证**（证明守护断言确实能拦住回归）。
- **来源与裁决**：CHG-16 上报的两项契约冲突由**用户裁决**（`MaxTransactionAttempts` 3 → 2）与本轮授权的**守护性断言**任务；本 CHG 为裁决的落地记录。
- **本轮裁决闭合的两处冲突（已消解，不再上报）**：
  1. **终态错误码**：裁决**保持代码现状 `1001`**（`DrawService.cs:125` 不改）；架构 `:544` 的「500」表述由 **software-architect 并行修订** `docs/30-architecture.md` —— 本角色**未触碰该契约文件**，仅把 `DrawService.cs` 的注释按裁决更新。
  2. **重试次数**：裁决**改代码对齐架构 `:544`「整个事务重试 1 次」** → 常量 3 → **2**（2 次尝试 / 1 次重试），最坏用户等待 **150s → 100s**。
- **上游契约变更**：**无**（本角色未改任何 `docs/10/20/30-*` 契约文件）。
- **未触发「必须确认的场景」**：无新第三方依赖、无表结构变更、无破坏性接口变更、**未新增 `InternalsVisibleTo`**（守护断言走公开 API `DatabaseFacade.GetCommandTimeout()` 读回生效值，无需开放内部可见性）。

### 1. 重试预算 3 → 2（`MaxTransactionAttempts`）

- **改动文件（生产，1 处 + 注释）**：`src/backend/src/LuckyDraw.Application/Services/DrawService.cs`
  - `:25-30`：常量 `MaxTransactionAttempts` 由 `3` 改为 `2`；`<summary>` 重写为「最大**尝试**次数 = 2（即重试 1 次）」，并注明数值对齐 `docs/30-architecture.md` D-08（` :544` 原文「整个事务重试 1 次」）与「单次尝试最坏吃满 `innodb_lock_wait_timeout`（默认 50s），用户最坏等待 ≈ 2 × 50s = 100s」。
  - `:119-120`：CRITICAL 分支的注释去掉「终态错误码待契约裁决」的旧表述，改为「终态错误码经用户裁决保持 `1001`（架构侧同步修订 `:544` 表述）」。**未改任何逻辑结构**：`catch (TransientDataException exception) when (attempt >= MaxTransactionAttempts)` 用的是 `>=`，常量变更后语义自动正确。
- **改动文件（测试，2 处，均为既有用例的期望值同步，无删除）**：`tests/unit/LuckyDraw.UnitTests/Services/DrawServiceTests.cs`
  - `DrawAsync_WhenTransientErrorPersists_DegradesTo1001AfterThreeAttempts` → 更名 `…AfterRetryBudget`（原名写死「ThreeAttempts」已不成立），`Transactions.Received(3)` → **`Received(2)`**，用例的 `<summary>` 同步为「按 D-08 只重试 1 次（共 2 次尝试）」。
  - 本轮新增的 `DrawAsync_WhenLateWritePointTransientErrorPersists_DegradesTo1001`（⑥⑦⑧ 三个写入点）：`hits: [0, 0, 0]` → `[0, 0]`（失败点在 ⑤ 之后，每次尝试都要重新抽取 = 2 次命中）、`Transactions.Received(3)` → **`Received(2)`**。
  - **未受影响**（已逐条核对）：`DrawAsync_WhenTransientErrorThenSuccess_RetriesWholeTransaction`（第 2 次成功，本就用 2 次尝试）与本轮新增的成功型 `[Theory]`（同为 2 次尝试）；`DrawAsync_*` 的库存重抽用例走的是 `MaxStockRetryRounds = 3`（**另一个常量，与本次无关，未改**）。
- **最长用户等待时间（更新，替代 CHG-16 的 150s 口径）**：**2 次尝试 × 50s = 100s**（单次尝试最坏吃满服务端 `innodb_lock_wait_timeout = 50s` 后由 MySQL 抛 1205）；与架构 `:544` 的「重试 1 次」口径一致。

### 2. `CommandTimeout` 接线守护性断言

- **要拦的回归（两个）**：① `DependencyInjection.cs:48` 的 `mySqlOptions.CommandTimeout(CommandTimeoutSeconds)` 接线被删除（回落 MySqlConnector 默认 30s）；② 常量被下调到 **≤** 服务端 `innodb_lock_wait_timeout`（50s）→ 命令超时重新抢在 1205 之前（BUG-05 静默复活）。
- **为什么原有用例拦不住**：`TransientErrorClassificationTests` 为把锁等待压到用例可接受的长度，以**会话级** `innodb_lock_wait_timeout = 1` 加速 —— 该口径下命令超时是 30 还是 60，MySQL 的 1205 都会先触发，**整套测试仍全绿**。
- **所选机制（读回 DI 中实际生效值，非同义反复）**：新增 `tests/integration/LuckyDraw.IntegrationTests/CommandTimeoutWiringTests.cs`（1 例）——
  - 从测试宿主容器解析 `AppDbContext`，用 **公开 API** `dbContext.Database.GetCommandTimeout()` 读回**实际生效**的命令超时（不是源码常量）；接线被删时该值为 **`null`**（负向验证实测），回落场景亦非 60。
  - 用 `SHOW VARIABLES LIKE 'innodb_lock_wait_timeout'` 读回**服务端实际值**（不假设 50），断言 `生效值 != null` 且 `生效值 > 服务端实际值` → 语义上是「MySQL 的 1205 必须先于客户端命令超时触发」这条不变量本身，因此常量上调 / 服务端被改都会重新校验。
- **放置位置的判断（集成层而非单测层）**：`tests/unit/LuckyDraw.UnitTests` **不引用 Infrastructure**（只引 Application + Domain），放单测层要么新增项目引用、要么开放内部成员；而集成层天然具备**真实配置 + 真实服务端取值 + 真实 DI 装配**（`WebApplicationFactory` 走的正是 `AddInfrastructure`），故置于 `tests/integration/**`。**未新增任何项目引用、未新增 `InternalsVisibleTo`**。
- **负向验证（实测，证明该断言确实能拦住两种回归；每次验证后均已还原并复跑全绿）**：

  | # | 人为制造的回归 | 命令 | 实测结果 | 还原 |
  | --- | --- | --- | --- | --- |
  | N1 | 常量 `CommandTimeoutSeconds` 由 60 改为 **50**（= 服务端值，`≤` 场景） | `dotnet test … --filter "FullyQualifiedName~CommandTimeout"` | **失败 1 / 通过 0**；报错原文 `Expected effective!.Value to be greater than 50 because 命令超时（50s）必须严格大于服务端 innodb_lock_wait_timeout（50s）… but found 50.` | 已还原为 60 并复跑 |
  | N2 | 删除 `mySqlOptions => mySqlOptions.CommandTimeout(CommandTimeoutSeconds)` 整行接线（回落默认 30s） | 同上 | **失败 1 / 通过 0**；断言在 `NotBeNull` 处失败 —— 实测**未接线时 `GetCommandTimeout()` 返回 `null`** | 已还原接线并复跑 |

### 本轮验证记录（实际命令 / 工作目录 / 实际结果，均在上述改动与还原全部完成后执行）

| # | 实际命令 | 工作目录 | 实际结果 |
| --- | --- | --- | --- |
| 1 | `dotnet test "tests/unit/LuckyDraw.UnitTests" --nologo` | 仓库根 | **通过 95 / 失败 0 / 跳过 0 / 总计 95**，249 ms，exit 0（用例数不变：仅同步期望值与一处更名） |
| 2 | `dotnet build "tests/integration/LuckyDraw.IntegrationTests" -p:OutputPath=<临时目录> -m:1 --nologo` | 仓库根 | **0 警告 / 0 错误**（临时输出目录的原因见下） |
| 3 | `dotnet test "tests/integration/LuckyDraw.IntegrationTests" --no-build -p:OutputPath=<临时目录> --nologo` | 仓库根 | **通过 43 / 失败 0 / 跳过 0 / 总计 43**，14 s（CHG-16 基线 42，净增 **1** = `CommandTimeoutWiringTests`；真实 MySQL 8.4.11 + Redis） |
| 4 | —（负向验证 N1 / N2，见上表） | 仓库根 | 各 **失败 1 / 通过 0**；验证后均已还原并复跑 #3 全绿 |
| 5 | `node node_modules/vitest/vitest.mjs run` | `src/frontend` | **Test Files 7 passed (7) / Tests 58 passed (58)**，1.73 s（本轮未改前端，重跑确认无回归） |
| 6 | `node node_modules/vue-tsc/bin/vue-tsc.js --noEmit`；`node node_modules/eslint/bin/eslint.js . --max-warnings 0` | `src/frontend` | 均 **exit 0**，无输出 |
| 7 | `dotnet format whitespace --verify-no-changes`（6 个项目目录分别执行，未加 `--nologo`） | Domain / Application / Infrastructure / Api / UnitTests / **IntegrationTests** | 前 5 个 **exit 0**；**IntegrationTests 首次 exit 2**（2 处 WHITESPACE，位于 `TransientErrorClassificationTests.cs:73-74` 的长参数续行缩进）→ 执行 `dotnet format whitespace`（生成式、纯空白）后**复验 exit 0**；改动后 #3 复跑全绿（SQL 字面量与断言结构未变，行为等价的旁证） |

- **`LuckyDraw.Api/bin` 文件锁（如实记录，未杀进程）**：`:5180` 运行中的开发服务器（**PID 4308**）仍锁定 Api 输出目录，故集成构建 / 运行继续使用 `-p:OutputPath=<临时目录>`（`C:\Users\…\Temp\luckydraw-int-chg17`），**未终止该进程**。

### 交付残留（裁决决定本轮不动，留待交付总结列明）

- **超时预算不匹配**：前端 `src/frontend/src/utils/request.ts` 的 `timeout: 15000`（15s）< 后端最坏 **100s**（2 次尝试 × 50s）。后果：浏览器用户会在 15s 由前端先行失败（服务端事务可能仍在进行），此时按 D-03 沿用同一幂等键重试是幂等安全的（该路径已由 AC-12 用例覆盖），但用户感知不到「系统繁忙」的权威结论。**经用户裁决本轮不动**：未改 `request.ts`、未改任何超时配置；若生产前置 nginx，其 `proxy_read_timeout` 默认 60s 也会先于 100s 切断请求 —— 均需交付侧统一口径。
- **E2E 待重跑（矩阵要求）**：`tests/e2e/api/tc56b-tx-rollback.mjs` 的注释提到「总耗时可能是 超时 × 次数」，重试预算 3 → 2 后该场景的最坏耗时由 150s 收敛到 100s（该脚本客户端超时按 320s 留足，超时预算仍够用，**脚本本身无需改动**），但**结论数字（实测耗时）应由 test-executor 复跑更新**；TC-48c / TC-56b 的复跑还依赖以新 DLL 重启 Api。

### 回滚建议

- 撤回 `DrawService.cs` 的常量（`2` → `3`）与两处注释，并把 `DrawServiceTests.cs` 的两处期望值（`Received(2)` → `Received(3)`）与用例名一并回退；删除 `CommandTimeoutWiringTests.cs`。
- **不建议单独回滚守护断言**：它拦的是「接线被删 / 常量被下调」这类静默复活 BUG-05 的改动，回滚即失去守卫；若确需回滚，请与 `DependencyInjection.cs` 的接线一并评估。
- 无数据库 / 迁移 / DTO / 接口行为连带；回滚后仅影响「重试次数」与警告日志时的尝试次数。

### 失效传播判定（按 `docs/artifacts.md` §5 矩阵）

- **`src/ 变更` → `tests/unit/**` / `tests/integration/**`**：**已在本轮实跑**（#1 / #3），无需再跑。
- **`src/ 变更` → `tests/e2e/**`**：**需重跑**（TC-56b 的耗时结论随重试预算变化），由 test-executor 执行；前置条件同 CHG-16（停服重建 Api）。
- **`40-changelog 变更` → `51-defects` / `52-qa-report` / `60-review`**：**需回写 / 复核**——`51` 的 BUG-05 状态、`52` 的 §1.6.6 与 QX-E-09、`60` 的对应条目；本角色无这三份文件写入权，仅在修复侧闭环。

- **关联**：BUG-05、D-08（`docs/30-architecture.md:544`「整个事务重试 1 次」）、CHG-16（已消解项：终态错误码保持 `1001` + 重试次数对齐）、`docs/artifacts.md` §5、`AS-07`（本条裁决后更新）。

---

## CHG-18 REV-21 闭环：重试耗尽 CRITICAL 告警的计数口径修正（尝试次数 → 重试次数）+ 守护性断言

- **状态**：**已落地**；下表全部为实跑输出（实际命令 + 工作目录 + 实测结果），另含 **2 项负向验证**（证明守护断言确实能拦住回归）。
- **来源与授权**：`docs/60-review.md` **v5** §A REV-21 行 + §G.5（建议级、`未闭环`，其给出两条候选修法）；**用户裁决「派 engineer 修」**，并明确为本轮**唯一**改动目标（不顺手重构）。
- **上游契约变更**：**无** —— 未修改 `docs/10-prd.md` / `docs/20-prototype.html` / `docs/30-architecture.md` / `docs/50-testcases.md` / `docs/51-defects.md` / `docs/52-qa-report.md` / `docs/60-review.md` / `docs/error-codes.md`。D-08（`docs/30-architecture.md:567`）「整个事务重试 **1 次**，仍失败 → `1001`（HTTP 200）+ CRITICAL 告警」**本来就正确**，本轮是**实现侧向契约收敛**（与 CHG-17 同方向）。
- **未触发「必须确认的场景」**：无新第三方依赖、无表结构 / 迁移变更、无 DTO / 接口结构变更、无破坏性接口变更、无目录结构变更、未新增 `InternalsVisibleTo`。

### 1. 修复对象与改前 / 改后文案（逐字对照）

**缺陷（REV-21）**：终态 CRITICAL 告警模板把**尝试次数**当成**重试次数**输出。生产实证（同一份日志里两行自相矛盾，`tests/e2e/logs/tc56-rerun-instance-20260917-182525.log`）：

```
[18:28:27 WRN] 抽奖事务遇数据库瞬时错误，重试整个事务（第 1 次）：UserId=631
[18:29:07 FTL] 抽奖事务重试 2 次后仍遇数据库瞬时错误（死锁 / 锁等待超时），已放弃本次抽奖：UserId=631
```

WRN 说「第 1 次」（预告即将进行的那 1 次重试，语义正确）、FTL 说「重试 2 次」（实际只重试 1 次）—— 按 FTL 做故障复盘会被误导。

| | 模板（逐字） | 传入参数 |
| --- | --- | --- |
| 改前（`DrawService.cs:127`） | `抽奖事务重试 {Attempt} 次后仍遇数据库瞬时错误（死锁 / 锁等待超时），已放弃本次抽奖：UserId={UserId}` | `attempt`（终态 = **2**，**尝试次数**被当作重试次数） |
| 改后（`DrawService.cs:130`） | `抽奖事务重试 {RetryCount} 次（共 {Attempts} 次尝试）后仍遇数据库瞬时错误（死锁 / 锁等待超时），已放弃本次抽奖：UserId={UserId}` | `attempt - 1`（终态 = **1**，**重试次数**）、`attempt`（**2**，尝试次数）、`userId` |

- 改后终态输出（模板 × 参数，`attempt = 2`）：`抽奖事务重试 1 次（共 2 次尝试）后仍遇数据库瞬时错误（死锁 / 锁等待超时），已放弃本次抽奖：UserId={用户ID}`；守护用例实跑断言其**包含** `重试 1 次（共 2 次尝试）`。
- **未改动（刻意）**：
  - WRN 行（`DrawService.cs:138-142`）保持「重试整个事务（第 {Attempt} 次）」+ 传 `attempt` 不变 —— 该行指**即将发生的第 N 次重试**，语义本来就正确（REV-21 明确要求勿动）。
  - `MaxTransactionAttempts`（`DrawService.cs:30`）**语义与取值均未改**：仍为「最大**尝试**次数 = 2」（其 `<summary>` 未改）。
  - 终态错误码保持 `1001`「系统繁忙，请稍后重试」、HTTP 状态码保持 **200**（用户裁决，见 CHG-17 / AS-06）。
  - **如实声明**：既有注释中的 `:544` 系架构 v3 时期行号（v4 已移至 `:567`）。为控制改动面**未改写既有锚点**，仅本轮新增的注释行按现行行号 `:567` 引用（如需统一锚点，建议单独批次处理）。

### 2. `{RetryCount}` 取值推导（为何是 `attempt - 1`）

- 循环 `for (var attempt = 1; ; attempt++)`（`:115`）；终态由 `catch (TransientDataException) when (attempt >= MaxTransactionAttempts)`（`:121`）命中，触发时 `attempt == MaxTransactionAttempts == 2`，即**已发生的尝试次数**；
- **重试次数 = 尝试次数 - 1 = `attempt - 1` = 1**（第 1 次是首次尝试，不计入重试）；
- 与 D-08（`docs/30-architecture.md:567`）「整个事务重试 **1 次**」**逐字一致**；与同一次抽奖日志 WRN 行的「第 1 次」互补而非冲突（WRN 预告「即将进行第 1 次重试」，CRITICAL 复盘「已重试 1 次（共 2 次尝试）」）；
- 文案**同时**给出「重试次数」与「尝试次数」两个数：读者无需在两种口径间换算，也不会把常量语义（最大**尝试**次数）误读成重试次数。

### 3. 守护性断言（同源根因治理）

- **落点**：`tests/unit/LuckyDraw.UnitTests/Services/DrawServiceTests.cs` 新增 `[Fact] DrawAsync_WhenTransientErrorPersists_CriticalLogReportsRetryCountNotAttempts`（紧邻既有 `…DegradesTo1001AfterRetryBudget`）；单测总数 95 → **96**。
- **机制**：该用例经 `new Harness(hits: [0], logger: Substitute.For<ILogger<DrawService>>())` 注入**日志替身**；帮助方法 `SingleCriticalLogMessage` 用 NSubstitute `ReceivedCalls()` 过滤出**唯一一条** `LogLevel.Critical`，经 logger 自带的 formatter 委托渲染出与落盘一致的正文，继而断言：
  1. `Transactions.BeginAsync` 实际调用数（= 尝试次数）**= 2** —— 重试预算（`MaxTransactionAttempts`）若漂移，在此**显式失败**并提示须复核 D-08；
  2. 告警正文必须包含 `重试 {attempts - 1} 次（共 {attempts} 次尝试）`（当前即 **`重试 1 次（共 2 次尝试）`**）—— 文案一旦改回传 `attempt`，在此失败：即断言「报告的重试次数 = `MaxTransactionAttempts - 1`」；
  3. 有且**仅有一条** CRITICAL 告警（防告警漏打 / 重复）。
- **改动边界（最小化）**：仅为该测试文件内的测试宿主 `Harness` 增加**可选**形参 `ILogger<DrawService>? logger = null`（缺省仍回落 `NullLogger<DrawService>.Instance`）；其余用例既有的 `NullLogger` 用法**逐字未改**、测试基类未重构、未新增项目引用。
- **为什么原来拦不住**：与 BUG-05 的 `CommandTimeout` 零覆盖同源 —— 告警文案 / 接线长期没有任何断言钉住，才可能长期漂移。本条把「计数口径」从注释约定升级为**可执行断言**。

### 4. 本轮验证记录（实际命令 / 工作目录 / 实际结果）

| # | 实际命令 | 工作目录 | 实际结果 |
| --- | --- | --- | --- |
| 1 | `dotnet build "src/backend/LuckyDraw.slnx" --nologo` | 仓库根 | **0 警告 / 0 错误**（Api 一并构建成功；本轮未遇 `bin` 文件锁） |
| 2 | `dotnet test "tests/unit/LuckyDraw.UnitTests" --nologo` | 仓库根 | **通过 96 / 失败 0 / 跳过 0 / 总计 96**，258 ms（CHG-17 基线 95，净增 **1** = 本轮守护用例） |
| 3 | `dotnet test "tests/integration/LuckyDraw.IntegrationTests" --nologo` | 仓库根 | **通过 43 / 失败 0 / 跳过 0 / 总计 43**，13 s（真实 MySQL 3307 + Redis；本轮未改集成层行为，重跑确认无回归） |
| 4 | `dotnet test … --filter "FullyQualifiedName~CriticalLogReportsRetryCount"` | 仓库根 | **通过 1 / 失败 0**（守护用例定点实跑） |
| 5 | 负向验证 N1 / N2（见下表） | 仓库根 | 各 **失败 1 / 通过 0**；验证后均已还原并复跑 #2 全绿（96/0/0） |
| 6 | `dotnet format whitespace --verify-no-changes`（6 个项目目录分别执行，未加 `--nologo`） | Domain / Application / Infrastructure / Api / UnitTests / IntegrationTests | **六项均 exit 0** |
| 7 | 格式门禁**负向验证**（人为把新增用例首行缩进减 1 空格） | `tests/unit/LuckyDraw.UnitTests` | **exit 2**，报 `DrawServiceTests.cs(298,6): error WHITESPACE`（证明门禁对本项目 / 本文件非空转）；还原后复验 #6 仍全 exit 0 |

**负向验证明细（实测，证明守护断言能拦住回归；每次验证后均已还原并复跑）**：

| # | 人为制造的回归 | 实测结果 | 还原 |
| --- | --- | --- | --- |
| N1 | 生产代码 CRITICAL 传参由 `attempt - 1` 改回 `attempt`（复原 REV-21 原始缺陷） | **失败 1 / 通过 0**；报错原文 `Expected message "抽奖事务重试 2 次（共 2 次尝试）后仍遇…UserId=7" to contain "重试 1 次（共 2 次尝试）"` | 已还原并复跑全绿（96/0/0） |
| N2 | 常量 `MaxTransactionAttempts` 由 `2` 改为 `3`（重试预算漂移） | **失败 1 / 通过 0**，在 `attempts.Should().Be(2, …)` 处失败 | 已还原为 `2` 并复跑全绿（96/0/0） |

### 5. 影响面

- **仅日志文案**：不改业务行为、不改返回结构、不改错误码（仍 `1001`）、不改 HTTP 状态码（仍 **200**）、不改事务 / 重试结构（`attempt >= MaxTransactionAttempts` 判定与循环均未动）、不改常量语义；无数据库 / 迁移 / DTO / 接口 / 前端连带。
- **日志面**：终态 CRITICAL 行的占位符由 `{Attempt}` 更名为 `{RetryCount}` 并新增 `{Attempts}` —— **结构化日志字段名变化**；Serilog 管道无需改动，若外部存在按字段名建索引 / 告警的消费方需同步（本仓无此类消费方；`tests/e2e/**` 脚本经检索不解析该文案）。
- **安全**：日志仍仅含 UserId 与异常对象，**无密码 / Token / 密钥**（逐参核对 `:128-133`）。

### 6. 回滚方式

- 撤回 `DrawService.cs:125-133` 本轮新增的 3 行注释与模板 / 传参（改回 `{Attempt}` 单参数 + `attempt`）；删除 `DrawServiceTests.cs` 的新增用例与 `SingleCriticalLogMessage` 帮助方法，并把 `Harness` 的 `logger` 形参、`Logger` 属性与构造末参回落为 `NullLogger<DrawService>.Instance` 的写法。
- 回滚后业务行为完全等价（日志文案退回「重试 2 次」的错位口径），无业务 / 契约连带；**不建议单独回滚守护断言** —— 回滚即失去守卫，REV-21 可再次静默漂移。

### 7. 失效传播判定（按 `docs/artifacts.md` §5 矩阵）

- **`src/ 变更` → `tests/unit/**`**：**已在本轮实跑**（#2 / #4 / N1 / N2）。
- **`src/ 变更` → `tests/integration/**`**：本轮未改集成层依赖的行为，已重跑 **43/0/0**（#3）确认无回归。
- **`src/ 变更` → `tests/e2e/**`**：**需 test-executor 复核判定** —— 已检索 `tests/e2e/**`（排除 `logs/`）无任何脚本解析该 CRITICAL / WRN 文案（`tc56b-tx-rollback.mjs` 只断言 HTTP / `code` / 耗时与库内三方一致），故本轮**不构成 E2E 断言失效**；`tests/e2e/logs/**` 历史日志保留旧文案，属既成事实。
- **`40-changelog 变更` → `51-defects` / `52-qa-report` / `60-review`**：**需回写 / 复核** —— `60-review` 的 REV-21 可据本条转「已闭环」，`51` / `52` 若引用该告警文案需同步；本角色无这三份文件写入权，仅在修复侧闭环。

- **关联**：REV-21（`docs/60-review.md` v5 §A / §G.5）、D-08（`docs/30-architecture.md:567`「整个事务重试 1 次」）、CHG-16 §2.3（其自述「输出异常、**尝试次数**、UserId」与本轮修正后的口径一致）、CHG-17、`docs/artifacts.md` §5。

---

## CHG-19 引用修复改动集：代码 / 用例注释与用例标题的裸行号锚点改稳定 ID（两角色同源，8 文件 / 18 行）＋ 同批护栏 T30 扩面与规则文本加固（补登 CHG —— `60:OBS-32` 的「无 CHG 记录」一半）

- **状态**：**已落地**（`src/**` + `tests/**` 8 个文件已落盘；构建 0 警告 / 0 错误、单测 96/0/0、护栏 369 项 / 突变自测 134 条 均实跑通过）；**集成复跑未通过 —— 环境不具备**（43 例全部止于夹具初始化期，逐条见 §4、归类见 §5）。
- **来源与调度**：`docs/60-review.md` v6 §I **`60:OBS-32`（阻断）**登记「工作区存在未提交、未复核、且无 CHG 记录的引用修复改动集（现行末条为 CHG-18）」；本 CHG 为该改动集中**引用修复部分**的补登。同源项：**`60:OBS-05`**（`DrawService.cs` 既有注释的行号锚点，登记时点即已观察到未提交修复，并入 `60:OBS-32` 统一处置）。同批同源登记：`docs/51-defects.md` / `docs/52-qa-report.md` 的「执行侧补验标签勘误（2026-09-18）」行。**同轮两角色同源**：`src/**` + `tests/unit/**` 由 engineer 改、`tests/e2e/**` 由 test-executor 改（本 CHG 覆盖**提交面全量**，权威口径 = `git diff --numstat -- src tests` 实测 8 文件 / 18 增 18 删）。**同批第二部分**（护栏与规则文本加固）由主对话落盘、与前半同一提交批次，一并登记在本条（不另立 CHG-20）。
- **性质**：**① 引用修复**（`docs/development-spec.md` 13.4「禁止只用行号」）—— **非缺陷修复**（无 `BUG` / `REV` 编号）；故按角色输出要求第 2 条，负向验证以**等价负向样本**形态给出（见 §6）。**② 护栏与规则文本加固**（同批，见 §1b）—— 属 **CHG 的实现期变更**（护栏断言扩面 + 规则文本同步 + `.gitignore` 口径），**非引用勘误**、非台账回写；其负向验证 = 护栏自测逐条「造错 → 断言必红 → 还原复绿」（实测 134/134，见 §4 / §6）。
- **上游契约变更**：**无** —— 未修改任何契约文件（`docs/10/20/30/50/51/52/60-*` 与 `docs/error-codes.md` 一律未触碰）。
- **未触发「必须确认的场景」**：无新第三方依赖、无表结构 / 迁移变更、无 DTO / 接口结构变更、无破坏性接口变更、无目录结构变更、无新增或删除测试用例、未改公共封装。
- **落盘证据强度（如实标注）**：本批**尚未提交**（本角色不执行 git 提交，提交由主对话统一执行）→ **无提交级落盘证据**；下文读数均为**实测**（命令 + 工作目录 + 实际结果），引用路径已逐个核验存在。

### 1. 改动清单与逐处前后对照（提交面全量：`git diff --numstat -- src tests` 实测 8 文件 / 18 增 18 删）

| # | 文件 | 改动处数 | 改前形态 | 改后（稳定 ID） |
| --- | --- | --- | --- | --- |
| 1 | `src/backend/src/LuckyDraw.Application/Services/DrawService.cs` | 4 行（常量 `<summary>` 1 处 + `catch` 块 `//` 注释 3 处） | `docs/30-architecture.md` 的**裸行号锚点**（v3 / v4 两代取值，均已随架构升版漂移失效；具体旧值登记于该文件「引用勘误」块与 `60:OBS-05`） | `D-08「异常与重试」`（保留原文引用「整个事务重试 1 次」） |
| 2 | `tests/unit/LuckyDraw.UnitTests/Services/DrawServiceTests.cs` | 2 行（XML `<summary>`） | 同上（`CHG-17` / `CHG-18` 用例内的裸行号锚点） | `D-08「异常与重试」` |
| 3 | `tests/e2e/api/perf-suite.mjs` | 1 行（常量注释） | `docs/30-architecture.md` 的裸行号锚点 | `docs/30-architecture.md` §8.2「后端接口」：`POST /api/v1/draw` 目标 P95 ≤ 200ms |
| 4 | `tests/e2e/api/tc56b-tx-rollback.mjs` | 2 行（文件头块注释 1 处 + 终态契约注释 1 处） | ①事务写入顺序注释里的 `DrawService.ExecuteDrawAsync` 行号区段；②`docs/error-codes.md` 的裸行号锚点 | ①方法名 `DrawService.ExecuteDrawAsync`（去行号）；②`docs/error-codes.md` §「使用边界」`1001` 条 |
| 5 | `tests/e2e/api/tc56c-redis-degradation.mjs` | 1 行（文件头块注释） | `docs/30-architecture.md` §6.1 + 裸行号区段 | `docs/30-architecture.md` §6.1「依赖降级矩阵」：表列为「链路 / Redis 不可用时的行为 / 是否可用」 |
| 6 | `tests/e2e/qa.spec.ts` | 2 行（用例标题字符串 1 行 + M9 用例注释 1 行） | ①标题 `TC-39 / TC-39b 确定命中：转盘落点与后端结果一致，弹层显示奖品名与入账提示`（逐字）；②M9 表头说明注释里的 `WinningRecordsView.vue` 行号引用（同句 2 处，含裸 `:NNN` 区段形态） | ①`TC-39 确定命中：转盘落点与后端结果一致，弹层显示奖品名与入账提示`（逐字）——判定见 §2；②「表头 `<TableHeader>` 是其上方兄弟节点。」（去行号） |
| 7 | `tests/e2e/api/stats-lib.mjs`（test-executor 面） | 5 行（文件头块注释 2 处 + 候选集枚举注释 2 处 + `restorePrizes` 注释 1 处） | 裸露行号锚点 5 处（`tests/e2e/qa.spec.ts` 区段、`IntegrationFixture.cs` 区段、`PrizeRepository.cs`、`AppDbContext.cs`、`IntegrationFixture.ResetAsync` 区段；均在注释内） | 对应稳定锚点：`tests/e2e/qa.spec.ts`（去区段）、集成测试夹具 `IntegrationFixture.ResetAsync`（2 处）、`PrizeRepository.GetDrawCandidatesAsync` 同口径、`AppDbContext` 全局查询过滤器 |
| 8 | `tests/e2e/api/stats-suite.mjs`（test-executor 面） | 1 行（文件头块注释） | TC-78 偏差说明中 `DrawService.cs` 的裸行号区段 | `DrawService` 的确定性分支 + 配置契约 `D-06` |

- **归属（同轮两角色同源，覆盖提交面全量）**：`src/**` + `tests/unit/**` 由 **engineer** 改；`tests/e2e/**` 由 **test-executor** 改（本表行 3～8 即其写入面）。两半为同一轮、同一提交批次，不拆成两条 CHG。
- **逐字对照的唯一权威记录 = 本批工作区的 `git diff -- src tests`**（本批未提交 → 无提交 hash；提交后 = 该提交对这两个路径的 diff）。本表只记形态与落点、**不转录行号**（13.4：行号只允许作被叙述对象出现；旧值已登记于 `docs/30-architecture.md`「引用勘误」块与 `60:OBS-05`，不在此重复第三处）。
- 改动量（实测 `git diff --numstat -- src tests`，2026-09-18 17:48 读数）：**18 增 / 18 删** —— 逐文件 `4/4`（`DrawService.cs`）、`1/1`（`perf-suite.mjs`）、`5/5`（`stats-lib.mjs`）、`1/1`（`stats-suite.mjs`）、`2/2`（`tc56b-tx-rollback.mjs`）、`1/1`（`tc56c-redis-degradation.mjs`）、`2/2`（`qa.spec.ts`）、`2/2`（`DrawServiceTests.cs`）。

### 1b. 同批第二部分：护栏与规则文本加固（实现期变更，非引用勘误）

| # | 落点 | 改前 → 改后 | 归属 |
| --- | --- | --- | --- |
| 1 | `tools/check-config.py`（T30 行号引用断言） | `_LN_REF` **扩面**：此前只匹配 `#LNN` 与 `xxx.md:NN`，看不见「代码文件:行号」形态 → 改为按扩展名清单匹配（`_LN_REF_EXT` 单点定义，扫描面扩展名自同一元组派生，消除「两处清单漂移」）；跳过判据改用声明闭集 `_TMP_PREFIXES`（`.tmp-` / `_tmp-`）；`_LN_EXEMPT` 增列 `docs/development-spec.md` 13.4 漂移示例的完整形态（行号是被叙述对象）；**新增断言**「临时目录前缀闭集与 `docs/role-protocol.md` §6 声明一致（双向锁）」（含反查：扫描面内不得存在形如临时目录而前缀未声明的目录） | 主对话（护栏维护方） |
| 2 | `docs/role-protocol.md` | §6 把临时目录前缀**闭集**写成明文（`.tmp-` / `_tmp-`），并注明义务「新增前缀须同时改本句与护栏 `_TMP_PREFIXES`，单边改即 FAIL」；原文里与护栏不一致的示例前缀已删除。同文件其余 hunk（§2 QX 编号段、§7.4 冻结时间条、新增 §7.6 校验基线条等规则文本加固）逐字见 `git diff -- docs/role-protocol.md` | 主对话 |
| 3 | `.gitignore` | `.tmp-*` 证据目录的放行只针对**本仓运行日志**（顶层 `.log`）；dotnet 每次调用新生成的 `Microsoft.NET.Workload_<pid>_<日期>_<时间>_<毫秒>.log`（约 1KB 工具链遥测，名字与内容每次调用都变）在其后**重新排除**（git 对同一路径按「末条匹配优先」判定，故该行不可上移）；并注明不做 `.tmp-*/**/*.log` 的死规则放行 | 主对话 |
| 4 | `tests/e2e/**` 的 6 处残余行号引用（T30 扩面后报出的 6 条，第 4 组） | `stats-lib.mjs` 4 处、`stats-suite.mjs` 1 处、`qa.spec.ts` 1 处 → 稳定锚点（逐处见 §1 表行 6 / 7 / 8） | test-executor |

- **如实记录（第 4 组的两点事实）**：①被报出的 6 处是 **HEAD 里既有**的（不是本批新引入的脏改动）；②`stats-lib.mjs` / `stats-suite.mjs` 两个文件**此前不在 `60:OBS-32` 登记的改动集清单里** → **本批提交面比 `60:OBS-32` 原登记宽这两个文件**，登记义务在本 CHG 内补齐（`60:OBS-32` 的状态回写归 code-reviewer）。
- **护栏读数（同一棵树实测，2026-09-18）**：检查总数 `364`（扩面前，`全部 PASS` —— 规则说「无行号引用」而扫描面上实有 6 处，属**机制比规则窄**）→ `365`（扩面后，`FAIL: 1 项`，逐条列出上述 6 处）→ `369`（终值，`全部 PASS`，6 处改完）；突变自测终值 `134/134 均被捕获、还原后全 PASS`（中间态实测 `132` 条 / `FAIL 4 条`，发生于并行落盘过程中）。该 `PASS → FAIL → PASS` 序列同时是 T30 扩面的**辨别力实测**（详见 §4 读数时序）。
- **三件改动量（实测 `git diff --numstat`，基线 HEAD `fc138ae`；读数时刻 2026-09-18 17:54，engineer 复测）**：`tools/check-config.py` **`656/19`**（`wc -l` = 1903 行）。**时点声明**：该文件在本批提交前仍由护栏维护方（主对话）持续加固 —— 此处记的是「**本 CHG 落盘时刻的读数**」、**不构成终值**；提交后的权威口径 = `git show --numstat <hash>`。（本行前稿曾记 `557/15`，系该文件的中间态读数，按 13.4 作废订正 —— 订正前已复测：三方口径交叉一致。）`docs/role-protocol.md` `6/3`、`.gitignore` `17/0`（三者均含同轮其他 hunk，逐字见各自 diff）。

### 2. `TC-39b` 判定：引用修复，非覆盖丢失

- **判定**：`TC-39b` **从未在 `docs/50-testcases.md` 登记过** —— 它是挂在用例标题上的**幽灵编号**，不承载任何独立判据；把它从标题去掉**不减少任何断言**，故属**引用修复**而非覆盖丢失。
- **依据（三条，逐条实测）**：
  1. `git log -S "TC-39b" --oneline -- docs/50-testcases.md` → **零命中**（无任何提交曾把该串写入用例表）。
  2. 用例表**字母后缀闭集**实测（`docs/50-testcases.md`）：`TC-18b`／`TC-25b`／`TC-38b`／`TC-38c`／`TC-48b`／`TC-48c`／`TC-56b`／`TC-56c`／`TC-59b`／`TC-68b`／`TC-73b`／`TC-79b` —— **无 `TC-39b`**。
  3. 用例体三条断言 ↔ `TC-39` 契约行（原文：「转盘**旋转 ≥ 2 圈**后停稳，落点与该奖品扇区**严格一致**；弹层（`page-draw--win`）显示「恭喜获得 {奖品名称}」+「已记入我的中奖记录」」）逐一对上：`expect.soft(winText).toContain('恭喜获得')`、`expect.soft(winText).toContain('中奖记录')`（弹层文案判据拆成的两条子串断言）与 `expect.soft(drawBody?.itemId, '落点（弹层奖品）与后端返回条目一致').toBe(1)`（落点判据）。即：**三条断言全部归属于 `TC-39`**。
- **如实说明（非本批引入、本批未改）**：契约判据中的「旋转 ≥ 2 圈」与「已记入我的中奖记录」全句**未被逐字断言**（弹层按子串「中奖记录」断言）——此为**既有覆盖粒度**；本批**未增删任何断言**，也未据此下调任何期望；如需补齐属测试设计 / 执行侧（`docs/50-testcases.md` 与 `tests/e2e/**`），本角色不越权处置。
- **同批同源**：`docs/51-defects.md` / `docs/52-qa-report.md` 各自已登记同一判定（对应契约用例 = `TC-39`；处置 = 订正标题；历史运行日志中的旧标题属历史快照、不回改）。本条与其口径一致。

### 3. 改后锚点核验（引用前实测，逐条读回原文）

| 新锚点 | 实测方式 | 结果 |
| --- | --- | --- |
| `D-08`（`docs/30-architecture.md` §3「异常与重试」） | Grep `D-08` + 读该节正文 | 存在；正文含「死锁 1213 → 整个事务重试 1 次，仍失败 → `1001`（HTTP 200）+ CRITICAL 告警」，与用例所钉口径一致 |
| `docs/30-architecture.md` §8.2「后端接口」 | 读该节表 | 存在；`POST /api/v1/draw` 行「目标 P95 = **≤ 200ms**」，与常量注释所写逐字一致 |
| `docs/30-architecture.md` §6.1「依赖降级矩阵」 | 读该节表头 | 存在；列名「链路 / Redis 不可用时的行为 / 是否可用」，与注释所写逐字一致 |
| `docs/error-codes.md` §「使用边界」（`1001` 条） | 读该节 | 存在；`1001`（系统繁忙）= 业务层主动降级提示（限流、熔断、依赖服务不可用等可重试场景），与注释所写语义一致 |
| `docs/50-testcases.md` `TC-39` | Grep `TC-39` + 读契约行 | 存在；判据见 §2 |
| `D-06`（`docs/30-architecture.md`，配置契约；`stats-suite.mjs` 新锚点） | Grep `D-06` | 存在（该文件内 20 行命中） |
| 集成测试夹具 `IntegrationFixture.ResetAsync`（`stats-lib.mjs` 新锚点） | Grep `ResetAsync` + 读签名 | 存在（`public async Task ResetAsync()`） |
| `PrizeRepository.GetDrawCandidatesAsync`（`stats-lib.mjs` 新锚点） | Grep 方法名 + 读签名 | 存在（`public async Task<IReadOnlyList<DrawCandidateDto>> GetDrawCandidatesAsync(CancellationToken)`） |
| `DrawService.ExecuteDrawAsync`（`tc56b` 新锚点） | Grep 方法名 + 读签名 | 存在（`private async Task<DrawResponseDto> ExecuteDrawAsync(…)`） |

### 4. 本轮验证记录（实际命令 / 工作目录 / 实际结果）

| # | 实际命令 | 工作目录 | 实际结果 |
| --- | --- | --- | --- |
| 1 | `dotnet build "src/backend/LuckyDraw.slnx" --nologo` | 仓库根 | **Build succeeded，0 Warning / 0 Error**（4.37 s）—— engineer 本轮**复跑实测** |
| 2 | 调度侧构建批次（输出目录见下「证据路径核验」） | 仓库根 | **0 警告 / 0 错误** —— **转述**调度侧读数（与 #1 同值；目录已实测存在） |
| 3 | `dotnet test "tests/unit/LuckyDraw.UnitTests" --nologo` | 仓库根 | **通过 96 / 失败 0 / 跳过 0 / 总计 96**，366 ms —— engineer 本轮**复跑实测**（与调度侧读数 96/0/0 一致） |
| 4 | 集成批次（调度侧；原始输出见下「证据路径核验」） | 仓库根 | **未通过**：`Failed: 43, Passed: 0, Skipped: 0, Total: 43`，106 ms —— 读原始日志汇总行实测；43 例**全部**止于夹具 `InitializeAsync`（`MigrateAsync`），错误原文 `MySqlConnector.MySqlException : Unable to connect to any of the specified MySQL hosts.` |
| 5 | `docker ps -a --format "{{.Names}} \| {{.Status}} \| {{.Ports}}"`（表内转义显示；实际命令用竖线分隔三列） | 仓库根 | **两个时点读数（如实并列）**：①集成批次运行时点（13:2x）—— `luckydraw-mysql` / `luckydraw-redis` 均 `Exited (255)`（端口映射仍登记为 3307 / 6379）；②17:5x 复测 —— 命令**失败**：`failed to connect to the docker API at npipe:////./pipe/dockerDesktopLinuxEngine … cannot find the file specified`（引擎不可达） |
| 6 | `docker info --format '{{.ServerVersion}}'` | 仓库根 | ①13:2x：`29.7.2` —— **引擎可达**；②17:5x 复测：不可达（同 #5 ②） |
| 7 | `netstat -ano` 全量输出中检索 `:3307` | 仓库根 | ①13:2x：**零命中**（宿主机无监听）；②17:5x 复测：**仍零命中** |
| 8 | `node --check`（复测：5 个改动过的 `.mjs` + 1 个已知坏样本） | 仓库根 | 改动文件 **exit 0 ×5**（`perf-suite` / `stats-lib` / `stats-suite` / `tc56b` / `tc56c`）；坏样本 `bad.mjs` **exit 1**（`SyntaxError`）—— 见 §6 |
| 9 | `python tools/check-config.py`（终值复跑） | 仓库根 | **共 369 项 / 全部 PASS ✓**；**校验基线：HEAD `fc138ae` / 工作区 脏 33 个文件**（按 `docs/role-protocol.md` §7.6：该 PASS 只证明工作区状态，不构成仓库级证明）。**读数时序（均 engineer 实测，同一棵树）**：`364 / 全部 PASS`（T30 扩面前 —— 扫描面上实有 6 处行号引用未被看见）→ `365 / FAIL 1 项`（扩面后，逐条列出 6 处）→ `369 / 全部 PASS`（6 处改完后） |
| 10 | `python tools/check-config.py --self-test` | 仓库根 | `自测开始：134 条突变，逐条负向验证` → `自测 PASS ✓（134/134 条突变均被捕获，还原后全 PASS）` —— engineer 实测复跑（同轮中间态读数 `132 条 / 自测 FAIL 4 条`，并行落盘进行中；两读数均为实测，终值以本条为准）。**机理说明（mtime 假阳性来源）**：该自测逐条改写护栏扫描面上的文件、断言捕获后**还原** —— 还原后闸门读数与自测前一致（`369 / 全部 PASS`）、`wc -l` 仍为 1903，但 **mtime 被刷新**（实测 `tools/check-config.py` mtime = `2026-09-18 17:49:48.733382800 +0800`，晚于同日首测读数时刻）。后续维护者看到「mtime 晚于编辑时刻」应归因于本自测，**不代表内容被改**（按 `docs/artifacts.md` §4，mtime 本就不作落盘证据） |

- **证据路径核验（逐个实测存在）**：
  - `D:\AI test\AI-Agents-workflow-Engineer-Rules\.tmp-build-annot-20260918-123530\`（调度侧构建输出目录，**登记绝对路径**）
  - `D:\AI test\AI-Agents-workflow-Engineer-Rules\.tmp-test-annot-20260918-123713\`（调度侧测试输出目录，含 `unit\` 与 `integration\` 子树）
  - `D:\AI test\AI-Agents-workflow-Engineer-Rules\.tmp-test-annot-20260918-123713\integration-run2.log`（**190197 字节**：集成失败的**原始输出**，运行时直接落盘、不在任何框架的清理范围内）
- **如实说明**：上述测试输出目录内**没有**单测 stdout 的落盘日志（只有构建产物与集成日志）；故单测读数以 #3 的本轮复跑为准，调度侧读数为同值对照。

### 5. 环境缺口与责任归属（「有理由的未复跑」闭集归类）

- **归类 = ② 环境客观不具备**（`docs/artifacts.md` §3.1 闭集）。分类说明：①「变更不触及该断言面」的检索证据（本批零可执行语句改动）**虽可给出**，但本轮**不复跑的事实依据是 ②** —— 以 ① 免除复跑会把「环境没搭好」静默转写成「不需要跑」；③ 无他人同版本复跑证据。
- **环境缺口（实测，两个时点如实并列）**：①集成批次运行时点（13:2x）：Docker 引擎**可达**（`docker info` → `29.7.2`），但 `luckydraw-mysql` 与 `luckydraw-redis` **两容器均 `Exited (255)`** → 宿主 `3307` / `6379` **无监听**（`netstat -ano` 检索零命中；17:5x 复测仍零命中）。②17:5x 复测：**引擎已不可达**（docker 命令报 `failed to connect to the docker API … cannot find the file specified`）—— 缺口加深，复跑仍受阻。集成夹具按 `AS-01` 连宿主 `3307` 与 `localhost:6379`，故 43 例**全部**止于夹具 `InitializeAsync`（`db.Database.MigrateAsync()`）—— **失败发生在任何用例进入测试体之前，属环境不具备，非代码缺陷**。
- **订正调度侧转述（如实记录，两时点并列）**：转述口径为「本机 Docker 引擎未运行」。engineer 在集成批次运行时点复查：**与该口径不符** —— 引擎在跑（`29.7.2`），退出的是两个容器；17:5x 复测时**引擎确已不可达**（该口径在此时点成立）。两时点对本条结论（环境不具备）均无影响；按证据纪律逐时点据实记录，**不以后者覆盖前者**。
- **责任归属**：**主对话**（环境调度：拉起两容器 / 另起等效实例窗口，然后调度集成复跑）。按 `docs/role-protocol.md` §8 第 5 条，本角色**不擅自启动 / 复位共享容器**（该 `3307` 实例同时承载开发库 `luckydraw_dev` 与测试库 `luckydraw_test`，动它可能抹掉他人正在使用的取证现场）。复跑执行方 = engineer。
- **`60:OBS-nn` 登记义务**：按 `docs/artifacts.md` §3.1，② 类须登记 `60:OBS-nn` + 责任人 + 计划时点；`docs/60-review.md` 的写入权归 **code-reviewer**，本角色无该文件写入权 → 在此如实登记缺口与责任，并**提请 code-reviewer 落 `60:OBS-nn`**。
- **计划复跑时点**：2026-09-19 前（与 `60:OBS-32` 的计划复核时点一致）。

### 6. 负向验证 / 等价负向样本（按 `docs/development-spec.md` 7.5 与角色输出要求第 2 条）

- **为何不存在「回退修复 → 断言必红」形态**：本批为引用修复（**非 `BUG` / `REV` 驱动**），且**零断言变更** —— 没有任何测试断言以「注释 / 标题里写的是稳定 ID 还是行号」为观测对象，故不存在可被回退操作翻转的断言；强行构造只能得到恒绿的伪验证。
- **等价负向样本（本轮实测，两步观测）**：观测对象 = 本批文字改动在**可执行性关口**（`.mjs` 解析）上的通过性 ——
  1. **已知坏样本必红**：`node --check tests/e2e/_tmp-verify-20260918-citefix/bad.mjs` → **exit 1**，报 `SyntaxError: Unexpected token ';'`（证明该关口非空转，不是「没有东西可检查就返回成功」的配置）；
  2. **本批改动文件必绿**：同命令对 `tests/e2e/api/perf-suite.mjs`、`tests/e2e/api/tc56b-tx-rollback.mjs`、`tests/e2e/api/tc56c-redis-degradation.mjs` → **exit 0 ×3**。
  - **证据强度 = 旁证**（本机直读，Node v24.15.0；样本目录为批次临时取证目录，非生产 CI）。
- **护栏 T30 侧的负向验证（本轮由护栏自测提供，engineer 实测复跑）**：T30 的逐条「造错 → 断言必红 → 还原复绿」由护栏自带 `--self-test` 承担 —— 实测 `自测 PASS ✓（134/134 条突变均被捕获，还原后全 PASS）`；其中对准 T30 行号引用断言与「临时目录前缀闭集（双向锁）」断言各有一条突变捕获记录。**另一条同树辨别力证据**：T30 扩面前后同一棵树读数 `364 / 全部 PASS` → `365 / FAIL 1 项（逐条列出 6 处）` —— 扩面前的「全 PASS」是**漏报**，扩面后才报出真实违反（见 §4 读数时序）。本角色**未修改 `tools/`**（写入范围仅本文件；护栏改动归主对话，见 §1b）；本批既有实验样本留存于 `tests/e2e/_tmp-verify-20260918-citefix/`（含故意坏样本 `bad.mjs`）备复核。

### 7. 影响面

- **零行为变更（本批核心结论）**：本批只改**注释与用例标题文本** ——
  - **无 `src/` 逻辑变更**：`DrawService.cs` 的 4 处改动全部位于注释 / XML 文档注释，可执行语句改动 **0 处**；
  - **无接口 / DTO / 错误码 / HTTP 状态码变更**（未触碰任何契约文件与错误码登记表）；
  - **无测试断言变更**：`git diff -- tests` 的 5 处改动 = 4 处注释 + 1 行用例标题字符串，断言行 **0 处**；
  - 无数据库 / 迁移 / 配置 / 前端（`src/frontend/**`）连带。
- **可执行面核查（实测）**：3 个改动过的 `.mjs` 全部 `node --check` **exit 0**；同一关口在坏样本上 **exit 1**（见 §6）。
- **用例标题变更面**：按标题检索历史 E2E 结果的命令需同步 —— `-g "TC-39"` 仍命中；`-g "TC-39b"` 不再命中（该串本就不属任何契约用例）。历史日志中的旧标题属历史快照、不回改。
- **护栏面（重测；旧读数「6 行 / 7 处」已失效作废，不再沿用）**：本批清零了行号引用残留 —— 全量扫描（口径同旧读数；另按 `docs/role-protocol.md` §6 前缀闭集排除 `.tmp-` / `_tmp-` 取证目录）**现为零命中**（engineer 实测，2026-09-18 17:5x；复现用）：

  ```text
  git grep -nE "[0-9a-zA-Z_./]+\.(md|cs|ts|mjs|vue|py|json|html):[0-9]+" -- src tests
  → 排除 tests/e2e/logs/ 、tests/e2e/.artifacts/ 与 .tmp-/ _tmp- 目录后：0 行
  ```

  旧读数「6 行 / 7 处」对应本批改动**落地前**的状态（那 6 处即 §1b 第 4 组，已由本批改完）；沿用旧数字会与工作区不符。
  另据实登记**护栏覆盖面差距（扩面后的剩余盲区，实测）**：T30 扩面后的匹配式仍不能表达两类形态 —— `<标识符>.<方法名>:<行号>`（无扩展名；本批实际清掉的此类有 2 处）与裸 `:NNN` / `:NNN-NNN` 区段（本批实际清掉的此类有 2 处：`tc56c` 的 §6.1 区段、`qa.spec.ts` M9 注释中 `:199-202` 后半段）。engineer 以两条更宽的检索式复扫 `src` + `tests`（同排除项）：**命中项全部为 URL / 端口 / JSON 字面量**（`http://127.0.0.1:5199`、`:5199`、`"code":500` 等），**无残留行号锚点**。该复扫同时证明：本批清掉的引用点里有 2 处**不在 T30 扩面后的报错清单里** —— 机制对上述两类形态仍无拦截，建议护栏维护方评估再扩面（本角色不改 `tools/`）。
- **安全**：无密码 / Token / 密钥涉及；改动文本不含敏感信息。

### 8. 回滚方式

- 逐文件把稳定 ID 引用换回原**裸行号**形态（反向应用 `git diff -- src tests` 即可逐字复原），并把 `qa.spec.ts` 的标题还原为 `TC-39 / TC-39b 确定命中：转盘落点与后端结果一致，弹层显示奖品名与入账提示`。
- 回滚后**行为完全等价**（注释 / 标题文本，零行为面）；但**不建议单独回滚**：回滚即重新引入 13.4 明令禁止的行号锚点（漂移后静默失效，本批修的就是这一事故），并使 `TC-39b` 这一幽灵编号重新挂上用例标题。
- 本产物侧回退：头部版本 v2 → v1、冻结时间回 `2026-09-17`、删除「本轮变更登记」行与「变更索引」的 `CHG-19` 行，并删除本节。
- **同批第二部分（护栏 / 规则文本 / `.gitignore`）的回退（书面建议，未执行）**：反向应用各自 `git diff` 即可逐字复原；**注意耦合**：`docs/role-protocol.md` §6 前缀闭集与护栏 `_TMP_PREFIXES` 双向锁 —— **单边回退即触发 T30 FAIL**，须成对回退；回退后 T30 恢复窄匹配面（`#LNN` 与 `*.md:NN` 之外的形态再次不可见）、`.gitignore` 回退则重新放行 dotnet 遥测日志噪声，均不建议。

### 9. 失效传播判定（按 `docs/artifacts.md` §5 矩阵）

- **`src/ 变更` → `tests/unit/**`**：**已在本轮实跑**（96 / 0 / 0）。
- **`src/ 变更` → `tests/integration/**`**：**需复跑 —— 本轮未通过（环境不具备，见 §5）**；在复跑产出有效结论前，本轮**不得**记为「已复跑」。
- **`src/ 变更` → `tests/e2e/**`**：**归 test-executor 复核 / 复跑** —— 本批在 `tests/e2e/` 改动 12 行（6 文件：注释 11 行 + 用例标题字符串 1 行），断言语句 0 处；脚本 / 用例文件本身已变，是否复跑按 `docs/50-testcases.md` 口径由 test-executor 判定（本角色不越权处置该目录）。
- **`tools/check-config.py` / `docs/role-protocol.md` / `.gitignore`（同批护栏与规则文本加固）**：`docs/artifacts.md` §5 矩阵**未收录 `tools/**` 与 `docs/role-protocol.md` 两类上游** → 无强制扇出行；按同一机理人工判定：①凡复述护栏 PASS 的产物 / 摘要须带**校验基线**（`docs/role-protocol.md` §7.6，本轮已在本节 §4 执行）；②前缀闭集与护栏 `_TMP_PREFIXES` 双向锁 —— 单边回退即护栏 FAIL（见 §8）；③其余产物（51 / 52 / 60）对本半的复核归 **code-reviewer**。
- **`40-changelog 变更` → `51-defects` / `52-qa-report` / `60-review`**：**需回写 / 复核** —— `60:OBS-32`（阻断）中「无 CHG 记录」一项可据本条转为**已登记**；其余三项（未提交 / 未复核 / 集成未复跑）仍挂 → **是否闭环由 code-reviewer 判定**；`51` / `52` 的「执行侧补验标签勘误」行与本条同批同源，如引用需同步。本角色无这三份文件的写入权。
- **前端 `src/frontend/**`**：**零命中**（本批未改）→ 按 §5 不触发该面，未重跑前端单测 / 构建（如需复跑属独立批次）。

- **关联**：`60:OBS-32`（阻断）、`60:OBS-05`（同源）、`docs/development-spec.md` 13.4（行号锚点禁令与漂移实测）、`docs/artifacts.md` §3.1（「有理由的未复跑」闭集）/ §4（版本与冻结）/ §5（失效传播矩阵）、`D-08`（`docs/30-architecture.md` §3「异常与重试」）、`docs/error-codes.md` §「使用边界」、`docs/50-testcases.md` `TC-39`、`CHG-16` / `CHG-17` / `CHG-18`（本批注释锚点的产生来源；`CHG-18` §1「未改动（刻意）」明写「如需统一锚点，建议单独批次处理」—— **本批即该批次**）、`docs/51-defects.md` / `docs/52-qa-report.md`「执行侧补验标签勘误（2026-09-18）」、`docs/role-protocol.md` §6（临时目录前缀闭集 / 双向锁义务）/ §7.6（校验基线）、`tools/check-config.py` T30（扩面与双向锁；自测 134/134）、`.gitignore`（`.tmp-*` 放行口径）。

---

## CHG-20 集成夹具的 Redis 覆盖值修复：连接与清理目标一律从 `LuckyDrawApiFactory.RedisConfiguration` 派生（`51:OBS-12` 闭环改动；含守护断言 + 实测负向验证）

- **状态**：**已落地**（`tests/integration/**` 2 个文件 / 74 增 7 删；构建 0 警告 / 0 错误（依据：本批各 `dotnet test` 运行日志中 `warning` / `Warning` / `警告` 命中数均为 0）；集成**判别轮 43/43**、**对照轮 43/43**；单测 96/0/0；护栏 **373 项 全部 PASS**，校验基线见 §2）。
- **来源与调度**：`docs/51-defects.md` §3 **`51:OBS-12`（阻断；归类 = `执行侧`）** —— 夹具声明支持 `LUCKDRAW_TEST_REDIS` 覆盖、实现却把实例与库号写死（「半截覆盖」）；test-executor 已独立复现并留存运行期证据。本条提供修复与可闭环证据；`51` 的台账状态回写归 test-executor（本角色不改该文件）。
- **性质**：**执行侧（测试基建 / 夹具）修复** —— `src/` 产品代码**零改动**；无新第三方依赖、无表结构 / DTO / 接口契约变更、无目录结构变更、无新增或删除用例 —— 未触发「必须确认的场景」。
- **上游契约变更**：**无**（`docs/10/20/30/50/51/52/60-*` 与 `docs/error-codes.md` 一律未触碰）。
- **关联**：`51:OBS-12`（驱动项）、`FR-02`（登录与登出；会话刷新语义未变，本改动只修夹具的清理靶）、`D-03`（幂等数据库兜底）、`D-06`（配置契约）、`D-15`（认证幂等）、`REV-09`（口令 / 密钥出仓）、`AS-01`（开发态端口映射）、`CHG-19`（上一条；本批沿用其「证据路径逐个核验 + 引用用稳定 ID」体例）、`QX-02`（本批新增待确认项，见文末）。
- **落盘证据强度（如实标注）**：本批**尚未提交**（本角色不执行 git 提交，提交由主对话统一执行）→ **无提交级落盘证据**；下列读数均为**实测**（命令 + 工作目录 + 实际结果），所引证据路径已逐个核验存在。

### 1. 改动清单（`git diff --stat -- tests/integration` 实测：2 文件 / 74 增 7 删）

| # | 文件 | 改动 |
| --- | --- | --- |
| 1 | `tests/integration/LuckyDraw.IntegrationTests/IntegrationFixture.cs` | ① **连接**：`ConnectionMultiplexer.Connect("localhost:6379,allowAdmin=true")` → 改为解析 `LuckyDrawApiFactory.RedisConfiguration` 得到 `ConfigurationOptions` 后连接；② **清理库号**：新增 `RedisDatabase` 属性 = 同源解析出的 `defaultDatabase`（未配置时 db0），替换两处字面量 `1`；③ **清理端点**：`GetServer("localhost:6379")` → 本连接自身的 `GetServers()`（不写死地址）；④ **新增守护** `EnsureRedisTargetMatchesApp()`（夹具 init 期即比对，见 §5）；⑤ 类与属性的 `<summary>` 改为实然陈述 |
| 2 | `tests/integration/LuckyDraw.IntegrationTests/LuckyDrawApiFactory.cs` | **仅** `<summary>` 扩写：写明该属性是「被测应用与集成夹具的共同取值来源」、夹具侧 `allowAdmin` 只在其派生副本上打开。**属性值本身逐字未变**，应用侧配置**未**新增 `allowAdmin` |

- **`51:OBS-12` 点名的两个坑，逐条处置**：① **`allowAdmin` 只加在夹具派生副本上** —— `options.AllowAdmin = true` 作用于夹具自行 `Parse` 出来的那份；注入应用的 `Redis__Configuration` 取自 `RedisConfiguration` 原值（无 `allowAdmin`），应用不需要 admin 权限；② **清理库号不再是字面量 `1`** —— `FlushDatabase(...)` 与 `Keys(database: ...)` 与 `GetDatabase(...)` 三处同取 `RedisDatabase`（= 配置的 `defaultDatabase`），与 `RedisConnectionProvider` 的 `GetDatabase()`（无参 → 配置库）**同源同值**。

### 2. 判别轮 / 对照轮（原始输出关键行，工作目录 = 仓库根）

| 轮次 | 实际命令 | 原始输出关键行 |
| --- | --- | --- |
| **判别轮（覆盖值 db2）** | `LUCKDRAW_TEST_REDIS="localhost:6379,defaultDatabase=2" LUCKDRAW_TEST_CONNECTION_STRING="Server=localhost;Port=3407;Database=luckydraw_test;User Id=root;Password=devonly;CharSet=utf8mb4;SslMode=None;AllowPublicKeyRetrieval=True" dotnet test tests/integration/LuckyDraw.IntegrationTests --nologo` | `Passed!  - Failed:     0, Passed:    43, Skipped:     0, Total:    43, Duration: 13 s`（exit=0） |
| **对照轮（不设覆盖值）** | 同上，去掉 `LUCKDRAW_TEST_REDIS` | `Passed!  - Failed:     0, Passed:    43, Skipped:     0, Total:    43, Duration: 13 s`（exit=0） |

- **负向对照（同命令、修复前）**：`Failed: 1, Passed: 42, Total: 43`，失败例 = `AuthApiTests.Refresh_WithoutCookie_Reports1204`，失败原文 `Expected envelope!.Code to be 1204 because HTTP 200 / {"code":1203,...}, but found 1203 (difference of -1)` —— 本轮 §4 样本 A 已复现该读数，两次独立观测（test-executor 批 / 本批）一致。
- **护栏读数**：`python tools/check-config.py` → `共 373 项检查 / 全部 PASS ✓`，**校验基线：HEAD 6a62868 / 工作区 脏 11 个文件**（本 CHG-20 落盘后的末次运行读数；落盘前同一命令为「脏 10 个文件」，两次均 373 项全 PASS）（role-protocol §7.6：该 PASS 只证明工作区状态）；`python tools/check-config.py --self-test` → `自测 PASS ✓（141/141 条突变均被捕获，还原后全 PASS）`（门禁非空转的实测；自测改写后已按 sha1 逐文件复核，3 个受检文件哈希前后一致）。
- **单测**：`dotnet test tests/unit/LuckyDraw.UnitTests --nologo` → `Passed!  - Failed: 0, Passed: 96, Skipped: 0, Total: 96`（本批未触碰 `src/`，此轮为「不受影响」的对照读数）。

### 3. 清理目标已修正的正向证据（redis-cli 探针实测，隔离 Redis `luckydraw-iso-redis`）

约定：每轮开跑前在 db0 / db1 / db2 各放 1 个无 TTL 的 marker `OBS12FIX:marker:dbN`；跑完看谁被动。

| 轮次 | db0 marker | db1 marker | db2 marker | 运行中/运行后应用键落点 | 判读 |
| --- | --- | --- | --- | --- | --- |
| 判别轮（覆盖 db2，修复后） | 存活 | **存活**（不再被错靶抹除） | **被抹** | db2 运行后 `DBSIZE = 0`（逐例清理到位） | 清理靶 = 应用实际库 db2；**别的库不再被动** |
| 对照轮（无覆盖值，修复后） | 存活 | **被抹** | 存活 | 运行中 ~16s 探针 db1 出现 `draw:auth:refresh:459 / 463 / 458` | 清理靶 = 默认 db1（= 应用实际库）；应用确实写进配置库 |
| 负向样本 A（回退修复 + 覆盖 db2） | 存活 | **被抹**（错靶） | **存活 + `DBSIZE = 72`**（`draw:auth:*` 41 / `draw:idempotency:*` 30 / marker 1） | 运行中 db2 键数 1 → 25 → 72（应用键全部落在 db2） | 复现缺陷本体：**清理错靶 + 应用库从未被清理** |

- 判读依据（应用侧代码只读核实）：`RedisConnectionProvider` 用 `ConfigurationOptions.Parse(配置串)`，四个存储一律 `GetDatabase()`（无参）→ 应用所用库 = 配置里的 `defaultDatabase`。
- **留下的 key 与最终状态**：本批共写入 3 个 marker（`OBS12FIX:marker:db0/1/2`，无 TTL），负向样本期间 db2 累计过 72 / 143 / 214 个应用残留键；登记与复位见 §8，**最终 = 三库全空**（与跑前一致）。

### 4. 负向验证（`docs/development-spec.md` 7.5 第 6 条：三步观测逐条实测）

回退/半截样本由补丁脚本 `tests/integration/_tmp-obs12-fix-20260920-01/10-apply-fix.py` 生成（逐段断言原文命中 1 次后才落盘；每种模式各留一份 `git diff` 快照）。

| 样本 | 回退 / 构造方式 | 观察到的失败（实测原文） | 还原后 |
| --- | --- | --- | --- |
| **A 回退修复**（= 修复前代码） | `--mode revert`：连接与清理目标全部退回 `localhost:6379` + 字面量 db1 | `Failed!  - Failed: 1, Passed: 42, Skipped: 0, Total: 43`（exit=1）：`Expected envelope!.Code to be 1204 because HTTP 200 / {"code":1203,...}, but found 1203 (difference of -1)` | `--mode fix` → 判别轮 **43/43 复绿**（exit=0） |
| **B 守护断言负向样本**（「只改连接、不改清理目标」） | `--mode halflink`：连接已按配置派生，清理库号写死 `1` | `Failed!  - Failed: 43, Passed: 0, Skipped: 0, Total: 43`（exit=1）：`System.InvalidOperationException : 夹具 Redis 清理目标与应用不一致：应用 = Unspecified/localhost:6379 / db2；夹具 = Unspecified/localhost:6379 / db1。`（夹具初始化即抛，**43 例全部未进入测试体**） | `--mode unhalf` → 复绿（同「regreen」轮） |
| **C 守护断言的边界样本** | `--mode halfsilent`：库号已派生，但 `FlushDatabase` / `Keys` 仍写死 `1` | 守护**看不见**（参数值无法自省）→ 运行期出现与样本 A 同形的**静默**失败：`Failed: 1, Passed: 42`（1203 vs 1204） | `--mode unhalf` → 复绿 |

- 结论：**修复的两个半边各自都是载荷** —— 只改连接（样本 B）会被守护断言在夹具初始化期拦下；只改连接且不设守护（样本 C）会退回静默错靶。样本 A/B/C 在还原后全部复绿（判别轮 43/43、对照轮 43/43，均为终态代码实测）。

### 5. 新增断言 / 守护的辨别力依据

- **`EnsureRedisTargetMatchesApp()`**（夹具 `InitializeAsync` 内）：一侧取宿主 DI 里**已绑定**的 `Redis:Configuration`（应用真正读到的那份），另一侧取夹具**实际生效**的目标（连接端点 = 本连接连到的端点；库号 = 清理命令实际使用的 `RedisDatabase`），逐项相等才放行，否则抛 `InvalidOperationException`。
  - **它在什么状态下失败**：任何「夹具生效目标 ≠ 应用已绑定配置」的状态 —— 实测触发条件 = 清理库号写死（样本 B，失败读数见 §4）；同理可捕获「夹具连接写死地址而应用走覆盖值」「应用侧注入路径被改动 / 被 `appsettings` 顶掉」这两类。
  - **它不覆盖**：「库号已派生、清理语句仍写死字面量」（样本 C 实测，参数值无法自省）→ 已在守护的文档注释中**显式声明该边界**，该形态由「单一调用点 + 唯一取值来源」约束。
- **既有断言的变动：零** —— 本批未新增 / 未修改 / 未删除任何用例断言；失败例的期望值 `1204` 来自契约（`docs/error-codes.md` 已登记的会话过期码），**未放宽**。

### 6. 影响面

- **产品面：零** —— `src/` 未改动，接口 / DTO / 错误码 / HTTP 状态码 / 数据库 / 迁移 / 前端全部未触碰；集成 43 例的**判据一字未改**。
- **测试面**：集成夹具的 Redis 目标语义由「写死 db1」变为「跟随 `defaultDatabase`」——
  - 默认运行（不设覆盖值）**行为等价**（target 仍是 `localhost:6379` / db1）；
  - 设 `LUCKDRAW_TEST_REDIS` 时行为**修正**：清理落在应用真正使用的库，且**不再误清其他库**（此前每例都会 `FlushDatabase(1)`，会抹掉同一实例上 db1 的他人数据 —— 这是本缺陷的破坏面）。
  - `ResetIdempotencyCacheAsync()`（`ConcurrencyTests` 用于验证 `D-03` 数据库兜底）同轮修正：此前在覆盖值场景下删的是 db1 的幂等键。
- **运行依赖**：夹具 init 期即建连（守护需要端点）→ 与「Redis 不可达则 43 例全部不可运行」的既有语义一致，只是失败时点提前到夹具初始化（原先在首例 `ResetAsync`）；见 §10 的实测。
- **护栏面**：`tests/` 在 T30 行号引用扫描面内 —— 本批新增注释**未使用任何「文件:行号」型引用**（一律稳定 ID：`51:OBS-12` / `CHG-20`），护栏 373 项全 PASS 为证。

### 7. 回滚建议

- **逐文件反向应用 `git diff -- tests/integration`** 即可逐字复原（两文件均含完整改动；补丁脚本另留 `20-diff-after-fix.txt` 快照）。**不建议单独回滚**：回滚即恢复「覆盖值有效、清理无效」的错靶状态（样本 A 实测：`Failed: 1 / Passed: 42` + 误抹 db1）。
- 本产物侧回退：删除本 CHG-20 节、`变更索引` 的 `CHG-20` 行、头部「本轮变更登记（2026-09-20）」行与 `QX-02` 行，并把「版本」回 `v2`、「冻结时间」回 `2026-09-18T17:54:00+08:00`。

### 8. 环境与复位登记（`docs/role-protocol.md` §8 三段式）

- **用到的环境**：隔离 MySQL `luckydraw-iso-mysql`（`localhost:3407`，库 `luckydraw_test`）、隔离 Redis `luckydraw-iso-redis`（`localhost:6379`）。**共享容器 `luckydraw-mysql` / `luckydraw-redis` 全程未触碰**（未对其发起任何 docker 命令）；隔离容器未停 / 未删 / 未重建。
- **破坏性操作（实测记录）**：① 3 个 marker 的写入与删除；② 负向样本期间 db2 累计 214 键的应用残留 → **11:14 实测 `FLUSHDB`（db2）** 后为零（此后 regreen 轮从空库起跑）；③ `dotnet test` 对 `luckydraw_test` 库执行夹具既定的 DELETE / UPDATE。
- **复位实测（§8 第 2 段）**：删 marker 后 `db0 = 0 / db1 = 0 / db2 = 0`、`INFO keyspace` 空 —— **与跑前（11:07 探针，三库全空）一致**；证据 `80-restore-and-final-state.txt`。
- **复位之后仍有追加运行（先后顺序如实登记）**：§10 的死端口轮（11:20）跑在上述复位**之后**，因此复位文件不覆盖它；该轮结束后的终态探针（11:27 实测）为 `db0 = 0 / db1 = 0 / db2 = 0`、三库 key 列表为空、marker 全 0 → **终态 = 空，与跑前（11:07）一致**；证据 `82-post-deadport-final-probe.txt`。
- **归属声明（§8 第 3 段）**：本批期间隔离 Redis 内的所有 key 均由本批产生（跑前为空，有探针为证）；`luckydraw-iso-*` 容器的其余流量非本批产生。

### 9. 失效传播判定（按 `docs/artifacts.md` §5 矩阵）

- **`src/ 变更` → `tests/**`**：本批 `src/` **未改** → 无该扇出；但为闭环 `51:OBS-12` 仍实跑了 `tests/integration/**`（判别轮 + 对照轮各 43/43）与 `tests/unit/**`（96/0/0）。
- **`tests/integration/**` 变更 → `tests/e2e/**`**：矩阵未单列该项；`tests/e2e/**` 不消费本夹具（其为 Playwright 侧、经 `tests/e2e/lib/launch-api.mjs` 起实例）→ 判定**不触发复跑**；该目录的最终判定权归 test-executor。
- **`40-changelog 变更` → `51-defects` / `52-qa-report` / `60-review`**：`51:OBS-12`（阻断）的**修复侧证据已具备**（本 CHG + 证据目录）→ 状态回写（`已闭环` 与否）与「下一轮集成回归」归 test-executor；本角色无这三份文件的写入权。

### 10. 未实测 / 存疑（如实登记，不粉饰）

- **原「未实测项」已转实测**：`51:OBS-12` 登记的「若 Redis 无监听则 43 例全部不可运行」在 test-executor 批为**旁证（代码路径推导）**；本批以**死端口**等价构造实测（`LUCKDRAW_TEST_REDIS="localhost:6399,defaultDatabase=2"`，等价于「无监听」，且**无需停容器**）：`Failed!  - Failed: 43, Passed: 0, Skipped: 0, Total: 43, Duration: 59 ms`（exit=1），首条异常为 `StackExchange.Redis.RedisConnectionException : It was not possible to connect to the redis server(s).` —— 夹具初始化期即失败，**无任何用例进入测试体**；该轮跑在 11:17 复位后的空实例上，其后的终态探针（11:27）实测 `db0 / db1 / db2` 的 `DBSIZE` 均为 0 → 未写入任何键。**如实标注**：测的是 6399 死端口而非「6379 被停」，二者对夹具路径等价，但「同端口被停」的具体形态仍**未实测**（隔离容器禁停，环境约束）。
- **`QX-02`（新增待确认项）**：失败例名为 `Refresh_WithoutCookie_Reports1204`，但夹具 `Client` 由 `WebApplicationFactory` 默认参数创建（`HandleCookies` 默认 `true`）→ 请求**实际带着历史 refresh cookie**，判据走「会话不存在 → `1204`」而非「未携带 cookie → `1204`」。修复后该例在两种状态下都通过，故本批**未动它**（改夹具的 cookie 行为会波及 43 例的既有语义，超出 `51:OBS-12` 面）。
- **旁证声明**：本批所有运行均为**本机隔离容器**环境（非生产 CI），证据强度 = 旁证（`dotnet test` 原始输出 + `redis-cli` 探针 + 复位实测）；`git diff` 快照为版本控制级，但本批**尚未提交**，故不构成仓库级证明。


## 待确认清单（默认假设）

| 编号 | 假设 | 依据 |
| --- | --- | --- |
| AS-01 | 开发态 MySQL 容器映射宿主 **3307**（非 3306），Redis 6379 | 本机 3306 已被既有容器占用，绑定失败；`appsettings.Development.json` 已同步 |
| AS-02 | 幂等键并发冲突返回 HTTP 409 + 业务码；其余业务异常一律 HTTP 200 + `code≥1000` | 架构 §5 幂等语义 + D-12；错误码取自 `docs/error-codes.md` 已登记范围 |
| AS-03 | 限流拒绝（429）保留 HTTP 429 状态码而非 200 | 限流属基础设施层拒绝，非业务校验失败；响应体仍为 `ApiResult`（`code=1001`） |
| AS-04 | `Microsoft.EntityFrameworkCore.Design` 计入设计期工具依赖（非运行期） | `dotnet ef` 必需；不影响运行期依赖面 |
| AS-05 | API-08 的 `winTime` 线上取值由 `2026-09-17T06:51:42.397853`（无时区标识）变为 `2026-09-17T06:51:42.397853+00:00`（BUG-02 修复），判为**向后兼容的格式补全**，v1 **不升版** | 契约本就要求 `winTime` = ISO 8601（UTC）（架构 §5.1 / API-08），修复是让实现补齐契约；前端 `parseUtc` 对新旧两种形态均兼容（实测）。**若产品认为线上取值变化需走 v2 升版，请裁决**——本轮按「补齐契约、不升版」执行 |
| AS-06 | 重试耗尽的终态错误码**为 `1001`「系统繁忙，请稍后重试」**（HTTP 200） | **已由用户裁决确认（CHG-17）**：保持代码现状 `1001`；架构 `:544` 的「500」表述由 software-architect 并行修订 `docs/30-architecture.md`（本角色未触碰该文件）。原冲突记录见 CHG-16「未裁决项 (1)」 |
| AS-07 | 整事务重试预算**为 2 次尝试（重试 1 次），最坏用户等待 100s** | **已由用户裁决确认并落地（CHG-17）**：`MaxTransactionAttempts` 由 3 改为 2，对齐架构 `:544`「整个事务重试 1 次」；原冲突记录见 CHG-16「未裁决项 (2)」 |
| QX-01 | 新增 CHG 条目是否应升版：本产物头部原为「版本 v1 / 冻结时间 `2026-09-17`」，而 `CHG-14`…`CHG-18` 落盘后头部未同步；本批（`CHG-19`）按 `docs/artifacts.md` §4「正文契约内容变更 → 升版」执行 **v1 → v2** 并同步冻结时间。**若需与历史批次口径一致（只登记、不升版）**，请裁决后回退（回退办法见 `CHG-19` §8 末条）。 | `docs/artifacts.md` §4「冻结后仍有落盘时的处置」；主对话调度指令（本批次） |
| QX-02 | 集成失败例 `AuthApiTests.Refresh_WithoutCookie_Reports1204` 的**用例名与实际请求不符**：夹具 `Client` 由 `WebApplicationFactory` 默认参数创建（`HandleCookies` 默认 `true`）→ 该请求实际携带历史 refresh cookie，判据走「会话不存在 → `1204`」而非「未携带 cookie → `1204`」。**本批未改**（改夹具 cookie 行为会波及 43 例既有语义，超出 `51:OBS-12` 面）。**若要严格对齐用例名**，可选：① 该用例改用独立 client（`CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false })`）；② 或按契约订正用例名 / 判据。属**测试设计面**（契约归 `docs/50-testcases.md`），请裁决后另行调度 | 本批实测：修复前该例失败值 `1203` 只有在**请求带 cookie** 时才可能产生（控制器在 cookie 为空时直接抛 `1204`），故「带 cookie」为实测推论而非推测 |
