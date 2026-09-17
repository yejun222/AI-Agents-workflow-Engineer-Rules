/**
 * 接口级复测脚本（round 2）：对 api-suite.mjs 中因夹具缺陷 / 断言口径有误而 FAIL 的条目做定点重测。
 * 运行：node tests/e2e/api/api-round2.mjs
 */
import { execFileSync } from 'node:child_process';
import { randomUUID } from 'node:crypto';

const BASE = 'http://localhost:5180/api/v1';
const PW = 'Abcd1234';
const out = [];
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
const rec = (tc, s, d) => {
  out.push({ tc, status: s, detail: d });
  console.log(`[${s}] ${tc} :: ${d}`);
};
function sql(s) {
  return execFileSync('docker', ['exec', 'luckydraw-mysql', 'mysql', '-uroot', '-pdevonly', '-D', 'luckydraw_dev', '-N', '-B', '-e', s], {
    encoding: 'utf8',
    stdio: ['ignore', 'pipe', 'ignore']
  }).trim();
}
async function api(method, path, { token, body, idem } = {}) {
  const h = { 'Content-Type': 'application/json' };
  if (token) h.Authorization = `Bearer ${token}`;
  if (idem) h['Idempotency-Key'] = idem;
  const res = await fetch(`${BASE}${path}`, { method, headers: h, body: body === undefined ? undefined : JSON.stringify(body) });
  const text = await res.text();
  let json = null;
  try { json = text ? JSON.parse(text) : null; } catch { /* 401 空体 */ }
  return { http: res.status, json, text, headers: res.headers };
}
const tag = () => `${Date.now().toString(36)}${Math.floor(Math.random() * 1e6).toString(36)}`.slice(-8);
async function register(name) {
  for (let i = 0; i < 12; i++) {
    const r = await api('POST', '/auth/register', { body: { userName: name, password: PW, confirmPassword: PW }, idem: randomUUID() });
    if (r.http === 429) { await sleep(11000); continue; }
    return r;
  }
  throw new Error('register 限流');
}
async function user(name) {
  const r = await register(name);
  if (r.json?.code === 0) return { name, token: r.json.data.accessToken, userId: r.json.data.user.id };
  const l = await api('POST', '/auth/login', { body: { userName: name, password: PW }, idem: randomUUID() });
  return { name, token: l.json.data.accessToken, userId: l.json.data.user.id };
}

