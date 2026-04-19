from __future__ import annotations

from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from struct import pack, unpack
from typing import Iterable

from .binary_codec import write_cat_meta_package, write_core_package
from .models import (
    CURRENT_VERSION,
    AgencyBuildingCostRow,
    AgencyBuildingCostSheetRow,
    AgencyBuildingRow,
    AgencyBuildingSheetRow,
    BundleReport,
    CatMetaPackage,
    CatMetaRow,
    CatSheetRow,
    CoreConfigPackage,
    ExportReport,
    MapRow,
    MapSheetRow,
    MetaLevelRow,
    PlayerLevelRow,
    PlayerLevelSheetRow,
    TaskRow,
    TaskSheetRow,
)
from .report_writer import write_cat_json, write_core_json, write_report_json
from .xlsx_reader import XlsxReadError, read_first_worksheet


MAP_TABLE_NAME = "Holmas_MapTable.xlsx"
CAT_TABLE_NAME = "Holmas_CatTable.xlsx"
TASK_TABLE_NAME = "Holmas_TaskTable.xlsx"
PLAYER_LEVEL_TABLE_NAME = "Holmas_PlayerLevelTable.xlsx"
AGENCY_BUILDING_TABLE_NAME = "Holmas_AgencyBuildingTable.xlsx"

CORE_BINARY_NAME = "holmas_core_config.bytes"
CAT_BINARY_NAME = "holmas_cat_meta.bytes"
CORE_PREVIEW_NAME = "holmas_core_config.json"
CAT_PREVIEW_NAME = "holmas_cat_meta.json"
REPORT_NAME = "holmas_export_report.json"


@dataclass
class ExportResult:
    report: ExportReport
    core_package: CoreConfigPackage
    cat_package: CatMetaPackage


def export_all(repo_root: Path | str, config_root: Path | str, json_root: Path | str, binary_root: Path | str) -> ExportResult:
    return _export(repo_root, config_root, json_root, binary_root, write_outputs=True)


def validate_all(repo_root: Path | str, config_root: Path | str, json_root: Path | str, binary_root: Path | str) -> ExportResult:
    return _export(repo_root, config_root, json_root, binary_root, write_outputs=False)


def _export(repo_root: Path | str, config_root: Path | str, json_root: Path | str, binary_root: Path | str, write_outputs: bool) -> ExportResult:
    repo_root = Path(repo_root).resolve()
    config_root = Path(config_root).resolve()
    json_root = Path(json_root).resolve()
    binary_root = Path(binary_root).resolve()

    report = ExportReport(
        exported_at_utc=_utc_now_string(),
        source_files=[
            _display_path(repo_root, config_root / MAP_TABLE_NAME),
            _display_path(repo_root, config_root / CAT_TABLE_NAME),
            _display_path(repo_root, config_root / TASK_TABLE_NAME),
            _display_path(repo_root, config_root / PLAYER_LEVEL_TABLE_NAME),
            _display_path(repo_root, config_root / AGENCY_BUILDING_TABLE_NAME),
        ],
    )

    map_rows = _load_map_table(report, repo_root, config_root)
    cat_rows = _load_cat_table(report, repo_root, config_root)
    task_rows = _load_task_table(report, repo_root, config_root)
    player_level_rows = _load_player_level_table(report, repo_root, config_root)
    agency_building_rows = _load_agency_building_table(report, repo_root, config_root)

    cat_lookup = _build_alias_lookup((row.cat_id, index) for index, row in enumerate(cat_rows))
    task_lookup = _build_alias_lookup((row.task_type_id, index) for index, row in enumerate(task_rows))
    map_lookup = _build_alias_lookup((row.map_id, index) for index, row in enumerate(map_rows))

    _normalize_map_rows(report, map_rows, map_lookup)
    _normalize_cat_rows(report, cat_rows, cat_lookup)
    _normalize_task_rows(report, task_rows, cat_lookup, task_lookup)
    _normalize_player_level_rows(report, player_level_rows, task_lookup, map_lookup)
    _validate_player_level_table(report, player_level_rows)
    _validate_agency_building_table(report, agency_building_rows)

    core_package = _build_core_package(map_rows, task_rows, player_level_rows, agency_building_rows)
    cat_package = _build_cat_package(cat_rows)

    report.bundle_reports = [
        BundleReport(
            bundle_name="core",
            source_table_names=[
                MAP_TABLE_NAME,
                TASK_TABLE_NAME,
                PLAYER_LEVEL_TABLE_NAME,
                AGENCY_BUILDING_TABLE_NAME,
            ],
            preview_json_path=_display_path(repo_root, json_root / CORE_PREVIEW_NAME),
            binary_path=_display_path(repo_root, binary_root / CORE_BINARY_NAME),
            row_count=len(map_rows) + len(task_rows) + len(player_level_rows) + len(agency_building_rows),
            warning_count=report.warning_count,
            error_count=report.error_count,
        ),
        BundleReport(
            bundle_name="cat_meta",
            source_table_names=[CAT_TABLE_NAME],
            preview_json_path=_display_path(repo_root, json_root / CAT_PREVIEW_NAME),
            binary_path=_display_path(repo_root, binary_root / CAT_BINARY_NAME),
            row_count=len(cat_rows),
            warning_count=report.warning_count,
            error_count=report.error_count,
        ),
    ]

    report.success = report.error_count == 0
    report.binary_written_count = 0

    if write_outputs:
        json_root.mkdir(parents=True, exist_ok=True)
        binary_root.mkdir(parents=True, exist_ok=True)
        write_core_json(json_root / CORE_PREVIEW_NAME, core_package)
        write_cat_json(json_root / CAT_PREVIEW_NAME, cat_package)
        if report.success:
            (binary_root / CORE_BINARY_NAME).write_bytes(write_core_package(core_package))
            (binary_root / CAT_BINARY_NAME).write_bytes(write_cat_meta_package(cat_package))
            report.binary_written_count = 2
        write_report_json(json_root / REPORT_NAME, report)

    return ExportResult(report=report, core_package=core_package, cat_package=cat_package)


