/**
 * TC-78 / TC-79 / TC-79b（概率统计）与 TC-84（性能）共用夹具与工具（test-executor，本轮覆盖补做）。
 *
 * 运行对象：独立的 Testing 实例（默认 `http://127.0.0.1:5199`），数据库为**独立测试库 `luckydraw_test`**
 *          （与主对话的 :5180 / `luckydraw_dev` 完全隔离，见 stats-suite.mjs 头部「实例启动口径」）。
 *
 * 夹具复用口径（CLAUDE.md 工作准则 2）：`tests/e2e/qa.spec.ts` 已有同名夹具，但其为 Playwright/TS
 * 内联实现且未导出（`safeRegister(page, …)` 还依赖 Page 对象），直接 import 会连带注册其全部 `test()`
 * 用例（重复执行）；抽取公共模块则须改动已冻结通过的用例文件。按「最小改动」原则，本模块**沿用同一套
 * 夹具语义与同一 DB 通道**（`docker exec luckydraw-mysql mysql -uroot -pdevonly -D <db>`），并在 Node 侧复用；
 * 库存 / 权重 / 启用状态的复位 SQL 直接取自集成测试夹具 `IntegrationFixture.ResetAsync`（`tests/integration/LuckyDraw.IntegrationTests/IntegrationFixture.cs`）。
 *
 * 严格模式：夹具 SQL 失败即抛错（上一轮曾出现「sql() 吞异常导致夹具静默失效」的教训，不重复）。
 */
import { execFileSync } from 'node:child_process';
import { randomUUID } from 'node:crypto';
import { performance } from 'node:perf_hooks';

export const BASE = process.env.STATS_BASE ?? 'http://127.0.0.1:5199/api/v1';
export const DB = process.env.STATS_DB ?? 'luckydraw_test';
export const PW = 'Abcd1234';

/** FR-03 / `AppDbContext.HasData` 冻结的默认奖池（权重、库存）。 */
export const DEFAULT_POOL = [
  { code: 'prize-keyboard', id: 1, type: 1, weight: 1, stock: 3 },
  { code: 'prize-earbuds', id: 2, type: 1, weight: 3, stock: 10 },
  { code: 'prize-mug', id: 3, type: 1, weight: 10, stock: 50 },
  { code: 'prize-coupon', id: 4, type: 2, weight: 20, stock: 200 },
  { code: 'no-prize', id: 5, type: 3, weight: 66, stock: 0 }
];

export const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
export const tag = () => `${Date.now().toString(36)}${Math.floor(Math.random() * 1e6).toString(36)}`.slice(-8);

