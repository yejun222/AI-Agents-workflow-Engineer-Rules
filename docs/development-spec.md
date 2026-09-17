# 项目全局开发规范（全量版）

> **用途**：本文件是全量开发规范，供人员阅读与评审参考。AI 编码助手每次会话自动加载的是精简版 [CLAUDE.md](../CLAUDE.md)——它只含核心红线与工作准则，详细规则以本文件为准；两者冲突时，以本文件为准。
> **生效原则**：本规范优先级高于通用最佳实践，冲突时以本文件为准。
> **维护**：由技术 Lead 牵头，每季度 review 一次；修改必须走 MR 并同步通知全组。**修改后必须检查 CLAUDE.md「三、核心红线」与「四、必须确认的场景」是否需同步更新。**

---

## 一、项目概述

| 项 目 | 说 明 |
| --- | --- |
| 架构模式 | 前后端完全分离，RESTful API 交互 |
| 核心场景 | 用户管理、权限控制、数据查询、业务表单、报表统计 |
| 开发模式 | AI 辅助编码 + 人工代码评审 + CI 强制校验 |

---

## 二、技术栈与版本要求

### 2.1 前端

| 技术 | 版本 | 强制要求 |
| --- | --- | --- |
| Vue | 3.5+ | 必须使用 Composition API + `<script setup lang="ts">`，禁止选项式 API |
| TypeScript | 5.0+ | strict 模式；禁止 `any`，所有变量/函数/参数显式声明类型 |
| Tailwind CSS | 4.x | 优先原子类，禁止新增独立 CSS 文件；主题配置用 CSS `@theme` |
| shadcn-vue | 最新稳定版 | 组件源码位于 `src/components/ui`，**禁止直接修改源码** |
| TanStack Vue Table | 8.x | 所有列表页必须使用，禁止手写原生表格逻辑 |
| Zod | **3.25.x（锁 3.x）** | ⚠️ **禁止 4.x**：`@vee-validate/zod` 的 peerDependency 是 `zod: ^3.24.0`，装 4.x 直接解析失败 |
| vee-validate + @vee-validate/zod | 4.15.x | 所有表单必须定义 Schema，与后端 DTO 校验规则对齐 |
| Axios | 1.x | 必须使用全局封装实例，禁止直接调用原生 axios |
| Pinia | 4.x | 管理用户信息、权限、全局配置 |
| Vue Router | 4.x | 全局前置守卫统一做登录校验、权限校验 |
| Vite | 8.x | 配置路径别名、自动导入、接口代理 |
| unplugin-auto-import | 最新版 | Vite「自动导入」依赖插件：组合式 API 按需导入 |
| unplugin-vue-components | 最新版 | Vite「自动导入」依赖插件：组件按需导入（含 shadcn-vue 组件） |
| Node.js | 24 LTS | 本地与 Docker 构建统一版本（Vite 8 要求 ≥20.19 / ≥22.12） |
| ESLint + Prettier | 10.x / 3.x | 统一代码风格与静态检查（CI lint 步骤）；配置随仓库提交 |

> ⚠️ **本表的"最新版"≠ npm 上的 latest**：好几个库的 latest 已经跨了大版本（vue-router 现在是 5.x、TypeScript 是 7.x、TanStack Table 是 9.x），`npm install <包名>` 装到的**不是规范要的版本**。**新增依赖必须按本表锁版本**（如 `npm install vue-router@^4.6`），拿不准时先 `npm view <包名> versions` 确认。

### 2.2 后端

| 技术 | 版本 | 强制要求 |
| --- | --- | --- |
| ASP.NET Core Web API | .NET 10.0 LTS | 使用 `[ApiController]`，顶层路由统一前缀 |
| EF Core | 10.0 | Code First，迁移管理表结构 |
| Microting.EntityFrameworkCore.MySql | 10.0.11 | Pomelo 尚无 EF Core 10 适配版（9.0.0 仅支持 EF Core 9）。**实测 Pomelo 9 + EF Core 10 组合运行期崩溃**：`NoWarn` 只能压住 NU1608 警告，压不住运行期的 `TypeLoadException` / `MissingMethodException`，而且 `build` 与生成迁移都能通过，要到真连库查询才暴露——所以**"编译通过"不能作为选型依据，必须真跑一次迁移 + 真查一次库**。改用 Microting 分支（10.x）：`UseMySql` API 一致，仅换包名与命名空间。禁止跨大版本混用。**回迁条件**：Pomelo 官方发布支持 EF Core 10 的稳定适配版后可回迁；回迁前必须按同一标准实测（生成迁移 + 真连库查询），并走规范第四章「先确认再动手」的依赖变更确认 |
| JWT Bearer | 框架内置 | 接口默认需要认证，公开接口显式标记 `[AllowAnonymous]` |
| FluentValidation | 最新版 | 后端 DTO 入参校验（见 8.2） |
| Serilog | 最新版 | 结构化日志：请求链路、异常堆栈、业务日志 |
| StackExchange.Redis | 最新版 | 分布式缓存（见第九章） |
| Asp.Versioning | 最新版 | URL 版本控制（见 6.1） |

测试类库（xUnit / FluentAssertions / NSubstitute / Testcontainers / Microsoft.AspNetCore.Mvc.Testing）见 7.1；限流见 8.6。

### 2.3 数据库与部署

MySQL 8.4 LTS / Docker 24+ / Docker Compose v2+ / Nginx alpine 稳定版

### 2.4 新增依赖原则

不引入本规范未提及的第三方依赖；确需新增必须先说明理由并征得同意。

---

## 三、前端工程规范

### 3.1 标准目录结构

```
src/
├── api/                  # API 接口定义，按业务模块分文件
├── assets/               # 静态资源
├── components/
│   ├── ui/               # shadcn-vue 原生组件【只读，禁止修改】
│   └── business/         # 业务封装组件
├── composables/          # 组合式函数（useXxx）
├── directives/           # 自定义指令（v-auth 权限指令等）
├── layouts/              # 全局布局组件
├── lib/utils.ts          # shadcn-vue CLI 生成的 cn() 助手，与其组件一并视为只读
├── router/index.ts       # 路由配置 + 全局权限守卫
├── stores/               # Pinia 状态管理，按领域分文件
├── types/                # TS 类型定义，与后端 DTO 一一对应
├── utils/request.ts      # Axios 全局封装实例
└── views/                # 页面组件，按业务模块分子目录
```

### 3.2 组件规则

**ui 与 business 的边界判定**：
- `components/ui` = 通用、无业务语义的展示控件（Button、Dialog、Select…），来自 shadcn-vue，**只读**
- `components/business` = 至少依赖一个业务实体类型或业务 Store 的复合控件（如 UserSelectDialog、DeptTreeSelect）
- 业务定制 ui 组件：通过外层包裹、props 传递、插槽、Tailwind 类名覆盖实现，**禁止改源码**
- 新增 ui 组件必须通过 `npx shadcn-vue@2.x add 组件名` 添加（固定主版本保证可复现，禁止 @latest），禁止手动复制

**TanStack Table**：
- 列定义必须使用 `ColumnDef<T>` 泛型，与实体类型绑定
- 排序、分页、筛选状态与后端查询参数联动，状态变化自动触发请求
- 禁止手写表格行渲染、排序切换等基础逻辑

