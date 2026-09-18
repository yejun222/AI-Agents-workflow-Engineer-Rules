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
    "00-brief": ["product-manager", "prototype-designer", "software-architect", "test-designer", "test-executor"],
    "10-prd": ["prototype-designer", "software-architect", "engineer", "code-reviewer", "test-designer", "test-executor"],
    "20-prototype": ["software-architect", "engineer", "test-designer", "test-executor"],
    "30-architecture": ["engineer", "code-reviewer", "test-designer", "test-executor"],
    "40-changelog": ["engineer", "code-reviewer", "test-executor"],
    "50-testcases": ["test-executor", "engineer", "code-reviewer"],
    "51-defects": ["engineer", "test-executor", "code-reviewer", "主对话"],
    "52-qa-report": ["主对话", "test-executor", "code-reviewer"],
    "60-review": ["engineer", "test-executor", "software-architect", "主对话"],
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
# 且审查角色无法用 diff 取证，只能自认「旁证」（实测见 `docs/60-review.md` §B 的 BUG-02 结构核验条、§F.3「证据强度声明」、§F.5「未能验证的部分」——引用一律用稳定 ID，不用行号，见 13.4）。
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
# 扫描面**不只 spec 正文**：架构取号表（30）与三份产物（51/52/60）同样会引用号码，
# 只扫一处等于「换个文件引用就能绕过登记」。实测这几处当前无悬空码（唯一命中是日期，见下）。
art = read(DOCS / "artifacts.md") or ""
ec = read(DOCS / "error-codes.md") or ""

_CODE_CTX = re.compile(r"(?:code|如)\s*`?\s*(\d{4})(?:\s*/\s*(\d{4}))?", re.I)
_DATE_TAIL = re.compile(r"^\s*(?:-\d{1,2}|年)")   # 如 `2026-09-17T06:37:31` / 2026 年


def referenced_codes(text):
    """提取正文引用的 4 位业务错误码，跳过日期。

    日期排除不是洁癖：`如 \\`2026-09-17T06:37:31\\`` 会让「年份」被当成错误码（实测 51-defects 命中），
    若不放行会把 T15 变成常红断言，进而被人直接删掉。
    """
    out = set()
    for m in _CODE_CTX.finditer(text):
        for gi in (1, 2):
            code = m.group(gi)
            if not code or _DATE_TAIL.match(text[m.end(gi):]):
                continue
            if int(code) >= 1000:
                out.add(code)
    return out


_CODE_SCAN_FILES = ["development-spec.md", "30-architecture.md", "51-defects.md",
                    "52-qa-report.md", "60-review.md"]
_decl_line = next((_l for _l in ec.splitlines() if "护栏扫描面" in _l), "")
_decl_missing = [_f for _f in _CODE_SCAN_FILES if _f not in _decl_line]
check("T15 错误码扫描面已登记于 error-codes.md", not _decl_missing,
      f"error-codes.md 的「护栏扫描面」行缺 {'、'.join(_decl_missing)}（或整行缺失）——"
      "扫描面是单点事实：只写在脚本里，改小它不会有任何痕迹；登记 + 断言绑定后才改不动")
_scan_missing = [_f for _f in _CODE_SCAN_FILES if read(DOCS / _f) is None]
check("T15 错误码扫描面文件齐全（防扫描面静默缩小）", not _scan_missing,
      f"扫描面文件不存在: {'、'.join(_scan_missing)} —— 文件改名 / 删除后扫描面会静默缩小，"
      "断言仍绿但已不覆盖那些产物（引用的号码换个文件写就绕过登记）")

_referenced = set()
for _f in _CODE_SCAN_FILES:
    _t = read(DOCS / _f)
    if _t:
        _referenced |= referenced_codes(_t)
_registered = set(re.findall(r"^\|\s*(\d{4})\s*\|", ec, re.M))
_dangling = sorted(_referenced - _registered)

check("T15 规范引用的业务错误码均已登记", not _dangling,
      f"正文引用了未在 error-codes.md 登记的号码: {_dangling or '（提取为空，请检查提取正则）'} ——"
      f"扫描面为 {'、'.join(_CODE_SCAN_FILES)}；号码引用与注册表之间没有强制一致的机制，"
      "只能靠本断言（历史案例：1203/1204 长期悬空）")
check("T15 错误码提取非空（防断言空转）", bool(_referenced),
      "未能从扫描面提取到任何业务错误码引用 —— 提取正则已失效，本组断言退化为空转（假绿）")
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
      "development-spec 缺 §6.6 契约表达与可测性（键值表优先 / 分支可构造 / 通用错误响应体形态 / 架构六问）")
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

# ---- 20. 门禁判据单点定义（D1 / D2 / D3）：发布结论的输入必须自身被定义 ----
# 背景：state-machine 的判定条件引用「覆盖达标」「其他阻断项」「缺陷级别」，但三者**从未被定义** ——
# 于是同一份事实可以落到不同结论行（「98/98 已执行」既被读作达标也被读作未达标），
# 缺陷级别只能现场写散文论证。护栏守不住「定义写得对不对」，但能守住「定义还在、还是闭集」。
_sm = read(DOCS / "state-machine.md") or ""
check("T20 判据定义节存在", "## 判据定义（判定顺序之前必读）" in _sm,
      "state-machine 缺「判据定义」节 —— 判定条件里的「覆盖达标 / 其他阻断项 / 缺陷级别」将再次无定义，"
      "同一份事实会被不同角色读成不同结论")
_i_def, _i_seq = _sm.find("## 判据定义"), _sm.find("## 判定顺序")
check("T20 判据定义位于判定顺序之前", 0 <= _i_def < _i_seq,
      "「判据定义」节未排在「判定顺序」之前 —— 读者先看到带条件的结论行，定义形同虚设（等于没有）")
check("T20 D1 覆盖达标三条口径", all(k in _sm for k in
                                    ("### D1", "执行覆盖闭合", "无静默缩表", "覆盖达标 ≠ 质量达标")),
      "D1 定义不完整 —— 须同时含：分母取 50 当前版本全部 TC 编号（含「手动」）、无静默缩表、"
      "以及「覆盖达标 ≠ 质量达标」的边界声明；缺任一条则覆盖达标可被自行解释")
check("T20 D2 其他阻断项为闭集", "### D2　其他阻断项（行 4 用）" in _sm and "被显式登记" in _sm,
      "D2「其他阻断项」未按闭集枚举 —— 「其他」一旦可任意解释，行 4 的判定就没有边界"
      "（哪些算阻断、由谁标注必须写死）")
check("T20 D3 缺陷级别判据表", all(k in _sm for k in
                                  ("### D3", "不得降级的两类", "每条缺陷 / 审查问题必须写明定级依据")),
      "D3 缺基准档表 / 降级规则 / 「必须写明定级依据」—— 级别是 F1 与多行判定的输入，"
      "无判据时只能凭语感给出（实测：定级异议只能靠散文论证来回扯）")
check("T20 F1 / F2 指向判据定义", "级别按下文 D3 判定" in _sm and "覆盖是否达标按下文 D1 判定" in _sm,
      "state-machine 的输入事实 F1 / F2 未指向 D1 / D3 —— 输入与判据脱钩时，"
      "执行角色仍会各自解释「级别」与「覆盖达标」两个词")

# ---- 21. 规范侧五项新增判据（6.6 随机源 / 7.5 负向验证 / 7.6 不稳定 / 7.7 统计 / 13.4 实然陈述）----
# 背景：五项各自对应一次**已发生的实测**，不是预防性条款。护栏只守「条款还在、关键约束未被删软」。
check("T21 6.6 随机源可控性（架构六问）", "随机源可控性" in spec and "六个问题" in spec,
      "development-spec 6.6 缺「随机源可控性」—— 架构阶段不裁决可注入性，"
      "「固定随机源」类前置会在实现下**不可构造**，只能由测试侧登记为前置偏离")
check("T21 7.5 修复类断言必须实测变红", "负向验证" in spec and "与没有断言等价" in spec,
      "development-spec 7.5 缺「由缺陷驱动的修复必须实测变红（回退 → 断言必红 → 还原复绿）」——"
      "写明「会失败」只是文档义务，**不等于它真的会失败**")
check("T21 7.6 用例独立性与不稳定判定", all(k in spec for k in
                                          ("### 7.6 用例独立性与不稳定判定", "三类归因",
                                           "跨 ≥2 轮复发", "隔离复跑")),
      "development-spec 缺 7.6（用例独立 / `retries` 为 0 / 三类归因顺序固定 / 隔离复跑的使用限制）——"
      "缺它则「全量失败」会被反复误报为产品缺陷，且不稳定会被「隔离复跑通过」永久洗成绿")
check("T21 7.7 统计与概率类验收", all(k in spec for k in
                                     ("### 7.7 统计与概率类验收", "阈值只取契约原值",
                                      "样本纯净性", "复位义务")),
      "development-spec 缺 7.7（阈值取契约原值 / 判定对象全程恒定 / 样本纯净性对账 / "
      "前置偏离登记 / 夹具复位并实测）—— 统计类用例缺判据时会自定阈值并把噪声写成结论")
check("T21 13.4 实然陈述随事实同步", "两类文本的处置方向相反" in spec,
      "development-spec 13.4 未区分「配置 / 注释里的实然陈述必须随事实同步」与「已签署产物的历史复述不回改」——"
      "两类文本处置方向**相反**，混用会使活的断言静默失效（实测：某配置把「路径含 `&`」写成具体事实，"
      "仓库改名后该断言静默失效）")
check("T21 附录 A 自查清单含新增三项", all(k in spec for k in
                                           ("修复类改动已做负向验证", "用例自带隔离前提", "统计 / 概率类用例")),
      "development-spec 附录 A 自查清单未同步新增判据 —— 正文写了而清单没写，"
      "交付时按清单过一遍就仍然会漏（清单才是实际检查面）")

# ---- 22. 环境占用与破坏性操作的协调规则（role-protocol §8）----
# 背景：多角色共用同一台机、同一批容器与同一份日志。此前全靠「临时声明兜着」：
# 执行侧反复口头声明「未触碰主对话实例与开发库」、共享日志被追加后结论不可归属。
check("T22 role-protocol §8 节存在", "## 8. 环境占用与破坏性操作" in rp,
      "role-protocol 缺 §8 环境占用与破坏性操作 —— 环境协调规则只写在一次性声明里，"
      "换一轮就无人知道谁在用哪个实例")
check("T22 §8 实例与端口谁用谁登记", "谁用谁登记" in rp,
      "§8 缺「实例与端口谁用谁登记、需要就另起（不得重启 / 停用 / 改配他人实例）」——"
      "共用实例上「谁把它改了」不可追溯，破坏性操作会打到别人的取证现场")
check("T22 §8 库与 Redis 分界", "luckydraw_test" in rp and "luckydraw_dev" in rp,
      "§8 缺库 / Redis db 分界（执行侧只用测试库与独立 db index，禁止触碰开发库）——"
      "界限不写死时，「只用了测试数据」无法被证伪")
check("T22 §8 破坏性操作三段式", "事前公示" in rp and "归属声明" in rp,
      "§8 缺破坏性操作的「事前公示 → 事后复位清单（实测）→ 归属声明」三段式 ——"
      "只有事后一句「已复位」无法让主对话在你动手前叫停")
check("T22 §8 共用证据文件的归属", "非本批流量" in rp,
      "§8 缺「共用日志必须声明批次边界与『非本批流量』」—— 文件名唯一**不足以**确立归属"
      "（实测：某批日志其后被另一批追加，含 1 次 500 与多次告警，不声明就会算进自己的结论）")
check("T22 §5 用例隔离缺陷签名", "超时 + 共享配额" in rp and "跨 ≥2 轮复发" in rp,
      "role-protocol §5 缺「超时 + 共享配额」这一失败签名的归因（判「用例隔离缺陷」而非产品缺陷，"
      "且同一签名跨 ≥2 轮复发必须补隔离手段）—— 实测两轮共 10 条次此类失败全被误报为产品缺陷")

# ---- 23. 新规则的落地落点（artifacts ↔ 各角色提示词）----
# 通则：规则只写在规范里 = 角色不会执行。每条规则必须在**执行它的那个角色文件**里有对应义务句。
_art = read(DOCS / "artifacts.md") or ""
_crv = read(AGENTS / "code-reviewer.md") or ""
_tdv = read(AGENTS / "test-designer.md") or ""
_tev = read(AGENTS / "test-executor.md") or ""
_env = read(AGENTS / "engineer.md") or ""
check("T23 artifacts 60:OBS-nn 命名空间", "60:OBS-nn" in _art,
      "artifacts.md 未把审查侧「未复核项」纳入 OBS 命名空间（`60:OBS-nn`）——"
      "实测 60-review §H.6 / §F.5 各 5 项未复核项无编号、无责任人、无期限，跨版本是否仍挂着无从查证")
check("T23 artifacts「有理由的未复跑」为闭集", "有理由的未复跑" in _art and "三类之外一律视为未复核" in _art,
      "artifacts.md 未把「有理由的未复跑」限定为闭集 —— 不设边界的「有理由」会变成"
      "跳过复跑的统一借口（三类：不触及断言面且有检索证据 / 环境不具备且已登记 / 他人同版本已复跑且有证据路径）")
check("T23 code-reviewer 未复核项登记义务", "60:OBS-nn" in _crv and "计划复核时点" in _crv,
      "code-reviewer 输出要求缺「未复核项必须登记 `60:OBS-nn`，含缘由 + 责任人 + 计划复核时点」——"
      "只写「如实声明」不足以免除登记义务；本角色的 tools 无 Edit，规则不落在输出要求里就不会被执行")
check("T23 test-designer AC↔TC 覆盖对照（D1 唯一分母）", "AC ↔ TC 覆盖对照" in _tdv and "唯一分母来源" in _tdv,
      "test-designer 缺「AC ↔ TC 覆盖对照 + 声明覆盖分母 = 本文件当前版本全部 TC 编号」——"
      "该对照是门禁「覆盖达标」的**唯一分母来源**，缺失即无法判定覆盖达标（D1 第 1 条）")