def _load_map_table(report: ExportReport, repo_root: Path, config_root: Path) -> list[MapSheetRow]:
    return _load_sheet(
        report,
        repo_root,
        config_root / MAP_TABLE_NAME,
        _parse_maps,
    )


def _load_cat_table(report: ExportReport, repo_root: Path, config_root: Path) -> list[CatSheetRow]:
    return _load_sheet(
        report,
        repo_root,
        config_root / CAT_TABLE_NAME,
        _parse_cats,
    )


def _load_task_table(report: ExportReport, repo_root: Path, config_root: Path) -> list[TaskSheetRow]:
    return _load_sheet(
        report,
        repo_root,
        config_root / TASK_TABLE_NAME,
        _parse_tasks,
    )


def _load_player_level_table(report: ExportReport, repo_root: Path, config_root: Path) -> list[PlayerLevelSheetRow]:
    return _load_sheet(
        report,
        repo_root,
        config_root / PLAYER_LEVEL_TABLE_NAME,
        _parse_player_levels,
    )


def _load_agency_building_table(report: ExportReport, repo_root: Path, config_root: Path) -> list[AgencyBuildingSheetRow]:
    return _load_sheet(
        report,
        repo_root,
        config_root / AGENCY_BUILDING_TABLE_NAME,
        _parse_agency_buildings,
    )


def _load_sheet(report: ExportReport, repo_root: Path, path: Path, parser):
    source_path = _display_path(repo_root, path)
    if not path.is_file():
        report.errors.append(f"找不到 xlsx 文件: {source_path}")
        return []

    try:
        rows = read_first_worksheet(path)
    except XlsxReadError as exc:
        report.errors.append(str(exc))
        return []

    if len(rows) < 2:
        report.errors.append(f"xlsx 结构不完整: {source_path}")
        return []

    header_map = _build_header_map(report, source_path, rows[1])
    return parser(report, source_path, rows, header_map)


def _parse_maps(report: ExportReport, source_path: str, rows: list[list[str]], header_map: dict[str, int]) -> list[MapSheetRow]:
    map_id_col = _require_column(report, source_path, header_map, "mapId")
    terrain_path_col = _require_column(report, source_path, header_map, "terrainPath")
    cat_count_max_col = _require_column(report, source_path, header_map, "catCountMax")
    cat_count_min_col = _require_column(report, source_path, header_map, "catCountMin")

    items: list[MapSheetRow] = []
    for row_index in range(2, len(rows)):
        row = rows[row_index]
        if _is_blank_row(row):
            continue

        cat_count_max, max_ok = _try_parse_int(_get_cell(row, cat_count_max_col))
        cat_count_min, min_ok = _try_parse_int(_get_cell(row, cat_count_min_col))

        item = MapSheetRow(
            row_index=len(items),
            map_id=_get_cell(row, map_id_col),
            terrain_path=_get_cell(row, terrain_path_col),
            cat_count_max=cat_count_max,
            cat_count_min=cat_count_min,
        )

        if not item.map_id:
            report.errors.append(f"{source_path} 第 {row_index + 1} 行缺少 mapId。")
            continue
        if not item.terrain_path:
            report.errors.append(f"{source_path} 第 {row_index + 1} 行缺少 terrainPath。")
            continue
        if not max_ok:
            report.errors.append(f"{source_path} 第 {row_index + 1} 行 catCountMax 无法解析。")
            continue
        if not min_ok:
            report.errors.append(f"{source_path} 第 {row_index + 1} 行 catCountMin 无法解析。")
            continue
        if item.cat_count_min > item.cat_count_max:
            report.errors.append(f"{source_path} 第 {row_index + 1} 行猫数量范围非法: min > max。")
            continue

        items.append(item)
    return items


