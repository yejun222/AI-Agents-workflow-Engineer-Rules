#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Claude Code 多角色配置一致性校验（tools/check-config.py）

用法：python3 tools/check-config.py
改动角色 / 命令 / 文档后运行；全部 PASS 才可交付。stdlib only，无外部依赖。
"""
import json
import re
import subprocess
import sys
from pathlib import Path

# Windows GBK 控制台无法编码 ✓/✗：全部 PASS 时仍会因 UnicodeEncodeError 以 exit 1 结束（误报失败），统一按 UTF-8 输出
if sys.platform == "win32" and hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

ROOT = Path(__file__).resolve().parent.parent
AGENTS = ROOT / ".claude" / "agents"
COMMANDS = ROOT / ".claude" / "commands"
DOCS = ROOT / "docs"

# 布局前置断言：脚本期望 <项目根>/tools/check-config.py，.claude/agents、.claude/commands、docs 与脚本同级布局
for _p in (AGENTS, COMMANDS, DOCS):
    if not _p.exists():
        sys.exit(f"路径不存在: {_p} —— 请确认目录布局（见 README 安装章节）："
                 f"脚本期望 <项目根>/.claude/agents、<项目根>/.claude/commands 与 <项目根>/docs；"
                 f"不要把 tools/ 放进 .claude/ 里")

ROLE_FILES = ["product-manager.md", "prototype-designer.md", "software-architect.md",
              "engineer.md", "code-reviewer.md", "test-designer.md", "test-executor.md"]
CMD_FILES = ["feature.md", "prd.md", "proto.md", "arch.md", "impl.md", "review.md", "test.md"]

# artifacts.md 登记：产物 → 读取者（唯一权威）
READERS = {
    "00-brief": ["product-manager", "prototype-designer", "test-designer", "test-executor"],
    "10-prd": ["prototype-designer", "software-architect", "engineer", "code-reviewer", "test-designer", "test-executor"],
    "20-prototype": ["software-architect", "engineer", "test-designer", "test-executor"],
    "30-architecture": ["engineer", "code-reviewer", "test-designer", "test-executor"],
    "40-changelog": ["engineer", "code-reviewer", "test-executor"],
    "50-testcases": ["test-executor", "engineer"],
    "51-defects": ["engineer", "test-executor", "主对话"],
    "52-qa-report": ["主对话", "test-executor"],
    "60-review": ["engineer", "test-executor", "主对话"],
}

CONCLUSIONS = ["可发布", "有条件发布", "有条件发布（含残留缺陷）", "不可发布（待确认接受）", "不可发布"]

failures = []
checks = []


def check(name, ok, detail=""):
    checks.append((name, ok, detail))
    if not ok:
        failures.append(f"{name}: {detail}")


def read(path):
    return path.read_text(encoding="utf-8") if path.exists() else None


def parse_frontmatter(text):
    m = re.match(r"^---\n(.*?)\n---\n", text, re.S)
    if not m:
        return None
    fm, body = m.group(1), m.group(0)
    return fm, body


def get_field(fm, key):
    m = re.search(rf"^{key}:\s*(.+)$", fm, re.M)
    return m.group(1).strip() if m else None


# ---- 1. 角色文件结构与 frontmatter ----
for fname in ROLE_FILES:
    path = AGENTS / fname
    check(f"agent 存在: {fname}", path.exists())
    if not path.exists():
        continue
    text = read(path)
    fm = parse_frontmatter(text)
    check(f"frontmatter: {fname}", fm is not None, "缺 --- 包裹的 frontmatter")
    if fm is None:
        continue
    fm, body = fm
    name = get_field(fm, "name")
    desc = get_field(fm, "description")
    tools = get_field(fm, "tools")
    model = get_field(fm, "model")
    turns = get_field(fm, "maxTurns")
    color = get_field(fm, "color")
    stem = path.stem
    check(f"name=文件名: {fname}", name == stem, f"frontmatter name={name}")
    check(f"description 双引号: {fname}", desc and desc.startswith('"') and desc.endswith('"'))
    check(f"description 含互斥指向: {fname}", desc and "→" in desc, "缺 '不负责 → 其他角色'")
    check(f"model 存在: {fname}", bool(model))
    check(f"maxTurns 存在: {fname}", bool(turns))
    check(f"color 存在: {fname}", bool(color))
    if tools:
        tool_list = [t.strip() for t in tools.split(",")]
        banned = [t for t in tool_list if t in ("Agent", "Task", "AskUserQuestion")]
        check(f"tools 无递归/提问工具: {fname}", not banned, f"发现 {banned}")

# ---- 1.5 写入者必须具备 Write/Edit 工具（防"声明可写但无写权限"的结构性矛盾，如 code-reviewer 缺 Write 复发）----
WRITERS_BY_ROLE = {
    "product-manager.md": "docs/10-prd.md", "prototype-designer.md": "docs/20-prototype.html",
    "software-architect.md": "docs/30-architecture.md", "engineer.md": "docs/40-changelog.md",
    "code-reviewer.md": "docs/60-review.md", "test-designer.md": "docs/50-testcases.md",
    "test-executor.md": "docs/51-defects.md / 52-qa-report.md",
}
for fname, artifact in WRITERS_BY_ROLE.items():
    path = AGENTS / fname
    if not path.exists():
        continue
    fm = parse_frontmatter(read(path))
    if fm is None:
        continue
    fm, _ = fm
    tools = get_field(fm, "tools") or ""
    has_writer_tool = any(t.strip() in ("Write", "Edit") for t in tools.split(","))
    check(f"写入者具备写工具: {fname}", has_writer_tool,
          f"声明可写 {artifact} 但 tools 无 Write / Edit")

# ---- 2. 重名检查 ----
names = [get_field(parse_frontmatter(read(AGENTS / f))[0], "name") if (AGENTS / f).exists() else None for f in ROLE_FILES]
dups = {n for n in names if n and names.count(n) > 1}
check("agent 无重名", not dups, f"重名: {dups}")

# ---- 3. 三向对齐：artifacts 读取者 == 角色输入声明 ----
# 写入者（生产者）自读自写为隐式，token 出现于任一章节即可；
# 非写入者必须在其「输入要求」章节显式声明读取该产物。
WRITERS = {
    "00-brief": "主对话", "10-prd": "product-manager", "20-prototype": "prototype-designer",
    "30-architecture": "software-architect", "40-changelog": "engineer",
    "50-testcases": "test-designer", "51-defects": "test-executor",
    "52-qa-report": "test-executor", "60-review": "code-reviewer",
}
for artifact, readers in READERS.items():
    for r in readers:
        if r == "主对话":
            continue
        path = AGENTS / f"{r}.md"
        if not path.exists():
            check(f"读取者角色存在: {r}", False, f"{artifact} 的读取者 {r} 无角色文件")
            continue
        text = read(path)
        tokens = {"00-brief": "00-brief", "10-prd": "10-prd", "20-prototype": "20-prototype",
                  "30-architecture": "30-architecture", "40-changelog": "40-changelog",
                  "50-testcases": "50-testcases", "51-defects": "51-defects",
                  "52-qa-report": "52-qa-report", "60-review": "60-review"}
        if r == WRITERS[artifact]:
            ok = tokens[artifact] in text
            scope_desc = "正文"
        else:
            m = re.search(r"##\s*输入要求(.*?)(?=\n##\s|\Z)", text, re.S)
            scope = m.group(1) if m else text
            ok = tokens[artifact] in scope
            scope_desc = "「输入要求」章节"
        check(f"三向对齐 {artifact}←{r}", ok, f"{r}.md 的{scope_desc}未声明读取 {artifact}")

# ---- 4. 结论字符串一致性 ----
sm = read(DOCS / "state-machine.md") or ""
feat = read(COMMANDS / "feature.md") or ""
tex = read(AGENTS / "test-executor.md") or ""
for c in CONCLUSIONS:
    check(f"结论在 state-machine: {c}", c in sm)
    check(f"结论在 test-executor: {c}", c in tex)
# 子串假通过专项：裸「有条件发布」必须独立出现过（不只作为「有条件发布（含残留缺陷）」的前缀）
check("test-executor 定义裸「有条件发布」", "「有条件发布」" in tex, "test-executor 缺独立的「有条件发布」定义（接受转达后落盘）")
check("test-executor 定义「有条件发布（含残留缺陷）」", "「有条件发布（含残留缺陷）」" in tex)
# 反向：test-executor / feature 的「结论」引号串必须都有定义（不得自造/复制未定义结论）
# 只检查以结论词开头的「」串（如「不可发布（待确认接受）」）；硬规则名如「致命 / 严重未闭环即不可发布」不以结论词开头，跳过
for name, text in (("test-executor", tex), ("feature", feat)):
    for q in set(re.findall(r"「([^」]+)」", text)):
        if re.match(r"^(可发布|有条件发布|不可发布)", q) and q not in CONCLUSIONS:
            check(f"{name} 结论串合法: {q}", False, f"出现未定义结论串: {q}")
check("feature 引用 state-machine", "state-machine.md" in feat)
check("test-executor 引用 state-machine", "state-machine.md" in tex)

# ---- 5. 命名残留 ----
for f in (ROOT / "claude-roles").glob("**/*.md") if False else []:
    pass
all_md = list((AGENTS).glob("*.md")) + list((COMMANDS).glob("*.md")) + list((DOCS).glob("*.md")) + [ROOT / "README.md"]
leftover = []
for p in all_md:
    t = read(p)
    if t and "需求简报" in t:
        leftover.append(str(p))
check("无 '需求简报' 残留", not leftover, str(leftover))
gone = [str(p) for p in all_md if read(p) and "tester.md" in read(p)]
check("无 tester.md 残留引用", not gone, str(gone))

# ---- 6. 命令文件 ----
for fname in CMD_FILES:
    path = COMMANDS / fname
    check(f"命令存在: {fname}", path.exists())
    if not path.exists():
        continue
    text = read(path)
    fm = parse_frontmatter(text)
    check(f"命令 frontmatter: {fname}", fm is not None)
    if fm is None:
        continue
    fm, body = fm
    check(f"命令无 name 字段: {fname}", get_field(fm, "name") is None, "commands 文件不应有 name")
    check(f"命令 allowed-tools 含 Agent: {fname}", "Agent" in (get_field(fm, "allowed-tools") or ""))
    check(f"命令 description 存在: {fname}", bool(get_field(fm, "description")))

# ---- 7. README 与产物登记表 ----
readme = read(ROOT / "README.md") or ""
art = read(DOCS / "artifacts.md") or ""
for r in ROLE_FILES:
    check(f"README 角色清单含 {r}", r in readme)
for a in ("10-prd.md", "20-prototype.html", "30-architecture.md", "40-changelog.md",
          "50-testcases.md", "51-defects.md", "52-qa-report.md", "60-review.md", "00-brief.md"):
    check(f"artifacts 登记含 {a}", a in art)

# ---- 8. 加固断言（对抗性测试盲区清零：行0限定 / REV体系 / feature硬约束 / README旁路）----
sm = read(DOCS / "state-machine.md") or ""
feat = read(COMMANDS / "feature.md") or ""
tex = read(AGENTS / "test-executor.md") or ""
cr = read(AGENTS / "code-reviewer.md") or ""
readme = read(ROOT / "README.md") or ""
art = read(DOCS / "artifacts.md") or ""

# T4：行 0 必须含「且 F4 = 否」精确限定（不可用 "否" in row——行内「是否已明确指示」含「否」会假阴性）
check("T4 行0 含且F4=否限定", re.search(r"\| 0 \|.*且\s*\*\*F4\s*=\s*否\*\*", sm) is not None,
      "state-machine 行 0 缺少「且 F4 = 否」，行 5 将成死分支")
# T4：行 5 判定条件完整性
check("T4 行5 含F4已指示", re.search(r"\| 5 \|.*F4\s*=\s*已指示", sm) is not None,
      "state-machine 行 5 缺少 F4=已指示 条件")
# T6：artifacts 必须登记 REV 编号行
check("T6 artifacts 登记 REV 行", re.search(r"^\| REV \|", art, re.M) is not None,
      "artifacts ID 体系缺少 REV 行")
# T7：code-reviewer 必须含具体编号格式（仅有 REV-xx 泛称不足以防漂移）
check("T7 code-reviewer REV-01 具体格式", "REV-01" in cr,
      "code-reviewer 缺少 REV-01… 具体编号格式约定")
# T7：状态枚举三项齐全
for st in ("`未闭环`", "`已闭环`", "`已接受（用户决断）`"):
    check(f"T7 code-reviewer 状态枚举含 {st}", st in cr, f"缺状态 {st}")
# T7：重跑必须回写状态
check("T7 重跑回写状态", "必须回写本文件逐条更新状态" in cr, "code-reviewer 缺重跑回写状态规则")
# T7：禁止自填「已接受（用户决断）」
check("T7 禁止自填已接受", "不由本角色填写" in cr, "code-reviewer 未声明「已接受」不由本角色填写")
# T7：具备 Write/Edit（与第 1.5 节互补，防 code-reviewer 单独回归）
check("T7 code-reviewer 具备 Write", "Write" in (get_field(parse_frontmatter(cr)[0], "tools") or ""),
      "code-reviewer tools 缺 Write")
# T8：README 60-review 去向含「已接受（用户决断）」旁路
check("T8 README 60去向含旁路", "已接受（用户决断）" in readme and "60-review 的去向" in readme,
      "README 60-review 去向未反映已接受旁路")
# T8：README 区分三条接受路径（REV / BUG / 覆盖缺口）
check("T8 README 区分三条接受路径", "三条接受路径不要混" in readme and "REV-xx" in readme and "BUG-xx" in readme,
      "README 未区分 REV / BUG / 覆盖缺口 三条接受路径")
# T8：覆盖缺口接受路径须在 README 与状态机修复循环出口双向登记
# 锚点用「既不是 REV 也不是 BUG」而非「覆盖缺口 / 行 3.5」——后者在回边图里也出现，
# 删掉三条清单中的条目后仍会被图那行满足（假通过，已实测）。
check("T8 README 覆盖缺口路径在三条清单内",
      "既不是 REV 也不是 BUG" in readme and "行 3.5" in readme,
      "README 的「三条接受路径」清单缺覆盖缺口条目（状态机行 3.5）")
check("T8 state-machine 接受覆盖缺口出口", "「**接受覆盖缺口**」" in sm,
      "state-machine 修复循环 c 缺「接受覆盖缺口」出口 —— 有判定行却无入口，F6 永远无法触发（死分支）")
# T8：60-review 不流向测试设计
check("T8 README 不流向测试设计", "不流向测试设计" in readme, "README 未声明 60 不流向测试设计")
# T8：审查回边说明
check("T8 README 含审查回边", "审查回边" in readme, "README 缺审查回边说明")
# T3：feature「唯一枚举 + 不自行发明」硬约束（不得挂 or 旁路，单独断言）
check("T3 feature 唯一枚举硬约束", "结论字符串只使用状态机的唯一枚举，不自行发明" in feat,
      "feature 缺「唯一枚举 / 不自行发明」硬约束")
# T3：状态机行 5 只针对 51 缺陷
check("T3 feature 行5只针对51缺陷", "只针对 51 缺陷" in feat or "只针对 **51 缺陷**" in feat,
      "feature 未声明行 5 只针对 51 缺陷")
# T3：判定条件不在本命令重复定义
check("T3 feature 不重复定义判定条件", "判定条件不在本命令重复定义" in feat,
      "feature 未声明不重复定义判定条件")

# ---- 9. 评审加固（P0-1 出口解耦 / P1-1 传播矩阵 / P1-2 软约束 / P1-3 快照）----
sm = read(DOCS / "state-machine.md") or ""
feat = read(COMMANDS / "feature.md") or ""
art = read(DOCS / "artifacts.md") or ""
td = read(AGENTS / "test-designer.md") or ""
tex = read(AGENTS / "test-executor.md") or ""
pm = read(AGENTS / "product-manager.md") or ""

check("T9 循环出口与级别解耦", "出口与缺陷级别解耦" in sm, "state-machine 修复循环 c 缺出口解耦规则（P0-1 复发）")
check("T9 修复按触发原因分派", "按触发原因分派" in sm, "state-machine 修复循环 a 缺按原因分派规则")
check("T9 矩阵 00-brief→10-prd", re.search(r"00-brief 变更.*10-prd", art) is not None, "artifacts 矩阵 00-brief 行未含 10-prd")
check("T9 矩阵 51→52 行", re.search(r"51-defects 变更.*52-qa-report", art) is not None, "artifacts 矩阵缺 51→52 行")
check("T9 test-designer 软约束标注", "软约束" in td, "test-designer 未标注禁读为软约束")
for label, text in (("feature", feat), ("product-manager", pm), ("test-designer", td), ("test-executor", tex)):
    check(f"T9 {label} 含快照规则", "原始需求快照" in text, f"{label} 缺跳过归档的原始需求快照规则")

# ---- 10. 第三轮评审加固（用例来源声明 / 评审门放行词 / 核验日期 / CI E2E+OpenAPI / Microting 回迁 / 并行优化记录）----
td = read(AGENTS / "test-designer.md") or ""
feat = read(COMMANDS / "feature.md") or ""
readme = read(ROOT / "README.md") or ""
devspec = read(DOCS / "development-spec.md") or ""
cc = read(DOCS / "config-checklist.md") or ""

check("T10 test-designer 用例来源声明", "用例来源" in td and "仅需求文档" in td,
      "test-designer 输出要求缺「用例来源 = 仅需求文档」头部声明")
check("T10 feature 评审门放行词", "确认继续" in feat,
      "feature 评审门缺明确放行词「确认继续」")
check("T10 README 平台事实核验日期", "核验日期" in readme,
      "README 平台事实节缺核验日期标注")
sec121 = re.search(r"### 12\.1 流水线.*?(?=### 12\.2|\Z)", devspec, re.S)
sec121 = sec121.group(0) if sec121 else ""
check("T10 12.1 含 E2E", "E2E" in sec121, "development-spec 12.1 缺 E2E 步骤")
check("T10 12.1 含 OpenAPI 契约校验", "OpenAPI" in sec121, "development-spec 12.1 缺 OpenAPI 契约校验")
check("T10 Microting 回迁条件", re.search(r"Microting\.EntityFrameworkCore\.MySql.*?回迁条件", devspec, re.S) is not None,
      "development-spec 2.2 Microting 行缺回迁条件")
check("T10 并行优化已记录", "test-designer 与 engineer 并行" in cc,
      "config-checklist 缺 test-designer/engineer 并行优化记录（E 节）")

# ---- 11. 状态机行 3.5（覆盖缺口接受路径）守护 ----
# 行 3.5 的约束与行 0.5 / 行 5 同源：位置错了就永远不可达或抢走别的行，条件漏了就静默降级。
sm = read(DOCS / "state-machine.md") or ""
tex = read(AGENTS / "test-executor.md") or ""

check("T11 定义 F6", re.search(r"\*\*F6\*\*", sm) is not None,
      "state-machine 未定义事实变量 F6（覆盖缺口接受路径的输入事实）")

row_pos = {m.group(1): m.start() for m in
           re.finditer(r"^\|\s*\*{0,2}([0-9]+(?:\.[0-9]+)?)\*{0,2}\s*\|", sm, re.M)}
check("T11 行3.5 存在", "3.5" in row_pos,
      f"判定表未找到行 3.5（已找到行号：{sorted(row_pos)}）")

m35 = re.search(r"^\|\s*\*{0,2}3\.5\*{0,2}\s*\|.*$", sm, re.M)
row35 = m35.group(0) if m35 else ""
check("T11 行3.5 含 F6=已转达", re.search(r"F6\s*=\s*已转达", row35) is not None,
      "行 3.5 缺少「F6 = 已转达」条件（否则它不是接受路径，只是一句废话）")
check("T11 行3.5 含无未闭环致命/严重前提",
      re.search(r"无未闭环致命\s*/\s*严重", row35) is not None,
      "行 3.5 缺少「无未闭环致命 / 严重」前提 —— 省略后 "
      "F1=是 + 覆盖不达标 + F6=已转达 + F4=已指示 会被它抢先判为「有条件发布」，"
      "使行 5「有条件发布（含残留缺陷）」沦为死分支、残留致命/严重缺陷被降级静默吸收")
check("T11 行3.5 复用既有结论枚举",
      "有条件发布" in row35 and "含残留缺陷" not in row35,
      "行 3.5 的结论须复用既有枚举成员「有条件发布」，不得自造，也不得抢占「有条件发布（含残留缺陷）」")
if all(k in row_pos for k in ("3", "3.5", "4")):
    check("T11 行3.5 位于行3之后", row_pos["3"] < row_pos["3.5"],
          "行 3.5 须位于行 3 之后 —— 否则「未转达接受的一般缺陷」会被它一并放行")
    check("T11 行3.5 位于行4之前", row_pos["3.5"] < row_pos["4"],
          "行 3.5 须位于行 4 之前 —— 行 4 的条件是其真超集，置于其后将永远不可达")
else:
    check("T11 行号 3/3.5/4 齐备", False, f"未同时找到行 3 / 3.5 / 4（已找到：{sorted(row_pos)}）")
check("T11 test-executor 声明行3.5不在建议范围",
      "行 3.5" in tex and "不在你的建议范围内" in tex,
      "test-executor 未声明行 3.5 须主对话转达 F6 后才由其在重调度中落盘")

# ---- 12. 产物保护：每评审门一次 wip 提交（防「交付前全裸奔」+ 证据强度降级）----
# 背景：Step 0–8 之间产物全为未跟踪文件 —— 一次错误 Write 即无从恢复，
# 且审查角色无法用 diff 取证，只能自认「旁证」（实测见 docs/60-review.md:265/398/411）。
feat = read(COMMANDS / "feature.md") or ""
rp = read(DOCS / "role-protocol.md") or ""

check("T12 feature 每门 wip 提交规则", "wip(step-" in feat and "每个评审门通过后" in feat,
      "feature 缺「每个评审门通过后做 wip 提交」的产物保护规则 —— "
      "产物在交付前全为未跟踪文件，一次错误 Write 即抹掉数小时工作且无从恢复")
check("T12 feature 开工建分支与基线 SHA", "git checkout -b feat/" in feat and "记录基线 SHA" in feat,
      "feature Step 0 缺「建分支 + 记录基线 SHA」—— 没有基线则 Step 8 无法 reset --soft 重整")
check("T12 feature reset --soft 重整", "git reset --soft <基线SHA>" in feat,
      "feature Step 8 缺 reset --soft 交付单元重整步骤（wip 历史不重整就会混进交付历史）")
check("T12 feature 禁用 rebase -i", "不得用 `git rebase -i`" in feat,
      "feature 未声明禁止用 git rebase -i 重整 —— 本环境不支持交互式标志，会让后来者踩空")
check("T12 feature wip 历史核验", "--grep='wip(step-'" in feat,
      "feature Step 8 缺 wip 历史条数核验 —— 漏提交不阻断交付，但必须如实登记，"
      "否则「某一步的产物当时长什么样」永久失据")
check("T12 role-protocol 取证优先级", "能用 diff 就别用旁证" in rp,
      "role-protocol 缺「能用 diff 就别用旁证」取证优先级规则 —— "
      "有版本控制却仍用 mtime / 锚点旁证，等于白拿 wip 提交")
check("T12 role-protocol 子代理仍禁止提交", "不提交代码" in rp and "不构成你可以自行提交的先例" in rp,
      "role-protocol 的「角色不得提交」在新增 wip 规则后有被冲掉的风险（子代理会自行提交）")

# ---- 13. 实例启动 / 就绪检查（防「伪就绪检查」复发：判据绕过真正会坏的那条路径）----
# 背景：ContentRoot 错 → appsettings 不加载 → 密钥为空 → 全接口 500 且日志 0 字节；
# 而 401 类判据在不带 token 时根本不进入签名校验，故障态与正常态给出完全相同的信号。
check("T13 §5 发布目录启动", "必须先进到发布目录再启动" in rp and "ContentRoot" in rp,
      "role-protocol §5 缺「必须先进到发布目录再启动」—— dotnet <相对路径>.dll 会把 cwd 当 ContentRoot")
check("T13 §5 启动模板唯一且 cwd 锁死", "收敛为唯一模板" in rp and "cwd: PUBLISH_DIR" in rp,
      "role-protocol §5 缺唯一的启动模板（含 cwd 锁死）—— 逐份复制的启动代码正是 cwd 漏写反复复发的原因")
check("T13 §5 就绪路径按项目定", "签发路径按项目定" in rp and "注册或登录" in rp and "auth/register" in rp,
      "role-protocol §5 把就绪检查的签发路径钉死成单一样例（旧文为 login）——"
      "照抄者会漏掉自己项目里真正会坏的那条路径")
check("T13 §5 伪就绪检查机理", "根本不会进入签名校验" in rp and "伪就绪检查" in rp,
      "role-protocol §5 缺「401 类判据为什么测不出配置缺失」的机理说明 ——"
      "只禁 health 而不给机理，新项目无法自行识破其它形态的伪就绪检查")
check("T13 §5 可迁移原则", "依赖配置最深" in rp,
      "role-protocol §5 缺「就绪判据必须走依赖配置最深的路径」这条可迁移原则")
check("T13 checklist 规则与脚本一致性核对项", "伪就绪检查" in cc and "门禁判据未改" in cc,
      "config-checklist 缺「§5 规则与 tests/e2e 实际代码是否一致」的人工核对项 ——"
      "实测出现过规则已改而脚本就绪门禁未改（签发调用排在门禁之后）")

# ---- 14. 前端命令的环境前提（路径含 & 时 npm run * 必失败；规范双落点措辞须逐字一致）----
# 背景：CLAUDE.md 与 development-spec.md 都是权威命令表，只改一处即制造新的不一致。
claude_md = read(ROOT / "CLAUDE.md") or ""
spec = read(DOCS / "development-spec.md") or ""
_env_note = "仓库绝对路径含 `&`（或空格）时"
check("T14 命令表环境前提（两处逐字一致）", _env_note in claude_md and _env_note in spec,
      "CLAUDE.md 与 development-spec.md 的前端命令表缺「路径含 `&` 时 npm run * 必失败」的环境前提，"
      "或两处措辞不一致（双落点漂移会让后来者只改一处）")
check("T14 环境前提的边界声明", "`npm ci` 不经 script-shell" in claude_md and "`npm ci` 不经 script-shell" in spec,
      "环境前提缺「npm ci 不经 script-shell / Linux / CI 不受影响」——只给失败条件不给边界，会把特例误读成通用禁令")

# ---- 15. 跨文件 ID 引用完整性（防「规范正文引用未登记的号码」）----
# 背景：1203/1204 早已被 development-spec 正文引用，却从未登记进 error-codes.md ——「补录历史欠账」。
# 引用的号码与注册表之间没有任何机制强制一致，只能靠护栏。
art = read(DOCS / "artifacts.md") or ""
ec = read(DOCS / "error-codes.md") or ""

_referenced = set()
for _m in re.finditer(r"(?:code|如)\s*`?\s*(\d{4})(?:\s*/\s*(\d{4}))?", spec, re.I):
    _referenced.add(_m.group(1))
    if _m.group(2):
        _referenced.add(_m.group(2))
_referenced = {c for c in _referenced if int(c) >= 1000}
_registered = set(re.findall(r"^\|\s*(\d{4})\s*\|", ec, re.M))
_dangling = sorted(_referenced - _registered)

check("T15 规范引用的业务错误码均已登记", not _dangling,
      f"development-spec 正文引用了未在 error-codes.md 登记的号码: {_dangling or '（提取为空，请检查提取正则）'} ——"
      "号码引用与注册表之间没有强制一致的机制，只能靠本断言（历史案例：1203/1204 长期悬空）")
check("T15 错误码提取非空（防断言空转）", bool(_referenced),
      "未能从 development-spec 提取到任何业务错误码引用 —— 提取正则已失效，本组断言退化为空转（假绿）")
check("T15 OBS 编号命名空间规则已声明", "按产物命名空间限定" in art and "51:OBS-nn" in art,
      "artifacts.md 未声明 OBS 编号「按产物命名空间限定」—— 实测同一编号在 51/52 指向不同内容且无人发现")
check("T15 未审/未执行项须有责任人与时点", "责任人 + 计划执行时点" in art,
      "artifacts.md 未要求「未审 / 未执行项必须含责任人 + 计划执行时点」——"
      "只声明「如实未审」不足以让它被处理（实测有项跨 4 个版本仍挂着）")

# ---- 16. 双落点一致性（T14 范式的推广：同一事实禁止多落点，不可避免时必须由护栏绑死）----
# 背景：本仓库跨领域的六个坑是同一个根因 —— 同一事实存在第二落点而两者无任何强制一致机制。
# 此处把「精简版 ↔ 全量版」的措辞逐字绑定，新增双落点事实时照此追加锚句。
_DUAL_ANCHORS = [
    ("重试判定 / 事务边界统一分类", "必须在事务边界统一分类"),
    ("超时配置互相约束（4.4）", "服务端同类超时"),
    ("安全配置单一来源 + fail-fast（8.7）", "安全敏感配置只允许一个取值来源"),
    ("引用用稳定 ID 不用行号（13.4）", "引用一律用稳定 ID，禁止用行号"),
    ("断言辨别力：禁止固化当前行为（7.5）", "禁止把当前行为固化成期望"),
    ("契约分支必须可构造（6.6）", "每个分支都必须可构造"),
    ("模板层必须有组件渲染测试（7.2）", "组件渲染测试（模板层）必须有"),
    ("路径含 # 时前端单测全量失败", "仓库绝对路径含 `#` 时"),
    ("长驻实例锁死 bin（命令表）", "长驻开发实例会锁死"),
    ("E2E 首次必做装浏览器内核（命令表）", "浏览器内核不在 `npm ci` 范围内"),
]
for _label, _anchor in _DUAL_ANCHORS:
    check(f"T16 双落点逐字一致: {_label}",
          _anchor in claude_md and _anchor in spec,
          f"锚句「{_anchor}」未在 CLAUDE.md 与 development-spec.md 同时出现 ——"
          "双落点措辞漂移（只改一处即制造新的不一致），两处必须逐字同步")
check("T16 新增规范条款存在性: 7.5 断言辨别力", "### 7.5 断言辨别力" in spec,
      "development-spec 缺 §7.5 断言辨别力（断言只允许加强 / 必须给出辨别力依据 / 禁止把当前行为固化成期望）")
check("T16 新增规范条款存在性: 6.6 契约表达与可测性", "### 6.6 契约表达与可测性" in spec,
      "development-spec 缺 §6.6 契约表达与可测性（键值表优先 / 分支可构造 / 通用错误响应体形态 / 架构五问）")
check("T16 新增规范条款存在性: 8.7 安全配置 fail-fast", "### 8.7 安全配置的加载与失效" in spec,
      "development-spec 缺 §8.7 安全配置的加载与失效（单一来源 + 启动期 fail-fast）")
check("T16 新增规范条款存在性: 14.1 跨层超时预算", "### 14.1 跨层超时预算" in spec,
      "development-spec 缺 §14.1 跨层超时预算（四层数值必须成表，不能各层各配）")
check("T16 新增规范条款存在性: 13.4 文档与引用", "### 13.4 文档与引用" in spec,
      "development-spec 缺 §13.4 文档与引用（稳定 ID / 只写应然 / 引用前实测）")
check("T16 role-protocol 证据卫生节", "## 7. 证据卫生" in rp,
      "role-protocol 缺「证据卫生」节（证据落在框架清理范围外 / 文件名唯一 / 引用前核验 / 时间戳脚本生成 / 证据强度分级）")
check("T16 role-protocol 长驻进程锁输出目录", "长驻进程会锁死构建输出目录" in rp,
      "role-protocol 缺「长驻进程锁死 bin/ → 一律写临时 OutputPath」的处置约定 ——"
      "实测该锁使 E2E 复跑前置条件跨多轮无法满足，整条验证链被堵死")
check("T16 role-protocol 同一事实禁止多落点", "同一事实禁止多落点" in rp,
      "role-protocol §1 缺「同一事实禁止多落点，不可避免时必须由护栏绑死」通则")

# ---- 17. 启动模板唯一性（把「规则」与「实现」绑定，而非只写散文）----
# 背景：role-protocol §5 早就写了「启动逻辑收敛为唯一模板」，但**只约束了 Playwright spec 侧**，
# 手工起实例这条最容易复现 ContentRoot 错误（cwd 由人的位置决定）的路径**仍然裸奔**；
# 这正是 config-checklist §D 警告的「规则已改而门禁判据未改」。故把该规则落到结构断言上。
_LIB = ROOT / "tests" / "e2e" / "lib" / "launch-api.mjs"
_cov = read(ROOT / "tests" / "e2e" / "coverage.spec.ts") or ""
check("T17 唯一启动模板文件存在", _LIB.exists(),
      "tests/e2e/lib/launch-api.mjs 不存在 —— role-protocol §5 的「唯一模板」失去落点")
check("T17 role-protocol 指向唯一模板", "tests/e2e/lib/launch-api.mjs" in rp,
      "role-protocol §5 未指向唯一启动模板（旧文指向 coverage.spec.ts 的 launchApi()，实现搬家后即成失效引用）")
check("T17 role-protocol 说明手工起实例入口", "launch-api.mjs --port" in rp,
      "role-protocol §5 未说明「手工起实例也必须走同一入口」——"
      "实测出问题的正是手敲 dotnet 这条路径，只约束 spec 侧等于漏掉主路径")
check("T17 coverage.spec.ts 不再自带启动实现", "spawn('dotnet'" not in _cov,
      "coverage.spec.ts 内又出现了 spawn('dotnet') —— 启动实现被复制回 spec，"
      "逐份复制正是「cwd 漏写 → 配置不加载 → 接口 500 且日志 0 字节」反复复发的原因")
check("T17 coverage.spec.ts 从唯一模板导入", "./lib/launch-api.mjs" in _cov,
      "coverage.spec.ts 未从唯一模板导入启动函数")

# 更一般的不变量：tests/e2e/** 下除唯一模板外，任何文件都不得直接 spawn dotnet 实例
_stray = []
for _p in sorted((ROOT / "tests" / "e2e").rglob("*")):
    if not _p.is_file() or _p.suffix not in (".ts", ".mjs", ".js"):
        continue
    if _p == _LIB:
        continue
    _txt = read(_p) or ""
    if "spawn('dotnet'" in _txt or 'spawn("dotnet"' in _txt:
        _stray.append(str(_p.relative_to(ROOT)))
check("T17 tests/e2e 下无第二份启动实现", not _stray,
      f"以下文件自行启动了 dotnet 实例（应改用 tests/e2e/lib/launch-api.mjs）: {_stray}")

# ---- 18. 全局兜底文件的定位约束（技术栈无关）与附录 C 的不复述约束 ----
# 背景：global/CLAUDE.md 对本机**所有项目**生效，且**无法引用**任何项目的规范文件 ——
# 它只能是手抄副本，抄了必然分叉。实测旧版：18 条红线里 17 条是技术栈特化的，
# 且落后项目规范 7 条规则、**从未被安装**（死文件）。故用断言把它钉在「技术栈无关」上。
_gm = read(ROOT / "global" / "CLAUDE.md") or ""
check("T18 全局文件声明边界节", "## 本文件的边界" in _gm,
      "global/CLAUDE.md 缺「本文件的边界」节 —— 该节是定位约束的载体，删掉即等于放弃「技术栈无关」原则")
# 「边界」一节按设计会举例提及技术栈名词，故先剔除，再检查正文不得出现技术栈特化规则
_m = re.search(r"^##\s+本文件的边界.*?(?=^##\s)", _gm, re.S | re.M)
_gm_body = _gm.replace(_m.group(0), "") if _m else _gm
_tech_marks = ["Composition API", "shadcn", "TanStack", "AsNoTracking", "v-auth", "Tailwind", "EF Core", "vue"]
_hit = [t for t in _tech_marks if t in _gm_body]
check("T18 全局文件正文不含技术栈特化规则", not _hit,
      f"global/CLAUDE.md 正文出现技术栈特化内容: {_hit} ——"
      "该文件对本机所有项目生效且无法引用项目规范，技术栈特化规则必然错配并漂移；应下沉到项目级 CLAUDE.md")

# 附录 C 不得复述权限规则（复述即第二落点：权限规则会随平台版本变化，两处措辞必然分叉）
_appc = ""
_mc = re.search(r"^##\s+附录 C.*?(?=^##\s|\Z)", spec, re.S | re.M)
if _mc:
    _appc = _mc.group(0)
check("T18 附录 C 不复述权限规则", _appc and "允许（免确认）" not in _appc and "拒绝（不可执行）" not in _appc,
      "development-spec 附录 C 又在复述 allow/ask/deny 三组规则 ——"
      "权威在 .claude/settings.json 与 .claude/README.md，复述即制造第二落点")
check("T18 附录 C 指向权威来源", ".claude/README.md" in _appc,
      "development-spec 附录 C 未指向 .claude/README.md（删掉复述后必须留下指针，否则读者找不到权威清单）")

# ---- 19. E2E 环境就绪（工具链自身）与 Bash 约定（env 劫持）----
# 背景：两种「环境没搭好」都以**产品缺陷的表象**出现（58 条里 55 条同时失败），
# 而常规探测（GET / → 200、GET /api → 401、GET /src/main.ts → 200）**全部返回"正常"**。
check("T19 §6 禁用 env 写法", "不要用 `env VAR=value cmd`" in rp,
      "role-protocol §6 仍在推荐 env VAR=value cmd —— 实测可被 PATH 上的同名脚本（uv 的 PATH 片段）"
      "劫持：只改 PATH、不执行传入命令、静默空转且退出 0")
check("T19 §6 记录 env 劫持机理", "静默空转" in rp and "只改 PATH" in rp,
      "role-protocol §6 缺 env 被劫持的机理（只改 PATH / 不执行传入命令 / 退出 0）——"
      "只给禁令不给机理，换个环境又会踩")
check("T19 退出码0无输出纪律", "不等于成功，可能命令根本没跑" in rp and "验证副作用" in rp,
      "role-protocol 缺「退出码 0 + 无输出 ≠ 成功，可能是命令根本没跑；关键命令必须验证副作用」的纪律")
check("T19 §5 浏览器内核就绪前提", "install chromium" in rp,
      "role-protocol §5 缺 Playwright 浏览器内核前置检查 —— 缺它会让所有浏览器用例 1ms 瞬时失败，"
      "而纯 API 级用例照常通过（表象像「产品坏了」）")
check("T19 §5 前端就绪判据=应用挂载", "返回 200 不算前端就绪" in rp and "page-login--default" in rp,
      "role-protocol §5 缺「GET / 返回 200 不算前端就绪，必须用浏览器断言应用已挂载」的判据")
check("T19 §5 先隔离复跑再定性", "先隔离复跑，再定性" in rp,
      "role-protocol §5 缺「失败用例先隔离复跑再定性」的归因纪律 —— 实测全量超时的 4 条隔离复跑全部秒过")

# ---- 自测：把「负向验证」从人工清单变成可执行命令 ----
# 用法：python3 tools/check-config.py --self-test
# 背景：config-checklist 要求「新增断言后逐条负向验证（故意破坏 → 必须 FAIL → 还原 → 必须 PASS）」，
# 并特别警告「每例必须先断言变更确实发生」——不先断言，replace 未匹配会静默不动，
# 于是「没验证」被当成「验证通过」，把假阴性写进护栏。本函数把这两条要求一并自动化。
MUTATIONS = [
    ("CLAUDE.md", "必须在事务边界统一分类", "必须在任意位置分别处理", "T16 双落点逐字一致: 重试判定"),
    ("docs/development-spec.md", "服务端同类超时", "服务端相近超时", "T16 双落点逐字一致: 超时配置"),
    ("CLAUDE.md", "安全敏感配置只允许一个取值来源", "安全敏感配置允许就地兜底", "T16 双落点逐字一致: 安全配置"),
    ("docs/development-spec.md", "引用一律用稳定 ID，禁止用行号", "引用可用行号", "T16 双落点逐字一致: 引用用稳定 ID"),
    ("CLAUDE.md", "禁止把当前行为固化成期望", "允许按当前行为编写期望", "T16 双落点逐字一致: 断言辨别力"),
    ("docs/development-spec.md", "每个分支都必须可构造", "分支不要求可构造", "T16 双落点逐字一致: 契约分支"),
    ("docs/development-spec.md", "### 7.5 断言辨别力", "### 7.6 断言辨别力", "T16 新增规范条款存在性: 7.5"),
    ("docs/development-spec.md", "### 14.1 跨层超时预算", "### 14.2 跨层超时预算", "T16 新增规范条款存在性: 14.1"),
    ("docs/role-protocol.md", "## 7. 证据卫生", "## 8. 证据卫生", "T16 role-protocol 证据卫生节"),
    ("docs/role-protocol.md", "长驻进程会锁死构建输出目录", "长驻进程不影响构建输出目录", "T16 role-protocol 长驻进程锁输出目录"),
    ("docs/role-protocol.md", "同一事实禁止多落点", "同一事实允许多落点", "T16 role-protocol 同一事实禁止多落点"),
    ("docs/error-codes.md", "| 1203 |", "| 9203 |", "T15 规范引用的业务错误码均已登记"),
    ("docs/artifacts.md", "按产物命名空间限定", "按产物顺序编号", "T15 OBS 编号命名空间规则已声明"),
    ("docs/role-protocol.md", "tests/e2e/lib/launch-api.mjs", "tests/e2e/coverage.spec.ts",
     "T17 role-protocol 指向唯一模板"),
    ("tests/e2e/coverage.spec.ts", "./lib/launch-api.mjs", "./lib/launch-api-x.mjs",
     "T17 coverage.spec.ts 从唯一模板导入"),
    ("global/CLAUDE.md", "## 通用红线（任何技术栈都成立）",
     "## 通用红线（任何技术栈都成立）\n\n- EF Core 只读查询必须 AsNoTracking()。",
     "T18 全局文件正文不含技术栈特化规则"),
    ("docs/development-spec.md", ".claude/README.md", ".claude/README-x.md",
     "T18 附录 C 指向权威来源"),
    ("CLAUDE.md", "仓库绝对路径含 `#` 时", "仓库绝对路径含 `%` 时",
     "T16 双落点逐字一致: 路径含 # 时前端单测全量失败"),
    ("docs/development-spec.md", "长驻开发实例会锁死", "长驻开发实例不影响",
     "T16 双落点逐字一致: 长驻实例锁死 bin（命令表）"),
    ("docs/role-protocol.md", "不要用 `env VAR=value cmd`", "推荐用 `env VAR=value cmd`",
     "T19 §6 禁用 env 写法"),
    ("docs/role-protocol.md", "install chromium", "install browser",
     "T19 §5 浏览器内核就绪前提"),
    ("CLAUDE.md", "浏览器内核不在 `npm ci` 范围内", "浏览器内核随 npm ci 一起安装",
     "T16 双落点逐字一致: E2E 首次必做装浏览器内核（命令表）"),
]


def run_self_test() -> int:
    """逐条突变 → 断言被捕获 → 还原 → 断言恢复全 PASS。任一步不成立即自测失败。"""
    bad = []
    print(f"自测开始：{len(MUTATIONS)} 条突变，逐条负向验证")
    me = str(Path(__file__).resolve())
    for rel, old, new, expect in MUTATIONS:
        path = ROOT / rel
        if not path.exists():
            bad.append(f"{rel}: 文件不存在")
            print(f"  ✗ [{rel}] 文件不存在")
            continue
        original = path.read_text(encoding="utf-8")
        # 「先断言变更确实发生」：不先断言，replace 未匹配会静默不动，把「没验证」当成「验证通过」
        if old not in original:
            bad.append(f"{rel}: 突变目标不存在（{old!r}）")
            print(f"  ✗ [{rel}] 突变目标不存在: {old!r}")
            continue
        try:
            # 替换**全部**落点：只改第一处的话，锚句仍在同一文件的其他位置，突变根本没破坏事实
            # （实测教训：两条锚句各在同一文件出现 2 次，用 count=1 突变时空转，差点被当成"护栏瞎了"）
            hits = original.count(old)
            mutated = original.replace(old, new)
            path.write_text(mutated, encoding="utf-8")
            if path.read_text(encoding="utf-8") == original:
                bad.append(f"{rel}: 变更未生效")
                print(f"  ✗ [{rel}] 变更未生效")
                continue
            proc = subprocess.run([sys.executable, me, "--json"],
                                  capture_output=True, text=True, encoding="utf-8")
            got = json.loads(proc.stdout or "{}").get("failures", [])
            if any(expect in g for g in got):
                print(f"  ✓ [{rel}] 已捕获（突变 {hits} 处）→ {expect}")
            else:
                bad.append(f"{rel}: 突变未被捕获（期望 {expect}）")
                print(f"  ✗ [{rel}] 未被捕获（突变 {hits} 处），期望 {expect}")
        except Exception as exc:  # noqa: BLE001
            bad.append(f"{rel}: 自测异常 {exc!r}")
            print(f"  ✗ [{rel}] 异常: {exc!r}")
        finally:
            path.write_text(original, encoding="utf-8")  # 必须还原
    proc = subprocess.run([sys.executable, me, "--json"],
                          capture_output=True, text=True, encoding="utf-8")
    if proc.returncode != 0:
        bad.append("还原后未恢复全 PASS")
        print("  ✗ 还原后仍有失败项")
    if bad:
        print(f"自测 FAIL: {len(bad)} 条")
        for b in bad:
            print("  ✗ " + b)
        return 1
    print(f"自测 PASS ✓（{len(MUTATIONS)}/{len(MUTATIONS)} 条突变均被捕获，还原后全 PASS）")
    return 0


# ---- 输出 ----
if "--json" in sys.argv:
    print(json.dumps({"total": len(checks),
                      "failures": [n for n, ok, _ in checks if not ok]}, ensure_ascii=False))
    sys.exit(1 if failures else 0)

if "--self-test" in sys.argv:
    sys.exit(run_self_test())

print(f"共 {len(checks)} 项检查")
if failures:
    print(f"FAIL: {len(failures)} 项未通过")
    for f in failures:
        print("  ✗ " + f)
    sys.exit(1)
print("全部 PASS ✓")
