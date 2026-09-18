/**
 * TC-56b：事务中途失败 → 三方一致回滚（test-executor，破坏性批）。
 *
 * 运行：node tests/e2e/api/tc56b-tx-rollback.mjs
 * 前置：Testing 实例已在 :5199 运行（发布目录启动、库 luckydraw_test、Redis db 2）。
 *
 * ═══ 注入手法（InnoDB 父行锁阻塞子表 FK 检查） ═══
 *   抽奖事务写入顺序（`DrawService.ExecuteDrawAsync`）：
 *     ① DrawRequest 幂等行 → ② 条件扣次（UserDrawQuota）→ ③ 候选集/加权随机/条件扣库存（PrizeItem）
 *     → ⑥ WinningRecord INSERT → ⑦ 审计 INSERT → ⑧ 回填流水 → COMMIT。
 *   实测 FK 事实（SHOW CREATE TABLE，本脚本内复核并落盘）：
 *     DrawRequest / UserDrawQuota / AuditLog **无 FK**；WinningRecord **有 FK** 指向 User(Id) 与 PrizeItem(Id)。
 *   → 在**另一条连接**上对 `User` 行持排他锁（START TRANSACTION; SELECT .. FOR UPDATE; 保持不提交），
 *     第 ⑥ 步 WinningRecord 的 FK 检查须对该父行加 S 锁 → 被阻塞 → InnoDB 1205 锁等待超时 → 事务失败回滚。
 *     此时 **② 扣次与 ③ 扣库存已写入**，正是用例要求的「事务中途失败」点（不是「第一条语句就失败」）。
 *
 * ═══ 前置条件（关键） ═══
 *   该用户的 `UserDrawQuota` 行**必须已存在**，否则第 ② 步（INSERT，表上无 FK，不在阻塞路径）
 *   之外的行为会变化；本脚本先做一次**探针抽奖**建立配额行。
 *   且必须确保抽中的是**真实奖品**（no-prize 不扣库存、无中奖记录，失败点将前移到 ⑧ 之后不可达）。
 *   本脚本以**候选集收窄**（仅保留 prize-mug）取得确定性，而非依赖 Draw:Deterministic 配置
 *   —— 不重启实例、不新增配置面；候选集构成以 SQL 枚举核对（见 fixtures()）。
 *
 * ═══ 超时口径（CHG-17 后重写） ═══
 *   innodb_lock_wait_timeout 实测（脚本内打印）；锁持有时间 holdSeconds 必须长于请求总耗时。
 *   CHG-16/17 后：CommandTimeout 已由默认 30s 提升到 **60s**（> innodb_lock_wait_timeout 50s）→
 *   单次尝试最坏吃满 50s 后由 **MySQL 抛 1205**（瞬时）→ DrawService 重试整事务；
 *   **MaxTransactionAttempts = 2**（= 重试 1 次，对齐 D-08）→ 总耗时最坏 ≈ **2 × 50s = 100s**。
 *   终态应为 **HTTP 200 / code=1001「系统繁忙，请稍后重试」**（不再是旧的 500；经用户裁决，见 CHG-17）。
 *   **锁须持有超过 100s**，否则第 2 次尝试会拿到锁并成功返回 —— 那将无法观察「终态失败」，
 *   本脚本默认 HOLD_SECONDS=300 满足该前提。客户端超时按测得值留足（默认 320s）。
 *
 * ═══ 复位 ═══
 *   释放锁（KILL holder 连接）→ 复位奖池为 FR-03 默认值 → flush Redis db 2。均在 finally 中执行。
 */
import { spawn } from 'node:child_process';
import { execFileSync } from 'node:child_process';
import { randomUUID } from 'node:crypto';
import { BASE, DB, sql, api, registerUser, prizeRows, candidateSet, setEnabled, setStock, restorePrizes, flushRedisDb, sleep, tag } from './stats-lib.mjs';

const HOLD_SECONDS = Number(process.env.TC56B_HOLD ?? 300);
const CLIENT_TIMEOUT_MS = Number(process.env.TC56B_TIMEOUT ?? 320000);
const PIN_MUG_STOCK = 5;
const MUG = 'prize-mug';

const started = Date.now();
const verdicts = [];
const evidence = { base: BASE, db: DB, phases: {} };
const log = (...a) => console.log(...a);

