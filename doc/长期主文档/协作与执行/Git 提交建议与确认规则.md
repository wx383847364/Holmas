# Git 提交建议与确认规则

这页只负责 Git 提交建议的展示格式、确认词映射，以及 `commit / push` 的执行口径。

这页不重复解释：

- 任务什么时候必须进入收尾
- `finalize_task.sh` 和 `check-last-finalize` 的调用顺序
- final 固定三段输出

对应入口分别看：

- [任务完成后自动维护文档](/Users/bruce/work/Holmas/doc/长期主文档/协作与执行/任务完成后自动维护文档.md)
- [Holmas 八位提交编号方案](/Users/bruce/work/Holmas/doc/长期主文档/方案与数据/Holmas%20八位提交编号方案.md)

## Summary

这份文档固定 Holmas 项目的 Git 提交建议与确认规则。

目标固定为：

- 让 `Git 提交建议` 的对外格式保持一致
- 让确认词和 `commit / push` 的执行语义保持一致
- 避免“文档是新规则，工具提示还是旧规则”的口径漂移

## 适用范围与会话锚点

这份文档负责：

- `Git 提交建议：适合提交 / 暂不建议提交` 的对外口径
- `标题：`、`内容：` 的展示格式
- `提交确认：` 的固定提示词
- 确认词对应的执行语义
- `.git/codex/pending_commit_suggestion.json` 的复用规则
- 模块编号登记文件与提交前编号校验口径

这份文档不负责：

- 收尾触发时机
- `finalize_task.sh`、`check-last-finalize`
- final 里必须显式包含的固定三段

会话锚点规则固定为：

- `@git提交规则` 只对当前会话生效
- 它不是全局默认规则
- 它不替代 [任务完成后自动维护文档](/Users/bruce/work/Holmas/doc/长期主文档/协作与执行/任务完成后自动维护文档.md) 的收尾规范

固定说明：

- 当前没有专门自动提交脚本，`commit / push` 仍由主线程按确认词执行
- 当前模块最新编号登记在 [commit_module_sequences.json](/Users/bruce/work/Holmas/doc/长期主文档/协作与执行/commit_module_sequences.json)
- `Git 提交建议` 生成时会先尽量 fetch 远端基线，再结合 git history 与本地登记文件重算下一个编号
- 真正执行 `git commit` 时，`.githooks/commit-msg` 会再次校验标题编号，并自动更新模块编号登记文件

## 联读硬规则

只要当前会话已经读到本页，并且要输出 `Git 提交建议` 里的 `标题：`，就固定视为：

- 必须继续读取 [Holmas 八位提交编号方案](/Users/bruce/work/Holmas/doc/长期主文档/方案与数据/Holmas%20八位提交编号方案.md)，或执行等价的脚本编号逻辑
- 不允许把 `[TMMSSSSS]`、`[230xxxxx]`、`UI：xxxx` 这类占位写法直接当成最终 `标题：`
- 不允许把“要不要继续看八位编号方案”再次回问给用户

只有下面情况才允许说明阻塞，而不是直接给出真实编号标题：

- 当前目录不在 Git 仓库中
- 当前仓库历史不可读，无法判断该模块下一个编号
- 当前改动边界明显混杂，暂时不能形成单独提交建议

如果当前工作区已有本轮相关改动，且边界足够清晰：

- 主线程应直接结合当前 `git history` 和八位编号方案，给出真实的推荐 `标题：`
- 主线程应直接补完整 `内容：`
- 主线程应直接追加 `提交确认：...`
- 不要先输出占位标题，再让用户补“按八位编号方案改一下”

## Git 提交建议输出格式

只要当前改动已经形成边界清晰、验证完成、适合独立提交的里程碑，就写：

```text
Git 提交建议：适合提交
标题：xxxx
内容：
- xxxx
- xxxx
提交确认：如需我继续执行，请回复 1（提交并推送） / 2（只提交） / 确认 / 提交 / 直接提交（只提交）。
```

如果当前改动仍混有多条主线、验证没跑完、审查没完成，或只是事务性协助，就写：

```text
Git 提交建议：暂不建议提交。原因是：xxxx
提交确认：当前不建议直接提交；如需强制提交，请明确说明。
```

