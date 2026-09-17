/**
 * 覆盖补做（回归轮，2026-09-17）—— 对应 docs/50-testcases.md（v2）首轮「未执行」的 4 条 E2E 时序类用例：
 *   TC-23（§2.8-4 恢复失败后可再试、无定时器循环）
 *   TC-25（§2.8-3 旧后端 refresh 响应缺 user → 按恢复失败处理）
 *   TC-38b（REV-01 回写优先级：抽奖响应回写 > 随后的刷新失败不覆盖）
 *   TC-48c（FR-05-R10 结果条目不在奖池快照时的落点渲染）
 *
 * 运行（于仓库根目录）：node node_modules/@playwright/test/cli.js test coverage.spec.ts
 * 前提：后端 http://localhost:5180、前端 http://localhost:5173 已启动。
 * 夹具口径：沿用 qa.spec.ts —— 直连测试库 luckydraw_dev 准备/还原（50 §0 授权，仅测试环境）。
 *
 * 另含部署配置验收（TC-76 / TC-77 / TC-85，2026-09-17 追加）：
 *   TC-76：生产配置正常启动 + 任意确定性注入 fail fast（另起实例 :5191，环境变量注入；生产产物 = Release publish 输出）
 *   TC-77：Testing 环境确定性强制命中 / 权重覆盖 / 库存夹具场景（另起实例 :5192，独立库 luckydraw_test）
 *   TC-85：生产构建产物路由懒加载分包 + 主 chunk gzip 预算 + 列表强制分页口径
 */
import { test, expect, type Page, type Route } from '@playwright/test';
import { execFileSync, spawnSync, type ChildProcess } from 'node:child_process';
import { randomUUID } from 'node:crypto';
import fs from 'node:fs';
import path from 'node:path';
import zlib from 'node:zlib';
// 后端实例启动的**唯一模板**（docs/role-protocol.md §5）。
// 本文件**禁止**再自行实现启动逻辑：逐份复制的启动代码正是「cwd 漏写 → ContentRoot 错 →
// 配置不加载 → 接口 500 且日志 0 字节」反复复发的原因。手工起实例请用该模块的命令行入口。
import { launchApi, waitExit, REPO_ROOT, PUBLISH_DIR, PUBLISH_DLL, TEST_DB } from './lib/launch-api.mjs';

const PW = 'Abcd1234';
const API = '/api/v1';
const tag = (): string => `${Date.now().toString(36)}${Math.floor(Math.random() * 1e6).toString(36)}`.slice(-8);
const sleep = (ms: number): Promise<void> => new Promise((r) => setTimeout(r, ms));
const tid = (n: string): string => `[data-testid="${n}"]`;

function sql(statement: string): string {
  try {
    return execFileSync(
      'docker',
      ['exec', 'luckydraw-mysql', 'mysql', '-uroot', '-pdevonly', '-D', 'luckydraw_dev', '-N', '-B', '-e', statement],
      { encoding: 'utf8', stdio: ['ignore', 'pipe', 'ignore'] }
    ).trim();
  } catch (e) {
    return `ERR:${String((e as Error).message).split('\n')[0].slice(0, 120)}`;
  }
}

async function safeRegister(page: Page, name: string): Promise<{ token: string; userId: number }> {
  for (let i = 0; i < 12; i++) {
    const res = await page.request.post(`${API}/auth/register`, {
      data: { userName: name, password: PW, confirmPassword: PW },
      headers: { 'Idempotency-Key': randomUUID() }
    });
    if (res.status() === 429) {
      await sleep(11000);
      continue;
    }
    const body = await res.json();
    if (body?.code === 0) return { token: body.data.accessToken, userId: body.data.user.id };
    const login = await page.request.post(`${API}/auth/login`, {
      data: { userName: name, password: PW },
      headers: { 'Idempotency-Key': randomUUID() }
    });
    const lb = await login.json();
    if (lb?.code === 0) return { token: lb.data.accessToken, userId: lb.data.user.id };
    throw new Error(`safeRegister 失败 ${name}: ${JSON.stringify(body)} / ${JSON.stringify(lb)}`);
  }
  throw new Error(`safeRegister 一直限流 ${name}`);
}

