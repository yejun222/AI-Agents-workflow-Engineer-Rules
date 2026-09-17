/**
 * TC-48c 回归复跑（BUG-04 修复验证）—— 「同前端 + 网络改写」版。
 *
 * 触发：`src/` 变更（CHG-16：BUG-04 前端修复 = 结果条目不在奖池快照时重拉一次 + 兜底文案）
 * + `docs/60-review.md` v5 要求 TC-48c 复跑（`docs/artifacts.md` §5「src/ 变更 → tests/e2e/**」）。
 * 原件 = `tests/e2e/coverage.spec.ts` 的 TC-48c（首轮 14:20 由它暴露 BUG-04）。
 *
 * ═══ 环境偏离（如实登记，不伪造） ═══
 * 原件经 `:5173`（vite，proxy → `:5180`）落到 `luckydraw_dev`。本轮 `:5180` 开发服务器**已不存在**
 * （PID 4308 已终止、端口无监听——本轮以 `tasklist` / `netstat` 实测），且调度指令禁止触碰 `:5180`。
 * 故本文件执行**同一场景**，仅替换后端接入方式：
 *   - 前端仍为 `http://localhost:5173`（vite dev；实测其服务的 `useDrawFlow.ts` / `DrawView.vue`
 *     已含修复代码，见日志的「前端模块探针」行）；
 *   - 页面发出的 `/api/v1/**` 请求经 `page.route` 全部转发到 `TC48C_API_TARGET`
 *     （默认 `http://127.0.0.1:5197`，**修复后二进制**、库 = `luckydraw_dev` —— 与原件同一库）；
 *   - 登录走 UI 表单真实链路（登录请求同样被改写），不复用 `page.request`（绕过 page.route）。
 *
 * ═══ 断言口径（与原件同口径 + 2 条强化，不删弱） ═══
 *   同口径：① 前置：目标条目不在初始奖池快照内；② 不白屏；③ 结果弹层可见（不卡旋转）；
 *          ④ 后端返回条目 = 快照外条目。
 *   强化（BUG-04 的修复点，修复前实测 0 / 空名称）：
 *          ⑤ 抽奖后奖池重拉次数 **== 1**（50 原文「重拉奖池一次再定位」）；
 *          ⑥ 结果弹层文案**含该奖品名称**（重拉后定位成功；修复前为「恭喜获得」后空名称）。
 *
 * 运行（仓库根目录）：node node_modules/@playwright/test/cli.js test tc48c-regression.spec.ts
 * 复位：奖池在测试内 `finally` 复位为 FR-03 默认值；一次性用户不清理（与既有 E2E 口径一致）。
 */
import { test, expect, type Route } from '@playwright/test';
import { execFileSync } from 'node:child_process';
import { randomUUID } from 'node:crypto';

const API_TARGET = process.env.TC48C_API_TARGET ?? 'http://127.0.0.1:5197';
const DB = process.env.TC48C_DB ?? 'luckydraw_dev';
const PW = 'Abcd1234';
const TARGET = 'prize-mug';
const tid = (n: string): string => `[data-testid="${n}"]`;
const tag = (): string => `${Date.now().toString(36)}${Math.floor(Math.random() * 1e6).toString(36)}`.slice(-8);

function sql(statement: string): string {
  try {
    return execFileSync(
      'docker',
      ['exec', 'luckydraw-mysql', 'mysql', '-uroot', '-pdevonly', '-D', DB, '-N', '-B', '-e', statement],
      { encoding: 'utf8', stdio: ['ignore', 'pipe', 'ignore'] }
    ).trim();
  } catch (e) {
    return `ERR:${String((e as Error).message).split('\n')[0].slice(0, 120)}`;
  }
}

/** FR-03 默认奖池复位（与 `coverage.spec.ts` 同口径）。 */
function restorePrizes(): void {
  sql("UPDATE PrizeItem SET Weight = 1,  Stock = 3   WHERE Code = 'prize-keyboard'");
  sql("UPDATE PrizeItem SET Weight = 3,  Stock = 10  WHERE Code = 'prize-earbuds'");
  sql("UPDATE PrizeItem SET Weight = 10, Stock = 50  WHERE Code = 'prize-mug'");
  sql("UPDATE PrizeItem SET Weight = 20, Stock = 200 WHERE Code = 'prize-coupon'");
  sql("UPDATE PrizeItem SET Weight = 66, Stock = 0   WHERE Code = 'no-prize'");
  sql('UPDATE PrizeItem SET IsEnabled = 1, IsDeleted = 0');
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

/** 直连目标实例注册一次性用户（不经 page.request：那条通道绕过 page.route，会打到已下线的 :5180）。 */
async function registerDirect(name: string): Promise<{ userId: number; token: string }> {
  const res = await fetch(`${API_TARGET}/api/v1/auth/register`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', 'Idempotency-Key': randomUUID() },
    body: JSON.stringify({ userName: name, password: PW, confirmPassword: PW })
  });
  const body = (await res.json()) as {
    code?: number;
    data?: { user?: { id?: number }; accessToken?: string };
  };
  if (body?.code !== 0 || !body.data?.accessToken || !body.data?.user?.id) {
    throw new Error(`注册失败 ${name}: http=${res.status} body=${JSON.stringify(body).slice(0, 200)}`);
  }
  return { userId: body.data.user.id, token: body.data.accessToken };
}

