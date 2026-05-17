# Holmas Tools

`tools/` 是 Holmas `Client` 工程根下的项目工具目录。

当前外层 `<workspace>/Holmas` 只是工作区父目录；Git 仓库根、Unity 工程根和工具命令执行根都是 `<workspace>/Holmas/Client`。这里统一放仓库级工具入口，避免和 Unity 源码目录 `Assets/Scripts`、`Assets/HotUpdateContent/Script` 混淆。

## 目录分工

- `config_export/`
  - 配置表导出相关工具
  - 高频入口：`python3 tools/config_export/export_holmas_config.py`
- `validation/`
  - 边界检查、运行时验证、回归脚本
  - 高频入口：`bash tools/validation/run_holmas_validation.sh`
  - 高频入口：`bash tools/validation/check_boundary.sh`
- `doc_maintenance/`
  - 文档维护、收尾、提交建议
  - 高频入口：`bash tools/doc_maintenance/finalize_task.sh`
  - 高频入口：`python3 tools/doc_maintenance/update_project_docs.py --doc-root doc check-last-finalize`
  - 路径检查：`python3 tools/doc_maintenance/update_project_docs.py --doc-root doc check-portable-paths`
- `repo_maintenance/`
  - 仓库维护与开发环境辅助
  - 高频入口：`bash tools/repo_maintenance/install_git_hooks.sh`
  - 高频入口：`bash tools/repo_maintenance/sync_codex_skills.sh`
- `ui_prefab_generator/`
  - UI 自动生成系统专属工具
- `tests/`
  - 工具链自身测试
  - 高频入口：`python3 -m unittest discover -s tools/tests -p 'test_*.py'`