check("T23 test-designer 用例独立性与前置预算", "前置预算必须核对" in _tdv and "7.6" in _tdv,
      "test-designer 缺「用例自带隔离前提 + 前置预算核对（用例数 × 受限配额 < 限流窗口）」——"
      "实测共享限流窗口使 4 条用例全量失败而隔离复跑秒过，误报为产品缺陷")
check("T23 test-executor 按 D3 定级 / 按 D1 判覆盖", "D3 判据表" in _tev and "D1 的三条口径" in _tev,
      "test-executor 缺「严重程度按 D3 判据表判定并写明定级依据」或「覆盖情况按 D1 三条口径逐条给出」——"
      "两者都是发布门禁的输入，只给「已执行 X/Y」等于把判定推回给读者")
check("T23 test-executor 统计类与 §8 环境纪律", "7.7 执行" in _tev and "环境占用与破坏性操作按" in _tev,
      "test-executor 缺 7.7 统计类执行义务或 §8 环境占用义务 —— 规则写在规范里而执行角色未接到，等于没有")
check("T23 engineer 修复类负向验证栏", "负向验证" in _env and "等价负向样本" in _env,
      "engineer 的 changelog 输出要求缺「负向验证」栏（回退修复 → 断言必红 → 还原复绿，三步观测记实；"
      "不可回退时给等价负向样本）—— 修复类改动的断言有效性只在此处留痕")

# ---- 24. 状态机判定闭环（第四次修订：D2 短路 / 无 owner 分派 / 落盘缺失 / 双落点分叉）----
# 通则：**每条判定行的前提必须真的拦得住它声称拦截的事实**。行序「首个命中即结论」意味着
# 任何一行漏了某前提，它之前定义的那类事实就会被静默吸收 —— 这与 D1 被创建时的动机是同一个失效。
_sav = read(AGENTS / "software-architect.md") or ""
_pmv = read(AGENTS / "product-manager.md") or ""
_pdv = read(AGENTS / "prototype-designer.md") or ""
_row_d2 = {}
for _line in _sm.splitlines():
    _rm = re.match(r"\|\s*\*{0,2}(0\.5|1|2|3|3\.5)\*{0,2}\s*\|", _line)
    if _rm and _rm.group(1) not in _row_d2:
        _row_d2[_rm.group(1)] = _line
_row_missing = [r for r in ("0.5", "1", "2", "3", "3.5")
                if "无其他阻断项（D2）" not in _row_d2.get(r, "")]
check("T24 五个判定行均含「无其他阻断项（D2）」前提", not _row_missing,
      f"判定行 {'、'.join(_row_missing)} 缺「无其他阻断项（D2）」前提（行 0.5 / 1 / 2 / 3 / 3.5 五行逐行都要有，"
      "不是「全表至少出现 5 次」——后者在新增行时会漏检）——原先只有行 4 含该条件，而它前面的三条行都不含 →"
      "「无未闭环致命 / 严重 + 覆盖达标」时 D2 的四类阻断项没有任何行能拦住，被静默吸收；"
      "「或存在其他阻断项」在覆盖达标时是死条件")
check("T24 D2 闭集含 60 未闭环审查问题",
      "`未闭环` 的 `REV-xx` 审查问题" in _sm,
      "D2 缺第 4 项「60-review 中未闭环的 REV-xx 审查问题」——一般 / 建议级 REV 此前不在任何判定行的条件里，"
      "而修复循环 a 却把「60 未闭环审查问题」列为 engineer 的派活触发条件：那是一个没有判定行能产生的进入条件")
check("T24 D2 双方标注义务",
      "由 test-executor 标注、`60:OBS-nn` 由 code-reviewer 标注" in _sm
      and "漏标注者按「阻断」处理" in _sm,
      "D2 第 2 项须同时覆盖 `52:OBS-nn`（test-executor）与 `60:OBS-nn`（code-reviewer），"
      "并规定「漏标注按阻断推定」——只管一侧会让另一侧的强制登记义务对结论的权重恒为 0；"
      "不推定阻断，登记方就能靠沉默过关")
check("T24 行 0.5 接受分支落盘", "把 52 落为「可发布」" in _sm,
      "行 0.5 的接受分支缺落盘动作（行 2 / 行 3.5 均有「重新调度 test-executor 落盘」）——"
      "其结论是「可发布」而非「有条件发布」，也套不上 test-executor 输出要求第 3 条，"
      "结果是被接受的建议级缺陷在 51 中永远停在「未闭环」、52 却给出「可发布」，矛盾无人负责")
check("T24 行 3 接受分支按本表重判", "按本表重判" in _sm,
      "行 3 的接受分支原写死「按行 2 执行」，而行 2 已带「覆盖达标」前提（2026-09-17 修订）——"
      "照字面执行会把覆盖缺口吸收进「有条件发布」，正是那次修订要防的失效；须改为「按本表重判」")
check("T24 行 3.5 落盘含已接受一般缺陷", "一并登记其接受人 / 理由 / 补丁计划" in _sm,
      "行 3.5 的落盘字段只记覆盖缺口 —— 同时被接受的一般缺陷（经行 3 接受、因覆盖不达标使行 2 不命中）"
      "没有落盘位置，其接受人 / 理由 / 补丁计划无处可写")
check("T24 修复循环 a 对 D2 四类分派 owner",
      "其他阻断项（D2 四类）" in _sm and "主对话裁决" in _sm and "按领域归属" in _sm,
      "修复循环 a 只分派「缺陷类」与「覆盖缺口类」——D2 的契约冲突 / 阻断型 OBS / 上游待复核 / 未闭环 REV "
      "四类均无 owner，行 4 到达 a 后原地停滞；须逐类写明由谁处置（缺陷有 owner、阻断项没有，等于行 4 断头）")
check("T24 test-executor OBS 阻断标注", "可机械提取的结构化字段" in _tev
      and "逐条标注「阻断 / 非阻断」" in _tev,
      "test-executor 缺「每条 OBS 逐条标注阻断 / 非阻断，且必须是可机械提取的结构化字段」——"
      "只写「不再构成任何阻断」这类散文式否定，读者与护栏都无法据此判定行 4，D2 整条落空"
      "（实测 52 的 OBS-10 / OBS-11 即此形）")
check("T24 test-executor 行 0.5 落盘", "52 落为 **「可发布」**" in _tev,
      "test-executor 的输出要求缺行 0.5 的落盘口径（落「可发布」而非「有条件发布」+ 缺陷状态改「已接受」）——"
      "接受决定转达后无角色落盘，51 与 52 会长期互相矛盾")
check("T24 code-reviewer 60:OBS 阻断标注", "逐条标注「阻断 / 非阻断」" in _crv,
      "code-reviewer 缺「`60:OBS-nn` 逐条标注阻断 / 非阻断」义务 —— D2 第 2 项要它标注，"
      "而它的输出要求里没有这一条，规则只写在状态机里 = 该角色不会执行（T23 同款失效）")
check("T24 code-reviewer 禁整份重写", "不得用 `Write` 整份重写" in _crv,
      "code-reviewer 的工作原则 4 要求「增量更新」，但未禁止用 `Write` 整份重写 ——"
      "`Write` 每次都要复现全文，任一处遗漏即静默删除已有内容且无任何报错；"
      "本仓库实测的 `60-review.md` 被覆盖事故正是这个机理")
_cr_tools = get_field(parse_frontmatter(_crv)[0], "tools") or ""
check("T24 code-reviewer 具备 Edit", "Edit" in _cr_tools,
      f"code-reviewer tools = {_cr_tools!r}，缺 `Edit` —— 它是 7 个角色中唯一没有 `Edit` 的，"
      "而恰恰是要求「增量更新」的角色之一：没有 `Edit`，增量更新只能靠整份重写。"
      "`Edit` 不新增任何能力（`Write` 本就能写任意文件），只是让该义务真的可执行")
_disk_roles = {"product-manager.md": _pmv, "prototype-designer.md": _pdv,
               "software-architect.md": _sav, "engineer.md": _env, "test-designer.md": _tdv}
_no_disk = [f for f, body in _disk_roles.items() if "先落盘、再打磨" not in body]
check("T24 五个角色含「先落盘、再打磨」", not _no_disk,
      f"缺该纪律的角色：{'、'.join(_no_disk)} —— 原只有 code-reviewer 与 test-executor 有。"
      "轮次耗尽即丢工作，且丢得静默：半写入的 `30-architecture.md` 不会被下游识别为「未完成」，"
      "只会被当作「契约就只有这些」，而它是冻结契约")
check("T24 架构六问双落点（architect ↔ spec）",
      "六个契约问题" in _sav and "随机源可控性" in _sav and "随机源可控性" in spec,
      "software-architect 的「架构阶段必须显式回答的六个契约问题」与 development-spec 6.6 第 5 条是"
      "同一事实的双落点，措辞必须都是「六个」且都含「随机源可控性」——"
      "原先角色文件写「五个」并漏掉该问，正是 TC-78「固定随机源」前置不可构造的根因（角色没被要求回答它）")
check("T24 6.6 引用按名称不按序号", "按名称引用，不按序号" in spec,
      "development-spec 7.7 第 4 条原引「见 6.6 第 6 问」，而随机源可控性是该节的**第 4** 问"
      "（第 6 问是可观测性落地），且 6.6 只有 5 个编号条目 —— 序号引用会随条款增删漂移，须按名称引用")
_tree_m = re.search(r"目标目录树.*?```\n(.*?)```", readme, re.S)
_tree = _tree_m.group(1) if _tree_m else ""
_tree_missing = [d for d in ("development-spec.md", "error-codes.md", "artifacts.md",
                             "state-machine.md", "role-protocol.md", "config-checklist.md",
                             "distribution.md") if d not in _tree]
check("T24 README 安装目录树含全部 7 份 docs", not _tree_missing,
      f"README 安装目录树缺 {'、'.join(_tree_missing)} —— 该树是分发到新项目时的复制依据；"
      "缺全量规范 / 错误码注册表，则角色提示词里按章节号的引用在新项目全部悬空")

# ---- 25. 规范强制要求的命令必须落在 allow（防「规范写了、配置没有」）----
# 失效机理（本仓库实测）：`docs/role-protocol.md` §5 与 CLAUDE.md 命令表**点名要求**的命令从未进过
# `.claude/settings.json` 的 allow。有人值守时子代理只是停在 ask 上等人点头；**无人值守（-p / dontAsk）
# 则直接按配置拒绝**（见 `.claude/README.md`「已知局限」）。于是「规范要求必做」与「配置不允许执行」
# 同时成立，而两份文件各自都没错——任何单文件校验都看不出来，只有跨「规范 ↔ 配置」才能发现。
# 断言方向固定为**规范 → 配置**：先证规范里的要求仍在（否则护栏条目自己悬空），再证配置真的放行。
_SETTINGS = ROOT / ".claude" / "settings.json"
_settings_txt = read(_SETTINGS)
_settings, _json_err = {}, ""
try:
    _settings = json.loads(_settings_txt) if _settings_txt else {}
except json.JSONDecodeError as _exc:
    _json_err = str(_exc)
_perms = _settings.get("permissions") or {}
_allow = _perms.get("allow") or []
_ask = _perms.get("ask") or []
_deny = _perms.get("deny") or []
check("T25 settings.json 合法 JSON 且含三组权限规则",
      not _json_err and bool(_allow) and bool(_ask) and bool(_deny)
      and all(isinstance(p, str) for p in _allow + _ask + _deny),
      (_json_err or f"allow={len(_allow)} / ask={len(_ask)} / deny={len(_deny)} 条；"
       "permissions 的 allow / ask / deny 必须同时存在且为字符串数组——缺任一组都仍然能正常加载与运行，"
       "Claude Code 不会报任何错，那一个方向的约束只是静默消失（deny 整组丢失 = 红线不再拦）"))
_dups = sorted({p for p in _allow if _allow.count(p) > 1})
check("T25 allow 无重复条目", not _dups,
      f"重复条目：{'、'.join(_dups)} —— 重复不改变语义，但它是「手工拼接」的伴生现象："
      "本仓库实测这类改动只能由用户手工写入（agent 自行放宽 allow 会被权限分类器拒绝），"
      "手工编辑最容易出现的正是漏改与重复并存")

# (allow 规则, 规范来源文件, 该来源中必须仍逐字存在的文本)
_REQUIRED_ALLOW = [
    ("Bash(node node_modules/@playwright/test/cli.js*)", "CLAUDE.md",
     "node node_modules/@playwright/test/cli.js install chromium"),
    ("Bash(node tests/e2e/lib/launch-api.mjs*)", "docs/role-protocol.md",
     "node tests/e2e/lib/launch-api.mjs"),
    ("Bash(node node_modules/vue-tsc/bin/vue-tsc.js*)", "CLAUDE.md", "vue-tsc/bin/vue-tsc.js"),
    ("Bash(node node_modules/vite/bin/vite.js*)", "CLAUDE.md", "vite/bin/vite.js"),
]
_src_cache = {}


def _src(rel):
    if rel not in _src_cache:
        _src_cache[rel] = read(ROOT / rel) or ""
    return _src_cache[rel]


_missing_allow = [p for p, _, _ in _REQUIRED_ALLOW if p not in _allow]
check("T25 规范强制要求的命令均已免确认", not _missing_allow,
      f"缺规则：{'、'.join(_missing_allow)}（共 {len(_REQUIRED_ALLOW)} 条受管）—— "
      "这些命令是规范**点名要求必做**的（E2E 内核安装不在 `npm ci` 范围内；§5 的启动入口是唯一模板；"
      "含 `&` 路径下 `npm run *` 必然失败故须直调），却不在 allow 里 → "
      "**无人值守时子代理会在问答态直接失败，且失败原因指向环境而非配置**。"
      "新增此类规范要求时须同步 allow 与本表")
