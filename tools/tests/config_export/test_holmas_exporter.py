from __future__ import annotations

import json
import sys
import tempfile
import unittest
import zipfile
from pathlib import Path


SCRIPT_ROOT = Path(__file__).resolve().parents[2] / "config_export"
if str(SCRIPT_ROOT) not in sys.path:
    sys.path.insert(0, str(SCRIPT_ROOT))

from holmas_exporter import export_all, validate_all  # noqa: E402
from holmas_exporter.xlsx_reader import read_first_worksheet  # noqa: E402


class HolmasPythonExporterTests(unittest.TestCase):
    def test_xlsx_reader_preserves_blank_columns(self) -> None:
        with tempfile.TemporaryDirectory(prefix="holmas_xlsx_reader_") as temp_dir:
            workbook_path = Path(temp_dir) / "blank_columns.xlsx"
            _write_workbook(
                workbook_path,
                "Sheet1",
                [
                    {"A1": "说明A", "C1": "说明C"},
                    {"A2": "fieldA", "C2": "fieldC"},
                    {"A3": "valueA", "C3": "valueC"},
                ],
                use_shared_strings=True,
            )

            rows = read_first_worksheet(workbook_path)

            self.assertEqual(rows[0], ["说明A", "", "说明C"])
            self.assertEqual(rows[1], ["fieldA", "", "fieldC"])
            self.assertEqual(rows[2], ["valueA", "", "valueC"])

    def test_export_all_writes_json_bytes_and_report(self) -> None:
        with tempfile.TemporaryDirectory(prefix="holmas_python_export_") as temp_dir:
            root = Path(temp_dir)
            repo_root = root / "Holmas"
            config_root = repo_root / "Assets" / "Config"
            json_root = config_root / "json"
            binary_root = repo_root / "Assets" / "HotUpdateContent" / "Config"
            config_root.mkdir(parents=True, exist_ok=True)
            json_root.mkdir(parents=True, exist_ok=True)
            binary_root.mkdir(parents=True, exist_ok=True)

            _write_fixture(config_root)

            dry_run_result = validate_all(repo_root, config_root, json_root, binary_root)
            self.assertTrue(dry_run_result.report.success, "\n".join(dry_run_result.report.errors))

            export_result = export_all(repo_root, config_root, json_root, binary_root)
            self.assertTrue(export_result.report.success, "\n".join(export_result.report.errors))
            self.assertEqual(export_result.report.binary_written_count, 2)

            core_json = json.loads((json_root / "holmas_core_config.json").read_text(encoding="utf-8"))
            cat_json = json.loads((json_root / "holmas_cat_meta.json").read_text(encoding="utf-8"))
            report_json = json.loads((json_root / "holmas_export_report.json").read_text(encoding="utf-8"))

            self.assertEqual(core_json["Version"], 6)
            self.assertEqual(core_json["PlayerLevels"][-1]["UpgradeExp"], 2000)
            self.assertNotIn("MetaLevels", core_json)
            self.assertEqual(cat_json["Cats"][0]["CatName"], "布偶猫")
            self.assertTrue(report_json["Success"])
            self.assertEqual(report_json["BinaryWrittenCount"], 2)
            self.assertTrue((binary_root / "holmas_core_config.bytes").is_file())
            self.assertTrue((binary_root / "holmas_cat_meta.bytes").is_file())

    def test_validate_all_accepts_matching_upgradeexp_and_minexperience_columns(self) -> None:
        with tempfile.TemporaryDirectory(prefix="holmas_python_export_dual_match_") as temp_dir:
            root = Path(temp_dir)
            repo_root = root / "Holmas"
            config_root = repo_root / "Assets" / "Config"
            json_root = config_root / "json"
            binary_root = repo_root / "Assets" / "HotUpdateContent" / "Config"
            config_root.mkdir(parents=True, exist_ok=True)
            json_root.mkdir(parents=True, exist_ok=True)
            binary_root.mkdir(parents=True, exist_ok=True)

            _write_fixture(config_root, growth_mode="both_match")

            result = validate_all(repo_root, config_root, json_root, binary_root)

            self.assertTrue(result.report.success, "\n".join(result.report.errors))

    def test_validate_all_rejects_conflicting_upgradeexp_and_minexperience_columns(self) -> None:
        with tempfile.TemporaryDirectory(prefix="holmas_python_export_dual_conflict_") as temp_dir:
            root = Path(temp_dir)
            repo_root = root / "Holmas"
            config_root = repo_root / "Assets" / "Config"
            json_root = config_root / "json"
            binary_root = repo_root / "Assets" / "HotUpdateContent" / "Config"
            config_root.mkdir(parents=True, exist_ok=True)
            json_root.mkdir(parents=True, exist_ok=True)
            binary_root.mkdir(parents=True, exist_ok=True)

            _write_fixture(config_root, growth_mode="both_conflict", conflict_level=7)

            result = validate_all(repo_root, config_root, json_root, binary_root)

            self.assertFalse(result.report.success)
            self.assertTrue(
                any("upgradeExp 与 minExperience 不一致" in error for error in result.report.errors),
                "\n".join(result.report.errors),
            )