function setUsed(userId: number, used: number): void {
  const drawDate = sql("SELECT DATE(CONVERT_TZ(UTC_TIMESTAMP(), '+00:00', '+08:00'))");
  const cnt = sql(`SELECT COUNT(*) FROM UserDrawQuota WHERE UserId = ${userId} AND DrawDate = '${drawDate}'`);
  if (cnt === '0') {
    sql(
      `INSERT INTO UserDrawQuota (UserId,DrawDate,UsedCount,CreateTime,UpdateTime) VALUES (${userId},'${drawDate}',${used},UTC_TIMESTAMP(),UTC_TIMESTAMP())`
    );
  } else {
    sql(`UPDATE UserDrawQuota SET UsedCount = ${used} WHERE UserId = ${userId} AND DrawDate = '${drawDate}'`);
  }
}

function restorePrizes(): void {
  sql("UPDATE PrizeItem SET Weight = 1,  Stock = 3   WHERE Code = 'prize-keyboard'");
  sql("UPDATE PrizeItem SET Weight = 3,  Stock = 10  WHERE Code = 'prize-earbuds'");
  sql("UPDATE PrizeItem SET Weight = 10, Stock = 50  WHERE Code = 'prize-mug'");
  sql("UPDATE PrizeItem SET Weight = 20, Stock = 200 WHERE Code = 'prize-coupon'");
  sql("UPDATE PrizeItem SET Weight = 66, Stock = 0   WHERE Code = 'no-prize'");
  sql('UPDATE PrizeItem SET IsEnabled = 1, IsDeleted = 0');
}

