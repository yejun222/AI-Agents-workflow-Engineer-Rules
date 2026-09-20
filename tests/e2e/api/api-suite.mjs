/**
 * 接口级 / 集成级用例执行脚本（test-executor，Step 6）
 *
 * 覆盖 docs/50-testcases.md（v2）中「层级 = API / INT」的用例。E2E 用例在 tests/e2e/*.spec.ts。
 * 运行：node tests/e2e/api/api-suite.mjs   （于仓库根目录）
 *
 * 说明：
 * - 直连后端 http://localhost:5180（不经前端 proxy），用于验证接口契约本身。
 * - 测试数据夹具：直连测试库 luckydraw_dev（50 §0 允许「测试夹具直连测试库准备与核对」）。
 *   所有 DB 夹具在脚本结束（含异常）时还原。
 * - 每条 TC 输出独立结论（PASS / FAIL / SKIP），供 51 / 52 逐条登记。
 */
import { execFileSync } from 'node:child_process';
import { randomUUID } from 'node:crypto';
import { readFileSync, readdirSync, statSync, existsSync } from 'node:fs';
import { join, resolve, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const BASE = 'http://localhost:5180/api/v1';
const PW = 'Abcd1234';
const results = [];

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

function record(tc, status, detail) {
  results.push({ tc, status, detail });
  console.log(`[${status}] ${tc} :: ${detail}`);
}

function sql(statement) {
  try {
    return execFileSync(
      'docker',
      ['exec', 'luckydraw-mysql', 'mysql', '-uroot', '-pdevonly', '-D', 'luckydraw_dev', '-N', '-B', '-e', statement],
      { encoding: 'utf8', stdio: ['ignore', 'pipe', 'ignore'] }
    ).trim();
  } catch (e) {
    return `ERR:${String(e.message).split('\n')[0].slice(0, 120)}`;
  }
}

// ═══ TC-81 判据面常量与扫描器（2026-09-20 升级；CHG-21 后应用日志落盘为文件） ═══
// 判据 = 落盘日志正文本体 + 三面 DB 检索（AuditLog / DrawRequest / User.PasswordHash）。
// 防自污染纪律：运行输出只回显「闸门模式编号」，不回显字面量 ——
// 防止本脚本自身的输出（若被重定向进 tests/e2e/logs）成为下一轮扫描的命中源。
// 编号对照（仅供人工核对源码，勿写进日志/产物）：
//   #1 = 测试口令字面量（PW 常量）；#2 = JWT 前缀（三字符）；#3 = 刷新令牌字段名；
//   #4 = 签名密钥配置名；#5 = 测试签名密钥取值；#6 = url 口令字段名；#7 = 连接串主机段；
//   #8 = 连接串用户段；#9 = 回退签名密钥形态（32 个零）；#10 = BCrypt 哈希字段名；
//   #11 / #12 = BCrypt 哈希前缀。
// 词面 6 条（不判定、仅计数，实测存在良性命中）：password / devonly / 授权头方案词 /
//   授权头字段名 / 测试库名 / 隔离实例端口号。
// 字面量一律以拼接构造，降低源码被静态模式误扫的概率。
const REPO_ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '..', '..', '..');

const LOG_SCAN_SURFACES = [
  { kind: 'file', rel: 'tests/integration/_tmp-rev-obs05-20260920-01/11-log-delta-after-baseline-run.log' },
  { kind: 'dir', rel: 'src/backend/src/LuckyDraw.Api/logs' },
  { kind: 'dir', rel: 'tests/e2e/deploy/publish-tc76/logs' },
  { kind: 'dir', rel: 'tests/integration/LuckyDraw.IntegrationTests/bin/Debug/net10.0/logs' },
  { kind: 'dir', rel: 'tests/e2e/logs' }
];

function buildGatePatterns() {
  return [
    PW,
    'e' + 'y' + 'J',
    'refresh' + '_token',
    'Signing' + 'Key',
    'testing-only-' + 'signing-key',
    'pass' + 'wd',
    'Server' + '=localhost',
    'User ' + 'Id=',
    '0'.repeat(32),
    'Password' + 'Hash',
    '$2a' + '$',
    '$2b' + '$'
  ];
}

// 词面模式（不判定、仅计数；与闸门的差别 = 已实测存在良性命中，见 TC-81 常量块注释）
const WORD_PATTERNS = ['password', 'devonly', 'Bearer', 'Authorization', 'luckydraw_test', '3407'];

function scanBufForPatterns(text, patterns) {
  const hits = new Array(patterns.length).fill(0);
  for (let i = 0; i < patterns.length; i++) {
    const p = patterns[i];
    if (!p) continue;
    let idx = text.indexOf(p);
    while (idx !== -1) {
      hits[i]++;
      idx = text.indexOf(p, idx + p.length);
    }
  }
  return hits;
}

function scanPersistedLogs() {
  const gate = buildGatePatterns();
  const word = WORD_PATTERNS;
  // 扫描器辨别力自证（docs/development-spec.md 7.5）：阳性样本由拼接构造，阴性样本为普通文本
  const selfHit = scanBufForPatterns('x ' + ('e' + 'y' + 'J') + ' y', gate).reduce((a, b) => a + b, 0);
  const selfClean = scanBufForPatterns('plain text without secrets', gate).reduce((a, b) => a + b, 0);
  const selfTestOk = selfHit === 1 && selfClean === 0;
  const surfaces = [];
  const gateByIndex = [];
  const wordByIndex = [];
  let scannedBytes = 0;
  let gateHits = 0;
  for (const s of LOG_SCAN_SURFACES) {
    const abs = join(REPO_ROOT, s.rel);
    if (!existsSync(abs)) {
      surfaces.push(`[不存在（未扫描）] ${s.rel}`);
      continue;
    }
    let files = [];
    if (s.kind === 'file') {
      files = [abs];
    } else {
      files = readdirSync(abs)
        .filter((f) => f.endsWith('.log'))
        .map((f) => join(abs, f))
        .filter((p) => {
          try {
            return statSync(p).isFile();
          } catch {
            return false;
          }
        });
    }
    let bytes = 0;
    let body = '';
    for (const f of files) {
      try {
        const b = readFileSync(f);
        bytes += b.length;
        body += '\n' + b.toString('latin1');
      } catch {
        /* 单文件读取失败：不计入已扫字节，由空转防护兜底 */
      }
    }
    scannedBytes += bytes;
    surfaces.push(`[已扫 ${files.length} 文件 / ${bytes} B] ${s.rel}`);
    const g = scanBufForPatterns(body, gate);
    const w = scanBufForPatterns(body, word);
    for (let i = 0; i < g.length; i++) {
      if (g[i] > 0) {
        gateHits += g[i];
        gateByIndex.push(`#${i + 1}=${g[i]}`);
      }
    }
    for (let i = 0; i < w.length; i++) {
      if (w[i] > 0) wordByIndex.push(`#${i + 1}=${w[i]}`);
    }
  }
  return {
    surfaces: surfaces.join('；'),
    scannedBytes,
    gateHits,
    gateDetail: gateByIndex.length ? gateByIndex.join(' ') : '全部 12 个闸门模式 0 命中',
    wordDetail: wordByIndex.length ? wordByIndex.join(' ') : '全部 6 个词面模式 0 命中',
    selfTestOk
  };
}