_stale = [f"{rel} 缺 {lit!r}" for _, rel, lit in _REQUIRED_ALLOW if lit not in _src(rel)]
check("T25 免确认规则的规范来源仍在", not _stale,
      "；".join(_stale) + " —— 规范已不再要求该命令，而 allow 仍在放行："
      "此时该条目已失去依据（护栏自身悬空）。要么恢复规范原文，要么连同 allow 规则一并删除，"
      "**不得让「配置放行的命令」多于「规范要求的命令」而不留痕**")
# 方向二（反方向，读的是 settings.json 而不是上面那张表）：
# allow 里每一条「直调 node 入口」的免确认，都必须在规范里被点过名。
_norm_text = _src("CLAUDE.md") + _src("docs/role-protocol.md")
_undeclared = [p for p in _allow
               if re.match(r"^Bash\(node\s", p)
               and Path(p[len("Bash("):-1].rstrip("*")).name not in _norm_text]
check("T25 allow 的直调入口均在规范里点过名", not _undeclared,
      f"{'、'.join(_undeclared)} —— 该规则放行了规范从未要求的入口。"
      "**本条的断言对象必须是配置文件本身**：若只比对「上面那张表 ↔ 规范原文」，"
      "则把 allow 里的 `vue-tsc.js` 换成任意其它工具后三条断言仍然全绿（自测实测：该突变未被捕获）——"
      "那是护栏自己在念自己的台词。免确认只能授予规范点过名的入口，"
      "否则「agent 能执行什么」会悄悄大于「规范要求什么」")


def _lit_prefix(pat):
    """取权限规则里首个通配符之前的字面前缀（用于遮蔽判定）。"""
    body = pat[len("Bash("):-1] if pat.startswith("Bash(") and pat.endswith(")") else pat
    for ch in "*?":
        body = body.split(ch, 1)[0]
    return body.rstrip()


_shadow = []
for p, _, _ in _REQUIRED_ALLOW:
    _body = p[len("Bash("):-1].rstrip("*")
    for _grp, _pats in (("ask", _ask), ("deny", _deny)):
        for _pat in _pats:
            _lp = _lit_prefix(_pat) if _pat.startswith("Bash(") else ""
            if _lp and _body.startswith(_lp):
                _shadow.append(f"{p} 被 {_grp} 的 {_pat} 遮蔽")
check("T25 要求的命令未被 ask/deny 遮蔽", not _shadow,
      "；".join(_shadow) + " —— Claude Code 的判定顺序是 deny > ask > allow，"
      "**allow 里的规则拦不住被 ask/deny 覆盖的命令**："
      "新加一条 `Bash(node *)` 到 ask 就足以让上述全部免确认规则失效，而 allow 组看上去完全正常")

_claude_readme = read(ROOT / ".claude" / "README.md") or ""
_doc_missing = [t for t in ("install chromium", "launch-api.mjs", "node_modules/")
                if t not in _claude_readme]
check("T25 免确认入口已登记在 .claude/README.md", not _doc_missing,
      f".claude/README.md 的 allow 说明缺 {_doc_missing} —— 该表是团队理解「什么被放行了」的唯一入口；"
      "配置放了行而说明表不提，等于把「agent 能执行哪些命令」变成只有读 JSON 才知道的事实")

# ---- 26. OBS 编号的跨命名空间裸引用（编号载体里的**事实**，此前只断言过规则文本存在）----
# 失效机理：T15 断言了 artifacts.md **写着**「跨产物引用必须带文件名前缀」，但从未看过 51 / 52 / 60
# 里**实际**有没有裸引用。实测：60-review.md 正文里有一处裸 `OBS-03`，而该文件**没有任何 OBS 定义序列**，
# 51 与 52 又各有一个含义不同的 OBS-03 —— 规则写在纸上、事实挂在文件里，两边从不相遇。
# 判定（扫描面与规则同源于 artifacts.md §3.1，防扫描面被悄悄缩小，同 T15 的手法）：
#   三份编号载体的**非引用块正文**里，裸 `OBS-nn` 必须 ① 能在本文件定义表查到，或 ② 已在本文件编号勘误块
#   中登记。两条都不满足 = 跨命名空间裸引用，没人处理过它。
_OBS_DOCS_EXPECT = ["51-defects.md", "52-qa-report.md", "60-review.md"]
_obs_decl = next((_l for _l in (read(DOCS / "artifacts.md") or "").splitlines()
                  if "护栏扫描面" in _l), "")
check("T26 OBS 引用扫描面已声明", all(d in _obs_decl for d in _OBS_DOCS_EXPECT),
      "artifacts.md §3.1 未逐字声明 OBS 裸引用的护栏扫描面（须列出 51-defects.md / 52-qa-report.md / "
      "60-review.md）——扫描面不声明就会随实现悄悄缩小：T15 的扫描面只覆盖 spec，"
      "于是 30-architecture 里的号码引用长期没被查过；本项同理，范围一旦靠代码隐式决定，"
      "下次「顺手少扫一份」不会有任何信号")
_OBS_DEF = re.compile(r"^\|\s*(OBS-\d{1,2})\s*\|", re.M)   # 定义表行（`| OBS-08 | …`）
_OBS_BARE = re.compile(r"(?<![0-9:])(OBS-\d{1,2})")        # 裸引用（`52:OBS-08` 不算裸）
for _d in _OBS_DOCS_EXPECT:
    _t = read(DOCS / _d) or ""
    _defs = {m.group(1) for m in _OBS_DEF.finditer(_t)}
    _body = [l for l in _t.splitlines() if not l.lstrip().startswith(">")]
    _quote = [l for l in _t.splitlines() if l.lstrip().startswith(">")]
    _orphan = sorted({m.group(1) for l in _body for m in _OBS_BARE.finditer(l)} - _defs)
    # 登记必须**点名被登记的编号本身**：① `>` 表格行的首格，或 ② `>` 行里反引号裸形（`OBS-nn`）。
    # 收紧前口径是「该编号在任一 `>` 行里出现过」——实测 52 的「状态重述」块顺带罗列了全部 11 个
    # 编号，于是把某条定义行改名后 orphan 被判为「已登记」，断言全绿而事实上无人登记过它。
    _registered = set()
    for _l in _quote:
        _rm = re.match(r"^>\s*\|\s*(OBS-\d{1,2})\s*\|", _l)
        if _rm:
            _registered.add(_rm.group(1))
        _registered |= set(re.findall(r"`(OBS-\d{1,2})`", _l))
    _unreg = [o for o in _orphan if o not in _registered]
    check(f"T26 {_d} 的跨命名空间裸引用已登记", not _unreg,
          f"裸引用 {'、'.join(_unreg)} 既不在本文件定义表内，也未在本文件编号勘误块中登记 —— "
          "该编号在别的命名空间真有同号条目时（实测 51 / 52 各有一个含义不同的 OBS-03 与 OBS-08），"
          "读者与检索都无法判定它指哪一份，且**没有任何环节会报错**。"
          "处置二选一：补命名空间前缀，或按 51 / 52 先例在编号勘误块中登记（不改写历史正文）")

# 阻断判别口径：同一条口径散落在四处，逐字绑定（T16 范式的推广），防止只在其中一处改措辞
_KOUJING = {"docs/artifacts.md": read(DOCS / "artifacts.md") or "",
            "docs/state-machine.md": _sm,
            ".claude/agents/test-executor.md": _tev,
            ".claude/agents/code-reviewer.md": _crv}
_kj_missing = [f for f, t in _KOUJING.items() if "环境 / 证据 / 工具链类" not in t]
check("T26 阻断判别口径四处逐字一致", not _kj_missing,
      f"缺「环境 / 证据 / 工具链类」：{'、'.join(_kj_missing)} —— 判别口径（哪类默认按阻断登记）"
      "是 D2 第 2 项唯一的判定依据，四处必须逐字一致；措辞漂移会让某一处按「非阻断」读，"
      "而四处各自的措辞看起来都「差不多」")
_kj_default = [f for f, t in _KOUJING.items()
               if f != "docs/state-machine.md" and "默认按**阻断**登记" not in t]
check("T26 阻断默认档三处逐字一致", not _kj_default,
      f"缺「默认按**阻断**登记」：{'、'.join(_kj_default)} —— "
      "只规定「逐条标注」而不给默认档，登记方在拿不准时不会主动表态；"
      "state-machine D2 用「漏标注按阻断处理」兜底，故不要求它重复本句")

# ---- 27. 已知优化提案（§E）的前提与现状：登记在案的假设不得无声腐烂 ----
# §E 是「未启用、改动前先读」的提案区。它最大的风险不是写错，而是**写下的前提日后失效而无人发现**：
# 提案本身没人执行，前提也没人核对，于是一条过期的依赖假设会一直以「已分析成立」的身份躺在清单里，
# 等下一个真去启用它的人踩空。此处把 §E 两项提案的可机器判定部分钉住。
_cl = read(DOCS / "config-checklist.md") or ""
_td_m = re.search(r"##\s*输入要求(.*?)(?=\n##\s|\Z)", _tdv, re.S)
_td_in = _td_m.group(1) if _td_m else ""
check("T27 并行提案前提: test-designer 输入不含 40-changelog", "40-changelog" not in _td_in,
      "`§E.2 test-designer 与 engineer 并行` 的全部依据是「test-designer 不消费 40」这一条依赖假设，"
      "而它此前**没有任何断言**——40 一旦进入 test-designer 的输入要求，该提案的前提即失效，"
      "清单里却仍写着「依赖分析成立」等着被启用（启用后 test-designer 会开始消费 engineer 的产物，"
      "并行派发立刻产生未定义顺序）。本条同时把「test-designer 禁读 40 / 60」从提示词级自律"
      "变成机器可查——原文自陈「机制上拦不住，只能靠角色自律」")
check("T27 §E.1 现状与绑定结论仍在", "未绑定 0 条" in _cl and "已远超本项原定触发线" not in _cl,
      "config-checklist §E.1 的现状段缺失、或被改回旧结论 —— 旧判据「跨产物复述超过 4–5 条即需升级」"
      "在 2026-09-18 逐条实测后已被推翻（7 条里 4 条本就是单一落点、3 条多落点已全部绑死）；"
      "留着旧结论会让下一个人按过期的触发线去启动一次「新增文件 + 改动框架结构」的变更")

# ---- 28. 错误码登记人与 429 例外（跨文件口径：同一事实在四处各写一份）----
# 两个实测缺陷，形态都是「两份文件各自都没错、单文件校验全绿」：
# ① 登记人：error-codes.md 文末写「主对话登记」、10-prd FR-10 写「由 engineer 取号登记」，
#    而 artifacts §2「写入者只能写本行列出的文件」里**根本没有 error-codes.md 这一行**——
#    于是「规范要求登记」与「写入范围表禁止写」同时成立，谁在法理上都写不了。
# ② 429：6.4 表只枚举 0/400/401/403/409/500/≥1000，**没有 429**，而它的说明写「HTTP 状态码与
#    0–500 段保持一致」；按该字面读，HTTP 429 时 code 应为 429，与架构 §5.1「响应体 code=1001」相反。
#    429 是本仓库唯一一处 HTTP 状态码与 ApiResult.code 取值不同的组合，必须写在表里才不算隐含例外。
_prd = read(DOCS / "10-prd.md") or ""
_arch = read(DOCS / "30-architecture.md") or ""
_429 = "**唯一例外是 `429` 限流：HTTP 429 而响应体 `code=1001`**"
# 扩展名单点定义：30-H 扫描面的扩展名从同一元组派生（两处清单必然漂移）。
_LN_REF_EXT = ("md", "cs", "ts", "vue", "mjs", "js", "json", "html", "py", "yml", "yaml", "sh")
# **必须覆盖代码文件的「文件:行号」形态**。实测：本模式原为 `#LNN` 与 `md:NN` 两式，于是
# `tests/e2e/api/*.mjs` 里 6 处「代码文件:行号」形态的定位引用**长期不被检查**，而护栏照报
# 「活文档与代码无行号引用 ✓」——断言比它声称的规则窄，是本仓库最怕的那种假绿。
_LN_REF = re.compile(r"(?:#L[0-9]+|[A-Za-z0-9_./-]+[.](?:" + "|".join(_LN_REF_EXT)
                     + r")[\s]*:[\s]*[0-9]+)")


def _scope_row(text, role):
    """取 artifacts §2 写入范围速查表某角色的一行（判写入权的唯一权威）。"""
    m = re.search(r"^\|\s*" + re.escape(role) + r"\s*\|[^\n]*$", text, re.M)
    return m.group(0) if m else ""


check("T28 错误码登记人按阶段归属已声明",
      "**架构期**——software-architect 取号" in ec
      and "**实现期**——engineer 取号并登记" in ec,
      "error-codes.md 文末未按阶段写明登记人 —— 原先只有一句「由 software-architect 取号、主对话登记」，"
      "与 10-prd FR-10「新增业务错误码由 engineer 取号登记」直接互斥，"
      "而 engineer 的提示词与 CLAUDE.md 交付前自查清单都要求它「已登记」："
      "照提示词做违反写入范围表，照写入范围表做永远满足不了自己的自查清单")
check("T28 artifacts §2 已列出 error-codes.md 的写入者",
      "docs/error-codes.md" in _scope_row(art, "engineer")
      and "docs/error-codes.md" in _scope_row(art, "主对话"),
      "artifacts §2 的 engineer / 主对话可写列至少缺一处 error-codes.md —— "
      "§2 表头写死「写入者只能写本行列出的文件」，而 error-codes.md 此前在该文件里一次都没出现过："
      "「规范要求主对话或 engineer 去登记」与「写入范围表禁止写」于是同时成立")