test.describe('覆盖补做（回归轮）', () => {
  test('TC-23 恢复失败后可再试一次，且不得定时器循环重试', async ({ page }) => {
    const name = `c23${tag()}`;
    await safeRegister(page, name); // 建立 refresh Cookie（同 context）

    let refreshCalls = 0;
    let failRecovery = true;
    await page.route('**/api/v1/auth/refresh', async (route: Route) => {
      refreshCalls += 1;
      if (failRecovery) await route.abort('failed');
      else await route.continue();
    });

    // 步骤 1：首次恢复到受保护路由，refresh 网络失败 → 引导登录
    await page.goto('/draw');
    await expect(page.locator(tid('page-login--guard'))).toBeVisible({ timeout: 15000 });
    await expect(page).toHaveURL(/\/login\?redirect=/);
    const callsAfterFail = refreshCalls;

    // 步骤 2：失败后静置 6s，观察是否出现定时器/自动循环重试
    await sleep(6000);
    const callsAfterWait = refreshCalls;
    expect.soft(callsAfterWait - callsAfterFail, '恢复失败后不得以定时器循环重试（6s 内不得新增刷新请求）').toBe(0);

    // 步骤 3：网络恢复后再次导航 → 允许再试一次并放行
    failRecovery = false;
    await page.goto('/draw');
    await expect(page).toHaveURL(/\/draw$/, { timeout: 15000 });
    await expect(page.locator(tid('page-draw--default'))).toBeVisible({ timeout: 15000 });
    const quotaText = await page.locator(tid('draw-quota')).innerText();
    console.log(
      `TC-23 :: 首次恢复失败 → 引导登录（refresh 请求数=${callsAfterFail}，静置 6s 后=${callsAfterWait}）；` +
        `恢复后再次导航 → URL=${page.url()}，徽标="${quotaText.replace(/\n/g, ' ')}"`
    );
  });

  test('TC-25 旧版后端 refresh 响应缺 user 字段 → 按恢复失败处理并引导登录（不白屏、不放行）', async ({ page }) => {
    const name = `c25${tag()}`;
    await safeRegister(page, name);

    let refreshCalls = 0;
    await page.route('**/api/v1/auth/refresh', async (route: Route) => {
      refreshCalls += 1;
      // 模拟旧版后端：code=0 且 data 内**没有 user 字段**
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ code: 0, message: '', data: { accessToken: 'legacy-token-without-user', expiresIn: 900 } })
      });
    });

    await page.goto('/draw');
    await expect(page.locator(tid('page-login--guard'))).toBeVisible({ timeout: 15000 });
    await expect(page).toHaveURL(/\/login\?redirect=/);
    const bodyText = (await page.locator('body').innerText()).trim();
    expect.soft(bodyText.length, '不得白屏').toBeGreaterThan(0);
    // 注意：断言须取 pathname —— `?redirect=/draw` 的查询串同样以 "/draw" 结尾，直接对整串做 /\/draw$/ 会误判
    expect.soft(new URL(page.url()).pathname, '不得以残缺会话放行到受保护页').not.toBe('/draw');
    console.log(
      `TC-25 :: refresh 响应缺 user → URL=${page.url()}；refresh 请求数=${refreshCalls}；页面文本长度=${bodyText.length}（不白屏）`
    );
  });

  test('TC-38b 抽奖响应先回写次数，随后的次数刷新失败不覆盖已确认值', async ({ page }) => {
    restorePrizes();
    const name = `c38b${tag()}`;
    const u = await safeRegister(page, name);
    setUsed(u.userId, 1); // 剩余 2 次

    await page.goto('/draw');
    await expect(page.locator(tid('page-draw--default'))).toBeVisible({ timeout: 15000 });
    await expect(page.locator(tid('draw-quota'))).toContainText('2', { timeout: 15000 });

    // 抽奖成功后令「随后的次数刷新」失败
    let quotaCalls = 0;
    await page.route('**/api/v1/draw/quota', async (route: Route) => {
      quotaCalls += 1;
      await route.abort('failed');
    });

    await page.locator(tid('draw-start')).click();
    await expect(page.locator(tid('draw-quota'))).toContainText('1', { timeout: 25000 });
    await sleep(2500);

    const badge = (await page.locator(tid('draw-quota')).innerText()).replace(/\n/g, ' ');
    expect.soft(badge, '刷新失败不得覆盖抽奖响应已确认的值').toContain('1');
    expect.soft(badge, '不得因刷新失败跳回未知态').not.toContain('—');
    expect.soft(await page.locator(tid('page-draw--noquota')).count(), '不得误判为次数用尽').toBe(0);
    console.log(`TC-38b :: 徽标（抽奖回写后）="${badge}"；随后的次数刷新请求数=${quotaCalls}（均注入失败）`);
  });

  test('TC-48c 结果条目不在奖池快照时的落点渲染（不白屏、结果反馈不被阻塞）', async ({ page }) => {
    const TARGET = 'prize-mug';
    restorePrizes();
    // 前置：目标条目在「页面加载时」处于停用 → 不在前端奖池快照内
    sql(`UPDATE PrizeItem SET IsEnabled = 0 WHERE Code = '${TARGET}'`);

    const name = `c48c${tag()}`;
    const u = await safeRegister(page, name);
    setUsed(u.userId, 0);

    let snapshotIds: number[] = [];
    await page.route('**/api/v1/prizes', async (route: Route) => {
      const resp = await route.fetch();
      const json = await resp.json();
      snapshotIds = (json?.data?.items ?? []).map((item: { id: number }) => item.id);
      await route.fulfill({ response: resp });
    });

    await page.goto('/draw');
    await expect(page.locator(tid('page-draw--default'))).toBeVisible({ timeout: 15000 });

    const targetId = Number(sql(`SELECT Id FROM PrizeItem WHERE Code = '${TARGET}'`));
    expect.soft(snapshotIds, '前置：目标条目不在前端奖池快照内').not.toContain(targetId);

    // 抽奖前：启用目标条目并令其成为唯一可中出候选（其余库存 0、「谢谢参与」权重 0）
    sql(`UPDATE PrizeItem SET IsEnabled = 1, Stock = 5 WHERE Code = '${TARGET}'`);
    sql(`UPDATE PrizeItem SET Stock = 0 WHERE Code <> 'no-prize' AND Code <> '${TARGET}'`);
    sql("UPDATE PrizeItem SET Weight = 0 WHERE Code = 'no-prize'");

    let drawBody: { itemId: number; isWin: boolean } | null = null;
    let prizePoolRefetch = 0;
    await page.route('**/api/v1/prizes', async (route: Route) => {
      prizePoolRefetch += 1;
      await route.continue();
    });
    await page.route('**/api/v1/draw', async (route: Route) => {
      const resp = await route.fetch();
      drawBody = (await resp.json()).data;
      await route.fulfill({ response: resp });
    });

    await page.locator(tid('draw-start')).click();
    // 期望：不卡在旋转动画上，结果弹层照常呈现
    await expect(page.locator(tid('page-draw--win'))).toBeVisible({ timeout: 25000 });
    const winText = (await page.locator(tid('page-draw--win')).innerText()).replace(/\n/g, ' ');
    const bodyText = (await page.locator('body').innerText()).trim();
    expect.soft(bodyText.length, '不得白屏').toBeGreaterThan(0);
    expect.soft(drawBody?.itemId, '后端返回的条目即快照外条目').toBe(targetId);

    console.log(
      `TC-48c :: 后端返回 itemId=${drawBody?.itemId}（快照外条目 id=${targetId}）；弹层文案="${winText}"；` +
        `抽奖后奖池重拉次数=${prizePoolRefetch}（50 原文期望「重拉奖池一次再定位」）`
    );
    restorePrizes();
  });
});