function verdict(tc, ok, detail) {
  verdicts.push({ tc, status: ok ? 'PASS' : 'FAIL', detail });
  log(`\n[${ok ? 'PASS' : 'FAIL'}] ${tc} :: ${detail}`);
}

/** 严格 SQL（失败即抛），stats-lib 同口径。 */
const q = (statement) => sql(statement);
/** 宽容 SQL：仅用于「探测态」查询，失败不中断（返回值附错误标记）。 */
function trySql(statement) {
  try {
    return sql(statement);
  } catch (e) {
    return `__ERR__${String(e.message).split('\n')[0].slice(0, 160)}`;
  }
}

const nowIso = () => new Date().toISOString();

// ─────────────────────────── 快照与核验 ───────────────────────────

function quotaRow(userId) {
  const raw = trySql(
    `SELECT DrawDate, UsedCount FROM UserDrawQuota WHERE UserId = ${userId} ORDER BY DrawDate DESC LIMIT 1`
  );
  if (!raw || raw.startsWith('__ERR__')) return { raw };
  const [drawDate, usedCount] = raw.split('\t');
  return { drawDate, usedCount: Number(usedCount), raw };
}

function mugStock() {
  return Number(q(`SELECT Stock FROM PrizeItem WHERE Code = '${MUG}'`));
}

function recordCount(userId) {
  return Number(q(`SELECT COUNT(*) FROM WinningRecord WHERE UserId = ${userId}`));
}

function drawRequestCount(userId) {
  return Number(q(`SELECT COUNT(*) FROM DrawRequest WHERE UserId = ${userId}`));
}

function auditCount(userId) {
  return Number(q(`SELECT COUNT(*) FROM AuditLog WHERE OperatorId = ${userId} AND Module = 'Draw'`));
}

function autoInc(table) {
  // information_schema 统计默认缓存 86400s（information_schema_stats_expiry），须置 0 才读到实时值
  return Number(
    q(
      'SET SESSION information_schema_stats_expiry=0; ' +
        `SELECT AUTO_INCREMENT FROM information_schema.TABLES WHERE TABLE_SCHEMA='${DB}' AND TABLE_NAME='${table}'`
    )
  );
}

/** 读库原文快照（不用接口投影）。 */
function snapshot(userId) {
  return {
    quota: quotaRow(userId),
    mugStock: mugStock(),
    records: recordCount(userId),
    drawRequests: drawRequestCount(userId),
    audits: auditCount(userId),
    autoIncDrawRequest: autoInc('DrawRequest'),
    autoIncWinningRecord: autoInc('WinningRecord'),
    at: nowIso()
  };
}

/** 锁等待实况（sys.innodb_lock_waits 为 MySQL 8 自带视图）。 */
function lockWaits() {
  return trySql(
    'SELECT waiting_trx_id, blocking_trx_id, locked_table, locked_index, waiting_pid, blocking_pid, ' +
      'wait_age, REPLACE(LEFT(waiting_query, 220), "\\n", " ") AS waiting_query ' +
      'FROM sys.innodb_lock_waits'
  );
}

function trxByConn(connId) {
  return trySql(
    `SELECT trx_id, trx_state, trx_rows_modified, trx_rows_locked, trx_started ` +
      `FROM information_schema.innodb_trx WHERE trx_mysql_thread_id = ${connId}`
  );
}

/** 该 User 行是否被排他锁占用：以 FOR UPDATE NOWAIT 探测（失败=被占）。 */
function userRowLockedExclusively(userId) {
  const out = trySql(`START TRANSACTION; SELECT Id FROM User WHERE Id = ${userId} FOR UPDATE NOWAIT; COMMIT;`);
  return out.startsWith('__ERR__') ? { locked: true, raw: out } : { locked: false, raw: out };
}

// ─────────────────────────── 夹具与锁 ───────────────────────────

function fixturesNarrowPool() {
  // 仅保留 prize-mug 为候选（候选集谓词：IsEnabled=1 AND IsDeleted=0 AND (Type=3 OR Stock>0)）
  for (const code of ['prize-keyboard', 'prize-earbuds', 'prize-coupon', 'no-prize']) setEnabled(code, 0);
  setEnabled(MUG, 1);
  setStock(MUG, PIN_MUG_STOCK);
}

const HOLDER_MARKER = 'tc56b_lock_holder';

