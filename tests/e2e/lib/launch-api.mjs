/**
 * 后端实例启动的**唯一模板**（`docs/role-protocol.md` §5）。
 *
 * 为什么必须唯一 —— 本仓库实测踩过，表象与真实原因完全不符，排查成本极高：
 *   1. `dotnet <相对路径>.dll` 会把 **ContentRoot 设为当前工作目录**，导致 `appsettings*.json`
 *      不被加载 → 签名密钥为空 → 所有签发令牌的接口返回 500，**且日志文件 0 字节**
 *      （日志 sink 本身也来自配置文件）。表象是"程序没起来"，真实原因是配置没读到。
 *   2. 就绪判据若走匿名可访问的接口（`/health`、`GET /prizes → 401`），**测不出配置缺失** ——
 *      不带 token 时认证中间件根本不进入签名校验，故障态与正常态给出**完全相同**的信号。
 *      本项目曾因此烧掉**整整 45 轮**。
 *   3. 逐份复制的启动代码正是第 1 条**反复复发**的原因：每份都可能漏掉 `cwd`。
 *
 * 因此：`tests/e2e/**` 下**只允许存在这一份**启动实现。两种用法都走它：
 *   - 代码内（Playwright spec / 其它 `.mjs`）：`import { launchApi } from './lib/launch-api.mjs'`
 *   - 命令行手工起（**替代手敲 `dotnet xxx.dll`**）：
 *       node tests/e2e/lib/launch-api.mjs --port 5199 --log stats-instance
 *     并可用 `--env Draw__DailyLimit=30000` 重复注入环境变量。
 *
 * 就绪检查请用 `waitReady()`，**不要**用端口连通 / `/health` / 匿名接口 —— 那些是伪就绪检查。
 * 签发路径按项目定：本项目是 `POST /api/v1/auth/register`（用例实际会走的那条）。
 */
import { spawn } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const HERE = path.dirname(fileURLToPath(import.meta.url));

/** 仓库根（由本文件位置推导，不依赖 cwd —— 从任何目录调用结果一致）。 */
export const REPO_ROOT = path.resolve(HERE, '..', '..', '..');
/** 生产产物目录（`dotnet publish` 输出；`cwd` 必须锁死为它，见文件头第 1 条）。 */
export const PUBLISH_DIR = path.join(REPO_ROOT, 'tests', 'e2e', 'deploy', 'publish-tc76');
export const PUBLISH_DLL = path.join(PUBLISH_DIR, 'LuckyDraw.Api.dll');
/** 可持久证据目录：落在 Playwright `outputDir`（`tests/e2e/.artifacts/`）**之外**。 */
export const LOGS_DIR = path.join(REPO_ROOT, 'tests', 'e2e', 'logs');
/** 独立测试库（与集成测试同库），避免污染开发库 `luckydraw_dev`。 */
export const TEST_DB = 'luckydraw_test';
export const CONNECTION_STRING =
  `Server=localhost;Port=3307;Database=${TEST_DB};User Id=root;Password=devonly;CharSet=utf8mb4;SslMode=None;AllowPublicKeyRetrieval=True`;

/**
 * 文件名用时间戳 + **进程内自增序号**。
 * 证据卫生要求（`docs/role-protocol.md` §7）：时间戳到秒**不足以**保证唯一 ——
 * 实测出现过三次 fail-fast 的日志因**同秒碰撞**聚合进同一文件，归属不可区分。
 */
const stamp = (() => {
  let seq = 0;
  return () => {
    const d = new Date();
    const p = (n) => String(n).padStart(2, '0');
    seq += 1;
    return `${d.getFullYear()}${p(d.getMonth() + 1)}${p(d.getDate())}-${p(d.getHours())}${p(d.getMinutes())}${p(d.getSeconds())}-${String(seq).padStart(2, '0')}`;
  };
})();

/**
 * 另起一个 API 实例：环境变量注入配置（不落任何配置文件），**工作目录 = 生产产物目录**。
 * @param {number} port 监听端口（建议每个批次用独立端口，避免与开发实例互抢）
 * @param {Record<string, string>} env 额外环境变量（覆盖默认值）
 * @param {string} logName 日志文件名前缀（完整文件名为 `<logName>-<时间戳>-<序号>.log`）
 * @returns {{ child: import('node:child_process').ChildProcess, logPath: string }}
 */
