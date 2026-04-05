#!/usr/bin/env python3
import argparse
from datetime import datetime
from pathlib import Path
import json
import re
import subprocess


LONG_DIR_NAME = "长期主文档"
ITER_DIR_NAME = "迭代记录"
LONG_INDEX_NAME = "主文档索引.md"
ITER_INDEX_NAME = "迭代记录索引.md"
UI_SYSTEM_DIR_NAME = "UI自动生成系统"
UI_SYSTEM_OVERVIEW_NAME = "00_总览.md"
AGENT_RULE_DOC = "协作与执行/Agent 启动与验收规范.md"
AGENT_STATUS_HEADING = "迭代文档默认分工状态"
AGENT_LINE_RE = re.compile(r"^\s*-?\s*Agent\s*([1-6])：(.+?)\s*$")
ALL_AGENT_MAX = 6
DEFAULT_PLACEHOLDERS = {
    "完成项": {"- 暂无"},
    "风险与阻塞": {"- 暂无"},
    "下一步": {"- 待补充"},
}
SESSION_BOOTSTRAP_DOC = "协作与执行/Codex新会话必读.md"
TASK_WRAPUP_DOC = "协作与执行/任务完成后自动维护文档.md"
LEGACY_LONG_DOCS = {
    f"{LONG_DIR_NAME}/方案与数据/Holmas UI 自动生成系统长期方案.md",
    f"{LONG_DIR_NAME}/方案与数据/UI Prefab 自动生成系统 Agent 规划入口.md",
    f"{LONG_DIR_NAME}/方案与数据/UI 自动生成系统隔离化孵化方案 v2.md",
    f"{LONG_DIR_NAME}/方案与数据/UI 自动生成系统隔离化孵化方案 v2 执行派工单与 Skill 规范.md",
}
TOPIC_PATTERNS = [
    ("文档整理", r"文档|主文档|迭代记录|索引|总览|规范|模板|跳转页|规则文档|update_project_docs|finalize_task"),
    ("流程与协作", r"会话|文档维护|交接|协作|启动卡|收尾|流程|自动维护"),
    ("审查与修复", r"Agent\s*6|审查|复审|修复|review|挑刺"),
    ("测试与验证", r"测试|验证|回归|smoke|qa|batchmode"),
    ("UI 与联调", r"UI|界面|prefab|presenter|controller|联调|图标"),
    ("配置表与数值扩展", r"配置表|导表|csv|json|bytes|catalog|数值|buildingtable|leveltable"),
    ("任务与长期进度", r"任务|成长|长期|进度|奖励|建筑升级|agency|playerLevel|playerlevel"),
    ("地图与棋盘", r"地图|棋盘|terrain|board|level|扫雷|开图|猫池"),
    ("边界与骨架", r"Shared|DTO|骨架|入口|bootstrap|组合层|boundary"),
]
PROCESS_ONLY_PATTERN = re.compile(
    r"文档|主文档|迭代记录|索引|启动卡|口令|模板|helper|收尾|去重|精简|pre-commit|commit helper|update_project_docs|finalize_task",
    re.IGNORECASE,
)
COMMIT_TITLE_PREFIX = {
    "文档整理": "文档：",
    "流程与协作": "流程：",
    "审查与修复": "修复：",
    "测试与验证": "测试：",
    "UI 与联调": "UI：",
    "配置表与数值扩展": "配置：",
    "任务与长期进度": "玩法：",
    "地图与棋盘": "玩法：",
    "边界与骨架": "架构：",
    "通用实现": "提交：",
    "未识别": "提交：",
}
PENDING_COMMIT_CACHE_RELATIVE = Path("codex") / "pending_commit_suggestion.json"
COMMIT_SEQUENCE_START = 50
PLAN_PROGRESS_HEADING = "完成情况"
PLAN_STATUS_VALUES = {"未开始", "进行中", "已完成"}


def ensure_dirs(doc_root: Path):
    long_dir = doc_root / LONG_DIR_NAME
    iter_dir = doc_root / ITER_DIR_NAME
    long_dir.mkdir(parents=True, exist_ok=True)
    iter_dir.mkdir(parents=True, exist_ok=True)
    return long_dir, iter_dir


def markdown_title(path: Path) -> str:
    try:
        text = path.read_text(encoding="utf-8")
    except FileNotFoundError:
        return path.stem
    for line in text.splitlines():
        if line.startswith("# "):
            return line[2:].strip()
    return path.stem


def relative_markdown_link(path: Path, root: Path) -> str:
    return path.relative_to(root).as_posix()


def absolute_doc_link(path: Path, root: Path) -> str:
    return f"{root.as_posix()}/{relative_markdown_link(path, root)}"


def extract_section_bullets(path: Path, heading: str):
    try:
        text = path.read_text(encoding="utf-8")
    except FileNotFoundError:
        return []
    lines = text.splitlines()
    target = f"## {heading}"
    bullets = []
    capture = False
    for line in lines:
        if line.strip() == target:
            capture = True
            continue
        if capture and line.strip().startswith("#"):
            break
        if capture and line.strip().startswith("- "):
            bullets.append(line.strip())
    return bullets


def extract_section_bullets_from_text(text: str, heading: str):
    lines = text.splitlines()
    target = f"## {heading}"
    bullets = []
    capture = False
    for line in lines:
        if line.strip() == target:
            capture = True
            continue
        if capture and line.strip().startswith("#"):
            break
        if capture and line.strip().startswith("- "):
            bullets.append(line.strip())
    return bullets


def extract_section_text(path: Path, heading: str) -> str:
    try:
        text = path.read_text(encoding="utf-8")
    except FileNotFoundError:
        return ""
    lines = text.splitlines()
    target = f"## {heading}"
    body = []
    capture = False
    for line in lines:
        if line.strip() == target:
            capture = True
            continue
        if capture and line.strip().startswith("## "):
            break
        if capture:
            stripped = line.strip()
            if stripped:
                body.append(stripped)
    return "\n".join(body).strip()


def extract_prefixed_value(path: Path, prefix: str) -> str:
    try:
        text = path.read_text(encoding="utf-8")
    except FileNotFoundError:
        return ""
    for line in text.splitlines():
        stripped = line.strip()
        if stripped.startswith(prefix):
            return stripped.replace(prefix, "", 1).strip()
    return ""


def extract_prefixed_value_from_bullets(bullets, prefix: str) -> str:
    for line in bullets:
        stripped = line.strip()
        if stripped.startswith(prefix):
            return stripped.replace(prefix, "", 1).strip()
    return ""