def _parse_cats(report: ExportReport, source_path: str, rows: list[list[str]], header_map: dict[str, int]) -> list[CatSheetRow]:
    cat_id_col = _require_column(report, source_path, header_map, "catId")
    cat_name_col = _require_column(report, source_path, header_map, "catName")
    icon_path_col = _get_optional_column(header_map, "iconPath")
    rarity_col = _get_optional_column(header_map, "rarity")
    weight_col = _get_optional_column(header_map, "weight")
    price_col = _get_optional_column(header_map, "price")

    has_any_icon_missing = False
    has_any_rarity_missing = False
    has_any_weight_missing = False
    has_any_price_missing = False

    items: list[CatSheetRow] = []
    for row_index in range(2, len(rows)):
        row = rows[row_index]
        if _is_blank_row(row):
            continue

        rarity, rarity_ok = _parse_byte(_get_cell(row, rarity_col), default_value=0)
        weight, weight_ok = _parse_int(_get_cell(row, weight_col), default_value=1)
        price, price_ok = _parse_int(_get_cell(row, price_col), default_value=0)

        item = CatSheetRow(
            row_index=len(items),
            cat_id=_get_cell(row, cat_id_col),
            cat_name=_get_cell(row, cat_name_col),
            icon_path=_get_cell(row, icon_path_col),
            rarity=rarity,
            weight=weight,
            price=price,
        )

        if not item.cat_id:
            report.errors.append(f"{source_path} 第 {row_index + 1} 行缺少 catId。")
            continue
        if not item.cat_name:
            report.errors.append(f"{source_path} 第 {row_index + 1} 行缺少 catName。")
            continue
        if not item.icon_path:
            has_any_icon_missing = True
        if not rarity_ok:
            has_any_rarity_missing = True
        if not weight_ok:
            has_any_weight_missing = True
        if not price_ok:
            has_any_price_missing = True

        items.append(item)

    if has_any_icon_missing:
        report.warnings.append(f"{source_path}: 有猫表行缺少 iconPath，已保留为空字符串。")
    if has_any_rarity_missing:
        report.warnings.append(f"{source_path}: 有猫表行缺少 rarity，已默认填 0。")
    if has_any_weight_missing:
        report.warnings.append(f"{source_path}: 有猫表行缺少 weight，已默认填 1。")
    if has_any_price_missing:
        report.warnings.append(f"{source_path}: 有猫表行缺少 price，已默认填 0。")

    return items


def _parse_tasks(report: ExportReport, source_path: str, rows: list[list[str]], header_map: dict[str, int]) -> list[TaskSheetRow]:
    task_type_id_col = _require_column(report, source_path, header_map, "taskTypeId")
    task_kind_col = _get_optional_column(header_map, "taskKind")
    cat_id_list_col = _require_column(report, source_path, header_map, "catIdList")
    count_max_col = _require_column(report, source_path, header_map, "countMax")
    count_min_col = _require_column(report, source_path, header_map, "countMin")
    reward_array_col = _get_optional_column(header_map, "rewardArray")
    level_reward_factor_col = _require_column(report, source_path, header_map, "levelRewardFactor")

    if task_kind_col < 0:
        report.warnings.append(f"{source_path}: 缺少 taskKind 列，已默认全部视为 Money。")

    items: list[TaskSheetRow] = []
    for row_index in range(2, len(rows)):
        row = rows[row_index]
        if _is_blank_row(row):
            continue

        task_kind_text = _get_cell(row, task_kind_col) if task_kind_col >= 0 else ""
        normalized_task_kind, valid_task_kind, missing_task_kind = _normalize_task_kind(task_kind_text)
        if not valid_task_kind:
            report.errors.append(f"{source_path} 第 {row_index + 1} 行 taskKind 不受支持: {task_kind_text}")
            continue
        if missing_task_kind:
            report.warnings.append(f"{source_path}: 第 {row_index + 1} 行 taskKind 为空，已默认 Money。")

        count_max, count_max_ok = _try_parse_int(_get_cell(row, count_max_col))
        count_min, count_min_ok = _try_parse_int(_get_cell(row, count_min_col))
        level_reward_factor, factor_ok = _parse_float32(_get_cell(row, level_reward_factor_col), default_value=1.0)

        item = TaskSheetRow(
            row_index=len(items),
            task_type_id=_get_cell(row, task_type_id_col),
            task_kind=normalized_task_kind,
            task_kind_code=_get_task_kind_code(normalized_task_kind),
            cat_id_list=_split_array(_get_cell(row, cat_id_list_col)),
            count_max=count_max,
            count_min=count_min,
            reward_array=_parse_int_array(_get_cell(row, reward_array_col), source_path, row_index + 1, report),
            level_reward_factor=level_reward_factor,
        )

        if not item.task_type_id:
            report.errors.append(f"{source_path} 第 {row_index + 1} 行缺少 taskTypeId。")
            continue
        if not item.cat_id_list:
            report.errors.append(f"{source_path} 第 {row_index + 1} 行缺少 catIdList。")
            continue
        if not count_max_ok:
            report.errors.append(f"{source_path} 第 {row_index + 1} 行 countMax 无法解析。")
            continue
        if not count_min_ok:
            report.errors.append(f"{source_path} 第 {row_index + 1} 行 countMin 无法解析。")
            continue
        if item.count_min > item.count_max:
            report.errors.append(f"{source_path} 第 {row_index + 1} 行数量范围非法: min > max。")
            continue
        if not factor_ok:
            report.warnings.append(f"{source_path} 第 {row_index + 1} 行 levelRewardFactor 无法解析，已默认 1。")

        items.append(item)

    return items


