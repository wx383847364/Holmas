#!/usr/bin/env python3

from __future__ import annotations

import argparse
import codecs
import glob
import os
import platform
import re
import shutil
import subprocess
import sys
import tempfile
from dataclasses import dataclass
from datetime import datetime
from pathlib import Path
from typing import Iterable


CSV_FILES = (
    "Holmas_MapTable.csv",
    "Holmas_CatTable.csv",
    "Holmas_TaskTable.csv",
    "Holmas_PlayerLevelTable.csv",
)

EXECUTE_METHOD = "Holmas.EditorTools.HolmasCsvBinaryExporter.ExportFromBatchMode"
SUCCESS_MARKER = "Exiting batchmode successfully now!"


@dataclass(frozen=True)
class CsvCheckResult:
    path: Path
    source_encoding: str
    had_bom: bool
    changed: bool


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Holmas CSV -> JSON/bytes 导出工具。")
    parser.add_argument("--editor", help="显式指定 Unity/Tuanjie 编辑器可执行文件路径。")
    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="只检查 CSV 编码和编辑器路径，不执行 Unity batchmode 导出。",
    )
    parser.add_argument(
        "--skip-bom-fix",
        action="store_true",
        help="发现 UTF-8 BOM 或 Excel 本地编码时不自动修复，直接报错退出。",
    )
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    repo_root = Path(__file__).resolve().parent.parent
    csv_root = repo_root / "Assets" / "Config"

    print(f"[info] project root: {repo_root}")
    print(f"[info] csv root: {csv_root}")

    csv_results = check_and_fix_csv_files(csv_root, skip_bom_fix=args.skip_bom_fix)
    print_csv_summary(csv_results)

    editor_path = resolve_editor_path(repo_root, args.editor)
    print(f"[info] editor: {editor_path}")

    if args.dry_run:
        print("[info] dry-run 完成，未执行导出。")
        return 0

    log_file = Path(tempfile.gettempdir()) / f"holmas_export_{datetime.now().strftime('%Y%m%d_%H%M%S')}.log"
    temp_root, temp_project_root = prepare_temp_project(repo_root)
    print(f"[info] temp project: {temp_project_root}")

    try:
        run_export(temp_project_root, editor_path, log_file)
        sync_export_outputs(temp_project_root, repo_root)
    except Exception:
        print(f"[warn] 导出失败，临时工程已保留：{temp_project_root}")
        raise
    else:
        shutil.rmtree(temp_root, ignore_errors=True)
        print(f"[info] 已删除临时工程：{temp_project_root}")

    print("[info] 导出完成。")
    print(f"[info] log: {log_file}")
    print(f"[info] json output: {repo_root / 'Assets/Config/json'}")
    print(f"[info] binary output: {repo_root / 'Assets/HotUpdateContent/Config'}")
    return 0


def check_and_fix_csv_files(csv_root: Path, skip_bom_fix: bool) -> list[CsvCheckResult]:
    results: list[CsvCheckResult] = []
    missing = [csv_root / name for name in CSV_FILES if not (csv_root / name).is_file()]
    if missing:
        joined = "\n".join(str(path) for path in missing)
        raise SystemExit(f"[error] 找不到以下 CSV 文件：\n{joined}")

    for csv_name in CSV_FILES:
        path = csv_root / csv_name
        raw = path.read_bytes()
        newline = detect_newline(raw)

        if raw.startswith(codecs.BOM_UTF8):
            text = raw.decode("utf-8-sig")
            if skip_bom_fix:
                raise SystemExit(f"[error] {path} 是 UTF-8 with BOM，请先转成 UTF-8 无 BOM 或移除 --skip-bom-fix。")

            with path.open("w", encoding="utf-8", newline="") as handle:
                handle.write(normalize_newlines(text, newline))
            results.append(CsvCheckResult(path=path, source_encoding="utf-8-sig", had_bom=True, changed=True))
            continue

        try:
            raw.decode("utf-8")
        except UnicodeDecodeError as exc:
            excel_text = try_decode_excel_ansi(raw)
            if excel_text is None:
                raise SystemExit(f"[error] {path} 不是合法 UTF-8 文件，也无法按 Excel 常见本地编码读取：{exc}") from exc

            if skip_bom_fix:
                raise SystemExit(f"[error] {path} 是 Excel 本地编码（{excel_text[1]}），请先转成 UTF-8 无 BOM 或移除 --skip-bom-fix。")

            with path.open("w", encoding="utf-8", newline="") as handle:
                handle.write(normalize_newlines(excel_text[0], newline))
            results.append(CsvCheckResult(path=path, source_encoding=excel_text[1], had_bom=False, changed=True))
            continue

        results.append(CsvCheckResult(path=path, source_encoding="utf-8", had_bom=False, changed=False))

    return results


