/**
 * TC-56c：缓存类中间件依赖不可用时的降级（test-executor，破坏性批）。
 *
 * 运行：node tests/e2e/api/tc56c-redis-degradation.mjs
 * 前置：Testing 实例已在 :5199 运行（发布目录启动、库 luckydraw_test、Redis db 2）。
 *      本脚本会 `docker stop` / `docker start` 容器 **luckydraw-redis**（收尾必复位，finally 中执行）。
 *
 * ═══ 被测承诺（docs/30-architecture.md §6.1 降级矩阵 :979-989） ═══
 *   抽奖判定与扣减「完全走 MySQL（幂等退化为 DB 唯一索引路径）」→ 可用；
 *   剩余次数 / 记录查询「不依赖 Redis」→ 可用；
 *   幂等重放「退化为 DB 路径，重复请求仍不重复扣次」→ 可用；
 *   结论：「Redis 不是抽奖主链路的强依赖」。
 *
 * ═══ 判据（读库原文 + 接口原文，均不放宽） ═══
 *   1) 重放同一幂等键 K：结果与首次一致；UserDrawQuota.UsedCount 不变；
 *      WinningRecord 无新行；DrawRequest 中该键仍仅 1 行；奖池快照逐行不变。
 *   2) 新键抽奖：主链路仍可用（code=0），次数正常 +1（中奖则记录 +1、对应库存 -1）。
 *   3) /draw/quota 与 /records：可用且与库内一致（/records 的 total 与 DB COUNT 相等）。
 *   4) 正向证据：实例日志出现设计内降级 warning（证明走的是降级路径，不是碰巧没报错）。
 *   5) 收尾：Redis 恢复后注册 + 抽奖恢复正常。
 */
import { execFileSync } from 'node:child_process';
import { connect } from 'node:net';
import fs from 'node:fs';
import { randomUUID } from 'node:crypto';
import { BASE, DB, sql, api, registerUser, prizeRows, sleep, tag } from './stats-lib.mjs';

const INSTANCE_LOG = process.env.TC56C_LOG ?? 'tests/e2e/logs/stats-instance-20260917-164034.log';
const REDIS_CONTAINER = 'luckydraw-redis';
const REDIS_DB = 2;

const verdicts = [];
const evidence = { base: BASE, db: DB, redis: { db: REDIS_DB, container: REDIS_CONTAINER }, instanceLog: INSTANCE_LOG, phases: {} };
const log = (...a) => console.log(...a);

function verdict(tc, ok, detail) {
  verdicts.push({ tc, status: ok ? 'PASS' : 'FAIL', detail });
  log(`\n[${ok ? 'PASS' : 'FAIL'}] ${tc} :: ${detail}`);
}

const q = (statement) => sql(statement);
function trySql(statement) {
  try {
    return sql(statement);
  } catch (e) {
    return `__ERR__${String(e.message).split('\n')[0].slice(0, 200)}`;
  }
}
const nowIso = () => new Date().toISOString();

// ─────────────────────────── Redis 通道 ───────────────────────────

/** 经容器内 redis-cli 探测；容器不可用时返回 __ERR__（本身即证据）。 */
function redisCli(args) {
  try {
    return execFileSync('docker', ['exec', REDIS_CONTAINER, 'redis-cli', ...args], {
      encoding: 'utf8',
      stdio: ['ignore', 'pipe', 'pipe']
    }).trim();
  } catch (e) {
    return `__ERR__${String(e.message).split('\n')[0].slice(0, 160)}`;
  }
}
const redisPing = () => redisCli(['ping']);

/** 不经容器、直接从宿主以 TCP 探测 6379（应用视角的「依赖是否可达」）。 */
function tcpProbe(host, port, timeoutMs = 2500) {
  return new Promise((resolve) => {
    const socket = connect({ host, port });
    let done = false;
    const finish = (r) => {
      if (done) return;
      done = true;
      socket.destroy();
      resolve(r);
    };
    socket.setTimeout(timeoutMs);
    socket.on('connect', () => finish({ reachable: true }));
    socket.on('timeout', () => finish({ reachable: false, error: `timeout(${timeoutMs}ms)` }));
    socket.on('error', (e) => finish({ reachable: false, error: String(e.code ?? e.message) }));
  });
}