def _parse_player_levels(report: ExportReport, source_path: str, rows: list[list[str]], header_map: dict[str, int]) -> list[PlayerLevelSheetRow]:
    player_level_col = _require_column(report, source_path, header_map, "playerLevel")
    upgrade_exp_col = _get_optional_column(header_map, "upgradeExp")
    min_experience_col = _get_optional_column(header_map, "minExperience")
    if upgrade_exp_col < 0:
        upgrade_exp_col = min_experience_col
    offline_reward_per_hour_col = _require_column(report, source_path, header_map, "offlineRewardPerHour")
    ad_unlock_hours_col = _require_column(report, source_path, header_map, "adUnlockHours")
    notes_col = _get_optional_column(header_map, "notes")
    task_type_ids_col = _require_column(report, source_path, header_map, "taskTypeIds")
    task_type_weights_col = _require_column(report, source_path, header_map, "taskTypeWeights")
    map_ids_col = _require_column(report, source_path, header_map, "mapIds")
    map_weights_col = _require_column(report, source_path, header_map, "mapWeights")

    if upgrade_exp_col < 0:
        report.errors.append(f"{source_path}: 缺少 upgradeExp/minExperience 列。Holmas_PlayerLevelTable 合并成长字段后必须提供升级门槛列。")
        return []

    items: list[PlayerLevelSheetRow] = []
    for row_index in range(2, len(rows)):
        row = rows[row_index]
        if _is_blank_row(row):
            continue

        player_level, level_ok = _try_parse_int(_get_cell(row, player_level_col))
        upgrade_exp, upgrade_exp_ok = _try_parse_int(_get_cell(row, upgrade_exp_col))
        min_experience, min_experience_ok = _try_parse_int(_get_cell(row, min_experience_col))
        offline_reward_per_hour, offline_ok = _try_parse_int(_get_cell(row, offline_reward_per_hour_col))
        ad_unlock_hours, ad_ok = _try_parse_int(_get_cell(row, ad_unlock_hours_col))
        item = PlayerLevelSheetRow(
            row_index=len(items),
            player_level=player_level,
            upgrade_exp=upgrade_exp,
            offline_reward_per_hour=offline_reward_per_hour,
            ad_unlock_hours=ad_unlock_hours,
            notes=_get_cell(row, notes_col),
            task_type_ids=_split_array(_get_cell(row, task_type_ids_col)),
            task_type_weights=_parse_int_array_strict(_get_cell(row, task_type_weights_col), source_path, row_index + 1, report),
            map_ids=_split_array(_get_cell(row, map_ids_col)),
            map_weights=_parse_int_array_strict(_get_cell(row, map_weights_col), source_path, row_index + 1, report),
        )

        if not level_ok:
            report.errors.append(f"{source_path} 第 {row_index + 1} 行 playerLevel 无法解析。")
            continue
        if not upgrade_exp_ok:
            report.errors.append(f"{source_path} 第 {row_index + 1} 行 upgradeExp 无法解析。")
            continue
        if upgrade_exp_col >= 0 and min_experience_col >= 0:
            if not min_experience_ok:
                report.errors.append(f"{source_path} 第 {row_index + 1} 行 minExperience 无法解析。")
                continue
            if upgrade_exp != min_experience:
                report.errors.append(f"{source_path} 第 {row_index + 1} 行 upgradeExp 与 minExperience 不一致。")
                continue
        if not offline_ok:
            report.errors.append(f"{source_path} 第 {row_index + 1} 行 offlineRewardPerHour 无法解析。")
            continue
        if not ad_ok:
            report.errors.append(f"{source_path} 第 {row_index + 1} 行 adUnlockHours 无法解析。")
            continue
        if not item.task_type_ids:
            report.errors.append(f"{source_path} 第 {row_index + 1} 行 taskTypeIds 为空。")
            continue
        if len(item.task_type_ids) != len(item.task_type_weights):
            report.errors.append(f"{source_path} 第 {row_index + 1} 行 taskTypeIds 与 taskTypeWeights 长度不一致。")
            continue
        if not item.map_ids:
            report.errors.append(f"{source_path} 第 {row_index + 1} 行 mapIds 为空。")
            continue
        if len(item.map_ids) != len(item.map_weights):
            report.errors.append(f"{source_path} 第 {row_index + 1} 行 mapIds 与 mapWeights 长度不一致。")
            continue

        items.append(item)

    return items