check("T28 10-prd FR-10 的 engineer 归属未被单边改动",
      "新增业务错误码由 engineer 按 `docs/error-codes.md` 取号登记" in _prd,
      "docs/10-prd.md FR-10 第 2 条的 engineer 归属被改（或删除）—— 该产物**已冻结 v2**，"
      "改它需升 v3 并按 artifacts §5 让七份下游标「待复核」；护栏绑的是它的现文，"
      "**单边改任一处即 FAIL**：要么同步改 error-codes.md 与 artifacts §2，要么先走升版流程")
check("T28 429 例外双落点逐字一致",
      _429 in spec and _429 in ec,
      f"「{_429}」在 development-spec 6.4 与 error-codes.md 头部未逐字一致（或缺失其一）—— "
      "429 是唯一一处 HTTP 状态码与 code 取值不同的组合，两处措辞一漂，"
      "读 6.4 的人会按「HTTP 状态码与 0–500 段保持一致」推出 code=429，与架构的 1001 相反")
check("T28 6.4 表含 429 行且标为 HTTP 例外",
      re.search(r"^\|\s*429\s*\|", spec, re.M) is not None and "非 `ApiResult.code` 取值" in spec,
      "6.4 错误码表缺 429 行，或该行没有标明「非 ApiResult.code 取值」—— "
      "6.4 表的说明是「表中数值为响应体 ApiResult.code 的取值」，直接补一行 429 而不标注，"
      "等于新造一处矛盾：表说它是 code 取值，架构说响应体是 1001")
check("T28 架构侧 429 → code=1001 定义仍在",
      "`429` 限流（响应体 `code=1001`）" in _arch,
      "30-architecture §5.1 的「429 限流（响应体 code=1001）」被改 —— "
      "这是 429 语义的唯一权威落点，它与 6.4 的 429 行必须同向；"
      "改架构侧须同步改 6.4 与 error-codes.md，否则前端会同时存在两套限流处理")
check("T28 错误码段标签与 6.4 一致（0–500 段）",
      "`0–500` 段" in ec and "4xx/5xx" not in ec and "0–500 段" in spec,
      "error-codes.md 用了「4xx/5xx 段」而 6.4 正文写「0–500 段」—— 同一族两个名字，"
      "且两者外延不同（0–500 含成功码 0、不含 501–599；4xx/5xx 正好相反），"
      "读者无法判定它们是不是同一回事")
_eng_scope_m = re.search(r"^##\s*写入范围(.*?)(?=\n##\s|\Z)", _env, re.S | re.M)
_eng_scope = _eng_scope_m.group(1) if _eng_scope_m else ""
check("T28 engineer 角色文件的写入范围已同步",
      "docs/error-codes.md" in _eng_scope,
      "engineer.md「## 写入范围」节未同步 artifacts §2（或该节缺失）—— 角色文件是子代理实际读到的那一份，"
      "只改速查表等于**给不出权限**：engineer 按自己的写入范围会拒绝登记（或更糟："
      "按 CLAUDE.md 自查清单去登记却踩了自己文件里的「不得写入范围外文件」），而 artifacts §2 看上去完全正确。"
      "⚠️ 本断言**只扫该节**：全文扫 `docs/error-codes.md` 会被上文「新增业务错误码必须登记」那句骗过"
      "（实测第一次就写成全文扫，突变删掉写入范围里的落点后断言仍然绿）")
check("T28 error-codes.md 内无行号引用",
      not _LN_REF.findall(ec),
      f"error-codes.md 出现行号引用 {_LN_REF.findall(ec)} —— 违反「引用一律用稳定 ID，禁止用行号」"
      "（全量规范 13.4）。实测：本文件原引 development-spec 的某个行号，"
      "该句所处行与引用值已相差 2 行，即规则预言的漂移已经发生过；改用章节号引用")

# ---- T29 状态机「表注 / 历史节」分离 + 行 5 的 D2 披露义务（第五次修订）----
# 背景：原「修订记录」块把三类性质不同的东西堆在同一个引用块里：① 现行约束（行序要求与前提）；
# ② 与判定表逐字重复的复述；③ 纯历史因果。后果是改表的人读不出哪句还在生效，而块内那处
# 不成立的表述（「使 D2 项一律下落到行 4」）在 `F1 = 是` 时是错的，却**零断言覆盖**。
_NOTE_MARK = "**表注（修订本表前必读）"
_seq_pos = _sm.find("## 判定顺序")
_note_pos = _sm.find(_NOTE_MARK)
_fix_pos = _sm.find("## 修复循环（a → b → c）")
_hist_pos = _sm.find("## 修订记录（历史，不回改）")
_hist = _sm[_hist_pos:] if _hist_pos >= 0 else ""
_row5_m = re.search(r"^\|\s*5\s*\|[^\n]*$", _sm, re.M)
_row5 = _row5_m.group(0) if _row5_m else ""

check("T29 状态机表注位于判定顺序与修复循环之间",
      _note_pos > _seq_pos >= 0 and 0 < _note_pos < _fix_pos,
      "判定顺序表下方缺「表注（修订本表前必读）」或它被挪走了 —— 表注承载的是**现行**的行序约束"
      "（行 0.5 必在行 1 前 / 行 3.5 必在行 4 前 / 行 3.5 必带致命严重前提），必须紧贴判定表："
      "读表的人改表前先看到约束，才不会再踩「行序即语义」这一条")
check("T29 表注含三条行序约束",
      "**行 0.5 必须位于行 1 之前**" in _sm
      and "**行 3.5 必须位于行 4 之前**" in _sm
      and "**行 3.5 必须带「无未闭环致命 / 严重」前提**" in _sm,
      "表注缺行序约束 —— 这三条是「首个命中即结论」的表里唯一能解释**为什么不能重排**的依据；"
      "缺任一条，后来者都会按「条件更全的行该排更后」的直觉重排，"
      "把行 0.5 / 行 3.5 变成永远不可达的死行，而表面上毫无异常（该行永远比前一行后命中）")
check("T29 行 5 的 D2 刻意例外已声明",
      "行 5 是本表唯一的「D2 刻意例外」" in _sm and "见文末修订记录第五次" in _sm,
      "表注未声明「行 5 是唯一的 D2 刻意例外」，或未指向第五次修订 —— 缺这句，"
      "表注里「五行都带 D2 前提」那条会被读成**全表通则**，后来者会顺手给行 5 也补上该前提："
      "那样 `F1 = 是` + `F4 = 已指示` + 存在 D2 项的场景将**无任何行命中**（本表在行 5 之后无兜底行）")
check("T29 行 5 落盘含 D2 披露义务",
      "一并被接受，但必须逐条登记披露" in _row5
      and "本行是它们在本表中唯一的落盘位置" in _row5
      and "已接受残留清单" in _row5,
      "行 5 的落盘动作只登记「缺陷编号」—— D2 四类里除缺陷者外（契约冲突 / 上游「待复核」/ "
      "未闭环 REV / 阻断型 OBS）都没有落盘位置，用户一旦在 `F1 = 是` 时选「带残留接受」，"
      "这些阻断项就被**静默吸收**。本仓「致命 / 严重」两类在 D3 表中标注「无实例」，"
      "所以该分支从未被踩到 —— 这类「没踩到所以没写」的缺口只能靠断言补")
check("T29 表注与历史节分离（现行约束未混回历史节）",
      _hist_pos > 0
      and not [a for a in ("永远不可达", "必须位于行") if a in _hist]
      and "永远不可达" in _sm[:_hist_pos],
      "现行约束混进了文末历史节，或表注里丢了 —— 两者性质相反：表注＝**尚未被改的约束，读表者必须执行**，"
      "历史节＝**当时的事实，按 13.4 不回改**。混在一处的后果正是本次修订前的状态："
      "块里同时有行序硬约束与五次修订的复述，改表的人分不清哪句还在生效。"
      "历史节可以**点名**这些约束（如「把…写进表注」），但不得用现行约束的措辞复述它们")
check("T29 表注声明守护归属（T11 / T24 / T29）",
      "由 `tools/check-config.py` 的 **T11** 断言守护" in _sm
      and "由 **T24** 守护" in _sm
      and "由 **T29** 守护" in _sm,
      "表注未写明哪段约束由哪个断言守护 —— 没有归属的约束在改动时会被顺手删掉，"
      "而删掉之后**没有任何断言会红**：本次修订前整个「修订记录」块零断言覆盖，"
      "连一处与行为不符的表述（D2 项一律下落到行 4）都能在里面长期存活")
check("T29 行 5 登记的 D2 四类与 D2 闭集同向",
      all(a in _row5 for a in ("契约冲突", "待确认清单", "OBS", "待复核", "REV-xx"))
      and all(a in _sm[_sm.find("### D2　其他阻断项"):_sm.find("### D3　缺陷级别")]
              for a in ("契约冲突上报", "待确认清单", "阻断", "待复核", "REV-xx")),
      "行 5 是 D2 项在 `F1 = 是` 时的唯一落盘位置，它列出的类别必须与「判据定义」D2 的闭集同向 —— "
      "两处是**同一事实的第二个落点**，按 `config-checklist.md` §B 「同一事实的第二个落点」条必须由断言绑死："
      "少一类 = 那一类在该场景下仍然被静默吸收（本次修订要修的正是这个）；"
      "改写时两处同步改即可，**不要删断言**")

# ---- 30. 产物侧合规（两份审计的共同根因：护栏断言的是**规则文本**，从不检查**产物是否遵守**）----
# 失效机理（本仓库实测）：T26 断言了「OBS 必须逐条标注阻断 / 非阻断」这条**规则**在四处逐字一致，
# 却从没检查 51 / 52 的表里到底有没有那一列、格子里写的是不是这两个取值；T23 断言了
# 「60:OBS-nn 必须含缘由 + 责任人 + 计划复核时点」，而 60 里根本没有 OBS 表。
# **规则文本全绿、产物为空**——审计报告的 F1 / F2 / F6 / F7 / F8 / F10 / F12 全是这一个根因的实例：
# 断言对象是「规范怎么写的」，而不是「产物怎么做的」。本块把断言对象换成产物本身。
_CONTRACT_DOCS = ["00-brief.md", "10-prd.md", "20-prototype.html", "30-architecture.md",
                  "50-testcases.md", "51-defects.md", "52-qa-report.md", "60-review.md"]
_OBS_DOCS = ["51-defects.md", "52-qa-report.md", "60-review.md"]
_BLOCKING_OK = ("阻断", "非阻断")


def _table_header(lines, i):
    """覆盖第 i 行的那张表的表头行号（markdown 表格：表头 / 分隔行 / 数据行连续，中间无空行）。"""
    j = i - 1
    while j >= 0 and lines[j].startswith("|"):
        j -= 1
    j += 1
    return j if j < i else None


def _cells(line):
    return [c.strip().strip("*`").strip() for c in line.strip().strip("|").split("|")]


def _obs_entry_lines(lines, i):
    """条目块：表格行取该行；列表 / 标题式条目取本行起至空行或下一条目。"""
    if lines[i].startswith("|"):
        return [lines[i]]
    out = [lines[i]]
    for k in range(i + 1, len(lines)):
        s = lines[k]
        if not s.strip() or re.match(r"^\|?\s*(?:\*\*)?(?:60:)?OBS-\d+", s) or re.match(r"^#{2,4}\s", s):
            break
        out.append(s)
    return out


def _row_indices(lines, name="OBS"):
    """条目行：markdown 表格行优先，其次 `- ` / `**` 开头的列表项。"""
    pat = rf"^\|\s*(?:60:)?{name}-\d+\s*\|"
    hit = [i for i, l in enumerate(lines) if re.match(pat, l)]
    if hit:
        return hit
    return [i for i, l in enumerate(lines)
            if re.match(rf"^(?:[-*]\s*)?(?:\*\*)?(?:60:)?{name}-\d+", l)]


# 30-A 阻断列：**规则**要求逐条标注（T26 已绑死），这里断言**产物**里真有那一列且取值可机械提取。
# 取值闭集 = 阻断 / 非阻断；**漏标注按「阻断」推定**（state-machine D2），故空格子不是「没意见」而是违规。
for _d in _OBS_DOCS:
    _lines = (read(DOCS / _d) or "").split("\n")
    _no_col, _bad_val = [], []
    for _i in _row_indices(_lines):
        _num = re.search(r"(?:60:)?(OBS-\d+)", _lines[_i]).group(1)
        if _lines[_i].startswith("|"):
            _h = _table_header(_lines, _i)
            _hc = _cells(_lines[_h]) if _h is not None else []
            _k = next((n for n, c in enumerate(_hc) if "阻断" in c), None)
            if _k is None:
                _no_col.append(_num)
                continue
            _rc = _cells(_lines[_i])
            _v = _rc[_k] if _k < len(_rc) else ""
        else:
            _blk = "\n".join(_obs_entry_lines(_lines, _i))
            _m = re.search(r"(非阻断|阻断)", _blk)
            _v = _m.group(1) if _m else ""
        if _v not in _BLOCKING_OK:
            _bad_val.append(f"{_num}={_v or '空'}")
    check(f"T30 {_d} 的 OBS 条目均含「阻断 / 非阻断」列", not _no_col,
          f"未找到取值列的条目：{'、'.join(_no_col)} —— `docs/state-machine.md` D2 第 2 项以这两列为**发布门禁的输入**，"
          "而本文件的表里根本没有这一列（**规则写在纸上、事实挂在文件里**）。必须逐条给出结构化取值，"
          "不得用「不构成任何阻断」这类散文式否定代替")
    check(f"T30 {_d} 的 OBS 阻断取值合法且无漏标", not _bad_val,
          f"非法或缺标：{'、'.join(_bad_val)} —— 取值只允许 `阻断` / `非阻断`；"
          "**漏标注者按「阻断」推定**（D2）：沉默不得成为放行手段，空格子会被 D2 当作阻断项处理，"
          "而判定者看到的却是一张「什么都没说」的表 —— 两侧读法不同且都不会报错")