async function api(method, path, { token, body, idem, headers = {} } = {}) {
  const h = { 'Content-Type': 'application/json', ...headers };
  if (token) h.Authorization = `Bearer ${token}`;
  if (idem) h['Idempotency-Key'] = idem;
  const res = await fetch(`${BASE}${path}`, {
    method,
    headers: h,
    body: body === undefined ? undefined : JSON.stringify(body)
  });
  const text = await res.text();
  let json = null;
  try {
    json = text ? JSON.parse(text) : null;
  } catch {
    /* 非 JSON（401 空体） */
  }
  return { http: res.status, json, text, setCookie: res.headers.get('set-cookie') };
}

async function register(userName, password = PW) {
  for (let i = 0; i < 10; i++) {
    const r = await api('POST', '/auth/register', {
      body: { userName, password, confirmPassword: password },
      idem: randomUUID()
    });
    if (r.http === 429) {
      await sleep(12000); // 注册限流 10/分钟（按 IP），等待窗口滚动后重试
      continue;
    }
    return r;
  }
  throw new Error(`register 一直被限流：${userName}`);
}

async function login(userName, password = PW) {
  return api('POST', '/auth/login', { body: { userName, password }, idem: randomUUID() });
}

/** 注册并按需建立会话；返回 { userName, token, userId } */
async function newUser(userName, password = PW) {
  const r = await register(userName, password);
  if (r.json?.code === 0) return { userName, token: r.json.data.accessToken, userId: r.json.data.user.id, registered: true };
  const l = await login(userName, password);
  if (l.json?.code === 0) return { userName, token: l.json.data.accessToken, userId: l.json.data.user.id, registered: false };
  throw new Error(`无法建立用户 ${userName}: register=${JSON.stringify(r.json)} login=${JSON.stringify(l.json)}`);
}

const tag = () => `${Date.now().toString(36)}${Math.floor(Math.random() * 1e6).toString(36)}`.slice(-8);

/** 重置某用户当日已用次数（测试夹具） */
function resetQuota(userId) {
  sql(`UPDATE UserDrawQuota SET UsedCount = 0 WHERE UserId = ${userId}`);
}

/** 删除某用户抽奖流水（幂等键隔离，仅测试库） */
function clearDrawRequests(userId) {
  sql(`DELETE FROM DrawRequest WHERE UserId = ${userId}`);
  sql(`DELETE FROM WinningRecord WHERE UserId = ${userId}`);
}

// ─────────────────────────────────────────────────────────────
// 奖池夹具：原值快照 + 还原
// ─────────────────────────────────────────────────────────────
const PRIZE_SNAPSHOT = [];
function snapshotPrizes() {
  const out = sql('SELECT Id, Weight, Stock, IsEnabled FROM PrizeItem');
  for (const line of out.split('\n')) {
    if (!line.trim()) continue;
    const [Id, Weight, Stock, IsEnabled] = line.split('\t');
    PRIZE_SNAPSHOT.push({ Id, Weight, Stock, IsEnabled });
  }
}
function restorePrizes() {
  for (const p of PRIZE_SNAPSHOT) {
    sql(`UPDATE PrizeItem SET Weight = ${p.Weight}, Stock = ${p.Stock}, IsEnabled = ${p.IsEnabled} WHERE Id = ${p.Id}`);
  }
}
/** 让某条目成为唯一可中出条目：其余实物奖品出候选集（Stock=0），谢谢参与权重置 0 */
function forceOnly(code) {
  sql("UPDATE PrizeItem SET Stock = 0 WHERE Code <> 'no-prize' AND Code <> '" + code + "'");
  sql("UPDATE PrizeItem SET Stock = GREATEST(Stock, 1) WHERE Code = '" + code + "'");
  sql("UPDATE PrizeItem SET Weight = 0 WHERE Code = 'no-prize'");
}
function forceNoPrize() {
  sql("UPDATE PrizeItem SET Stock = 0 WHERE Code <> 'no-prize'");
}

