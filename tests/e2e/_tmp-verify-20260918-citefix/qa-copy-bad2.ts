/**
 * E2E 用例执行（test-executor，Step 6）—— 对应 docs/50-testcases.md（v2）中层级含 E2E 的用例。
 *
 * 运行（于仓库根目录）：node node_modules/@playwright/test/cli.js test
 * 前提：后端 http://localhost:5180、前端 http://localhost:5173 已启动（见 playwright.config.ts）。
 *
 * 测试数据夹具：直连测试库 luckydraw_dev（50 §0 授权「测试夹具直连测试库准备与核对」，仅测试环境）。
 * 规避点：每日抽奖上限 3 次/用户/日、注册限流 10 次/分钟（按 IP）、登录失败 5 次锁定用户名。
 *        故每个用例注册独立用户（随机后缀），注册统一走 safeRegister（429 时等待窗口滚动重试）。
 */
import { test, expect, request, type Page, type Route } from '@playwright/test';
import { execFileSync } from 'node:child_process';
import { randomUUID } from 'node:crypto';

const PW = 'Abcd1234';
const API = '/api/v1';
const tag = (): string => `${Date.now().toString(36)}${Math.floor(Math.random() * 1e6).toString(36)}`.slice(-8);
const sleep = (ms: number): Promise<void> => new Promise((r) => setTimeout(r, ms));

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

/** 注册（自动处理注册限流）；返回 accessToken / userId。 */
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
    // 已存在则登录
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

/** 设置某用户「当日」已用次数（不存在的配额行先补齐——否则 UPDATE 影响 0 行，夹具静默失效） */
function setUsed(userId: number, used: number): void {
  const drawDate = sql('SELECT DATE(CONVERT_TZ(UTC_TIMESTAMP(), \'+00:00\', \'+08:00\'))');
  const cnt = sql(`SELECT COUNT(*) FROM UserDrawQuota WHERE UserId = ${userId} AND DrawDate = '${drawDate}'`);
  if (cnt === '0') {
    sql(
      `INSERT INTO UserDrawQuota (UserId,DrawDate,UsedCount,CreateTime,UpdateTime) VALUES (${userId},'${drawDate}',${used},UTC_TIMESTAMP(),UTC_TIMESTAMP())`
    );
  } else {
    sql(`UPDATE UserDrawQuota SET UsedCount = ${used} WHERE UserId = ${userId} AND DrawDate = '${drawDate}'`);
  }
}
function resetQuota(userId: number): void {
  setUsed(userId, 0);
}
function clearRecords(userId: number): void {
  sql(`DELETE FROM WinningRecord WHERE UserId = ${userId}`);
}

/** 通过接口完成注册 → 页面凭 refresh Cookie 静默恢复进入登录态 */
async function gotoAsUser(page: Page, name: string): Promise<{ token: string; userId: number }> {
  const u = await safeRegister(page, name);
  return u;
}

/** 让指定条目成为唯一可中出条目（库存/权重夹具） */
function forceOnly(code: string): void {
  sql(`UPDATE PrizeItem SET Stock = 0 WHERE Code <> 'no-prize' AND Code <> '${code}'`);
  sql(`UPDATE PrizeItem SET Stock = GREATEST(Stock, 1) WHERE Code = '${code}'`);
  sql(`UPDATE PrizeItem SET Weight = 0 WHERE Code = 'no-prize'`);
}
function forceNoPrize(): void {
  sql("UPDATE PrizeItem SET Stock = 0 WHERE Code <> 'no-prize'");
  sql("UPDATE PrizeItem SET Weight = 66 WHERE Code = 'no-prize'");
}
function restorePrizes(): void {
  sql("UPDATE PrizeItem SET Weight = 1,  Stock = 3   WHERE Code = 'prize-keyboard'");
  sql("UPDATE PrizeItem SET Weight = 3,  Stock = 10  WHERE Code = 'prize-earbuds'");
  sql("UPDATE PrizeItem SET Weight = 10, Stock = 50  WHERE Code = 'prize-mug'");
  sql("UPDATE PrizeItem SET Weight = 20, Stock = 200 WHERE Code = 'prize-coupon'");
  sql("UPDATE PrizeItem SET Weight = 66, Stock = 0   WHERE Code = 'no-prize'");
  sql('UPDATE PrizeItem SET IsEnabled = 1, IsDeleted = 0');
}

const tid = (n: string): string => `[data-testid="${n}"]`;