**Zod 表单**：
- Schema 定义在对应 `types` 文件中，校验规则与后端 DTO 校验规则（DataAnnotations + FluentValidation）完全一致
- 使用 vee-validate（`@vee-validate/zod`）绑定，错误统一通过 FormMessage 展示
- 禁止手动编写 if-else 校验逻辑

### 3.3 状态管理判定标准

| 场景 | 方案 |
| --- | --- |
| 跨 ≥2 个页面或组件树共享（用户信息、权限、全局配置） | Pinia Store |
| 仅单页面内部状态 | `ref` / `reactive` |
| 可复用的有状态逻辑 | 抽成 `composables/useXxx.ts` |

- **access token 仅存内存**（不落 localStorage / sessionStorage）：页面刷新或新标签页打开后，由**受保护路由**触发一次静默刷新恢复会话，退出登录同步清空
  - 理由：localStorage 里的 access token 一次 XSS 即可被读走；仅存内存把凭证的生命周期限制在当前页面上下文内。代价是刷新后必须有一次静默恢复
  - 恢复逻辑必须做到「**只在受保护路由触发，公开页不触发**」——否则每次进入公开页都会白打一次刷新接口；反过来，若改用标签页级存储（sessionStorage）缓存登录态，会导致**新标签页即使 refresh Cookie 有效也被强制登出**
- **refresh token 禁止存 localStorage**，必须走 httpOnly + Secure + SameSite=Strict Cookie（一次 XSS 即可窃取 7 天长期凭证），刷新时由浏览器自动携带
- 禁止把页面私有状态塞进全局 Store

### 3.4 请求规范

- 所有接口必须调用 `@/utils/request` 导出的 `request` 实例，接口函数统一在 `api/` 目录定义
- 入参、出参必须声明明确类型，禁止组件内直接写 axios 调用
- `/api` 前缀由 request 实例的 baseURL 统一携带，`api/` 目录函数内路径不再重复 `/api`（如 `get('/v1/users')`），避免出现 `/api/api`
- **判断请求成败看 `ApiResult.code`，不看 HTTP 状态码**：业务异常（`code ≥ 1000`）返回的是 **HTTP 200**（见 6.4），只看状态码会把业务失败当成功
- **token 无感刷新**：响应拦截器捕获 401 → 调 refresh 接口换新（refresh token 走 Cookie，请求 `withCredentials`）→ 重放原请求；refresh 失效才跳转登录页。以下几点缺一个都会出故障：
  - **并发刷新必须去重**：把在途的刷新 Promise 存下来，其余 401 复用它排队等待。**这不是性能优化**——refresh token 是**一次性轮换**的，并发刷新会让后发的那个拿着已作废的令牌去请求，服务端判定「凭证复用」，**注销该用户全部会话，用户被直接踢下线**
  - 请求上打 `_retry` 标记，**每条请求只重放一次**；不加会「401 → 刷新 → 仍 401 → 再刷新」无限循环
  - 刷新请求必须用**不带拦截器的裸 axios 实例**发送；复用主实例会让刷新失败的 401 再次进入同一个拦截器，形成递归
  - 刷新成功后重放必须走**带请求拦截器的封装实例**（不能直接用 axios），否则读不到刚更新的访问令牌，重放会立刻再 401
  - ⚠️ 因为业务异常按 6.4 返回 **HTTP 200**，刷新接口失败（`code 1203/1204`）**不会**进入 axios 的错误分支——刷新函数里**必须自己判 `response.data.code`**，这是唯一的防线

### 3.5 路由与权限

- 公开页面在路由 meta 标记 `public: true`，其余默认需要登录
- 页面权限：路由 meta `permission: '模块:操作'`，全局守卫自动校验
- 按钮级权限：`v-auth="'权限码'"` 指令，无权限自动移除元素
  - ⚠️ `v-auth` 靠**替换 DOM 节点**实现，只适用于渲染一次就不再变动的按钮。TanStack Table 的行内按钮由表格在每次翻页/排序时重建，被指令换掉的节点仍被 Vue 引用，下一次 patch 会抛 `NotFoundError` 直接白屏。**动态渲染的按钮改用条件渲染**（`hasPermission()` + `v-if`）
- **鉴权只认后端**：前端隐藏按钮只是体验优化，绕过前端直接调接口必须同样被拦下（权限判定在服务端逐次执行，不依赖前端是否渲染过按钮）
- 权限码命名：`模块名:操作名`（如 `user:view`、`user:add`）
- **数据权限（行级）**：后端返回用户 DataScope（All=全部 / Dept=本部门 / DeptAndChild=本部门及下级 / Self=仅本人），查询过滤在 Service 层统一拼接，禁止前端控制数据范围

### 3.6 TypeScript 与样式

- 所有变量、函数参数、返回值显式声明类型，禁止隐式 `any`
- 对象结构用 `interface`，联合类型、工具类型用 `type`
- 业务实体类型在 `types/` 目录，字段名、类型与后端 DTO 完全一致
- 样式优先 Tailwind 原子类；主题色、间距、圆角、阴影统一用 CSS `@theme` 配置（Tailwind 4 写法），不硬编码色值

### 3.7 前端错误边界

- 全局兜底：`app.config.errorHandler` 捕获未处理异常，记录并上报，展示友好错误页而非白屏
- 路由级：路由组件加载/渲染失败时降级展示（重试按钮 + 返回上一页），禁止整页白屏
- 组件级：复用组件用 `onErrorCaptured` 捕获子组件异常并降级渲染，避免局部错误拖垮整页

---

## 四、后端工程规范

### 4.1 分层架构

```
YourProject.Api/               # 接口层：Controllers / Filters / Program.cs
YourProject.Application/       # 业务层：Services / Dtos / Interfaces
YourProject.Infrastructure/    # 基础设施层：Data(AppDbContext) / Repositories / Migrations / Cache
YourProject.Domain/            # 领域层：Entities / Enums / Exceptions
```

**分层原则**：上层依赖下层，依赖注入管理生命周期；禁止跨层调用，禁止控制器直接操作 DbContext。接口（抽象）定义在 Application 层、实现在 Infrastructure 层（依赖倒置），项目引用方向为 `Api → Application`、`Infrastructure → Application`、`Infrastructure → Domain`。

### 4.2 统一响应与异常

```csharp
public class ApiResult<T>
{
    public int Code { get; set; }     // 0=成功，其他=失败
    public string Message { get; set; }
    public T Data { get; set; }
}
```

- 控制器直接返回业务对象，全局 `ResultFilter` 自动包装；禁止手动构造 ApiResult
- 业务校验失败抛 `BusinessException`（含错误码），全局 `ExceptionFilter` 统一捕获转换
- 系统异常对外仅返回"系统内部错误"，完整堆栈记入日志
- 禁止在控制器中 try-catch 后手动返回错误结果

### 4.3 认证与授权

- JWT 双 token 机制：access token 2 小时（**仅存内存**，不落 localStorage / sessionStorage，见 3.3）+ refresh token 7 天（httpOnly Cookie，见 3.3）
- refresh token 一次性使用（轮换），旧 token 刷新后立即失效，防止重放
- 密码使用 BCrypt 哈希存储，禁止明文存储和传输
- Token Claims 只包含：用户ID、角色、DataScope（**权限标识不入 Claims**，防 JWT 膨胀——权限多的用户每个请求都携带大 token）
- 权限标识：Redis 缓存 + 变更主动失效（权限变更即时生效，无需等 token 过期）；关键权限变更应主动踢线
- 登录失败连续 5 次锁定账户 15 分钟（Redis 计数）