def _parse_agency_buildings(report: ExportReport, source_path: str, rows: list[list[str]], header_map: dict[str, int]) -> list[AgencyBuildingSheetRow]:
    stage_id_col = _require_column(report, source_path, header_map, "agencyStageId")
    stage_name_col = _require_column(report, source_path, header_map, "stageName")
    promotion_ids_col = _require_column(report, source_path, header_map, "promotionIds")
    promotion_caps_col = _require_column(report, source_path, header_map, "promotionLevelCaps")
    promotion_costs_col = _require_column(report, source_path, header_map, "promotionUpgradeCosts")
    notes_col = _get_optional_column(header_map, "notes")

    items: list[AgencyBuildingSheetRow] = []
    for row_index in range(2, len(rows)):
        row = rows[row_index]
        if _is_blank_row(row):
            continue

        stage_id, stage_id_ok = _try_parse_int(_get_cell(row, stage_id_col))
        promotion_ids = _split_array(_get_cell(row, promotion_ids_col))
        promotion_level_caps = _parse_int_array_strict(_get_cell(row, promotion_caps_col), source_path, row_index + 1, report)
        promotion_upgrade_costs = _parse_nested_int_arrays(_get_cell(row, promotion_costs_col), source_path, row_index + 1, report)

        item = AgencyBuildingSheetRow(
            row_index=len(items),
            agency_stage_id=stage_id,
            stage_name=_get_cell(row, stage_name_col),
            promotion_ids=promotion_ids,
            promotion_level_caps=promotion_level_caps,
            promotion_upgrade_costs=promotion_upgrade_costs,
            notes=_get_cell(row, notes_col),
        )

        if not stage_id_ok or item.agency_stage_id <= 0:
            report.errors.append(f"{source_path} 第 {row_index + 1} 行的 agencyStageId 必须是正整数。")
            continue
        if not item.stage_name:
            report.errors.append(f"{source_path} 第 {row_index + 1} 行缺少 stageName。")
            continue
        expected_stage_id = len(items) + 1
        if item.agency_stage_id != expected_stage_id:
            report.errors.append(
                f"{source_path} 第 {row_index + 1} 行的 agencyStageId 必须按 1..N 连续递增，当前值为 {item.agency_stage_id}，期望值为 {expected_stage_id}。"
            )
            continue
        if not item.promotion_ids:
            report.errors.append(f"{source_path} 第 {row_index + 1} 行 promotionIds 为空。")
            continue
        if len(item.promotion_ids) != len(item.promotion_level_caps):
            report.errors.append(f"{source_path} 第 {row_index + 1} 行 promotionIds 与 promotionLevelCaps 长度不一致。")
            continue
        if len(item.promotion_ids) != len(item.promotion_upgrade_costs):
            report.errors.append(f"{source_path} 第 {row_index + 1} 行 promotionIds 与 promotionUpgradeCosts 长度不一致。")
            continue

        items.append(item)

    return items


def _normalize_map_rows(report: ExportReport, rows: list[MapSheetRow], map_lookup: dict[str, int]) -> None:
    for row in rows:
        row.map_id_index, resolved = _resolve_index(row.map_id, map_lookup)
        row.terrain_path_index = -1
        if not resolved:
            report.errors.append(f"地图引用未解析: {row.map_id}。")


def _normalize_cat_rows(report: ExportReport, rows: list[CatSheetRow], cat_lookup: dict[str, int]) -> None:
    for row in rows:
        row.cat_id_index, resolved = _resolve_index(row.cat_id, cat_lookup)
        row.cat_name_index = -1
        row.icon_path_index = -1
        if not resolved:
            report.errors.append(f"猫引用未解析: {row.cat_id}。")


def _normalize_task_rows(report: ExportReport, rows: list[TaskSheetRow], cat_lookup: dict[str, int], task_lookup: dict[str, int]) -> None:
    unresolved_cats: set[str] = set()
    for row in rows:
        row.task_type_id_index, task_resolved = _resolve_index(row.task_type_id, task_lookup)
        row.cat_indices = []
        for cat_id in row.cat_id_list:
            cat_index, cat_resolved = _resolve_index(cat_id, cat_lookup)
            row.cat_indices.append(cat_index)
            if not cat_resolved:
                unresolved_cats.add(cat_id)

        if not task_resolved:
            report.warnings.append(f"任务模板引用未解析: {row.task_type_id}。")

    if unresolved_cats:
        report.errors.append(f"任务模板中存在未解析猫引用: {'; '.join(sorted(unresolved_cats))}。")


