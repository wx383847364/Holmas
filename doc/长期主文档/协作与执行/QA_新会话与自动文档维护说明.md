# QA：新会话如何自动维护文档

## agent最短启动口令

以后你最少只要复制这句，再补一个目标就够了：

```text
目标：……  按长期主文档里的 Agent 启动与验收规范，执行 Agent 1。
目标：…… ，按长期主文档里的 Agent 启动与验收规范，帮我规划一下 应该启动哪几个subagent 来完成任务。
```
## 最短结论

新开会话后，如果你想让 Codex 每次完成一个任务都顺手维护文档，开场直接说这句：

```text
按长期主文档规则执行。每次完成一个任务后都执行文档维护流程。
```

## 如果是人手动执行

每次完成一个任务后，直接运行：

```bash
bash scripts/finalize_task.sh --summary "本轮完成了什么" --done "已完成项" --next "下一步"
```

这条命令会自动：

- 更新最新迭代记录
- 刷新 `项目总览.md` 里的当前阶段入口
- 同步主文档索引
- 同步迭代记录索引
- 暂存 `doc/` 下的文档改动

## 什么时候新建新的迭代记录

最短规则记这三条就够了：

- 同一天同一主线继续推进：继续写 `001`
- 同一天新开大主题：新建 `002`
- 跨天：默认新建一份新的迭代记录

如果要新建，先执行：

```bash
python3 scripts/update_project_docs.py new-iteration --title "本轮标题" --goal "本轮目标"
```

然后再继续用：

```bash
bash scripts/finalize_task.sh --summary "..." --done "..." --next "..."
```

## 如果是 Codex / agent 协作

新开会话时，最好第一句就写：

```text
按长期主文档规则执行。每次完成一个任务后都执行文档维护流程。
```

这样当前会话里，Codex 在每轮任务结束后就会主动做文档收尾。

## 提交前自动兜底

每个同事本地仓库至少执行一次：

```bash
bash scripts/install_git_hooks.sh
```

执行后，提交前会自动检查文档维护是否漏掉。  
如果缺少迭代记录，提交会被拦住。

## 给新同事的最短用法

1. 第一次拉项目后先执行：

```bash
bash scripts/install_git_hooks.sh
```

2. 新开会话第一句写：

```text
按长期主文档规则执行。每次完成一个任务后都执行文档维护流程。
```

3. 如果不是 Codex 自动执行，就自己运行：

```bash
bash scripts/finalize_task.sh --summary "..." --done "..." --next "..."
```

## Assumptions

- 这份文档只保留最短使用方式。
- 正式规则仍以 [文档维护与 Git 提交流程](/Users/bruce/work/Holmas/doc/长期主文档/协作与执行/任务完成后自动维护文档.md) 为准。
