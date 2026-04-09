from __future__ import annotations

from pathlib import Path
from xml.etree import ElementTree
import zipfile


SPREADSHEET_NAMESPACE = "http://schemas.openxmlformats.org/spreadsheetml/2006/main"
DOCUMENT_RELATIONSHIP_NAMESPACE = "http://schemas.openxmlformats.org/officeDocument/2006/relationships"
PACKAGE_RELATIONSHIP_NAMESPACE = "http://schemas.openxmlformats.org/package/2006/relationships"

NS = {
    "a": SPREADSHEET_NAMESPACE,
    "r": DOCUMENT_RELATIONSHIP_NAMESPACE,
    "p": PACKAGE_RELATIONSHIP_NAMESPACE,
}


class XlsxReadError(RuntimeError):
    pass


def read_first_worksheet(path: str | Path) -> list[list[str]]:
    workbook_path = Path(path)
    if not workbook_path.is_file():
        raise XlsxReadError(f"找不到 xlsx 文件: {workbook_path}")

    try:
        with zipfile.ZipFile(workbook_path, "r") as archive:
            shared_strings = _read_shared_strings(archive)
            worksheet_path = _resolve_first_worksheet_path(archive)
            if not worksheet_path:
                raise XlsxReadError(f"xlsx 缺少工作表: {workbook_path}")

            try:
                worksheet_xml = archive.read(worksheet_path)
            except KeyError as exc:
                raise XlsxReadError(f"xlsx 找不到工作表内容: {worksheet_path}") from exc

            return _read_worksheet_rows(worksheet_xml, shared_strings)
    except XlsxReadError:
        raise
    except Exception as exc:  # pragma: no cover - defensive wrapper
        raise XlsxReadError(str(exc)) from exc


def _read_shared_strings(archive: zipfile.ZipFile) -> dict[int, str]:
    try:
        data = archive.read("xl/sharedStrings.xml")
    except KeyError:
        return {}

    document = ElementTree.fromstring(data)
    items = document.findall("a:si", NS)
    return {index: _read_rich_text(item) for index, item in enumerate(items)}


def _resolve_first_worksheet_path(archive: zipfile.ZipFile) -> str:
    try:
        workbook_xml = archive.read("xl/workbook.xml")
        relations_xml = archive.read("xl/_rels/workbook.xml.rels")
    except KeyError:
        return ""

    workbook_document = ElementTree.fromstring(workbook_xml)
    relations_document = ElementTree.fromstring(relations_xml)

    first_sheet = workbook_document.find("a:sheets/a:sheet", NS)
    if first_sheet is None:
        return ""

    relation_id = first_sheet.attrib.get(f"{{{DOCUMENT_RELATIONSHIP_NAMESPACE}}}id", "").strip()
    if not relation_id:
        return ""

    for relation in relations_document.findall("p:Relationship", NS):
        if relation.attrib.get("Id") != relation_id:
            continue

        target = relation.attrib.get("Target", "").strip()
        if not target:
            return ""
        if target.startswith("/"):
            return target.lstrip("/")
        return "xl/" + target.lstrip("/")

    return ""


def _read_worksheet_rows(worksheet_xml: bytes, shared_strings: dict[int, str]) -> list[list[str]]:
    document = ElementTree.fromstring(worksheet_xml)
    rows: list[list[str]] = []

    for row_node in document.findall("a:sheetData/a:row", NS):
        cell_nodes = row_node.findall("a:c", NS)
        if not cell_nodes:
            rows.append([])
            continue

        values: dict[int, str] = {}
        max_column_index = -1
        for cell_node in cell_nodes:
            column_index = _get_column_index(cell_node.attrib.get("r", ""))
            if column_index < 0:
                continue

            max_column_index = max(max_column_index, column_index)
            values[column_index] = _read_cell_value(cell_node, shared_strings)

        if max_column_index < 0:
            rows.append([])
            continue

        row = [""] * (max_column_index + 1)
        for column_index, value in values.items():
            row[column_index] = value
        rows.append(row)

    return rows


def _read_cell_value(cell_node: ElementTree.Element, shared_strings: dict[int, str]) -> str:
    cell_type = cell_node.attrib.get("t", "")
    if cell_type == "inlineStr":
        inline_node = cell_node.find("a:is", NS)
        return _read_rich_text(inline_node) if inline_node is not None else ""

    value_node = cell_node.find("a:v", NS)
    if value_node is None or value_node.text is None:
        return ""

    raw_value = value_node.text
    if cell_type == "s":
        try:
            return shared_strings.get(int(raw_value), "")
        except ValueError:
            return ""

    return raw_value


def _read_rich_text(node: ElementTree.Element | None) -> str:
    if node is None:
        return ""

    text_nodes = node.findall(".//a:t", NS)
    if not text_nodes:
        return "".join(node.itertext())

    return "".join(text_node.text or "" for text_node in text_nodes)


def _get_column_index(reference: str) -> int:
    if not reference:
        return -1

    value = 0
    for char in reference:
        if not char.isalpha():
            break
        value = (value * 26) + (ord(char.upper()) - ord("A") + 1)

    return value - 1
