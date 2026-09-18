/**
 * TC-78 / TC-79 / TC-79b 概率统计执行（test-executor，覆盖补做）。
 *
 * 运行：
 *   node tests/e2e/api/stats-suite.mjs            # 默认 :5199 / luckydraw_test / 并发 6
 *   STATS_CONCURRENCY=8 node tests/e2e/api/stats-suite.mjs
 *
 * ═══ 实例启动口径（本脚本不在内部起实例，须由调用方先拉起） ═══
 *   ASPNETCORE_ENVIRONMENT=Testing
 *   ASPNETCORE_URLS=http://127.0.0.1:5199
 *   ConnectionStrings__Default=Server=localhost;Port=3307;Database=luckydraw_test;User Id=root;Password=devonly;CharSet=utf8mb4;SslMode=None;AllowPublicKeyRetrieval=True
 *   Redis__Configuration=localhost:6379,defaultDatabase=2
 *   Draw__DailyLimit=30000            # 默认 3 次/用户/日，无法承载 1 万次抽样（QX-E-05 授权项）
 *   RateLimit__DrawPerMinute=200000   # 限流非本批被测对象
 * 全程只经**环境变量**注入，不修改任何 appsettings*.json / launchSettings.json / src/**。
 *
 * ═══ 前置条件与 50 原文的偏差（如实登记，不伪造） ═══
 *   TC-78 原文要求「默认奖池 + **固定随机源**」。实测实现（`DrawService` 的确定性分支，配置契约见 `docs/30-architecture.md` D-06）的确定性机制是
 *   **按用户名强制结果**（`Draw:Deterministic:ForcedResults[].UserName` → `PrizeItemCode`），
 *   **不存在可播种的随机源**；用户名未命中即回退 `WeightedSampler.Pick(…, _randomSource)` 真随机
 *   （生产路径，`SystemRandomSource` = `RandomNumberGenerator.GetInt32`）。
 *   → 本方案**按真随机执行**：不构造任何 ForcedResults，测的就是生产路径本身（比「固定随机源」更强）。
 *   → 另需固定**候选集**：默认库存仅 3/10/50/200（合计 263），1 万次抽样按理论期望会中出约 3400 次实物，
 *     库存必然耗尽 → 奖品退出候选集 → 频率必然塌向 no-prize。故夹具把四个实物/虚拟奖品库存钉高
 *     （见下方 PIN_STOCK），使候选集在整轮抽样中恒为 5 条；候选集构成由 SQL 逐轮枚举核对。
 */
import {
  BASE,
  DB,
  DEFAULT_POOL,
  sql,
  api,
  registerUser,
  drawOnce,
  prizeRows,
  candidateSet,
  setStock,
  setEnabled,
  pinStockAll,
  restorePrizes,
  flushRedisDb,
  normalizedTheory,
  runPool,
  tag
} from './stats-lib.mjs';

const CONCURRENCY = Number(process.env.STATS_CONCURRENCY ?? 6);
const N_TC78 = Number(process.env.STATS_N78 ?? 10000); // 契约下限 10000，不放宽
const N_TC79 = Number(process.env.STATS_N79 ?? 2000); // 契约下限 1000
const N_TC79B = Number(process.env.STATS_N79B ?? 2000); // 契约下限 1000
const DEV_LIMIT_PP = 2.0; // TC-78 / TC-79 契约偏差上限（百分点），不放宽
const PIN_STOCK = 1000000;

const started = Date.now();
const verdicts = [];
const evidence = { base: BASE, db: DB, concurrency: CONCURRENCY, phases: {} };

const log = (...a) => console.log(...a);
const pct = (x) => (x * 100).toFixed(3); // 概率 → 百分数
const pp = (x) => Number(x).toFixed(3); // 偏差已是「百分点」，直接展示（不做二次 ×100）

function verdict(tc, ok, detail) {
  verdicts.push({ tc, status: ok ? 'PASS' : 'FAIL', detail });
  log(`\n[${ok ? 'PASS' : 'FAIL'}] ${tc} :: ${detail}`);
}

/** 偏差表：obs vs 理论（按候选集实时归一）。返回 { rows, maxDev, sumObs }。 */
function deviationTable(rows, codes, counts, total, weightOf = (r) => r.weight) {
  const { theory, totalWeight } = normalizedTheory(rows, codes, weightOf);
  const out = [];
  let maxDev = 0;
  for (const code of codes) {
    const obs = (counts[code] ?? 0) / total;
    const th = theory[code];
    const dev = Math.abs(obs - th) * 100;
    maxDev = Math.max(maxDev, dev);
    out.push({ code, weight: weightOf(rows.find((r) => r.code === code)), obs, th, dev, count: counts[code] ?? 0 });
  }
  return { rows: out, maxDev, sumObs: Object.values(counts).reduce((a, b) => a + b, 0) / total, totalWeight };
}