固定补充规则：

- 即使这轮只是事务性协助，也必须给出 `Git 提交建议`
- `内容` 统一放进一个 `text` 代码块
- 如果当前规则和旧文档、旧提示词冲突，统一以这份文档和最新工具提示为准
- 如果当前工作区已有 repo 改动，且这轮又要求按本页给提交建议，默认就要继续补到真实编号标题，不要停在占位写法

## 标题与编号来源

`标题：` 和 `内容：` 默认由 `tools/doc_maintenance/finalize_task.sh` / `tools/doc_maintenance/update_project_docs.py` 生成。

固定要求：

- 主线程可以人工改写脚本生成的 `标题` 与 `内容`
- 但不能省略 `标题：`、`内容：`、`提交确认：`
- 提交标题继续沿用正式八位编号方案
- 读到本页后，只要需要给出 `标题：`，就必须继续联读八位编号方案，或复用脚本已经算出的真实编号结果

这里仅保留最小规则：

- 标题格式固定为 `[TMMSSSSS] 前缀：摘要`
- 只有走这套正式提交流程的编号标题才视为占号
- merge 和旧的无编号提交不占号
- `[TMMSSSSS]` 只是格式说明，不是允许直接对外输出的占位模板

完整模块表与高频速查统一看 [Holmas 八位提交编号方案](/Users/bruce/work/Holmas/doc/长期主文档/方案与数据/Holmas%20八位提交编号方案.md)。

## 确认词映射与执行口径

确认词固定映射为：

- `1`
  - 表示：提交并推送
- `2`
  - 表示：只提交
- `确认`
  - 表示：只提交
- `提交`
  - 表示：只提交
- `直接提交`
  - 表示：只提交

执行语义固定为：

1. `只提交`
- 优先复用 `.git/codex/pending_commit_suggestion.json` 中最近一次缓存的 `标题` 与 `内容`
- 使用该缓存执行本地 `git commit`

2. `提交并推送`
- 先按同样方式执行本地 `git commit`
- 再推送当前分支的上游远端

默认不写死：

- 不把 `main -> origin/main` 写成长期固定规则
- 长期口径统一写成“当前分支的上游远端”

## 缓存复用与执行前检查

最近一次“尚未提交、但已给出的提交建议”缓存到：

```text
.git/codex/pending_commit_suggestion.json
```

主线程确认执行前固定检查：

- 当前工作区里是否存在与本轮无关、且不应混入本次提交的脏改
- 当前暂存区或待暂存文件是否能清晰界定为“本轮改动”
- 缓存里的 `head_commit` 是否仍然对应当前仓库基线

如果存在下面任一情况，应暂停而不是盲目提交：

- 工作区里存在明显无关改动且无法安全排除
- 缓存已失效或不可用
- 当前 HEAD 已变化，无法确认缓存标题是否仍对应当前基线

相关辅助入口：

- `python3 tools/doc_maintenance/update_project_docs.py --doc-root doc show-pending-commit`
  - 只负责显示最近一次缓存的提交建议
  - 不负责自动消费确认词
- `python3 tools/doc_maintenance/update_project_docs.py --doc-root doc sync-commit-sequences --fetch`
  - 根据最新 git history 与远端基线刷新模块编号登记文件
- `python3 tools/doc_maintenance/update_project_docs.py --doc-root doc validate-commit-message --message-file <path>`
  - 按最新基线校验当前提交标题是否仍是该模块的正确下一个编号

## 与收尾规范的边界

固定分工如下：

- 任务何时必须进入收尾
  - 看 [任务完成后自动维护文档](/Users/bruce/work/Holmas/doc/长期主文档/协作与执行/任务完成后自动维护文档.md)
- final 里必须包含哪三段
  - 看 [任务完成后自动维护文档](/Users/bruce/work/Holmas/doc/长期主文档/协作与执行/任务完成后自动维护文档.md)
- Git 提交建议的格式、确认词、`commit / push` 口径
  - 看本页
- 八位编号和模块表
  - 看 [Holmas 八位提交编号方案](/Users/bruce/work/Holmas/doc/长期主文档/方案与数据/Holmas%20八位提交编号方案.md)
