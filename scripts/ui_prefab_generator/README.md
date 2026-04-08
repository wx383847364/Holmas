# ui_prefab_generator scripts

这个目录只放 `UiPrefabGenerator` 系统自己的脚本入口。  
当前提供：

- `check_ui_generator_layout.py`
- `run_task_auto_analysis.sh`
- `run_ui_prefab_generator_editmode_tests.sh`
- `run_sample_ui_pipeline.sh`
- `UiPrefabGeneratorValidationMenu` batch entry in the editor package

`run_task_auto_analysis.sh` 用于把单个 task 的 `request.json` 送入临时工程 batchmode 自动分析，并回写 `design_packet.json`、`ui_prefab_spec.json`、`resource_match_report.json`、`analysis_result.json` 和 `analysis_summary.md`。

spec、manifest、analysis artifacts 和 regression 的正式校验逻辑当前放在 `Assets/Tools/UiPrefabGenerator/Editor/Validation` 与 `Tests`。