function printTable(title, tbl, total) {
  log(`\n${title}（样本 n=${total}，候选集权重总和 Σw=${tbl.totalWeight}）`);
  log('| 条目 | 权重 w | 理论概率（w/Σw） | 实测频次 | 实测频率 | 偏差(pp) | ≤2pp |');
  log('| --- | --- | --- | --- | --- | --- | --- |');
  for (const r of tbl.rows) {
    log(
      `| ${r.code} | ${r.weight} | ${pct(r.th)}% | ${r.count} | ${pct(r.obs)}% | ${pp(r.dev)} | ${r.dev <= DEV_LIMIT_PP ? '✔' : '✘'} |`
    );
  }
  log(`实测频率合计 = ${pct(tbl.sumObs)}%（契约要求 100%）；最大偏差 = ${pp(tbl.maxDev)} pp（契约上限 ${DEV_LIMIT_PP} pp）`);
}

/** 以「全局固定概率」假设（wᵢ/100，即忽略候选集剔除）做对照，用于 TC-79b 的口径判别。 */
function globalHypothesisTable(rows, codes, counts, total) {
  const globalTotal = DEFAULT_POOL.reduce((s, r) => s + r.weight, 0); // 100
  const out = [];
  let maxDev = 0;
  for (const code of codes) {
    const w = rows.find((r) => r.code === code)?.weight ?? 0;
    const th = w / globalTotal;
    const obs = (counts[code] ?? 0) / total;
    const dev = Math.abs(obs - th) * 100;
    maxDev = Math.max(maxDev, dev);
    out.push({ code, w, th, obs, dev });
  }
  return { rows: out, maxDev, globalTotal };
}

