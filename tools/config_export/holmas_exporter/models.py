from __future__ import annotations

from dataclasses import dataclass, field


CORE_MAGIC = 0x48434F52
CAT_META_MAGIC = 0x48434154
CURRENT_VERSION = 7


@dataclass
class MapSheetRow:
    row_index: int = 0
    map_id: str = ""
    terrain_path: str = ""
    cat_count_min: int = 0
    cat_count_max: int = 0
    map_id_index: int = -1
    terrain_path_index: int = -1


@dataclass
class CatSheetRow:
    row_index: int = 0
    cat_id: str = ""
    cat_name: str = ""
    icon_path: str = ""
    rarity: int = 0
    weight: int = 1
    price: int = 0
    cat_id_index: int = -1
    cat_name_index: int = -1
    icon_path_index: int = -1


@dataclass
class TaskSheetRow:
    row_index: int = 0
    task_type_id: str = ""
    task_kind: str = ""
    task_kind_code: int = 0
    cat_id_list: list[str] = field(default_factory=list)
    cat_indices: list[int] = field(default_factory=list)
    count_min: int = 0
    count_max: int = 0
    reward_array: list[int] = field(default_factory=list)
    level_reward_factor: float = 1.0
    task_type_id_index: int = -1


@dataclass
class PlayerLevelSheetRow:
    row_index: int = 0
    player_level: int = 0
    min_experience: int = 0
    offline_reward_per_hour: int = 0
    ad_unlock_hours: int = 0
    notes: str = ""
    task_type_ids: list[str] = field(default_factory=list)
    task_type_indices: list[int] = field(default_factory=list)
    task_type_weights: list[int] = field(default_factory=list)
    map_ids: list[str] = field(default_factory=list)
    map_indices: list[int] = field(default_factory=list)
    map_weights: list[int] = field(default_factory=list)


@dataclass
class AgencyBuildingTableCostSheetRow:
    costs: list[int] = field(default_factory=list)


@dataclass
class AgencyBuildingTableSheetRow:
    row_index: int = 0
    agency_stage_id: int = 0
    stage_name: str = ""
    promotion_ids: list[str] = field(default_factory=list)
    promotion_level_caps: list[int] = field(default_factory=list)
    promotion_upgrade_costs: list[AgencyBuildingTableCostSheetRow] = field(default_factory=list)
    notes: str = ""


@dataclass
class MapRow:
    map_id: str = ""
    terrain_path: str = ""
    cat_count_min: int = 0
    cat_count_max: int = 0


@dataclass
class CatMetaRow:
    cat_id: str = ""
    cat_name: str = ""
    icon_path: str = ""
    rarity: int = 0
    weight: int = 1
    price: int = 0


@dataclass
class TaskRow:
    task_type_id: str = ""
    task_kind: int = 0
    cat_id_list: list[str] = field(default_factory=list)
    count_min: int = 0
    count_max: int = 0
    reward_values: list[int] = field(default_factory=list)
    level_reward_factor: float = 1.0


@dataclass
class PlayerLevelRow:
    player_level: int = 0
    min_experience: int = 0
    offline_reward_per_hour: int = 0
    ad_unlock_hours: int = 24
    task_type_ids: list[str] = field(default_factory=list)
    task_type_weights: list[int] = field(default_factory=list)
    map_ids: list[str] = field(default_factory=list)
    map_weights: list[int] = field(default_factory=list)


@dataclass
class AgencyBuildingTableCostRow:
    costs: list[int] = field(default_factory=list)


@dataclass
class AgencyBuildingTableRow:
    agency_stage_id: int = 0
    stage_name: str = ""
    promotion_ids: list[str] = field(default_factory=list)
    promotion_level_caps: list[int] = field(default_factory=list)
    promotion_upgrade_costs: list[AgencyBuildingTableCostRow] = field(default_factory=list)
    notes: str = ""


@dataclass
class CoreConfigPackage:
    version: int = CURRENT_VERSION
    holmas_map_table: list[MapRow] = field(default_factory=list)
    holmas_task_table: list[TaskRow] = field(default_factory=list)
    holmas_player_level_table: list[PlayerLevelRow] = field(default_factory=list)
    holmas_agency_building_table: list[AgencyBuildingTableRow] = field(default_factory=list)


@dataclass
class CatMetaPackage:
    version: int = CURRENT_VERSION
    holmas_cat_table: list[CatMetaRow] = field(default_factory=list)


@dataclass
class BundleReport:
    bundle_name: str = ""
    source_table_names: list[str] = field(default_factory=list)
    preview_json_path: str = ""
    binary_path: str = ""
    row_count: int = 0
    warning_count: int = 0
    error_count: int = 0


@dataclass
class ExportReport:
    exported_at_utc: str = ""
    success: bool = False
    binary_written_count: int = 0
    source_files: list[str] = field(default_factory=list)
    bundle_reports: list[BundleReport] = field(default_factory=list)
    errors: list[str] = field(default_factory=list)
    warnings: list[str] = field(default_factory=list)

    @property
    def error_count(self) -> int:
        return len(self.errors)

    @property
    def warning_count(self) -> int:
        return len(self.warnings)
