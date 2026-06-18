#!/usr/bin/env python3
import argparse
import hashlib
import re
import sys
from pathlib import Path

LEGACY_GUID = "1693f6cae3cf4a04a1db136bfe640668"
FOUNDATION_GUID = "0248dd89852e42a3a8995da77973a8ea"

PREFABS = [
    "Assets/HotUpdateContent/Res/Perfabs/UI/MainPanel.prefab",
    "Assets/HotUpdateContent/Res/Perfabs/UI/BattlePanel.prefab",
    "Assets/HotUpdateContent/Res/Perfabs/UI/LoadingPanel.prefab",
    "Assets/HotUpdateContent/Res/Perfabs/UI/LeadbroadPanel.prefab",
    "Assets/HotUpdateContent/Res/Perfabs/Generated/Holmas/Portrait/AgencyMainPanel.prefab",
]

BLOCK_RE = re.compile(r"(?ms)^--- !u!114 &(?P<file_id>\d+)\nMonoBehaviour:\n(?P<body>.*?)(?=^--- !u!|\Z)")
FIELD_RE = re.compile(r"^  - _bindingKey: (?P<binding_key>.*)\n    _componentType: (?P<component_type>.*)\n    _nodePath: (?P<node_path>.*)\n    _eventName:(?: (?P<event_name>.*))?\n    _target: (?P<target>\{fileID: .*?\})$", re.MULTILINE)


def stable_file_id(prefab_path: str, old_file_id: str) -> str:
    digest = hashlib.sha1((prefab_path + ":" + old_file_id + ":foundation").encode("utf-8")).hexdigest()
    # Keep it positive and inside signed 63-bit to match Unity's text serialization style.
    value = int(digest[:15], 16) & 0x7FFFFFFFFFFFFFFF
    if value == int(old_file_id):
        value += 1
    return str(value)


def find_collector_blocks(text: str, guid: str):
    return [match for match in BLOCK_RE.finditer(text) if f"guid: {guid}" in match.group("body")]


def parse_entries(block_body: str):
    entries = []
    seen = set()
    for match in FIELD_RE.finditer(block_body):
        entry = {
            "binding_key": match.group("binding_key") or "",
            "component_type": match.group("component_type") or "",
            "node_path": match.group("node_path") or "",
            "event_name": match.group("event_name") or "",
            "target": match.group("target") or "{fileID: 0}",
        }
        key = (entry["binding_key"], entry["component_type"], entry["node_path"], entry["event_name"], entry["target"])
        if key in seen:
            continue

        seen.add(key)
        entries.append(entry)
    return entries


def format_foundation_block(file_id: str, game_object: str, entries):
    lines = [
        f"--- !u!114 &{file_id}",
        "MonoBehaviour:",
        "  m_ObjectHideFlags: 0",
        "  m_CorrespondingSourceObject: {fileID: 0}",
        "  m_PrefabInstance: {fileID: 0}",
        "  m_PrefabAsset: {fileID: 0}",
        f"  m_GameObject: {game_object}",
        "  m_Enabled: 1",
        "  m_EditorHideFlags: 0",
        f"  m_Script: {{fileID: 11500000, guid: {FOUNDATION_GUID}, type: 3}}",
        "  m_Name: ",
        "  m_EditorClassIdentifier: ",
        "  _entries:",
    ]

    for entry in entries:
        lines.extend(
            [
                f"  - Id: {entry['binding_key']}",
                f"    Path: {entry['node_path']}",
                f"    ComponentType: {entry['component_type']}",
                f"    EventName: {entry['event_name']}",
                "    Notes: ",
                "    RequiresManualWiring: 0",
                "    Required: 1",
                f"    Target: {entry['target']}",
            ]
        )

    return "\n".join(lines) + "\n"


def extract_game_object(block_body: str):
    match = re.search(r"^  m_GameObject: (?P<value>\{fileID: \d+\})$", block_body, re.MULTILINE)
    if not match:
        raise RuntimeError("collector block is missing m_GameObject")
    return match.group("value")


def scan(root: Path):
    reports = []
    ok = True
    for prefab in PREFABS:
        path = root / prefab
        text = path.read_text(encoding="utf-8")
        legacy_blocks = find_collector_blocks(text, LEGACY_GUID)
        foundation_blocks = find_collector_blocks(text, FOUNDATION_GUID)
        reports.append(f"{prefab} legacyCollectors={len(legacy_blocks)} hasFoundationCollector={len(foundation_blocks) > 0} foundationCollectors={len(foundation_blocks)}")
        for block in legacy_blocks:
            entries = parse_entries(block.group("body"))
            reports.append(f"  legacyCollector fileID={block.group('file_id')} entries={len(entries)}")
            for index, entry in enumerate(entries):
                reports.append(
                    "    [{0}] bindingKey={binding_key} componentType={component_type} nodePath={node_path} eventName={event_name} target={target}".format(
                        index,
                        **entry,
                    )
                )
        for block in foundation_blocks:
            entries = parse_entries(block.group("body").replace("  - Id:", "  - _bindingKey:").replace("    Path:", "    _nodePath:").replace("    ComponentType:", "    _componentType:").replace("    EventName:", "    _eventName:").replace("    Target:", "    _target:"))
            reports.append(f"  foundationCollector fileID={block.group('file_id')} parsedEntries={len(entries)}")
        if len(legacy_blocks) != 1:
            ok = False
    return ok, "\n".join(reports)