function startLockHolder(userId) {
  const script = [
    'SELECT CONNECTION_ID();',
    'START TRANSACTION;',
    `SELECT Id FROM User WHERE Id = ${userId} FOR UPDATE;`,
    `SELECT SLEEP(${HOLD_SECONDS}) AS ${HOLDER_MARKER};`,
    'COMMIT;'
  ].join(' ');
  const child = spawn(
    'docker',
    ['exec', 'luckydraw-mysql', 'mysql', '-uroot', '-pdevonly', '-D', DB, '-N', '-B', '-e', script],
    { stdio: ['ignore', 'pipe', 'pipe'] }
  );
  let stdout = '';
  let stderr = '';
  child.stdout.on('data', (d) => (stdout += d.toString()));
  child.stderr.on('data', (d) => (stderr += d.toString()));
  return {
    child,
    getStdout: () => stdout.trim(),
    getStderr: () => stderr.trim()
  };
}

/**
 * 按 processlist 中的标记定位锁持有连接（mysql 客户端在管道下 stdout 为块缓冲，
 * CONNECTION_ID 在进程退出前不落盘，故不依赖其标准输出）。
 */
function findHolderConnection() {
  const out = trySql(
    `SELECT id FROM information_schema.processlist WHERE info LIKE 'SELECT SLEEP(%${HOLDER_MARKER}%' LIMIT 1`
  );
  return /^\d+$/.test(out) ? Number(out) : null;
}

function killConnection(connId) {
  return trySql(`KILL ${connId}`);
}

// ─────────────────────────── 主流程 ───────────────────────────

let holder = null;
let holderConnId = null;
let user = null;
let restOk = false;

