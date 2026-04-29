from __future__ import annotations

import json
from pathlib import Path
from struct import pack, unpack

from .models import (
    AgencyBuildingTableCostRow,
    AgencyBuildingTableRow,
    BundleReport,
    CatMetaPackage,
    CatMetaRow,
    CoreConfigPackage,
    ExportReport,
    MapRow,
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
        "Holmas_MapTable": [_map_row_to_dict(row) for row in package.holmas_map_table],
        "Holmas_TaskTable": [_task_row_to_dict(row) for row in package.holmas_task_table],
        "Holmas_PlayerLevelTable": [_player_level_row_to_dict(row) for row in package.holmas_player_level_table],
        "Holmas_AgencyBuildingTable": [_agency_building_table_row_to_dict(row) for row in package.holmas_agency_building_table],
    }


def _cat_package_to_dict(package: CatMetaPackage) -> dict:
    return {
        "Version": package.version,
        "Holmas_CatTable": [_cat_row_to_dict(row) for row in package.holmas_cat_table],
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
        "mapId": row.map_id,
        "terrainPath": row.terrain_path,
        "catCountMin": row.cat_count_min,
        "catCountMax": row.cat_count_max,
    }


def _cat_row_to_dict(row: CatMetaRow) -> dict:
    return {
        "catId": row.cat_id,
        "catName": row.cat_name,
        "iconPath": row.icon_path,
        "rarity": row.rarity,
        "weight": row.weight,
        "price": row.price,
    }


def _task_row_to_dict(row: TaskRow) -> dict:
    return {
        "taskTypeId": row.task_type_id,
        "taskKind": row.task_kind,
        "catIdList": list(row.cat_id_list),
        "countMin": row.count_min,
        "countMax": row.count_max,
        "rewardArray": list(row.reward_values),
        "levelRewardFactor": _to_float32(row.level_reward_factor),
    }


def _player_level_row_to_dict(row: PlayerLevelRow) -> dict:
    return {
        "playerLevel": row.player_level,
        "minExperience": row.min_experience,
        "offlineRewardPerHour": row.offline_reward_per_hour,
        "adUnlockHours": row.ad_unlock_hours,
        "taskTypeIds": list(row.task_type_ids),
        "taskTypeWeights": list(row.task_type_weights),
        "mapIds": list(row.map_ids),
        "mapWeights": list(row.map_weights),
    }

def _agency_building_table_row_to_dict(row: AgencyBuildingTableRow) -> dict:
    return {
        "agencyStageId": row.agency_stage_id,
        "stageName": row.stage_name,
        "promotionIds": list(row.promotion_ids),
        "promotionLevelCaps": list(row.promotion_level_caps),
        "promotionUpgradeCosts": [_agency_building_table_cost_row_to_dict(cost_row) for cost_row in row.promotion_upgrade_costs],
        "notes": row.notes,
    }


def _agency_building_table_cost_row_to_dict(row: AgencyBuildingTableCostRow) -> dict:
    return {
        "costs": list(row.costs),
    }


def _to_float32(value: float) -> float:
    return unpack("<f", pack("<f", float(value)))[0]