test.describe('TC-48c 回归复跑（BUG-04 修复验证，网络改写后端）', () => {
  test('TC-48c 结果条目不在奖池快照时的落点渲染（不白屏、重拉一次、弹层含名称）', async ({ page }) => {
    test.setTimeout(90_000);
    restorePrizes();
    // 前置：目标条目在「页面加载时」处于停用 → 不在前端奖池快照内
    sql(`UPDATE PrizeItem SET IsEnabled = 0 WHERE Code = '${TARGET}'`);

    const name = `c48c${tag()}`;
    const u = await registerDirect(name);
    setUsed(u.userId, 0);

    // 网络改写 + 观测：所有 /api/v1/** → API_TARGET；记录 /prizes 响应（快照）与 /draw 响应体
    const prizesResponses: { at: number; ids: number[] }[] = [];
    let drawBody: { itemId?: number; isWin?: boolean } | null = null;
    await page.route('**/api/v1/**', async (route: Route) => {
      const url = new URL(route.request().url());
      const target = `${API_TARGET}${url.pathname}${url.search}`;
      const resp = await route.fetch({ url: target });
      if (url.pathname.endsWith('/prizes')) {
        const body = (await resp.json().catch(() => null)) as { data?: { items?: { id: number }[] } } | null;
        prizesResponses.push({ at: Date.now(), ids: (body?.data?.items ?? []).map((i) => i.id) });
      }
      if (url.pathname.endsWith('/draw') && route.request().method() === 'POST') {
        const body = (await resp.json().catch(() => null)) as { data?: { itemId?: number; isWin?: boolean } } | null;
        drawBody = body?.data ?? null;
      }
      await route.fulfill({ response: resp });
    });

    // 登录（UI 真实链路）→ SPA 内跳到 /draw → 奖池快照加载（此时目标条目已停用）
    await page.goto('/login');
    await expect(page.locator(tid('page-login--default'))).toBeVisible({ timeout: 15000 });
    await page.fill('#login-username', name);
    await page.fill('#login-password', PW);
    await page.locator('form button[type="submit"]').click();
    await expect(page).toHaveURL(/\/draw$/, { timeout: 15000 });
    await expect(page.locator(tid('page-draw--default'))).toBeVisible({ timeout: 15000 });
    await expect.poll(() => prizesResponses.length, { timeout: 10000 }).toBeGreaterThan(0);

    const targetId = Number(sql(`SELECT Id FROM PrizeItem WHERE Code = '${TARGET}'`));
    const initialIds = prizesResponses[0].ids;
    expect.soft(initialIds, '前置：目标条目不在前端奖池快照内').not.toContain(targetId);

    // 抽奖前：启用目标条目并令其成为唯一可中出候选（其余库存 0、「谢谢参与」权重 0）
    sql(`UPDATE PrizeItem SET IsEnabled = 1, Stock = 5 WHERE Code = '${TARGET}'`);
    sql(`UPDATE PrizeItem SET Stock = 0 WHERE Code <> 'no-prize' AND Code <> '${TARGET}'`);
    sql("UPDATE PrizeItem SET Weight = 0 WHERE Code = 'no-prize'");

    const nBefore = prizesResponses.length;
    await page.locator(tid('draw-start')).click();
    // 期望：不卡在旋转动画上，结果弹层照常呈现
    await expect(page.locator(tid('page-draw--win'))).toBeVisible({ timeout: 25000 });
    const winText = (await page.locator(tid('page-draw--win')).innerText()).replace(/\n/g, ' ');
    const bodyText = (await page.locator('body').innerText()).trim();
    const refetch = prizesResponses.length - nBefore;

    // 目标条目名称（以接口原文为准，避免控制台编码干扰）
    const prizesResp = await fetch(`${API_TARGET}/api/v1/prizes`, {
      headers: { Authorization: `Bearer ${u.token}` }
    });
    const prizesJson = (await prizesResp.json()) as { data?: { items?: { id: number; name: string }[] } };
    const targetName = prizesJson.data?.items?.find((i) => i.id === targetId)?.name ?? '';
    const lastSnapshotIds = prizesResponses.length > 0 ? prizesResponses[prizesResponses.length - 1].ids : [];

    expect.soft(bodyText.length, '不得白屏').toBeGreaterThan(0);
    expect.soft(drawBody?.itemId, '后端返回的条目即快照外条目').toBe(targetId);
    expect.soft(refetch, 'BUG-04 修复口径：抽奖后奖池重拉恰好 1 次（修复前实测 0）').toBe(1);
    expect.soft(lastSnapshotIds, '重拉后的快照应已含目标条目').toContain(targetId);
    expect.soft(winText, `结果弹层应含奖品名称「${targetName}」（修复前为空名称）`).toContain(targetName);

    console.log(
      `TC-48c :: 后端返回 itemId=${drawBody?.itemId ?? '-'}（快照外条目 id=${targetId}）；` +
        `弹层文案="${winText}"；抽奖后奖池重拉次数=${refetch}（期望 1）；` +
        `重拉后快照含目标条目=${lastSnapshotIds.includes(targetId)}；目标条目名称="${targetName}"`
    );
    restorePrizes();
  });
});