try {
  log('══ TC-56b 事务中途失败 → 三方一致回滚 ══');
  log(`实例 = ${BASE} ；库 = ${DB} ；时间 = ${nowIso()}`);

  // 阶段 0：实例健康（必须先拿到签发令牌，见 52 §1.6.5 教训）
  log('\n── 阶段 0：实例健康 + 注册取证（复核签发路径可用） ──');
  const uname = `tc56b_${tag()}`;
  user = await registerUser(uname);
  log(`注册成功：userName=${user.name} userId=${user.userId} token=${user.token ? '已签发' : '缺失'}`);
  evidence.phases.health = { userName: user.name, userId: user.userId, tokenIssued: Boolean(user.token) };

  const quota0 = await api('GET', '/draw/quota', { token: user.token });
  log(`GET /draw/quota → http=${quota0.http} body=${quota0.text.slice(0, 200)}`);
  const dailyLimit = quota0.json?.data?.dailyLimit;
  evidence.phases.health.quota = quota0.json?.data ?? null;
  if (!user.token || quota0.http !== 200) throw new Error('实例健康检查失败：未取得可用令牌或配额接口不可用');

  // 阶段 1：FK 事实复核（简报要求自己复核，不采信转述）
  log('\n── 阶段 1：FK 事实复核（SHOW CREATE TABLE 实测） ──');
  const fkFacts = {};
  for (const t of ['DrawRequest', 'UserDrawQuota', 'WinningRecord', 'AuditLog']) {
    const ddl = trySql('SHOW CREATE TABLE `' + t + '`');
    const fks = (ddl.match(/CONSTRAINT .*?FOREIGN KEY/g) || []).map((s) => s.trim());
    fkFacts[t] = fks.length ? fks : ['（无 FK）'];
  }
  for (const [t, v] of Object.entries(fkFacts)) log(`  ${t}: ${v.join(' ; ')}`);
  evidence.phases.fkFacts = fkFacts;

  // 阶段 2：确定性夹具（候选集收窄）+ 候选集枚举核对
  log('\n── 阶段 2：夹具（候选集收窄至 prize-mug，避免 no-prize 导致 ⑥ 不可达） ──');
  fixturesNarrowPool();
  const cands = candidateSet();
  const pool = prizeRows();
  log(`候选集（SQL 枚举）= [${cands.join(', ')}]`);
  log(`奖池实测 = ${JSON.stringify(pool)}`);
  evidence.phases.fixture = { candidates: cands, pool };
  if (cands.length !== 1 || cands[0] !== MUG) throw new Error(`夹具失败：候选集应为 [${MUG}]，实测 [${cands.join(',')}]`);

  // 阶段 3：探针抽奖（建立 UserDrawQuota 行，且验证 ⑥ 路径可达）
  log('\n── 阶段 3：探针抽奖（建立配额行；验证会走到 ⑥ 中奖记录） ──');
  const probe = await api('POST', '/draw', { token: user.token, idem: randomUUID(), timeoutMs: 30000 });
  log(`探针抽奖 → http=${probe.http} body=${probe.text.slice(0, 200)} elapsed=${probe.ms.toFixed(0)}ms`);
  evidence.phases.probe = { http: probe.http, body: probe.json, ms: probe.ms };
  if (probe.http !== 200 || probe.json?.code !== 0) throw new Error('探针抽奖失败，无法建立前置条件');
  const probeItemId = probe.json.data.itemId;
  if (!probe.json.data.isWin || probeItemId !== 3) {
    throw new Error(`探针抽奖未按夹具命中 prize-mug（itemId=${probeItemId} isWin=${probe.json.data.isWin}）`);
  }

  // 阶段 4：基线快照（读库原文）
  await sleep(300);
  const baseline = snapshot(user.userId);
  log(`\n── 阶段 4：基线快照（读库原文） ──`);
  log(`  UserDrawQuota = ${JSON.stringify(baseline.quota)}`);
  log(`  ${MUG}.Stock = ${baseline.mugStock} ；WinningRecord(user) = ${baseline.records} ；DrawRequest(user) = ${baseline.drawRequests} ；AuditLog(Draw) = ${baseline.audits}`);
  log(`  AUTO_INCREMENT: DrawRequest=${baseline.autoIncDrawRequest} WinningRecord=${baseline.autoIncWinningRecord}`);
  evidence.phases.baseline = baseline;

  // 阶段 5：注入（另一连接持 User 行排他锁）
  log(`\n── 阶段 5：注入 —— 另一连接对 User(${user.userId}) 持 X 锁（hold=${HOLD_SECONDS}s） ──`);
  holder = startLockHolder(user.userId);
  for (let i = 0; i < 20 && holderConnId === null; i++) {
    await sleep(500);
    holderConnId = findHolderConnection();
  }
  const holderConn = holderConnId;
  log(`  锁持有连接 id = ${holderConn}（经 processlist 标记 '${HOLDER_MARKER}' 定位）`);
  evidence.phases.holder = { connId: holderConn, holdSeconds: HOLD_SECONDS, stdout: holder.getStdout() };
  if (!holderConn) throw new Error(`未能取得锁持有连接 id（stderr=${holder.getStderr()}）`);

  const lockProbe = userRowLockedExclusively(user.userId);
  log(`  User 行锁探测（FOR UPDATE NOWAIT）→ locked=${lockProbe.locked} raw=${lockProbe.raw.slice(0, 160)}`);
  evidence.phases.holder.lockProbe = lockProbe;
  if (!lockProbe.locked) throw new Error('注入失败：User 行未被排他锁占用');

  // 阶段 6：触发抽奖（后台在飞）+ 在飞期锁等待实况取证
  log('\n── 阶段 6：触发抽奖（中途失败注入） ──');
  const drawKey = randomUUID();
  const t0 = Date.now();
  const drawPromise = api('POST', '/draw', { token: user.token, idem: drawKey, timeoutMs: CLIENT_TIMEOUT_MS });
  let drawDone = false;
  drawPromise.then(() => (drawDone = true), () => (drawDone = true));

  const inflight = [];
  let firstWaitCaptured = null;
  let waiterTrxEvidence = null;
  while (!drawDone) {
    const w = lockWaits();
    const t = trxByConn(holderConn);
    // 等待方（抽奖事务）自身的 trx 状态：rows_modified > 0 = 该事务在阻塞前已写入若干行
    const waiterTrxId = w && !w.startsWith('__ERR__') && w.trim() !== '' ? w.split('\t')[0] : null;
    const waiter = waiterTrxId
      ? trySql(
          `SELECT trx_state, trx_rows_modified, trx_rows_locked, trx_started FROM information_schema.innodb_trx WHERE trx_id = '${waiterTrxId}'`
        )
      : null;
    const line =
      `t+${((Date.now() - t0) / 1000).toFixed(1)}s | lockWaits=${JSON.stringify(w)} | waiterTrx(id=${waiterTrxId})=${JSON.stringify(waiter)} | holderTrx=${JSON.stringify(t)}`;
    inflight.push(line);
    if (!firstWaitCaptured && w && !w.startsWith('__ERR__') && w.trim() !== '') {
      firstWaitCaptured = { t: Date.now() - t0, row: w, waiter };
    }
    if (!waiterTrxEvidence && waiterTrxId) waiterTrxEvidence = { trxId: waiterTrxId, waiter, at: Date.now() - t0 };
    log(`  ${line}`);
    await sleep(1500);
    if (Date.now() - t0 > CLIENT_TIMEOUT_MS + 5000) break;
  }
  const draw = await drawPromise;
  const elapsedMs = Date.now() - t0;
  log(`\n抽奖响应 → http=${draw.http} code=${draw.json?.code} msg=${draw.json?.message ?? ''} elapsed=${elapsedMs}ms error=${draw.error ?? 'none'}`);
  log(`  原始响应体 = ${draw.text.slice(0, 300)}`);
  evidence.phases.injection = {
    key: drawKey,
    holderConn,
    elapsedMs,
    http: draw.http,
    body: draw.json,
    text: draw.text,
    error: draw.error ?? null,
    inflightSamples: inflight.length,
    firstLockWait: firstWaitCaptured,
    waiterTrxEvidence,
    inflightTail: inflight.slice(-6)
  };

  // 阶段 7：释放锁 + 验证释放
  log('\n── 阶段 7：释放锁 ──');
  const killOut = killConnection(holderConn);
  log(`  KILL ${holderConn} → ${killOut.slice(0, 120)}`);
  await sleep(1200);
  const stillThere = trySql(`SELECT COUNT(*) FROM information_schema.processlist WHERE id = ${holderConn}`);
  const probe2 = userRowLockedExclusively(user.userId);
  log(`  连接残留数 = ${stillThere} ；User 行锁探测 → locked=${probe2.locked}`);
  evidence.phases.release = { killOut, stillThere, lockProbe: probe2 };

  // 阶段 8：回滚一致性核验（读库原文）
  log('\n── 阶段 8：三方（+流水 / 审计）一致性核验（读库原文） ──');
  await sleep(500);
  const after = snapshot(user.userId);
  log(`  UserDrawQuota = ${JSON.stringify(after.quota)}`);
  log(`  ${MUG}.Stock = ${after.mugStock} ；WinningRecord(user) = ${after.records} ；DrawRequest(user) = ${after.drawRequests} ；AuditLog(Draw) = ${after.audits}`);
  log(`  AUTO_INCREMENT: DrawRequest=${after.autoIncDrawRequest} WinningRecord=${after.autoIncWinningRecord}`);
  evidence.phases.after = after;

  const checks = [
    ['UserDrawQuota.UsedCount 未变', baseline.quota.usedCount === after.quota.usedCount, `${baseline.quota.usedCount} → ${after.quota.usedCount}`],
    [`PrizeItem(${MUG}).Stock 未变`, baseline.mugStock === after.mugStock, `${baseline.mugStock} → ${after.mugStock}`],
    ['WinningRecord 无新行', baseline.records === after.records, `${baseline.records} → ${after.records}`],
    ['DrawRequest 无残留行', baseline.drawRequests === after.drawRequests, `${baseline.drawRequests} → ${after.drawRequests}`],
    ['AuditLog(Draw) 无残留行', baseline.audits === after.audits, `${baseline.audits} → ${after.audits}`]
  ];
  let allOk = true;
  for (const [name, ok, detail] of checks) {
    log(`  [${ok ? 'OK' : 'VIOLATION'}] ${name}（${detail}）`);
    if (!ok) allOk = false;
  }
  evidence.phases.consistency = checks.map(([name, ok, detail]) => ({ name, ok, detail }));

  // 失败点归属：AUTO_INCREMENT 前进 = 该表 INSERT 语句被真正执行过（InnoDB 回滚不回收自增值）
  const incDelta = {
    drawRequest: after.autoIncDrawRequest - baseline.autoIncDrawRequest,
    winningRecord: after.autoIncWinningRecord - baseline.autoIncWinningRecord
  };
  log(`  失败点旁证（AUTO_INCREMENT 前进量，回滚不回收）：DrawRequest=+${incDelta.drawRequest} WinningRecord=+${incDelta.winningRecord}`);
  evidence.phases.failurePointEvidence = incDelta;

  // 失败点归属（「至少一次写入之后」的判据，三选一命中即可）：
  //   a) 在飞期抓到的等待方事务 rows_modified ≥ 1（事务已改写过行才去等锁）
  //   b) 在飞期抓到的等待语句 = WinningRecord 的 INSERT（该语句位于 ② 扣次 / ③ 扣库存之后）
  //   c) 回滚后 AUTO_INCREMENT 前进（InnoDB 回滚不回收自增值 → 说明该表 INSERT 被真正执行过）
  const rowsModifiedBeforeBlock = Number(
    (evidence.phases.injection.waiterTrxEvidence?.waiter ?? '').split('\t')[1] ?? 0
  );
  const failedMidway =
    (draw.http !== 200 || draw.json?.code !== 0) &&
    (rowsModifiedBeforeBlock >= 1 || incDelta.drawRequest >= 1 || incDelta.winningRecord >= 1) &&
    elapsedMs > 2000;
  // 终态契约（CHG-17 经用户裁决）：重试耗尽 → HTTP 200 / code=1001「系统繁忙，请稍后重试」，
  // 不再是旧的 500「系统内部错误」。1001 系「限流 / 依赖不可用的降级提示」（docs/error-codes.md §「使用边界」1001 条）。
  const terminalOk = draw.http === 200 && draw.json?.code === 1001;
  // 耗时预算（D-08 / CHG-17）：单次尝试最坏吃满 innodb_lock_wait_timeout 50s、MaxTransactionAttempts=2
  // → 预期 ≈ 2 × 50s ≈ 100s。低于 90s 说明整事务重试未真正发生（单次尝试 ~50s）；
  // 高于客户端超时说明观察无效。本项用于把「重试预算 2 次」钉成可回归的断言。
  const budgetOk = elapsedMs >= 90_000 && elapsedMs <= CLIENT_TIMEOUT_MS;
  evidence.phases.failurePoint = { rowsModifiedBeforeBlock, incDelta, failedMidway, terminalOk, budgetOk };
  verdict(
    'TC-56b',
    allOk && failedMidway && terminalOk && budgetOk,
    `终态 = http=${draw.http} code=${draw.json?.code ?? '-'} msg=${draw.json?.message ?? '(空)'}（期望 200 / 1001「系统繁忙，请稍后重试」→ terminalOk=${terminalOk}）；` +
      `elapsed=${(elapsedMs / 1000).toFixed(1)}s（期望 ≥90s 且 ≤客户端超时，即整事务重试确已发生 → budgetOk=${budgetOk}）；` +
      `回滚一致性 ${checks.filter((c) => c[1]).length}/${checks.length} 项通过；` +
      `失败点 = 「至少一次写入之后」：等待方事务阻塞前 rows_modified=${rowsModifiedBeforeBlock}，` +
      `AUTO_INCREMENT 前进 DrawRequest+${incDelta.drawRequest} / WinningRecord+${incDelta.winningRecord} → ${failedMidway}`
  );
} catch (e) {
  verdict('TC-56b', false, `执行中断：${String(e?.message ?? e)}`);
  evidence.error = String(e?.stack ?? e);
} finally {
  // 复位：锁 → 奖池 → Redis
  log('\n── 收尾复位 ──');
  try {
    if (holder) {
      const cid = holderConnId ?? findHolderConnection();
      if (cid) log(`  KILL 锁持有连接 ${cid} → ${killConnection(cid).slice(0, 80)}`);
      holder.child.kill();
      log('  锁持有进程已终止');
    }
  } catch (e) {
    log(`  锁复位异常：${String(e?.message ?? e)}`);
  }
  try {
    restorePrizes();
    flushRedisDb();
    restOk = true;
    log(`  奖池已复位为 FR-03 默认值：${JSON.stringify(prizeRows())}`);
    log(`  Redis db 2 已 flush`);
  } catch (e) {
    log(`  复位异常：${String(e?.message ?? e)}`);
  }
  evidence.reset = { ok: restOk, pool: (() => { try { return prizeRows(); } catch { return null; } })() };

  log(`\n══ 汇总 ══`);
  for (const v of verdicts) log(`[${v.status}] ${v.tc} :: ${v.detail}`);
  log(`总耗时 ${((Date.now() - started) / 1000).toFixed(1)}s`);
  log(`\nJSON_EVIDENCE_BEGIN`);
  log(JSON.stringify(evidence, null, 2));
  log(`JSON_EVIDENCE_END`);
  process.exitCode = verdicts.every((v) => v.status === 'PASS') ? 0 : 1;
}
