import { defineConfig, devices } from '@playwright/test'

/**
 * 顶层 E2E 配置（用例在 `tests/e2e/**`）。
 *
 * **为什么配置与 `package.json` 在仓库根目录**：Node 的模块解析是**向上查找**的，
 * `tests/e2e/` 下的 `*.spec.ts` 里的 `import ... from '@playwright/test'` 只能从用例所在目录
 * 向上找到 `<root>/node_modules`。把依赖挂在前端工程（`src/frontend/node_modules`）时
 * 用例解析不到该包（实测 `Cannot find module '@playwright/test'`）。
 * 故 E2E 工具链在根目录独立成一套，与 `src/frontend` 的前端依赖相互独立。
 *
 * 运行方式（于仓库根目录）：
 *   node node_modules/@playwright/test/cli.js test
 * 或 `npm run test:e2e`——但**当仓库绝对路径含 `&`（或空格）时不可用**：npm script 在 cmd.exe
 * 下会把 `&` 当命令分隔符（报 `'xxx' 不是内部或外部命令`）。上面的直连方式在该情形下同样可用，
 * 故**一律用直连方式**（判定条件与边界见 `docs/development-spec.md` 附录 B）。
 *
 * ⚠️ 本注释旧版本把「仓库绝对路径含 `&`」写成**本机实然事实**，仓库改名后该断言即失效，
 * 而读的人无从判断。**描述环境前提一律用条件式，不要写实然**（`docs/development-spec.md` 13.4）。
 *
 * 启动前提（两个服务需先手动起，本配置**不托管 webServer**——`webServer.command` 走 shell，
 * 与 npm script 踩同一个坑）：
 * - 后端 `dotnet run --project src/backend/src/LuckyDraw.Api` → http://localhost:5180
 *   （依赖 `dotnet user-secrets` 的 `ConnectionStrings:Default` / `Jwt:SigningKey`，见 REV-09）
 * - 前端 于 `src/frontend/` 执行 `node node_modules/vite/bin/vite.js` → http://localhost:5173
 *   前端经 vite proxy 把 `/api` 转发到 5180，故 E2E 只访问 5173（同源，refresh Cookie 不跨站）
 *
 * 证据留痕：`outputDir`（`tests/e2e/.artifacts/`）**每次运行前被 Playwright 自动清空**，
 * 失败截图 / trace 只在本轮有效，会被任意一次后续运行抹掉（**不是人工删除**）。
 * 需要跨轮持久化的证据必须写到该目录**之外**（本仓库为 `tests/e2e/logs/*.log`），
 * 文件名含唯一序号——见 `docs/role-protocol.md` §7「证据卫生」。
 */
export default defineConfig({
  testDir: './tests/e2e',
  // 用例共享同一套 MySQL / Redis（每日次数、库存、幂等键都会互相影响），必须串行
  fullyParallel: false,
  workers: 1,
  // 失败即失败，不靠重试掩盖不稳定用例——测试执行阶段要的是事实，不是绿色
  retries: 0,
  timeout: 30_000,
  expect: { timeout: 5_000 },
  reporter: [['list']],
  outputDir: './tests/e2e/.artifacts',
  use: {
    baseURL: 'http://localhost:5173',
    // 失败留痕：截图 + trace，供 test-executor 写缺陷证据
    screenshot: 'only-on-failure',
    trace: 'retain-on-failure',
    locale: 'zh-CN',
    timezoneId: 'Asia/Shanghai'
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] }
    }
  ]
})
