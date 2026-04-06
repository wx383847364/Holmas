# ui_prefab_generator scripts

这个目录只放 `UiPrefabGenerator` 系统自己的脚本入口。  
当前提供：

- `check_ui_generator_layout.py`
- `run_ui_prefab_generator_editmode_tests.sh`
- `run_sample_ui_pipeline.sh`
- `UiPrefabGeneratorValidationMenu` batch entry in the editor package

spec、manifest 和 regression 的正式校验逻辑当前放在 `Assets/Tools/UiPrefabGenerator/Editor/Validation` 与 `Tests`。