// ══════════════════════════════════════════════════════════════════════════════
async function main() {
  log('═'.repeat(100));
  log(`TC-78 / TC-79 / TC-79b 概率统计执行 @ ${new Date().toISOString()}`);
  log(`实例 = ${BASE}；库 = ${DB}；并发度 = ${CONCURRENCY}；样本量 = TC-78:${N_TC78} / TC-79:${N_TC79} / TC-79b:${N_TC79B}`);
  log(`契约阈值（不放宽）：单条目偏差 ≤ ${DEV_LIMIT_PP} 个百分点；样本下限 TC-78 ≥ 10000、TC-79 ≥ 1000`);
  log('═'.repeat(100));

  // ── 阶段 0：前置核验 ───────────────────────────────────────────────────────
  log('\n── 阶段 0：前置核验 ──');
  const noAuth = await api('GET', '/prizes');
  log(`未登录 GET /prizes → HTTP ${noAuth.http}（期望 401，实例存活且鉴权生效）`);

  const u = await registerUser(`st_${tag()}`);
  log(`注册用户 ${u.name} → userId=${u.userId}`);
  const quota0 = await api('GET', '/draw/quota', { token: u.token });
  log(`GET /draw/quota → ${JSON.stringify(quota0.json?.data)}`);
  evidence.phases.preflight = { userId: u.userId, quota: quota0.json?.data };
  if ((quota0.json?.data?.dailyLimit ?? 0) < N_TC78) {
    throw new Error(`Draw:DailyLimit=${quota0.json?.data?.dailyLimit} < 样本量 ${N_TC78}，环境变量未生效`);
  }

  let rows = prizeRows();
  const mismatch = DEFAULT_POOL.filter((d) => {
    const r = rows.find((x) => x.code === d.code);
    return !r || r.weight !== d.weight || r.stock !== d.stock || r.enabled !== 1 || r.deleted !== 0;
  });
  if (mismatch.length > 0) {
    log(`⚠ 奖池非默认态（${mismatch.map((m) => m.code).join(',')}），夹具执行 FR-03 默认奖池复位`);
    restorePrizes();
    rows = prizeRows();
  }
  log('奖池快照（库内原文）：');
  for (const r of rows) log(`  id=${r.id} code=${r.code} type=${r.type} weight=${r.weight} stock=${r.stock} enabled=${r.enabled} deleted=${r.deleted}`);

  try {
    // ── 阶段 1：TC-78 ────────────────────────────────────────────────────────
    log('\n── 阶段 1：TC-78（默认奖池 + 真随机，n=' + N_TC78 + '）──');
    pinStockAll(PIN_STOCK);
    rows = prizeRows();
    const cand78 = candidateSet();
    log(`夹具已把实物/虚拟奖品库存钉高：UPDATE PrizeItem SET Stock = ${PIN_STOCK} WHERE Type <> 3`);
    log(`候选集枚举（SQL 口径 IsEnabled=1 AND IsDeleted=0 AND (Type=3 OR Stock>0)）= [${cand78.join(', ')}]（共 ${cand78.length} 条）`);
    const before78 = Object.fromEntries(rows.map((r) => [r.code, r.stock]));

    const t0 = Date.now();
    let done = 0;
    const res78 = await runPool(
      Array.from({ length: N_TC78 }, (_, i) => i),
      async () => {
        const r = await drawOnce(u.token);
        if (++done % 2000 === 0) log(`  …已完成 ${done}/${N_TC78}（${((Date.now() - t0) / 1000).toFixed(1)}s）`);
        return r;
      },
      CONCURRENCY
    );
    const elapsed78 = (Date.now() - t0) / 1000;

    const ok78 = res78.filter((r) => r.http === 200 && r.json?.code === 0);
    const bad78 = res78.filter((r) => !(r.http === 200 && r.json?.code === 0));
    log(`抽奖请求 ${N_TC78} 次，成功 ${ok78.length}，异常 ${bad78.length}，耗时 ${elapsed78.toFixed(1)}s（${(N_TC78 / elapsed78).toFixed(1)} req/s）`);
    if (bad78.length > 0) log(`异常样本（前 10）：${bad78.slice(0, 10).map((r) => `http=${r.http} code=${r.json?.code} err=${r.error ?? '-'}`).join(' | ')}`);

    const id2code = Object.fromEntries(rows.map((r) => [r.id, r.code]));
    const counts78 = {};
    for (const r of ok78) {
      const code = id2code[r.json.data.itemId] ?? `unknown(${r.json.data.itemId})`;
      counts78[code] = (counts78[code] ?? 0) + 1;
    }
    const after78 = Object.fromEntries(prizeRows().map((r) => [r.code, r.stock]));
    const tbl78 = deviationTable(rows, cand78, counts78, ok78.length);
    printTable('TC-78 频率分布', tbl78, ok78.length);
    log('库存变化（钉高后 / 抽样后）——证明不钉库存必然耗尽候选集：');
    for (const r of DEFAULT_POOL) {
      log(`  ${r.code}: ${before78[r.code]} → ${after78[r.code]}（本轮实际中出 ${counts78[r.code] ?? 0} 次；FR-03 默认库存 = ${r.stock}）`);
    }
    const sum100 = Math.abs(tbl78.sumObs - 1) < 1e-9;
    const pass78 = bad78.length === 0 && cand78.length === 5 && tbl78.maxDev <= DEV_LIMIT_PP && sum100;
    evidence.phases.tc78 = { n: ok78.length, bad: bad78.length, elapsedSec: elapsed78, counts: counts78, table: tbl78.rows, maxDev: tbl78.maxDev, sum: tbl78.sumObs };
    verdict(
      'TC-78',
      pass78,
      `n=${ok78.length}（契约 ≥10000）、失败请求=${bad78.length}；候选集=5 条默认条目；各条目频率与归一理论权重最大偏差 = ${pp(tbl78.maxDev)} pp（契约 ≤2 pp）；实测频率合计 = ${pct(tbl78.sumObs)}%（契约 100%）。前置条件偏差：50 原文「固定随机源」不可满足（实现无播种通道，确定性=按用户名强制结果），本轮按**真随机生产路径**执行`,
    );

    // ── 阶段 2：TC-79 ────────────────────────────────────────────────────────
    log('\n── 阶段 2：TC-79（prize-coupon 库存置 0，n=' + N_TC79 + '）──');
    setStock('prize-coupon', 0);
    rows = prizeRows();
    const cand79 = candidateSet();
    log(`夹具：UPDATE PrizeItem SET Stock = 0 WHERE Code = 'prize-coupon'（权重 ${rows.find((r) => r.code === 'prize-coupon').weight}，其余奖品库存仍为 ${PIN_STOCK}）`);
    log(`候选集枚举 = [${cand79.join(', ')}]（共 ${cand79.length} 条；coupon 已剔除）`);

    const t1 = Date.now();
    const res79 = await runPool(Array.from({ length: N_TC79 }, (_, i) => i), () => drawOnce(u.token), CONCURRENCY);
    const elapsed79 = (Date.now() - t1) / 1000;
    const ok79 = res79.filter((r) => r.http === 200 && r.json?.code === 0);
    const bad79 = res79.filter((r) => !(r.http === 200 && r.json?.code === 0));
    const counts79 = {};
    for (const r of ok79) {
      const code = id2code[r.json.data.itemId] ?? `unknown(${r.json.data.itemId})`;
      counts79[code] = (counts79[code] ?? 0) + 1;
    }
    log(`抽奖请求 ${N_TC79} 次，成功 ${ok79.length}，异常 ${bad79.length}，耗时 ${elapsed79.toFixed(1)}s`);
    if (bad79.length > 0) log(`异常样本（前 10）：${bad79.slice(0, 10).map((r) => `http=${r.http} code=${r.json?.code} err=${r.error ?? '-'}`).join(' | ')}`);
    const tbl79 = deviationTable(rows, cand79, counts79, ok79.length);
    printTable('TC-79 频率分布（coupon 出局后按剩余权重归一）', tbl79, ok79.length);
    const couponCount = counts79['prize-coupon'] ?? 0;
    const noPrizeShare = (counts79['no-prize'] ?? 0) / ok79.length;
    log(
      `「谢谢参与」常驻性核验：库存 = ${rows.find((r) => r.code === 'no-prize').stock}（NoPrize 类型豁免库存），实测中出 ${counts79['no-prize'] ?? 0} 次，` +
        `频率 ${pct(noPrizeShare)}%，理论（66/80）= ${pct(66 / 80)}% —— 未因库存 0 被剔除`
    );
    const pass79 =
      bad79.length === 0 &&
      couponCount === 0 &&
      tbl79.maxDev <= DEV_LIMIT_PP &&
      Math.abs(noPrizeShare - 66 / 80) * 100 <= DEV_LIMIT_PP;
    evidence.phases.tc79 = { n: ok79.length, bad: bad79.length, elapsedSec: elapsed79, counts: counts79, candidateSet: cand79, table: tbl79.rows, maxDev: tbl79.maxDev };
    verdict(
      'TC-79',
      pass79,
      `n=${ok79.length}（契约 ≥1000）、失败请求=${bad79.length}；库存置 0 的 prize-coupon 出现 ${couponCount} 次（契约 0 次）；其余条目按剩余权重（Σw=80）归一，最大偏差 = ${pp(tbl79.maxDev)} pp（契约 ≤2 pp）；「谢谢参与」stock=0 仍参与，实测频率 ${pct(noPrizeShare)}%（理论 82.5%）`,
    );

    // ── 阶段 3：TC-79b ───────────────────────────────────────────────────────
    log('\n── 阶段 3：TC-79b（概率口径 = wᵢ / 候选集Σw；候选集随库存 / 启用状态变化，n=' + N_TC79B + '）──');
    // (a) 口径判别：TC-79 的实测分布 vs「全局固定概率」假设
    const g79 = globalHypothesisTable(rows, [...cand79, 'prize-coupon'], counts79, ok79.length);
    log('\n(a) 口径判别（用 TC-79 样本）：候选集实时归一 vs「全局固定概率 w/100」');
    log('| 条目 | 权重 w | 全局固定假设 w/100 | 候选集归一假设 w/Σw(80) | 实测频率 | 与全局假设偏差(pp) | 与候选集假设偏差(pp) |');
    log('| --- | --- | --- | --- | --- | --- | --- |');
    for (const r of g79.rows) {
      const candTh = r.code === 'prize-coupon' ? 0 : r.w / 80;
      log(`| ${r.code} | ${r.w} | ${pct(r.th)}% | ${pct(candTh)}% | ${pct(r.obs)}% | ${pp(r.dev)} | ${pp(Math.abs(r.obs - candTh) * 100)} |`);
    }
    log(`→ 与「全局固定概率」假设的最大偏差 = ${pp(g79.maxDev)} pp（> 2 pp ⇒ 拒绝该假设）；与「候选集实时归一」假设的最大偏差 = ${pp(tbl79.maxDev)} pp（≤ 2 pp ⇒ 接受）`);

    // (b) 启用状态维度：停用 prize-earbuds（库存充足、权重 3），候选集应随之变化
    setStock('prize-coupon', PIN_STOCK);
    setEnabled('prize-earbuds', 0);
    rows = prizeRows();
    const cand79b = candidateSet();
    log(`\n(b) 夹具：prize-coupon 库存恢复 ${PIN_STOCK}，prize-earbuds IsEnabled=0（权重 3、库存 ${rows.find((r) => r.code === 'prize-earbuds').stock}）`);
    log(`候选集枚举 = [${cand79b.join(', ')}]（共 ${cand79b.length} 条；earbuds 因 IsEnabled=0 出局，Σw = ${cand79b.reduce((s, c) => s + rows.find((r) => r.code === c).weight, 0)}）`);

    const t2 = Date.now();
    const res79b = await runPool(Array.from({ length: N_TC79B }, (_, i) => i), () => drawOnce(u.token), CONCURRENCY);
    const elapsed79b = (Date.now() - t2) / 1000;
    const ok79b = res79b.filter((r) => r.http === 200 && r.json?.code === 0);
    const bad79b = res79b.filter((r) => !(r.http === 200 && r.json?.code === 0));
    const counts79b = {};
    for (const r of ok79b) {
      const code = id2code[r.json.data.itemId] ?? `unknown(${r.json.data.itemId})`;
      counts79b[code] = (counts79b[code] ?? 0) + 1;
    }
    log(`抽奖请求 ${N_TC79B} 次，成功 ${ok79b.length}，异常 ${bad79b.length}，耗时 ${elapsed79b.toFixed(1)}s`);
    const tbl79b = deviationTable(rows, cand79b, counts79b, ok79b.length);
    printTable('TC-79b 频率分布（earbuds 停用后按新候选集归一，Σw=97）', tbl79b, ok79b.length);
    const earbudsCount = counts79b['prize-earbuds'] ?? 0;
    const g79b = globalHypothesisTable(rows, [...cand79b, 'prize-earbuds'], counts79b, ok79b.length);
    log(`与「全局固定概率」假设的最大偏差 = ${pp(g79b.maxDev)} pp；与「候选集归一」假设的最大偏差 = ${pp(tbl79b.maxDev)} pp`);
    const pass79b =
      bad79b.length === 0 &&
      earbudsCount === 0 &&
      tbl79b.maxDev <= DEV_LIMIT_PP &&
      g79.maxDev > DEV_LIMIT_PP &&
      g79b.maxDev > DEV_LIMIT_PP;
    evidence.phases.tc79b = {
      globalVsCandidate: { globalMaxDevPp: g79.maxDev, candidateMaxDevPp: tbl79.maxDev },
      enabledDimension: { n: ok79b.length, bad: bad79b.length, counts: counts79b, candidateSet: cand79b, table: tbl79b.rows, maxDev: tbl79b.maxDev, globalMaxDevPp: g79b.maxDev }
    };
    verdict(
      'TC-79b',
      pass79b,
      `口径 = 条目权重 / **候选集**权重总和（实时归一）：(a) 用 TC-79 样本判别，与「全局固定概率 w/100」假设最大偏差 ${pp(g79.maxDev)} pp（拒绝），与候选集归一假设最大偏差 ${pp(tbl79.maxDev)} pp（接受）；(b) 停用 prize-earbuds（n=${ok79b.length}）后候选集由 5 条变为 4 条 [${cand79b.join(', ')}]，earbuds 出现 ${earbudsCount} 次，其余条目最大偏差 ${pp(tbl79b.maxDev)} pp（契约 ≤2 pp）；候选集随库存（阶段 2：5→4）与启用状态（本阶段：4→4，成员置换）实时变化，均经 SQL 枚举核对`,
    );
  } finally {
    // ── 收尾：奖池复位为 FR-03 默认 + 清空本实例专用 Redis 库（db=2）──
    log('\n── 收尾：奖池复位 + Redis db=2 清空 ──');
    restorePrizes();
    const restored = prizeRows();
    const okRestore = DEFAULT_POOL.every((d) => {
      const r = restored.find((x) => x.code === d.code);
      return r && r.weight === d.weight && r.stock === d.stock && r.enabled === 1 && r.deleted === 0;
    });
    log(`奖池已复位为 FR-03 默认值：${okRestore ? '✔ 与默认权重/库存/启用状态逐条一致' : '✘ 复位不一致'}`);
    log(`复位后候选集 = [${candidateSet().join(', ')}]`);
    flushRedisDb();
    log('Redis defaultDatabase=2 已清空');
    evidence.restored = okRestore;
  }

  const failed = verdicts.filter((v) => v.status === 'FAIL');
  log('\n' + '═'.repeat(100));
  log(`汇总：PASS=${verdicts.length - failed.length} / FAIL=${failed.length}（${verdicts.map((v) => `${v.tc}:${v.status}`).join(' ')}）`);
  log(`总耗时 ${((Date.now() - started) / 1000).toFixed(1)}s`);
  log('═'.repeat(100));
  log('\nEVIDENCE_JSON_BEGIN');
  log(JSON.stringify(evidence, null, 2));
  log('EVIDENCE_JSON_END');
  process.exitCode = failed.length === 0 ? 0 : 1;
}

main().catch((e) => {
  console.error('脚本异常：', e);
  process.exitCode = 2;
});