# 30-A 的第二个盲区：上面两条遍历的是**已存在的条目行** —— 编号被静默丢弃时它们一律看不见。
# 本仓既有惯例是把缺号写进 `>` 前缀的缺席登记表并给出理由（实测 51 的 OBS-09 / OBS-10：前者与
# `51:OBS-08` 为同一事实、后者属代码审查发现不进测试台账；52 对同两条亦做了对照登记）。
# 惯例只活在产物里、无断言时，下一次「顺手删掉一行」不会有任何信号 → 把惯例变成可执行判据：
# 1..max 内每个编号必须**要么有定义行、要么在 `>` 块里登记缺席理由**（「自 01 起连续」是本仓口径）。
# 登记通道沿用 T26 对 `>` 块的处理（该块即「编号勘误 / 缺席登记」块），不另立新通道。
for _d in _OBS_DOCS:
    _lines = (read(DOCS / _d) or "").splitlines()
    _nums = sorted(int(re.search(r"OBS-(\d+)", _lines[_i]).group(1)) for _i in _row_indices(_lines))
    _quoted = [l for l in _lines if l.lstrip().startswith(">")]
    _absent = [n for n in range(1, (max(_nums) if _nums else 0) + 1)
               if n not in _nums
               and not any(re.search("OBS-" + str(n).zfill(2) + "(?![0-9])", l) for l in _quoted)]
    check(f"T30 {_d} 的 OBS 编号无未登记缺号", not _absent,
          f"缺号且未登记：{'、'.join('OBS-' + str(n).zfill(2) for n in _absent)} —— 编号是引用的锚点："
          "缺号被静默丢弃后，外部对它的引用会指向不存在的位置且**没有任何环节会报错**。"
          "本仓既有惯例是把缺号写进 `>` 前缀的缺席登记表并给出理由（实测 51 的 OBS-09 / OBS-10 即如此）。"
          "处置二选一：补条目，或按该惯例登记缺席理由")

# 30-B 审查侧未复核项台账：**规则**（artifacts §3.1）要求 `60:OBS-nn` 含缘由 + 责任人 + 计划复核时点，
# 这里断言 60 里真有这些条目与字段（此前 60 的未复核项无编号、无责任人、无期限，跨版本无从查证）。
_t60 = (read(DOCS / "60-review.md") or "").split("\n")
_i60 = _row_indices(_t60)
check("T30 60-review 存在 60:OBS-nn 未复核项台账", bool(_i60),
      "`docs/60-review.md` 里一条 OBS 台账都没有 —— 该文件此前把未复核项写成「如实声明」的散文："
      "无编号、无责任人、无期限（实测 §H.6 与 §F.5 各 5 项），**跨版本是否仍挂着无从查证**；"
      "而测试侧的同类项（51 / 52）有台账有期限，两侧待遇不对称。按 `docs/artifacts.md` §3.1 必须逐条登记")
_led_lack = []
for _i in _i60:
    _blk = "\n".join(_obs_entry_lines(_t60, _i))
    _num = re.search(r"(?:60:)?(OBS-\d+)", _blk).group(1)
    _lack = [f for f in ("缘由", "责任人", "计划复核") if f not in _blk]
    if _lack:
        _led_lack.append(f"{_num} 缺 {'/'.join(_lack)}")
check("T30 60:OBS-nn 台账含缘由 / 责任人 / 计划复核时点", not _led_lack,
      f"{'；'.join(_led_lack)} —— 三字段是「谁来在什么时候把它处置掉」的唯一载体。"
      "只写「未独立复核」而不写缘由与期限，等于把它变成一条**永不处置**的记录："
      "本仓库实测有测试侧观察项跨 4 个版本仍挂着，正是因为没有责任人与时点")

# 30-C 冻结时间：**规则**（artifacts §4「禁止标称钟点」）已写，这里断言 8 份契约产物的冻结时间行里
# 不再出现**无来源标注**的整点钟点（标称钟点晚于实际落盘，属逻辑上不可能的值）。
_NOMINAL = re.compile(r"T?(\d{2}):00:00")
_FREEZE_SRC = ("实测", "勘误", "未采集", "标称")


def _groups_around(text, pos):
    """pos 处所在的**全部**括号组（由内向外；全角 / 半角都认，按嵌套深度配对）。

    早先按「最近的前括号 + 最近的先后括号」取组，会把 pos **之前就已闭合**的组算进来：
    实测 `原记 X（v1 首冻）/ Y（v2）均系标称` 里，Y 被判进早已闭合的 `（v1 首冻）`，
    于是明明写在勘误段里的取值被报成「未注明来源」。按深度配对是这里唯一可靠的口径。
    """
    out = []
    for m in re.finditer(r"[（(]", text):
        op, ch = m.start(), m.group(0)
        close = "）" if ch == "（" else ")"
        depth = 0
        for k in range(op, len(text)):
            if text[k] == ch:
                depth += 1
            elif text[k] == close:
                depth -= 1
                if depth == 0:
                    if op < pos < k:
                        out.append(text[op:k + 1])
                    break
    return out


def _clause(text, pos):
    """pos 处的句子（以 `。` / `；` / 表格竖线为界）。"""
    a = max(text.rfind(ch, 0, pos) for ch in ("。", "；", "|"))
    b = [text.find(ch, pos) for ch in ("。", "；", "|") if text.find(ch, pos) != -1]
    return text[a + 1:min(b) if b else len(text)]


def _freeze_row(doc, text):
    if doc.endswith(".html"):
        m = re.search(r"<tr>(?:(?!</tr>).)*冻结时间.*?</tr>", text, re.S)
    else:
        m = re.search(r"^\|\s*冻结时间\s*\|.*$", text, re.M)
    return m.group(0) if m else ""


_fz_none, _fz_bad = [], []
for _d in _CONTRACT_DOCS:
    _row = _freeze_row(_d, read(DOCS / _d) or "")
    if not _row:
        _fz_none.append(_d)
        continue
    for _m in _NOMINAL.finditer(_row):
        _gs = _groups_around(_row, _m.start())
        _ok = any(any(s in g for s in _FREEZE_SRC) for g in _gs) or any(
            s in _clause(_row, _m.start()) for s in _FREEZE_SRC)
        if not _ok:
            _fz_bad.append(f"{_d} 的 {_m.group(0)}（所在括号组与所在句子均未写来源）")
check("T30 八份契约产物的冻结时间行存在", not _fz_none,
      f"缺：{'、'.join(_fz_none)} —— 冻结时间是判定「落盘证据是否成立」与「上游依赖是否满足」的唯一依据"
      "（`docs/artifacts.md` §4）；没有这一行的产物无法被任何下游核对")
check("T30 冻结时间的整点钟点均注明来源", not _fz_bad,
      f"{'；'.join(_fz_bad)} —— 整点钟点（`T\\d\\d:00:00` / `\\d\\d:00:00`）几乎必然是**标称钟点**而非实测值："
      "本仓库实测原值 `20:00` / `22:00` **晚于**该产物末次落盘时刻 `19:15:27`，"
      "即「文件在 19:15 落盘，正文却写着 22:00 修订」——逻辑上不可能，而此前无任何断言看得见。"
      "判据（`docs/artifacts.md` §4）：整点钟点必须在其**所在括号组**内写明来源（实测 / 勘误 / 未采集 / 标称）；"
      "未采集时刻的按日粒度写，**不得另推一个钟点顶替**")

# 30-D 上游依赖 ⊆ 读取清单：**规则**（artifacts §5）要求「声明的是已消费的版本」，这里断言
# 「声明的产物 ∈ 该产物写入者的读取清单」——本仓库实测 30 声明消费 00-brief / 60-review，而两者
# 都不在 §1「读取者」/ §2「只读」列里：声明是真事实，漏的是登记表。
_up_none, _up_bad = [], []
for _d in _CONTRACT_DOCS:
    _key = Path(_d).stem
    _row = ""
    _txt = read(DOCS / _d) or ""
    if _d.endswith(".html"):
        _m = re.search(r"<tr>(?:(?!</tr>).)*上游依赖.*?</tr>", _txt, re.S)
    else:
        _m = re.search(r"^\|\s*上游依赖\s*\|(.*)$", _txt, re.M)
    if _m:
        _row = _m.group(0)
    if not _row.strip():
        _up_none.append(_d)
        continue
    _own = WRITERS.get(_key)
    for _dep in READERS:
        if _dep == _key:
            continue
        if _dep in _row and _own and _own not in READERS[_dep]:
            _up_bad.append(f"{_d} 声明消费 {_dep}，但 {_dep} 的读取者不含 {_own}")
check("T30 八份契约产物的上游依赖行存在", not _up_none,
      f"缺：{'、'.join(_up_none)} —— 「上游依赖」行是失效传播矩阵的唯一入口（`docs/artifacts.md` §5），"
      "没有它就无法判断下游是否需要重跑")
check("T30 上游依赖行声明的产物 ∈ 该写入者的读取清单", not _up_bad,
      f"{'；'.join(_up_bad)} —— 判据（`docs/artifacts.md` §5）：上游依赖行是**可机检的声明**，"
      "声明的产物必须在该产物写入者的读取清单（§1「读取者」列 / §2「只读」列）内。"
      "**对账方向固定为「声明 → 清单」**：声明属实就补清单，声明不实就订正该行。"
      "本条堵的是「产物自己声明依赖了某上游，而登记表里根本没有这个读取者」——"
      "两份文件各自都没错，单文件校验全绿，只有把两者放在一起才看得出来")

# 30-E README 目标目录树：**规则**（README 安装章节）声明了交付物清单，这里断言树里真的有它们
# —— 实测该树长期缺 `CLAUDE.md`（每次会话自动加载的工作规范）与 `.claude/settings.json`，
# 照树复制的项目会静默失去权限规则与精简版规范。
_rm_txt = read(ROOT / "README.md") or ""
_m_tree = re.search(r"目标目录树.*?```(.*?)```", _rm_txt, re.S)
_tree = _m_tree.group(1) if _m_tree else ""
_TREE_NEED = ["CLAUDE.md", "README.md", "tools/", "check-config.py", "docs/", ".claude/",
              "settings.json", "agents/", "commands/"]
_tree_lack = [t for t in _TREE_NEED if t not in _tree]
check("T30 README 目标目录树含全部交付物", bool(_tree) and not _tree_lack,
      (f"树中缺：{'、'.join(_tree_lack)}" if _tree else "README 里找不到「目标目录树」代码块") + " —— "
      "该树是「分发到新项目」的唯一清单，缺项会被照抄，且**新项目不会报任何错**："
      "少了 `CLAUDE.md` 就少了每次会话自动加载的规范，少了 `.claude/settings.json` 就少了权限闭环")
_copy_line = next((l for l in _rm_txt.split("\n") if "复制到项目根" in l), "")
check("T30 README 复制指令含被树补上的交付物", all(t in _copy_line for t in ("CLAUDE.md", "tools/", ".claude/")),
      f"复制指令未提到：{'、'.join(t for t in ('CLAUDE.md', 'tools/', '.claude/') if t not in _copy_line)} —— "
      "树补齐了但指令没跟上，读者仍会漏复制（本次实测：指令行只列了 tools/ 与 docs/，未含 `CLAUDE.md`）")

# 30-F 脚手架命令：**规范**（CLAUDE.md「四、必须确认的场景」：架构选型 / 新增分层 / 修改目录结构）
# 要求先征得同意，而 `dotnet new` / `dotnet sln` / `npm create` 正是这一类操作。实测这三条长期躺在
# **allow**（无条件免确认）里——规范与配置直接冲突，且 allow 组此前从未被任何断言覆盖。
# 断言做成**双向锁**：只断言其中一侧，放宽 allow 的改动仍可静默通过。
_SCAFFOLD_CMDS = ["Bash(dotnet new *)", "Bash(dotnet sln *)", "Bash(npm create *)"]
_sc_in_allow = [c for c in _SCAFFOLD_CMDS if c in _allow]
check("T30 allow 不含脚手架命令（架构选型类必须走确认）", not _sc_in_allow,
      f"{'、'.join(_sc_in_allow)} 仍在 allow —— 它们创建解决方案 / 项目 / 分层目录，属 CLAUDE.md"
      "「四、必须确认的场景」中的「架构选型 / 新增分层 / 修改目录结构」，**必须先征得同意**；"
      "放进 allow 就是把最需要确认的一类操作变成无条件放行")
_sc_missing_ask = [c for c in _SCAFFOLD_CMDS if c not in _ask]
check("T30 ask 含脚手架命令", not _sc_missing_ask,
      f"ask 缺 {'、'.join(_sc_missing_ask)} —— 从 allow 移出后必须落到 ask，"
      "否则它们会退回「默认首次询问」：看似等效，实则不受本表约束、也不会被任何断言看见")

# 30-G 编号前缀闭集：**规则**（artifacts §3「登记表是闭集，且由护栏机检」）已写，这里真的扫。
# 实测教训：同一类「待确认 / 假设 / 冲突上报」事实在产物里被自造出 QX / AS / QA / CAR 四个前缀，
# 跨文件检索无法收敛，而任何单文件校验都看不出来。
_art_txt = read(DOCS / "artifacts.md") or ""
_sec3 = (_art_txt.split("## 3. ID 体系") + [""])[1].split("### 3.1")[0]
_registered = set()
for _m in re.finditer(r"^\|[^|\n]*\|", _sec3, re.M):
    _registered |= set(re.findall(r"[A-Z]{1,4}", _m.group(0)))
check("T30 编号前缀登记表可解析", bool(_registered),
      "`docs/artifacts.md` §3 的 ID 前缀登记表解析不出任何前缀 —— 登记表格式变了而断言没跟上，"
      "此时闭集断言会变成「恒真」（空集包含一切），静默失效")