// ============================================================================
// 部署配置验收（TC-76 / TC-77 / TC-85）—— 覆盖补做（2026-09-17）
// 约束：另起实例（:5191 / :5192），配置一律经环境变量注入；不修改 src/ 与任何 appsettings*.json。
// 生产构建产物 = `dotnet publish -c Release` 输出（tests/e2e/deploy/publish-tc76，由 TC-76 用例按需生成）。
// ============================================================================

const API_PROJECT_DIR = path.join(REPO_ROOT, 'src', 'backend', 'src', 'LuckyDraw.Api');
const FRONTEND_DIR = path.join(REPO_ROOT, 'src', 'frontend');

/** 直连独立测试库（部署验收专用；仅测试环境，50 §0 授权口径）。 */
function deploySql(statement: string): string {
  try {
    return execFileSync(
      'docker',
      ['exec', 'luckydraw-mysql', 'mysql', '-uroot', '-pdevonly', '-D', TEST_DB, '-N', '-B', '-e', statement],
      { encoding: 'utf8', stdio: ['ignore', 'pipe', 'ignore'] }
    ).trim();
  } catch (e) {
    return `ERR:${String((e as Error).message).split('\n')[0].slice(0, 120)}`;
  }
}

/** 还原独立测试库奖池为 FR-03 默认值（与 qa.spec.ts 的 restorePrizes 同口径）。 */
function restoreDeployPrizes(): void {
  deploySql("UPDATE PrizeItem SET Weight = 1,  Stock = 3   WHERE Code = 'prize-keyboard'");
  deploySql("UPDATE PrizeItem SET Weight = 3,  Stock = 10  WHERE Code = 'prize-earbuds'");
  deploySql("UPDATE PrizeItem SET Weight = 10, Stock = 50  WHERE Code = 'prize-mug'");
  deploySql("UPDATE PrizeItem SET Weight = 20, Stock = 200 WHERE Code = 'prize-coupon'");
  deploySql("UPDATE PrizeItem SET Weight = 66, Stock = 0   WHERE Code = 'no-prize'");
  deploySql('UPDATE PrizeItem SET IsEnabled = 1, IsDeleted = 0');
}

type LaunchedApi = { child: ChildProcess; logPath: string };

/** 轮询 HTTP 直至返回任一状态码；连接失败视为未就绪，超时返回 null。 */
async function waitHttp(url: string, timeoutMs: number): Promise<number | null> {
  const deadline = Date.now() + timeoutMs;
  while (Date.now() < deadline) {
    try {
      const res = await fetch(url);
      return res.status;
    } catch {
      await sleep(500);
    }
  }
  return null;
}

/** 终止实例（kill → 5s 未退出则 taskkill /T /F 兜底）；已退出则不动作。 */
async function stopApi(child: ChildProcess): Promise<string> {
  if (child.exitCode !== null) return `already-exited(${child.exitCode})`;
  const exited = await new Promise<boolean>((resolve) => {
    const timer = setTimeout(() => resolve(false), 5000);
    child.once('exit', () => {
      clearTimeout(timer);
      resolve(true);
    });
    child.kill();
  });
  if (exited) return `killed(${child.exitCode ?? 'exit'})`;
  if (child.pid !== undefined) {
    spawnSync('taskkill', ['/PID', String(child.pid), '/T', '/F'], { encoding: 'utf8' });
  }
  return 'force-killed(taskkill /T /F)';
}

type DrawData = { itemId: number; isWin: boolean; remainingAttempts: number };

async function apiRegister(port: number, name: string): Promise<{ token: string; userId: number }> {
  const res = await fetch(`http://localhost:${port}/api/v1/auth/register`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', 'Idempotency-Key': randomUUID() },
    body: JSON.stringify({ userName: name, password: PW, confirmPassword: PW })
  });
  const body = (await res.json()) as { code: number; data: { accessToken: string; user: { id: number } } };
  expect(body.code, `注册 ${name} 失败：${JSON.stringify(body)}`).toBe(0);
  return { token: body.data.accessToken, userId: body.data.user.id };
}