def _write_fixture(config_root: Path, growth_mode: str = "min_only", conflict_level: int = -1) -> None:
    _write_tabular_workbook(
        config_root / "Holmas_MapTable.xlsx",
        "Sheet1",
        [
            ["地图id", "地图对应的数据地址Path", "猫的最大数量max", "猫的最小数量mini"],
            ["mapId", "terrainPath", "catCountMax", "catCountMin"],
            ["map_001", "Assets/HotUpdateContent/Res/Map/1.asset", "15", "12"],
        ],
    )

    _write_tabular_workbook(
        config_root / "Holmas_CatTable.xlsx",
        "Holmas_CatTable",
        [
            ["猫的id", "猫的名称", "对应的猫的图片资源地址path", "稀有度", "出现的权重值", "价格"],
            ["catId", "catName", "iconPath", "rarity", "weight", "price"],
            ["1", "布偶猫", "Assets/HotUpdateContent/Res/Icons/cat_01.png", "1", "1000", "1000"],
            ["2", "短毛猫", "Assets/HotUpdateContent/Res/Icons/cat_02.png", "1", "900", "1200"],
        ],
    )

    _write_tabular_workbook(
        config_root / "Holmas_TaskTable.xlsx",
        "Holmas_TaskTable",
        [
            ["任务种类id", "任务类型", "对应的猫的id数组", "数量的最大值max", "数量的最小值mini", "奖励数组", "等级奖励系数"],
            ["taskTypeId", "taskKind", "catIdList", "countMax", "countMin", "rewardArray", "levelRewardFactor"],
            ["task_001", "Money", "1;2", "2", "1", "", "1"],
        ],
    )

    min_experience = [0, 40, 85, 135, 190, 250, 320, 400, 490, 590, 700, 825, 965, 1120, 1290, 1475, 1675, 1840, 1930, 2000]
    if growth_mode == "both_match" or growth_mode == "both_conflict":
        player_rows = [
            ["玩家等级", "升级所需经验", "达到该等级所需累计经验", "每小时离线奖励", "广告解锁时长", "备注", "任务id组", "任务id组中任务出现权重", "地图id数组", "地图id权重数组"],
            ["playerLevel", "upgradeExp", "minExperience", "offlineRewardPerHour", "adUnlockHours", "notes", "taskTypeIds", "taskTypeWeights", "mapIds", "mapWeights"],
        ]
        for level, exp in enumerate(min_experience, start=1):
            upgrade_exp = exp + 1 if growth_mode == "both_conflict" and level == conflict_level else exp
            player_rows.append(
                [
                    str(level),
                    str(upgrade_exp),
                    str(exp),
                    str(6000 + (level - 1) * 600),
                    "8",
                    "",
                    "task_001",
                    "100",
                    "map_001",
                    "100",
                ]
            )
    else:
        player_rows = [
            ["玩家等级", "达到该等级所需累计经验", "每小时离线奖励", "广告解锁时长", "备注", "任务id组", "任务id组中任务出现权重", "地图id数组", "地图id权重数组"],
            ["playerLevel", "minExperience", "offlineRewardPerHour", "adUnlockHours", "notes", "taskTypeIds", "taskTypeWeights", "mapIds", "mapWeights"],
        ]
        for level, exp in enumerate(min_experience, start=1):
            player_rows.append(
                [
                    str(level),
                    str(exp),
                    str(6000 + (level - 1) * 600),
                    "8",
                    "",
                    "task_001",
                    "100",
                    "map_001",
                    "100",
                ]
            )
    _write_tabular_workbook(config_root / "Holmas_PlayerLevelTable.xlsx", "Holmas_PlayerLevelTable", player_rows)

    agency_rows = [["城市阶段id", "城市名", "宣传功能id数组", "宣传升级级数上限数组", "宣传升级费用数组", "备注"], ["agencyStageId", "stageName", "promotionIds", "promotionLevelCaps", "promotionUpgradeCosts", "notes"]]
    promotion_ids = "leaflet;radio;online;tv"
    promotion_caps = "5;5;5;5"
    promotion_costs = "100;200;300;400;500|120;240;360;480;600|140;280;420;560;700|160;320;480;640;800"
    for stage in range(1, 101):
        agency_rows.append([str(stage), f"城市{stage:03d}", promotion_ids, promotion_caps, promotion_costs, ""])
    _write_tabular_workbook(config_root / "Holmas_AgencyBuildingTable.xlsx", "Holmas_AgencyBuildingTable", agency_rows)