### 4.4 EF Core 数据访问

- 只读查询必须 `AsNoTracking()`
- 列表查询使用 `Select()` 投影返回 DTO，禁止直接返回 Entity
- 关联数据用 `Include()` 显式加载，**禁止懒加载**（防 N+1）
- 字符串字段必须显式 `HasMaxLength()`，禁止默认 longtext
- 高频筛选、排序字段必须配置索引（`OnModelCreating` 中定义）
- 分页查询统一封装，返回 `PageResult<T>`（结构见 6.3）
- 事务在业务层控制，禁止在控制器中使用事务
- **唯一性校验必须 `IgnoreQueryFilters()`**：唯一索引建在**物理列**上，逻辑删除并不会释放唯一性。不加这句，被软删除的记录在业务层"查不到"，校验放行 → 数据库报重复键 → 用户看到莫名其妙的 500。**改唯一索引前先想清楚"删掉的数据还算不算占用"**，两条路选一条并写进注释：①唯一索引带上 `IsDeleted`（部分索引，MySQL 不支持；改用 `IsDeleted` 参与的组合唯一列）②校验时 `IgnoreQueryFilters()`
- **禁止无脑 `Update(entity)`**：它把实体的**全部字段**标记为已修改，审计日志会退化成"所有字段都变了"，也掩盖了并发修改。先按 Id 查出实体、逐字段赋值、只调用 `SaveChangesAsync()`
- ⚠️ **`ExecuteUpdateAsync` / `ExecuteDeleteAsync` 绕过变更跟踪与拦截器**：不走 `SaveChanges` 的批量操作，审计拦截器**不会被触发**。用于批量改状态时必须**显式补写审计日志**；`SaveChanges` 之外的一切写库路径都要过一遍这个检查
- ⚠️ **重试判定不得逐写入点手写 `catch`，必须在事务边界统一分类**：一次性业务事务往往有多个写路径（各仓储的 `SaveChangesAsync`、`ExecuteUpdateAsync`、审计服务……），逐个方法加 `catch` 必然漏——**每新增一个写路径就会静默失效一次**。本仓库实测：同一事务的 8 个写路径中只有 3 个做了瞬时错误分类，落在中奖记录 INSERT（第 6 步）的死锁直接冒泡成 500，**整事务重试链完全没被触发**（失败发生在已改写 4 行之后；且 `ExecuteUpdateAsync` 是一条不经过 `SaveChanges` 的独立通道，异常形态与拦截点都不同，天然更容易被漏掉）。正确做法是**在事务边界统一捕获、统一分类、统一决定是否重试**（事务管理器 / 拦截器 / 中间件），并把「本项目事务内的全部写路径」列为审查清单项
- ⚠️ **超时类配置互相约束，不能各配各的**：客户端命令超时（MySqlConnector `CommandTimeout` 默认 **30s**）必须 **≥** 服务端同类超时（MySQL `innodb_lock_wait_timeout` 默认 **50s**）。配反了会让「专门为服务端超时写的分支」**永不可达**——本仓库实测：`30 < 50` 使锁等待先被客户端中断，而命令超时抛出的异常号既非 1205 也非 1213，`IsTransient` 判 false，**恰好是它被写出来要处理的那个场景**。推论：任何「为某种超时 / 异常写的分支」都必须**验证真的可达**（构造一次真实触发），否则它就是死代码；补 `catch` 不能替代把根因配置配上对
- 判错必须走 `InnerException` 链：EF Core 会把 provider 异常包在 `DbUpdateException` 内层，只看最外层会漏判（本仓库 `MySqlErrors.FindMySqlException` 即按此实现，可作正例参照）

### 4.5 控制器与依赖注入

- 路由：`[Route("api/v{version:apiVersion}/[controller]")]` + `AddApiVersioning()`（控制器标注 `[ApiVersion("1.0")]`，URL 形如 `/api/v1/users`），并启用 `AddRouting(o => o.LowercaseUrls = true)` 统一小写 URL
- RESTful 风格：GET 查询 / POST 新增 / PUT 修改 / DELETE 删除
- 入参使用 DTO，禁止直接使用 Entity 作为接口参数
- 分页参数继承 `PageQuery` 基类（PageIndex、PageSize、SortField、SortOrder）
- 异步方法后缀 `Async`
- 构造函数注入，禁止手动 new 服务；业务服务/仓储注册为 Scoped，工具类/全局配置注册为 Singleton；禁止用静态类存储状态

---

## 五、数据库规范

### 5.1 基础

- 字符集 `utf8mb4`，排序规则 `utf8mb4_0900_ai_ci`（MySQL 8.x 官方默认，Unicode 9.0 排序更快更准），存储引擎 InnoDB
- 表名与实体一致，帕斯卡命名（`User`、`Role`）

### 5.2 表与字段

- 所有业务表必填：`Id`（自增主键）、`CreateTime`、`UpdateTime`（datetime）；普通业务表用 int，审计日志等高速增长表用 **bigint**（int 上限约 21 亿）
- 重要业务表包含 `IsDeleted`（bool，默认 false）实现逻辑删除，**禁止物理删除**
- 「重要业务表」指用户、角色、权限、订单、审批等核心实体；日志表（审计日志除外，见第十章）、会话表、临时表等允许物理删除
- 字符串按业务指定长度（姓名 50、邮箱 100、备注 500）
- 枚举字段用 int 存储，禁止字符串
- 禁止使用存储过程、触发器

### 5.3 索引

- WHERE、排序、关联字段必须建索引；联合索引遵循最左前缀
- 大字符串字段不建普通索引，必要时用前缀索引
- 单表索引一般 ≤5 个；大数据量表按查询频率评估，不设死上限
- ⚠️ **唯一索引与逻辑删除会打架**：唯一索引建在物理列上，逻辑删除**不释放唯一性**——用户名 `alice` 被软删除后，再建一个 `alice` 会直接撞数据库唯一键。设计时就选好：①唯一索引改成 `(UserName, IsDeleted)` 这类组合列并约定删除时的占位值（`IsDeleted` 是 bool 时简单加进去不够用，通常配套一个 `DeletedAt` 时间戳列）②接受"删掉的用户名不再可用"。**无论选哪条，查询侧的唯一性校验都要 `IgnoreQueryFilters()`**（见 4.4），否则"校验说没冲突、数据库说冲突"

### 5.4 迁移

- 所有表结构变更走 EF Core Migrations，迁移文件随代码提交
- 生产环境用 `dotnet ef migrations script` 生成 SQL（跨平台，CI/CD 容器可用），DBA 审核后执行
- **已提交的迁移文件禁止修改**，结构变更必须新增迁移
- 部署时迁移执行先于应用启动（见第十二章）

---

## 六、前后端接口约定

### 6.1 版本控制

- 接口 URL 统一带版本号：`/api/v1/[controller]`（Asp.Versioning 参数化路由，见 4.5；默认版本 v1）
- 破坏性变更必须升 `v2`（新控制器或同控制器 `[ApiVersion("2.0")]` 分方法映射），旧版本保留至少一个大版本周期并标注废弃

### 6.2 通用分页请求

| 参数 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| pageIndex | int | 是 | 页码，从 1 开始 |
| pageSize | int | 是 | 每页条数，上限 100（超出按 100 处理） |
| sortField | string | 否 | 排序字段名 |
| sortOrder | string | 否 | `asc` / `desc` |

