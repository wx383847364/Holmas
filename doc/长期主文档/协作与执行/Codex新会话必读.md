# QA：新会话如何自动进入工作流

这页只负责回答两件事：

- 新会话第一句怎么说
- briefing 和 execution dispatch 怎么切换

这页不重复展开解释：

- Agent 职责、启动边界、验收规则
- 固定三段收尾、Git 提交流程的全部细节

详细规则分别看：

- [Agent 启动与验收规范](/Users/bruce/work/Holmas/doc/长期主文档/协作与执行/Agent 启动与验收规范.md)
- [Agent 启动口令清单](/Users/bruce/work/Holmas/doc/长期主文档/协作与执行/Agent 启动口令清单.md)
- [任务完成后自动维护文档](/Users/bruce/work/Holmas/doc/长期主文档/协作与执行/任务完成后自动维护文档.md)

## 默认入口口令

新开会话后，默认直接说：

```text
按长期主文档规则执行。
```

这句固定表示主线程走完整的三阶段工作流：

- `briefing`
- `execution dispatch`
- `completion finalize`

## 收尾硬门

只要这轮发生了 repo 级改动，最终回复必须同时满足下面两件事：

- 已执行 `finalize_task.sh -> check-last-finalize`
- `final` 消息里显式包含 `文档维护 / Git 提交建议 / 会话建议` 三段内容

固定强调：

- `check-last-finalize` 返回 `[ok]`，只表示“允许进入 final”，不表示“可以省略三段收尾输出”
- 如果最终回复只写实现总结、只说“已完成收尾”，或只说“脚本已经执行”，都视为未完成当前轮收尾
- 发送 final 时，默认应直接复用 `finalize_task.sh` 刚输出的三段内容，而不是临时重新概括一版

## 阶段 A：briefing

默认只做低 token 主线判断。

固定读取：

- [项目总览](/Users/bruce/work/Holmas/doc/长期主文档/项目总览.md)
- [迭代记录索引](/Users/bruce/work/Holmas/doc/迭代记录/迭代记录索引.md)
- 最新迭代记录中的 `当前状态`、`关键结论`、`风险与阻塞`、`下一步`、`给下一轮的人`

默认输出：

- 当前主线
- 当前阻塞
- `1 ~ 3` 个任务建议
- 推荐先做哪个

默认不做：

- 不扫项目代码
- 不通读全量协作文档
- 不直接进入执行

## 阶段 B：execution dispatch

只有你确认开始执行某个任务后，主线程才进入执行调度阶段。

进入后固定按 [Agent 启动与验收规范](/Users/bruce/work/Holmas/doc/长期主文档/协作与执行/Agent 启动与验收规范.md) 判断：

- 主线程直做 / 只启 helper / 启动一个或多个真实 subagent
- 是否进入 Agent 6 阶段里程碑审查闭环
- 是否需要复用已有实例或自动补位

## 阶段 C：completion finalize

只要这轮发生了 repo 级改动，结束前就必须进入收尾阶段。

固定要求：

- 不允许把 `append-iteration + sync` 视为已完成收尾
- 默认必须执行 `bash scripts/finalize_task.sh`
- 只有在明确无法走 shell 收尾时，才允许退回 `update_project_docs.py suggest-handoff`
- 默认完整链固定为：`finalize_task.sh -> check-last-finalize -> final`
- 只有看到了 `文档维护 / Git 提交建议 / 会话建议` 三段输出，才能视为当前轮真正结束
- `final` 消息若没有显式包含这三段，即使脚本已执行、`check-last-finalize` 返回 `[ok]`，也仍视为未完成
- `finalize_task.sh` 完成后，应以 `.git/codex/last_finalize_report.json` 作为最近一次完整收尾状态
- 在发送 final 前，必须通过 `python3 scripts/update_project_docs.py --doc-root doc check-last-finalize`

## 最短执行确认模板

```text
按长期主文档规则执行。
开始执行：……
如果需要，按长期 subagent 编组方案和线程级辅助 subagent 角色自动判断并启动合适的 helper 或真实 subagent。
按阶段里程碑进入 Agent 6 审查闭环，直到通过后再收尾。
```

## 跳过 briefing 的口令

如果任务已经明确，想直接进入执行调度阶段，可以说：

```text
按长期主文档规则执行，默认启动subagent。
```

## 约束提醒

- `按长期主文档规则执行。` 不等于“已经有现成 Agent 实例”
- `按长期主文档规则执行。` 也不等于“新会话一上来就扫代码”
- 如果你显式补充“这轮不要开真实 subagent”或“先只给建议”，该限制优先于默认入口
- `按长期主文档规则执行。` 不只约束开始，也约束结束；任务结束后必须按 [任务完成后自动维护文档](/Users/bruce/work/Holmas/doc/长期主文档/协作与执行/任务完成后自动维护文档.md) 收尾