def extract_plan_progress(path: Path):
    bullets = extract_section_bullets(path, PLAN_PROGRESS_HEADING)
    status = extract_prefixed_value_from_bullets(bullets, "- 当前状态：")
    note = extract_prefixed_value_from_bullets(bullets, "- 进度说明：")
    if status not in PLAN_STATUS_VALUES:
        status = "未开始"
    return {
        "status": status,
        "note": note,
    }


def normalize_agent_status_line(line: str) -> str:
    stripped = line.strip()
    if stripped.startswith("- "):
        stripped = stripped[2:].strip()
    match = AGENT_LINE_RE.match(stripped)
    if not match:
        raise ValueError(f"非法的 agent 状态行：{line}")
    return f"- Agent {match.group(1)}：{match.group(2).strip()}"


def agent_number_from_line(line: str):
    match = AGENT_LINE_RE.match(line.strip())
    if not match:
        return None
    return int(match.group(1))


def load_default_agent_statuses(doc_root: Path):
    path = doc_root / LONG_DIR_NAME / AGENT_RULE_DOC
    bullets = extract_section_bullets(path, AGENT_STATUS_HEADING)
    if len(bullets) != ALL_AGENT_MAX:
        raise ValueError(f"{path} 的 “{AGENT_STATUS_HEADING}” 必须正好包含 {ALL_AGENT_MAX} 条 agent 状态")
    normalized = [normalize_agent_status_line(line) for line in bullets]
    numbers = [agent_number_from_line(line) for line in normalized]
    if numbers != list(range(1, ALL_AGENT_MAX + 1)):
        raise ValueError(f"{path} 的 “{AGENT_STATUS_HEADING}” 必须按 Agent 1 到 Agent {ALL_AGENT_MAX} 排列")
    return normalized


def recent_files(paths, count=3):
    return sorted(paths, key=lambda p: p.stat().st_mtime, reverse=True)[:count]


def parse_iteration_key(path: Path):
    match = re.match(r"^迭代记录_(\d{8})_(\d{3})\.md$", path.name)
    if not match:
        return None, None
    date_raw, seq = match.groups()
    date_pretty = f"{date_raw[0:4]}-{date_raw[4:6]}-{date_raw[6:8]}"
    return date_pretty, seq


def classify_topic(text: str) -> str:
    compact = " ".join(text.split())
    if not compact:
        return "未识别"
    for label, pattern in TOPIC_PATTERNS:
        if re.search(pattern, compact, re.IGNORECASE):
            return label
    return "通用实现"


def first_non_empty(items):
    for item in items:
        if item and item.strip():
            return item.strip()
    return ""


def dedupe_preserve_order(items):
    seen = set()
    ordered = []
    for item in items:
        if not item:
            continue
        normalized = item.strip()
        if not normalized or normalized in seen:
            continue
        seen.add(normalized)
        ordered.append(normalized)
    return ordered


def bullet_text(item: str) -> str:
    stripped = item.strip()
    if stripped.startswith("- "):
        return stripped[2:].strip()
    return stripped


def is_process_only_item(text: str) -> bool:
    compact = bullet_text(text)
    if not compact:
        return False
    return classify_topic(compact) == "流程与协作" or bool(PROCESS_ONLY_PATTERN.search(compact))


def select_mainline_bullets(handoff_bullets, next_steps, overall_judgement: str):
    handoff_candidates = [item for item in handoff_bullets if not is_process_only_item(item)]
    next_candidates = [item for item in next_steps if not is_process_only_item(item)]

    if handoff_candidates:
        return handoff_candidates[:2]
    if overall_judgement and not is_process_only_item(overall_judgement):
        return [overall_judgement]
    if next_candidates:
        return next_candidates[:2]
    if overall_judgement:
        return [overall_judgement]
    return next_steps[:2]


def simplify_commit_title(text: str) -> str:
    value = " ".join(text.split()).strip("。；，, ")
    if not value:
        return "更新当前任务"
    value = re.sub(r"^(完成|继续|开始|正在|统一|补充|补齐|更新|修复|实现|新增|收口)", "", value).strip()
    value = value.strip("：: ")
    return value or "更新当前任务"


def next_commit_sequence(doc_root: Path) -> int:
    repo_root = doc_root.resolve().parent
    try:
        inside_repo = subprocess.run(
            ["git", "-C", str(repo_root), "rev-parse", "--is-inside-work-tree"],
            capture_output=True,
            text=True,
            check=False,
        )
    except OSError:
        return COMMIT_SEQUENCE_START

    if inside_repo.returncode != 0:
        return COMMIT_SEQUENCE_START

    log_result = subprocess.run(
        ["git", "-C", str(repo_root), "log", "--format=%s"],
        capture_output=True,
        text=True,
        check=False,
    )
    if log_result.returncode != 0:
        return COMMIT_SEQUENCE_START

    max_sequence = 0
    for line in log_result.stdout.splitlines():
        match = re.match(r"^\[(\d+)\]\s+", line.strip())
        if match:
            max_sequence = max(max_sequence, int(match.group(1)))
    return max(max_sequence + 1, COMMIT_SEQUENCE_START)


def format_commit_sequence(sequence: int) -> str:
    return f"[{sequence:04d}]"


def build_commit_title(doc_root: Path, summary: str, done, topic: str) -> str:
    prefix = COMMIT_TITLE_PREFIX.get(topic, "提交：")
    base = first_non_empty([summary, done[0] if done else "", "更新当前任务"])
    simplified = simplify_commit_title(base)
    if not simplified.startswith(prefix):
        simplified = f"{prefix}{simplified}"
    sequence = format_commit_sequence(next_commit_sequence(doc_root))
    if simplified.startswith(f"{sequence} "):
        return simplified
    return f"{sequence} {simplified}"


def build_commit_content(summary: str, done):
    items = dedupe_preserve_order(done)
    if summary:
        items = dedupe_preserve_order(items + [summary])
    if not items:
        items = ["整理并收口当前任务改动"]
    return items[:4]


def git_dir_for_doc_root(doc_root: Path):
    repo_root = doc_root.resolve().parent
    try:
        result = subprocess.run(
            ["git", "-C", str(repo_root), "rev-parse", "--git-dir"],
            capture_output=True,
            text=True,
            check=False,
        )
    except OSError:
        return None

    if result.returncode != 0:
        return None

    git_dir = result.stdout.strip()
    if not git_dir:
        return None

    path = Path(git_dir)
    if not path.is_absolute():
        path = (repo_root / path).resolve()
    return path


def pending_commit_cache_path(doc_root: Path):
    git_dir = git_dir_for_doc_root(doc_root)
    if git_dir is None:
        return None
    return git_dir / PENDING_COMMIT_CACHE_RELATIVE