> ⚠️ **前后端页码基准不同**：TanStack Table 的 `pageIndex` **从 0 开始**，本接口的 `pageIndex` **从 1 开始**。转换必须**收敛在一处**（composable 或 api 层，通常是"请求时 `+1`、回填时 `-1`"），**禁止在组件里零散地 ±1**——否则必然出现"第 2 页显示的是第 1 页数据"这类只在特定页才暴露的错位。测试用例要显式覆盖 `pageIndex` 0 与 1 的边界。

### 6.3 通用分页响应与统一响应结构

```ts
interface PageResult<T> {
  items: T[]
  totalCount: number
  pageIndex: number
  pageSize: number
}

interface ApiResult<T> {
  code: number     // 0=成功
  message: string
  data: T
}
```

### 6.4 错误码

| 错误码 | 含义 | 前端处理 |
| --- | --- | --- |
| 0 | 成功 | 正常处理数据 |
| 400 | 参数绑定/框架级校验失败（模型绑定、DataAnnotations） | 展示错误信息 |
| 401 | 未登录 / Token 失效 | 走无感刷新，失败则跳转登录 |
| 403 | 无权限 | 提示无权限 |
| 409 | 幂等冲突（重复提交） | 提示"请勿重复提交" |
| 500 | 系统内部错误 | 通用错误提示 |
| ≥1000 | 业务异常（含业务层校验失败，如 1002） | 展示后端具体错误信息 |

> **说明**：表中数值为响应体 `ApiResult.code` 的取值。HTTP 状态码与 0–500 段保持一致（401 未登录、403 无权限、409 冲突等）；业务异常（≥1000）HTTP 返回 200，前端依据 `code` 区分处理。
>
> **业务异常码分配**：`≥1000` 的错误码按模块分段登记在 [error-codes.md](error-codes.md)，新增业务错误码必须先查表取号并登记，禁止随意编造、禁止冲突复用。

### 6.5 其他约定

- 认证：`Authorization: Bearer {token}`
- 时间：ISO 8601 字符串
- 提交：`application/json`
- 序列化：JSON 统一 camelCase（ASP.NET Core 默认行为，前端类型定义与此对齐）
- 幂等：POST 提交类接口携带 `Idempotency-Key` 请求头（UUID），后端 Redis 去重 + 数据库唯一索引兜底。实现细则：
  - 前端生成时机：表单初始化时生成 UUID 随首次提交发送，重试沿用同一 key，新表单重新生成
  - 后端去重：Redis `SET NX` 写入 `{项目}:idempotency:{模块}:{key}`（与第九章缓存 key 命名对齐；同时缓存首次响应），TTL 24 小时（按业务可调）；已存在时对比请求体：一致 → 重放首次响应（覆盖网络超时后重试且首次已成功的场景），不一致 → 返回 409
  - 并发兜底：Redis 不可用或 SET NX 失败时由数据库唯一索引兜底，唯一约束冲突同样返回 409

### 6.6 契约表达与可测性

契约是下游（研发、用例设计、测试执行、审查）的唯一依据，**它的表达方式直接决定下游能不能对齐**。以下五类是本项目实测的契约空白，它们全部在下游以「观察项」形式反复出现，且每次都要重新论证「是否算缺陷」：

1. **能用键值表表达的就不要用散文**：同一配置事实若一处写成可机检的键值表、另一处写成散文描述，二者必然分叉。实测：同一配置一处散文写「**只含** `Enabled = false`」，用例写「**不含**该节」，风险表又写「不含该节」——三处口径互斥，而实现是**该节存在、仅取值不同**。规则：**同一事实单点定义，其余处引用 ID**；描述配置一律用键值表
2. **用例只断言取值，禁止断言「某节是否存在」这类文本形态**：文本形态随实现与序列化设置漂移，且不表达业务语义。要求「不含某节」这类断言，等价于把序列化细节写进契约
3. **契约中的每个分支都必须可构造**：若某分支在当前接口形态下**无法被用例构造**（实测：抽奖请求体恒为 `{}`，使「同键不同请求体」分支不可达），须在架构阶段显式裁决「删分支 / 补构造手段 / 明确不作要求」，不得留给测试去"判空白"
4. **通用错误响应体形态一次写死**：401 / 403 / 429 等由框架直接产出的响应，其响应体是否包装为 `ApiResult`、字段如何取值，必须在接口约定中一次规定。实测 401 响应体为 `Content-Length: 0`（未经包装），与业务接口体形态不一致而契约未规定 → 只能判「契约空白」
5. **架构阶段必须显式回答的五个问题**（不回答就会在下游反复付代价，且每次都要重新论证）：
   - 通用错误响应体形态（见上条）
   - 并发闸门 / 一次性闸门的作用域：每实例、每页面还是每会话（实测「两页面实例各刷新一次」被判定为"每实例"语义，属解释而非契约）
   - 时间可控性：时钟能否注入——不能注入则 `23:59:59` / `00:00:00` 这类**逐秒边界用例无法执行**，只能用等价夹具替代
   - 降级与回退语义：依赖不可用（Redis 挂掉）、库存耗尽、候选集为空时分别如何表现
   - 可观测性落地：日志是否**落盘**、能否离线检索（实测 Serilog 仅 `WriteTo: Console`，离线无法检索，用例只能绕道三面交叉验证）

---

## 七、测试规范

### 7.1 后端

- 框架：xUnit + FluentAssertions + NSubstitute
- 单元测试：Service 层覆盖核心业务分支，**行覆盖率 ≥ 60%**（Coverlet 统计，CI 强制卡点）
- 集成测试（两种方案按项目条件选，覆盖 Repository 与关键 API 链路）：
  - **方案一 Testcontainers**：起真实 MySQL 容器，与开发环境完全隔离（**需本地 Docker 运行**；环境缺失时反馈并等待处理，禁止跳过集成测试交差）
  - **方案二 `WebApplicationFactory<Program>`**：不起独立容器，直连开发态容器，靠**独立库名 + 独立 Redis db** 隔离。需在 Api 项目末尾加 `public partial class Program { }` 开放入口点（顶级语句生成的 `Program` 默认 internal，测试程序集看不见）
  - ⚠️ **两种方案的配置覆盖都必须走环境变量**（`ConnectionStrings__Default` / `Redis__Configuration`），**禁止用 `ConfigureAppConfiguration`**：应用读取连接串发生在 `builder.Build()` **之前**，而该覆盖要等 `Build()` 才合并——覆盖会**静默失效**，测试实际连的是**开发库**，夹具一句 `EnsureDeleted()` 就把开发库整个删掉重建。对照：`ConfigureServices` 有效，因为服务是惰性解析的
  - ⚠️ **测试隔离必须被验证**：跑完要确认测试库真的存在且被写过（例如核对库里出现了 `xxx_test`），**不能只看测试变绿**——全绿也可能跑在错误的库上
- 测试命名：`方法名_场景_期望结果`（如 `CreateUserAsync_EmailDuplicated_ThrowsBusinessException`）
- 测试代码与生产代码同 MR 提交，**新增/修改业务逻辑必须附带测试**

### 7.2 前端

