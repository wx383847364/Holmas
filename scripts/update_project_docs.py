#!/usr/bin/env python3
import argparse
from datetime import datetime
from pathlib import Path
import re


LONG_DIR_NAME = "长期主文档"
ITER_DIR_NAME = "迭代记录"
LONG_INDEX_NAME = "主文档索引.md"
ITER_INDEX_NAME = "迭代记录索引.md"


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
        if capture and line.startswith("## "):
            break
        if capture and line.strip().startswith("- "):
            bullets.append(line.strip())
    return bullets


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


def recent_files(paths, count=3):
    return sorted(paths, key=lambda p: p.stat().st_mtime, reverse=True)[:count]


def parse_iteration_key(path: Path):
    match = re.match(r"^迭代记录_(\d{8})_(\d{3})\.md$", path.name)
    if not match:
        return None, None
    date_raw, seq = match.groups()
    date_pretty = f"{date_raw[0:4]}-{date_raw[4:6]}-{date_raw[6:8]}"
    return date_pretty, seq


def suggest_agent_start(bullets):
    patterns = [
        (r"Agent\s*5|测试|质量", "Agent 5：测试与质量保障"),
        (r"Agent\s*4|UI|联调", "Agent 4：UI 与验证"),
        (r"Agent\s*3|任务|长期进度", "Agent 3：任务与长期进度"),
        (r"Agent\s*2|地图|棋盘", "Agent 2：地图与棋盘"),
        (r"Agent\s*1|骨架|Shared|入口冻结", "Agent 1：边界与骨架"),
    ]
    for bullet in bullets:
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
    next_steps = extract_section_bullets(latest, "下一步")
    stage = current_status[0][2:] if current_status else "暂无"
    if stage.startswith("当前阶段："):
        stage = stage.replace("当前阶段：", "", 1).strip()
    mainline = "；".join(item[2:] for item in next_steps[:2]) if next_steps else "暂无"
    start_agent = extract_prefixed_value(latest, "- 建议起始 Agent：") or suggest_agent_start(next_steps)
    return {
        "stage": stage,
        "mainline": mainline,
        "start_agent": start_agent,
    }


def classify_long_doc(path: Path):
    if path.name == "项目总览.md":
        return "项目总览"
    parts = set(path.parts)
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
        "方案与数据": [],
        "架构与边界": [],
        "协作与执行": [],
    }
    for path in long_docs:
        grouped[classify_long_doc(path)].append(path)
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
        lines.append("- [ ] 暂无")
    else:
        for path in grouped["方案与数据"]:
            title = markdown_title(path)
            rel = relative_markdown_link(path, doc_root)
            lines.append(f"- [ ] [{title}]({absolute_doc_link(path, doc_root)})")
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
    latest_done = extract_section_bullets(latest, "完成项")[:3] if latest else []
    latest_risks = extract_section_bullets(latest, "风险与阻塞")[:3] if latest else []
    latest_next = extract_section_bullets(latest, "下一步")[:3] if latest else []
    latest_updates = recent_files(iteration_docs, 3)
    suggested_agent = suggest_agent_start(latest_next)
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
        "- Agent 1：待补充",
        "- Agent 2：待补充",
        "- Agent 3：待补充",
        "- Agent 4：待补充",
        "- Agent 5：待补充",
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


def append_iteration(doc_root: Path, target: str, latest: bool, summary: str, done, risks, next_steps):
    if latest:
        path = latest_iteration_file(doc_root)
    else:
        path = Path(target)
        if not path.is_absolute():
            path = doc_root / target
    text = path.read_text(encoding="utf-8")

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

    path.write_text(text, encoding="utf-8")
    sync_indexes(doc_root)
    return path


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
        path = append_iteration(doc_root, args.file or "", args.latest, args.summary, args.done, args.risk, args.next_steps)
        print(f"[ok] updated iteration log: {path}")
        return


if __name__ == "__main__":
    main()