function containerAction(action) {
  const out = execFileSync('docker', [action, REDIS_CONTAINER], { encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe'] }).trim();
  return out;
}

// ─────────────────────────── 日志证据（GBK 解码） ───────────────────────────

function logSize() {
  try {
    return fs.statSync(INSTANCE_LOG).size;
  } catch {
    return 0;
  }
}

/** 读取 [offset, EOF) 增量字节并以 GBK 解码（Serilog 控制台输出为 Windows 代码页 936 字节）。 */
function logTailFrom(offset) {
  const fd = fs.openSync(INSTANCE_LOG, 'r');
  try {
    const size = fs.fstatSync(fd).size;
    const len = Math.max(0, size - offset);
    if (len === 0) return '';
    const buf = Buffer.alloc(len);
    fs.readSync(fd, buf, 0, len, offset);
    return new TextDecoder('gbk', { fatal: false }).decode(buf);
  } finally {
    fs.closeSync(fd);
  }
}

// ─────────────────────────── 库内事实 ───────────────────────────

const usedCount = (userId) => {
  const raw = trySql(`SELECT UsedCount FROM UserDrawQuota WHERE UserId = ${userId} ORDER BY DrawDate DESC LIMIT 1`);
  return raw && !raw.startsWith('__ERR__') ? Number(raw) : null;
};
const recordCount = (userId) => Number(q(`SELECT COUNT(*) FROM WinningRecord WHERE UserId = ${userId}`));
const drawReqRows = (userId, key) =>
  Number(q(`SELECT COUNT(*) FROM DrawRequest WHERE UserId = ${userId} AND IdempotencyKey = '${key}'`));
const poolById = () => Object.fromEntries(prizeRows().map((r) => [r.id, r.stock]));

// ─────────────────────────── 主流程 ───────────────────────────

let redisStopped = false;
let user = null;

try {
  log('══ TC-56c：Redis 不可用时的降级 ══');
  log(`实例 = ${BASE} ；库 = ${DB} ；Redis = ${REDIS_CONTAINER} db${REDIS_DB} ；时间 = ${nowIso()}`);

  // 阶段 0：前置（Redis 正常）+ 注册 + 基线抽奖（键 K）
  log('\n── 阶段 0：前置（Redis 正常）+ 注册 + 基线抽奖 ──');
  const ping0 = redisPing();
  const tcp0 = await tcpProbe('127.0.0.1', 6379);
  log(`  redis-cli ping = ${ping0} ；TCP 127.0.0.1:6379 reachable = ${tcp0.reachable}`);
  evidence.phases.pre = { ping: ping0, tcp: tcp0 };
  if (ping0 !== 'PONG' || !tcp0.reachable) throw new Error('前置失败：Redis 不可用，无法建立「正常基线」');

  user = await registerUser(`tc56c_${tag()}`);
  log(`  注册成功：userName=${user.name} userId=${user.userId} token=${user.token ? '已签发' : '缺失'}`);
  if (!user.token) throw new Error('前置失败：未取得访问令牌');

  const quota0 = await api('GET', '/draw/quota', { token: user.token });
  log(`  GET /draw/quota → http=${quota0.http} ${quota0.text.slice(0, 160)}`);

  const K = randomUUID();
  const base = await api('POST', '/draw', { token: user.token, idem: K, timeoutMs: 30000 });
  log(`  基线抽奖（键 K=${K}）→ http=${base.http} code=${base.json?.code} data=${JSON.stringify(base.json?.data)}`);
  if (base.http !== 200 || base.json?.code !== 0) throw new Error('前置失败：基线抽奖未成功');
  const cacheKey = `draw:idempotency:${user.userId}:${K}`;
  const cacheExists = redisCli(['-n', String(REDIS_DB), 'EXISTS', cacheKey]);
  log(`  幂等结果缓存 ${cacheKey} EXISTS = ${cacheExists}（证明降级前的缓存路径确实生效）`);
  const baseline = {
    usedCount: usedCount(user.userId),
    records: recordCount(user.userId),
    drawReqRows: drawReqRows(user.userId, K),
    pool: poolById(),
    response: base.json.data,
    cacheKey,
    cacheExists
  };
  log(`  基线：UsedCount=${baseline.usedCount} WinningRecord=${baseline.records} DrawRequest(键K)=${baseline.drawReqRows}`);
  log(`  奖池库存快照 = ${JSON.stringify(baseline.pool)}`);
  evidence.phases.pre.baseline = baseline;
  if (baseline.cacheExists !== '1') log('  [注意] 缓存键不存在，降级前基线取证不完整（如实记录）');

  // 阶段 1：停 Redis
  log('\n── 阶段 1：停 Redis（注入依赖不可用） ──');
  const logOffset = logSize();
  log(`  实例日志增量起点 offset = ${logOffset}（${INSTANCE_LOG}）`);
  const stopOut = containerAction('stop');
  redisStopped = true;
  log(`  docker stop ${REDIS_CONTAINER} → ${stopOut}`);
  const ping1 = redisPing();
  const tcp1 = await tcpProbe('127.0.0.1', 6379);
  log(`  停止后：redis-cli ping = ${ping1}`);
  log(`  停止后：TCP 127.0.0.1:6379 reachable = ${tcp1.reachable}（error=${tcp1.error ?? '-'}）`);
  evidence.phases.outage = { ping: ping1, tcp: tcp1, logOffset };
  if (tcp1.reachable) throw new Error('注入失败：Redis 仍可达');

  // 阶段 2：重放同一幂等键 K
  log('\n── 阶段 2：重放同一幂等键 K（不得重复扣次） ──');
  const t2 = Date.now();
  const replay = await api('POST', '/draw', { token: user.token, idem: K, timeoutMs: 90000 });
  const replayMs = Date.now() - t2;
  log(`  重放 → http=${replay.http} code=${replay.json?.code} elapsed=${replayMs}ms data=${JSON.stringify(replay.json?.data)}`);
  evidence.phases.replay = { http: replay.http, body: replay.json, text: replay.text, ms: replayMs };

  await sleep(300);
  const afterReplay = {
    usedCount: usedCount(user.userId),
    records: recordCount(user.userId),
    drawReqRows: drawReqRows(user.userId, K),
    pool: poolById()
  };
  log(`  重放后：UsedCount=${afterReplay.usedCount} WinningRecord=${afterReplay.records} DrawRequest(键K)=${afterReplay.drawReqRows}`);
  log(`  奖池库存快照 = ${JSON.stringify(afterReplay.pool)}`);
  evidence.phases.replay.after = afterReplay;

  const sameAsFirst =
    replay.http === 200 &&
    replay.json?.code === 0 &&
    replay.json?.data?.itemId === baseline.response.itemId &&
    replay.json?.data?.isWin === baseline.response.isWin &&
    replay.json?.data?.remainingAttempts === baseline.response.remainingAttempts;
  const replayChecks = [
    ['重放结果与首次一致（itemId / isWin / remainingAttempts 全等）', sameAsFirst,
      `首次=${JSON.stringify(baseline.response)} 重放=${JSON.stringify(replay.json?.data)}`],
    ['UsedCount 未变（不重复扣次）', afterReplay.usedCount === baseline.usedCount, `${baseline.usedCount} → ${afterReplay.usedCount}`],
    ['WinningRecord 无新行', afterReplay.records === baseline.records, `${baseline.records} → ${afterReplay.records}`],
    ['DrawRequest 中键 K 仍仅 1 行', afterReplay.drawReqRows === 1 && baseline.drawReqRows === 1, `${baseline.drawReqRows} → ${afterReplay.drawReqRows}`],
    ['奖池库存逐行未变', JSON.stringify(afterReplay.pool) === JSON.stringify(baseline.pool),
      `${JSON.stringify(baseline.pool)} → ${JSON.stringify(afterReplay.pool)}`]
  ];
  let replayOk = true;
  for (const [name, ok, detail] of replayChecks) {
    log(`  [${ok ? 'OK' : 'VIOLATION'}] ${name}（${detail}）`);
    if (!ok) replayOk = false;
  }
  evidence.phases.replay.checks = replayChecks.map(([name, ok, detail]) => ({ name, ok, detail }));

  // 阶段 3：新键正常抽奖（主链路仍可用）
  log('\n── 阶段 3：新键抽奖（主链路可用性） ──');
  const K2 = randomUUID();
  const t3 = Date.now();
  const fresh = await api('POST', '/draw', { token: user.token, idem: K2, timeoutMs: 90000 });
  const freshMs = Date.now() - t3;
  log(`  新键抽奖 → http=${fresh.http} code=${fresh.json?.code} elapsed=${freshMs}ms data=${JSON.stringify(fresh.json?.data)}`);
  await sleep(300);
  const afterFresh = { usedCount: usedCount(user.userId), records: recordCount(user.userId), pool: poolById() };
  log(`  新键后：UsedCount=${afterFresh.usedCount} WinningRecord=${afterFresh.records}`);
  const freshWin = fresh.json?.data?.isWin === true;
  const freshItemId = fresh.json?.data?.itemId;
  const freshChecks = [
    ['主链路可用（http=200 / code=0）', fresh.http === 200 && fresh.json?.code === 0, `http=${fresh.http} code=${fresh.json?.code}`],
    ['次数正常扣 1', afterFresh.usedCount === afterReplay.usedCount + 1, `${afterReplay.usedCount} → ${afterFresh.usedCount}`],
    ['中奖记录与 isWin 一致', afterFresh.records === afterReplay.records + (freshWin ? 1 : 0),
      `记录 ${afterReplay.records} → ${afterFresh.records}（isWin=${freshWin}）`],
    ['中奖时对应库存 -1', !freshWin || afterFresh.pool[freshItemId] === afterReplay.pool[freshItemId] - 1,
      `itemId=${freshItemId} 库存 ${afterReplay.pool[freshItemId]} → ${afterFresh.pool[freshItemId]}`]
  ];
  let freshOk = true;
  for (const [name, ok, detail] of freshChecks) {
    log(`  [${ok ? 'OK' : 'VIOLATION'}] ${name}（${detail}）`);
    if (!ok) freshOk = false;
  }
  evidence.phases.freshDraw = { http: fresh.http, body: fresh.json, ms: freshMs, after: afterFresh, win: freshWin, itemId: freshItemId };
  evidence.phases.freshDraw.checks = freshChecks.map(([name, ok, detail]) => ({ name, ok, detail }));

  // 阶段 4：次数 / 记录查询不受影响
  log('\n── 阶段 4：查询不受影响（/draw/quota、/records） ──');
  const quotaQ = await api('GET', '/draw/quota', { token: user.token, timeoutMs: 30000 });
  const recordsQ = await api('GET', '/records?pageIndex=1&pageSize=10', { token: user.token, timeoutMs: 30000 });
  const dbRecords = recordCount(user.userId);
  const dbUsed = usedCount(user.userId);
  log(`  GET /draw/quota → http=${quotaQ.http} ${quotaQ.text.slice(0, 200)}`);
  log(`  GET /records    → http=${recordsQ.http} ${recordsQ.text.slice(0, 260)}`);
  log(`  库内对照：UsedCount=${dbUsed} WinningRecord=${dbRecords}`);
  const quotaChecks = [
    ['quota 接口可用', quotaQ.http === 200 && quotaQ.json?.code === 0, `http=${quotaQ.http} code=${quotaQ.json?.code}`],
    ['quota 剩余次数与库内一致', quotaQ.json?.data?.remainingAttempts === (quotaQ.json?.data?.dailyLimit ?? 0) - dbUsed,
      `remaining=${quotaQ.json?.data?.remainingAttempts} dailyLimit=${quotaQ.json?.data?.dailyLimit} 库内 UsedCount=${dbUsed}`],
    ['records 接口可用', recordsQ.http === 200 && recordsQ.json?.code === 0, `http=${recordsQ.http} code=${recordsQ.json?.code}`],
    ['records totalCount 与库内一致', recordsQ.json?.data?.totalCount === dbRecords, `接口 totalCount=${recordsQ.json?.data?.totalCount} 库内=${dbRecords}`],
    ['records 条目数与库内一致', recordsQ.json?.data?.items?.length === Math.min(10, dbRecords),
      `items=${recordsQ.json?.data?.items?.length} 库内=${dbRecords}`]
  ];
  let queryOk = true;
  for (const [name, ok, detail] of quotaChecks) {
    log(`  [${ok ? 'OK' : 'VIOLATION'}] ${name}（${detail}）`);
    if (!ok) queryOk = false;
  }
  evidence.phases.queries = {
    quota: quotaQ.json,
    records: { totalCount: recordsQ.json?.data?.totalCount, items: recordsQ.json?.data?.items?.length, first: recordsQ.json?.data?.items?.[0] },
    db: { usedCount: dbUsed, records: dbRecords },
    checks: quotaChecks.map(([name, ok, detail]) => ({ name, ok, detail }))
  };

  // 阶段 5：降级正向证据（实例日志增量，GBK 解码）
  log('\n── 阶段 5：降级正向证据（实例日志增量） ──');
  await sleep(500);
  const tail = logTailFrom(logOffset);
  const markers = ['退化为数据库幂等路径', '写入幂等结果缓存失败', '降级为无幂等执行', 'Redis 连接不可用', '会话缺失'];
  const lines = tail.split(/\r?\n/);
  const hits = {};
  for (const m of markers) hits[m] = lines.filter((l) => l.includes(m));
  const redisLines = lines.filter((l) => /Redis|redis/.test(l));
  for (const m of markers) log(`  标记「${m}」命中 ${hits[m].length} 行${hits[m].length ? ' → ' + JSON.stringify(hits[m][0].slice(0, 200)) : ''}`);
  log(`  含 Redis 字样的日志行 ${redisLines.length} 行；示例 = ${JSON.stringify(redisLines.slice(0, 3).map((l) => l.slice(0, 180)))}`);
  evidence.phases.degradationEvidence = {
    logOffset,
    newBytes: logSize() - logOffset,
    hits: Object.fromEntries(Object.entries(hits).map(([k, v]) => [k, v.map((l) => l.slice(0, 300))])),
    redisLineSample: redisLines.slice(0, 6).map((l) => l.slice(0, 300)),
    redisLineCount: redisLines.length
  };
  const degradeEvidenced = hits['退化为数据库幂等路径'].length > 0 || hits['写入幂等结果缓存失败'].length > 0;
  log(`  降级路径正向证据成立 = ${degradeEvidenced}`);

  // 阶段 6：收尾 —— 恢复 Redis 并验证恢复正常
  log('\n── 阶段 6：恢复 Redis 并验证恢复正常 ──');
  const startOut = containerAction('start');
  redisStopped = false;
  log(`  docker start ${REDIS_CONTAINER} → ${startOut}`);
  let ping2 = '';
  for (let i = 0; i < 20; i++) {
    await sleep(500);
    ping2 = redisPing();
    if (ping2 === 'PONG') break;
  }
  const tcp2 = await tcpProbe('127.0.0.1', 6379);
  log(`  恢复后：redis-cli ping = ${ping2} ；TCP reachable = ${tcp2.reachable}`);
  const recovered = await registerUser(`tc56c_r_${tag()}`);
  const recDraw = await api('POST', '/draw', { token: recovered.token, idem: randomUUID(), timeoutMs: 30000 });
  log(`  恢复后注册 = 成功（userId=${recovered.userId}）；抽奖 → http=${recDraw.http} code=${recDraw.json?.code} data=${JSON.stringify(recDraw.json?.data)}`);
  const recoverOk = ping2 === 'PONG' && tcp2.reachable && recDraw.http === 200 && recDraw.json?.code === 0;
  evidence.phases.recovery = { ping: ping2, tcp: tcp2, registerUserId: recovered.userId, draw: recDraw.json, ok: recoverOk };

  // 判定
  const ok = replayOk && freshOk && queryOk && degradeEvidenced && recoverOk;
  verdict(
    'TC-56c',
    ok,
    `重放不一致项 ${replayChecks.filter((c) => !c[1]).length}；主链路失败项 ${freshChecks.filter((c) => !c[1]).length}；` +
      `查询失败项 ${quotaChecks.filter((c) => !c[1]).length}；降级正向证据=${degradeEvidenced}；恢复=${recoverOk}`
  );
} catch (e) {
  verdict('TC-56c', false, `执行中断：${String(e?.message ?? e)}`);
  evidence.error = String(e?.stack ?? e);
} finally {
  log('\n── 收尾 ──');
  if (redisStopped) {
    try {
      log(`  Redis 处于停止态 → docker start ${REDIS_CONTAINER} → ${containerAction('start')}`);
    } catch (e) {
      log(`  Redis 恢复异常：${String(e?.message ?? e)}`);
    }
  }
  await sleep(1500);
  const pingEnd = redisPing();
  log(`  收尾 redis-cli ping = ${pingEnd}`);
  evidence.reset = { ping: pingEnd, pool: (() => { try { return prizeRows(); } catch { return null; } })() };

  log(`\n══ 汇总 ══`);
  for (const v of verdicts) log(`[${v.status}] ${v.tc} :: ${v.detail}`);
  log(`\nJSON_EVIDENCE_BEGIN`);
  log(JSON.stringify(evidence, null, 2));
  log(`JSON_EVIDENCE_END`);
  process.exitCode = verdicts.every((v) => v.status === 'PASS') ? 0 : 1;
}