def current_head_commit(doc_root: Path) -> str:
    repo_root = doc_root.resolve().parent
    result = subprocess.run(
        ["git", "-C", str(repo_root), "rev-parse", "HEAD"],
        capture_output=True,
        text=True,
        check=False,
    )
    if result.returncode != 0:
        return ""
    return result.stdout.strip()


def write_pending_commit_suggestion(doc_root: Path, commit):
    cache_path = pending_commit_cache_path(doc_root)
    if cache_path is None:
        return
    cache_path.parent.mkdir(parents=True, exist_ok=True)
    payload = {
        "title": commit["title"],
        "content": commit["content"],
        "head_commit": current_head_commit(doc_root),
        "created_at": datetime.now().isoformat(timespec="seconds"),
    }
    cache_path.write_text(json.dumps(payload, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def clear_pending_commit_suggestion(doc_root: Path):
    cache_path = pending_commit_cache_path(doc_root)
    if cache_path is None or not cache_path.exists():
        return
    cache_path.unlink()


def read_pending_commit_suggestion(doc_root: Path):
    cache_path = pending_commit_cache_path(doc_root)
    if cache_path is None or not cache_path.exists():
        return None
    return json.loads(cache_path.read_text(encoding="utf-8"))


def suggest_commit_message(doc_root: Path, summary: str, done, risks, agent6_review: str, doc_log_skipped: bool):
    if doc_log_skipped:
        return {
            "suitable": False,
            "reason": "这轮属于事务性协助，没有形成需要写入项目状态的独立里程碑。",
            "title": "",
            "content": [],
        }

    if agent6_review == "failed":
        return {
            "suitable": False,
            "reason": "Agent 6 审查未通过，当前仍在修复链中，暂不适合提交。",
            "title": "",
            "content": [],
        }

    if agent6_review == "pending":
        return {
            "suitable": False,
            "reason": "Agent 6 审查尚未完成，当前还不能确认这轮改动已经收口。",
            "title": "",
            "content": [],
        }

    if agent6_review == "deferred":
        return {
            "suitable": False,
            "reason": "Agent 6 审查当前处于待回补复审，正式审查闭环尚未完成。",
            "title": "",
            "content": [],
        }

    content = build_commit_content(summary, done)
    topics = {
        classify_topic(item)
        for item in [summary, *done]
        if item and item.strip()
    }
    topics.discard("未识别")
    topics.discard("通用实现")
    if len(topics) > 1:
        return {
            "suitable": False,
            "reason": "当前总结里仍混有多条主线，建议先拆分后再分别生成提交标题和内容。",
            "title": "",
            "content": [],
        }

    if risks and not done:
        return {
            "suitable": False,
            "reason": "当前仍以风险说明为主，缺少清晰完成项，暂不建议直接提交。",
            "title": "",
            "content": [],
        }

    topic = next(iter(topics), classify_topic(summary))
    return {
        "suitable": True,
        "reason": "",
        "title": build_commit_title(doc_root, summary, done, topic),
        "content": content,
    }


def extract_first_text_code_block(path: Path) -> str:
    try:
        text = path.read_text(encoding="utf-8")
    except FileNotFoundError:
        return ""
    match = re.search(r"```text\s*(.*?)```", text, re.DOTALL)
    if not match:
        return ""
    return match.group(1).strip()


def suggest_agent_start(bullets):
    patterns = [
        (r"Agent\s*5|测试|质量", "Agent 5：测试与质量保障"),
        (r"Agent\s*4|UI|联调", "Agent 4：UI 与验证"),
        (r"Agent\s*3|任务|长期进度", "Agent 3：任务与长期进度"),
        (r"Agent\s*2|地图|棋盘", "Agent 2：地图与棋盘"),
        (r"Agent\s*1|骨架|Shared|入口冻结", "Agent 1：边界与骨架"),
    ]
    for bullet in bullets:
        if is_process_only_item(bullet):
            continue
        for pattern, label in patterns:
            if re.search(pattern, bullet, re.IGNORECASE):
                return label
    return "暂无明确建议"


def latest_iteration_context(doc_root: Path):
    iteration_docs = scan_iteration_docs(doc_root)
    latest = iteration_docs[-1] if iteration_docs else None
    if not latest:
        return {
            "stage": "暂无",
            "mainline": "暂无",
            "start_agent": "暂无明确建议",
        }
    current_status = extract_section_bullets(latest, "当前状态")
    handoff = extract_section_bullets(latest, "给下一轮的人")
    next_steps = extract_section_bullets(latest, "下一步")
    stage = current_status[0][2:] if current_status else "暂无"
    if stage.startswith("当前阶段："):
        stage = stage.replace("当前阶段：", "", 1).strip()
    overall_judgement = extract_prefixed_value(latest, "- 整体判断：")
    mainline_bullets = select_mainline_bullets(handoff, next_steps, overall_judgement)
    mainline = "；".join(bullet_text(item) for item in mainline_bullets) if mainline_bullets else "暂无"
    start_agent = extract_prefixed_value(latest, "- 建议起始 Agent：")
    if not start_agent or start_agent == "待补充":
        start_agent = suggest_agent_start(handoff + next_steps)
    return {
        "stage": stage,
        "mainline": mainline,
        "start_agent": start_agent,
    }


def classify_long_doc(path: Path):
    if path.name == "项目总览.md":
        return "项目总览"
    parts = set(path.parts)
    if UI_SYSTEM_DIR_NAME in parts:
        return "UI自动生成系统"
    if "方案与数据" in parts:
        return "方案与数据"
    if "架构与边界" in parts:
        return "架构与边界"
    if "协作与执行" in parts:
        return "协作与执行"
    return "协作与执行"


def scan_long_docs(doc_root: Path):
    files = []
    for path in sorted(doc_root.rglob("*.md")):
        rel = path.relative_to(doc_root).as_posix()
        if rel.startswith(f"{ITER_DIR_NAME}/"):
            continue
        if rel == f"{LONG_DIR_NAME}/{LONG_INDEX_NAME}":
            continue
        if rel in LEGACY_LONG_DOCS:
            continue
        files.append(path)
    return files


def scan_iteration_docs(doc_root: Path):
    iter_dir = doc_root / ITER_DIR_NAME
    files = []
    for path in sorted(iter_dir.glob("*.md")):
        if path.name == ITER_INDEX_NAME:
            continue
        files.append(path)
    return files


def replace_section_bullets(text: str, heading: str, bullets) -> str:
    heading_line = f"## {heading}"
    lines = text.splitlines()
    for i, line in enumerate(lines):
        if line.strip() == heading_line:
            body_start = i + 1
            while body_start < len(lines) and lines[body_start].strip() == "":
                body_start += 1
            body_end = body_start
            while body_end < len(lines) and not lines[body_end].startswith("## "):
                body_end += 1

            new_lines = lines[: i + 1] + [""]
            new_lines.extend(bullets)
            if body_end < len(lines):
                new_lines += [""] + lines[body_end:]
            return "\n".join(new_lines).rstrip() + "\n"

    block = "\n".join(bullets)
    return text.rstrip() + f"\n\n{heading_line}\n\n{block}\n"


def clean_placeholder_bullets(text: str, heading: str, placeholders) -> str:
    bullets = extract_section_bullets_from_text(text, heading)
    if not bullets:
        return text
    real_items = [line for line in bullets if line not in placeholders]
    if not real_items:
        return text
    cleaned = [line for line in bullets if line not in placeholders]
    return replace_section_bullets(text, heading, cleaned)


def ensure_default_agent_statuses(text: str, default_statuses) -> str:
    bullets = extract_section_bullets_from_text(text, "分工状态")
    if not bullets:
        return replace_section_bullets(text, "分工状态", default_statuses)

    existing = {}
    replaced = False
    for line in bullets:
        number = agent_number_from_line(line)
        if number is None:
            continue
        if line.strip().endswith("待补充"):
            existing[number] = default_statuses[number - 1]
            replaced = True
        else:
            existing[number] = normalize_agent_status_line(line)

    if not existing:
        return replace_section_bullets(text, "分工状态", default_statuses)

    merged = []
    for index, default_line in enumerate(default_statuses, start=1):
        merged.append(existing.get(index, default_line))
        if index not in existing:
            replaced = True

    if replaced or merged != bullets:
        return replace_section_bullets(text, "分工状态", merged)
    return text


def apply_agent_status_overrides(text: str, overrides):
    if not overrides:
        return text
    bullets = extract_section_bullets_from_text(text, "分工状态")
    if not bullets:
        raise ValueError("分工状态章节不存在，无法覆写 agent 状态")

    merged = {}
    for line in bullets:
        number = agent_number_from_line(line)
        if number is not None:
            merged[number] = normalize_agent_status_line(line)

    for line in overrides:
        normalized = normalize_agent_status_line(line)
        number = agent_number_from_line(normalized)
        merged[number] = normalized

    ordered = [merged[index] for index in range(1, ALL_AGENT_MAX + 1)]
    return replace_section_bullets(text, "分工状态", ordered)


def write_project_overview(doc_root: Path):
    overview_path = doc_root / LONG_DIR_NAME / "项目总览.md"
    if not overview_path.exists():
        return

    context = latest_iteration_context(doc_root)
    bullets = [
        f"- 当前阶段：{context['stage']}",
        f"- 当前主线：{context['mainline']}",
        f"- 当前建议起点：{context['start_agent']}",
    ]

    text = overview_path.read_text(encoding="utf-8")
    text = replace_section_bullets(text, "当前阶段", bullets)
    overview_path.write_text(text, encoding="utf-8")


def write_long_index(doc_root: Path):
    long_dir, _ = ensure_dirs(doc_root)
    long_docs = scan_long_docs(doc_root)
    context = latest_iteration_context(doc_root)
    overview_path = next((p for p in long_docs if p.name == "项目总览.md"), None)
    latest_docs = recent_files(long_docs, 3)
    grouped = {
        "项目总览": [],
        "UI自动生成系统": [],
        "方案与数据": [],
        "架构与边界": [],
        "协作与执行": [],
    }
    for path in long_docs:
        grouped[classify_long_doc(path)].append(path)
    ui_system_overview = next(
        (
            path
            for path in grouped["UI自动生成系统"]
            if path.name == UI_SYSTEM_OVERVIEW_NAME
        ),
        grouped["UI自动生成系统"][0] if grouped["UI自动生成系统"] else None,
    )
    lines = [
        "# 主文档索引",
        "",
        "本索引用“研发入口页”的方式组织长期主文档，帮助快速判断当前项目阶段、先看什么，以及下一步从哪里开始。",
        "",
        "## 当前概览",
        "",
        f"- 长期文档总数：{len(long_docs)}",
        f"- 当前阶段：{context['stage']}",
        f"- 当前主线：{context['mainline']}",
        f"- 推荐先读：{markdown_title(overview_path) if overview_path else '暂无'}",
        f"- 当前实现起点：{context['start_agent']}",
        "",
        "## 稳定决策速览",
        "",
        "- `App.AOT` 只放宿主、资源、持久化、HybridCLR 和平台基础设施",
        "- `App.Shared` 只放稳定 DTO / 接口 / 事件",
        "- `App.HotUpdate` 承载正式玩法",
        "- `MinesweeperTerrainData` 只作为地图模板，不保存运行时状态",
        "- 正式运行时资源统一走 YooAssets",
        "- UI 只做表现和交互，不承载奖励公式和地图生成逻辑",
        "",
        "## 推荐阅读路径",
        "",
        "### 如果你是第一次接手项目",
        "",
        f"- [ ] 先读 [{markdown_title(overview_path) if overview_path else '项目总览'}]({absolute_doc_link(overview_path, doc_root) if overview_path else f'{doc_root.as_posix()}/{LONG_DIR_NAME}/项目总览.md'})",
        "- [ ] 再读方案与数据目录中的主方案文档",
        "- [ ] 再读协作与执行目录中的配对与任务模板文档",
        "",
        "### 如果你要开始写代码",
        "",
        "- [ ] 先读架构与边界目录中的规范文档",
        "- [ ] 再读协作与执行目录中的任务模板文档",
        "- [ ] 最后打开最新一轮迭代记录索引",
        "",
        "## 文档分区",
        "",
        "### A. 项目总览",
        "",
    ]
    if not grouped["项目总览"]:
        lines.append("- [ ] 暂无")
    else:
        for path in grouped["项目总览"]:
            title = markdown_title(path)
            rel = relative_markdown_link(path, doc_root)
            lines.append(f"- [ ] [{title}]({absolute_doc_link(path, doc_root)})")
    lines += [
        "",
        "### B. 方案与数据",
        "",
    ]
    if not grouped["方案与数据"]:
        if ui_system_overview is None:
            lines.append("- [ ] 暂无")
    else:
        if ui_system_overview is not None:
            lines.append(f"- [ ] [UI 自动生成系统专区]({absolute_doc_link(ui_system_overview, doc_root)})")
        for path in grouped["方案与数据"]:
            title = markdown_title(path)
            plan_progress = extract_plan_progress(path)
            label = f"[{plan_progress['status']}] {title}"
            lines.append(f"- [ ] [{label}]({absolute_doc_link(path, doc_root)})")
    lines += [
        "",
        "### C. 架构与边界",
        "",
    ]
    if not grouped["架构与边界"]:
        lines.append("- [ ] 暂无")
    else:
        for path in grouped["架构与边界"]:
            title = markdown_title(path)
            rel = relative_markdown_link(path, doc_root)
            lines.append(f"- [ ] [{title}]({absolute_doc_link(path, doc_root)})")
    lines += [
        "",
        "### D. 协作与执行",
        "",
    ]
    if not grouped["协作与执行"]:
        lines.append("- [ ] 暂无")
    else:
        for path in grouped["协作与执行"]:
            title = markdown_title(path)
            rel = relative_markdown_link(path, doc_root)
            lines.append(f"- [ ] [{title}]({absolute_doc_link(path, doc_root)})")
    lines += [
        "",
        "## 最近更新",
        "",
    ]
    if not latest_docs:
        lines.append("- 暂无最近更新文档")
    else:
        for path in latest_docs:
            title = markdown_title(path)
            rel = relative_markdown_link(path, doc_root)
            lines.append(f"- [{title}]({absolute_doc_link(path, doc_root)})")
    lines += [
        "",
        "## 维护建议",
        "",
        "- 这份索引不只是目录，而是长期研发入口页",
        "- 项目阶段变化时，优先更新“当前概览”",
        "- 稳定边界变化时，优先更新“稳定决策速览”",
        "- 新增长期文档时，再补“文档分区”",
    ]
    (long_dir / LONG_INDEX_NAME).write_text("\n".join(lines) + "\n", encoding="utf-8")


def write_iteration_index(doc_root: Path):
    _, iter_dir = ensure_dirs(doc_root)
    iteration_docs = scan_iteration_docs(doc_root)
    latest = iteration_docs[-1] if iteration_docs else None
    context = latest_iteration_context(doc_root)
    latest_done = extract_section_bullets(latest, "完成项")[:3] if latest else []
    latest_risks = extract_section_bullets(latest, "风险与阻塞")[:3] if latest else []
    latest_next = extract_section_bullets(latest, "下一步")[:3] if latest else []
    latest_updates = recent_files(iteration_docs, 3)
    suggested_agent = context["start_agent"]
    lines = [
        "# 迭代记录索引",
        "",
        "本索引用项目清单方式汇总每一轮工作的记录，方便快速找到最近一轮状态、风险和下一步。",
        "",
        "## 当前概览",
        "",
        f"- 迭代记录总数：{len(iteration_docs)}",
        f"- 最新记录：{markdown_title(latest) if latest else '暂无'}",
        "",
        "## 建议动作",
        "",
        "- [ ] 优先阅读最新一轮记录",
        "- [ ] 确认最新风险与阻塞",
        "- [ ] 确认下一步是否已经拆成可执行任务",
        "",
        "## 最近完成",
        "",
    ]
    if not latest_done:
        lines.append("- 暂无记录的完成项")
    else:
        lines.extend(latest_done)
    lines += [
        "",
        "## 最新阻塞",
        "",
    ]
    if not latest_risks:
        lines.append("- 暂无记录的阻塞")
    else:
        lines.extend(latest_risks)
    lines += [
        "",
        "## 下一步入口",
        "",
    ]
    if not latest_next:
        lines.append("- 暂无记录的下一步")
    else:
        lines.extend(latest_next)
    lines += [
        "",
        "## 建议从哪个 Agent 开始继续",
        "",
        f"- {suggested_agent}",
        "",
        "## 最近更新记录",
        "",
    ]
    if not latest_updates:
        lines.append("- 暂无最近更新记录")
    else:
        for path in latest_updates:
            title = markdown_title(path)
            rel = relative_markdown_link(path, doc_root)
            lines.append(f"- [{title}]({absolute_doc_link(path, doc_root)})")
    lines += [
        "",
        "## 记录清单",
        "",
    ]
    if not iteration_docs:
        lines.append("- [ ] 暂无迭代记录")
    else:
        grouped = {}
        others = []
        for path in reversed(iteration_docs):
            date_pretty, seq = parse_iteration_key(path)
            if date_pretty:
                grouped.setdefault(date_pretty, []).append((seq, path))
            else:
                others.append(path)

        for date_pretty in sorted(grouped.keys(), reverse=True):
            lines.append(f"### {date_pretty}")
            lines.append("")
            for seq, path in grouped[date_pretty]:
                title = markdown_title(path)
                rel = relative_markdown_link(path, doc_root)
                lines.append(f"- [ ] {seq} / [{title}]({absolute_doc_link(path, doc_root)})")
            lines.append("")

        if others:
            lines.append("### 其他记录")
            lines.append("")
            for path in others:
                title = markdown_title(path)
                rel = relative_markdown_link(path, doc_root)
                lines.append(f"- [ ] [{title}]({absolute_doc_link(path, doc_root)})")
    (iter_dir / ITER_INDEX_NAME).write_text("\n".join(lines) + "\n", encoding="utf-8")


def sync_indexes(doc_root: Path):
    write_project_overview(doc_root)
    write_long_index(doc_root)
    write_iteration_index(doc_root)


def today_sequence(iter_dir: Path) -> int:
    date_str = datetime.now().strftime("%Y%m%d")
    pattern = re.compile(rf"^迭代记录_{date_str}_(\d{{3}})\.md$")
    seq = 0
    for path in iter_dir.glob(f"迭代记录_{date_str}_*.md"):
        match = pattern.match(path.name)
        if match:
            seq = max(seq, int(match.group(1)))
    return seq + 1


def create_iteration(doc_root: Path, title: str, goal: str, status: str):
    _, iter_dir = ensure_dirs(doc_root)
    default_agent_statuses = load_default_agent_statuses(doc_root)
    now = datetime.now()
    date_str = now.strftime("%Y%m%d")
    seq = today_sequence(iter_dir)
    path = iter_dir / f"迭代记录_{date_str}_{seq:03d}.md"
    previous_files = scan_iteration_docs(doc_root)
    previous = previous_files[-1] if previous_files else None
    previous_label = markdown_title(previous) if previous else "无"
    previous_link = (
        f"[{previous_label}]({absolute_doc_link(previous, doc_root)})"
        if previous
        else "无"
    )
    lines = [
        f"# 迭代记录 {now.strftime('%Y-%m-%d')} {seq:03d}",
        "",
        "## 迭代标识",
        "",
        f"- 迭代编号：{date_str}-{seq:03d}",
        f"- 记录日期：{now.strftime('%Y-%m-%d')}",
        f"- 当前状态：{status}",
        f"- 承接上一轮：{previous_link}",
        f"- 建议起始 Agent：待补充",
        "",
        "---",
        "",
        "## 本轮主题",
        "",
        title,
        "",
        "## 本轮目标",
        "",
        f"- [ ] {goal}",
        "",
        "## 当前状态",
        "",
        f"- 当前阶段：{status}",
        "- 整体判断：待补充",
        "",
        "## 关键结论",
        "",
        "- 待补充",
        "",
        "## 分工状态",
        "",
        *default_agent_statuses,
        "",
        "## 工作日志",
        "",
        f"### {now.strftime('%Y-%m-%d %H:%M')}",
        "",
        "- 创建本轮迭代记录",
        "",
        "## 完成项",
        "",
        "- 暂无",
        "",
        "## 风险与阻塞",
        "",
        "- 暂无",
        "",
        "## 下一步",
        "",
        "- 待补充",
        "",
        "---",
        "",
        "## 给下一轮的人",
        "",
        "- 待补充",
        "",
    ]
    path.write_text("\n".join(lines), encoding="utf-8")
    sync_indexes(doc_root)
    return path


def latest_iteration_file(doc_root: Path):
    files = scan_iteration_docs(doc_root)
    if not files:
        raise FileNotFoundError("未找到迭代记录，请先使用 new-iteration 创建")
    return files[-1]


def insert_under_heading(text: str, heading: str, block: str) -> str:
    heading_line = f"## {heading}"
    lines = text.splitlines()
    for i, line in enumerate(lines):
        if line.strip() == heading_line:
            insert_at = i + 1
            while insert_at < len(lines) and lines[insert_at].strip() == "":
                insert_at += 1
            block_lines = block.rstrip("\n").splitlines()
            new_lines = lines[: i + 1] + [""] + block_lines
            if insert_at < len(lines):
                new_lines += [""] + lines[insert_at:]
            return "\n".join(new_lines).rstrip() + "\n"
    return text.rstrip() + f"\n\n{heading_line}\n\n{block.rstrip()}\n"


def append_iteration(doc_root: Path, target: str, latest: bool, summary: str, done, risks, next_steps, agent_statuses):
    if latest:
        path = latest_iteration_file(doc_root)
    else:
        path = Path(target)
        if not path.is_absolute():
            path = doc_root / target
    text = path.read_text(encoding="utf-8")

    default_agent_statuses = load_default_agent_statuses(doc_root)
    text = ensure_default_agent_statuses(text, default_agent_statuses)
    text = apply_agent_status_overrides(text, agent_statuses)

    timestamp = datetime.now().strftime("%Y-%m-%d %H:%M")
    if summary:
        block = f"### {timestamp}\n\n- {summary}"
        text = insert_under_heading(text, "工作日志", block)

    for item in done:
        text = insert_under_heading(text, "完成项", f"- {item}")
    for item in risks:
        text = insert_under_heading(text, "风险与阻塞", f"- {item}")
    for item in next_steps:
        text = insert_under_heading(text, "下一步", f"- {item}")

    for heading, placeholders in DEFAULT_PLACEHOLDERS.items():
        text = clean_placeholder_bullets(text, heading, placeholders)

    path.write_text(text, encoding="utf-8")
    sync_indexes(doc_root)
    return path


def latest_iteration_metadata(doc_root: Path):
    files = scan_iteration_docs(doc_root)
    latest = files[-1] if files else None
    if not latest:
        return {
            "path": None,
            "date": "",
            "title": "",
            "theme": "",
            "topic": "未识别",
        }
    date_pretty, _ = parse_iteration_key(latest)
    theme = extract_section_text(latest, "本轮主题")
    title = markdown_title(latest)
    return {
        "path": latest,
        "date": date_pretty or "",
        "title": title,
        "theme": theme,
        "topic": classify_topic(f"{title}\n{theme}"),
    }


def build_session_bootstrap_prompt(doc_root: Path, goal: str, done, risks, next_steps, docs):
    startup_doc = doc_root / LONG_DIR_NAME / SESSION_BOOTSTRAP_DOC
    intro = extract_first_text_code_block(startup_doc)
    if not intro:
        intro = (
            "按长期主文档规则执行。每次完成一个任务后都执行文档维护流程。"
            "每次任务结束时都必须汇报文档维护、Git 提交建议、会话建议和迭代记录建议。"
        )

    lines = [intro]
    if goal:
        lines.append(f"当前目标：{goal}")
    if done:
        lines.append("已完成：")
        for item in done[:3]:
            lines.append(f"- {item}")
    if risks:
        lines.append("当前风险：")
        for item in risks[:3]:
            lines.append(f"- {item}")
    if next_steps:
        lines.append("当前下一步：")
        for item in next_steps[:3]:
            lines.append(f"- {item}")
    if docs:
        lines.append("建议先读：")
        for item in docs:
            lines.append(f"- {item}")
    return "\n".join(lines)


def suggest_handoff(
    doc_root: Path,
    summary: str,
    done,
    risks,
    next_steps,
    agent6_review: str,
    session_mode: str,
    iteration_mode: str,
    next_session_title: str,
    next_session_goal: str,
    next_session_docs,
    doc_log_skipped: bool,
    context_compressed: bool,
    session_major_task_count: int,
):
    commit = suggest_commit_message(doc_root, summary, done, risks, agent6_review, doc_log_skipped)
    if commit["suitable"]:
        write_pending_commit_suggestion(doc_root, commit)
    else:
        clear_pending_commit_suggestion(doc_root)
    latest = latest_iteration_metadata(doc_root)
    today = datetime.now().strftime("%Y-%m-%d")
    next_focus = first_non_empty([next_session_goal, next_steps[0] if next_steps else "", summary])
    next_topic = classify_topic(next_focus)
    review_gate_open = agent6_review in {"passed", "passed-with-suggestions", "not-required"}
    review_failed = agent6_review == "failed"
    review_pending = agent6_review == "pending"
    review_deferred = agent6_review == "deferred"
    session_quality_reasons = []
    if context_compressed:
        session_quality_reasons.append("本会话已出现过自动压缩背景信息，后续继续堆叠上下文的收益会明显下降。")
    if session_major_task_count >= 2:
        session_quality_reasons.append(
            f"当前会话内已连续完成 {session_major_task_count} 个大任务，当前更适合开一个新会话重新装载上下文。"
        )
    session_quality_degraded = bool(session_quality_reasons)

    if review_failed:
        session_advice = "建议新开修复/复审会话"
        session_reason = "Agent 6 未通过，本轮只能先修复并复审，不能直接切到下一阶段。"
        title = next_session_title or "修复 Agent 6 审查问题"
        goal = next_session_goal or first_non_empty(
            [
                next_steps[0] if next_steps else "",
                risks[0] if risks else "",
                "按 Agent 6 审查意见修复当前交付，并准备复审。",
            ]
        )
    elif review_deferred:
        if session_quality_degraded:
            session_advice = "建议新开修复/复审会话"
            session_reason = "Agent 6 审查已转为待回补复审，当前不能宣告下一阶段通过。 " + " ".join(session_quality_reasons)
            title = next_session_title or "继续当前修复/验证链"
            goal = next_session_goal or first_non_empty(
                [
                    next_steps[0] if next_steps else "",
                    risks[0] if risks else "",
                    "继续当前修复与验证，并在审查结果回传后回补复审。",
                ]
            )
        else:
            session_advice = "继续当前修复/验证会话"
            session_reason = "Agent 6 审查已转为待回补复审；当前可继续已知修复与验证，但暂不生成下一阶段启动卡。"
            title = ""
            goal = ""
    elif review_pending and not session_quality_degraded:
        session_advice = "继续当前会话"
        session_reason = "Agent 6 审查尚未完成，暂不建议生成下一阶段启动卡。"
        title = ""
        goal = ""
    elif review_pending and session_quality_degraded:
        session_advice = "建议新开修复/复审会话"
        session_reason = "Agent 6 审查已发起但结果尚未回传，当前不能宣告下一阶段通过。 " + " ".join(session_quality_reasons)
        title = next_session_title or "继续当前修复/复审链"
        goal = next_session_goal or first_non_empty(
            [
                next_steps[0] if next_steps else "",
                summary,
                "等待 Agent 6 审查结果回传，并继续当前修复链。",
            ]
        )
    else:
        should_new_session = False
        if session_mode == "new":
            should_new_session = True
        elif session_mode == "continue":
            should_new_session = False
        elif session_quality_degraded:
            should_new_session = True
        else:
            should_new_session = bool(next_focus) and (
                latest["topic"] != next_topic
                or len(done) >= 2
                or bool(re.search(r"完成|收口|通过|里程碑", summary))
            )

        if should_new_session and (review_gate_open or session_quality_degraded):
            session_advice = "建议新开下一阶段会话"
            if session_quality_degraded:
                session_reason = " ".join(session_quality_reasons)
            else:
                session_reason = "当前任务已基本收口，下一步可以按独立主题重新装载上下文。"
            title = next_session_title or first_non_empty(
                [
                    next_steps[0] if next_steps else "",
                    summary,
                    "继续下一阶段任务",
                ]
            )
            goal = next_session_goal or first_non_empty(
                [
                    next_steps[0] if next_steps else "",
                    summary,
                    "继续推进下一阶段任务",
                ]
            )
        else:
            session_advice = "继续当前会话"
            session_reason = "下一步仍然依赖当前上下文，暂时不需要切到新的会话。"
            title = ""
            goal = ""

    if iteration_mode == "new":
        iteration_advice = "建议新建迭代记录"
        iteration_reason = "主控显式要求新建迭代记录。"
    elif iteration_mode == "continue":
        iteration_advice = "继续当前迭代记录"
        iteration_reason = "主控显式要求沿用当前迭代记录。"
    else:
        if not latest["path"]:
            iteration_advice = "建议新建迭代记录"
            iteration_reason = "当前还没有迭代记录，下一轮需要先建立记录。"
        elif review_failed or review_pending or review_deferred:
            iteration_advice = "继续当前迭代记录"
            iteration_reason = "当前仍在同一轮修复/复审链路内，不应提前切出新的迭代文件。"
        elif latest["date"] and latest["date"] != today:
            iteration_advice = "建议新建迭代记录"
            iteration_reason = "已跨天，按当前规则默认新建新的迭代记录。"
        elif latest["topic"] != next_topic and next_focus:
            iteration_advice = "建议新建迭代记录"
            iteration_reason = "下一步主题已明显切换，应该把这轮和下一轮分开记录。"
        else:
            iteration_advice = "继续当前迭代记录"
            iteration_reason = "下一步仍属于同一主线，继续沿用当前迭代记录更清晰。"

    doc_paths = []
    for item in next_session_docs:
        candidate = Path(item)
        if candidate.is_absolute():
            doc_paths.append(candidate.as_posix())
        else:
            doc_paths.append((doc_root / item).resolve().as_posix())

    default_docs = [
        (doc_root / LONG_DIR_NAME / SESSION_BOOTSTRAP_DOC).resolve().as_posix(),
        (doc_root / LONG_DIR_NAME / TASK_WRAPUP_DOC).resolve().as_posix(),
    ]
    if latest["path"]:
        default_docs.append(latest["path"].resolve().as_posix())
    docs = dedupe_preserve_order(doc_paths + default_docs)

    startup_prompt = ""
    if title and goal:
        startup_prompt = build_session_bootstrap_prompt(doc_root, goal, done, risks, next_steps, docs)

    return {
        "doc_maintenance_status": "未执行。原因是：这轮属于事务性协助，没有写入项目总览或迭代记录。" if doc_log_skipped else "已执行",
        "commit_suggestion": commit,
        "session_advice": session_advice,
        "session_reason": session_reason,
        "iteration_advice": iteration_advice,
        "iteration_reason": iteration_reason,
        "title": title,
        "goal": goal,
        "docs": docs,
        "startup_prompt": startup_prompt,
        "doc_log_skipped": doc_log_skipped,
    }


def format_handoff_report(report):
    lines = [
        f"文档维护：{report['doc_maintenance_status']}",
    ]
    commit = report["commit_suggestion"]
    if commit["suitable"]:
        lines += [
            "Git 提交建议：适合提交",
            f"标题：{commit['title']}",
            "内容：",
            "```text",
        ]
        for item in commit["content"]:
            lines.append(f"- {item}")
        lines.append("```")
        lines.append("提交确认：如需我直接提交到 git，请回复 1 / 确认 / 提交 / 直接提交。")
    else:
        lines.append(f"Git 提交建议：暂不建议提交。原因是：{commit['reason']}")
        lines.append("提交确认：当前不建议直接提交；如需强制提交，请明确说明。")

    lines.append(f"会话建议：{report['session_advice']}")
    lines += [
        f"原因是：{report['session_reason']}",
        f"迭代记录建议：{report['iteration_advice']}",
        f"原因是：{report['iteration_reason']}",
    ]
    if report["doc_log_skipped"]:
        lines += [
            "文档落点：本轮被判定为事务性协助，本次只输出建议，不写入迭代记录。",
        ]
    else:
        lines += [
            "文档落点：本次建议默认只在收尾输出中展示，不自动写入迭代记录。",
        ]
    if report["title"] and report["goal"]:
        lines += [
            f"下一会话标题：{report['title']}",
            f"下一会话目标：{report['goal']}",
            "建议先读：",
        ]
        for item in report["docs"]:
            lines.append(f"- {item}")
        lines += [
            "建议首条消息：",
            "```text",
            report["startup_prompt"],
            "```",
        ]
    else:
        lines.append("下一会话启动卡：当前不生成，继续当前会话即可。")
    return "\n".join(lines)


def backfill_agent_status(doc_root: Path, from_date: str):
    if not re.fullmatch(r"\d{8}", from_date):
        raise ValueError("--from 必须是 YYYYMMDD 格式，例如 20260330")

    default_agent_statuses = load_default_agent_statuses(doc_root)
    updated = []
    for path in scan_iteration_docs(doc_root):
        match = re.match(r"^迭代记录_(\d{8})_(\d{3})\.md$", path.name)
        if not match or match.group(1) < from_date:
            continue

        original = path.read_text(encoding="utf-8")
        text = ensure_default_agent_statuses(original, default_agent_statuses)
        for heading, placeholders in DEFAULT_PLACEHOLDERS.items():
            text = clean_placeholder_bullets(text, heading, placeholders)

        if text != original:
            path.write_text(text, encoding="utf-8")
            updated.append(path)

    sync_indexes(doc_root)
    return updated


def main():
    parser = argparse.ArgumentParser(description="Maintain long-term docs and iteration logs for this project.")
    parser.add_argument("--doc-root", default="doc", help="Project doc root directory")
    sub = parser.add_subparsers(dest="command", required=True)

    sub.add_parser("sync", help="Regenerate long-term and iteration indexes")

    new_iteration = sub.add_parser("new-iteration", help="Create a new iteration log")
    new_iteration.add_argument("--title", required=True, help="Iteration title")
    new_iteration.add_argument("--goal", required=True, help="Iteration goal")
    new_iteration.add_argument("--status", default="进行中", help="Initial status")

    append = sub.add_parser("append-iteration", help="Append work notes to an iteration log")
    append_target = append.add_mutually_exclusive_group(required=True)
    append_target.add_argument("--file", help="Target iteration file, relative to doc root or absolute path")
    append_target.add_argument("--latest", action="store_true", help="Append to the latest iteration log")
    append.add_argument("--summary", default="", help="Short work summary")
    append.add_argument("--done", action="append", default=[], help="Completed item, repeatable")
    append.add_argument("--risk", action="append", default=[], help="Risk or blocker, repeatable")
    append.add_argument("--next", dest="next_steps", action="append", default=[], help="Next-step item, repeatable")
    append.add_argument("--agent-status", action="append", default=[], help="Full Agent status line, repeatable")

    handoff = sub.add_parser("suggest-handoff", help="Suggest whether to start a new session and/or iteration")
    handoff.add_argument("--summary", default="", help="Short work summary")
    handoff.add_argument("--done", action="append", default=[], help="Completed item, repeatable")
    handoff.add_argument("--risk", action="append", default=[], help="Risk or blocker, repeatable")
    handoff.add_argument("--next", dest="next_steps", action="append", default=[], help="Next-step item, repeatable")
    handoff.add_argument(
        "--agent6-review",
        default="pending",
        choices=["passed", "passed-with-suggestions", "failed", "pending", "deferred", "not-required"],
        help="Current Agent 6 review status",
    )
    handoff.add_argument(
        "--session-mode",
        default="auto",
        choices=["auto", "new", "continue"],
        help="Override the session suggestion",
    )
    handoff.add_argument(
        "--iteration-mode",
        default="auto",
        choices=["auto", "new", "continue"],
        help="Override the iteration suggestion",
    )
    handoff.add_argument("--next-session-title", default="", help="Explicit next-session title")
    handoff.add_argument("--next-session-goal", default="", help="Explicit next-session goal")
    handoff.add_argument("--next-session-doc", action="append", default=[], help="Recommended doc path, repeatable")
    handoff.add_argument("--doc-log-skipped", action="store_true", help="Mark that this round did not write project docs")
    handoff.add_argument(
        "--context-compressed",
        action="store_true",
        help="Mark that this session has already shown automatic context compression or obvious context degradation",
    )
    handoff.add_argument(
        "--session-major-task-count",
        type=int,
        default=0,
        help="Explicit count of major tasks already completed in the current session",
    )

    sub.add_parser("show-pending-commit", help="Show the latest cached suitable commit suggestion")

    backfill = sub.add_parser("backfill-agent-status", help="Backfill default agent status into iteration logs")
    backfill.add_argument("--from", dest="from_date", required=True, help="Start date in YYYYMMDD")

    args = parser.parse_args()
    doc_root = Path(args.doc_root).resolve()
    ensure_dirs(doc_root)

    if args.command == "sync":
        sync_indexes(doc_root)
        print(f"[ok] synced indexes under {doc_root}")
        return

    if args.command == "new-iteration":
        path = create_iteration(doc_root, args.title, args.goal, args.status)
        print(f"[ok] created iteration log: {path}")
        return

    if args.command == "append-iteration":
        path = append_iteration(
            doc_root,
            args.file or "",
            args.latest,
            args.summary,
            args.done,
            args.risk,
            args.next_steps,
            args.agent_status,
        )
        print(f"[ok] updated iteration log: {path}")
        return

    if args.command == "suggest-handoff":
        report = suggest_handoff(
            doc_root,
            args.summary,
            args.done,
            args.risk,
            args.next_steps,
            args.agent6_review,
            args.session_mode,
            args.iteration_mode,
            args.next_session_title,
            args.next_session_goal,
            args.next_session_doc,
            args.doc_log_skipped,
            args.context_compressed,
            args.session_major_task_count,
        )
        print(format_handoff_report(report))
        return

    if args.command == "show-pending-commit":
        payload = read_pending_commit_suggestion(doc_root)
        if not payload:
            raise SystemExit("未找到最近一次可直接复用的提交建议缓存。")
        print(json.dumps(payload, ensure_ascii=False, indent=2))
        return

    if args.command == "backfill-agent-status":
        updated = backfill_agent_status(doc_root, args.from_date)
        print(f"[ok] backfilled {len(updated)} iteration logs under {doc_root}")
        return


if __name__ == "__main__":
    main()