- 框架：Vitest + @vue/test-utils（`environment: 'jsdom'`，基建已具备，**不是待办事项**）
- `stores/`、`utils/`、`composables/` 必须有单测
- **组件渲染测试（模板层）必须有**：放置于被测组件同级的 `__tests__/<组件名>.spec.ts`。以下改动**不得**以「无组件测试基建」为由豁免：
  - 模板层的**条件渲染分支**（`v-if` / 三元 / 兜底文案），典型如「值为 `null` 时不得渲染为 `0`」这类占位规则
  - 模板层直接消费的**冻结文案与兜底值**（实测出现过「结果名称为空」的模板层缺陷）
  - 由 props 驱动的展示格式化（单位、时间格式、空态）
  > ⚠️ 为什么必须：本仓库实测存在**两个缺口叠加 = 一整层无人验证** —— 模板层缺陷全部推给 E2E，而 E2E 又因环境锁跨多轮未复跑。每个缺口单独看都"有理由"，叠起来就是「没有任何一层在验证模板」。
- E2E：Playwright 覆盖登录、核心 CRUD 流程

### 7.3 红线

- **CI 中任一测试失败禁止合并**
- 禁止为了凑覆盖率写无断言测试
- 禁止 skip 测试合入 main（临时 skip 必须带 TODO 与 issue 号）

### 7.4 编写与执行职责

| 环节 | 责任方 | 方式 |
| --- | --- | --- |
| 测试编写 | AI 编码助手 | 随业务代码同 MR 生成（见「十五、AI 编码协作核心准则」第 7 条） |
| 测试执行 | 全自动 | 本地：`dotnet test` / `vitest`；CI：MR 触发自动执行 + 覆盖率卡点 |
| 测试评审 | 人工 | MR 评审时检查测试有效性（分支覆盖、断言质量） |
| 验收测试 | 人工 | 上线前 UAT，自动化不替代 |

- AI 生成测试时必须保证本地可运行通过后再提交
- 人工不做重复的手动回归，回归一律交给自动化测试

### 7.5 断言辨别力（测试有效性）

**一条断言只有在「已知坏」的样本上会红，才算得上断言。** 无法区分「修复前」与「修复后」的断言会稳定产出假绿——这是本项目实测中复现最多、代价最高的一类问题。以下为硬性要求：

1. **只允许加强，不允许放宽**：修改既有断言时必须逐条给出「改前 / 改后 / 理由」，方向只能是收窄口径或补上缺失判据。**禁止为求全绿而调整断言**——全绿应当是修复的结果，不是断言的结果。确有需要放宽时，必须显式上报并说明影响面，不得静默改
2. **新增或修改断言必须给出辨别力依据**：写明「它在什么输入 / 状态下会失败」。实测反例：
   - `toContain('加载失败')`：修复前文案「加载失败」、修复后「**奖池**加载失败」**都含该子串**，无法区分修复（应收紧为 `toContain('奖池加载失败')`）
   - 只断言 `http !== 200`：「正确的业务终态」与「未处理的 500」都满足，判据没有指向性（应显式断言 `http === 200 && code === 1001`，并另加耗时区间断言佐证重试确实发生过）
   - 只断言响应体、不断言 HTTP 状态码：本项目实测缺陷正是「业务码正确、HTTP 码错」。**前端只读 `response.data.code` 会系统性掩盖此类偏差**，而非浏览器客户端按 HTTP 码判定会把被拒的重复提交当成功——两处都要断言
   - 断言关系而非硬编码值：守护性断言应钉「常量（预算）+ 关系（口径）+ 数量（唯一性）」，并声明它防什么、不防什么（如「重试次数 = 尝试次数 − 1」随实测值自适应，而不是写死 `1`）
3. **禁止把当前行为固化成期望**：断言必须来自**契约**，不得来自**现有实现的输出**。实测出现过用例的文档注释把缺陷行为写成 `HTTP 200 + 业务码（D-12）`，于是修复前集成套件**全绿**——**缺陷不是没测，是测试把它固化了**。审查环节须专项扫描「是否有断言在守护错误期望」，做法：检索同类断言的全部分布（本项目做法为「扫描 `tests/integration/**` 全部 `HttpStatusCode.OK` 断言并逐一裁决是否落在契约要求 200 的路径上」）后逐条判定
4. **字段名与选择器必须逐字来自契约与 DOM 结构**：跨接口借用字段名（契约是 `totalCount`、断言却读 `data.total`）、选择器作用域写错（锚点挂在表体却去查表头文案）会产出**永远不可能通过**的断言；其镜像形态是**永远通过**的断言。两者都无法靠正常运行暴露，只能在编写时对照契约逐字核对
5. **不可达分支是契约缺陷信号**：契约中的分支若在实现下**不可构造**（如请求体恒为 `{}`，则「同键异体」分支不可达），必须上报并显式登记，**不得静默跳过或删除用例了事**

---

## 八、安全规范

### 8.1 认证与会话

- BCrypt 哈希存储密码；登录失败锁定（见 4.3）
- 密码规则：长度 ≥ 8，含大小写字母 + 数字；首次登录强制改密（可选开启）

### 8.2 传输与注入

- EF Core 参数化查询，禁止拼接 SQL 字符串
- 前端输出自动转义（XSS），后端 FluentValidation 校验输入；**禁止 `v-html` 渲染服务端/用户输入**，富文本必须先过白名单净化库（如 DOMPurify）

### 8.3 CORS 与安全响应头

- CORS 白名单显式配置，**禁止 `AllowAnyOrigin`**
- 统一添加安全响应头：`X-Content-Type-Options: nosniff`、`X-Frame-Options: DENY`、Content-Security-Policy

### 8.4 敏感信息

- 手机号、邮箱、身份证按权限脱敏：**后端脱敏后返回**（无权限直接返回 `138****8000`，本人/授权角色返回明文），前端只负责展示——前端持明文做掩码防不住抓包与调试工具
- 日志禁止输出密码、Token、密钥
- 敏感配置：开发环境 `dotnet user-secrets`，生产环境环境变量注入，**禁止入库/入仓**

### 8.5 文件上传

- 类型白名单（按业务配置，默认 jpg/png/pdf/xlsx）+ 大小限制（默认 10MB）
- 存储文件名随机化（UUID），路径不可猜测；禁止保留原始文件名直接落盘
- 文件头魔数校验：前端预检只是体验优化，**服务端必须复核**（读文件头字节比对类型白名单），不信任仅扩展名

### 8.6 限流与防滥用

- 登录、验证码、查询导出接口配置限流：优先 .NET 内置 RateLimiter 中间件（`System.Threading.RateLimiting`）；分布式限流用 Redis + 自定义分区器
- **反代后必须还原真实客户端 IP**，否则按 IP 分区会退化成"全局共享一个配额"——一个人狂点，所有用户一起被挡在门外：
  - `UseForwardedHeaders` 必须排在 `UseRateLimiter` **之前**，限流读的就是它还原出来的 `RemoteIpAddress`；顺序反了完全不起作用
  - 清空可信代理必须用 `KnownNetworks.Clear()` / `KnownProxies.Clear()`。写成集合初始化器 `KnownNetworks = { }` 的含义是**"不添加任何元素"，不是清空**——默认的可信代理（回环地址）会原样留着，反代容器不被信任，`X-Forwarded-For` 被整个忽略。**代码看着写对了、编译也通过，实际一点作用都没有**
  - 清空的前提是「应用不暴露到宿主机、只能从内部网络到达」；一旦暴露到宿主机，必须改为只信任固定代理网段，否则任何人都能伪造 `X-Forwarded-For` 绕过限流

### 8.7 安全配置的加载与失效（fail-fast）