def detect_newline(raw: bytes) -> str:
    if b"\r\n" in raw:
        return "\r\n"
    if b"\r" in raw:
        return "\r"
    return "\n"


def normalize_newlines(text: str, newline: str) -> str:
    return text.replace("\r\n", "\n").replace("\r", "\n").replace("\n", newline)


def print_csv_summary(results: Iterable[CsvCheckResult]) -> None:
    for result in results:
        relative = result.path.relative_to(result.path.parents[2])
        if result.changed:
            if result.had_bom:
                print(f"[info] 已将 UTF-8 BOM 转为无 BOM：{relative}")
            else:
                print(f"[info] 已将 {result.source_encoding} 转为 UTF-8 无 BOM：{relative}")
        elif result.had_bom:
            print(f"[info] 检测到 BOM：{relative}")
        else:
            print(f"[info] UTF-8 无 BOM：{relative}")


def try_decode_excel_ansi(raw: bytes) -> tuple[str, str] | None:
    for encoding in ("gb18030", "gbk"):
        try:
            return raw.decode(encoding), encoding
        except UnicodeDecodeError:
            continue
    return None


def resolve_editor_path(repo_root: Path, override: str | None) -> Path:
    if override:
        return validate_editor_path(Path(override))

    for key in ("TUANJIE_EDITOR_PATH", "TUANJIE_EDITOR", "UNITY_EDITOR_PATH", "UNITY_EDITOR"):
        value = os.environ.get(key)
        if value:
            return validate_editor_path(Path(value))

    editor_version = read_editor_version(repo_root / "ProjectSettings" / "ProjectVersion.txt")
    candidates = list(find_exact_editor_candidates(editor_version))
    if not candidates:
        candidates = list(find_scanned_editor_candidates())

    if not candidates:
        raise SystemExit("[error] 未找到可用的 Unity/Tuanjie 编辑器，请用 --editor 指定路径。")

    candidates = [path for path in candidates if path.is_file()]
    if not candidates:
        raise SystemExit("[error] 已找到编辑器候选路径，但没有可执行文件，请用 --editor 指定路径。")

    candidates.sort(key=sort_key_for_editor_path, reverse=True)
    return candidates[0]


def read_editor_version(version_file: Path) -> str | None:
    if not version_file.is_file():
        return None

    pattern = re.compile(r"^m_EditorVersion:\s*(.+?)\s*$")
    for line in version_file.read_text(encoding="utf-8").splitlines():
        match = pattern.match(line)
        if match:
            return match.group(1)
    return None


def find_exact_editor_candidates(editor_version: str | None) -> Iterable[Path]:
    if not editor_version:
        return []

    system = platform.system()
    candidates: list[Path] = []

    if system == "Darwin":
        candidates.extend(
            [
                Path(f"/Applications/Tuanjie/Hub/Editor/{editor_version}/Tuanjie.app/Contents/MacOS/Tuanjie"),
                Path(f"/Applications/Unity/Hub/Editor/{editor_version}/Unity.app/Contents/MacOS/Unity"),
            ]
        )
    elif system == "Windows":
        candidates.extend(
            [
                Path(fr"C:\Program Files\Tuanjie\Hub\Editor\{editor_version}\Editor\Tuanjie.exe"),
                Path(fr"C:\Program Files\Tuanjie\Hub\Editor\{editor_version}\Tuanjie.exe"),
                Path(fr"C:\Program Files\Unity\Hub\Editor\{editor_version}\Editor\Unity.exe"),
            ]
        )

    return [path for path in candidates if path.is_file()]


def find_scanned_editor_candidates() -> Iterable[Path]:
    system = platform.system()
    if system == "Darwin":
        patterns = [
            "/Applications/Tuanjie/Hub/Editor/*/Tuanjie.app/Contents/MacOS/Tuanjie",
            "/Applications/Unity/Hub/Editor/*/Unity.app/Contents/MacOS/Unity",
        ]
    elif system == "Windows":
        patterns = [
            r"C:\Program Files\Tuanjie\Hub\Editor\*\Editor\Tuanjie.exe",
            r"C:\Program Files\Tuanjie\Hub\Editor\*\Tuanjie.exe",
            r"C:\Program Files\Unity\Hub\Editor\*\Editor\Unity.exe",
        ]
    else:
        patterns = []

    candidates: list[Path] = []
    for pattern in patterns:
        candidates.extend(Path(item) for item in glob.glob(pattern))
    return [path for path in candidates if path.is_file()]