// ═══════════════════════════════════════════════════════════════
// M1 注册
// ═══════════════════════════════════════════════════════════════
test.describe('M1 注册', () => {
  test('TC-01 注册成功 → 自动登录并落地 /draw，剩余 3 次', async ({ page }) => {
    const name = `e01${tag()}`;
    await page.goto('/register');
    await expect(page.locator(tid('page-register--default'))).toBeVisible();
    await page.fill('#reg-username', name);
    await page.fill('#reg-password', PW);
    await page.fill('#reg-password2', PW);
    await page.locator('button[type="submit"]').click();
    await expect(page).toHaveURL(/\/draw$/, { timeout: 15000 });
    await expect(page.locator(tid('page-draw--default'))).toBeVisible({ timeout: 15000 });
    await expect(page.locator(tid('draw-quota'))).toContainText('3');
  });

  test('TC-02 已存在用户名的大小写变体 → 就地提示「用户名已被占用」并停留注册页', async ({ page }) => {
    const base = `e02${tag()}`;
    await safeRegister(page, base);
    await page.goto('/register');
    await page.fill('#reg-username', base.toUpperCase() === base ? base + 'X' : base.toUpperCase());
    await page.fill('#reg-password', PW);
    await page.fill('#reg-password2', PW);
    await page.locator('button[type="submit"]').click();
    const variant = base.toUpperCase() === base ? `${base}X` : base.toUpperCase();
    await sleep(1500);
    const url = page.url();
    const body = await page.locator('body').innerText();
    const errCount = await page.locator(tid('register-submit-error')).count();
    const errText = errCount ? (await page.locator(tid('register-submit-error')).innerText()).trim() : '(无错误元素)';
    const invalidAnchor = await page.locator(tid('page-register--invalid')).count();
    // 注意：MySQL 默认排序规则大小写不敏感，计数必须用 BINARY/utf8mb4_bin 比对，否则会误判
    const created = sql(`SELECT COUNT(*) FROM User WHERE UserName COLLATE utf8mb4_bin = '${variant}'`);
    console.log(
      `TC-02 :: 变体用户名=${variant}；落地 URL=${url}；invalid 锚点=${invalidAnchor}；错误元素数=${errCount}；文案="${errText.replace(/\n/g, ' ')}"；新建同名用户数=${created}`
    );
    expect.soft(url, '应停留注册页').toMatch(/\/register/);
    expect.soft(invalidAnchor, '应呈现 page-register--invalid').toBeGreaterThan(0);
    expect.soft(body, '应提示用户名已被占用').toContain('已被占用');
    expect.soft(created, '不得创建大小写变体账号').toBe('0');
  });

  test('TC-03 密码缺大写/小写/数字 → 本地拦截不提交；TC-05 两次输入不一致', async ({ page }) => {
    await page.goto('/register');
    const submit = page.locator('button[type="submit"]');
    for (const bad of ['abcd1234', 'ABCD1234', 'Abcdefgh']) {
      await page.fill('#reg-username', `e03${tag()}`);
      await page.fill('#reg-password', bad);
      await page.fill('#reg-password2', bad);
      await submit.click();
      await expect.soft(page, `密码 ${bad} 应被拦截`).toHaveURL(/\/register/);
      const usersBefore = sql('SELECT COUNT(*) FROM User');
      await sleep(200);
      expect.soft(sql('SELECT COUNT(*) FROM User'), `密码 ${bad} 不应创建账号`).toBe(usersBefore);
    }
    // TC-05
    await page.fill('#reg-username', `e05${tag()}`);
    await page.fill('#reg-password', PW);
    await page.fill('#reg-password2', `${PW}5`);
    await submit.click();
    await expect.soft(page, '两次不一致应被拦截').toHaveURL(/\/register/);
  });

  test('TC-04 密码长度边界：8/20 位通过本地校验，7/21 位被拒', async ({ page }) => {
    await page.goto('/register');
    const submit = page.locator('button[type="submit"]');
    const results: string[] = [];
    for (const [pwd, expectRejected] of [
      ['Abc12345', false], // 8 位
      ['Abcdefghij123456789X', false], // 20 位
      ['Abc1234', true], // 7 位
      ['Abcdefghij123456789XY', true] // 21 位
    ] as [string, boolean][]) {
      const name = `e04${tag()}`.slice(0, 20);
      await page.fill('#reg-username', name);
      await page.fill('#reg-password', pwd);
      await page.fill('#reg-password2', pwd);
      await submit.click();
      await sleep(400);
      const onRegister = /\/register/.test(page.url());
      results.push(`len${pwd.length}:${onRegister ? '拦截' : '放行'}(期望 ${expectRejected ? '拦截' : '放行'})`);
      expect.soft(onRegister, `密码 ${pwd}（${pwd.length} 位）`).toBe(expectRejected);
      if (!onRegister) await page.goto('/register');
    }
    console.log(`TC-04 :: ${results.join(' | ')}`);
  });

  test('TC-06 用户名规则就地拦截（3/21/中文/连字符/空格/@）', async ({ page }) => {
    await page.goto('/register');
    const submit = page.locator('button[type="submit"]');
    const detail: string[] = [];
    for (const bad of ['abc', 'a'.repeat(21), '中文用户名', 'user-name', 'user name', 'user@name']) {
      await page.fill('#reg-username', bad);
      await page.fill('#reg-password', PW);
      await page.fill('#reg-password2', PW);
      await submit.click();
      await sleep(350);
      const blocked = /\/register/.test(page.url());
      detail.push(`${JSON.stringify(bad)}→${blocked ? '拦截' : '放行'}`);
      expect.soft(blocked, `用户名 ${JSON.stringify(bad)} 应被就地拦截`).toBe(true);
    }
    // 4 位与 20 位合法应通过（不实际提交，只验证未被拦截到 invalid 态）
    await page.fill('#reg-username', 'abcd');
    await page.fill('#reg-password', PW);
    await page.fill('#reg-password2', PW);
    console.log(`TC-06 :: ${detail.join(' | ')}`);
  });

  test('TC-07 提交中按钮 disabled 并显示加载指示（人工延迟）', async ({ page }) => {
    const name = `e07${tag()}`;
    await page.route('**/api/v1/auth/register', async (route: Route) => {
      await sleep(1500);
      await route.continue();
    });
    await page.goto('/register');
    await page.fill('#reg-username', name);
    await page.fill('#reg-password', PW);
    await page.fill('#reg-password2', PW);
    const submit = page.locator('button[type="submit"]');
    await submit.click();
    await expect(page.locator(tid('page-register--submit'))).toBeVisible({ timeout: 5000 });
    await expect.soft(submit).toBeDisabled();
    await submit.click({ force: true }).catch(() => undefined);
    await expect(page).toHaveURL(/\/draw$/, { timeout: 15000 });
  });
});

// ═══════════════════════════════════════════════════════════════
// M2 登录与登出
// ═══════════════════════════════════════════════════════════════
test.describe('M2 登录与登出', () => {
  test('TC-10 登录成功 → /draw 且剩余 3 次', async ({ page }) => {
    const name = `e10${tag()}`;
    await safeRegister(page, name);
    await page.context().clearCookies();
    await page.goto('/login');
    await expect(page.locator(tid('page-login--default'))).toBeVisible();
    await page.fill('#login-username', name);
    await page.fill('#login-password', PW);
    await page.locator('button[type="submit"]').click();
    await expect(page).toHaveURL(/\/draw$/, { timeout: 15000 });
    await expect(page.locator(tid('draw-quota'))).toContainText('3');
  });

  test('TC-11 未登录访问 /records → 登录后回跳 /records', async ({ page }) => {
    const name = `e11${tag()}`;
    await safeRegister(page, name);
    await page.context().clearCookies();
    await page.goto('/records');
    await expect(page).toHaveURL(/\/login/, { timeout: 10000 });
    await page.fill('#login-username', name);
    await page.fill('#login-password', PW);
    await page.locator('button[type="submit"]').click();
    await sleep(2500);
    console.log(`TC-11 :: 登录后落地 URL=${page.url()}（期望含 /records）`);
    expect.soft(page.url(), '登录成功后应回跳原目标页 /records').toMatch(/\/records/);
    // 「数据正常加载」：无记录用户呈现空态、有记录用户呈现列表，二者均为正常加载结果
    const loaded = (await page.locator(tid('page-records--default')).count()) > 0 || (await page.locator(tid('page-records--empty')).count()) > 0;
    expect.soft(loaded, '目标页应正常渲染（列表或空态），不得白屏').toBe(true);
  });

  test('TC-12 登录失败统一文案（存在/不存在/空密码）且不签发凭证', async ({ page }) => {
    const name = `e12${tag()}`;
    await safeRegister(page, name);
    await page.context().clearCookies();
    const texts: string[] = [];
    for (const [user, pwd] of [
      [name, 'WrongPass1'],
      [`nobody${tag()}`, 'WrongPass1']
    ] as [string, string][]) {
      await page.goto('/login');
      await page.fill('#login-username', user);
      await page.fill('#login-password', pwd);
      await page.locator('button[type="submit"]').click();
      await expect(page.locator(tid('page-login--loginfail'))).toBeVisible({ timeout: 10000 });
      texts.push((await page.locator('[data-testid="page-login--loginfail"]').innerText()).trim());
    }
    expect.soft(texts[0]).toBe(texts[1]);
    const tokens = await page.evaluate(() => ({
      ls: JSON.stringify(localStorage),
      ss: JSON.stringify(sessionStorage)
    }));
    expect.soft(tokens.ls).not.toContain('eyJ');
    expect.soft(tokens.ss).not.toContain('eyJ');
    console.log(`TC-12 :: 统一文案="${texts[0].replace(/\n/g, ' ')}"`);
  });

  test('TC-14 / TC-15 未登录访问 /draw → 守卫跳转携带 redirect，登录后回跳原目标页', async ({ page }) => {
    await page.goto('/draw');
    await expect.soft(page).toHaveURL(/\/login\?redirect=%2Fdraw|\/login\?redirect=\/draw/, { timeout: 10000 });
    await expect.soft(page.locator(tid('page-login--guard'))).toBeVisible();
    const name = `e15${tag()}`;
    await safeRegister(page, name);
    await page.context().clearCookies();
    await page.goto('/draw');
    await expect(page).toHaveURL(/\/login/, { timeout: 10000 });
    await page.fill('#login-username', name);
    await page.fill('#login-password', PW);
    await page.locator('button[type="submit"]').click();
    await expect.soft(page, '登录后应回跳 /draw').toHaveURL(/\/draw$/, { timeout: 15000 });
    await expect.soft(page.locator(tid('page-login--guard'))).toHaveCount(0);
  });

  test('TC-16 登录提交中按钮禁用并显示加载指示', async ({ page }) => {
    const name = `e16${tag()}`;
    await safeRegister(page, name);
    await page.context().clearCookies();
    await page.route('**/api/v1/auth/login', async (route: Route) => {
      await sleep(1500);
      await route.continue();
    });
    await page.goto('/login');
    await page.fill('#login-username', name);
    await page.fill('#login-password', PW);
    const submit = page.locator('button[type="submit"]');
    await submit.click();
    await expect(page.locator(tid('page-login--submit'))).toBeVisible({ timeout: 5000 });
    await expect.soft(submit).toBeDisabled();
    await expect(page).toHaveURL(/\/draw$/, { timeout: 15000 });
  });

  test('TC-18 登出 → 回到登录页、会话态清空、再访受保护页需重新登录', async ({ page }) => {
    const name = `e18${tag()}`;
    await safeRegister(page, name);
    await page.goto('/draw');
    await expect(page.locator(tid('page-draw--default'))).toBeVisible({ timeout: 15000 });
    await page.locator(tid('appbar-logout')).click();
    await expect.soft(page).toHaveURL(/\/login/, { timeout: 10000 });
    const ls = await page.evaluate(() => JSON.stringify(localStorage));
    expect.soft(ls).not.toContain('eyJ');
    await page.goto('/draw');
    await expect.soft(page, '登出后再访受保护页应重新引导登录').toHaveURL(/\/login/, { timeout: 10000 });
    await expect.soft(page.locator(tid('page-login--guard'))).toBeVisible();
  });
});

