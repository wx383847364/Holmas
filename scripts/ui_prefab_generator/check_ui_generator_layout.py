#!/usr/bin/env python3
from pathlib import Path
import re
import sys


REQUIRED_PATHS = [
    Path("doc/长期主文档/UI自动生成系统/00_总览.md"),
    Path("doc/长期主文档/UI自动生成系统/09_Holmas接入约束.md"),
    Path("Assets/Tools/UiPrefabGenerator/Runtime/Core/UiPrefabGenerator.Core.asmdef"),
    Path("Assets/Tools/UiPrefabGenerator/Runtime/Core/Intake/UiPrefabGenerator.Core.Intake.asmdef"),
    Path("Assets/Tools/UiPrefabGenerator/Runtime/Core/Intake/DesignPacketToUiPrefabSpecInterpreter.cs"),
    Path("Assets/Tools/UiPrefabGenerator/Runtime/Core/Manifest/PrefabBindingManifestBuilder.cs"),
    Path("Assets/Tools/UiPrefabGenerator/Runtime/HolmasAdapter/UiPrefabGenerator.HolmasAdapter.asmdef"),
    Path("Assets/Tools/UiPrefabGenerator/Editor/UiPrefabGenerator.Editor.asmdef"),
    Path("Assets/Tools/UiPrefabGenerator/Editor/Generation/UiPrefabDraftWriter.cs"),
    Path("Assets/Tools/UiPrefabGenerator/Editor/Generation/SampleUiPipelineRunner.cs"),
    Path("Assets/Tools/UiPrefabGenerator/Editor/Analysis/UiPrefabGeneratorAutoAnalysisService.cs"),
    Path("Assets/Tools/UiPrefabGenerator/Editor/Analysis/UiPrefabGeneratorAnalysisBatch.cs"),
    Path("Assets/Tools/UiPrefabGenerator/Editor/Bridge/UiPrefabGeneratorAutoAnalysisBridge.cs"),
    Path("Assets/Tools/UiPrefabGenerator/Editor/Validation/UiPrefabGeneratorValidationMenu.cs"),
    Path("Assets/Tools/UiPrefabGenerator/Editor/Validation/PrefabDraftStructureValidation.cs"),
    Path("Assets/Tools/UiPrefabGenerator/Editor/Validation/PrefabBindingManifestValidation.cs"),
    Path("Assets/Tools/UiPrefabGenerator/Tests/UiPrefabGenerator.Tests.asmdef"),
    Path("Assets/Tools/UiPrefabGenerator/Tests/EditMode/UiPrefabGeneratorAutoAnalysisBridgeTests.cs"),
    Path("Assets/Tools/UiPrefabGenerator/Tests/EditMode/UiPrefabGeneratorAutoAnalysisServiceTests.cs"),
    Path("Assets/Tools/UiPrefabGenerator/Tests/EditMode/UiPrefabDraftWriterTests.cs"),
    Path("Assets/Tools/UiPrefabGenerator/Tests/EditMode/PrefabDraftStructureValidatorTests.cs"),
    Path("Assets/Tools/UiPrefabGenerator/Tests/EditMode/SampleUiPipelineRunnerTests.cs"),
    Path("Assets/Tools/UiPrefabGenerator/Documentation~/README.md"),
    Path("Assets/Tools/UiPrefabGenerator/Samples~/Holmas/sample_design_packet.json"),
    Path("Assets/Tools/UiPrefabGenerator/Samples~/Holmas/sample_design_packet_intake_assessment.json"),
    Path("Assets/Tools/UiPrefabGenerator/Samples~/Holmas/sample_prefab_binding_manifest.json"),
    Path("Assets/Tools/UiPrefabGenerator/Samples~/Holmas/sample_holmas_generated_result_plan.json"),
    Path("Assets/Tools/UiPrefabGenerator/Samples~/Holmas/validation_baseline.json"),
    Path("scripts/ui_prefab_generator/run_task_auto_analysis.sh"),
    Path("scripts/ui_prefab_generator/run_ui_prefab_generator_editmode_tests.sh"),
    Path("scripts/ui_prefab_generator/run_sample_ui_pipeline.sh"),
]

META_ROOT = Path("Assets/Tools/UiPrefabGenerator")
HEX_GUID = re.compile(r"^[0-9a-f]{32}$")


def find_invalid_meta_guids():
    invalid = []
    for path in sorted(META_ROOT.rglob("*.meta")):
        guid = None
        for line in path.read_text(encoding="utf-8").splitlines():
            if line.startswith("guid: "):
                guid = line.split(": ", 1)[1].strip()
                break
        if guid is None or not HEX_GUID.fullmatch(guid):
            invalid.append((path, guid))
    return invalid


def main() -> int:
    missing = [path for path in REQUIRED_PATHS if not path.exists()]
    if missing:
        for path in missing:
            print(f"[missing] {path}")
        return 1

    invalid_meta_guids = find_invalid_meta_guids()
    if invalid_meta_guids:
        for path, guid in invalid_meta_guids:
            print(f"[invalid-meta-guid] {path}: {guid}")
        return 1

    print("[ok] ui prefab generator isolated layout is present")
    return 0


if __name__ == "__main__":
    sys.exit(main())