def sort_key_for_editor_path(path: Path) -> tuple[list[int], str]:
    match = re.search(r"Editor[/\\\\]([^/\\\\]+)", str(path))
    version = match.group(1) if match else path.name
    numbers = [int(piece) for piece in re.findall(r"\d+", version)]
    return (numbers, version)


def validate_editor_path(path: Path) -> Path:
    if not path.is_file():
        raise SystemExit(f"[error] 指定的编辑器路径不存在：{path}")
    return path


def prepare_temp_project(repo_root: Path) -> tuple[Path, Path]:
    temp_parent = Path("/private/tmp") if platform.system() == "Darwin" and Path("/private/tmp").is_dir() else Path(tempfile.gettempdir())
    temp_root = Path(tempfile.mkdtemp(prefix="holmas_export_", dir=str(temp_parent)))
    temp_project_root = temp_root / repo_root.name
    copy_project(repo_root, temp_project_root)
    return temp_root, temp_project_root


def copy_project(repo_root: Path, temp_project_root: Path) -> None:
    ignore_names = shutil.ignore_patterns(".git", "Library", "Temp", "Logs", "obj", ".DS_Store")
    rsync = shutil.which("rsync")
    if rsync and platform.system() != "Windows":
        command = [
            rsync,
            "-a",
            "--delete",
            "--exclude",
            ".git",
            "--exclude",
            "Library",
            "--exclude",
            "Temp",
            "--exclude",
            "Logs",
            "--exclude",
            "obj",
            "--exclude",
            ".DS_Store",
            f"{repo_root}/",
            f"{temp_project_root}/",
        ]
        subprocess.run(command, check=True)
        return

    shutil.copytree(repo_root, temp_project_root, ignore=ignore_names)


def sync_export_outputs(temp_project_root: Path, repo_root: Path) -> None:
    sync_output_dir(
        temp_project_root / "Assets" / "Config" / "json",
        repo_root / "Assets" / "Config" / "json",
    )
    sync_output_dir(
        temp_project_root / "Assets" / "HotUpdateContent" / "Config",
        repo_root / "Assets" / "HotUpdateContent" / "Config",
    )


def sync_output_dir(source: Path, target: Path) -> None:
    if not source.is_dir():
        raise SystemExit(f"[error] 导出结果缺少目录：{source}")

    if target.exists():
        shutil.rmtree(target)

    shutil.copytree(source, target)


def run_export(project_root: Path, editor_path: Path, log_file: Path) -> None:
    command = [
        str(editor_path),
        "-batchmode",
        "-quit",
        "-projectPath",
        str(project_root),
        "-executeMethod",
        EXECUTE_METHOD,
        "-logFile",
        str(log_file),
    ]

    print(f"[info] executing: {' '.join(command)}")
    env = build_clean_env(editor_path)

    result = subprocess.run(command, cwd=project_root, env=env, check=False)
    if result.returncode != 0:
        raise SystemExit(f"[error] Unity batchmode 导出失败，退出码：{result.returncode}。日志：{log_file}")

    if not log_file.is_file():
        raise SystemExit(f"[error] 导出未生成日志文件：{log_file}")

    log_text = log_file.read_text(encoding="utf-8", errors="ignore")
    if SUCCESS_MARKER not in log_text:
        raise SystemExit(f"[error] 日志未出现成功标记，导出结果不可信：{log_file}")


def build_clean_env(editor_path: Path) -> dict[str, str]:
    system = platform.system()
    current = os.environ

    if system == "Windows":
        env = current.copy()
        env.setdefault("PYTHONUTF8", "1")
        return env

    path_entries = [
        "/usr/bin",
        "/bin",
        "/usr/sbin",
        "/sbin",
        "/opt/homebrew/bin",
        str(editor_path.parent),
    ]

    env: dict[str, str] = {
        "PATH": ":".join(dict.fromkeys(path_entries)),
        "LANG": "en_US.UTF-8",
        "LC_ALL": "en_US.UTF-8",
        "TMPDIR": current.get("TMPDIR", tempfile.gettempdir()),
    }

    for key in ("HOME", "USER", "LOGNAME", "SHELL", "TMP", "TEMP"):
        value = current.get(key)
        if value:
            env[key] = value

    return env


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except KeyboardInterrupt:
        raise SystemExit("[error] 导出已被中断。")