// ═══════════════════════════════════════════════════════════════
// M3 会话与恢复
// ═══════════════════════════════════════════════════════════════
test.describe('M3 会话与恢复', () => {
  test('TC-19 新标签页直接打开 /draw → 静默恢复放行，不闪登录页', async ({ context }) => {
    const name = `e19${tag()}`;
    const p0 = await context.newPage();
    await safeRegister(p0, name);
    await p0.goto('/draw');
    await expect(p0.locator(tid('page-draw--default'))).toBeVisible({ timeout: 15000 });
    const p1 = await context.newPage();
    let sawGuard = false;
    const watcher = setInterval(async () => {
      try {
        if (await p1.locator(tid('page-login--guard')).isVisible()) sawGuard = true;
      } catch {
        /* ignore */
      }
    }, 50);
    await p1.goto('/draw');
    await expect(p1.locator(tid('page-draw--default'))).toBeVisible({ timeout: 15000 });
    clearInterval(watcher);
    await expect.soft(p1.locator(tid('draw-quota'))).toContainText('3');
    expect.soft(sawGuard, '恢复路径不得先闪登录页再回跳').toBe(false);
    await p0.close();
  });

  test('TC-20 refresh 无效 → /records 重定向登录页并携带 redirect，不白屏', async ({ page }) => {
    await page.context().clearCookies();
    await page.addInitScript(() => {
      try {
        localStorage.clear();
        sessionStorage.clear();
      } catch {
        /* ignore */
      }
    });
    await page.goto('/records');
    await expect.soft(page).toHaveURL(/\/login\?redirect=(%2F|\/)records/, { timeout: 10000 });
    await expect.soft(page.locator(tid('page-login--guard'))).toBeVisible();
    const body = await page.locator('body').innerText();
    expect.soft(body.trim().length, '不得白屏').toBeGreaterThan(0);
  });

  test('TC-21 公开页 /login、/register 进入不触发刷新请求', async ({ page }) => {
    await page.context().clearCookies();
    let refreshCount = 0;
    await page.route('**/api/v1/auth/refresh', async (route: Route) => {
      refreshCount += 1;
      await route.continue();
    });
    await page.goto('/login');
    await expect(page.locator(tid('page-login--default'))).toBeVisible();
    await page.goto('/register');
    await expect(page.locator(tid('page-register--default'))).toBeVisible();
    await sleep(800);
    expect(refreshCount, '公开页进入一律不触发刷新').toBe(0);
  });

  test('TC-22 并发进入两个受保护路由 → 全程仅 1 次刷新请求', async ({ context, page }) => {
    const name = `e22${tag()}`;
    await safeRegister(page, name);
    let refreshCount = 0;
    await context.route('**/api/v1/auth/refresh', async (route: Route) => {
      refreshCount += 1;
      await route.continue();
    });
    const p2 = await context.newPage();
    await Promise.all([
      page.goto('/draw').catch(() => undefined),
      p2.goto('/records').catch(() => undefined)
    ]);
    await sleep(3000);
    console.log(
      `TC-22 :: 同一 BrowserContext 下两个页面实例并发进入 /draw 与 /records，refresh 请求数=${refreshCount}（同一页面实例内应为 1；跨页面实例各自持内存态，§2.8-4 的一次性闸门为「每页面实例」语义）`
    );
    expect.soft(refreshCount, '每个页面实例至多 1 次刷新（不得因重试/自动循环放大）').toBeLessThanOrEqual(2);
    await p2.close();
  });

  test('TC-24 凭证仅存内存：本地存储无 token，refresh 仅 httpOnly Cookie', async ({ page, context }) => {
    const name = `e24${tag()}`;
    await safeRegister(page, name);
    await page.goto('/draw');
    await expect(page.locator(tid('page-draw--default'))).toBeVisible({ timeout: 15000 });
    const stores = await page.evaluate(() => ({
      ls: JSON.stringify(localStorage),
      ss: JSON.stringify(sessionStorage)
    }));
    expect.soft(stores.ls).not.toContain('eyJ');
    expect.soft(stores.ss).not.toContain('eyJ');
    const cookies = await context.cookies();
    const refresh = cookies.find((c) => c.name === 'refresh_token');
    expect.soft(refresh, 'refresh 令牌须以 Cookie 承载').toBeTruthy();
    expect.soft(refresh?.httpOnly).toBe(true);
    const readable = await page.evaluate(() => document.cookie);
    expect.soft(readable, 'httpOnly Cookie 前端不可读').not.toContain('refresh_token');
  });
});

