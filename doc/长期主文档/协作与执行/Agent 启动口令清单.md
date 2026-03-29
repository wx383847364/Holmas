# Agent 启动口令清单

## Summary

这份清单只保留最常用、可直接复制的启动口令。  
如果你不想每次都自己组织语言，就直接从这里复制一句发给我。

详细规则看：

- [Agent 启动与验收规范](/Users/bruce/work/Holmas/doc/长期主文档/协作与执行/Agent 启动与验收规范.md)

## 通用模板

```text
按长期主文档里的 Agent 启动与验收规范，执行 Agent X。
目标：……
是否启动真实子 Agent：是 / 否。
完成后按验收规范汇报，并执行文档维护流程。
```

## Agent 1

```text
按长期主文档里的 Agent 启动与验收规范，执行 Agent 1。
目标：冻结 Shared DTO，补 HotUpdate 骨架。
这次不要启动其他 agent。
完成后按验收规范自检，并执行文档维护流程。
```

## Agent 2

```text
按长期主文档里的 Agent 启动与验收规范，执行 Agent 2。
目标：实现 terrain -> BoardTemplate -> LevelSnapshot，只做地图与棋盘第一阶段。
不要修改 App.Shared，不要碰 UI。
完成后按验收规范汇报，并执行文档维护流程。
```

## Agent 3

```text
按长期主文档里的 Agent 启动与验收规范，执行 Agent 3。
目标：实现任务栏、任务抽取、奖励计算和任务推进第一阶段。
不要修改 App.Shared，不要碰 HotUpdate 入口，不要碰 UI。
完成后按验收规范汇报，并执行文档维护流程。
```

## Agent 4

```text
按长期主文档里的 Agent 启动与验收规范，执行 Agent 4。
目标：接任务栏、领奖、结算和主界面第一阶段。
这次不要改 Shared，不要写奖励公式和棋盘底层逻辑。
完成后按验收规范汇报，并执行文档维护流程。
```

## 启动真实子 Agent

如果你要我真的去开一个子 Agent，而不是让我自己按某个 Agent 规范做事，就把这句写清楚：

```text
按长期主文档里的 Agent 启动与验收规范，启动 Agent 2 真实子 Agent。
目标：……
完成后把修改文件、输入输出、风险和阻塞汇报给我，并执行文档维护流程。
```

## 顺序控制口令

如果你不想一次开多个 Agent，可以直接这样说：

```text
先按规范执行 Agent 2。
等 Agent 2 通过验收后，再决定是否启动 Agent 3。
这轮先不要启动 UI 线。
```

## Assumptions

- 默认“执行 Agent X”表示让我按该 Agent 规范做事，不代表自动创建真实子 Agent。
- 只有你明确说“启动真实子 Agent”或“启动子代理”，我才会真的开子 Agent。
- 如果你没有特别说明，默认我会按长期主文档中的 Agent 规范和验收规则执行。