- **同一安全敏感配置只允许一个取值来源**：签名密钥、连接串、加密密钥等，禁止在多个消费点各自兜底。实测后果：同一「签名密钥为空」场景存在三种处理——校验侧回退为 32 个 `'0'`、签发侧**无回退**（空串直接进 `SymmetricSecurityKey`）、幂等指纹侧回退为 32 个 `'0'`。于是**校验侧与签发侧的口径永不可能互相匹配**，即便签发成功也会全部验签失败
- **必须在启动期 fail-fast**：关键安全配置缺失时应拒绝启动（配置校验 + `ValidateOnStart()`），而不是"能启动、照常监听端口、只在真正用到时才抛异常"。实测后果：应用照常启动，任何**签发令牌**的接口返回 500，**且失败时账号行与审计日志行已入库、Redis 刷新键已写入**——客户端看到 500，数据却已不可收回
- **「先写库、后签发」的顺序必须保证原子性或前置校验**：否则「一半成功、一半失败」的半成品状态会被持久化
- **配置缺失的表象是「接口 500 + 日志 0 字节」**：日志 sink 本身也来自配置文件，配置没加载就没有日志——排查时不要因为"没有日志"就断定进程没起来，也不要因此断定"没有异常发生"（详见 `docs/role-protocol.md` 的实例启动前置条件）

---

## 九、缓存规范

- 分布式缓存：Redis（StackExchange.Redis）；进程内缓存：`IMemoryCache`
- 模式：cache-aside —— 读：先查缓存，未命中再查数据库并回填缓存；写：先更新数据库，再失效缓存
- Key 命名：`{项目}:{模块}:{实体}:{id}`，如 `admin:user:info:1001`
- 字典、组织架构、权限等低频变更数据：本地缓存 TTL 10 分钟 + 更新时主动失效；**多实例部署时改用 Redis**（或本地缓存 + Redis Pub/Sub 广播失效）——IMemoryCache 是进程内的，主动失效只清当前节点，其余节点旧数据最长残留 10 分钟
- 热点查询：Redis，TTL 按业务定（默认 5 分钟）
- **禁止缓存敏感信息明文**；缓存必须设置过期时间，禁止永久 key

---

## 十、审计日志

- 关键操作必须留痕：登录/登出、权限变更、角色分配、数据删除、审批动作、导出
- `AuditLog` 表字段：操作人ID、操作时间、IP、UserAgent、模块、操作类型、目标对象、变更前 JSON、变更后 JSON
- 变更前后 JSON 中密码哈希、手机号、邮箱、身份证等敏感字段必须脱敏或排除，禁止原样落审计表（与 8.4 一致）
- 实现：EF Core `SaveChangesInterceptor` 自动审计实体变更 + 显式 `IAuditService` 记录业务动作
- 审计日志只增不改（禁止 Update/Delete），保留策略按合规要求（默认 ≥ 1 年）
- 审计日志写入失败不阻断主流程，但必须告警

---

## 十一、Git 协作流程

### 11.1 分支模型

- `main`：受保护分支，禁止直接 push、禁止 force push
- 功能分支从 `main` 拉出，命名：`feature/xxx`、`fix/xxx`、`docs/xxx`、`refactor/xxx`、`hotfix/xxx`
- 合并必须走 MR/PR，至少 1 人 approve + CI 全绿

### 11.2 Commit 规范

Conventional Commits：`feat|fix|docs|style|refactor|test|chore(scope): 描述`

示例：`feat(user): 新增用户导出功能`、`fix(auth): 修复 token 刷新并发丢失`

### 11.3 MR 自查清单

- [ ] 遵循本规范命名与分层
- [ ] 复用已有封装，无重复造轮子
- [ ] 新增/修改逻辑已附带测试
- [ ] 类型完整，无 `any`
- [ ] 无敏感信息硬编码
- [ ] 数据库变更已生成迁移文件
- [ ] 文档/接口约定已同步更新

---

## 十二、CI/CD 与部署

### 12.1 流水线（每次 MR 自动触发）

```
lint → build → test（单测+集成）→ 覆盖率卡点 → E2E（Playwright）→ OpenAPI 契约校验 → 镜像构建（只构建不推送，验证 Dockerfile 可构建）
```

- 镜像仓库：Harbor / 阿里云 ACR
- 镜像 tag：`{语义化版本}-{git短sha}`，禁止覆盖已推送的 tag
- 镜像推送时机：MR 阶段只构建不推送；合入 main 后构建并推送正式 tag（避免 MR 临时镜像堆积）
- E2E：`npx playwright test`（`tests/e2e/**`）。CI 中起前端构建产物 + 后端容器（docker compose 冒烟环境）跑核心路径冒烟；全量 E2E 放夜间 / 发版前。按变更路径触发（无 e2e 用例变更时跳过，省 CI 时长）
- OpenAPI 契约校验：后端用框架内置 Microsoft.AspNetCore.OpenApi 生成 `openapi.json`，与仓库基线 `docs/openapi.baseline.json` diff，漂移即失败；接口变更必须同步更新基线（升 v2 时旧版本基线保留，与 6.1 版本控制呼应）

### 12.2 环境与发布

| 环境 | 触发方式 | 说明 |
| --- | --- | --- |
| dev | 合入 main 自动部署 | 每日验证 |
| staging | 手动审批 | 预发验证，数据结构同生产 |
| prod | 手动审批 | 保留最近 3 个版本，支持一键回滚 |

- 部署顺序：**先执行数据库迁移脚本，再滚动更新应用**；迁移必须向后兼容（先扩后收：先加列/加表，删除列/改名放后续版本），滚动更新期间新旧版本共存
- 生产发布后 15 分钟内观察核心指标（错误率、P95 耗时），异常立即回滚；错误率按 `ApiResult.code ≠ 0` 统计——业务异常 HTTP 返回 200，只看 5xx 会漏报

### 12.3 Docker 构建

- 前端：多阶段构建（node:24-alpine 构建 → **`nginx:alpine-slim`** 托管），最终镜像 ≤ 50MB，Nginx 同时处理静态资源与 `/api` 反代
  - **必须用 `-slim` 变体**：实测 `nginx:alpine` **103MB**、`nginx:alpine-slim` **30MB**。用 `alpine` 永远达不到 50MB 预算——多出来的是 geoip / xslt / perl / image-filter 等静态托管与反代一个都用不上的可选模块
  - 体积以 `docker image ls`（**解压后**）为准：Docker Hub 页面标的是**压缩后**体积，两者能差一倍以上，做预算别抄页面上的数字
  - **SPA 托管的三个必备配置**（缺一条都出事）：
    - `try_files $uri $uri/ /index.html;`：前端路由是**客户端路由**，直接访问或刷新 `/system/users` 时服务端并没有这个文件，不退回到 `index.html` 就是 **404**
    - `index.html` 必须 `Cache-Control: no-store`：它是唯一**不带内容哈希**的文件，被缓存住就会出现"发版后用户看到的还是旧版"——而旧的 `index.html` 会去引用已经被删掉的旧哈希资源，表现为**白屏**（这是发版后白屏最常见的原因）
    - 带哈希的静态资源（`/assets/*`）走 `Cache-Control: public, max-age=31536000, immutable`：文件名里带着内容指纹，内容一变文件名就变，可以放心长缓存