_ID_PAT = re.compile(r"(?<![A-Za-z0-9_§\-])([A-Z]{1,4})-(\d{1,3})(?![0-9])")
# 豁免：非产物编号的通用写法（豁免必须登记在案，且护栏会断言锚句仍然存在——它是登记披露通道，
# 不是静默放宽的通道）。
_ID_EXEMPT = {"UTF-8": "字符编码名（规范 / 说明书通用写法），非产物编号",
              "SHA-256": "摘要算法名（规范通用写法），非产物编号"}
_ID_FACE = ([ROOT / "CLAUDE.md", ROOT / "README.md", ROOT / ".claude" / "README.md"]
            + sorted(DOCS.glob("*.md")) + sorted(AGENTS.glob("*.md"))
            + sorted(COMMANDS.glob("*.md")) + sorted((ROOT / "global").glob("*.md")))
_undeclared = {}
for _p in _ID_FACE:
    for _m in _ID_PAT.finditer(read(_p) or ""):
        if _m.group(1) in _registered or _m.group(0) in _ID_EXEMPT:
            continue
        _undeclared.setdefault(_m.group(0), set()).add(str(_p.relative_to(ROOT)).replace("\\", "/"))
check("T30 编号前缀均在 artifacts §3 登记", not _undeclared,
      "；".join(f"{k}（{sorted(v)[0]} 等）" for k, v in sorted(_undeclared.items()))
      + " —— 就地自造前缀即为违规：同一类事实一旦被命名成多个前缀，跨文件检索无法收敛，"
        "而任何单文件校验都看不出来（实测：同一类「待确认」事实散成 QX / AS / QA / CAR 四个前缀）。"
        "新增前缀必须先登记再使用；历史正文里的既有前缀按 13.4 不回改，只补登记")
_stale_ex = [k for k in _ID_EXEMPT
             if not any(k in (read(_p) or "") for _p in _ID_FACE)]
check("T30 编号前缀豁免仍被使用", not _stale_ex,
      f"{'、'.join(_stale_ex)} 已不在扫描面内出现 —— 豁免是**登记披露**通道："
      "锚句消失后仍留着豁免，下次扫描面扩大时同一形态会被静默放过。要么删豁免，要么说明它还在哪")

# 30-H 活文档与代码不得出现「文件名:行号」型定位引用（**规则**见 13.4，此处断言扫描面与产物）。
# 实测事故：代码注释以架构文档的某个行号为锚点，架构升版后该锚点漂移，全部引用静默失效。
# 扫描面 = 13.4 声明的「活文档」闭集 + `src/` / `tests/` / `tools/` 的代码与注释；
# **已签署产物（00–60）不在扫描面内**——它们的既有段落按 13.4 不回改，订正走各自产物的勘误块。
_LIVE_DOCS = ["CLAUDE.md", "README.md", ".claude/README.md", ".claude/agents/", ".claude/commands/",
              "docs/artifacts.md", "docs/development-spec.md", "docs/role-protocol.md",
              "docs/state-machine.md", "docs/config-checklist.md", "docs/error-codes.md",
              "global/CLAUDE.md", "src/", "tests/", "tools/"]
_decl_134 = next((l for l in read(DOCS / "development-spec.md").split("\n") if "活文档」是闭集" in l), "")
# 判定用**枚举成员**，不用子串命中：整行是巨行，删掉某一项后，该项仍可能被同行别处提到
# （实测：删去「`tests/` 与 `tools/`」后，二者仍从同行后半句的 `tests/e2e/...` 与
# 「含 `src/` / `tests/` / `tools/` 的代码文件」里被子串命中 → 断言看似全绿、实际什么都没验）。
# 取值域 = 该行「：」之后到「的代码与注释」之前的**反引号 token 集合**。
_134_seg = _decl_134.split("：", 1)[-1].split("的代码与注释")[0]
_declared_134 = set(re.findall(r"`([^`]+)`", _134_seg))
_missing_134 = [d for d in _LIVE_DOCS if not any(t.startswith(d) for t in _declared_134)]
check("T30 13.4 活文档闭集已逐项声明", not _missing_134,
      f"13.4 闭合集未逐项声明：{'、'.join(_missing_134)} —— "
      "判定口径 = 该行列举段的**反引号 token 集合必须逐项覆盖**（子串命中不算：巨行里别处的提及"
      "会让删除静默通过）。扫描面必须与规范声明逐项对应：范围一旦靠代码隐式决定，"
      "「顺手少扫一份」不会有任何信号，而少扫的那一份里可能正躺着已漂移的行号锚点")

# 边界声明：机检只认「文件名:行号」，而规则禁止的是一切「以行号定位」的引用（符号名:行号同型）。
# 不写出边界，读者会把「护栏全 PASS」读成「没有行号引用」——实测 `tests/e2e/api/stats-lib.mjs`
# 就以「集成夹具方法:行号范围」的形式留着一处，机检全程没看见。
_LN_SCOPE_DECL = "「符号名:行号」等同型锚点不在机检范围"
check("T30 13.4 已声明行号机检范围的边界", _LN_SCOPE_DECL in _decl_134,
      "13.4 未声明行号机检的范围边界 —— 机检的形态是「文件名:行号」，「符号名:行号」等同型锚点同样违规、"
      "但不在机检面内。边界不写出来，读者会把「护栏全 PASS」读成「没有行号引用」：实测确有这种残留"
      "（取证见 13.4 本条与 `tests/e2e/api/stats-lib.mjs` 的勘误回执）")
# 豁免字面量用拼接构造：**护栏源码自身也登记在扫描面上**，写全了会自己报自己（实测三条自伤告警）。
# 豁免条目同样受「锚句仍然存在」断言的约束——它是登记披露通道，不是静默放宽的通道。
# 注：两条均指 13.4 正文那句漂移示例（**行号是被叙述对象**，不是定位引用），两种形态各一条。
_LN_EXEMPT = {"docs/development-spec.md": ["." + "md" + ":" + "544",
                                          "30-architecture" + "." + "md" + ":544"]}
_LN_SKIP_DIRS = {"bin", "obj", "node_modules", ".git", "dist", "coverage"}
# 临时验证目录的前缀**闭集**（`docs/role-protocol.md` §6 逐字声明，由下面的断言双向锁定）。
# 实测教训：该排除曾只认 `.tmp-`，而本仓库实际用的是 `_tmp-`（`tests/e2e/_tmp-verify-20260918-citefix/`）
# ——排除规则**命中 0 个文件**、形同不存在，取证目录里的故意坏样本（`*-bad.ts`）整轮留在扫描面上。
_TMP_PREFIXES = (".tmp-", "_tmp-")
_LN_EXT = tuple("." + _e for _e in _LN_REF_EXT)
_ln_files = []
for _d in _LIVE_DOCS:
    _p = ROOT / _d
    if _d.endswith("/"):
        _ln_files += [q for q in sorted(_p.rglob("*"))
                      if q.is_file() and q.suffix in _LN_EXT
                      and not _LN_SKIP_DIRS & set(q.parts)
                      # 临时验证目录（唯一前缀 + 登记绝对路径）不进扫描面：它们是被登记的取证来源，
                      # 内容可能是改动前的快照，随取证批次改名，不属于「活文档」。
                      and not any(str(x).startswith(_TMP_PREFIXES) for x in q.parts)]
    else:
        _ln_files.append(_p)
_ln_bad = []
for _p in _ln_files:
    _rel = str(_p.relative_to(ROOT)).replace("\\", "/")
    for _i, _l in enumerate((read(_p) or "").split("\n"), 1):
        for _m in _LN_REF.finditer(_l):
            if _m.group(0) in _LN_EXEMPT.get(_rel, []):
                continue
            _ln_bad.append(f"{_rel}:{_i} → {_m.group(0)}")
check("T30 活文档与代码无「文件名:行号」型定位引用", not _ln_bad,
      f"{'；'.join(_ln_bad[:8])}{' 等' if len(_ln_bad) > 8 else ''} —— 行号必然漂移："
      "实测：代码注释以架构文档的某个行号为锚点，架构升版后该锚点漂移，"
      "**全部引用静默失效且无人会回头核对**（完整实测段见 `docs/development-spec.md` 13.4）。"
      "改用稳定 ID（FR / AC / MOD / API / D / RSK / CHG / TC / BUG / REV / OBS / QX）。"
      "行号只允许作为**被叙述对象**出现（描述漂移事实本身的句子），且必须同时给出稳定 ID")
_stale_ln = [f"{k} 的豁免锚句 {lit!r}" for k, lits in _LN_EXEMPT.items() for lit in lits
             if lit not in (read(ROOT / k) or "")]
check("T30 行号豁免锚句仍然存在", not _stale_ln,
      f"{'、'.join(_stale_ln)} 已不存在 —— 豁免是登记披露通道：锚句消失后它就成了「扫描面上有一处"
      "永不检查的区域」，且不留任何痕迹。要么删豁免，要么说明该形态现在何处合法")

# 30-H 的扫描面排除规则与 `role-protocol` §6 的声明**双向锁**（与 allow/ask 双向锁同型）：
# 规则文本与护栏清单必须同时改，单边改即 FAIL。反查项挂在本条上：扫描面内不得出现
# 「形如临时目录、前缀却未声明」的目录（新增前缀必须先改 §6 与 `_TMP_PREFIXES` 两处）。
# 反查项的负向验证**覆盖不到**（MUTATIONS 只做文本替换，造不出目录）→ 手工 probe：
#   mkdir -p tests/e2e/tmp-probe-x && python tools/check-config.py   # 期望本条 FAIL
#   rmdir tests/e2e/tmp-probe-x                                     # 复原 → 复绿
# 实测 2026-09-18：加目录 → 366 项中 1 项 FAIL（本条，指名 tests/e2e/tmp-probe-x）；删后全 PASS。
_LN_TMP_SHAPE = re.compile(r"^[._]?tmp[._-]")
_TMP_DECL = "**前缀闭集：`.tmp-` / `_tmp-`**"
_rp_txt = read(DOCS / "role-protocol.md") or ""
_decl_tmp = tuple(re.findall(r"`([._][A-Za-z0-9_-]*tmp[A-Za-z0-9_-]*)`", _TMP_DECL))
_undecl_tmp = []
for _d in _LIVE_DOCS:
    if not _d.endswith("/"):
        continue
    for _q in sorted((ROOT / _d).rglob("*")):
        if _q.is_dir() and not _LN_SKIP_DIRS & set(_q.parts):
            _nm = _q.name
            if _LN_TMP_SHAPE.match(_nm) and not _nm.startswith(_TMP_PREFIXES):
                _undecl_tmp.append("/".join(_q.relative_to(ROOT).parts))
# 三合取项**分别**具名：曾共用一个「§6 未逐字声明…」的开头，§6 明明写对时也会把维护者引去改
# 一处正确的规范文本（实测：造未声明前缀目录时 fail 首句误指 §6 声明缺失）。
_tmp_why = []
if _TMP_DECL not in _rp_txt:
    _tmp_why.append("§6 未逐字声明该锚句")
if _decl_tmp != tuple(_TMP_PREFIXES):
    _tmp_why.append(f"§6 声明集合 {list(_decl_tmp)} ≠ 护栏闭集 {list(_TMP_PREFIXES)}")
if _undecl_tmp:
    _tmp_why.append("扫描面内存在未声明前缀的临时目录：" + "、".join(_undecl_tmp))