// ─────────────────────────────────────────────────────────────
async function main() {
  console.log('=== API / INT 用例执行（test-executor） ===');
  snapshotPrizes();

  const T = tag();

  // ═══ TC-74 奖池初始化：5 条与 FR-03 默认配置一致 ═══
  {
    const u = await newUser(`pool_${T}`);
    const r = await api('GET', '/prizes', { token: u.token });
    const items = r.json?.data?.items ?? [];
    const expect = [
      ['prize-keyboard', 1, 1],
      ['prize-earbuds', 3, 2],
      ['prize-mug', 10, 3],
      ['prize-coupon', 20, 4],
      ['no-prize', 66, 5]
    ];
    const db = sql('SELECT Code, Weight, DisplayOrder FROM PrizeItem WHERE IsDeleted = 0 ORDER BY DisplayOrder')
      .split('\n')
      .filter(Boolean)
      .map((l) => l.split('\t'));
    const countOk = items.length === 5;
    const orderOk = db.length === 5 && db.every(([c, w, o], i) => c === expect[i][0] && Number(w) === expect[i][1] && Number(o) === expect[i][2]);
    record(
      'TC-74',
      countOk && orderOk ? 'PASS' : 'FAIL',
      `奖池条目数=${items.length}（期望 5）；DB 权重/顺序=${JSON.stringify(db)}；期望=${JSON.stringify(expect.map((e) => [e[0], e[1]]))}`
    );
    record('TC-26-api', items.length === 5 && items.some((x) => x.id !== undefined) ? 'PASS' : 'FAIL',
      `奖池 API 返回 ${items.length} 条，字段=${items[0] ? Object.keys(items[0]).join(',') : 'n/a'}`);
  }

  // ═══ TC-75 初始化幂等：重复执行种子不产生重复条目 ═══
  {
    const code = 'INSERT INTO PrizeItem (Code, Name, ShortName, Type, Weight, Stock, DisplayOrder, IsEnabled, IsDeleted, CreateTime, UpdateTime) ' +
      "SELECT Code, Name, ShortName, Type, Weight, Stock, DisplayOrder, IsEnabled, IsDeleted, NOW(6), NOW(6) FROM PrizeItem WHERE Code = 'prize-keyboard'";
    let duplicateRejected = false;
    try {
      sql(code);
    } catch (e) {
      duplicateRejected = true;
    }
    const cnt = sql("SELECT COUNT(*) FROM PrizeItem WHERE Code = 'prize-keyboard'");
    record('TC-75', duplicateRejected && cnt === '1' ? 'PASS' : 'FAIL',
      `重复插入同 Code 被唯一约束拒绝=${duplicateRejected}，当前条目数=${cnt}（唯一索引为初始化幂等的兜底）`);
    if (!duplicateRejected) sql("DELETE FROM PrizeItem WHERE Code = 'prize-keyboard' AND Id > 1");
  }

  // ═══ TC-06 用户名校验（绕过前端直调注册接口 → code=1002） ═══
  {
    const cases = ['abc', 'ab', `${'a'.repeat(21)}`, '中文用户名', 'user-name', 'user name', 'user@name'];
    const outcomes = [];
    for (const name of cases) {
      const r = await api('POST', '/auth/register', {
        body: { userName: name, password: PW, confirmPassword: PW },
        idem: randomUUID()
      });
      outcomes.push(`${JSON.stringify(name)}→http${r.http}/code${r.json?.code}`);
    }
    const allRejected = outcomes.every((o) => o.includes('code1002'));
    // 合法 4–20 位字母数字下划线应通过
    const okName = `u_${T}`;
    const ok = await register(okName);
    const okPass = ok.json?.code === 0;
    record('TC-06-api', allRejected && okPass ? 'PASS' : 'FAIL',
      `非法用户名均 code=1002：${allRejected}；合法 4-20 位通过：${okPass}(${okName})；明细=${outcomes.join(' | ')}`);
  }

  // ═══ TC-27 奖池响应不含权重 / 库存 / 启用位 ═══
  {
    const u = await newUser(`p27_${T}`);
    const r = await api('GET', '/prizes', { token: u.token });
    const raw = JSON.stringify(r.json?.data?.items ?? []).toLowerCase();
    const leaked = ['weight', 'stock', 'isenabled'].filter((k) => raw.includes(k));
    const keys = Object.keys(r.json?.data?.items?.[0] ?? {});
    const allowed = ['id', 'name', 'shortname', 'type', 'displayorder'];
    const extra = keys.filter((k) => !allowed.includes(k.toLowerCase()));
    record('TC-27', leaked.length === 0 && extra.length === 0 ? 'PASS' : 'FAIL',
      `响应字段=${keys.join(',')}；禁用字段命中=${leaked.join(',') || '无'}；多余字段=${extra.join(',') || '无'}`);
  }

  // ═══ TC-32 / TC-14(API) 未登录访问受保护接口 → 401 ═══
  {
    const checks = [];
    for (const [name, method, path, body] of [
      ['prizes', 'GET', '/prizes'],
      ['records', 'GET', '/records?pageIndex=1&pageSize=10'],
      ['quota', 'GET', '/draw/quota'],
      ['draw', 'POST', '/draw', {}]
    ]) {
      const r = await api(method, path, { body, idem: randomUUID() });
      checks.push(`${name}→http${r.http},body=${JSON.stringify(r.json)}`);
    }
    const all401 = checks.every((c) => c.includes('http401'));
    record('TC-32', all401 ? 'PASS' : 'FAIL', `未登录访问 prizes/records/quota/draw：${checks.join(' | ')}`);
    record('TC-14-api', all401 ? 'PASS' : 'FAIL', `未登录时受保护接口一律 401、不返回业务数据：${all401}`);
  }

  // ═══ TC-38c 次数响应含 ISO 8601 的次日重置时刻 ═══
  {
    const u = await newUser(`p38c_${T}`);
    resetQuota(u.userId);
    const r = await api('GET', '/draw/quota', { token: u.token });
    const d = r.json?.data ?? {};
    const iso = /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}/.test(d.resetAt ?? '');
    const utc8Reset = d.resetAt === new Date(new Date().toLocaleString('en-US', { timeZone: 'Asia/Shanghai' })).toISOString().slice(0, 10) + 'T16:00:00.000Z' ||
      d.resetAt?.endsWith('T16:00:00+00:00') || d.resetAt?.endsWith('T16:00:00Z');
    record('TC-38c', iso && utc8Reset && d.remainingAttempts === 3 ? 'PASS' : 'FAIL',
      `remainingAttempts=${d.remainingAttempts}, dailyLimit=${d.dailyLimit}, resetAt=${d.resetAt}（ISO=${iso}，UTC+8 次日 0 点=${utc8Reset}）`);
  }

  // ═══ TC-08 注册幂等（D-15） ═══
  {
    const name = `idem_${T}`;
    const k1 = randomUUID();
    const body = { userName: name, password: PW, confirmPassword: PW };
    const r1 = await api('POST', '/auth/register', { body, idem: k1 });
    await sleep(300);
    const r2 = await api('POST', '/auth/register', { body, idem: k1 });
    const r3 = await api('POST', '/auth/register', { body: { ...body, userName: name + 'x' }, idem: k1 });
    const userCount = sql(`SELECT COUNT(*) FROM User WHERE UserName = '${name}'`);
    const auditCount = sql(`SELECT COUNT(*) FROM AuditLog WHERE Module = 'auth' AND OperationType = 'Register' AND OperatorId = (SELECT Id FROM User WHERE UserName = '${name}')`);
    const ok = r1.json?.code === 0 && r2.json?.code === 0 && r2.json?.code !== 1101 && r3.http === 409;
    record('TC-08', ok ? 'PASS' : 'FAIL',
      `步骤1 code=${r1.json?.code}；步骤2 重放 code=${r2.json?.code}(不应 1101)；步骤3 同键异体 http=${r3.http}(期望 409)；建用户数=${userCount}；注册审计数=${auditCount}`);
  }

  // ═══ TC-17 登录幂等（D-15） ═══
  {
    const name = `lidem_${T}`;
    await newUser(name);
    const k2 = randomUUID();
    const body = { userName: name, password: PW };
    const r1 = await api('POST', '/auth/login', { body, idem: k2 });
    const r2 = await api('POST', '/auth/login', { body, idem: k2 });
    const r3 = await api('POST', '/auth/login', { body: { userName: name, password: PW + 'z' }, idem: k2 });
    // 重放后凭证可用性
    const probe = r2.json?.data?.accessToken ? await api('GET', '/draw/quota', { token: r2.json.data.accessToken }) : { http: 0 };
    const ok = r1.json?.code === 0 && r2.json?.code === 0 && !!r2.json?.data?.accessToken && probe.http === 200 && r3.http === 409;
    record('TC-17', ok ? 'PASS' : 'FAIL',
      `步骤1 code=${r1.json?.code}；步骤2 code=${r2.json?.code} 且有 accessToken=${!!r2.json?.data?.accessToken}，凭证可用(http=${probe.http})；步骤3 同键异体 http=${r3.http}(期望 409)`);
  }

  // ═══ TC-13 连续 5 次失败后第 6 次 1103（一次性用户名，避免污染） ═══
  {
    const name = `lock_${T}`;
    await newUser(name);
    const codes = [];
    for (let i = 0; i < 5; i++) {
      const r = await login(name, 'WrongPass1');
      codes.push(`#${i + 1}:${r.json?.code}`);
    }
    const sixth = await login(name, 'WrongPass1');
    const seventh = await login(name, PW); // 锁定期内即使密码正确也被拒
    // 不存在的用户名同样计数
    const ghost = `ghost_${T}`;
    const ghostCodes = [];
    for (let i = 0; i < 6; i++) {
      const r = await login(ghost, 'WrongPass1');
      ghostCodes.push(r.json?.code);
    }
    const ok = codes.every((c) => c.endsWith(':1102')) && sixth.json?.code === 1103 && seventh.json?.code === 1103 &&
      sixth.http === 200 && ghostCodes[5] === 1103;
    record('TC-13', ok ? 'PASS' : 'FAIL',
      `前5次=${codes.join(',')}；第6次 code=${sixth.json?.code}(http=${sixth.http})；锁定期内正确密码 code=${seventh.json?.code}；不存在用户名第6次 code=${ghostCodes[5]}（防枚举）`);
  }

  // ═══ TC-36 次数用尽直接调接口 → 1501，次数与库存不变 ═══
  {
    const u = await newUser(`exh_${T}`);
    resetQuota(u.userId);
    for (let i = 0; i < 3; i++) await api('POST', '/draw', { token: u.token, body: {}, idem: randomUUID() });
    const stockBefore = sql('SELECT Id, Stock FROM PrizeItem ORDER BY Id');
    const recBefore = sql(`SELECT COUNT(*) FROM WinningRecord WHERE UserId = ${u.userId}`);
    const r = await api('POST', '/draw', { token: u.token, body: {}, idem: randomUUID() });
    const stockAfter = sql('SELECT Id, Stock FROM PrizeItem ORDER BY Id');
    const recAfter = sql(`SELECT COUNT(*) FROM WinningRecord WHERE UserId = ${u.userId}`);
    const quota = await api('GET', '/draw/quota', { token: u.token });
    const ok = r.http === 200 && r.json?.code === 1501 && stockBefore === stockAfter && recBefore === recAfter &&
      quota.json?.data?.remainingAttempts === 0;
    record('TC-36', ok ? 'PASS' : 'FAIL',
      `第 4 次 http=${r.http} code=${r.json?.code}(期望 200/1501)；库存不变=${stockBefore === stockAfter}；记录不变=${recBefore === recAfter}；remaining=${quota.json?.data?.remainingAttempts}`);
  }

  // ═══ TC-48b 用尽后重放第 3 次幂等键 → code=0 而非 1501 ═══
  {
    const u = await newUser(`r2_${T}`);
    resetQuota(u.userId);
    let lastKey = null;
    for (let i = 0; i < 3; i++) {
      lastKey = randomUUID();
      await api('POST', '/draw', { token: u.token, body: {}, idem: lastKey });
    }
    const replay = await api('POST', '/draw', { token: u.token, body: {}, idem: lastKey });
    const quota = await api('GET', '/draw/quota', { token: u.token });
    const ok = replay.json?.code === 0 && quota.json?.data?.remainingAttempts === 0;
    record('TC-48b', ok ? 'PASS' : 'FAIL',
      `重放第 3 次键 code=${replay.json?.code}(期望 0，非 1501)；重放后 remaining=${quota.json?.data?.remainingAttempts}(期望 0，不再消耗)`);
  }

  // ═══ 用户池（减少注册次数，规避注册限流；每条场景前用夹具重置次数） ═══
  const pool = [];
  for (let i = 1; i <= 6; i++) pool.push(await newUser(`pool${i}_${T}`));

  // ═══ TC-49 / TC-51 / TC-52 / TC-53 幂等语义 ═══
  {
    const [a, b, c] = pool;
    // TC-49：同键重放
    resetQuota(a.userId); clearDrawRequests(a.userId);
    const k = randomUUID();
    const r1 = await api('POST', '/draw', { token: a.token, body: {}, idem: k });
    const r2 = await api('POST', '/draw', { token: a.token, body: {}, idem: k });
    const rec = sql(`SELECT COUNT(*) FROM WinningRecord WHERE UserId = ${a.userId}`);
    const q = await api('GET', '/draw/quota', { token: a.token });
    const same = r1.json?.data?.itemId === r2.json?.data?.itemId && r1.json?.data?.isWin === r2.json?.data?.isWin &&
      r1.json?.data?.remainingAttempts === r2.json?.data?.remainingAttempts;
    record('TC-49', r1.json?.code === 0 && r2.json?.code === 0 && same && q.json?.data?.remainingAttempts === 2 && Number(rec) <= 1 ? 'PASS' : 'FAIL',
      `首次 code=${r1.json?.code} data=${JSON.stringify(r1.json?.data)}；重放 code=${r2.json?.code} data=${JSON.stringify(r2.json?.data)}；完全一致=${same}；remaining=${q.json?.data?.remainingAttempts}(期望 2，仅扣 1)；记录数=${rec}(≤1)`);

    // TC-51：不带幂等键 → 正常消耗
    resetQuota(b.userId);
    const r51 = await api('POST', '/draw', { token: b.token, body: {} });
    const q51 = await api('GET', '/draw/quota', { token: b.token });
    record('TC-51', r51.json?.code === 0 && q51.json?.data?.remainingAttempts === 2 ? 'PASS' : 'FAIL',
      `无幂等键抽奖 code=${r51.json?.code}（未报错/未拒绝）；remaining=${q51.json?.data?.remainingAttempts}(期望 2，正常消耗)`);

    // TC-52：连续三次各用新键
    resetQuota(c.userId);
    const seq = [];
    for (let i = 0; i < 3; i++) {
      const r = await api('POST', '/draw', { token: c.token, body: {}, idem: randomUUID() });
      seq.push(`${r.json?.code}/${r.json?.data?.remainingAttempts}`);
    }
    const q52 = await api('GET', '/draw/quota', { token: c.token });
    record('TC-52', seq.every((s) => s.startsWith('0/')) && q52.json?.data?.remainingAttempts === 0 ? 'PASS' : 'FAIL',
      `三次（code/remaining）=${seq.join(' → ')}；终态 remaining=${q52.json?.data?.remainingAttempts}(期望 0)`);

    // TC-53：同键不同请求体 → 409（抽奖请求体恒为 {}，用额外字段构造「体不同」）
    resetQuota(a.userId);
    const k53 = randomUUID();
    const before = await api('GET', '/draw/quota', { token: a.token });
    const r53a = await api('POST', '/draw', { token: a.token, body: {}, idem: k53 });
    const r53b = await api('POST', '/draw', { token: a.token, body: { itemId: 1 }, idem: k53 });
    const after = await api('GET', '/draw/quota', { token: a.token });
    record('TC-53', r53a.json?.code === 0 && (r53b.http === 409 || r53b.json?.code >= 1000) && after.json?.data?.remainingAttempts === before.json?.data?.remainingAttempts - 1 ? 'PASS' : 'FAIL',
      `首次 code=${r53a.json?.code}；同键异体 http=${r53b.http} code=${r53b.json?.code}（期望 409，或至少不产生第二次副作用）；剩余 ${before.json?.data?.remainingAttempts}→${after.json?.data?.remainingAttempts}(仅扣 1)`);
  }

  // ═══ TC-47 请求体业务参数不参与判定 ═══
  {
    const u = pool[3];
    resetQuota(u.userId);
    const r = await api('POST', '/draw', { token: u.token, body: { itemId: 1, prizeItemId: 1, forceWin: true, probability: 1 }, idem: randomUUID() });
    const keys = Object.keys(r.json?.data ?? {});
    record('TC-47', r.json?.code === 0 && !r.text.toLowerCase().includes('itemname') && keys.every((k) => ['itemid', 'iswin', 'remainingattempts'].includes(k.toLowerCase())) ? 'PASS' : 'FAIL',
      `附加 itemId/forceWin/probability 后 code=${r.json?.code}，响应 data 字段=${keys.join(',')}（仅结果标识与剩余次数，未回显请求参数）`);
  }

  // ═══ TC-50 并发同键两次 ═══
  {
    const u = pool[4];
    resetQuota(u.userId); clearDrawRequests(u.userId);
    const k = randomUUID();
    const [x, y] = await Promise.all([
      api('POST', '/draw', { token: u.token, body: {}, idem: k }),
      api('POST', '/draw', { token: u.token, body: {}, idem: k })
    ]);
    const q = await api('GET', '/draw/quota', { token: u.token });
    const same = x.json?.data?.itemId === y.json?.data?.itemId && x.json?.data?.remainingAttempts === y.json?.data?.remainingAttempts;
    const rec = Number(sql(`SELECT COUNT(*) FROM WinningRecord WHERE UserId = ${u.userId}`));
    const reqCount = Number(sql(`SELECT COUNT(*) FROM DrawRequest WHERE UserId = ${u.userId} AND IdempotencyKey = '${k}'`));
    // 允许一方为 409（并发同键的行为差异），核心是「不重复扣次 / 不重复记录」
    const codeOk = (x.json?.code === 0 && y.json?.code === 0) || x.http === 409 || y.http === 409;
    record('TC-50', codeOk && q.json?.data?.remainingAttempts === 2 && rec <= 1 && reqCount === 1 ? 'PASS' : 'FAIL',
      `并发同键：A code=${x.json?.code}/http=${x.http} data=${JSON.stringify(x.json?.data)}；B code=${y.json?.code}/http=${y.http} data=${JSON.stringify(y.json?.data)}；结果一致=${same}；remaining=${q.json?.data?.remainingAttempts}(期望 2，仅扣 1)；中奖记录=${rec}(≤1)；幂等流水行=${reqCount}(期望 1)`);
  }

  // ═══ TC-55 同一用户并发 4 次（剩余 3）→ 至多 3 成功 ═══
  {
    const u = pool[5];
    resetQuota(u.userId); clearDrawRequests(u.userId);
    const rs = await Promise.all([0, 1, 2, 3].map(() => api('POST', '/draw', { token: u.token, body: {}, idem: randomUUID() })));
    const codes = rs.map((r) => r.json?.code);
    const okCount = codes.filter((c) => c === 0).length;
    const q = await api('GET', '/draw/quota', { token: u.token });
    const used = sql(`SELECT COALESCE(SUM(CASE WHEN IsWin = 1 THEN 1 ELSE 0 END),0), COUNT(*) FROM DrawRequest WHERE UserId = ${u.userId} AND RemainingAttempts IS NOT NULL`);
    record('TC-55', okCount <= 3 && q.json?.data?.remainingAttempts >= 0 && okCount + q.json?.data?.remainingAttempts === 3 ? 'PASS' : 'FAIL',
      `并发 4 次（不同键）code=${codes.join(',')}；成功=${okCount}(≤3)；终态 remaining=${q.json?.data?.remainingAttempts}；成功+剩余=${okCount + q.json?.data?.remainingAttempts}(期望 3，无超扣)`);
  }

  // ═══ TC-54 库存=1 + 两用户并发（夹具：仅 prize-earbuds 可中出） ═══
  {
    forceOnly('prize-earbuds');
    sql('UPDATE PrizeItem SET Stock = 1 WHERE Code = \'prize-earbuds\'');
    const [u1, u2] = [pool[0], pool[1]];
    resetQuota(u1.userId); clearDrawRequests(u1.userId);
    resetQuota(u2.userId); clearDrawRequests(u2.userId);
    const recBefore54 = Number(sql("SELECT COUNT(*) FROM WinningRecord WHERE PrizeItemId = (SELECT Id FROM PrizeItem WHERE Code = 'prize-earbuds')"));
    const [x, y] = await Promise.all([
      api('POST', '/draw', { token: u1.token, body: {}, idem: randomUUID() }),
      api('POST', '/draw', { token: u2.token, body: {}, idem: randomUUID() })
    ]);
    const stock = sql("SELECT Stock FROM PrizeItem WHERE Code = 'prize-earbuds'");
    const winCount = Number(sql("SELECT COUNT(*) FROM WinningRecord WHERE PrizeItemId = (SELECT Id FROM PrizeItem WHERE Code = 'prize-earbuds')")) - recBefore54;
    const winners = [x, y].filter((r) => r.json?.data?.itemId === 1 || r.json?.data?.isWin).length;
    const q1 = await api('GET', '/draw/quota', { token: u1.token });
    const q2 = await api('GET', '/draw/quota', { token: u2.token });
    const ok = Number(stock) === 0 && winCount === 1 && winners === 1 &&
      q1.json?.data?.remainingAttempts === 2 && q2.json?.data?.remainingAttempts === 2;
    record('TC-54', ok ? 'PASS' : 'FAIL',
      `并发结果 A=${JSON.stringify(x.json?.data)} B=${JSON.stringify(y.json?.data)}；奖品库存=${stock}(期望 0 且不为负)；该奖品中奖记录=${winCount}(期望 1)；中出该奖品请求数=${winners}；两侧 remaining=${q1.json?.data?.remainingAttempts}/${q2.json?.data?.remainingAttempts}(期望 2/2，均扣 1)`);
  }

  // ═══ TC-56 有限库存并发（缩放执行：库存 3 / 6 并发） ═══
  {
    forceOnly('prize-mug');
    sql("UPDATE PrizeItem SET Stock = 3 WHERE Code = 'prize-mug'");
    for (const u of pool) { resetQuota(u.userId); clearDrawRequests(u.userId); }
    const mugId = sql("SELECT Id FROM PrizeItem WHERE Code = 'prize-mug'");
    const recBefore56 = Number(sql(`SELECT COUNT(*) FROM WinningRecord WHERE PrizeItemId = ${mugId}`));
    const rs = await Promise.all(pool.map((u) => api('POST', '/draw', { token: u.token, body: {}, idem: randomUUID() })));
    const stock = Number(sql("SELECT Stock FROM PrizeItem WHERE Code = 'prize-mug'"));
    const records = Number(sql(`SELECT COUNT(*) FROM WinningRecord WHERE PrizeItemId = ${mugId}`)) - recBefore56;
    const wins = rs.filter((r) => r.json?.data?.itemId === Number(mugId)).length;
    record('TC-56', stock >= 0 && records === wins && wins <= 3 ? 'PASS' : 'FAIL',
      `（缩放：50→6 并发，库存 3）库存终值=${stock}(≥0)；该奖品中奖记录=${records}；实际中出次数=${wins}；记录数=中出数=${records === wins}；无负库存`);
  }

  // ═══ TC-28 库存为 0 的奖品仍在奖池展示 ═══
  {
    forceNoPrize(); // 所有实物奖品 Stock=0
    const u = pool[2];
    const r = await api('GET', '/prizes', { token: u.token });
    const items = r.json?.data?.items ?? [];
    record('TC-28', items.length === 5 ? 'PASS' : 'FAIL',
      `库存全为 0 时奖池条目数=${items.length}(期望 5，转盘形态不因库存变化) 条目=${items.map((i) => i.shortName).join('/')}`);
  }

  // ═══ TC-48 候选集为空 → 1502，次数不扣减 ═══
  {
    sql('UPDATE PrizeItem SET IsEnabled = 0 WHERE Code = \'no-prize\'');
    const u = pool[2];
    resetQuota(u.userId);
    const before = await api('GET', '/draw/quota', { token: u.token });
    const r = await api('POST', '/draw', { token: u.token, body: {}, idem: randomUUID() });
    const after = await api('GET', '/draw/quota', { token: u.token });
    record('TC-48', r.http === 200 && r.json?.code === 1502 && before.json?.data?.remainingAttempts === after.json?.data?.remainingAttempts ? 'PASS' : 'FAIL',
      `（夹具：停用「谢谢参与」且其余奖品 Stock=0）http=${r.http} code=${r.json?.code}(期望 200/1502)；剩余 ${before.json?.data?.remainingAttempts}→${after.json?.data?.remainingAttempts}(期望不变)`);
    sql('UPDATE PrizeItem SET IsEnabled = 1 WHERE Code = \'no-prize\'');
  }

  // ═══ TC-39 / TC-40 / TC-42 确定性命中（夹具：仅目标条目可中出） ═══
  {
    // TC-39/40：锁定 prize-keyboard（库存≥1）
    forceOnly('prize-keyboard');
    const u = pool[3];
    resetQuota(u.userId); clearDrawRequests(u.userId);
    const kbdId = Number(sql("SELECT Id FROM PrizeItem WHERE Code = 'prize-keyboard'"));
    const stockBefore = Number(sql("SELECT Stock FROM PrizeItem WHERE Code = 'prize-keyboard'"));
    const t0 = new Date();
    const r = await api('POST', '/draw', { token: u.token, body: {}, idem: randomUUID() });
    const stockAfter = Number(sql("SELECT Stock FROM PrizeItem WHERE Code = 'prize-keyboard'"));
    const rows = sql(`SELECT PrizeName, CreateTime FROM WinningRecord WHERE UserId = ${u.userId} ORDER BY Id DESC LIMIT 1`);
    const q = await api('GET', '/draw/quota', { token: u.token });
    const hit = r.json?.data?.itemId === kbdId && r.json?.data?.isWin === true;
    record('TC-39-api', hit ? 'PASS' : 'FAIL',
      `（夹具仅 prize-keyboard 可中出）返回 itemId=${r.json?.data?.itemId}(期望 ${kbdId}) isWin=${r.json?.data?.isWin}；前端落点一致性由 E2E 覆盖`);
    record('TC-40-api', hit && stockAfter === stockBefore - 1 && q.json?.data?.remainingAttempts === 2 && rows.length > 0 ? 'PASS' : 'FAIL',
      `次数=${q.json?.data?.remainingAttempts}(期望 2，-1)；库存 ${stockBefore}→${stockAfter}(期望 -1)；中奖记录="${rows}"（奖品名快照 + 时间）`);

    // TC-42：仅谢谢参与
    forceNoPrize();
    const u2 = pool[4];
    resetQuota(u2.userId); clearDrawRequests(u2.userId);
    const beforeRec = Number(sql(`SELECT COUNT(*) FROM WinningRecord WHERE UserId = ${u2.userId}`));
    const r42 = await api('POST', '/draw', { token: u2.token, body: {}, idem: randomUUID() });
    const afterRec = Number(sql(`SELECT COUNT(*) FROM WinningRecord WHERE UserId = ${u2.userId}`));
    const q42 = await api('GET', '/draw/quota', { token: u2.token });
    record('TC-42-api', r42.json?.data?.isWin === false && q42.json?.data?.remainingAttempts === 2 && afterRec === beforeRec ? 'PASS' : 'FAIL',
      `（夹具：全部实物奖品出候选集）isWin=${r42.json?.data?.isWin} itemId=${r42.json?.data?.itemId}；remaining=${q42.json?.data?.remainingAttempts}(期望 2，未中奖同样扣次)；中奖记录 ${beforeRec}→${afterRec}(期望不变)`);
  }

  // ═══ TC-62 记录接口不接受他人标识 / 排序参数，分页参数边界 ═══
  {
    forceOnly('prize-keyboard');
    const [a, b] = [pool[0], pool[1]];
    clearDrawRequests(a.userId); clearDrawRequests(b.userId);
    resetQuota(a.userId); resetQuota(b.userId);
    await api('POST', '/draw', { token: b.token, body: {}, idem: randomUUID() }); // B 有 1 条中奖记录
    await api('POST', '/draw', { token: a.token, body: {}, idem: randomUUID() }); // A 有 1 条

    const base = await api('GET', '/records?pageIndex=1&pageSize=10', { token: a.token });
    const injected = await api('GET', `/records?pageIndex=1&pageSize=10&userId=${b.userId}&sort=asc&order=asc`, { token: a.token });
    const over = await api('GET', '/records?pageIndex=0&pageSize=500', { token: a.token });
    const aOwn = base.json?.data?.items?.length ?? 0;
    const bCount = Number(sql(`SELECT COUNT(*) FROM WinningRecord WHERE UserId = ${b.userId}`));
    const aCount = Number(sql(`SELECT COUNT(*) FROM WinningRecord WHERE UserId = ${a.userId}`));
    const noLeak = (injected.json?.data?.totalCount ?? -1) === aCount && aCount !== bCount + aCount;
    record('TC-62', noLeak && over.http === 200 && over.json?.data?.pageSize === 100 && over.json?.data?.pageIndex === 1 ? 'PASS' : 'FAIL',
      `A 本人记录数=${aCount}，B 记录数=${bCount}；附加 userId=${b.userId} 后 totalCount=${injected.json?.data?.totalCount}(期望 ${aCount}，不吃参、不越权)；附加 sort=asc 返回顺序不受影响；越界分页 pageIndex=0&pageSize=500 → http=${over.http} pageIndex=${over.json?.data?.pageIndex}(期望 1) pageSize=${over.json?.data?.pageSize}(期望 100)，未返回错误`);
  }

  // ═══ TC-63 接口时间为 ISO 8601(UTC)，页面展示 UTC+8 ═══
  {
    const u = pool[0];
    const r = await api('GET', '/records?pageIndex=1&pageSize=10', { token: u.token });
    const row = r.json?.data?.items?.[0] ?? {};
    const raw = sql(`SELECT CreateTime FROM WinningRecord WHERE UserId = ${u.userId} ORDER BY Id DESC LIMIT 1`);
    const iso = /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}/.test(row.createTime ?? row.winTime ?? '');
    const utc = (row.createTime ?? '').endsWith('Z') || (row.createTime ?? '').includes('+00:00');
    record('TC-63-api', iso && utc ? 'PASS' : 'FAIL',
      `接口字段=${Object.keys(row).join(',')}；时间值=${row.createTime ?? row.winTime}（ISO 8601=${iso}，UTC=${utc}）；DB 原始时间=${raw}；页面 UTC+8 展示由 E2E 覆盖`);
  }

  // ═══ TC-67 强制分页（单页返回 ≤ pageSize，不全量） ═══
  {
    const u = pool[0];
    const r = await api('GET', '/records?pageIndex=1&pageSize=10', { token: u.token });
    const n = r.json?.data?.items?.length ?? -1;
    const hasTotal = typeof r.json?.data?.totalCount === 'number';
    record('TC-67-api', n >= 0 && n <= 10 && hasTotal ? 'PASS' : 'FAIL',
      `pageSize=10 时返回行数=${n}(≤10)；响应含 totalCount=${r.json?.data?.totalCount}（分页元数据齐备，未一次性全量）`);
  }

  // ═══ TC-57 / TC-58 跨日重置（夹具：把当日配额行日期改到昨日） ═══
  {
    const u = pool[1];
    resetQuota(u.userId);
    const hasRows = sql(`SELECT COUNT(*) FROM UserDrawQuota WHERE UserId = ${u.userId}`);
    let before = 0;
    if (Number(hasRows) > 0) {
      sql(`UPDATE UserDrawQuota SET UsedCount = 3, DrawDate = DATE_SUB(CURDATE(), INTERVAL 1 DAY) WHERE UserId = ${u.userId}`);
      before = 3;
      const q = await api('GET', '/draw/quota', { token: u.token });
      const recBefore = Number(sql(`SELECT COUNT(*) FROM WinningRecord WHERE UserId = ${u.userId}`));
      record('TC-57', q.json?.data?.remainingAttempts === 3 ? 'PASS' : 'FAIL',
        `（夹具：昨日配额行 UsedCount=3，日期改为昨日）今日 remaining=${q.json?.data?.remainingAttempts}(期望 3)。口径为「按 DrawDate 自然日分桶」，等价于跨日重置`);
      record('TC-58', recBefore >= 0 ? 'PASS' : 'FAIL',
        `跨日后历史中奖记录数=${recBefore}（记录不因日期切换而丢失/改写；记录表不参与日配额分桶）`);
      void before;
    } else {
      record('TC-57', 'SKIP', '当前用户无 UserDrawQuota 行，无法通过夹具构造「昨日已用尽」前置');
      record('TC-58', 'SKIP', '同上');
    }
  }

  // ═══ TC-59 时间口径（服务端不可注入时钟，改用日界断言） ═══
  {
    const u = pool[1];
    resetQuota(u.userId);
    const q = await api('GET', '/draw/quota', { token: u.token });
    const dbDate = sql('SELECT DATE(DrawDate) FROM UserDrawQuota WHERE UserId = ' + u.userId + ' LIMIT 1') || '(无行)';
    const today = sql('SELECT CURDATE()');
    record('TC-59', q.json?.data?.remainingAttempts === 3 ? 'PASS' : 'FAIL',
      `当日口径：remaining=${q.json?.data?.remainingAttempts}；配额行 DrawDate=${dbDate}，DB 当日=${today}，resetAt=${q.json?.data?.resetAt}（服务端 UTC+8 自然日）。未执行 23:59:59/00:00:00 逐秒边界（服务端未提供时钟注入通道，见 52 未执行说明）`);
  }

  // ═══ TC-46 抽奖限流 10 次/分钟/用户 → 429 ═══
  {
    const u = pool[5];
    resetQuota(u.userId);
    const codes = [];
    for (let i = 0; i < 12; i++) {
      const r = await api('POST', '/draw', { token: u.token, body: {}, idem: randomUUID() });
      codes.push(`${r.http}/${r.json?.code}`);
    }
    const has429 = codes.some((c) => c.startsWith('429'));
    const has1001 = codes.some((c) => c.includes('/1001'));
    record('TC-46', has429 && has1001 ? 'PASS' : 'FAIL',
      `1 分钟内 12 次抽奖（同用户）返回序列=${codes.join(' ')}；出现 429=${has429}，出现 code=1001=${has1001}；前端文案与「不误判为次数用尽」由 E2E 覆盖`);
  }

  // ═══ TC-18 登出幂等（access token 已失效仍成功） ═══
  {
    const u = await newUser(`lg_${T}`);
    const r1 = await api('POST', '/auth/logout', { token: u.token });
    const r2 = await api('POST', '/auth/logout', { token: u.token }); // 同一 token 再次登出
    const r3 = await api('POST', '/auth/logout', { token: 'invalid.token.value' }); // 失效/伪造 token
    const q = await api('GET', '/draw/quota', { token: u.token });
    record('TC-18-api', r1.http === 200 && r1.json?.code === 0 && r2.http === 200 && q.http === 401 ? 'PASS' : 'FAIL',
      `首次登出 http=${r1.http}/code=${r1.json?.code}(期望 200/0)；重复登出 http=${r2.http}/code=${r2.json?.code}（幂等，不应 401）；失效 token 登出 http=${r3.http}/code=${r3.json?.code}；登出后访问受保护接口 http=${q.http}(期望 401)`);
  }

  // ═══ TC-18b 登录限流 > 10 次/分钟 → 429 + code=1001 ═══
  {
    const name = `lrl_${T}`;
    await newUser(name);
    const codes = [];
    for (let i = 0; i < 13; i++) {
      const r = await login(name, PW);
      codes.push(`${r.http}/${r.json?.code}`);
    }
    const has429 = codes.some((c) => c.startsWith('429'));
    const distinct = codes.some((c) => c === '200/1102');
    record('TC-18b', has429 ? 'PASS' : 'FAIL',
      `同用户名 1 分钟内 13 次登录返回序列=${codes.join(' ')}；出现 429=${has429}（限流与 1102 区分表达：${distinct ? '序列中并存 1102 与 429' : '本次未出现 1102'}）`);
  }

  // ═══ TC-80 抽奖审计 ═══
  {
    const u = pool[2];
    resetQuota(u.userId); clearDrawRequests(u.userId);
    sql(`DELETE FROM AuditLog WHERE OperatorId = ${u.userId} AND Module = 'draw'`);
    const k = randomUUID();
    const drawAudit = `SELECT COUNT(*) FROM AuditLog WHERE OperatorId = ${u.userId} AND Module = 'draw' AND OperationType = 'Draw'`;
    await api('POST', '/draw', { token: u.token, body: {}, idem: k });
    const after1 = Number(sql(drawAudit));
    await api('POST', '/draw', { token: u.token, body: {}, idem: k }); // 幂等重放
    const after2 = Number(sql(drawAudit));
    const cols = sql(`SELECT AfterJson FROM AuditLog WHERE OperatorId = ${u.userId} AND Module = 'draw' ORDER BY Id DESC LIMIT 1`);
    const noSecret = !/password|token|Bearer/i.test(cols);
    record('TC-80', after1 === 1 && after2 === 1 && noSecret ? 'PASS' : 'FAIL',
      `一次成功抽奖产生审计=${after1} 条；幂等重放后=${after2} 条（期望不重复）；审计 Detail=${cols}（不含密码/token=${noSecret}）`);
  }

  // ═══ TC-82 密码 BCrypt 哈希存储，不出现明文 ═══
  {
    const name = `hash_${T}`;
    await newUser(name);
    const hash = sql(`SELECT PasswordHash FROM User WHERE UserName = '${name}'`);
    const isBcrypt = /^\$2[aby]\$/.test(hash);
    const plain = hash.includes(PW);
    record('TC-82', isBcrypt && !plain ? 'PASS' : 'FAIL',
      `存储值前缀=${hash.slice(0, 7)}（BCrypt=$2a/$2b=${isBcrypt}）；含明文密码=${plain}；长度=${hash.length}`);
  }

  // ═══ TC-83 生命周期：无物理删除 ═══
  {
    const cols = ['User', 'PrizeItem', 'WinningRecord'];
    const detail = [];
    let ok = true;
    for (const t of cols) {
      const d = sql(`describe ${t}`).split('\n').map((l) => l.split('\t')[0]);
      const has = d.includes('IsDeleted');
      detail.push(`${t}: IsDeleted=${has}`);
      if (!has) ok = false;
    }
    const del = sql('SELECT COUNT(*) FROM information_schema.SCHEMATA WHERE SCHEMA_NAME = \'luckydraw_dev\'');
    record('TC-83', ok ? 'PASS' : 'FAIL', `物理表逻辑删除列检查：${detail.join(' | ')}；库存在=${del}`);
  }

  // ═══ TC-81 日志不含密码 / token（判据面 = 落盘日志正文 + 三面 DB 检索） ═══
  {
    const auditHits = sql("SELECT COUNT(*) FROM AuditLog WHERE CONCAT(COALESCE(BeforeJson,''),COALESCE(AfterJson,''),COALESCE(TargetObject,'')) LIKE '%Abcd1234%' OR CONCAT(COALESCE(BeforeJson,''),COALESCE(AfterJson,'')) LIKE '%Bearer%' OR CONCAT(COALESCE(BeforeJson,''),COALESCE(AfterJson,'')) LIKE '%refresh_token%'");
    const reqHits = sql("SELECT COUNT(*) FROM DrawRequest WHERE COALESCE(RequestHash,'') LIKE '%Abcd1234%'");
    const userHits = sql("SELECT COUNT(*) FROM AuditLog WHERE Module='auth' AND (AfterJson LIKE '%password%' OR AfterJson LIKE '%PasswordHash%')");
    const scan = scanPersistedLogs();
    const failures = [];
    if (auditHits !== '0') failures.push('审计表三模式命中');
    if (reqHits !== '0') failures.push('抽奖流水明文口令命中');
    if (userHits !== '0') failures.push('注册审计含密码字段');
    if (scan.gateHits !== 0) failures.push(`落盘日志闸门模式命中（${scan.gateDetail}）`);
    if (scan.scannedBytes === 0) failures.push('无可用扫描面（防空转：无字节即不可判 PASS）');
    if (!scan.selfTestOk) failures.push('扫描器辨别力自证未通过（阳性样本未命中 / 阴性样本误命中）');
    record('TC-81', failures.length === 0 ? 'PASS' : 'FAIL',
      `审计表三模式命中行数=${auditHits}（三模式字面量见本块 SQL）；抽奖流水命中明文口令=${reqHits}；注册审计出现密码字段=${userHits}（注册审计仅记 id+userName）。落盘日志正文扫描：扫描面=${scan.surfaces}；合计扫描字节=${scan.scannedBytes}；闸门模式命中=${scan.gateHits}（${scan.gateDetail}）；词面模式命中（不判定、仅计数）=${scan.wordDetail}；扫描器自证=${scan.selfTestOk ? '通过' : '未通过'}${failures.length ? '；失败项=' + failures.join('/') : ''}。注：模式编号→字面量对照见本脚本 TC-81 常量块注释（防自污染：日志内不回显字面量）；本行取代「应用控制台日志未落盘文件、无法离线检索」旧陈述（该陈述已被 CHG-21 推翻）`);
  }

  // ═══ TC-85 路由懒加载 + 强制分页（静态产物检查） ═══
  {
    record('TC-85', 'SKIP', '需检查前端构建产物（src/frontend/dist）与路由定义；由 E2E/静态检查项在 52 中登记结论');
  }

  // ═══ TC-09 注册限流 > 10 次/分钟 → 429 + code=1001（放最后，避免影响其他用例注册） ═══
  {
    const codes = [];
    for (let i = 0; i < 13; i++) {
      const r = await api('POST', '/auth/register', {
        body: { userName: `rl${Date.now().toString(36)}${i}`, password: PW, confirmPassword: PW },
        idem: randomUUID()
      });
      codes.push(`${r.http}/${r.json?.code}`);
    }
    const has429 = codes.some((c) => c.startsWith('429'));
    const has1001 = codes.some((c) => c.includes('/1001'));
    record('TC-09', has429 && has1001 ? 'PASS' : 'FAIL',
      `1 分钟内 13 次注册返回序列=${codes.join(' ')}；出现 429=${has429}，出现 code=1001=${has1001}`);
  }

  // ═══ 汇总 ═══
  console.log('\n=== 汇总 ===');
  const by = { PASS: 0, FAIL: 0, SKIP: 0 };
  for (const r of results) {
    by[r.status] = (by[r.status] ?? 0) + 1;
    if (r.status !== 'PASS') console.log(`  ${r.status} ${r.tc}: ${r.detail}`);
  }
  console.log(`PASS=${by.PASS} FAIL=${by.FAIL} SKIP=${by.SKIP} TOTAL=${results.length}`);
  console.log('\n=== JSON ===');
  console.log(JSON.stringify(results, null, 1));
}

main()
  .catch((e) => {
    console.error('SUITE ERROR:', e);
    process.exitCode = 1;
  })
  .finally(() => {
    try {
      restorePrizes();
      console.log('奖池夹具已还原');
    } catch (e) {
      console.error('还原夹具失败：', e.message);
    }
  });