- 后端：多阶段构建（sdk 构建 → aspnet 镜像运行，如 `mcr.microsoft.com/dotnet/aspnet:10.0`），容器内端口 8080（.NET 8+ 官方镜像默认 `ASPNETCORE_HTTP_PORTS=8080`，可按需覆盖）
  - 运行阶段用镜像自带的非 root 用户（.NET 8+ 为 `USER app`），容器被攻破时不会直接拿到 root
  - ⚠️ **运行镜像里既没有 curl 也没有 wget**，健康检查用镜像自带的 `bash` 对 `/health` 发 `/dev/tcp` 请求；**不要为了装 curl 引入 `apt-get`**——那会让镜像构建依赖 apt 源的可达性（国内网络下经常 502），把构建卡在一个与代码毫无关系的地方
- 构建顺序：**先拷依赖清单（`.csproj` / `package.json` + lockfile）单独还原，再拷源码**。Docker 按层缓存，依赖没变时改业务代码可命中缓存；顺序写反了，改一行代码就要重下全部依赖
- 前端安装依赖用 `npm ci` 而非 `npm install`（严格按 lockfile，lockfile 与 package.json 不一致时直接失败）
- ⚠️ **换行符必须锁 LF**：仓库加 `.gitattributes` 声明 `* text=auto eol=lf`（至少 `*.sh text eol=lf`）。Windows 上检出成 CRLF 的 shell 脚本、entrypoint、nginx 配置进 Linux 容器后，会报 `\r: command not found` 或配置解析失败——错误信息完全指不到"换行符"上，是典型的排查黑洞
- 镜像构建**不跑测试**：用 `.dockerignore` 排除 `tests/`、`node_modules/`、`bin/`、`obj/`。测试是 CI 的职责，让镜像构建跑测试只会把"构建问题"和"代码问题"混在一起
- **禁止镜像内硬编码连接串、密钥**，统一环境变量注入

### 12.4 生产编排

- docker-compose 编排前端、后端、MySQL、Redis
- MySQL 数据、Redis 数据挂载宿主机卷（Linux 容器下**用命名卷，禁止 bind mount**：Windows 上 bind mount 走 9p 文件系统，会导致 InnoDB 权限失败、性能极差甚至崩溃）
- 仅前端 Nginx 暴露 80/443；后端、数据库、Redis 仅内部网络通信
- 服务间用**服务名**互访（`mysql:3306`、`redis:6379`、`http://api:8080`），这是 Docker 内建 DNS 的能力
- **启动顺序用 `depends_on` + `condition` 表达**，不要靠"多试几次"：
  - 数据库用 `service_healthy`（健康检查要发**真实查询**而非 `mysqladmin ping`——ping 在认证失败时也可能返回成功）
  - 迁移用一次性服务 + `condition: service_completed_successfully`，并设 `restart: "no"`（一次性任务失败就该停下来让人看见，否则会陷入「失败 → 重启 → 再失败」的无限循环刷爆日志）
  - 应用用 `service_healthy`，避免"表还没建好就起来查询"的随机失败
- ⚠️ **一次性数据库初始化不要放 `/docker-entrypoint-initdb.d`**：它**只在数据卷为空时执行一次**，之后改配置永远不会重跑——会变成"明明加了配置却还是 Access denied"的谜题。改用一次性服务或幂等脚本
- ⚠️ compose **只转发它明确列出的环境变量**：光写进 `.env` 而没在 `environment:` 里列出的变量，容器里根本看不到。`$$` 才是容器内的字面 `$`（`$` 会被 compose 提前吃掉），健康检查里传 shell 变量时必须用 `$$`

---

## 十三、代码风格与命名

### 13.1 前端（TS / Vue）

| 类型 | 规范 | 示例 |
| --- | --- | --- |
| 组件文件/组件名 | 帕斯卡 | `UserManage.vue` |
| 变量 / 函数 | 小驼峰 | `userList`、`fetchData()` |
| 接口 / 类型 | 帕斯卡 | `interface UserDto` |
| 常量 | 全大写下划线 | `DEFAULT_PAGE_SIZE = 10` |

### 13.2 后端（C#）

| 类型 | 规范 | 示例 |
| --- | --- | --- |
| 类 / 接口 | 帕斯卡，接口前缀 I | `UserService`、`IUserRepository` |
| 方法 / 属性 | 帕斯卡，异步后缀 Async | `CreateUserAsync()` |
| 参数 / 局部变量 | 小驼峰 | `userId` |

### 13.3 通用

- 命名语义化，禁止拼音、无意义缩写
- 注释与文档统一使用中文（标识符用英文，见 13.1 / 13.2）
- 关键业务逻辑必须注释；公共方法必须有文档注释（`<summary>`）
- 函数单一职责，超过 50 行拆分
- 缩进：前端 2 空格，后端 4 空格

### 13.4 文档与引用

- **引用一律用稳定 ID，禁止用行号**：行号会随任何一次编辑漂移。实测：代码注释以 `30-architecture.md:544` 为锚点，架构升版后该锚点变为 `:567`，**全部引用静默失效且无人会回头核对**。可用的稳定标识：`FR-xx` / `AC-xx` / `MOD-xx` / `API-xx` / `D-xx` / `RSK-xx` / `CHG-xx` / `TC-xx` / `BUG-xx` / `REV-xx` / `OBS-xx`（ID 体系见 `docs/artifacts.md`）
- **规范文档只写「应然」，不写「实然」**：把**当前实现的缺陷状态**写进规范，会被后来者当成契约照做。实测：契约某处写着「重试耗尽 → `500`」，而 `500` 描述的其实是**修复前的现实**（异常未分类、直接冒泡到全局 ExceptionFilter 兜底）——在那之前，任何照契约实现的人都会复刻这个缺陷
- **引用他人产物时必须给可核验锚点**（稳定 ID + 文件名 + 原文片段），且**引用前实测**。实测出现过：被引证据文件名在磁盘不存在（笔误，同一错名被 3 处引用）、标称冻结时刻晚于实际落盘时刻、违规行范围记为 92–122 而实为 95–121（转述误差）。**声称"实测"的数字必须真的来自实测**，转述与实测要分别标注

---

## 十四、性能预算

| 指标 | 预算 | 校验方式 |
| --- | --- | --- |
| 前端主 chunk（gzip） | ≤ 500KB | vite-bundle-visualizer，CI 卡点 |
| 首屏 LCP | ≤ 2.5s | Lighthouse / 线上监控 |
| API P95 响应 | ≤ 500ms（不含导出类） | Serilog 耗时统计 + 监控告警 |
| 路由级代码分割 | 必须 | 所有 views 路由懒加载 |
| 列表页分页 | 必须 | 禁止一次性全量查询 |

### 14.1 跨层超时预算（必须成表，不能各层各配）

一次请求要穿过四层超时，**哪一层先到，用户看到的行为就由那一层决定**。四层各自"合理"、合起来必然不合理。本项目实测残留：前端 `request.ts` 兜底 `timeout: 15000`（15s），后端最坏重试预算 ≈100s（2 次尝试 × 50s 命令超时），实测终态耗时 **101037ms**——用户在第 15 秒就认为失败；若前置 nginx，其 `proxy_read_timeout` **默认 60s** 还会先切断。

| 层 | 配置项 | 约束 |
| --- | --- | --- |
| 浏览器 / 前端 | `utils/request` 的 `timeout` | **≥ 服务端最坏重试预算**（含重试次数与退避） |
| 反向代理 | `proxy_read_timeout` / `proxy_connect_timeout` | ≥ 后端最坏预算，且 ≤ 前端预算 |
| 应用服务端 | HTTP 请求超时、事务重试总预算 | 由「尝试次数 × 单次超时」**显式推导**并写进注释与契约 |
| 数据库 / 连接 | `CommandTimeout`、`innodb_lock_wait_timeout` 等 | 客户端命令超时 **≥** 服务端同类超时（见 4.4） |