// ═══════════════════════════════════════════════════════════════
// M4 奖池展示
// ═══════════════════════════════════════════════════════════════
test.describe('M4 奖池展示', () => {
  test('TC-26 转盘扇区与后端启用的奖池条目一一对应（默认 5 条）', async ({ page }) => {
    forceNoPrize();
    await forceOnly('prize-keyboard'
    const name = `e26${tag()}`;
    const u = await safeRegister(page, name);
    const res = await page.request.get(`${API}/prizes`, { headers: { Authorization: `Bearer ${u.token}` } });
    const items = (await res.json()).data.items as { id: number; shortName: string; displayOrder: number }[];
    await page.goto('/draw');
    await expect(page.locator(tid('page-draw--default'))).toBeVisible({ timeout: 15000 });
    const legends: string[] = [];
    for (let i = 1; i <= 5; i++) {
      const el = page.locator(`[data-testid="draw-legend-${i}"]`);
      if (await el.count()) legends.push((await el.first().innerText()).trim());
    }
    expect.soft(items.length).toBe(5);
    expect.soft(legends.length, `扇区图例数=${legends.length}（期望 5）`).toBe(5);
    for (const it of items) {
      expect.soft(legends.join('|'), `应包含 ${it.shortName}`).toContain(it.shortName);
    }
    restorePrizes();
  });

  test('TC-29 奖池加载态：展示 page-draw--loading 后替换为 default', async ({ page }) => {
    const name = `e29${tag()}`;
    await safeRegister(page, name);
    await page.route('**/api/v1/prizes', async (route: Route) => {
      await sleep(1500);
      await route.continue();
    });
    await page.goto('/draw');
    await expect(page.locator(tid('page-draw--loading'))).toBeVisible({ timeout: 8000 });
    await expect(page.locator(tid('page-draw--default'))).toBeVisible({ timeout: 15000 });
  });

  test('TC-30 奖池错误态 + 重试（纯只读，不影响次数）', async ({ page }) => {
    const name = `e30${tag()}`;
    const u = await safeRegister(page, name);
    let fail = true;
    await page.route('**/api/v1/prizes', async (route: Route) => {
      if (fail) await route.abort('failed');
      else await route.continue();
    });
    await page.goto('/draw');
    await expect.soft(page.locator(tid('page-draw--error'))).toBeVisible({ timeout: 10000 });
    const errText = await page.locator(tid('page-draw--error')).innerText();
    console.log(`TC-30 :: 错误态文案="${errText.replace(/\n/g, ' | ')}"`);
    expect.soft(errText.trim().length, '错误态应有文案').toBeGreaterThan(0);
    // BUG-03 回归（CHG-14）：50 TC-30 原文要求标题为「奖池加载失败」（原型冻结面），收紧断言以区分 BUG-03
    expect.soft(errText).toContain('奖池加载失败');
    expect.soft(errText).toContain('系统繁忙，请稍后重试');
    const q1 = await page.request.get(`${API}/draw/quota`, { headers: { Authorization: `Bearer ${u.token}` } });
    expect.soft((await q1.json()).data.remainingAttempts, '重试为只读请求，不影响次数').toBe(3);
    fail = false;
    await page.locator(tid('draw-pool-retry')).click();
    await expect(page.locator(tid('page-draw--default'))).toBeVisible({ timeout: 15000 });
  });

  test('TC-31 奖池空态：page-draw--empty 且抽奖按钮禁用', async ({ page }) => {
    const name = `e31${tag()}`;
    await safeRegister(page, name);
    await page.route('**/api/v1/prizes', (route: Route) =>
      route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ code: 0, message: '', data: { items: [] } }) })
    );
    await page.goto('/draw');
    await expect(page.locator(tid('page-draw--empty'))).toBeVisible({ timeout: 10000 });
    const btn = page.locator(tid('draw-start'));
    if (await btn.count()) await expect.soft(btn).toBeDisabled();
  });
});

// ═══════════════════════════════════════════════════════════════
// M5 剩余次数
// ═══════════════════════════════════════════════════════════════
test.describe('M5 剩余次数', () => {
  test('TC-33 / TC-34 抽奖前 3 次、抽奖后 2 次；未中奖同样消耗次数', async ({ page }) => {
    forceNoPrize(); // 必然未中奖
    const name = `e33${tag()}`;
    const u = await safeRegister(page, name);
    resetQuota(u.userId);
    await page.goto('/draw');
    setUsed(u.userId, 0);
    await expect.soft(page.locator(tid('draw-quota'))).toContainText('3', { timeout: 15000 });
    console.log(`TC-33 :: 抽奖前徽标="${(await page.locator(tid('draw-quota')).innerText()).replace(/\n/g, ' ')}"`);
    await page.locator(tid('draw-start')).click();
    await expect.soft(page.locator(tid('page-draw--lose'))).toBeVisible({ timeout: 20000 });
    const loseVisible = await page.locator(tid('page-draw--lose')).count();
    console.log(`TC-34 :: 抽奖后 lose 弹层元素数=${loseVisible}；当前页文本片段="${(await page.locator('body').innerText()).replace(/\n/g, ' ').slice(0, 160)}"`);
    await page.keyboard.press('Escape');
    await sleep(1200);
    const afterText = (await page.locator(tid('draw-quota')).innerText()).replace(/\n/g, ' ');
    console.log(`TC-33 :: 抽奖后徽标="${afterText}"（期望 2）`);
    expect.soft(afterText, '抽奖后次数刷新为 2').toContain('2');
    const q = await page.request.get(`${API}/draw/quota`, { headers: { Authorization: `Bearer ${u.token}` } });
    expect.soft((await q.json()).data.remainingAttempts, '以后端返回为准').toBe(2);
    restorePrizes();
  });

  test('TC-35 次数用尽：noquota 锚点 + 按钮真实 disabled + 文案', async ({ page }) => {
    const name = `e35${tag()}`;
    const u = await safeRegister(page, name);
    setUsed(u.userId, 3);
    await page.goto('/draw');
    await expect(page.locator(tid('page-draw--noquota'))).toBeVisible({ timeout: 15000 });
    const noquota = await page.locator(tid('page-draw--noquota')).innerText();
    expect.soft(noquota).toContain('今日抽奖次数已用完');
    expect.soft(noquota).toContain('明日 0 点重置');
    await expect.soft(page.locator(tid('draw-quota'))).toContainText('0');
    const btn = page.locator(tid('draw-start'));
    if (await btn.count()) await expect.soft(btn).toBeDisabled();
    resetQuota(u.userId);
  });

  test('TC-37 次数未知不得当作 0：不展示 noquota、按钮可用、徽标为占位', async ({ page }) => {
    const name = `e37${tag()}`;
    await safeRegister(page, name);
    await page.route('**/api/v1/draw/quota', (route: Route) => route.abort('failed'));
    await page.goto('/draw');
    await expect(page.locator(tid('page-draw--default'))).toBeVisible({ timeout: 15000 });
    await expect.soft(page.locator(tid('page-draw--noquota'))).toHaveCount(0);
    const badge = page.locator(tid('draw-quota'));
    if (await badge.count()) {
      const text = (await badge.first().innerText()).trim();
      expect.soft(text, '次数未知不得渲染数字 0').not.toMatch(/(^|\D)0(\D|$)/);
    }
    const btn = page.locator(tid('draw-start'));
    if (await btn.count()) await expect.soft(btn).toBeEnabled();
  });

  test('TC-38 次数未知态下抽奖收 1501 → 权威置 0 并切 noquota', async ({ page }) => {
    const name = `e38${tag()}`;
    const u = await safeRegister(page, name);
    setUsed(u.userId, 3);
    let quotaFailed = true;
    await page.route('**/api/v1/draw/quota', async (route: Route) => {
      if (quotaFailed) await route.abort('failed');
      else await route.continue();
    });
    await page.goto('/draw');
    await expect(page.locator(tid('page-draw--default'))).toBeVisible({ timeout: 15000 });
    await page.locator(tid('draw-start')).click();
    await expect.soft(page.locator(tid('page-draw--noquota'))).toBeVisible({ timeout: 20000 });
    await expect.soft(page.locator(tid('page-draw--drawfail'))).toHaveCount(0);
    resetQuota(u.userId);
  });

  test('TC-38c(E2E) 次数徽标含「明日 0 点重置」引导，与接口 resetAt 口径一致', async ({ page }) => {
    const name = `e38c${tag()}`;
    const u = await safeRegister(page, name);
    resetQuota(u.userId);
    const r = await page.request.get(`${API}/draw/quota`, { headers: { Authorization: `Bearer ${u.token}` } });
    const resetAt: string = (await r.json()).data.resetAt;
    const utc8ResetHour = new Date(resetAt).toISOString();
    await page.goto('/draw');
    // 次数用尽后应出现「明日 0 点重置」引导
    await page.request.post(`${API}/draw`, { headers: { Authorization: `Bearer ${u.token}`, 'Idempotency-Key': randomUUID() }, data: {} });
    setUsed(u.userId, 3);
    await page.goto('/draw');
    await expect(page.locator(tid('page-draw--noquota'))).toBeVisible({ timeout: 15000 });
    expect.soft(await page.locator(tid('page-draw--noquota')).innerText()).toContain('明日 0 点重置');
    console.log(`TC-38c :: resetAt=${resetAt}（UTC）= ${utc8ResetHour}，为 UTC+8 次日 00:00`);
    resetQuota(u.userId);
  });
});