export function launchApi(port, env, logName) {
  fs.mkdirSync(LOGS_DIR, { recursive: true });
  const logPath = path.join(LOGS_DIR, `${logName}-${stamp()}.log`);
  const stream = fs.createWriteStream(logPath, { flags: 'a' });
  const child = spawn('dotnet', [PUBLISH_DLL], {
    cwd: PUBLISH_DIR, // ← 关键行：工作目录 = 生产产物目录；漏掉它 = 配置不加载 + 日志 0 字节
    env: {
      ...process.env,
      ASPNETCORE_URLS: `http://localhost:${port}`,
      ConnectionStrings__Default: CONNECTION_STRING,
      Redis__Configuration: 'localhost:6379,defaultDatabase=2',
      // 仅测试夹具用的签名密钥（原 coverage.spec.ts 内联值，迁移时**逐字保留**以免改变 TC-76 行为）
      Jwt__SigningKey: 'tc76-production-signing-key-0123456789abcdef',
      ...env
    },
    stdio: ['ignore', 'pipe', 'pipe'] // stdout/stderr 必须落文件，否则故障时只剩 0 字节日志
  });
  child.stdout?.pipe(stream);
  child.stderr?.pipe(stream);
  return { child, logPath };
}

/**
 * 真实就绪检查：**必须走依赖配置最深的那条路径**（签发令牌），不能用端口连通 / `/health` / 匿名接口。
 * 本项目实测：`GET /prizes → 401` 的判据恰好绕过了唯一会坏的那条路径。
 * @returns {Promise<boolean>} 就绪返回 true；超时返回 false（此时后续一切"用例失败"结论**无效**）
 */
export async function waitReady(port, timeoutMs = 30_000) {
  const deadline = Date.now() + timeoutMs;
  while (Date.now() < deadline) {
    try {
      const res = await fetch(`http://localhost:${port}/api/v1/auth/register`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', 'Idempotency-Key': crypto.randomUUID() },
        body: JSON.stringify({ userName: `ready${Date.now().toString(36)}`, password: 'Abcd1234', confirmPassword: 'Abcd1234' })
      });
      // 只要能走到签发逻辑就说明配置已加载：业务码返回即视为就绪（400/409 等均说明服务与配置可用）
      const body = await res.json().catch(() => null);
      if (res.status !== 500 && body && typeof body.code === 'number') return true;
    } catch {
      /* 实例还没起来，继续等 */
    }
    await new Promise((r) => setTimeout(r, 500));
  }
  return false;
}

/** 进程退出等待；超时返回 null。 */
export function waitExit(child, timeoutMs) {
  return new Promise((resolve) => {
    if (child.exitCode !== null) {
      resolve(child.exitCode);
      return;
    }
    const timer = setTimeout(() => resolve(null), timeoutMs);
    child.once('exit', (code) => {
      clearTimeout(timer);
      resolve(code ?? -1);
    });
  });
}

// ---- 命令行入口：手工起实例也走唯一模板（替代手敲 `dotnet xxx.dll`）----
if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const argv = process.argv.slice(2);
  const getArg = (name, fallback) => {
    const i = argv.indexOf(name);
    return i >= 0 && argv[i + 1] ? argv[i + 1] : fallback;
  };
  const port = Number(getArg('--port', '5199'));
  const logName = getArg('--log', 'manual-instance');
  const extra = {};
  for (let i = 0; i < argv.length; i += 1) {
    if (argv[i] === '--env' && argv[i + 1]) {
      const [k, ...rest] = argv[i + 1].split('=');
      if (k) extra[k] = rest.join('=');
    }
  }
  if (!fs.existsSync(PUBLISH_DLL)) {
    console.error(`发布产物不存在：${PUBLISH_DLL}\n请先执行：dotnet publish -c Release -o ${PUBLISH_DIR}`);
    process.exit(2);
  }
  const { child, logPath } = launchApi(port, extra, logName);
  console.log(`已启动实例 pid=${child.pid} port=${port}`);
  console.log(`日志（可持久证据）：${logPath}`);
  const ok = await waitReady(port);
  if (!ok) {
    console.error('就绪检查未通过（走的是签发令牌路径）—— 后续一切"用例失败"结论无效，不得据此记缺陷。');
    child.kill();
    process.exit(3);
  }
  console.log('就绪检查通过（POST /api/v1/auth/register 可签发）');
  const stop = () => {
    child.kill();
    process.exit(0);
  };
  process.on('SIGINT', stop);
  process.on('SIGTERM', stop);
}