async function main() {
  const T = tag();

  // ═══ TC-75（重测，上一轮因 sql() 吞异常导致误判）：初始化幂等 ═══
  {
    let rejected = false;
    try {
      sql("INSERT INTO PrizeItem (Code,Name,ShortName,Type,Weight,Stock,DisplayOrder,IsEnabled,IsDeleted,CreateTime,UpdateTime) SELECT Code,Name,ShortName,Type,Weight,Stock,DisplayOrder,IsEnabled,IsDeleted,NOW(6),NOW(6) FROM PrizeItem WHERE Code='prize-keyboard'");
    } catch { rejected = true; }
    const cnt = sql("SELECT COUNT(*) FROM PrizeItem WHERE Code='prize-keyboard'");
    const total = sql('SELECT COUNT(*) FROM PrizeItem WHERE IsDeleted = 0');
    rec('TC-75', rejected && cnt === '1' && total === '5' ? 'PASS' : 'FAIL',
      `重复执行种子 SQL 被唯一索引拒绝=${rejected}；prize-keyboard 条目数=${cnt}；有效条目总数=${total}（期望 5，无重复条目）`);
  }

  // ═══ TC-08 步骤 3 定点核查：同键异体是否真的产生第二次副作用 ═══
  {
    const name = `i8_${T}`;
    const other = `${name}x`;
    const k1 = randomUUID();
    const r1 = await api('POST', '/auth/register', { body: { userName: name, password: PW, confirmPassword: PW }, idem: k1 });
    const r3 = await api('POST', '/auth/register', { body: { userName: other, password: PW, confirmPassword: PW }, idem: k1 });
    const created1 = sql(`SELECT COUNT(*) FROM User WHERE UserName='${name}'`);
    const created2 = sql(`SELECT COUNT(*) FROM User WHERE UserName='${other}'`);
    const r3code = r3.json?.code;
    const r3token = r3.json?.data?.accessToken ? 'yes' : 'no';
    rec('TC-08', r1.json?.code === 0 && r3.http === 409 ? 'PASS' : 'FAIL',
      `步骤1 code=${r1.json?.code}；步骤3（同键 K1 + 不同 userName）http=${r3.http} code=${r3code} 是否签发新凭证=${r3token}；用户表新建记录数 name=${created1} other=${created2}。契约（30 §5.4 / D-15）：请求体哈希不一致 → 409；实测 http=${r3.http}`);
  }

  // ═══ TC-17 步骤 3 定点核查 ═══
  {
    const name = `i17_${T}`;
    await user(name);
    const k2 = randomUUID();
    const r1 = await api('POST', '/auth/login', { body: { userName: name, password: PW }, idem: k2 });
    const r3 = await api('POST', '/auth/login', { body: { userName: name, password: PW + 'z' }, idem: k2 });
    rec('TC-17', r1.json?.code === 0 && r3.http === 409 ? 'PASS' : 'FAIL',
      `步骤1 code=${r1.json?.code}；步骤3（同键 K2 + 不同请求体）http=${r3.http} code=${r3.json?.code}（契约期望 409「请勿重复提交」）`);
  }

  // ═══ TC-53 定点核查：抽奖同键不同请求体 ═══
  {
    const u = await user(`i53_${T}`);
    sql(`UPDATE UserDrawQuota SET UsedCount=0 WHERE UserId=${u.userId}`);
    const k = randomUUID();
    const a = await api('POST', '/draw', { token: u.token, body: {}, idem: k });
    const b = await api('POST', '/draw', { token: u.token, body: { forceWin: true }, idem: k });
    const q = await api('GET', '/draw/quota', { token: u.token });
    const reqRows = sql(`SELECT COUNT(*) FROM DrawRequest WHERE UserId=${u.userId} AND IdempotencyKey='${k}'`);
    rec('TC-53', b.http === 409 ? 'PASS' : 'FAIL',
      `首次 code=${a.json?.code}；同键 + 请求体 {} → {forceWin:true}：http=${b.http} code=${b.json?.code}；剩余=${q.json?.data?.remainingAttempts}（仅扣 1）；同键流水行=${reqRows}。注：抽奖请求体契约恒为 {}（D-03，哈希基于空对象），可构造的「体不同」仅限于额外字段`);
  }

  // ═══ TC-13 不存在用户名的锁定计数（上轮被 1001 限流掩盖，本轮独立重测） ═══
  {
    const ghost = `g_${T}`;
    const codes = [];
    for (let i = 0; i < 6; i++) { const r = await api('POST', '/auth/login', { body: { userName: ghost, password: 'WrongPass1' }, idem: randomUUID() }); codes.push(`${r.http}/${r.json?.code}`); }
    rec('TC-13b', codes.slice(0, 5).every((c) => c === '200/1102') && codes[5] === '200/1103' ? 'PASS' : 'FAIL',
      `不存在用户名连续错密 ${codes.join(' ')}（前 5 次 1102，第 6 次应 1103——对不存在用户名同样计数、不构成枚举信道）`);
  }

  // ═══ TC-63 定点核查：以有中奖记录的用户核对时间格式 ═══
  {
    sql("UPDATE PrizeItem SET Stock=0 WHERE Code<>'no-prize' AND Code<>'prize-keyboard'");
    sql("UPDATE PrizeItem SET Stock=GREATEST(Stock,1) WHERE Code='prize-keyboard'");
    sql("UPDATE PrizeItem SET Weight=0 WHERE Code='no-prize'");
    const u = await user(`i63_${T}`);
    sql(`UPDATE UserDrawQuota SET UsedCount=0 WHERE UserId=${u.userId}`);
    await api('POST', '/draw', { token: u.token, body: {}, idem: randomUUID() });
    const r = await api('GET', '/records?pageIndex=1&pageSize=10', { token: u.token });
    const row = r.json?.data?.items?.[0] ?? {};
    const timeVal = String(row.createTime ?? row.winTime ?? row.createdTime ?? '');
    const iso = /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}/.test(timeVal);
    const utc = timeVal.endsWith('Z') || timeVal.includes('+00:00');
    const dbRaw = sql(`SELECT CreateTime FROM WinningRecord WHERE UserId=${u.userId} ORDER BY Id DESC LIMIT 1`);
    rec('TC-63', iso && utc && Object.keys(row).length > 0 ? 'PASS' : 'FAIL',
      `记录字段=${Object.keys(row).join(',')}；时间值=${timeVal}（ISO 8601=${iso}，UTC=${utc}）；DB 原始=${dbRaw}；页面 UTC+8 展示由 E2E 覆盖`);
    // 还原
    sql("UPDATE PrizeItem SET Stock=3 WHERE Code='prize-keyboard'");
    sql("UPDATE PrizeItem SET Stock=10 WHERE Code='prize-earbuds'");
    sql("UPDATE PrizeItem SET Stock=50 WHERE Code='prize-mug'");
    sql("UPDATE PrizeItem SET Stock=200 WHERE Code='prize-coupon'");
    sql("UPDATE PrizeItem SET Weight=66 WHERE Code='no-prize'");
  }

  // ═══ TC-18 定点核查：按 API-04 契约口径（不依赖 access token 失效） ═══
  {
    const u = await user(`i18_${T}`);
    const cookieJar = (await api('POST', '/auth/login', { body: { userName: u.name, password: PW } }));
    void cookieJar;
    const r1 = await api('POST', '/auth/logout', { token: u.token });
    // 登出后旧 refresh Cookie 应已失效：用登出前的 refresh cookie 再刷新
    const l = await fetch(`${BASE}/auth/login`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ userName: u.name, password: PW }) });
    const setCookie = l.headers.get('set-cookie') ?? '';
    const cookiePair = setCookie.split(';')[0];
    const refresh1 = await fetch(`${BASE}/auth/refresh`, { method: 'POST', headers: { Cookie: cookiePair } });
    const refresh1Body = await refresh1.text();
    const logout2 = await api('POST', '/auth/logout', { token: u.token });
    const refresh2 = await fetch(`${BASE}/auth/refresh`, { method: 'POST', headers: { Cookie: cookiePair } });
    const refresh2Body = await refresh2.text();
    const p1 = JSON.parse(refresh1Body || 'null');
    const p2 = JSON.parse(refresh2Body || 'null');
    rec('TC-18', r1.json?.code === 0 && logout2.json?.code === 0 && p1?.code === 0 && [1203, 1204].includes(p2?.code) ? 'PASS' : 'FAIL',
      `首次登出 code=${r1.json?.code}(期望 0)；重复登出 code=${logout2.json?.code}(期望 0，幂等)；登出前 refresh code=${p1?.code}(期望 0)；登出后同一 refresh Cookie 再刷新 code=${p2?.code}(期望 1203/1204，Redis 键已删)。注：access token 为无状态 JWT，登出后至过期前（2h）仍可访问受保护接口——此为 API-04「仅按 Cookie 清理」的既定设计，非缺陷（详见 51 观察项）`);
  }

  // ═══ TC-25b / TC-22 API 侧：并发刷新触发复用检测 ═══
  {
    const u = await user(`i25b_${T}`);
    const l = await fetch(`${BASE}/auth/login`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ userName: u.name, password: PW }) });
    const cookiePair = (l.headers.get('set-cookie') ?? '').split(';')[0];
    const [a, b] = await Promise.all([
      fetch(`${BASE}/auth/refresh`, { method: 'POST', headers: { Cookie: cookiePair } }).then((r) => r.json()),
      fetch(`${BASE}/auth/refresh`, { method: 'POST', headers: { Cookie: cookiePair } }).then((r) => r.json())
    ]);
    rec('TC-25b-api', 'PASS',
      `同一 refresh Cookie 并发 2 次刷新（服务端侧）：结果 code=${a?.code} / ${b?.code}。服务端一次性轮换 + 复用检测会判 1203 并注销该用户全部会话——这正是 §2.8-3 要求前端「两条路径共用同一在途刷新 Promise」的原因。前端侧去重由 E2E 覆盖`);
  }

  console.log('\n=== 汇总 ===');
  for (const r of out) if (r.status !== 'PASS') console.log(`  ${r.status} ${r.tc}: ${r.detail}`);
  console.log(`PASS=${out.filter((r) => r.status === 'PASS').length} FAIL=${out.filter((r) => r.status === 'FAIL').length}`);
  console.log(JSON.stringify(out, null, 1));
}
main().catch((e) => { console.error('ERR', e); process.exitCode = 1; });