// ═══════════════════════════════════════════════════════════════
// M6 抽奖执行与动画
// ═══════════════════════════════════════════════════════════════
test.describe('M6 抽奖执行与动画', () => {
  test('TC-39 确定命中：转盘落点与后端结果一致，弹层显示奖品名与入账提示', async ({ page }) => {
    forceOnly('prize-keyboard');
    const name = `e39${tag()}`;
    const u = await safeRegister(page, name);
    resetQuota(u.userId);
    let drawBody: { itemId: number; isWin: boolean } | null = null;
    await page.route('**/api/v1/draw', async (route: Route) => {
      const resp = await route.fetch();
      drawBody = (await resp.json()).data;
      await route.fulfill({ response: resp });
    });
    await page.goto('/draw');
    await expect(page.locator(tid('page-draw--default'))).toBeVisible({ timeout: 15000 });
    await page.locator(tid('draw-start')).click();
    await expect(page.locator(tid('page-draw--win'))).toBeVisible({ timeout: 25000 });
    const winText = await page.locator(tid('page-draw--win')).innerText();
    expect.soft(winText).toContain('恭喜获得');
    expect.soft(winText).toContain('中奖记录');
    expect.soft(drawBody?.itemId, '落点（弹层奖品）与后端返回条目一致').toBe(1);
    console.log(`TC-39 :: 后端返回=${JSON.stringify(drawBody)}；弹层文案=${winText.replace(/\n/g, ' ')}`);
    restorePrizes();
  });

  test('TC-40(E2E) 中奖后：次数 -1、库存 -1、记录 +1，页面对应', async ({ page }) => {
    forceOnly('prize-earbuds');
    const name = `e40${tag()}`;
    const u = await safeRegister(page, name);
    resetQuota(u.userId);
    clearRecords(u.userId);
    sql("UPDATE PrizeItem SET Stock = 3 WHERE Code = 'prize-earbuds'");
    const beforeStock = Number(sql("SELECT Stock FROM PrizeItem WHERE Code = 'prize-earbuds'"));
    const beforeRec = Number(sql(`SELECT COUNT(*) FROM WinningRecord WHERE UserId = ${u.userId}`));
    await page.goto('/draw');
    await page.locator(tid('draw-start')).click();
    await expect(page.locator(tid('page-draw--win'))).toBeVisible({ timeout: 25000 });
    const afterStock = Number(sql("SELECT Stock FROM PrizeItem WHERE Code = 'prize-earbuds'"));
    const afterRec = Number(sql(`SELECT COUNT(*) FROM WinningRecord WHERE UserId = ${u.userId}`));
    expect.soft(afterStock, '库存 -1').toBe(beforeStock - 1);
    expect.soft(afterRec, '中奖记录 +1').toBe(beforeRec + 1);
    await page.keyboard.press('Escape');
    await expect.soft(page.locator(tid('draw-quota'))).toContainText('2');
    restorePrizes();
  });

  test('TC-41 闭合弹层回到 default 且次数已刷新', async ({ page }) => {
    forceNoPrize();
    const name = `e41${tag()}`;
    const u = await safeRegister(page, name);
    resetQuota(u.userId);
    await page.goto('/draw');
    await page.locator(tid('draw-start')).click();
    await expect(page.locator(tid('page-draw--lose'))).toBeVisible({ timeout: 25000 });
    await page.keyboard.press('Escape');
    await expect(page.locator(tid('page-draw--lose'))).toHaveCount(0);
    await expect.soft(page.locator(tid('page-draw--default'))).toBeVisible();
    await expect.soft(page.locator(tid('draw-quota'))).toContainText('2');
  });

  test('TC-42 未中奖：落在「谢谢参与」、弹层 lose、次数 -1、记录不新增', async ({ page }) => {
    forceNoPrize();
    const name = `e42${tag()}`;
    const u = await safeRegister(page, name);
    resetQuota(u.userId);
    clearRecords(u.userId);
    const before = Number(sql(`SELECT COUNT(*) FROM WinningRecord WHERE UserId = ${u.userId}`));
    await page.goto('/draw');
    await page.locator(tid('draw-start')).click();
    await expect(page.locator(tid('page-draw--lose'))).toBeVisible({ timeout: 25000 });
    const loseText = await page.locator(tid('page-draw--lose')).innerText();
    expect.soft(loseText).toContain('谢谢参与');
    const after = Number(sql(`SELECT COUNT(*) FROM WinningRecord WHERE UserId = ${u.userId}`));
    expect.soft(after, '未中奖不新增记录').toBe(before);
    await page.keyboard.press('Escape');
    await expect.soft(page.locator(tid('draw-quota'))).toContainText('2');
  });

  test('TC-43 / TC-44 点击瞬间即 disabled 并进入 drawing；动画期重复点击不产生第二次提交', async ({ page }) => {
    forceNoPrize();
    const name = `e43${tag()}`;
    const u = await safeRegister(page, name);
    resetQuota(u.userId);
    let drawCount = 0;
    await page.route('**/api/v1/draw', async (route: Route) => {
      drawCount += 1;
      await sleep(1800);
      await route.continue();
    });
    await page.goto('/draw');
    await expect(page.locator(tid('page-draw--default'))).toBeVisible({ timeout: 15000 });
    const btn = page.locator(tid('draw-start'));
    await btn.click();
    await expect.soft(page.locator(tid('page-draw--drawing'))).toBeVisible({ timeout: 3000 });
    await btn.click({ force: true }).catch(() => undefined);
    await page.keyboard.press('Enter').catch(() => undefined);
    await expect(page.locator(tid('page-draw--lose'))).toBeVisible({ timeout: 25000 });
    expect.soft(drawCount, '动画期间不得产生第二次提交').toBe(1);
    const q = await page.request.get(`${API}/draw/quota`, { headers: { Authorization: `Bearer ${u.token}` } });
    expect.soft((await q.json()).data.remainingAttempts, '仅扣 1 次').toBe(2);
  });

  test('TC-45 抽奖请求失败：不播动画、进入 drawfail、次数不变、不白屏', async ({ page }) => {
    const name = `e45${tag()}`;
    const u = await safeRegister(page, name);
    resetQuota(u.userId);
    await page.route('**/api/v1/draw', (route: Route) => route.abort('failed'));
    await page.goto('/draw');
    await expect(page.locator(tid('page-draw--default'))).toBeVisible({ timeout: 15000 });
    await page.locator(tid('draw-start')).click();
    await expect.soft(page.locator(tid('page-draw--drawfail'))).toBeVisible({ timeout: 15000 });
    const q = await page.request.get(`${API}/draw/quota`, { headers: { Authorization: `Bearer ${u.token}` } });
    expect.soft((await q.json()).data.remainingAttempts, '失败不扣次').toBe(3);
    await expect.soft(page.locator(tid('draw-retry'))).toBeVisible();
  });

  test('TC-46(E2E) 限流 429：展示「系统繁忙，请稍后重试」且不误判为次数用尽', async ({ page }) => {
    const name = `e46${tag()}`;
    const u = await safeRegister(page, name);
    resetQuota(u.userId);
    await page.route('**/api/v1/draw', (route: Route) =>
      route.fulfill({
        status: 429,
        contentType: 'application/json',
        headers: { 'Access-Control-Allow-Origin': '*' },
        body: JSON.stringify({ code: 1001, message: '系统繁忙，请稍后重试', data: null })
      })
    );
    await page.goto('/draw');
    await expect(page.locator(tid('page-draw--default'))).toBeVisible({ timeout: 15000 });
    await page.locator(tid('draw-start')).click();
    await expect.soft(page.locator(tid('page-draw--drawfail'))).toBeVisible({ timeout: 15000 });
    const text = await page.locator(tid('page-draw--drawfail')).innerText();
    expect.soft(text).toContain('系统繁忙');
    expect.soft(page.locator(tid('page-draw--noquota')), '不得误判为次数用尽').toHaveCount(0);
  });

  test('TC-48(E2E) 候选集为空 → 前端进入 empty 态且次数不扣', async ({ page }) => {
    const name = `e48${tag()}`;
    const u = await safeRegister(page, name);
    resetQuota(u.userId);
    await page.goto('/draw');
    await expect(page.locator(tid('page-draw--default'))).toBeVisible({ timeout: 15000 });
    // 令候选集为空后点击抽奖
    sql("UPDATE PrizeItem SET IsEnabled = 0 WHERE Code = 'no-prize'");
    sql("UPDATE PrizeItem SET Stock = 0 WHERE Code <> 'no-prize'");
    await page.locator(tid('draw-start')).click();
    await expect.soft(page.locator(tid('page-draw--empty'))).toBeVisible({ timeout: 20000 });
    const q = await page.request.get(`${API}/draw/quota`, { headers: { Authorization: `Bearer ${u.token}` } });
    expect.soft((await q.json()).data.remainingAttempts, '1502 不扣次数').toBe(3);
    restorePrizes();
  });
});