check("T30 临时目录前缀闭集与 §6 声明一致（双向锁）",
      _TMP_DECL in _rp_txt and _decl_tmp == tuple(_TMP_PREFIXES) and not _undecl_tmp,
      f"触发项 = {'；'.join(_tmp_why) or '无'} —— 该前缀是「取证目录不进静态扫描面」的唯一判据："
      "实测排除规则曾只认一个前缀，"
      "而本仓库实际用的是另一个（`tests/e2e/_tmp-verify-*`），规则命中 0 个文件、形同不存在，"
      "取证目录里的故意坏样本整轮留在扫描面上；新增前缀须同时改 §6 与护栏 `_TMP_PREFIXES`")


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
    # 扫描面覆盖证明：把号码引用挪到 spec 之外的文件（30-architecture），旧的 spec-only 扫描不会发现
    ("docs/30-architecture.md", "`code 1001`", "`code 1099`", "T15 规范引用的业务错误码均已登记"),
    ("docs/error-codes.md", "docs/51-defects.md", "docs/61-defects.md",
     "T15 错误码扫描面已登记于 error-codes.md"),
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
    # T20 门禁判据单点定义（D1 / D2 / D3）
    ("docs/state-machine.md", "## 判据定义（判定顺序之前必读）", "## 判据定义（排在判定顺序之后）",
     "T20 判据定义节存在"),
    ("docs/state-machine.md", "执行覆盖闭合", "执行覆盖大致闭合", "T20 D1 覆盖达标三条口径"),
    ("docs/state-machine.md", "### D2　其他阻断项（行 4 用）", "### D2　其他阻断项",
     "T20 D2 其他阻断项为闭集"),
    ("docs/state-machine.md", "不得降级的两类", "允许降级的两类", "T20 D3 缺陷级别判据表"),
    ("docs/state-machine.md", "级别按下文 D3 判定", "级别按经验判定", "T20 F1 / F2 指向判据定义"),
    # T28 错误码登记人 / 429 例外 / 段标签 / 行号引用
    ("docs/error-codes.md", "**实现期**——engineer 取号并登记", "**实现期**——由主对话代登记",
     "T28 错误码登记人按阶段归属已声明"),
    ("docs/error-codes.md", "**架构期**——software-architect 取号", "**架构期**——由 product-manager 取号",
     "T28 错误码登记人按阶段归属已声明"),
    ("docs/artifacts.md", "、`docs/error-codes.md`（**仅实现期新增登记**，见下表后注）", "",
     "T28 artifacts §2 已列出 error-codes.md 的写入者"),
    ("docs/10-prd.md", "**新增业务错误码由 engineer 按", "**新增业务错误码由主对话按",
     "T28 10-prd FR-10 的 engineer 归属未被单边改动"),
    ("docs/development-spec.md", "唯一例外是 `429` 限流：HTTP 429 而响应体 `code=1001`",
     "唯一例外是 `429` 限流：HTTP 429 而响应体 `code=1002`", "T28 429 例外双落点逐字一致"),
    ("docs/development-spec.md", "| 429 | **例外：HTTP 状态码，非 `ApiResult.code` 取值**", "| 429 | **限流**",
     "T28 6.4 表含 429 行且标为 HTTP 例外"),
    ("docs/30-architecture.md", "`429` 限流（响应体 `code=1001`）", "`429` 限流（响应体 `code=1002`）",
     "T28 架构侧 429 → code=1001 定义仍在"),
    ("docs/error-codes.md", "`0–500` 段与 HTTP 状态码对齐", "`4xx/5xx` 段与 HTTP 状态码对齐",
     "T28 错误码段标签与 6.4 一致（0–500 段）"),
    ("docs/error-codes.md", "## 登记流程", "见 " + "`docs/development-spec.md" + ":131` 的说明。\n\n## 登记流程",
     "T28 error-codes.md 内无行号引用"),
    # 写入权的第二个落点：artifacts §2（由上面那条突变覆盖）与 engineer.md 的「写入范围」必须同时成立
    (".claude/agents/engineer.md", "、`docs/error-codes.md`（**仅追加「新增业务错误码」的登记行**",
     "（**仅追加「新增业务错误码」的登记行**", "T28 engineer 角色文件的写入范围已同步"),
    # T21 规范侧五项新增判据
    ("docs/development-spec.md", "随机源可控性", "随机源确定性", "T21 6.6 随机源可控性（架构六问）"),
    ("docs/development-spec.md", "与没有断言等价", "与加强断言等价", "T21 7.5 修复类断言必须实测变红"),
    ("docs/development-spec.md", "### 7.6 用例独立性与不稳定判定", "### 7.6 用例独立性与稳定性",
     "T21 7.6 用例独立性与不稳定判定"),
    ("docs/development-spec.md", "阈值只取契约原值", "阈值可酌情自定", "T21 7.7 统计与概率类验收"),
    ("docs/development-spec.md", "两类文本的处置方向相反", "两类文本可统一处置",
     "T21 13.4 实然陈述随事实同步"),
    ("docs/development-spec.md", "修复类改动已做负向验证", "修复类改动已做回归验证",
     "T21 附录 A 自查清单含新增三项"),
    # T22 环境占用与破坏性操作（role-protocol §8）
    ("docs/role-protocol.md", "## 8. 环境占用与破坏性操作", "## 8. 环境协调", "T22 role-protocol §8 节存在"),
    ("docs/role-protocol.md", "谁用谁登记", "用完即还", "T22 §8 实例与端口谁用谁登记"),
    ("docs/role-protocol.md", "luckydraw_dev", "luckydraw_prod", "T22 §8 库与 Redis 分界"),
    ("docs/role-protocol.md", "事前公示", "事前知会", "T22 §8 破坏性操作三段式"),
    ("docs/role-protocol.md", "非本批流量", "非本报告流量", "T22 §8 共用证据文件的归属"),
    ("docs/role-protocol.md", "超时 + 共享配额", "超时 + 网络抖动", "T22 §5 用例隔离缺陷签名"),
    # T23 新规则的落地落点（artifacts ↔ 各角色提示词）
    ("docs/artifacts.md", "60:OBS-nn", "70:OBS-nn", "T23 artifacts 60:OBS-nn 命名空间"),
    ("docs/artifacts.md", "三类之外一律视为未复核", "三类之外视为已复核",
     "T23 artifacts「有理由的未复跑」为闭集"),
    (".claude/agents/code-reviewer.md", "计划复核时点", "复核时间待定",
     "T23 code-reviewer 未复核项登记义务"),
    (".claude/agents/test-designer.md", "AC ↔ TC 覆盖对照", "AC 与 TC 对照",
     "T23 test-designer AC↔TC 覆盖对照（D1 唯一分母）"),
    (".claude/agents/test-designer.md", "前置预算必须核对", "前置预算建议核对",
     "T23 test-designer 用例独立性与前置预算"),
    (".claude/agents/test-executor.md", "D3 判据表", "D3 经验判断",
     "T23 test-executor 按 D3 定级 / 按 D1 判覆盖"),
    (".claude/agents/test-executor.md", "7.7 执行", "7.7 参考",
     "T23 test-executor 统计类与 §8 环境纪律"),
    (".claude/agents/engineer.md", "等价负向样本", "等价回归样本", "T23 engineer 修复类负向验证栏"),
    # T24 状态机判定闭环（第四次修订）
    ("docs/state-machine.md", "无其他阻断项（D2）", "无其他阻断项",
     "T24 五个判定行均含「无其他阻断项（D2）」前提"),
    ("docs/state-machine.md", "`未闭环` 的 `REV-xx` 审查问题", "`未闭环` 的 REV 审查问题",
     "T24 D2 闭集含 60 未闭环审查问题"),
    ("docs/state-machine.md", "由 test-executor 标注、`60:OBS-nn` 由 code-reviewer 标注",
     "由 test-executor 标注", "T24 D2 双方标注义务"),
    ("docs/state-machine.md", "漏标注者按「阻断」处理", "漏标注者按「非阻断」处理", "T24 D2 双方标注义务"),
    ("docs/state-machine.md", "把 52 落为「可发布」", "把 52 落为「有条件发布」", "T24 行 0.5 接受分支落盘"),
    ("docs/state-machine.md", "按本表重判", "按行 2 执行", "T24 行 3 接受分支按本表重判"),
    ("docs/state-machine.md", "一并登记其接受人 / 理由 / 补丁计划", "只登记覆盖缺口编号",
     "T24 行 3.5 落盘含已接受一般缺陷"),
    ("docs/state-machine.md", "其他阻断项（D2 四类）", "其他阻断项",
     "T24 修复循环 a 对 D2 四类分派 owner"),
    ("docs/state-machine.md", "按领域归属", "按提交顺序", "T24 修复循环 a 对 D2 四类分派 owner"),
    (".claude/agents/test-executor.md", "可机械提取的结构化字段", "散文式说明",
     "T24 test-executor OBS 阻断标注"),
    (".claude/agents/test-executor.md", "52 落为 **「可发布」**", "52 落为 **「有条件发布」**",
     "T24 test-executor 行 0.5 落盘"),
    (".claude/agents/code-reviewer.md", "逐条标注「阻断 / 非阻断」", "逐条说明影响",
     "T24 code-reviewer 60:OBS 阻断标注"),
    (".claude/agents/code-reviewer.md", "不得用 `Write` 整份重写", "可用 `Write` 整份重写",
     "T24 code-reviewer 禁整份重写"),
    (".claude/agents/code-reviewer.md", "Write, Edit", "Write", "T24 code-reviewer 具备 Edit"),
    (".claude/agents/product-manager.md", "先落盘、再打磨", "后落盘、再打磨",
     "T24 五个角色含「先落盘、再打磨」"),
    (".claude/agents/prototype-designer.md", "先落盘、再打磨", "后落盘、再打磨",
     "T24 五个角色含「先落盘、再打磨」"),
    (".claude/agents/software-architect.md", "先落盘、再打磨", "后落盘、再打磨",
     "T24 五个角色含「先落盘、再打磨」"),
    (".claude/agents/engineer.md", "先落盘、再打磨", "后落盘、再打磨",
     "T24 五个角色含「先落盘、再打磨」"),
    (".claude/agents/test-designer.md", "先落盘、再打磨", "后落盘、再打磨",
     "T24 五个角色含「先落盘、再打磨」"),
    (".claude/agents/software-architect.md", "六个契约问题", "五个契约问题",
     "T24 架构六问双落点（architect ↔ spec）"),
    (".claude/agents/software-architect.md", "随机源可控性", "随机源确定性",
     "T24 架构六问双落点（architect ↔ spec）"),
    ("docs/development-spec.md", "按名称引用，不按序号", "按序号引用",
     "T24 6.6 引用按名称不按序号"),
    ("README.md", "│   ├── development-spec.md", "│   ├── (removed)",
     "T24 README 安装目录树含全部 7 份 docs"),
    # T25 规范强制要求的命令必须落在 allow（跨「规范 ↔ 配置」的负向验证）
    (".claude/settings.json", '"Bash(node tests/e2e/lib/launch-api.mjs*)",\n', "",
     "T25 规范强制要求的命令均已免确认"),
    (".claude/settings.json", '"Bash(npm install*)",', '"Bash(npm install*)",\n      "Bash(node *)",',
     "T25 要求的命令未被 ask/deny 遮蔽"),
    (".claude/settings.json", '"Bash(npm ci)",', '"Bash(npm ci)",\n      "Bash(npm ci)",',
     "T25 allow 无重复条目"),
    (".claude/settings.json", '"defaultMode": "default",', '"defaultMode" "default",',
     "T25 settings.json 合法 JSON 且含三组权限规则"),
    (".claude/settings.json", "node_modules/vue-tsc/bin/vue-tsc.js",
     "node_modules/other-tool/bin/other.js",
     "T25 allow 的直调入口均在规范里点过名"),
    ("CLAUDE.md", "node node_modules/@playwright/test/cli.js install chromium",
     "node node_modules/@playwright/test/cli.js install firefox",
     "T25 免确认规则的规范来源仍在"),
    (".claude/README.md", "node tests/e2e/lib/launch-api.mjs", "node tests/e2e/lib/launch-api-x.mjs",
     "T25 免确认入口已登记在 .claude/README.md"),
    # T26 OBS 编号的跨命名空间裸引用（编号载体里的**事实**）
    ("docs/60-review.md", "`OBS-03`", "`OBS-XX`",
     "T26 60-review.md 的跨命名空间裸引用已登记"),
    ("docs/52-qa-report.md", "| OBS-11 |", "| OBS-X1 |",
     "T26 52-qa-report.md 的跨命名空间裸引用已登记"),
    ("docs/artifacts.md", "以 `51-defects.md` / `52-qa-report.md` / `60-review.md` 三份文件为扫描面",
     "以 `51-defects.md` / `52-qa-report.md` 两份文件为扫描面", "T26 OBS 引用扫描面已声明"),
    ("docs/artifacts.md", "环境 / 证据 / 工具链类", "环境 / 证据 / 依赖类",
     "T26 阻断判别口径四处逐字一致"),
    (".claude/agents/test-executor.md", "默认按**阻断**登记", "默认按**非阻断**登记",
     "T26 阻断默认档三处逐字一致"),
    # T27 §E 提案的前提与现状
    (".claude/agents/test-designer.md", "（接口契约与错误语义，用于接口级与异常路径用例设计）",
     "（接口契约与错误语义，用于接口级与异常路径用例设计；另读 `docs/40-changelog.md`）",
     "T27 并行提案前提: test-designer 输入不含 40-changelog"),
    ("docs/config-checklist.md", "未绑定 0 条", "未绑定 3 条", "T27 §E.1 现状与绑定结论仍在"),