def _normalize_player_level_rows(report: ExportReport, rows: list[PlayerLevelSheetRow], task_lookup: dict[str, int], map_lookup: dict[str, int]) -> None:
    unresolved_tasks: set[str] = set()
    unresolved_maps: set[str] = set()
    for row in rows:
        row.task_type_indices = []
        for task_id in row.task_type_ids:
            task_index, task_resolved = _resolve_index(task_id, task_lookup)
            row.task_type_indices.append(task_index)
            if not task_resolved:
                unresolved_tasks.add(task_id)

        row.map_indices = []
        for map_id in row.map_ids:
            map_index, map_resolved = _resolve_index(map_id, map_lookup)
            row.map_indices.append(map_index)
            if not map_resolved:
                unresolved_maps.add(map_id)

    if unresolved_tasks:
        report.errors.append(f"玩家等级表中存在未解析任务引用: {'; '.join(sorted(unresolved_tasks))}。")
    if unresolved_maps:
        report.errors.append(f"玩家等级表中存在未解析地图引用: {'; '.join(sorted(unresolved_maps))}。")


def _validate_player_level_table(report: ExportReport, player_rows: list[PlayerLevelSheetRow]) -> None:
    if not player_rows:
        report.errors.append("缺少 Holmas_PlayerLevelTable 数据。")
        return

    seen_player_levels: set[int] = set()
    expected_player_level = 1

    for index, player_row in enumerate(player_rows):
        if player_row.player_level != expected_player_level:
            report.errors.append(f"Holmas_PlayerLevelTable 的 playerLevel 必须从 1 连续递增，当前第 {index + 1} 行是 {player_row.player_level}。")
            return
        expected_player_level += 1
        if player_row.player_level in seen_player_levels:
            report.errors.append(f"Holmas_PlayerLevelTable 存在重复 playerLevel: {player_row.player_level}。")
            return
        seen_player_levels.add(player_row.player_level)
        if player_row.upgrade_exp < 0:
            report.errors.append(f"Holmas_PlayerLevelTable 的 upgradeExp 不能为负: level={player_row.player_level}。")
            return
        if index > 0 and player_row.upgrade_exp <= player_rows[index - 1].upgrade_exp:
            report.errors.append(f"Holmas_PlayerLevelTable 的 upgradeExp 必须严格递增: level={player_row.player_level}。")
            return
        if player_row.offline_reward_per_hour < 0:
            report.errors.append(f"Holmas_PlayerLevelTable 的 offlineRewardPerHour 不能为负: level={player_row.player_level}。")
            return
        if player_row.ad_unlock_hours <= 0:
            report.errors.append(f"Holmas_PlayerLevelTable 的 adUnlockHours 必须大于 0: level={player_row.player_level}。")
            return


def _validate_agency_building_table(report: ExportReport, rows: list[AgencyBuildingSheetRow]) -> None:
    if not rows:
        report.errors.append("缺少 Holmas_AgencyBuildingTable 数据。")
        return
    if len(rows) != 100:
        report.errors.append(f"Holmas_AgencyBuildingTable 行数必须为 100，当前为 {len(rows)}。")
        return

    seen_stage_ids: set[int] = set()
    seen_stage_names: set[str] = set()
    expected_promotion_ids = ["leaflet", "radio", "online", "tv"]

    for index, row in enumerate(rows):
        if row.agency_stage_id != index + 1:
            report.errors.append(f"Holmas_AgencyBuildingTable 的 agencyStageId 必须按 1..N 连续递增，当前第 {index + 1} 行为 {row.agency_stage_id}。")
            return
        if row.agency_stage_id in seen_stage_ids:
            report.errors.append(f"Holmas_AgencyBuildingTable 存在重复 agencyStageId: {row.agency_stage_id}。")
            return
        seen_stage_ids.add(row.agency_stage_id)
        if not row.stage_name:
            report.errors.append(f"Holmas_AgencyBuildingTable {row.agency_stage_id} 缺少 stageName。")
            return
        if row.stage_name in seen_stage_names:
            report.errors.append(f"Holmas_AgencyBuildingTable 存在重复 stageName: {row.stage_name}。")
            return
        seen_stage_names.add(row.stage_name)
        if len(row.promotion_ids) != len(expected_promotion_ids):
            report.errors.append(f"Holmas_AgencyBuildingTable {row.agency_stage_id} 的 promotionIds 数量必须为 4。")
            return
        if len(row.promotion_level_caps) != len(expected_promotion_ids):
            report.errors.append(f"Holmas_AgencyBuildingTable {row.agency_stage_id} 的 promotionLevelCaps 数量必须为 4。")
            return
        if len(row.promotion_upgrade_costs) != len(expected_promotion_ids):
            report.errors.append(f"Holmas_AgencyBuildingTable {row.agency_stage_id} 的 promotionUpgradeCosts 数量必须为 4。")
            return

        for promotion_index, expected_promotion_id in enumerate(expected_promotion_ids):
            if row.promotion_ids[promotion_index] != expected_promotion_id:
                report.errors.append(f"Holmas_AgencyBuildingTable {row.agency_stage_id} 的 promotionIds 顺序不正确。")
                return

            cap = row.promotion_level_caps[promotion_index]
            if cap != 5:
                report.errors.append(f"Holmas_AgencyBuildingTable {row.agency_stage_id} 的 promotion cap 必须固定为 5: {row.promotion_ids[promotion_index]}。")
                return

            costs = row.promotion_upgrade_costs[promotion_index].costs if row.promotion_upgrade_costs[promotion_index] else []
            if len(costs) != cap:
                report.errors.append(
                    f"Holmas_AgencyBuildingTable {row.agency_stage_id} 的 promotion {row.promotion_ids[promotion_index]} 成本档位数量与 cap 不一致。"
                )
                return
            for cost in costs:
                if cost <= 0:
                    report.errors.append(f"Holmas_AgencyBuildingTable {row.agency_stage_id} 的 promotion {row.promotion_ids[promotion_index]} 存在非正费用。")
                    return


