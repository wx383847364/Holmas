#!/usr/bin/env python3

from __future__ import annotations

import argparse
from pathlib import Path

from holmas_exporter import export_all, validate_all


XLSX_FILES = (
    "Holmas_MapTable.xlsx",
    "Holmas_CatTable.xlsx",
    "Holmas_TaskTable.xlsx",
    "Holmas_PlayerLevelTable.xlsx",
    "Holmas_MetaLevelTable.xlsx",
    "Holmas_AgencyBuildingTable.xlsx",
)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Holmas Xlsx -> JSON/bytes 导出工具。")
    parser.add_argument(
        "--editor",
        help="兼容旧参数，纯 Python 导表已不再依赖 Unity/Tuanjie 编辑器。",
    )
    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="只检查 Xlsx 源文件与导表前置条件，不写入 json/bytes。",
    )
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    repo_root = Path(__file__).resolve().parents[2]
    config_root = repo_root / "Assets" / "Config"
    json_root = config_root / "json"
    binary_root = repo_root / "Assets" / "HotUpdateContent" / "Config"

    print(f"[info] project root: {repo_root}")
    print(f"[info] config root: {config_root}")
    _check_xlsx_files(config_root)
    _print_xlsx_summary(config_root, repo_root)

    if args.editor:
        print("[warn] --editor 已被忽略；当前正式导表链已改为纯 Python。")

    if args.dry_run:
        result = validate_all(repo_root, config_root, json_root, binary_root)
        _print_report_summary(result.report, dry_run=True)
        return 0 if result.report.success else 1

    result = export_all(repo_root, config_root, json_root, binary_root)
    _print_report_summary(result.report, dry_run=False)
    if not result.report.success:
        return 1

    print("[info] 导出完成。")
    print(f"[info] json output: {json_root}")
    print(f"[info] binary output: {binary_root}")
    return 0


def _check_xlsx_files(config_root: Path) -> None:
    missing = [config_root / name for name in XLSX_FILES if not (config_root / name).is_file()]
    if missing:
        joined = "\n".join(str(path) for path in missing)
        raise SystemExit(f"[error] 找不到以下 Xlsx 文件：\n{joined}")


def _print_xlsx_summary(config_root: Path, repo_root: Path) -> None:
    for file_name in XLSX_FILES:
        path = config_root / file_name
        print(f"[info] xlsx source: {path.relative_to(repo_root).as_posix()}")


def _print_report_summary(report, dry_run: bool) -> None:
    mode = "dry-run" if dry_run else "export"
    if report.warnings:
        for warning in report.warnings:
            print(f"[warn] {warning}")

    if report.errors:
        for error in report.errors:
            print(f"[error] {error}")

    if report.success:
        if dry_run:
            print("[info] dry-run 完成，导表前置校验通过。")
        else:
            print(f"[info] {mode} 完成，已写入 {report.binary_written_count} 个二进制文件。")
    else:
        if dry_run:
            print("[error] dry-run 失败，存在导表前置错误。")
        else:
            print("[error] 导出失败，已阻止正式 bytes 覆盖。")


if __name__ == "__main__":
    raise SystemExit(main())