# T11 行 3.5 的位置与前提（第六次修订补齐：此前 T11 的 9 条断言零突变覆盖，
# 表注却声称「行 3.5 的位置与前提由 T11 守护」——断言有辨别力，只是从未被负向验证过）。
# 位置类突变用「在表末追加一行同号行」实现：row_pos 取**最后一次**出现的行号，追加即等于把该行下移。
    ('docs/state-machine.md', '\n## 修复循环（a → b → c）', '\n| **3.5** | 无未闭环致命 / 严重；覆盖不达标；无其他阻断项（D2）；且 F6 = 已转达 | **有条件发布** | x |\n\n## 修复循环（a → b → c）',
     'T11 行3.5 位于行4之前'),
    ('docs/state-machine.md', '\n## 修复循环（a → b → c）', '\n| 3 | 无未闭环致命 / 严重；无其他阻断项（D2）；存在一般缺陷且 F3 = 已转达 | **有条件发布** | x |\n\n## 修复循环（a → b → c）',
     'T11 行3.5 位于行3之后'),
    ('docs/state-machine.md', '**无未闭环致命 / 严重**；**覆盖不达标**', '**覆盖不达标**',
     'T11 行3.5 含无未闭环致命/严重前提'),
    ('docs/state-machine.md', '且 **F6 = 已转达**', '且 **F6 = 未转达**',
     'T11 行3.5 含 F6=已转达'),
    ('docs/state-machine.md', '（**覆盖缺口编号 / 接受人 / 理由 / 补丁计划**', '（**含残留缺陷 / 覆盖缺口编号 / 接受人 / 理由 / 补丁计划**',
     'T11 行3.5 复用既有结论枚举'),
    # T29 状态机表注 / 历史节分离与行 5 的 D2 披露义务（第五次修订）
    ("docs/state-machine.md", "**表注（修订本表前必读）", "**修订说明（历史记录）",
     "T29 状态机表注位于判定顺序与修复循环之间"),
    ("docs/state-machine.md", "**行 3.5 必须位于行 4 之前**", "**行 3.5 可以位于行 4 之后**",
     "T29 表注含三条行序约束"),
    ("docs/state-machine.md", "行 5 是本表唯一的「D2 刻意例外」", "行 5 与其他判定行一样含 D2 前提",
     "T29 行 5 的 D2 刻意例外已声明"),
    ("docs/state-machine.md", "一并被接受，但必须逐条登记披露", "一并放行，无需登记",
     "T29 行 5 落盘含 D2 披露义务"),
    ("docs/state-machine.md", "本表历次修订的**历史因果**",
     "本表历次修订的**历史因果**（行 0.5 一旦下移将永远不可达）",
     "T29 表注与历史节分离（现行约束未混回历史节）"),
    ("docs/state-machine.md", "由 **T29** 守护", "由 **T30** 守护",
     "T29 表注声明守护归属（T11 / T24 / T29）"),
    ("docs/state-machine.md", "契约冲突 / 未消解的待确认清单项", "契约冲突",
     "T29 行 5 登记的 D2 四类与 D2 闭集同向"),
    # T30 产物侧合规：22 条断言的负向验证（M3 —— 每条新断言都必须有一个能被它抓住的变异）。
    # 变异一律落在**产物**上（而不是规则文本上）：本块的断言对象就是产物，若变异改的是规范措辞，
    # 那证明的是「规范还在」，恰好放过本块要堵的那一类（规则写在纸上、事实挂在文件里）。
    # 30-A 阻断列：表头缺列 / 取值越界各一例（两种失败模式必须分别可捕获，只测一种会漏掉另一种）
    ("docs/51-defects.md", "| 编号 | 内容 | 判定依据 | 关联 TC | 阻断 / 非阻断 |",
     "| 编号 | 内容 | 判定依据 | 关联 TC | 影响 |",
     "T30 51-defects.md 的 OBS 条目均含「阻断 / 非阻断」列"),
    ("docs/51-defects.md", "| 非阻断 |", "| 不阻断 |",
     "T30 51-defects.md 的 OBS 阻断取值合法且无漏标"),
    ("docs/60-review.md", "| 编号 | 缘由 | 责任人 | 计划复核时点 | 阻断 / 非阻断 |",
     "| 编号 | 缘由 | 责任人 | 计划复核时点 | 影响 |",
     "T30 60-review.md 的 OBS 条目均含「阻断 / 非阻断」列"),
    ("docs/60-review.md", "| 非阻断 |", "| 不阻断 |",
     "T30 60-review.md 的 OBS 阻断取值合法且无漏标"),
    # 52 的两条在 test-executor 补齐该列之后才能构造（补列前该表头里没有「阻断」二字，
    # 变异无法落在「列存在但取值越界」这一侧——顺序上必须先有产物，才能有对产物的变异）
    ("docs/52-qa-report.md", "| 编号 | 内容 | 关联 TC | 阻断 / 非阻断 |",
     "| 编号 | 内容 | 关联 TC | 影响 |",
     "T30 52-qa-report.md 的 OBS 条目均含「阻断 / 非阻断」列"),
    ("docs/52-qa-report.md", "| 非阻断 |", "| 不阻断 |",
     "T30 52-qa-report.md 的 OBS 阻断取值合法且无漏标"),
    # 30-B 审查侧台账：整张表消失 / 单条缺字段
    ("docs/60-review.md", "| 60:OBS-", "| 60X:OBS-",
     "T30 60-review 存在 60:OBS-nn 未复核项台账"),
    ("docs/60-review.md", "| 60:OBS-01 | 缘由：", "| 60:OBS-01 | 起因：",
     "T30 60:OBS-nn 台账含缘由 / 责任人 / 计划复核时点"),
    # 30-C 冻结时间：行缺失 / 行在但钟点无来源（后者才是本仓库真实发生过的形态——
    # 原值 `20:00` 晚于落盘时刻 `19:15:27`，行本身一直在，此前没有任何断言看得见）
    ("docs/60-review.md", "| 冻结时间 | 2026-09-17 |", "| 冻结时刻 | 2026-09-17 |",
     "T30 八份契约产物的冻结时间行存在"),
    ("docs/60-review.md", "| 冻结时间 | 2026-09-17 |",
     "| 冻结时间 | 2026-09-17T10:00:00+08:00 |",
     "T30 冻结时间的整点钟点均注明来源"),
    # 30-D 上游依赖：行缺失 / 声明了清单外的读取关系
    ("docs/60-review.md", "| 上游依赖 |", "| 上游输入 |",
     "T30 八份契约产物的上游依赖行存在"),
    ("docs/10-prd.md", "| 上游依赖 | `docs/00-brief.md` **v2**",
     "| 上游依赖 | `docs/60-review.md`、`docs/00-brief.md` **v2**",
     "T30 上游依赖行声明的产物 ∈ 该写入者的读取清单"),
    # 30-E README 目标树 / 复制指令
    ("README.md", "│   └── check-config.py", "│   └── guard.py",
     "T30 README 目标目录树含全部交付物"),
    ("README.md", "把 `CLAUDE.md`、`README.md`、`tools/`、`docs/` 复制到项目根",
     "把 `README.md`、`tools/`、`docs/` 复制到项目根",
     "T30 README 复制指令含被树补上的交付物"),
    # 30-F 脚手架命令的**双向锁**：只测一侧的话，放宽 allow 的改动仍可静默通过
    (".claude/settings.json", '"Bash(npm ci)",', '"Bash(npm ci)", "Bash(dotnet new *)",',
     "T30 allow 不含脚手架命令（架构选型类必须走确认）"),
    (".claude/settings.json", '"Bash(npm create *)"', '"Bash(npm create-x *)"',
     "T30 ask 含脚手架命令"),
    # 30-G 编号前缀闭集：登记表解析不出（空集包含一切 → 断言恒真）/ 自造前缀 / 过期豁免
    # 破坏点必须**打断子串**：原写法在标题尾部追加「（登记表）」，而解析用的是
    # `split("## 3. ID 体系")` —— 子串仍在，解析照旧成功、变异空转（实测未被捕获）。
    ("docs/artifacts.md", "## 3. ID 体系", "## 3. ID体系",
     "T30 编号前缀登记表可解析"),
    ("docs/config-checklist.md", "## A. 产物登记表回溯",
     "QZ-01：见下。\n\n## A. 产物登记表回溯",
     "T30 编号前缀均在 artifacts §3 登记"),
    # 过期豁免用「塞一条从未出现的豁免」构造：让锚句从扫描面消失需要同时改动 2–3 份文件，
    # 单条突变做不到（这一条同时也是对豁免机制本身的负向验证）
    ("tools/check-config.py", '"SHA-256": "摘要算法名（规范通用写法），非产物编号"',
     '"SHA-256": "摘要算法名（规范通用写法），非产物编号",\n'
     '              "ZZZ-99": "自测夹具：一条从未在扫描面出现过的豁免"',
     "T30 编号前缀豁免仍被使用"),
    # 30-H 行号引用：闭集声明缺项 / 扫描面上出现行号引用 / 豁免锚句消失
    ("docs/development-spec.md", "，以及 `src/`、`tests/` 与 `tools/` 的代码与注释。",
     "，以及 `src/` 的代码与注释。", "T30 13.4 活文档闭集已逐项声明"),
    ("docs/config-checklist.md", "## E. 已知优化（未启用，改动前先读这里）",
     "见 " + "`docs/development-spec.md" + ":120` 的说明。\n\n## E. 已知优化（未启用，改动前先读这里）",
     "T30 活文档与代码无「文件名:行号」型定位引用"),
    ("docs/development-spec.md", "." + "md" + ":" + "544", "." + "md" + " 544",
     "T30 行号豁免锚句仍然存在"),
    # 30-H 扩面（2026-09-18）：断言必须真能看见代码文件的「文件:行号」形态——原模式只认
    # `#LNN` / `.md:NN` 两式，实测漏掉 6 处（全在 `tests/e2e/api/*.mjs` 与 `qa.spec.ts`）。
    ("tests/e2e/api/stats-suite.mjs", " * TC-78 / TC-79 / TC-79b 概率统计执行（test-executor，覆盖补做）。",
     " * TC-78 / TC-79 / TC-79b 概率统计执行（test-executor，覆盖补做；锚点 `DrawService"
     + ".cs:312`）。",   # 拼接构造：变异串本身落在扫描面上，写全了会自己报自己
     "T30 活文档与代码无「文件名:行号」型定位引用"),
    ("docs/role-protocol.md", "**前缀闭集：`.tmp-` / `_tmp-`**", "**前缀闭集：`.tmp-` 与 `_tmp-`**",
     "T30 临时目录前缀闭集与 §6 声明一致（双向锁）"),
    ("docs/development-spec.md", "「符号名:行号」等同型锚点不在机检范围", "「符号名:行号」等同型锚点不在机检口径内",
     "T30 13.4 已声明行号机检范围的边界"),
    # 30-A 的第二个盲区（2026-09-18）：编号**缺号** —— 上面两条只遍历「已存在的行」，
    # 静默丢号时全绿。51 的 OBS-09 恰有 `>` 前缀的缺席登记（「与 51 的 OBS-08 为同一事实」），
    # 把该登记行的编号改掉即等于「缺号未登记」→ 期望被新断言捕获。
    ("docs/51-defects.md", "| OBS-09 | **无此条** |", "| OBS-12 | **无此条** |",
     "T30 51-defects.md 的 OBS 编号无未登记缺号"),
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
        # 字节级读写：read_text/write_text 会做换行翻译（Windows 上 CRLF→LF→CRLF；Linux 上 CRLF→LF 且**不还原**），
        # 突变还原会把仓库的换行风格改掉 —— 自测本身成了工作区污染源
        original = path.read_bytes()
        # ① 换行风格无关：本仓工作区 CRLF、git blob LF（core.autocrlf=true），锚点里写裸 LF
        #    会在工作区**永不命中**、变异静默失效（实测 1 条长期如此，自测一跑就现形）。
        #    故按**目标文件自身的**风格归一锚点与替换文本，而不是要求写变异的人记住 CRLF。
        _style = b"\r\n" if b"\r\n" in original else b"\n"
        _nl = lambda b: b.replace(b"\r\n", b"\n").replace(b"\n", _style)
        ob, nb = _nl(old.encode("utf-8")), _nl(new.encode("utf-8"))
        # ② 突变**只能改断言逻辑区**：`MUTATIONS` 表与断言逻辑同处一个文件，replace 全部落点会
        #    把变异**自己的字面量**也改掉 → 护栏源码语法崩溃 → 子进程无 JSON → 被判「未捕获」
        #    （实测 1 条）。故替换只在 `MUTATIONS = [` **之前**生效，表自身逐字节不动。
        # 只有**护栏源码自己**含变异表；其它目标文件整份都是断言逻辑，不切分。
        _cut = original.find(b"\nMUTATIONS = [")
        if _cut < 0:
            _cut = len(original)
        _head, _tail = original[:_cut], original[_cut:]
        # 「先断言变更确实发生」：不先断言，replace 未匹配会静默不动，把「没验证」当成「验证通过」
        if ob not in _head:
            _why = ("（锚点在表内、不在断言逻辑区——变异不能改自己）" if ob in original
                    else "（该锚点在磁盘上不存在）")
            bad.append(f"{rel}: 突变目标不在断言逻辑区 {_why}（{old!r}）")
            print(f"  ✗ [{rel}] 突变目标不在断言逻辑区 {_why}: {old!r}")
            continue
        try:
            # 替换**全部**落点：只改第一处的话，锚句仍在同一文件的其他位置，突变根本没破坏事实
            # （实测教训：两条锚句各在同一文件出现 2 次，用 count=1 突变时空转，差点被当成"护栏瞎了"）
            hits = _head.count(ob)
            _mh = _head.replace(ob, nb)
            if _mh == _head:
                bad.append(f"{rel}: 变更未生效")
                print(f"  ✗ [{rel}] 变更未生效")
                continue
            mutated = _mh + _tail
            path.write_bytes(mutated)
            if path.read_bytes() == original:
                bad.append(f"{rel}: 变更未生效")
                print(f"  ✗ [{rel}] 变更未生效")
                continue
            proc = subprocess.run([sys.executable, me, "--json"],
                                  capture_output=True, text=True, encoding="utf-8")
            # 子进程没吐出 JSON = 护栏自身跑不起来（例如语法崩溃）。这**不是**「未被捕获」：
            # 混为一谈会把「机制坏了」读成「断言很强」——实测正是如此被误判过一次。
            try:
                got = json.loads(proc.stdout or "{}").get("failures", [])
            except json.JSONDecodeError:
                bad.append(f"{rel}: 护栏子进程未产出 JSON（rc={proc.returncode}）"
                           f"：{proc.stderr.strip()[:180]}")
                print(f"  ✗ [{rel}] 子进程未产出 JSON（rc={proc.returncode}）")
                continue
            if any(expect in g for g in got):
                print(f"  ✓ [{rel}] 已捕获（突变 {hits} 处）→ {expect}")
            else:
                bad.append(f"{rel}: 突变未被捕获（期望 {expect}）")
                print(f"  ✗ [{rel}] 未被捕获（突变 {hits} 处），期望 {expect}")
        except Exception as exc:  # noqa: BLE001
            bad.append(f"{rel}: 自测异常 {exc!r}")
            print(f"  ✗ [{rel}] 异常: {exc!r}")
        finally:
            path.write_bytes(original)  # 必须还原（字节级：还原 = 与突变前逐字节相同）
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

def _git(args):
    """只用于「校验基线」自证：读取当前 HEAD 与工作区状态。不读历史提交、不做任何写入。"""
    try:
        r = subprocess.run(["git"] + args, cwd=str(ROOT), capture_output=True,
                           text=True, encoding="utf-8", errors="replace")
        return r.stdout.strip() if r.returncode == 0 else ""
    except Exception:  # noqa: BLE001
        return ""


# 校验基线（M2）：护栏的结论只在**某一份代码 + 某一份工作区**上成立。实测教训：在没有基线的结论上
# 复述「N 项断言全 PASS」，读者无法判断它说的是哪一版护栏、工作区是否已改（12+ 个已修改未提交的
# 文件就是「规范已改、证据没落盘」的形态）。基线只自报、不参与判定；--json 模式不打印（自测逐条调用）。
_head = _git(["rev-parse", "--short", "HEAD"])
_dirty = [x for x in _git(["status", "--porcelain"]).splitlines() if x.strip()]
print(f"校验基线：HEAD {_head or ch} / 工作区 {'干净' if not _dirty else f'脏 {len(_dirty)} 个文件'}")
print(f"共 {len(checks)} 项检查")
if failures:
    print(f"FAIL: {len(failures)} 项未通过")
    for f in failures:
        print("  ✗ " + f)
    sys.exit(1)
print("全部 PASS ✓")