def _build_core_package(
    map_rows: list[MapSheetRow],
    task_rows: list[TaskSheetRow],
    player_level_rows: list[PlayerLevelSheetRow],
    agency_building_rows: list[AgencyBuildingSheetRow],
) -> CoreConfigPackage:
    return CoreConfigPackage(
        version=CURRENT_VERSION,
        maps=[
            MapRow(
                map_id=row.map_id,
                terrain_path=row.terrain_path,
                cat_count_min=row.cat_count_min,
                cat_count_max=row.cat_count_max,
            )
            for row in map_rows
        ],
        tasks=[
            TaskRow(
                task_type_id=row.task_type_id,
                task_kind=row.task_kind_code,
                cat_indices=list(row.cat_indices),
                count_min=row.count_min,
                count_max=row.count_max,
                reward_values=list(row.reward_array),
                level_reward_factor=_to_float32(row.level_reward_factor),
            )
            for row in task_rows
        ],
        player_levels=[
            PlayerLevelRow(
                player_level=row.player_level,
                upgrade_exp=row.upgrade_exp,
                offline_reward_per_hour=row.offline_reward_per_hour,
                ad_unlock_hours=row.ad_unlock_hours,
                task_type_indices=list(row.task_type_indices),
                task_type_weights=list(row.task_type_weights),
                map_indices=list(row.map_indices),
                map_weights=list(row.map_weights),
            )
            for row in player_level_rows
        ],
        meta_levels=[
            MetaLevelRow(
                player_level=row.player_level,
                min_experience=row.upgrade_exp,
                offline_reward_per_hour=row.offline_reward_per_hour,
                ad_unlock_hours=row.ad_unlock_hours,
                notes=row.notes,
            )
            for row in player_level_rows
        ],
        agency_buildings=[
            AgencyBuildingRow(
                agency_stage_id=row.agency_stage_id,
                stage_name=row.stage_name,
                promotion_ids=list(row.promotion_ids),
                promotion_level_caps=list(row.promotion_level_caps),
                promotion_upgrade_costs=[
                    AgencyBuildingCostRow(costs=list(cost_row.costs))
                    for cost_row in row.promotion_upgrade_costs
                ],
                notes=row.notes,
            )
            for row in agency_building_rows
        ],
    )


def _build_cat_package(cat_rows: list[CatSheetRow]) -> CatMetaPackage:
    return CatMetaPackage(
        version=CURRENT_VERSION,
        cats=[
            CatMetaRow(
                cat_id=row.cat_id,
                cat_name=row.cat_name,
                icon_path=row.icon_path,
                rarity=row.rarity,
                weight=row.weight,
                price=row.price,
            )
            for row in cat_rows
        ],
    )


def _build_header_map(report: ExportReport, source_path: str, header_row: list[str] | None) -> dict[str, int]:
    header_map: dict[str, int] = {}
    if header_row is None:
        report.errors.append(f"{source_path} 缺少字段名行。")
        return header_map

    for index, header in enumerate(header_row):
        header_text = (header or "").strip()
        if not header_text:
            continue
        header_key = header_text.lower()
        if header_key in header_map:
            report.errors.append(f"{source_path} 存在重复字段名: {header_text}")
            continue
        header_map[header_key] = index

    return header_map


def _require_column(report: ExportReport, source_path: str, header_map: dict[str, int], column_name: str) -> int:
    index = _get_optional_column(header_map, column_name)
    if index < 0:
        report.errors.append(f"{source_path} 缺少必要列: {column_name}")
    return index


def _get_optional_column(header_map: dict[str, int], column_name: str) -> int:
    return header_map.get(column_name.lower(), -1)


