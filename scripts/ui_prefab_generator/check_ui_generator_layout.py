#!/usr/bin/env python3
from pathlib import Path
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
    Path("Assets/Tools/UiPrefabGenerator/Editor/Validation/PrefabBindingManifestValidation.cs"),
    Path("Assets/Tools/UiPrefabGenerator/Tests/UiPrefabGenerator.Tests.asmdef"),
    Path("Assets/Tools/UiPrefabGenerator/Documentation~/README.md"),
    Path("Assets/Tools/UiPrefabGenerator/Samples~/Holmas/sample_design_packet.json"),
    Path("Assets/Tools/UiPrefabGenerator/Samples~/Holmas/sample_design_packet_intake_assessment.json"),
    Path("Assets/Tools/UiPrefabGenerator/Samples~/Holmas/sample_prefab_binding_manifest.json"),
    Path("Assets/Tools/UiPrefabGenerator/Samples~/Holmas/sample_holmas_generated_result_plan.json"),
    Path("Assets/Tools/UiPrefabGenerator/Samples~/Holmas/validation_baseline.json"),
]


def main() -> int:
    missing = [path for path in REQUIRED_PATHS if not path.exists()]
    if missing:
        for path in missing:
            print(f"[missing] {path}")
        return 1

    print("[ok] ui prefab generator isolated layout is present")
    return 0


if __name__ == "__main__":
    sys.exit(main())