/** 直连测试库执行 SQL（-N -B 制表符分隔）。SQL 报错时 execFileSync 抛错 —— 夹具失败必须显式暴露。 */
export function sql(statement) {
  return execFileSync(
    'docker',
    ['exec', 'luckydraw-mysql', 'mysql', '-uroot', '-pdevonly', '-D', DB, '-N', '-B', '-e', statement],
    { encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe'] }
  ).trim();
}

/** 通用接口调用；返回 { http, json, text, ms, error }。ms 为客户端墙钟（含 Node HTTP 开销）。 */
export async function api(method, path, { token, body, idem, timeoutMs = 20000 } = {}) {
  const headers = { 'Content-Type': 'application/json' };
  if (token) headers.Authorization = `Bearer ${token}`;
  if (idem) headers['Idempotency-Key'] = idem;

  const t0 = performance.now();
  let res;
  let text = '';
  let error = null;
  try {
    res = await fetch(`${BASE}${path}`, {
      method,
      headers,
      body: body === undefined ? undefined : JSON.stringify(body),
      signal: AbortSignal.timeout(timeoutMs)
    });
    text = await res.text();
  } catch (e) {
    error = e?.name === 'TimeoutError' ? `timeout(${timeoutMs}ms)` : String(e?.message ?? e);
  }
  const ms = performance.now() - t0;
  let json = null;
  if (text) {
    try {
      json = JSON.parse(text);
    } catch {
      json = null;
    }
  }
  return { http: res?.status ?? 0, json, text, ms, error };
}

/** 注册一次性用户（429 时等待限流窗口滚动）；已存在则登录。 */
export async function registerUser(name) {
  for (let i = 0; i < 12; i++) {
    const r = await api('POST', '/auth/register', {
      body: { userName: name, password: PW, confirmPassword: PW },
      idem: randomUUID()
    });
    if (r.http === 429) {
      await sleep(11000);
      continue;
    }
    if (r.json?.code === 0) return { name, token: r.json.data.accessToken, userId: r.json.data.user.id };

    const l = await api('POST', '/auth/login', {
      body: { userName: name, password: PW },
      idem: randomUUID()
    });
    if (l.json?.code === 0) return { name, token: l.json.data.accessToken, userId: l.json.data.user.id };
    throw new Error(`注册/登录失败 ${name}: ${r.http}/${r.text} | ${l.http}/${l.text}`);
  }
  throw new Error(`注册一直限流 ${name}`);
}

/** 执行一次抽奖（每次全新幂等键，D-03）。 */
export function drawOnce(token, timeoutMs = 20000) {
  return api('POST', '/draw', { token, idem: randomUUID(), timeoutMs });
}

/** 奖池全量快照（读库原文，非接口投影）。 */
export function prizeRows() {
  const raw = sql('SELECT Id,Code,Type,Weight,Stock,IsEnabled,IsDeleted FROM PrizeItem ORDER BY DisplayOrder');
  return raw
    .split('\n')
    .filter(Boolean)
    .map((line) => {
      const [id, code, type, weight, stock, enabled, deleted] = line.split('\t');
      return {
        id: Number(id),
        code,
        type: Number(type),
        weight: Number(weight),
        stock: Number(stock),
        enabled: Number(enabled),
        deleted: Number(deleted)
      };
    });
}

/**
 * 候选集枚举 —— 与 `PrizeRepository.GetDrawCandidatesAsync` 同口径：
 *   `IsEnabled && !IsDeleted && (Type == NoPrize || Stock > 0)`
 * 其中 `!IsDeleted` 来自 `AppDbContext` 的全局查询过滤器。
 */
export function candidateSet() {
  const raw = sql(
    "SELECT Code FROM PrizeItem WHERE IsEnabled = 1 AND IsDeleted = 0 AND (Type = 3 OR Stock > 0) ORDER BY DisplayOrder"
  );
  return raw.split('\n').filter(Boolean);
}

/** 夹具：设置库存 / 权重 / 启用状态。 */
export const setStock = (code, v) => sql(`UPDATE PrizeItem SET Stock = ${v} WHERE Code = '${code}'`);
export const setWeight = (code, v) => sql(`UPDATE PrizeItem SET Weight = ${v} WHERE Code = '${code}'`);
export const setEnabled = (code, v) => sql(`UPDATE PrizeItem SET IsEnabled = ${v} WHERE Code = '${code}'`);
export const pinStockAll = (v) => sql(`UPDATE PrizeItem SET Stock = ${v} WHERE Type <> 3`);

/** 复位为 FR-03 默认奖池（SQL 取自集成测试夹具 `IntegrationFixture.ResetAsync`，语义一致）。 */
export function restorePrizes() {
  sql(
    'UPDATE `PrizeItem` SET ' +
      "`Weight` = CASE `Code` WHEN 'prize-keyboard' THEN 1 WHEN 'prize-earbuds' THEN 3 " +
      "WHEN 'prize-mug' THEN 10 WHEN 'prize-coupon' THEN 20 ELSE 66 END, " +
      "`Stock` = CASE `Code` WHEN 'prize-keyboard' THEN 3 WHEN 'prize-earbuds' THEN 10 " +
      "WHEN 'prize-mug' THEN 50 WHEN 'prize-coupon' THEN 200 ELSE 0 END, " +
      '`IsEnabled` = 1, `IsDeleted` = 0'
  );
}

/** 清空本脚本使用的 Redis 逻辑库（仅 defaultDatabase=2：本实例专用，不触碰 dev(0) / 集成(1)）。 */
export function flushRedisDb() {
  execFileSync('docker', ['exec', 'luckydraw-redis', 'redis-cli', '-n', '2', 'FLUSHDB'], {
    encoding: 'utf8',
    stdio: ['ignore', 'pipe', 'pipe']
  });
}

/** 计算「按候选集实时归一」的理论概率：P(i) = wᵢ / Σw（候选集内）。 */
export function normalizedTheory(rows, codes, weightOf = (r) => r.weight) {
  const inSet = rows.filter((r) => codes.includes(r.code));
  const total = inSet.reduce((s, r) => s + Math.max(0, weightOf(r)), 0);
  const theory = {};
  for (const r of inSet) theory[r.code] = Math.max(0, weightOf(r)) / total;
  return { theory, totalWeight: total, inSet };
}

/** 百分位（线性插值，与常见监控口径一致：p = (n-1) * q）。 */
export function percentile(sortedAsc, q) {
  if (sortedAsc.length === 0) return NaN;
  const pos = (sortedAsc.length - 1) * q;
  const lo = Math.floor(pos);
  const hi = Math.ceil(pos);
  if (lo === hi) return sortedAsc[lo];
  return sortedAsc[lo] + (sortedAsc[hi] - sortedAsc[lo]) * (pos - lo);
}

/** 直方图分桶（毫秒）。 */
export const LAT_BUCKETS = [10, 20, 30, 50, 75, 100, 150, 200, 300, 500, 1000, 2000, Infinity];
export function histogram(samples) {
  const out = LAT_BUCKETS.map((upper, i) => ({
    upper,
    lower: i === 0 ? 0 : LAT_BUCKETS[i - 1],
    count: samples.filter((v) => v >= (i === 0 ? 0 : LAT_BUCKETS[i - 1]) && v < upper).length
  }));
  return out;
}

/** 固定并发度的任务池（保证同时在飞 ≤ concurrency）。 */
export async function runPool(items, worker, concurrency) {
  const results = new Array(items.length);
  let next = 0;
  const runners = Array.from({ length: Math.min(concurrency, items.length) }, async () => {
    for (;;) {
      const i = next++;
      if (i >= items.length) return;
      results[i] = await worker(items[i], i);
    }
  });
  await Promise.all(runners);
  return results;
}