def _get_cell(row: list[str] | None, index: int) -> str:
    if row is None or index < 0 or index >= len(row):
        return ""
    return (row[index] or "").strip()


def _is_blank_row(row: list[str] | None) -> bool:
    if not row:
        return True
    return all(not (item or "").strip() for item in row)


def _try_parse_int(text: str) -> tuple[int, bool]:
    try:
        return int(text), True
    except (TypeError, ValueError):
        return 0, False


def _parse_int(text: str, default_value: int) -> tuple[int, bool]:
    value, ok = _try_parse_int(text)
    return (value if ok else default_value), ok


def _parse_float32(text: str, default_value: float) -> tuple[float, bool]:
    try:
        return _to_float32(float(text)), True
    except (TypeError, ValueError):
        return _to_float32(default_value), False


def _parse_byte(text: str, default_value: int) -> tuple[int, bool]:
    value, ok = _try_parse_int(text)
    if not ok or value < 0 or value > 255:
        return default_value, False
    return value, True


def _parse_int_array(text: str, source_path: str, row_number: int, report: ExportReport) -> list[int]:
    if not text or not text.strip():
        return []

    values: list[int] = []
    for part in _split_array(text):
        if not part:
            continue
        try:
            values.append(int(part))
        except ValueError:
            report.errors.append(f"{source_path} 第 {row_number} 行数组值无法解析为整数: {part}")
    return values


def _parse_int_array_strict(text: str, source_path: str, row_number: int, report: ExportReport) -> list[int]:
    if not text or not text.strip():
        return []

    values: list[int] = []
    for part in _split_array_preserve_empty(text):
        if not part:
            report.errors.append(f"{source_path} 第 {row_number} 行数组存在空值。")
            return []
        try:
            values.append(int(part))
        except ValueError:
            report.errors.append(f"{source_path} 第 {row_number} 行数组值无法解析为整数: {part}")
            return []
    return values


def _parse_nested_int_arrays(text: str, source_path: str, row_number: int, report: ExportReport) -> list[AgencyBuildingCostSheetRow]:
    if not text or not text.strip():
        return []

    segments = [
        item.strip()
        for item in text.replace("｜", "|").split("|")
        if item.strip()
    ]
    rows: list[AgencyBuildingCostSheetRow] = []
    for segment_index, segment in enumerate(segments, start=1):
        parts = [
            item.strip()
            for item in segment.replace("；", ";").split(";")
            if item.strip()
        ]
        costs: list[int] = []
        for part in parts:
            try:
                costs.append(int(part))
            except ValueError:
                report.errors.append(f"{source_path} 第 {row_number} 行第 {segment_index} 段升级费用无法解析为整数: {part}")
        rows.append(AgencyBuildingCostSheetRow(costs=costs))
    return rows


def _split_array(text: str) -> list[str]:
    if not text or not text.strip():
        return []
    return [item.strip() for item in text.replace("；", ";").split(";") if item.strip()]


def _split_array_preserve_empty(text: str) -> list[str]:
    if not text or not text.strip():
        return []
    return [item.strip() for item in text.replace("；", ";").split(";")]


def _normalize_task_kind(text: str) -> tuple[str, bool, bool]:
    normalized = "Money" if not text or not text.strip() else text.strip()
    missing = not text or not text.strip()
    if normalized.lower() == "money":
        return "Money", True, missing
    if normalized.lower() == "gamble":
        return normalized, False, missing
    return normalized, False, missing


def _get_task_kind_code(task_kind: str) -> int:
    return 1 if task_kind.lower() == "gamble" else 0


def _build_alias_lookup(items: Iterable[tuple[str, int]]) -> dict[str, int]:
    lookup: dict[str, int] = {}
    for value, index in items:
        for candidate in _get_lookup_candidates(value):
            if candidate not in lookup:
                lookup[candidate] = index
    return lookup


def _resolve_index(value: str, lookup: dict[str, int]) -> tuple[int, bool]:
    if not value or lookup is None:
        return -1, False
    for candidate in _get_lookup_candidates(value):
        if candidate in lookup:
            return lookup[candidate], True
    return -1, False


def _get_lookup_candidates(value: str):
    trimmed = (value or "").strip()
    if not trimmed:
        return []

    candidates = [trimmed]
    digit_start = len(trimmed)
    while digit_start > 0 and trimmed[digit_start - 1].isdigit():
        digit_start -= 1

    if digit_start < len(trimmed):
        suffix = trimmed[digit_start:].lstrip("0")
        candidates.append(suffix or "0")

    return candidates


def _display_path(repo_root: Path, path: Path) -> str:
    try:
        relative = path.resolve().relative_to(repo_root.resolve())
        return relative.as_posix()
    except ValueError:
        return path.as_posix()


def _to_float32(value: float) -> float:
    return unpack("<f", pack("<f", float(value)))[0]


def _utc_now_string() -> str:
    return datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%S.%f") + "0Z"