def migrate(root: Path):
    reports = []
    for prefab in PREFABS:
        path = root / prefab
        text = path.read_text(encoding="utf-8")
        legacy_blocks = find_collector_blocks(text, LEGACY_GUID)
        foundation_blocks = find_collector_blocks(text, FOUNDATION_GUID)
        if len(legacy_blocks) != 1:
            raise RuntimeError(f"{prefab}: expected 1 legacy collector, found {len(legacy_blocks)}")
        if foundation_blocks:
            raise RuntimeError(f"{prefab}: already has Foundation collector")

        block = legacy_blocks[0]
        old_file_id = block.group("file_id")
        new_file_id = stable_file_id(prefab, old_file_id)
        if f"&{new_file_id}" in text or f"fileID: {new_file_id}" in text:
            raise RuntimeError(f"{prefab}: generated fileID collides: {new_file_id}")

        body = block.group("body")
        entries = parse_entries(body)
        if not entries:
            raise RuntimeError(f"{prefab}: legacy collector has no parseable entries")

        component_reference = f"  - component: {{fileID: {old_file_id}}}"
        if component_reference not in text:
            raise RuntimeError(f"{prefab}: GameObject component list does not reference collector {old_file_id}")

        new_block = format_foundation_block(new_file_id, extract_game_object(body), entries)
        text = text[: block.start()] + new_block + text[block.end() :]
        text = text.replace(component_reference, f"  - component: {{fileID: {new_file_id}}}", 1)
        path.write_text(text, encoding="utf-8")
        reports.append(f"{prefab} migrated entries={len(entries)} oldFileID={old_file_id} newFileID={new_file_id}")
    return "\n".join(reports)


def verify(root: Path):
    reports = []
    ok = True
    for prefab in PREFABS:
        text = (root / prefab).read_text(encoding="utf-8")
        legacy_count = len(find_collector_blocks(text, LEGACY_GUID))
        foundation_blocks = find_collector_blocks(text, FOUNDATION_GUID)
        foundation_count = len(foundation_blocks)
        entry_count = 0
        if foundation_blocks:
            block = foundation_blocks[0]
            entry_count = len(re.findall(r"^  - Id: ", block.group("body"), re.MULTILINE))
            duplicate_count = count_foundation_duplicates(block.group("body"))
            component_reference = f"  - component: {{fileID: {block.group('file_id')}}}"
            if component_reference not in text:
                ok = False
                reports.append(f"{prefab} foundationCollectorNotInComponentList fileID={block.group('file_id')}")
            if duplicate_count:
                ok = False
                reports.append(f"{prefab} duplicateFoundationEntries={duplicate_count}")
        if legacy_count != 0 or foundation_count != 1 or entry_count <= 0:
            ok = False
        reports.append(f"{prefab} legacyCollectors={legacy_count} foundationCollectors={foundation_count} foundationEntries={entry_count}")
    return ok, "\n".join(reports)


def count_foundation_duplicates(block_body: str):
    entry_re = re.compile(
        r"^  - Id: (?P<binding_key>.*)\n"
        r"    Path: (?P<node_path>.*)\n"
        r"    ComponentType: (?P<component_type>.*)\n"
        r"    EventName: (?P<event_name>.*)\n"
        r"    Notes: .*\n"
        r"    RequiresManualWiring: .*\n"
        r"    Required: .*\n"
        r"    Target: (?P<target>\{fileID: .*?\})$",
        re.MULTILINE,
    )
    seen = set()
    duplicate_count = 0
    for match in entry_re.finditer(block_body):
        key = (
            match.group("binding_key"),
            match.group("component_type"),
            match.group("node_path"),
            match.group("event_name"),
            match.group("target"),
        )
        if key in seen:
            duplicate_count += 1
        else:
            seen.add(key)
    return duplicate_count


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("mode", choices=["scan", "migrate", "verify"])
    parser.add_argument("--root", default=".")
    args = parser.parse_args()

    root = Path(args.root).resolve()
    if args.mode == "scan":
        ok, report = scan(root)
        print(report)
        return 0 if ok else 1
    if args.mode == "migrate":
        print(migrate(root))
        return 0
    ok, report = verify(root)
    print(report)
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
