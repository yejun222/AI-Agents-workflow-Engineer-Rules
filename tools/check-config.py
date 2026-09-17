#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Claude Code 多角色配置一致性校验（tools/check-config.py）

用法：python3 tools/check-config.py
改动角色 / 命令 / 文档后运行；全部 PASS 才可交付。stdlib only，无外部依赖。
"""
import re
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

print(f"共 {len(checks)} 项检查")
if failures:
    print(f"FAIL: {len(failures)} 项未通过")
    for f in failures:
        print("  ✗ " + f)
    sys.exit(1)
print("全部 PASS ✓")
