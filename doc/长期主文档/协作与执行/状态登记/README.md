# 协作状态登记

本目录存放长期协作流程使用的机器可读状态文件。

## commit_module_sequences.json

`commit_module_sequences.json` 记录 Holmas 八位提交编号中各模块当前已使用到的最新流水号。

用途固定为：

- `tools/doc_maintenance/update_project_docs.py` 生成下一条提交建议时读取它
- `.githooks/post-commit` 在提交成功后根据 Git 历史同步它
- 提交编号校验流程用它避免同一模块重复或跳号

维护规则：

- 这个 JSON 保持纯数据结构，只保留 `version` 和 `modules`
- 不要在 JSON 内添加说明字段；同步脚本会按固定结构重写文件
- 给阅读者看的解释放在本 README，避免影响自动维护流程
- 若手工调整模块流水号，调整前应先确认 Git 历史中的最新编号
