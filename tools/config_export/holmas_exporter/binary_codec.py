from __future__ import annotations

from io import BytesIO
from struct import pack

from .models import CAT_META_MAGIC, CORE_MAGIC, CURRENT_VERSION, CatMetaPackage, CoreConfigPackage


def write_core_package(package: CoreConfigPackage) -> bytes:
    stream = BytesIO()
    _write_int32(stream, CORE_MAGIC)
    _write_int32(stream, CURRENT_VERSION)
    _write_int32(stream, package.version)
    _write_map_rows(stream, package.maps)
    _write_task_rows(stream, package.tasks)
    _write_player_level_rows(stream, package.player_levels)
    _write_agency_building_rows(stream, package.agency_buildings)
    return stream.getvalue()


def write_cat_meta_package(package: CatMetaPackage) -> bytes:
    stream = BytesIO()
    _write_int32(stream, CAT_META_MAGIC)
    _write_int32(stream, CURRENT_VERSION)
    _write_int32(stream, package.version)
    _write_cat_rows(stream, package.cats)
    return stream.getvalue()


def _write_map_rows(stream: BytesIO, rows) -> None:
    _write_int32(stream, len(rows))
    for row in rows:
        _write_string(stream, row.map_id)
        _write_string(stream, row.terrain_path)
        _write_int32(stream, row.cat_count_min)
        _write_int32(stream, row.cat_count_max)


def _write_cat_rows(stream: BytesIO, rows) -> None:
    _write_int32(stream, len(rows))
    for row in rows:
        _write_string(stream, row.cat_id)
        _write_string(stream, row.cat_name)
        _write_string(stream, row.icon_path)
        _write_int32(stream, row.rarity)
        _write_int32(stream, row.weight)
        _write_int32(stream, row.price)


def _write_task_rows(stream: BytesIO, rows) -> None:
    _write_int32(stream, len(rows))
    for row in rows:
        _write_string(stream, row.task_type_id)
        _write_byte(stream, row.task_kind)
        _write_int_array(stream, row.cat_indices)
        _write_int32(stream, row.count_min)
        _write_int32(stream, row.count_max)
        _write_int_array(stream, row.reward_values)
        _write_float32(stream, row.level_reward_factor)


def _write_player_level_rows(stream: BytesIO, rows) -> None:
    _write_int32(stream, len(rows))
    for row in rows:
        _write_int32(stream, row.player_level)
        _write_int32(stream, row.upgrade_exp)
        _write_int32(stream, row.offline_reward_per_hour)
        _write_int32(stream, row.ad_unlock_hours)
        _write_int_array(stream, row.task_type_indices)
        _write_int_array(stream, row.task_type_weights)
        _write_int_array(stream, row.map_indices)
        _write_int_array(stream, row.map_weights)
def _write_agency_building_rows(stream: BytesIO, rows) -> None:
    _write_int32(stream, len(rows))
    for row in rows:
        _write_int32(stream, row.agency_stage_id)
        _write_string(stream, row.stage_name)
        _write_string_array(stream, row.promotion_ids)
        _write_int_array(stream, row.promotion_level_caps)
        _write_building_cost_rows(stream, row.promotion_upgrade_costs)


def _write_building_cost_rows(stream: BytesIO, rows) -> None:
    _write_int32(stream, len(rows))
    for row in rows:
        _write_int_array(stream, row.costs)


def _write_string(stream: BytesIO, value: str) -> None:
    encoded = (value or "").encode("utf-8")
    _write_7bit_encoded_int(stream, len(encoded))
    stream.write(encoded)


def _write_string_array(stream: BytesIO, values) -> None:
    _write_int32(stream, len(values))
    for value in values:
        _write_string(stream, value)


def _write_int_array(stream: BytesIO, values) -> None:
    _write_int32(stream, len(values))
    for value in values:
        _write_int32(stream, value)


def _write_int32(stream: BytesIO, value: int) -> None:
    stream.write(pack("<i", int(value)))


def _write_byte(stream: BytesIO, value: int) -> None:
    stream.write(pack("<B", int(value) & 0xFF))


def _write_float32(stream: BytesIO, value: float) -> None:
    stream.write(pack("<f", float(value)))


def _write_7bit_encoded_int(stream: BytesIO, value: int) -> None:
    remaining = int(value)
    while remaining >= 0x80:
        stream.write(bytes(((remaining | 0x80) & 0xFF,)))
        remaining >>= 7
    stream.write(bytes((remaining & 0xFF,)))