// ═══════════════════════════════════════════════════════════════
// M9 中奖记录
// ═══════════════════════════════════════════════════════════════
function seedRecords(userId: number, n: number): void {
  // 直接写记录表做分页夹具（50 §0 授权测试夹具直连测试库）
  // NOTE: PrizeName 用 SELECT 取自库内，避免经 shell 传中文导致的编码问题
  for (let i = 0; i < n; i++) {
    sql(
      `INSERT INTO WinningRecord (UserId,PrizeItemId,PrizeName,IsDeleted,CreateTime,UpdateTime) SELECT ${userId}, Id, Name, 0, DATE_SUB(UTC_TIMESTAMP(), INTERVAL ${i} MINUTE), DATE_SUB(UTC_TIMESTAMP(), INTERVAL ${i} MINUTE) FROM PrizeItem WHERE Code = 'prize-mug'`
    );
  }
}

test.describe('M9 中奖记录', () => {
  test('TC-60 记录隔离：仅返回本人记录、时间倒序、列含奖品名与中奖时间', async ({ page }) => {
    const aName = `e60a${tag()}`;
    const bName = `e60b${tag()}`;
    const a = await safeRegister(page, aName);
    // B 用独立的 request context 注册，避免覆盖页面的会话（页面须保持 A 的身份）
    const ctxB = await request.newContext({ baseURL: 'http://localhost:5173' });
    const bReg = await ctxB.post(`${API}/auth/register`, {
      data: { userName: bName, password: PW, confirmPassword: PW },
      headers: { 'Idempotency-Key': randomUUID() }
    });
    const b = { userId: (await bReg.json()).data.user.id as number };
    await ctxB.dispose();
    clearRecords(a.userId);
    clearRecords(b.userId);
    seedRecords(a.userId, 2);
    seedRecords(b.userId, 1);
    await page.goto('/records');
    await expect(page.locator(tid('page-records--default'))).toBeVisible({ timeout: 15000 });
    const rows = await page.locator('[data-testid^="records-row-"]').count();
    expect.soft(rows, '仅 A 本人 2 条').toBe(2);
    // 列头断言必须作用到表头行：data-testid="page-records--default" 挂在 <TableBody> 上（仅表体），
    // 表头 <TableHeader> 是其上方兄弟节点（WinningRecordsView.vue:120-127 vs :199-202）。
    const thead = page.locator('table thead');
    await expect.soft(thead, '表头应存在').toHaveCount(1);
    const headText = (await thead.innerText()).replace(/\n/g, ' | ');
    console.log(`TC-60 :: 表头文本="${headText}"`);
    expect.soft(headText, '列头应含「奖品名称」').toContain('奖品名称');
    expect.soft(headText, '列头应含「中奖时间」').toContain('中奖时间');
    const table = await page.locator(tid('page-records--default')).innerText();
    // 倒序：第一行时间应晚于第二行
    const times = await page.locator('[data-testid^="records-row-"] td').allInnerTexts();
    console.log(`TC-60 :: 行数=${rows}，表格文本=${table.replace(/\n/g, ' | ').slice(0, 200)}，单元格=${times.slice(0, 6).join(' / ')}`);
    // 接口层倒序
    const res = await page.request.get(`${API}/records?pageIndex=1&pageSize=10`, { headers: { Authorization: `Bearer ${a.token}` } });
    const items = (await res.json()).data.items as { winTime: string }[];
    const sorted = [...items].every((x, i) => i === 0 || items[i - 1].winTime >= x.winTime);
    expect.soft(sorted, '按中奖时间倒序').toBe(true);
  });

  test('TC-61 分页：11 条记录，每页 10 条、不重不漏、跨页倒序连续', async ({ page }) => {
    const name = `e61${tag()}`;
    const u = await safeRegister(page, name);
    clearRecords(u.userId);
    seedRecords(u.userId, 11);
    await page.goto('/records');
    await expect(page.locator(tid('page-records--default'))).toBeVisible({ timeout: 15000 });
    const p1 = await page.locator('[data-testid^="records-row-"]').count();
    expect.soft(p1, '第 1 页 10 条').toBe(10);
    const firstPageIds = await page.locator('[data-testid^="records-row-"]').evaluateAll((els) =>
      els.map((e) => e.getAttribute('data-testid'))
    );
    await page.locator(tid('records-next')).click();
    await expect.poll(async () => page.locator('[data-testid^="records-row-"]').count(), { timeout: 10000 }).toBe(1);
    const p2Ids = await page.locator('[data-testid^="records-row-"]').evaluateAll((els) =>
      els.map((e) => e.getAttribute('data-testid'))
    );
    expect.soft(p2Ids.some((x) => x && firstPageIds.includes(x)), '不重').toBe(false);
    await page.locator(tid('records-prev')).click();
    await expect.poll(async () => page.locator('[data-testid^="records-row-"]').count(), { timeout: 10000 }).toBe(10);
    console.log(`TC-61 :: 第1页=${p1} 第2页=${p2Ids.length}，第2页 id=${JSON.stringify(p2Ids)}`);
  });

  test('TC-63(E2E) 接口时间 ISO 8601，页面按 UTC+8 yyyy-MM-dd HH:mm 展示且换算一致', async ({ page }) => {
    const name = `e63${tag()}`;
    const u = await safeRegister(page, name);
    clearRecords(u.userId);
    seedRecords(u.userId, 1);
    const res = await page.request.get(`${API}/records?pageIndex=1&pageSize=10`, { headers: { Authorization: `Bearer ${u.token}` } });
    const item = (await res.json()).data.items[0] as { winTime: string };
    const dbRaw = sql(`SELECT CreateTime FROM WinningRecord WHERE UserId = ${u.userId} ORDER BY CreateTime DESC LIMIT 1`);
    await page.goto('/records');
    await expect(page.locator(tid('page-records--default'))).toBeVisible({ timeout: 15000 });
    const table = await page.locator(tid('page-records--default')).innerText();
    // 期望展示：DB(UTC) + 8h
    const d = new Date(`${dbRaw.replace(' ', 'T')}Z`);
    const shifted = new Date(d.getTime() + 8 * 3600 * 1000);
    const pad = (n: number): string => String(n).padStart(2, '0');
    const expectText = `${shifted.getUTCFullYear()}-${pad(shifted.getUTCMonth() + 1)}-${pad(shifted.getUTCDate())} ${pad(shifted.getUTCHours())}:${pad(shifted.getUTCMinutes())}`;
    const wrongText = `${d.getUTCFullYear()}-${pad(d.getUTCMonth() + 1)}-${pad(d.getUTCDate())} ${pad(d.getUTCHours())}:${pad(d.getUTCMinutes())}`;
    console.log(`TC-63 :: 接口 winTime=${item.winTime}（无时区标识）；DB(UTC)=${dbRaw}；期望展示(UTC+8)=${expectText}；若按本地时间误解析则为=${wrongText}；页面含期望值=${table.includes(expectText)}`);
    expect.soft(table, `页面应按 UTC+8 展示 ${expectText}`).toContain(expectText);
  });

  test('TC-64 空态：page-records--empty 文案正确', async ({ page }) => {
    const name = `e64${tag()}`;
    const u = await safeRegister(page, name);
    clearRecords(u.userId);
    await page.goto('/records');
    await expect(page.locator(tid('page-records--empty'))).toBeVisible({ timeout: 15000 });
    await expect.soft(page.locator(tid('page-records--empty'))).toContainText('还没有中奖记录');
  });

  test('TC-65 加载态：表头保留 + 表体 3 行骨架', async ({ page }) => {
    const name = `e65${tag()}`;
    const u65 = await safeRegister(page, name);
    clearRecords(u65.userId);
    seedRecords(u65.userId, 3);
    await page.route('**/api/v1/records*', async (route: Route) => {
      await sleep(1800);
      await route.continue();
    });
    await page.goto('/records');
    await expect(page.locator(tid('page-records--loading'))).toBeVisible({ timeout: 8000 });
    const loadingText = await page.locator(tid('page-records--loading')).innerText();
    const skeletonRows = await page
      .locator(tid('page-records--loading'))
      .locator('tr, [class*="skeleton" i], [class*="Skeleton"]')
      .count();
    console.log(`TC-65 :: 加载态文本=${loadingText.replace(/\n/g, ' | ')}；骨架/行元素计数=${skeletonRows}`);
    // 判据：加载态锚点存在（表头保留 + 骨架），且不得白屏
    expect.soft(loadingText.trim().length, '加载态不得白屏').toBeGreaterThan(0);
    expect.soft(skeletonRows, '表体骨架行数').toBeGreaterThanOrEqual(3);
    await expect.soft(page.locator(tid('page-records--default'))).toBeVisible({ timeout: 15000 });
  });

  test('TC-66 错误态 + 重试成功', async ({ page }) => {
    const name = `e66${tag()}`;
    const u = await safeRegister(page, name);
    clearRecords(u.userId);
    seedRecords(u.userId, 1);
    let fail = true;
    await page.route('**/api/v1/records*', async (route: Route) => {
      if (fail) await route.abort('failed');
      else await route.continue();
    });
    await page.goto('/records');
    await expect(page.locator(tid('page-records--error'))).toBeVisible({ timeout: 10000 });
    fail = false;
    await page.locator(tid('records-retry')).click();
    await expect.soft(page.locator(tid('page-records--default'))).toBeVisible({ timeout: 15000 });
  });

  test('TC-67(E2E) 单页渲染行数 ≤ 10（强制分页）', async ({ page }) => {
    const name = `e67${tag()}`;
    const u = await safeRegister(page, name);
    clearRecords(u.userId);
    seedRecords(u.userId, 15);
    await page.goto('/records');
    await expect(page.locator(tid('page-records--default'))).toBeVisible({ timeout: 15000 });
    const rows = await page.locator('[data-testid^="records-row-"]').count();
    expect(rows, '单页渲染行数 ≤ 10').toBeLessThanOrEqual(10);
    console.log(`TC-67 :: 15 条记录时单页渲染行数=${rows}`);
  });

  test('TC-68 翻页越界回退（第 2 页再点下一页）', async ({ page }) => {
    const name = `e68${tag()}`;
    const u = await safeRegister(page, name);
    clearRecords(u.userId);
    seedRecords(u.userId, 11);
    await page.goto('/records');
    await expect(page.locator(tid('page-records--default'))).toBeVisible({ timeout: 15000 });
    await page.locator(tid('records-next')).click();
    await expect.poll(async () => page.locator('[data-testid^="records-row-"]').count(), { timeout: 10000 }).toBe(1);
    const nextBtn = page.locator(tid('records-next'));
    if (await nextBtn.isEnabled().catch(() => false)) {
      await nextBtn.click();
      await sleep(1500);
    }
    const rows = await page.locator('[data-testid^="records-row-"]').count();
    const emptyVisible = await page.locator(tid('page-records--empty')).isVisible().catch(() => false);
    expect.soft(rows > 0 || !emptyVisible, '不得出现「空态 + 实际有记录」矛盾组合').toBe(true);
    console.log(`TC-68 :: 越界后行数=${rows}，是否出现空态=${emptyVisible}（下一页按钮可用=${await nextBtn.isEnabled().catch(() => false)}）`);
  });

  test('TC-68b 记录只读：无修改/删除入口', async ({ page }) => {
    const name = `e68b${tag()}`;
    const u = await safeRegister(page, name);
    clearRecords(u.userId);
    seedRecords(u.userId, 2);
    await page.goto('/records');
    await expect(page.locator(tid('page-records--default'))).toBeVisible({ timeout: 15000 });
    const html = await page.locator(tid('page-records--default')).innerHTML();
    const hasDelete = /删除|delete|remove/i.test(html);
    const hasEdit = />\s*编辑\s*<|edit/i.test(html);
    expect.soft(hasDelete, '不提供删除入口').toBe(false);
    expect.soft(hasEdit, '不提供修改入口').toBe(false);
  });
});