规则：

- 四层数值必须在**架构阶段一次性定死并成表**（落在 `docs/30-architecture.md` 的「性能与成本预算」节），实现阶段只允许引用、不允许各配各的
- 若业务上确实需要「长事务 + 前端短等待」，必须改为**异步语义**（受理即返回受理号 + 轮询结果），不得用前端超时硬扛
- 幂等键能保住**正确性**（用户重试不会重复扣减），但**保不住体验**，也保不住「用户以为失败、实际已成功」的语义——**不能用它替代超时预算对齐**

---

## 十五、AI 编码协作核心准则

> 优先级从高到低，冲突时序号小者优先

1. **规范优先**：严格遵循本规范，冲突时以本文件为准
2. **复用优先**：优先使用已有封装、组件、工具函数，禁止重复造轮子
3. **必须先确认再动手的场景**（其余场景可直接交付）：
   - 引入任何本规范未提及的第三方依赖
   - 架构选型、新增分层、修改目录结构
   - 删除或重写已有文件/公共封装
   - 数据库表结构变更（含索引调整）
   - 破坏性接口变更（涉及 API 版本升级）
   - 后端 DTO / 接口数据结构变更（需同步前端 types）
4. **类型安全**：全链路类型严格对齐，禁止 `any`、禁止类型不匹配
5. **分层清晰**：严格遵守前后端分层，不跨层写逻辑
6. **完整可运行**：输出代码必须完整、可直接运行，关键依赖必须说明
7. **测试随行**：新增/修改业务逻辑必须附带测试
8. **注释适度**：关键逻辑加注释，不写冗余注释
9. **最小改动**：不修改无关代码结构，不做规范未要求的"顺手重构"

---

## 附录 A：代码输出自查清单

输出代码前逐项校验：

- [ ] 符合命名与格式要求
- [ ] 复用了已有封装（request、store、通用组件），无重复实现
- [ ] 类型完整，无 `any`
- [ ] 符合前后端接口约定（版本、分页、幂等、错误码）
- [ ] 无安全隐患（明文密码、SQL 拼接、敏感信息泄露、CORS 放开）
- [ ] 无未约定的第三方依赖
- [ ] 数据库变更有迁移文件
- [ ] 后端 DTO 变更已同步前端 types
- [ ] 新增业务错误码已登记 docs/error-codes.md
- [ ] 新逻辑附带测试
- [ ] 关键操作有审计日志
- [ ] 页面/接口满足性能预算

## 附录 B：常用命令速查

### 前端

| 用途 | 命令 |
| --- | --- |
| 安装依赖 | `npm ci`（按 lockfile）/ `npm install`（变更依赖，需确认） |
| 启动开发 | `npm run dev` |
| 构建 | `npm run build` |
| 代码检查 | `npm run lint` / `npm run format` |
| 单元测试 | `npm run test:unit` |
| E2E 测试 | `npx playwright test`（**首次必做**：`node node_modules/@playwright/test/cli.js install chromium` —— 浏览器内核不在 `npm ci` 范围内） |
| 新增 shadcn 组件 | `npx shadcn-vue@2.x add <组件名>`（固定主版本，禁止 @latest） |

> ⚠️ **仓库绝对路径含 `&`（或空格）时**（Windows）：`npm run *` 经 `cmd.exe` 展开脚本会把 `&` 当作命令分隔符而**必然失败**，且报错指向完全错误的方向（`'xxx' 不是内部或外部命令` / `Cannot find module 'D:\...'`）→ 改直调工具入口 `node node_modules/<tool>/bin/<tool>.js`（如 `vue-tsc/bin/vue-tsc.js`、`vite/bin/vite.js`）。**`npm ci` 不经 script-shell，不受影响**；Linux / CI 同样不受影响。
> ⚠️ **仓库绝对路径含 `#` 时，前端单测会全量失败**，且**与 `npm run` 无关**——Vitest/Vite 自身的路径解析被 `#` 打断，报 `Cannot find module '/src/...'`（8 个文件全部失败，看起来像"测试全挂了"）。实测对照：**同一份代码、同一份 `node_modules`，在无 `#` 的路径下 8 文件 / 63 用例全通过**。处置：把仓库移到不含 `#` 的路径，或按本文「环境约束」的既定做法在无 `#` 的镜像目录中运行并**登记该目录绝对路径作为取证来源**（唯一前缀命名，不清理）。

### 后端

| 用途 | 命令 |
| --- | --- |
| 还原依赖 | `dotnet restore` |
| 构建 | `dotnet build` |
| 启动接口 | `dotnet run --project src/YourProject.Api` |
| 单元/集成测试 | `dotnet test` |
| 新增迁移 | `dotnet ef migrations add <Name> --project src/YourProject.Infrastructure --startup-project src/YourProject.Api` |
| 格式检查 | `dotnet format`（**进到各项目目录**执行，见下方说明） |

> 项目名/目录名以实际仓库为准；与精简版 [CLAUDE.md](../CLAUDE.md)「一、常用命令」保持一致。
> ⚠️ **长驻开发实例会锁死 `bin/` 输出目录**：此后向常规输出目录构建会报 `MSB3027` / `MSB3021`（"文件被 `<进程名> (<PID>)` 锁定"），且**产物停留在改动前的版本**。处置：构建时一律写临时输出目录（`-p:OutputPath=<临时目录>`，测试再加 `--no-build`），**同时登记「`bin/` 内产物是改动前版本」并声明哪些验证因此不可信**；需要刷新 `bin/` 时走停服窗口（细则见 `docs/role-protocol.md` §6）。实测该锁使 E2E 复跑的前置条件（"以新 DLL 重启实例"）**跨多轮无法满足，整条验证链被堵死**。
> ⚠️ `dotnet format` **不支持 `.slnx` 工作区**：在解决方案根目录执行会打印 help 并**以退出码 0 结束**——静默通过，CI 会绿着放过未格式化的代码，比直接报错更危险。正确做法是进到各项目目录执行 `dotnet format whitespace --verify-no-changes`。另注意 `dotnet format whitespace` **不接受 `--nologo`**（会被当成文件路径）。
> `dotnet ef` 工具版本需与 EF Core 主版本匹配（版本不匹配会报错；升级：`dotnet tool update --global dotnet-ef`）。

## 附录 C：本地 AI 权限配置

仓库根目录 `.claude/settings.json` 将「十五、AI 编码协作核心准则」第 3 条固化为 Claude Code 的权限规则（allow / ask / deny 三组，随仓库提交、团队共享）。

**权威在配置本身与 [.claude/README.md](../.claude/README.md)** —— 三组规则的完整清单、与规范的对应关系、按仓库调整方法（前端目录位置 / 包管理器 / 后端路径）、以及已知局限（前缀匹配绕过、Windows 原生命令、无人值守模式行为）**均在那里，本附录不再复述**。

> 不复述的理由：复述即制造**第二落点**。权限规则会随平台版本变化（实测已出现「子代理命中 ask 的行为随版本改变」），两处措辞必然分叉，而读者无从判断哪份是新的（见 13.4）。

个人差异写在 `.claude/settings.local.json`（已加入 `.gitignore`，不随仓库提交）。