async function apiDraw(port: number, token: string): Promise<{ status: number; code: number; data?: DrawData }> {
  const res = await fetch(`http://localhost:${port}/api/v1/draw`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${token}`,
      'Idempotency-Key': randomUUID()
    },
    body: '{}'
  });
  const body = (await res.json()) as { code: number; data?: DrawData };
  return { status: res.status, code: body.code, data: body.data };
}

test.describe('部署配置验收（TC-76 / TC-77 / TC-85）', () => {
  test('TC-76 生产配置：正常启动 + 任意确定性注入 fail fast + 生产产物配置节', async () => {
    test.setTimeout(240_000);

    // 生产构建产物：不存在则现场生成（dotnet publish -c Release，写测试侧目录、不改 src/）
    if (!fs.existsSync(PUBLISH_DLL)) {
      const pub = spawnSync('dotnet', ['publish', '-c', 'Release', '-o', PUBLISH_DIR], {
        cwd: API_PROJECT_DIR,
        encoding: 'utf8',
        timeout: 300_000
      });
      expect(pub.status, `dotnet publish 失败：${pub.stdout ?? ''}\n${pub.stderr ?? ''}`).toBe(0);
    }

    // (a) 生产基线配置：确定性开关关闭、覆盖项为空（生产产物 appsettings.json）
    const baseCfg = JSON.parse(fs.readFileSync(path.join(PUBLISH_DIR, 'appsettings.json'), 'utf8')) as {
      Draw: { Deterministic: { Enabled: boolean; ForcedResults: unknown[] } };
      Prize: { WeightOverrides: Record<string, number> };
    };
    console.log(
      `TC-76 :: 生产基线 appsettings.json：Deterministic.Enabled=${baseCfg.Draw.Deterministic.Enabled}，` +
        `ForcedResults=${baseCfg.Draw.Deterministic.ForcedResults.length} 项，WeightOverrides=${Object.keys(baseCfg.Prize.WeightOverrides).length} 项`
    );
    expect(baseCfg.Draw.Deterministic.Enabled).toBe(false);
    expect(baseCfg.Draw.Deterministic.ForcedResults).toHaveLength(0);
    expect(Object.keys(baseCfg.Prize.WeightOverrides)).toHaveLength(0);

    // 端口必须空闲（防上一轮残留实例造成假结论）
    expect(await waitHttp('http://localhost:5191/api/v1/prizes', 1500)).toBeNull();

    // (b) 以生产配置启动 → 正常监听（确定性配置一旦生效会 fail fast，走不到这一步）
    const ok = launchApi(5191, { ASPNETCORE_ENVIRONMENT: 'Production' }, 'coverage-tc76-prod-start');
    let okStatus: number | null = null;
    try {
      okStatus = await waitHttp('http://localhost:5191/api/v1/prizes', 60_000);
    } finally {
      const stop = await stopApi(ok.child);
      console.log(`TC-76 :: 生产配置启动：未登录访问 /api/v1/prizes → HTTP=${okStatus}（期望 401）；日志=${ok.logPath}；停止=${stop}`);
    }
    expect.soft(okStatus, `生产配置应正常启动并可服务（日志 ${ok.logPath}）`).toBe(401);

    // (c) 注入任意确定性配置 → 启动失败（fail fast）
    const injections: Array<[string, Record<string, string>]> = [
      ['Deterministic.Enabled=true', { Draw__Deterministic__Enabled: 'true' }],
      [
        'ForcedResults 非空',
        {
          Draw__Deterministic__ForcedResults__0__UserName: 'tc76x',
          Draw__Deterministic__ForcedResults__0__PrizeItemCode: 'prize-mug'
        }
      ],
      ['WeightOverrides 非空', { 'Prize__WeightOverrides__prize-mug': '5' }]
    ];
    for (let i = 0; i < injections.length; i += 1) {
      const [label, env] = injections[i];
      const fail = launchApi(5191, { ASPNETCORE_ENVIRONMENT: 'Production', ...env }, `coverage-tc76-failfast-${i + 1}`);
      const code = await waitExit(fail.child, 90_000);
      await stopApi(fail.child);
      // 宿主日志为本机控制台编码（GBK）——只匹配 ASCII 标记，避免编码干扰
      const raw = fs.readFileSync(fail.logPath, 'latin1');
      const rejected =
        raw.includes('OptionsValidationException') && raw.includes('Development / Testing') && raw.includes('D-06 / PRD R5');
      console.log(
        `TC-76 :: 注入「${label}」→ exit=${code === null ? '未退出(90s)' : code}；拒绝启动标记=${rejected}；日志=${fail.logPath}`
      );
      expect.soft(code, `注入「${label}」必须 fail fast（日志 ${fail.logPath}）`).not.toBeNull();
      expect.soft(code === null ? -999 : code, `注入「${label}」退出码应为非 0（日志 ${fail.logPath}）`).not.toBe(0);
      expect.soft(rejected, `注入「${label}」应输出 D-06 拒绝启动错误（日志 ${fail.logPath}）`).toBe(true);
    }

    // (d) 生产构建产物确定性相关键的**取值**符合 D-06 配置键表（50 v3 第三条款口径）
    //     2026-09-17 用户裁决（解释 1：判 TC-76 原用词过严）→ 50 v3 改为断言取值，
    //     不以「该配置节是否存在」为判定条件；取值 = D-06 配置键表 495-500 行「生产基线取值」列。
    console.log(
      `TC-76 :: 生产产物确定性键取值 → Draw:Deterministic:Enabled=${baseCfg.Draw.Deterministic.Enabled}（期望 false）、` +
        `Draw:Deterministic:ForcedResults.length=${baseCfg.Draw.Deterministic.ForcedResults.length}（期望 0）、` +
        `Prize:WeightOverrides 键数=${Object.keys(baseCfg.Prize.WeightOverrides).length}（期望 0）`
    );
    expect.soft(baseCfg.Draw.Deterministic.Enabled, 'Draw:Deterministic:Enabled 应为 false（D-06 配置键表）').toBe(false);
    expect
      .soft(baseCfg.Draw.Deterministic.ForcedResults.length, 'Draw:Deterministic:ForcedResults[] 应为空数组（D-06 配置键表）')
      .toBe(0);
    expect
      .soft(Object.keys(baseCfg.Prize.WeightOverrides).length, 'Prize:WeightOverrides{} 应为空对象（D-06 配置键表）')
      .toBe(0);
  });

  test('TC-77 测试环境（Testing）：确定性强制命中 / 权重覆盖 / 库存夹具场景生效', async () => {
    test.setTimeout(240_000);
    restoreDeployPrizes();

    const hitUser = `d77a${tag()}`;
    const loseUser = `d77b${tag()}`;
    const weightUser = `d77c${tag()}`;

    expect(await waitHttp('http://localhost:5192/api/v1/prizes', 1500)).toBeNull();

    // 前置：蓝牙耳机库存 = 1（测试夹具；对应 AC-13 的 Given「库存 = 1 且被配置为必然中出」）
    deploySql("UPDATE PrizeItem SET Stock = 1 WHERE Code = 'prize-earbuds'");
    const earbudsId = Number(deploySql("SELECT Id FROM PrizeItem WHERE Code = 'prize-earbuds'"));
    const couponId = Number(deploySql("SELECT Id FROM PrizeItem WHERE Code = 'prize-coupon'"));
    const noPrizeId = Number(deploySql("SELECT Id FROM PrizeItem WHERE Code = 'no-prize'"));

    const inst = launchApi(
      5192,
      {
        ASPNETCORE_ENVIRONMENT: 'Testing',
        // 让宿主启动日志落入证据文件（Testing 配置默认抑制 Microsoft.* 信息级日志）
        Serilog__MinimumLevel__Override__Microsoft: 'Information',
        Draw__Deterministic__Enabled: 'true',
        Draw__Deterministic__ForcedResults__0__UserName: hitUser,
        Draw__Deterministic__ForcedResults__0__PrizeItemCode: 'prize-earbuds',
        Draw__Deterministic__ForcedResults__1__UserName: loseUser,
        Draw__Deterministic__ForcedResults__1__PrizeItemCode: 'no-prize',
        'Prize__WeightOverrides__prize-coupon': '5'
      },
      'coverage-tc77-testing'
    );

    try {
      const ready = await waitHttp('http://localhost:5192/api/v1/prizes', 60_000);
      expect.soft(ready, `Testing 实例应正常启动（日志 ${inst.logPath}）`).toBe(401);
      console.log(`TC-77 :: Testing 实例就绪（HTTP=${ready}）；日志=${inst.logPath}`);

      const u1 = await apiRegister(5192, hitUser);
      const u2 = await apiRegister(5192, loseUser);
      const u3 = await apiRegister(5192, weightUser);

      // AC-08：强制命中「必然中出」奖品（库存 1 → 0，记录 +1，次数 3 → 2）
      const r1 = await apiDraw(5192, u1.token);
      const stockAfterHit = deploySql("SELECT Stock FROM PrizeItem WHERE Code = 'prize-earbuds'");
      const rec1 = deploySql(`SELECT COUNT(*) FROM WinningRecord WHERE UserId = ${u1.userId}`);
      const rec1Name = deploySql(`SELECT PrizeName FROM WinningRecord WHERE UserId = ${u1.userId} LIMIT 1`);
      console.log(
        `TC-77 :: AC-08 强制命中 → itemId=${r1.data?.itemId}（期望 ${earbudsId}）isWin=${r1.data?.isWin} remaining=${r1.data?.remainingAttempts}；` +
          `库存 1→${stockAfterHit}；记录数=${rec1}（奖品名="${rec1Name}"）`
      );
      expect.soft(r1.code).toBe(0);
      expect.soft(r1.data?.itemId).toBe(earbudsId);
      expect.soft(r1.data?.isWin).toBe(true);
      expect.soft(r1.data?.remainingAttempts).toBe(2);
      expect.soft(stockAfterHit).toBe('0');
      expect.soft(rec1).toBe('1');

      // AC-09：强制命中「谢谢参与」（次数 -1，无记录）
      const r2 = await apiDraw(5192, u2.token);
      const rec2 = deploySql(`SELECT COUNT(*) FROM WinningRecord WHERE UserId = ${u2.userId}`);
      console.log(
        `TC-77 :: AC-09 强制未中奖 → itemId=${r2.data?.itemId}（期望 ${noPrizeId}）isWin=${r2.data?.isWin} remaining=${r2.data?.remainingAttempts}；记录数=${rec2}`
      );
      expect.soft(r2.data?.itemId).toBe(noPrizeId);
      expect.soft(r2.data?.isWin).toBe(false);
      expect.soft(r2.data?.remainingAttempts).toBe(2);
      expect.soft(rec2).toBe('0');

      // 权重覆盖生效（判别式）：库内全部权重置 0 + 配置覆盖 prize-coupon=5
      // 覆盖生效 → 唯一正权重条目（coupon）必中；覆盖未生效 → 权重总和 0 回退末位候选（no-prize）
      deploySql('UPDATE PrizeItem SET Weight = 0');
      const r3 = await apiDraw(5192, u3.token);
      console.log(
        `TC-77 :: 权重覆盖判别（库内权重全 0 + ` +
          `Prize__WeightOverrides__prize-coupon=5）→ itemId=${r3.data?.itemId}` +
          `（覆盖生效应为 ${couponId}；未生效将回退末位 ${noPrizeId}）`
      );
      expect.soft(r3.code).toBe(0);
      expect.soft(r3.data?.itemId).toBe(couponId);
    } finally {
      const stop = await stopApi(inst.child);
      restoreDeployPrizes();
      console.log(`TC-77 :: 实例已停止（${stop}）；独立测试库奖池已还原为 FR-03 默认值`);
    }
  });

  test('TC-85 生产构建产物：路由懒加载分包 + 主 chunk gzip 预算 + 列表强制分页', async () => {
    test.setTimeout(420_000);
    const tscBin = path.join(FRONTEND_DIR, 'node_modules', 'vue-tsc', 'bin', 'vue-tsc.js');
    const viteBin = path.join(FRONTEND_DIR, 'node_modules', 'vite', 'bin', 'vite.js');
    expect(fs.existsSync(tscBin), `缺少 ${tscBin}`).toBe(true);
    expect(fs.existsSync(viteBin), `缺少 ${viteBin}`).toBe(true);

    // 构建（仓库路径含 `&`，npm script 在 cmd.exe 下不可用 → 直调 node 入口）
    const tsc = spawnSync('node', [tscBin, '--noEmit'], { cwd: FRONTEND_DIR, encoding: 'utf8', timeout: 300_000 });
    console.log(`TC-85 :: vue-tsc --noEmit exit=${tsc.status}`);
    expect.soft(tsc.status, `vue-tsc 失败：${(tsc.stdout ?? '').slice(-800)}${(tsc.stderr ?? '').slice(-400)}`).toBe(0);

    const build = spawnSync('node', [viteBin, 'build'], { cwd: FRONTEND_DIR, encoding: 'utf8', timeout: 300_000 });
    const buildOut = `${build.stdout ?? ''}${build.stderr ?? ''}`;
    console.log(
      `TC-85 :: vite build exit=${build.status}\n` +
        buildOut
          .split('\n')
          .filter((l) => /modules transformed|built in|error|warning/i.test(l))
          .join('\n')
    );
    expect.soft(build.status, `vite build 失败：${buildOut.slice(-800)}`).toBe(0);

    const assetsDir = path.join(FRONTEND_DIR, 'dist', 'assets');
    const assets = fs.readdirSync(assetsDir).filter((f) => f.endsWith('.js'));
    const readAsset = (name: string): string => fs.readFileSync(path.join(assetsDir, name), 'utf8');

    // (1) 路由懒加载：4 个视图各自独立分包，且被其它 chunk 以动态 import 引用（而非内联进入口）
    const views = ['RegisterView', 'LoginView', 'DrawView', 'WinningRecordsView'];
    const entry = assets.find((f) => /^index-.*\.js$/.test(f));
    console.log(`TC-85 :: 入口 chunk = ${entry ?? '缺失'}；全部 JS 分包 = ${assets.join(', ')}`);
    for (const v of views) {
      const chunk = assets.find((f) => f.startsWith(`${v}-`));
      console.log(`TC-85 :: 视图分包 ${v}：${chunk ?? '缺失'}（${chunk ? fs.statSync(path.join(assetsDir, chunk)).size : 0} B）`);
      expect.soft(chunk, `缺少 ${v} 的独立 chunk（路由未懒加载）`).toBeTruthy();
      if (chunk) {
        const referenced = assets.some((f) => f !== chunk && readAsset(f).includes(chunk));
        expect.soft(referenced, `${chunk} 未被动态引用（可能被内联）`).toBe(true);
      }
    }
    expect.soft(entry, '缺少入口 chunk index-*.js').toBeTruthy();
    if (entry) {
      const entrySrc = readAsset(entry);
      expect.soft(entrySrc.includes('page-records--'), '记录视图代码被内联进入口 chunk（未懒加载）').toBe(false);
      const entryRaw = fs.statSync(path.join(assetsDir, entry)).size;
      const entryGz = zlib.gzipSync(fs.readFileSync(path.join(assetsDir, entry))).length;
      console.log(
        `TC-85 :: 主 chunk ${entry}：raw=${entryRaw} B，gzip=${entryGz} B（预算 ≤ ${500 * 1024} B，规范十四）`
      );
      expect.soft(entryGz, '主 chunk gzip 超预算（规范十四：≤ 500KB）').toBeLessThanOrEqual(500 * 1024);
    }
    expect.soft(fs.existsSync(path.join(FRONTEND_DIR, 'dist', 'index.html')), 'dist/index.html 缺失').toBe(true);

    // (2) 列表强制分页、无一次性全量查询（源码口径；运行时证据见 TC-67 / TC-62）
    const recordsView = fs.readFileSync(path.join(FRONTEND_DIR, 'src', 'views', 'records', 'WinningRecordsView.vue'), 'utf8');
    const recordApi = fs.readFileSync(path.join(FRONTEND_DIR, 'src', 'api', 'record.ts'), 'utf8');
    const passesPage = /pageIndex:\s*pageIndex\.value/.test(recordsView) && /pageSize:\s*pageSize\.value/.test(recordsView);
    const apiPaged = recordApi.includes("http.get<PageResult<WinningRecordDto>>('/records'") && recordApi.includes('PageQuery');
    console.log(`TC-85 :: 列表强制分页：视图传参 pageIndex/pageSize=${passesPage}；api 走 PageQuery 分页=${apiPaged}（tC-67 运行时已验证单页 ≤10）`);
    expect.soft(passesPage, '记录列表未传 pageIndex/pageSize（存在全量查询风险）').toBe(true);
    expect.soft(apiPaged, '记录接口未走分页 PageQuery').toBe(true);
  });
});
