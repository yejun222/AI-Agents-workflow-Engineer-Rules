#!/usr/bin/env bash
# TC-76 部署验收（R5 / D-06 / 附录 B B1）——确定性配置的「生产 fail fast」验收。
#
# 口径（docs/50-testcases.md TC-76）：
#   步骤 1：生产配置（确定性配置为空或关闭）启动一次 → 正常启动、开关关闭、覆盖项为空；
#   步骤 2：再向配置注入任意确定性覆盖项后启动 → 启动失败（fail fast，宁可起不来）。
# 另核：生产构建产物中不含测试确定性配置节（appsettings.json 的 Draw:Deterministic / Prize:WeightOverrides 为空且关闭）。
#
# 约束：**不修改 src/ 下任何文件**；配置仅经环境变量（.NET `__` 双下划线映射）注入；
#       直接运行既有构建产物（不触发 rebuild，避免与在跑的实例争用 DLL 文件锁）；
#       使用独立端口 5184/5185，不触碰 :5180 / :5173。
#
# 运行（于仓库根目录）：bash tests/e2e/api/tc76-deploy-check.sh
set -u

ROOT="$(cd "$(dirname "$0")/../../.." && pwd)"
OUT="$ROOT/src/backend/src/LuckyDraw.Api/bin/Debug/net10.0"
DLL="$OUT/LuckyDraw.Api.dll"
# 测试专用签名密钥（非真实密钥，仅满足「非 Development/Testing 环境必须提供 ≥32 位密钥」的启动前置）
KEY="e2e-tc76-only-local-test-signing-key-0123456789"
LOGDIR="$ROOT/tests/e2e/logs"
STAMP="$(date +%Y%m%d-%H%M%S)"
LOG="$LOGDIR/tc76-deploy-$STAMP.log"

mkdir -p "$LOGDIR"
: > "$LOG"

echo "=== TC-76 部署验收（fail fast） $(date '+%F %T') ===" | tee -a "$LOG"
echo "DLL=$DLL" | tee -a "$LOG"
ls -la "$DLL" | tee -a "$LOG"

log() { echo "$@" | tee -a "$LOG"; }

# ---------- 前置核验：生产构建产物中的确定性配置节为空且关闭 ----------
log ""
log "--- 前置核验：构建产物 appsettings.json 的确定性配置节 ---"
python - "$OUT/appsettings.json" <<'PY' 2>&1 | tee -a "$LOG"
import json, sys
cfg = json.load(open(sys.argv[1], encoding='utf-8'))
det = cfg.get('Draw', {}).get('Deterministic', {})
ovr = cfg.get('Prize', {}).get('WeightOverrides', {})
print(f"Draw:DailyLimit={cfg.get('Draw', {}).get('DailyLimit')}")
print(f"Draw:Deterministic:Enabled={det.get('Enabled')}  ForcedResults={det.get('ForcedResults')}")
print(f"Prize:WeightOverrides={ovr}")
ok = (det.get('Enabled') is False) and (det.get('ForcedResults') == []) and (ovr == {})
print(f"[{'PASS' if ok else 'FAIL'}] 生产构建产物确定性配置为空且关闭（期望 PASS）")
PY

# ---------- 步骤 1：生产配置基线启动（预期成功） ----------
log ""
log "--- 步骤 1：Production 基线启动（无确定性配置；期望正常启动）---"
BASELINE_LOG="$LOGDIR/tc76-baseline-$STAMP.log"
(
  cd "$OUT" || exit 9
  ASPNETCORE_ENVIRONMENT=Production \
  ASPNETCORE_URLS=http://localhost:5184 \
  Jwt__SigningKey="$KEY" \
  Redis__Configuration=localhost:6379 \
  dotnet LuckyDraw.Api.dll > "$BASELINE_LOG" 2>&1 &
  echo $! > /tmp/tc76.pid
)
PID="$(cat /tmp/tc76.pid)"
CODE="000"
for _ in $(seq 1 45); do
  CODE="$(curl -s -o /dev/null -w '%{http_code}' http://localhost:5184/api/v1/prizes 2>/dev/null || echo 000)"
  [ "$CODE" != "000" ] && [ -n "$CODE" ] && break
  sleep 1
done
LISTEN="$(grep -c 'Now listening on' "$BASELINE_LOG" 2>/dev/null || echo 0)"
kill "$PID" 2>/dev/null
sleep 2
log "基线启动：HTTP=$CODE（期望 401=服务已在监听且未认证）、'Now listening on' 行数=$LISTEN（期望 ≥1）"
if [ "$CODE" = "401" ] && [ "$LISTEN" -ge 1 ]; then
  log "[PASS] 步骤 1 生产配置基线正常启动"
else
  log "[FAIL] 步骤 1 生产配置基线未正常启动（详见 $BASELINE_LOG）"
fi
log "基线启动日志尾部："
tail -6 "$BASELINE_LOG" 2>/dev/null | tee -a "$LOG"

# ---------- 步骤 2：注入确定性配置 → 预期 fail fast ----------
run_fail_case() {
  # $1=用例名；其余=注入的环境变量
  local name="$1"; shift
  local clog="$LOGDIR/tc76-failfast-${name}-$STAMP.log"
  (
    cd "$OUT" || exit 9
    ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_URLS=http://localhost:5185 \
    Jwt__SigningKey="$KEY" \
    Redis__Configuration=localhost:6379 \
    "$@" \
    dotnet LuckyDraw.Api.dll > "$clog" 2>&1
    echo "EXITCODE=$?" >> "$clog"
  )
  local exit_code
  exit_code="$(grep -o 'EXITCODE=[0-9]*' "$clog" | tail -1 | cut -d= -f2)"
  local refused
  refused="$(grep -c '应用拒绝启动' "$clog" 2>/dev/null || echo 0)"
  log ""
  log "--- 步骤 2（$name）：exit=$exit_code、日志含「应用拒绝启动」次数=$refused（期望 exit≠0 且 ≥1）---"
  grep -m2 -E '拒绝启动|OptionsValidationException' "$clog" | cut -c1-220 | tee -a "$LOG"
  if [ "$exit_code" != "0" ] && [ "$refused" -ge 1 ]; then
    log "[PASS] 步骤 2（$name）fail fast 生效"
  else
    log "[FAIL] 步骤 2（$name）未 fail fast（详见 $clog）"
  fi
}

run_fail_case "deterministic-enabled"            Draw__Deterministic__Enabled=true
run_fail_case "forced-results"                   Draw__Deterministic__ForcedResults__0__UserName=tc76prod Draw__Deterministic__ForcedResults__0__PrizeItemCode=prize-mug
run_fail_case "weight-overrides"                 Prize__WeightOverrides__prize-mug=100

echo "" | tee -a "$LOG"
echo "=== 证据：$LOG ===" | tee -a "$LOG"
