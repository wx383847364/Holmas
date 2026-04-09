from __future__ import annotations

import json
from pathlib import Path
from struct import pack, unpack

from .models import (
    AgencyBuildingCostRow,
    AgencyBuildingRow,
    BundleReport,
    CatMetaPackage,
    CatMetaRow,
    CoreConfigPackage,
    ExportReport,
    MapRow,
    MetaLevelRow,
    PlayerLevelRow,
    TaskRow,
)


def write_core_json(path: Path, package: CoreConfigPackage) -> None:
    _write_json(path, _core_package_to_dict(package))


def write_cat_json(path: Path, package: CatMetaPackage) -> None:
    _write_json(path, _cat_package_to_dict(package))


def write_report_json(path: Path, report: ExportReport) -> None:
    _write_json(path, _report_to_dict(report))


def _write_json(path: Path, payload: dict) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, ensure_ascii=False, indent=4) + "\n", encoding="utf-8")


def _core_package_to_dict(package: CoreConfigPackage) -> dict:
    return {
        "Version": package.version,
        "Maps": [_map_row_to_dict(row) for row in package.maps],
        "Tasks": [_task_row_to_dict(row) for row in package.tasks],
        "PlayerLevels": [_player_level_row_to_dict(row) for row in package.player_levels],
        "MetaLevels": [_meta_level_row_to_dict(row) for row in package.meta_levels],
        "AgencyBuildings": [_agency_building_row_to_dict(row) for row in package.agency_buildings],
    }


def _cat_package_to_dict(package: CatMetaPackage) -> dict:
    return {
        "Version": package.version,
        "Cats": [_cat_row_to_dict(row) for row in package.cats],
    }


def _report_to_dict(report: ExportReport) -> dict:
    return {
        "ExportedAtUtc": report.exported_at_utc,
        "Success": report.success,
        "BinaryWrittenCount": report.binary_written_count,
        "SourceFiles": list(report.source_files),
        "BundleReports": [_bundle_report_to_dict(bundle) for bundle in report.bundle_reports],
        "Errors": list(report.errors),
        "Warnings": list(report.warnings),
    }


def _bundle_report_to_dict(bundle: BundleReport) -> dict:
    return {
        "BundleName": bundle.bundle_name,
        "SourceTableNames": list(bundle.source_table_names),
        "PreviewJsonPath": bundle.preview_json_path,
        "BinaryPath": bundle.binary_path,
        "RowCount": bundle.row_count,
        "WarningCount": bundle.warning_count,
        "ErrorCount": bundle.error_count,
    }


def _map_row_to_dict(row: MapRow) -> dict:
    return {
        "MapId": row.map_id,
        "TerrainPath": row.terrain_path,
        "CatCountMin": row.cat_count_min,
        "CatCountMax": row.cat_count_max,
    }


def _cat_row_to_dict(row: CatMetaRow) -> dict:
    return {
        "CatId": row.cat_id,
        "CatName": row.cat_name,
        "IconPath": row.icon_path,
        "Rarity": row.rarity,
        "Weight": row.weight,
        "Price": row.price,
    }


def _task_row_to_dict(row: TaskRow) -> dict:
    return {
        "TaskTypeId": row.task_type_id,
        "TaskKind": row.task_kind,
        "CatIndices": list(row.cat_indices),
        "CountMin": row.count_min,
        "CountMax": row.count_max,
        "RewardValues": list(row.reward_values),
        "LevelRewardFactor": _to_float32(row.level_reward_factor),
    }


def _player_level_row_to_dict(row: PlayerLevelRow) -> dict:
    return {
        "PlayerLevel": row.player_level,
        "UpgradeExp": row.upgrade_exp,
        "TaskTypeIndices": list(row.task_type_indices),
        "TaskTypeWeights": list(row.task_type_weights),
        "MapIndices": list(row.map_indices),
        "MapWeights": list(row.map_weights),
    }


def _meta_level_row_to_dict(row: MetaLevelRow) -> dict:
    return {
        "PlayerLevel": row.player_level,
        "MinExperience": row.min_experience,
        "OfflineRewardPerHour": row.offline_reward_per_hour,
        "AdUnlockHours": row.ad_unlock_hours,
        "Notes": row.notes,
    }


def _agency_building_row_to_dict(row: AgencyBuildingRow) -> dict:
    return {
        "AgencyStageId": row.agency_stage_id,
        "StageName": row.stage_name,
        "PromotionIds": list(row.promotion_ids),
        "PromotionLevelCaps": list(row.promotion_level_caps),
        "PromotionUpgradeCosts": [_agency_building_cost_row_to_dict(cost_row) for cost_row in row.promotion_upgrade_costs],
        "Notes": row.notes,
    }


def _agency_building_cost_row_to_dict(row: AgencyBuildingCostRow) -> dict:
    return {
        "Costs": list(row.costs),
    }


def _to_float32(value: float) -> float:
    return unpack("<f", pack("<f", float(value)))[0]