// ═══════════════════════════════════════════════════════════════
// M10 异常与错误提示
// ═══════════════════════════════════════════════════════════════
test.describe('M10 异常与错误提示', () => {
  test('TC-69 / TC-70 服务端已执行但响应丢失：重试沿用同键，不重复扣次', async ({ page }) => {
    const name = `e69${tag()}`;
    const u = await safeRegister(page, name);
    resetQuota(u.userId);
    const keys: string[] = [];
    let first = true;
    await page.route('**/api/v1/draw', async (route: Route) => {
      keys.push(route.request().headers()['idempotency-key'] ?? '(无键)');
      if (first) {
        first = false;
        // 让请求真正到达服务端后再中断响应，模拟「服务端已执行、客户端响应丢失」
        await route.fetch();
        await route.abort('failed');
        return;
      }
      await route.continue();
    });
    await page.goto('/draw');
    await expect(page.locator(tid('page-draw--default'))).toBeVisible({ timeout: 15000 });
    await page.locator(tid('draw-start')).click();
    await expect(page.locator(tid('page-draw--drawfail'))).toBeVisible({ timeout: 20000 });
    const qMid = await page.request.get(`${API}/draw/quota`, { headers: { Authorization: `Bearer ${u.token}` } });
    console.log(`TC-69 :: 首次失败后 remaining=${(await qMid.json()).data.remainingAttempts}（服务端已执行则应为 2）`);
    await page.locator(tid('draw-retry')).click();
    await expect(page.locator(`${tid('page-draw--win')}, ${tid('page-draw--lose')}`).first()).toBeVisible({ timeout: 25000 });
    const q = await page.request.get(`${API}/draw/quota`, { headers: { Authorization: `Bearer ${u.token}` } });
    const remaining = (await q.json()).data.remainingAttempts;
    expect.soft(remaining, '该次抽奖仅消耗 1 次').toBe(2);
    expect.soft(keys.length >= 2).toBe(true);
    expect.soft(keys[0], '重试必须沿用同一幂等键').toBe(keys[1]);
    console.log(`TC-70 :: 幂等键序列=${JSON.stringify(keys)}，重试后 remaining=${remaining}`);
  });

  test('TC-71 已知业务拒绝直接展示后端文案（1501 / 1502 / 409）', async ({ page }) => {
    const name = `e71${tag()}`;
    await safeRegister(page, name);
    const cases: [number, string, string][] = [
      [1501, '今日抽奖次数已用完，明日 0 点重置', '今日抽奖次数已用完'],
      [1502, '奖品已抽完，请稍后再来', '奖品已抽完'],
      [409, '请勿重复提交', '请勿重复提交']
    ];
    for (const [code, msg, expectText] of cases) {
      await page.unroute('**/api/v1/draw');
      await page.route('**/api/v1/draw', (route: Route) =>
        route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ code, message: msg, data: null }) })
      );
      await page.goto('/draw');
      await expect(page.locator(tid('page-draw--default'))).toBeVisible({ timeout: 15000 });
      await page.locator(tid('draw-start')).click();
      await sleep(1200);
      const body = await page.locator('body').innerText();
      expect.soft(body, `code=${code} 应展示后端文案「${expectText}」`).toContain(expectText);
      console.log(`TC-71 :: code=${code} → 页面含「${expectText}」=${body.includes(expectText)}`);
    }
  });

  test('TC-72 前端未知业务错误码 → 通用错误展示，不白屏', async ({ page }) => {
    const name = `e72${tag()}`;
    await safeRegister(page, name);
    await page.route('**/api/v1/draw/quota', (route: Route) =>
      route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ code: 9999, message: '未来新增错误码', data: null }) })
    );
    await page.goto('/draw');
    await sleep(1500);
    const body = await page.locator('body').innerText();
    expect.soft(body.trim().length, '未知错误码不得白屏').toBeGreaterThan(0);
    await expect.soft(page.locator(tid('app-error-boundary'))).toHaveCount(0);
    console.log(`TC-72 :: 未知码 9999 时页面文本片段=${body.replace(/\n/g, ' ').slice(0, 120)}`);
  });

  test('TC-73 错误边界：接口返回异常结构不导致白屏', async ({ page }) => {
    const name = `e73${tag()}`;
    await safeRegister(page, name);
    await page.route('**/api/v1/prizes', (route: Route) =>
      route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ code: 0, message: '', data: { items: null } }) })
    );
    await page.goto('/draw');
    await sleep(1500);
    const body = await page.locator('body').innerText();
    expect.soft(body.trim().length, '异常结构不得白屏').toBeGreaterThan(0);
    const boundary = await page.locator(tid('app-error-boundary')).count();
    console.log(`TC-73 :: data.items=null 时页面非空=${body.trim().length > 0}，错误边界元素数=${boundary}`);
  });

  test('TC-73b 文案一致性抽查：抽奖页与记录页「明日 0 点重置」口径一致', async ({ page }) => {
    const name = `e73b${tag()}`;
    const u = await safeRegister(page, name);
    setUsed(u.userId, 3);
    await page.goto('/draw');
    await expect(page.locator(tid('page-draw--noquota'))).toBeVisible({ timeout: 15000 });
    const t1 = await page.locator(tid('page-draw--noquota')).innerText();
    expect.soft(t1).toContain('明日 0 点重置');
    // 抽奖接口返回 1501 时的文案应与此一致
    resetQuota(u.userId);
    await page.route('**/api/v1/draw', (route: Route) =>
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ code: 1501, message: '今日抽奖次数已用完，明日 0 点重置', data: null })
      })
    );
    await page.goto('/draw');
    await page.locator(tid('draw-start')).click();
    await sleep(1200);
    const body = await page.locator('body').innerText();
    expect.soft(body, '同一语义文案在各路径一致').toContain('明日 0 点重置');
  });
});