def _write_tabular_workbook(path: Path, sheet_name: str, rows: list[list[str]]) -> None:
    cell_rows: list[dict[str, str]] = []
    for row_index, row in enumerate(rows, start=1):
        cells: dict[str, str] = {}
        for column_index, value in enumerate(row, start=1):
            if value == "":
                continue
            cells[f"{_column_name(column_index)}{row_index}"] = value
        cell_rows.append(cells)
    _write_workbook(path, sheet_name, cell_rows, use_shared_strings=True)


def _write_workbook(path: Path, sheet_name: str, rows: list[dict[str, str]], use_shared_strings: bool) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    shared_string_lookup: dict[str, int] = {}
    shared_strings: list[str] = []

    if use_shared_strings:
        for row in rows:
            for value in row.values():
                if value not in shared_string_lookup:
                    shared_string_lookup[value] = len(shared_strings)
                    shared_strings.append(value)

    with zipfile.ZipFile(path, "w", compression=zipfile.ZIP_DEFLATED) as archive:
        _write_entry(
            archive,
            "[Content_Types].xml",
            """<?xml version="1.0" encoding="UTF-8"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
<Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
<Default Extension="xml" ContentType="application/xml"/>
<Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
<Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
"""
            + (
                '<Override PartName="/xl/sharedStrings.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sharedStrings+xml"/>'
                if use_shared_strings
                else ""
            )
            + "</Types>",
        )
        _write_entry(
            archive,
            "_rels/.rels",
            """<?xml version="1.0" encoding="UTF-8"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
<Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
</Relationships>""",
        )
        _write_entry(
            archive,
            "xl/workbook.xml",
            f"""<?xml version="1.0" encoding="UTF-8"?>
<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
<sheets><sheet name="{_escape_xml(sheet_name)}" sheetId="1" r:id="rId1"/></sheets>
</workbook>""",
        )
        _write_entry(
            archive,
            "xl/_rels/workbook.xml.rels",
            """<?xml version="1.0" encoding="UTF-8"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
<Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
"""
            + (
                '<Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/sharedStrings" Target="sharedStrings.xml"/>'
                if use_shared_strings
                else ""
            )
            + "</Relationships>",
        )
        if use_shared_strings:
            shared_string_xml = "".join(f"<si><t>{_escape_xml(value)}</t></si>" for value in shared_strings)
            _write_entry(
                archive,
                "xl/sharedStrings.xml",
                f"""<?xml version="1.0" encoding="UTF-8"?>
<sst xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" count="{len(shared_strings)}" uniqueCount="{len(shared_strings)}">{shared_string_xml}</sst>""",
            )

        sheet_rows_xml = []
        for row_index, row in enumerate(rows, start=1):
            cells_xml = []
            for reference, value in sorted(row.items()):
                if use_shared_strings:
                    shared_index = shared_string_lookup[value]
                    cells_xml.append(f'<c r="{reference}" t="s"><v>{shared_index}</v></c>')
                else:
                    cells_xml.append(f'<c r="{reference}" t="inlineStr"><is><t>{_escape_xml(value)}</t></is></c>')
            sheet_rows_xml.append(f'<row r="{row_index}">{"".join(cells_xml)}</row>')

        _write_entry(
            archive,
            "xl/worksheets/sheet1.xml",
            f"""<?xml version="1.0" encoding="UTF-8"?>
<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
<sheetData>{"".join(sheet_rows_xml)}</sheetData>
</worksheet>""",
        )


def _write_entry(archive: zipfile.ZipFile, name: str, content: str) -> None:
    archive.writestr(name, content.encode("utf-8"))


def _column_name(index: int) -> str:
    result = []
    value = index
    while value > 0:
        value, remainder = divmod(value - 1, 26)
        result.append(chr(ord("A") + remainder))
    return "".join(reversed(result))


def _escape_xml(value: str) -> str:
    return (
        value.replace("&", "&amp;")
        .replace("<", "&lt;")
        .replace(">", "&gt;")
        .replace('"', "&quot;")
        .replace("'", "&apos;")
    )


if __name__ == "__main__":
    unittest.main()