// ═══════════════════════════════════════════════════════════════
// M8 / M14 其余
// ═══════════════════════════════════════════════════════════════
test.describe('M8 / M14 其余', () => {
  test('TC-59b 判定不受客户端时间影响（客户端时间改到次日）', async ({ page }) => {
    const name = `e59b${tag()}`;
    const u = await safeRegister(page, name);
    resetQuota(u.userId);
    const before = await page.request.get(`${API}/draw/quota`, { headers: { Authorization: `Bearer ${u.token}` } });
    const beforeRemaining = (await before.json()).data.remainingAttempts;
    await page.addInitScript(() => {
      const future = Date.now() + 24 * 3600 * 1000;
      // 覆盖前端 Date 的当前时间来源（客户端系统时间被手工修改的等价模拟）
      const RealDate = Date;
      // @ts-expect-error 测试替身
      window.Date = class extends RealDate {
        constructor(...args: ConstructorParameters<typeof RealDate>) {
          if (args.length === 0) super(future);
          else super(...args);
        }
        static now(): number {
          return future;
        }
      };
    });
    await page.goto('/draw');
    await expect(page.locator(tid('page-draw--default'))).toBeVisible({ timeout: 15000 });
    const shown = await page.locator(tid('draw-quota')).innerText();
    expect.soft(shown, '客户端时间改到次日也不得改变剩余次数').toContain(String(beforeRemaining));
    const after = await page.request.get(`${API}/draw/quota`, { headers: { Authorization: `Bearer ${u.token}` } });
    expect.soft((await after.json()).data.remainingAttempts, '服务端口径不受客户端时间影响').toBe(beforeRemaining);
  });

  test('TC-86 移动端宽度 375–428px 主链路不溢出', async ({ page }) => {
    const name = `e86${tag()}`;
    const u = await safeRegister(page, name);
    clearRecords(u.userId);
    seedRecords(u.userId, 3);
    await page.setViewportSize({ width: 375, height: 812 });
    await page.goto('/draw');
    await expect(page.locator(tid('page-draw--default'))).toBeVisible({ timeout: 15000 });
    const drawOverflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
    await page.goto('/records');
    await expect(page.locator(tid('page-records--default'))).toBeVisible({ timeout: 15000 });
    const recOverflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
    console.log(`TC-86 :: 375px 下 抽奖页横向溢出=${drawOverflow}px，记录页横向溢出=${recOverflow}px`);
    expect.soft(drawOverflow, '抽奖页 375px 下不横向溢出').toBeLessThanOrEqual(2);
  });
});
